// File: Assets/_CyberPickle/Code/DOTS/Components/ProjectileTumble.cs
// Namespace: CyberPickle.DOTS.Components
//
// Per-projectile rotational velocity. Stamped at fire time by WeaponFiring
// (typically for parabolic projectiles — grenade tumble visual). Applied
// each tick by ProjectileMovementSystem: rotation = rotation * Euler(rate × dt).
//
// Absent from straight-flight projectiles — they keep their fire-time
// rotation (which points along the muzzle's forward, so the projectile
// reads as facing its travel direction).

using Unity.Entities;
using Unity.Mathematics;

namespace CyberPickle.DOTS.Components
{
    public struct ProjectileTumble : IComponentData
    {
        /// <summary>Angular velocity around each local axis (radians per second). Common pattern: (1, 0.5, 0.3) × tumbleSpeed for a chaotic-looking tumble. Set zero on any axis you don't want to spin.</summary>
        public float3 AnglesPerSecondRad;
    }
}
