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
    }
}
