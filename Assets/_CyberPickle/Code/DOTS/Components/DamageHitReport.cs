// File: Assets/_CyberPickle/Code/DOTS/Components/DamageHitReport.cs
// Namespace: CyberPickle.DOTS.Components
//
// Single-hit report emitted by ProjectileCollisionSystem (Burst) and
// drained by DamageReportDrainSystem (managed). The managed side fans
// it out to:
//   - PerWeaponStatsTracker (analytics — per-weapon DPS/kills/hits)
//   - HitVfxApplier (M9 PR F — Mono-side element-tinted hit visual)
//
// All fields are blittable so the struct is Burst-safe and can sit in a
// NativeQueue.
//
// One report = one hit on one enemy. If a weapon hits 5 enemies in a
// frame (multi-shot, AOE), 5 reports get enqueued.

using Unity.Collections;
using Unity.Mathematics;

namespace CyberPickle.DOTS.Components
{
    public struct DamageHitReport
    {
        /// <summary>Source weapon's id (matches ProjectileSource.WeaponId).</summary>
        public FixedString64Bytes WeaponId;

        /// <summary>Damage actually applied to the target (post-Power, post-crit, pre-Defense).</summary>
        public float DamageDealt;

        /// <summary>True if the crit roll succeeded for this hit.</summary>
        public bool IsCrit;

        /// <summary>True if this hit reduced the target's HP to zero or below.</summary>
        public bool KilledTarget;

        // ─── M9 PR F: Mono-side hit VFX payload ──────────────────────────

        /// <summary>World position of the projectile at collision. The Mono-side HitVfxApplier spawns the hit visual here.</summary>
        public float3 HitPosition;

        /// <summary>Element of the projectile at fire time. Underlying byte of CyberPickle.Core.ElementId. Drives prefab pick + tint.</summary>
        public byte Element;

        /// <summary>
        /// Normalized direction the projectile was traveling when it hit
        /// (= the projectile's velocity direction). The Mono-side
        /// HitVfxApplier uses this to orient the hit VFX so the burst's
        /// emission cone faces back along the projectile's path (rather
        /// than the default world-up identity orientation, which makes hit
        /// bursts feel detached from the bullet's trajectory).
        /// Zero vector if velocity wasn't available (rare — typically only
        /// for projectiles spawned by future systems that skip velocity).
        /// </summary>
        public float3 HitDirection;

        /// <summary>
        /// When true, DamageReportDrainSystem skips the HitVfxApplier.Play
        /// call for this hit — the projectile uses the hybrid Hovl-authored
        /// hit visual (CyberPickleProjectileVisual.OnHit handles it). Set
        /// by ProjectileCollisionSystem based on the ProjectileHasHybridVisual
        /// tag stamped at fire time. Prevents two hit visuals stacking at
        /// slightly different positions ("weird behavior" pre-fix).
        /// </summary>
        public bool SuppressDefaultHitVfx;

        /// <summary>
        /// Marks the SINGLE central-explosion report that
        /// ProjectileExplosionSystem emits per detonation (the one at the
        /// AoE epicenter with DamageDealt=0). DamageReportDrainSystem fires
        /// MusicEvent.WeaponDetonate exactly when it sees this flag — so
        /// the Wwise snare hits once per grenade explosion, not once per
        /// damaged enemy. Always false on per-enemy hit reports (those
        /// already carry DamageDealt > 0).
        /// </summary>
        public bool IsDetonation;
    }
}
