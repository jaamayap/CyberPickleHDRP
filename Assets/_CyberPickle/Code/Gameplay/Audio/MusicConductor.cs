// File: Assets/_CyberPickle/Code/Gameplay/Audio/MusicConductor.cs
// Namespace: CyberPickle.Gameplay.Audio
//
// Single source of musical timing for the run. Owns the master tempo and
// emits OnBeat / OnBar / OnSubdivision events that gameplay systems
// subscribe to for beat-quantized firing, UI pulsing, particle sync, etc.
//
// Stage 0 (today): naive Time.time-based clock. Sufficient for visual
// pulsing, weapon-firing intent buffering, and validating the architecture.
// Frame-rate dips can drop subdivisions if a frame >1 grid unit; fine for
// stub work, not for shipping.
//
// Stage 2 (M9 Wwise): replace the Update loop with Wwise music callbacks
// (AK_MusicSyncBeat / AK_MusicSyncGrid). Sample-accurate to the audio
// thread. Public API stays identical — subscribers don't need to know
// which clock is driving the events.
//
// Scene-bound: belongs in Game.unity. Other scenes don't need it; menus
// don't have a tempo concept yet (might add later for menu-music sync).

using System;
using UnityEngine;
using CyberPickle.Core.Management;
using CyberPickle.Gameplay.RunState;

namespace CyberPickle.Gameplay.Audio
{
    [DisallowMultipleComponent]
    public class MusicConductor : Manager<MusicConductor>
    {
        // Scene-bound: dies with the Game scene. Same lifecycle as
        // RunStateManager / RunStatsTracker.
        protected override bool PersistAcrossScenes => false;

        [Header("Tempo")]
        [Tooltip("Master tempo in beats per minute. Default 128 — French-electro / cyberpunk standard. See GDD §3.5.2.")]
        [SerializeField, Range(60f, 200f)] private float bpm = 128f;

        [Tooltip("Subdivisions per beat. 4 = 16th-note grid (fast weapons), 2 = 8th-note grid (medium), 1 = quarter (slow). Most weapons will quantize to 16ths.")]
        [SerializeField, Range(1, 8)] private int subdivisionsPerBeat = 4;

        [Header("Diagnostics")]
        [Tooltip("Log each beat / bar to the console. Off by default — chatty.")]
        [SerializeField] private bool verbose;

        [Tooltip("Mirror MusicEventBus.VerboseLogging — when ON, every Fire() call logs (RunStart, WeaponFire, EnemyDeath, PlayerHit, etc.). Useful for confirming producers are wired. WARNING: WeaponFire alone can spam 30+/sec in combat. Toggle off when not actively diagnosing.")]
        [SerializeField] private bool verboseEventBus;

        // ─── Public state ─────────────────────────────────────────────────

        public float BPM => bpm;
        public int   SubdivisionsPerBeat => subdivisionsPerBeat;

        /// <summary>Seconds per beat at the current BPM. Useful for tweens / wait times that need to match the grid.</summary>
        public float SecondsPerBeat => 60f / bpm;

        /// <summary>Seconds per subdivision. shortest grid unit.</summary>
        public float SecondsPerSubdivision => 60f / (bpm * subdivisionsPerBeat);

        /// <summary>Number of beats fired since the most recent RunStart. 0 before the first beat fires.</summary>
        public int CurrentBeat { get; private set; }

        /// <summary>Number of subdivisions fired since the most recent RunStart.</summary>
        public int CurrentSubdivision { get; private set; }

        /// <summary>Bar number (4 beats per bar in 4/4). 0-indexed.</summary>
        public int CurrentBar => CurrentBeat / 4;

        // ─── Public events ────────────────────────────────────────────────

        /// <summary>Fires every beat. Subscribe for quarter-note locked behavior (kicks, slow weapons).</summary>
        public event Action OnBeat;

        /// <summary>Fires every bar (every 4 beats in 4/4). Subscribe for phrase-level events (transitions, big swells).</summary>
        public event Action OnBar;

        /// <summary>Fires every subdivision (16th note by default). Highest-resolution grid for fast weapons + UI sync.</summary>
        public event Action OnSubdivision;

        // ─── Internal clock state ─────────────────────────────────────────

        private float _runStartTime;
        private int _lastSubdivisionFired = -1;
        private int _lastBeatFired = -1;
        private int _lastBarFired = -1;

        // ─── Manager lifecycle ────────────────────────────────────────────

