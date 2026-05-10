// File: Assets/_CyberPickle/Code/DOTS/Components/ProjectileSource.cs
// Namespace: CyberPickle.DOTS.Components
//
// Tags a projectile entity with the weapon that fired it. Read by
// ProjectileCollisionSystem on hit so per-weapon damage can be reported
// to PerWeaponStatsTracker (rolling DPS, kill counts, etc., per
// GDD §3.12.x and the M7.4 hover-tooltip plan).
//
// Why FixedString64Bytes for the id: Burst-compatible (no managed string
// allocation), human-readable in the inspector / Entities Hierarchy
// during diagnostics, fits typical weapon names (< 63 chars). For very
// large weapon catalogs we'd switch to a hashed int id; not worth the
// indirection at this scale.

using Unity.Collections;
using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct ProjectileSource : IComponentData
    {
        /// <summary>
        /// Stable identifier for the weapon that spawned this projectile.
        /// Convention: lower-snake-case derived from the weapon's GameObject
        /// name (e.g., "laser_blaster", "plasma_lance"). Set by WeaponFiring.
        /// </summary>
        public FixedString64Bytes WeaponId;
    }
}
