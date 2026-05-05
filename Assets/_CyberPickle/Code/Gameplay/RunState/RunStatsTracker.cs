// File: Assets/_CyberPickle/Code/Gameplay/RunState/RunStatsTracker.cs
// Namespace: CyberPickle.Gameplay.RunState
//
// Per-run metrics accumulator. Listens for gameplay events and exposes
// the totals to the results screen. Resets to zero each time a fresh
// run starts (RunStateManager transitions to Running from Loading or
// GameOver).
//
// Currently tracks:
//   - TimeSurvived (read from RunStateManager.RunTime at GameOver)
//   - EnemiesKilled (incremented from EnemyDeathSystem on each death)
//   - LevelReached (queried from PlayerXP singleton at GameOver)
//
// Gem-XP totals deferred — adding requires a clean per-collection event
// from XPMagnetSystem (Burst/ECS), which we'll wire when XP-collected
// becomes worth showing in the results screen.

using UnityEngine;
using CyberPickle.Core.Management;
using Unity.Entities;
using CyberPickle.DOTS.Components;

namespace CyberPickle.Gameplay.RunState
{
    [DisallowMultipleComponent]
    public class RunStatsTracker : Manager<RunStatsTracker>
    {
        // Scene-bound: per-run metrics. Counters reset on every fresh run via
        // OnPhaseChanged anyway, but pairing this with RunStateManager (also
        // scene-bound) keeps the lifecycle symmetric and avoids "duplicate"
        // warnings on Try Again retries.
        protected override bool PersistAcrossScenes => false;

        // ─── Tracked metrics ──────────────────────────────────────────────

        public int   EnemiesKilled { get; private set; }

        /// <summary>
        /// Time alive in seconds. Read-through to RunStateManager.RunTime
        /// rather than maintained separately to avoid double-counting.
        /// </summary>
        public float TimeSurvived => RunStateManager.Instance != null
            ? RunStateManager.Instance.RunTime
            : 0f;

        /// <summary>
        /// Player level reached. Read live from the PlayerXP ECS singleton
        /// when queried. Returns 1 if the singleton doesn't exist yet
        /// (early-run / no-player-spawn scenarios).
        /// </summary>
        public int LevelReached
        {
            get
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null) return 1;

                using var query = world.EntityManager.CreateEntityQuery(typeof(PlayerXP));
                if (query.CalculateEntityCount() == 0) return 1;
                return query.GetSingleton<PlayerXP>().CurrentLevel;
            }
        }

        // ─── Public API ───────────────────────────────────────────────────

        /// <summary>Called by EnemyDeathSystem when an enemy dies. Increments the kill counter.</summary>
        public void RecordEnemyKilled() => EnemiesKilled++;

        /// <summary>Reset all counters to zero. Called at run start (RunStateManager → Running).</summary>
        public void Reset()
        {
            EnemiesKilled = 0;
            // TimeSurvived auto-resets via RunStateManager.RunTime.
            // LevelReached auto-reads from PlayerXP each time.
        }

        // ─── Lifecycle ────────────────────────────────────────────────────

        protected override void OnManagerAwake()
        {
            base.OnManagerAwake();
            if (RunStateManager.Instance != null)
                RunStateManager.Instance.OnPhaseChanged += HandlePhaseChanged;
        }

        protected override void OnManagerDestroyed()
        {
            base.OnManagerDestroyed();
            if (RunStateManager.Instance != null)
                RunStateManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        }

        private void HandlePhaseChanged(RunStatePhase phase)
        {
            // Reset counters on every fresh Running transition (i.e., new run begins).
            // RunStateManager already handles RunTime reset; we follow suit for kills.
            if (phase == RunStatePhase.Running)
            {
                Reset();
            }
        }
    }
}
