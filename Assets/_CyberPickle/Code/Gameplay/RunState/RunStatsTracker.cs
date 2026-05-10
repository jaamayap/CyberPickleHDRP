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
using CyberPickle.Gameplay.Audio;
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

        protected override void OnManagerEnabled()
        {
            base.OnManagerEnabled();
            // 2026-05-10: switched from RunStateManager.OnPhaseChanged to
            // MusicEventBus.RunStart. The old hook reset on EVERY transition
            // to Running phase — including LevelUpPaused→Running, which
            // wiped kills every time the player picked a card. RunStart only
            // fires on Loading→Running and GameOver→Running (the actual
            // fresh-run starts), which is the right semantics. Same pattern
            // PerWeaponStatsTracker uses.
            MusicEventBus.OnEvent += HandleMusicEvent;
        }

        protected override void OnManagerDisabled()
        {
            base.OnManagerDisabled();
            MusicEventBus.OnEvent -= HandleMusicEvent;
        }

        private void HandleMusicEvent(MusicEvent type, object payload)
        {
            if (type == MusicEvent.RunStart) Reset();
        }
    }
}
