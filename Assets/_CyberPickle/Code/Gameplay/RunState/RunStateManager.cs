// File: Assets/_CyberPickle/Code/Gameplay/RunState/RunStateManager.cs
// Namespace: CyberPickle.Gameplay.RunState
//
// Singleton state machine for the active run. Owns:
//   - CurrentPhase (Loading / Running / LevelUpPaused / Paused / GameOver)
//   - RunTime (seconds since transition to Running)
//   - Time.timeScale management — paused phases set to 0, Running to 1
//
// Pause discipline:
//   When CurrentPhase != Running, Time.timeScale = 0. Most gameplay systems
//   read SystemAPI.Time.DeltaTime / Time.deltaTime, see 0, and effectively
//   no-op. UI animations that need to play during pause use unscaledDeltaTime.
//
// Lifecycle:
//   GameSceneBootstrap → TransitionTo(Loading) at start, TransitionTo(Running)
//   after the player is spawned + initialized.
//   PlayerHealth.OnPlayerDied → TransitionTo(GameOver).
//   Future M7.3 LevelUpScreen → TransitionTo(LevelUpPaused) on level-up
//   event, TransitionTo(Running) when card is picked.
//
// One transition method, one event. Every system observes via OnPhaseChanged
// or polls CurrentPhase — there are no parallel "is paused" flags.

using System;
using UnityEngine;
using CyberPickle.Core.Management;

namespace CyberPickle.Gameplay.RunState
{
    [DisallowMultipleComponent]
    public class RunStateManager : Manager<RunStateManager>
    {
        [Header("Diagnostics")]
        [Tooltip("Log each phase transition to the console.")]
        public bool verbose = true;

        // Scene-bound: this manager only exists during a run. Subscribers
        // (RunStatsTracker, ResultsScreenController, future HUDs) all live in
        // the Game scene and bind via OnEnable. Persisting across scenes would
        // produce "duplicate" warnings on Try Again retries and leak event
        // subscriptions from the dead first-run instance.
        protected override bool PersistAcrossScenes => false;

        // ─── State ─────────────────────────────────────────────────────────

        /// <summary>Current run phase. Defaults to Loading at scene start until something explicitly transitions.</summary>
        public RunStatePhase CurrentPhase { get; private set; } = RunStatePhase.Loading;

        /// <summary>True iff CurrentPhase == Running. Convenience for systems that just need "should I tick?".</summary>
        public bool IsRunning => CurrentPhase == RunStatePhase.Running;

        /// <summary>Seconds elapsed since the most recent transition to Running. Reset on every fresh Running transition (Loading->Running, GameOver->Running).</summary>
        public float RunTime { get; private set; }

        // ─── Events ───────────────────────────────────────────────────────

        /// <summary>Fires after CurrentPhase changes. Argument is the NEW phase.</summary>
        public event Action<RunStatePhase> OnPhaseChanged;

        // ─── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Transition to a new phase. No-op if already in that phase.
        /// Sets Time.timeScale appropriately. Resets RunTime when entering
        /// Running fresh (i.e., from Loading or GameOver — not from
        /// LevelUpPaused/Paused, which preserve the run timer).
        /// </summary>
        public void TransitionTo(RunStatePhase phase)
        {
            if (phase == CurrentPhase) return;

            var previous = CurrentPhase;
            CurrentPhase = phase;

            // Reset RunTime when entering Running from a non-paused phase.
            // LevelUpPaused / Paused → Running keeps the timer.
            if (phase == RunStatePhase.Running &&
                (previous == RunStatePhase.Loading || previous == RunStatePhase.GameOver))
            {
                RunTime = 0f;
            }

            // Manage Time.timeScale based on the new phase.
            Time.timeScale = phase == RunStatePhase.Running ? 1f : 0f;

            if (verbose)
            {
                Debug.Log($"<color=yellow>[RunStateManager]</color> {previous} → {phase} (timeScale {Time.timeScale}, RunTime {RunTime:F1}s)");
            }

            OnPhaseChanged?.Invoke(phase);
        }

        // ─── Lifecycle ────────────────────────────────────────────────────

        private void Update()
        {
            // Tick RunTime only during Running. Uses unscaled delta because
            // Time.deltaTime is 0 during pause (which is why we can't use
            // it here — but we also wouldn't WANT to during pause).
            if (CurrentPhase == RunStatePhase.Running)
            {
                RunTime += Time.unscaledDeltaTime;
            }
        }

        protected override void OnManagerDestroyed()
        {
            base.OnManagerDestroyed();
            // Restore time scale so the next scene doesn't load paused.
            Time.timeScale = 1f;
        }
    }
}
