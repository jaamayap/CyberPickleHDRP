// File: Assets/_CyberPickle/Code/DOTS/Components/ProjectileAoE.cs
// Namespace: CyberPickle.DOTS.Components
//
// Marks a projectile as an explosion / area-of-effect weapon. Stamped at
// fire time by WeaponFiring from WeaponData.baseAreaOfEffect when the
// weapon's design is AoE (grenade launcher, M9 PR E).
//
// Read by ProjectileCollisionSystem: when an AoE projectile triggers a
// proximity hit, it damages EVERY enemy within Radius of its current
// position — not just the one it proximity-hit. One DamageHitReport per
// damaged enemy → one hit-VFX per enemy → "chain of detonations" feel
// (similar to sniper pierce but radial instead of linear).
//
// AoE projectiles ALWAYS destroy on first impact — the Pierce mechanic
// is mutually exclusive with AoE (you can't pierce-and-explode).

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct ProjectileAoE : IComponentData
    {
        /// <summary>Explosion radius (world units). Damage applied to every enemy whose XZ-distance from the projectile's current position is &lt;= Radius. Typical grenade: 3-5m. Larger = wider clear, lower per-target damage (designer balances via baseDamage × Rarity vs Radius).</summary>
        public float Radius;
    }
}
