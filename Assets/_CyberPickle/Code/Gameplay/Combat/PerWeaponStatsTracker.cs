// File: Assets/_CyberPickle/Code/Gameplay/Combat/PerWeaponStatsTracker.cs
// Namespace: CyberPickle.Gameplay.Combat
//
// Manager<T> singleton that accumulates per-weapon damage statistics
// across the run. Fed by DamageReportDrainSystem (which dequeues
// DamageHitReport records emitted by ProjectileCollisionSystem) and
// consumed by Day-3 hover-tooltips.
//
// Per-weapon stats tracked:
//   - Cumulative damage (total this run)
//   - Kill count (this run)
//   - Hit count
//   - Crit count
//   - Rolling DPS (last N seconds, configurable window)
//
// Why a separate tracker instead of stuffing it into RunStatsTracker:
// run-level metrics (timer, total kills) are conceptually different
// from per-weapon metrics. Mixing them complicates both. RunStatsTracker
// stays simple; this owns weapon-specific data.
//
// Scene-bound: per-run state, dies with the Game scene. Resets on RunStart
// for the same reasons LevelUpCoordinator does — explicit reset removes
// dependence on scene-recreation lifecycle.

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using CyberPickle.Core.Management;
using CyberPickle.DOTS.Components;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.RunState;

namespace CyberPickle.Gameplay.Combat
{
    /// <summary>
    /// Per-run accumulated statistics for a single weapon. UI hover-tooltips
    /// read these values directly. All counters reset to zero at RunStart.
    /// </summary>
    public class WeaponRunStats
    {
        public string WeaponId;

        // Cumulative across the run.
        public float TotalDamageDealt;
        public int   TotalHits;
        public int   TotalCrits;
        public int   TotalKills;

        // Rolling-window DPS calculation. Stored as paired
        // damage values + timestamps; old samples evicted in Update.
        // Capacity bounded so a runaway weapon can't grow this unboundedly.
        internal readonly Queue<(float time, float damage)> rollingSamples
            = new Queue<(float, float)>(capacity: 256);

        /// <summary>Damage / second over the rolling window (default 5s).</summary>
        public float RollingDps;

        /// <summary>Crit rate as a 0..1 fraction. Returns 0 if no hits yet.</summary>
        public float CritRate => TotalHits > 0 ? (float)TotalCrits / TotalHits : 0f;

        /// <summary>Average damage per hit. Useful for the tooltip's headline number.</summary>
        public float AverageDamagePerHit => TotalHits > 0 ? TotalDamageDealt / TotalHits : 0f;

        public void Reset()
        {
            TotalDamageDealt = 0f;
            TotalHits = 0;
            TotalCrits = 0;
            TotalKills = 0;
            RollingSamplesClear();
            RollingDps = 0f;
        }

        internal void RollingSamplesClear()
        {
            rollingSamples.Clear();
        }
    }

    [DisallowMultipleComponent]
    public class PerWeaponStatsTracker : Manager<PerWeaponStatsTracker>
    {
        // Scene-bound: per-run data dies with the Game scene.
        protected override bool PersistAcrossScenes => false;

        [Header("Rolling DPS Window")]
        [Tooltip("Seconds of history used to compute rolling DPS. Default 5s — long enough to smooth burst weapons, short enough to feel live.")]
        [Min(1f)] [SerializeField] private float dpsWindowSeconds = 5f;

        [Header("Diagnostics")]
        [Tooltip("Log every damage report received. Off by default — fires per hit.")]
        [SerializeField] private bool verbose;

        // Per-weapon stats keyed by WeaponId. Allocated lazily per weapon
        // on first hit, so unused weapons don't bloat the dictionary.
        private readonly Dictionary<string, WeaponRunStats> _stats = new Dictionary<string, WeaponRunStats>();

        // ─── Manager lifecycle ────────────────────────────────────────────

        protected override void OnManagerEnabled()
        {
            base.OnManagerEnabled();
            MusicEventBus.OnEvent += HandleMusicEvent;
        }

        protected override void OnManagerDisabled()
        {
            base.OnManagerDisabled();
            MusicEventBus.OnEvent -= HandleMusicEvent;
        }

        private void HandleMusicEvent(MusicEvent type, object payload)
        {
            if (type == MusicEvent.RunStart) ResetAll();
        }

        private void ResetAll()
        {
            foreach (var kv in _stats) kv.Value.Reset();
            // Don't clear the dictionary itself — keep allocated WeaponRunStats
            // so subsequent runs reuse them without re-allocating. A re-run
            // touching the same weapons is the common case.
            if (verbose) Debug.Log("[PerWeaponStatsTracker] All weapon stats reset.");
        }

        // ─── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Called by DamageReportDrainSystem for each hit dequeued from the
        /// ECS-side report queue. Aggregates into the per-weapon stats.
        /// </summary>
        public void RecordHit(DamageHitReport report)
        {
            string weaponId = report.WeaponId.ToString();
            if (string.IsNullOrEmpty(weaponId)) weaponId = "unknown";

            if (!_stats.TryGetValue(weaponId, out var s))
            {
                s = new WeaponRunStats { WeaponId = weaponId };
                _stats[weaponId] = s;
            }

            s.TotalDamageDealt += report.DamageDealt;
            s.TotalHits++;
            if (report.IsCrit)      s.TotalCrits++;
            if (report.KilledTarget) s.TotalKills++;

            // Push into rolling-DPS sample queue.
            float now = RunStateManager.Instance != null ? RunStateManager.Instance.RunTime : Time.time;
            s.rollingSamples.Enqueue((now, report.DamageDealt));

            if (verbose)
                Debug.Log($"[PerWeaponStatsTracker] {weaponId}: +{report.DamageDealt:F1} dmg" +
                          $"{(report.IsCrit ? " (CRIT)" : "")}{(report.KilledTarget ? " (KILL)" : "")} " +
                          $"| total {s.TotalDamageDealt:F0} / {s.TotalHits} hits / {s.TotalKills} kills");
        }

        /// <summary>
        /// Read-only access to a weapon's current run stats. Returns null
        /// if no hits from that weapon have been recorded yet.
        /// </summary>
        public WeaponRunStats GetStats(string weaponId)
        {
            return _stats.TryGetValue(weaponId, out var s) ? s : null;
        }

        /// <summary>Read-only enumeration of every weapon that has dealt at least one hit.</summary>
        public IEnumerable<WeaponRunStats> AllWeapons => _stats.Values;

        // ─── Rolling-DPS update ───────────────────────────────────────────

        private void Update()
        {
            // Recompute rolling DPS for every tracked weapon. O(weapons × samples-in-window),
            // bounded by dpsWindowSeconds × max-fire-rate. For 8 weapons firing 30/sec
            // over 5s window, that's 1200 samples — trivial.
            float now = RunStateManager.Instance != null ? RunStateManager.Instance.RunTime : Time.time;
            float cutoff = now - dpsWindowSeconds;

            foreach (var s in _stats.Values)
            {
                // Evict samples older than the window.
                while (s.rollingSamples.Count > 0 && s.rollingSamples.Peek().time < cutoff)
                    s.rollingSamples.Dequeue();

                // Sum remaining damage and divide by window.
                float total = 0f;
                foreach (var sample in s.rollingSamples) total += sample.damage;
                s.RollingDps = total / dpsWindowSeconds;
            }
        }
    }
}