        protected override void OnManagerEnabled()
        {
            base.OnManagerEnabled();
            // Apply inspector toggle to the static bus flag. Editor-friendly:
            // the user can flip the bool in the inspector mid-Play and changes
            // take effect immediately via OnValidate (below).
            MusicEventBus.VerboseLogging = verboseEventBus;
            // Subscribe to the bus for RunStart so we can reset our clock to
            // align beat 0 with the moment combat begins.
            MusicEventBus.OnEvent += HandleMusicEvent;

            // Also align with RunStateManager directly: TransitionTo(Running)
            // is the run's actual start, regardless of who fires it. Belt and
            // suspenders — the bus handles producer-driven RunStart while the
            // direct subscription handles RunStateManager initialization.
            if (RunStateManager.Instance != null)
            {
                RunStateManager.Instance.OnPhaseChanged += HandleRunPhaseChanged;
                if (RunStateManager.Instance.IsRunning) ResetClock();
            }
        }

        protected override void OnManagerDisabled()
        {
            base.OnManagerDisabled();
            MusicEventBus.OnEvent -= HandleMusicEvent;
            if (RunStateManager.Instance != null)
                RunStateManager.Instance.OnPhaseChanged -= HandleRunPhaseChanged;
        }

        // ─── Event handlers ───────────────────────────────────────────────

        private void HandleMusicEvent(MusicEvent type, object _)
        {
            if (type == MusicEvent.RunStart) ResetClock();
        }

        private void HandleRunPhaseChanged(RunStatePhase phase)
        {
            // Reset on the run's first entry to Running; PRESERVE the clock
            // through LevelUpPaused / Paused so the music keeps phase across
            // the level-up screen (a card pick that lands "on the beat" must
            // pick the same beat that was about to play).
            if (phase == RunStatePhase.Running && CurrentSubdivision == 0)
            {
                ResetClock();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Apply inspector tweaks live during Play so the user can toggle
            // verbose logging mid-session without restarting.
            MusicEventBus.VerboseLogging = verboseEventBus;
        }
#endif

        private void ResetClock()
        {
            _runStartTime = Time.time;
            _lastSubdivisionFired = -1;
            _lastBeatFired = -1;
            _lastBarFired = -1;
            CurrentSubdivision = 0;
            CurrentBeat = 0;
            if (verbose) Debug.Log($"[MusicConductor] Clock reset. BPM={bpm}, subdivisions/beat={subdivisionsPerBeat}.");
        }

        // ─── Frame loop ───────────────────────────────────────────────────

        private void Update()
        {
            // Don't tick during paused phases — Time.time still advances but
            // the run is effectively frozen and music should pause with it.
            // (Stage 2 with Wwise will tick from the audio callback regardless,
            // since Wwise has its own clock; revisit then.)
            var rsm = RunStateManager.Instance;
            if (rsm != null && !rsm.IsRunning) return;

            float elapsed = Time.time - _runStartTime;
            int subdivision = Mathf.FloorToInt(elapsed / SecondsPerSubdivision);

            // Catch up across multiple subdivisions if a frame was long
            // enough to skip one. We fire each missed event once so weapon
            // patterns don't desync from the grid on hitches.
            while (_lastSubdivisionFired < subdivision)
            {
                _lastSubdivisionFired++;
                CurrentSubdivision = _lastSubdivisionFired;
                OnSubdivision?.Invoke();

                int beat = CurrentSubdivision / subdivisionsPerBeat;
                if (beat != _lastBeatFired)
                {
                    _lastBeatFired = beat;
                    CurrentBeat = beat;
                    if (verbose) Debug.Log($"[MusicConductor] Beat {beat}");
                    OnBeat?.Invoke();

                    int bar = beat / 4;
                    if (bar != _lastBarFired)
                    {
                        _lastBarFired = bar;
                        if (verbose) Debug.Log($"[MusicConductor] Bar {bar}");
                        OnBar?.Invoke();
                    }
                }
            }
        }

        // ─── Quantization helper (Day 1 baseline; extended in M7.3) ─────

        /// <summary>
        /// Returns the seconds-from-now of the next subdivision boundary.
        /// Caller can use this to delay firing until the grid lands. Stage 0
        /// implementation; Stage 2 routes through Wwise's tempo timeline.
        /// </summary>
        public float TimeUntilNextSubdivision()
        {
            float elapsed = Time.time - _runStartTime;
            float secs = SecondsPerSubdivision;
            float intoCurrent = elapsed % secs;
            return secs - intoCurrent;
        }
    }
}
