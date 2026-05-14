// File: Assets/_CyberPickle/Code/DOTS/Components/ProjectilePierce.cs
// Namespace: CyberPickle.DOTS.Components
//
// Per-projectile pierce counter, stamped at fire time by WeaponFiring.
// Read + mutated by ProjectileCollisionSystem on every hit.
//
// Semantics: <c>Remaining</c> = "additional hits this projectile can absorb
// AFTER the current one". A fresh projectile with <c>Remaining = 0</c>
// destroys on first hit (single-target, the default). <c>Remaining = N</c>
// at fire time lets the projectile hit N+1 enemies total — each pierce
// decrements the counter; when <c>Remaining == 0</c> at hit time, the
// projectile destroys.
//
// Examples:
//   pierce = 0 → hits 1 enemy (current behavior, default for pistol/shotgun)
//   pierce = 1 → hits 2 enemies (the first hit + 1 pierce)
//   pierce = 9 → hits 10 enemies (sniper at L5-Legendary with basePierce=1)
//
// Burst-readable. Counter is byte-sized — pierce > 255 doesn't make
// gameplay sense (the projectile would punch through the entire game).
//
// Companion: <see cref="ProjectileHitTarget"/> dynamic buffer tracks WHICH
// enemies have been hit by this projectile so cross-frame double-hits are
// prevented (a projectile lingering in an enemy's hit radius for several
// frames otherwise drains its pierce counter on the same enemy).

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct ProjectilePierce : IComponentData
    {
        /// <summary>Hits this projectile can still absorb. Decremented per hit while &gt; 0; when 0, the next hit destroys the projectile.</summary>
        public byte Remaining;
    }
}
