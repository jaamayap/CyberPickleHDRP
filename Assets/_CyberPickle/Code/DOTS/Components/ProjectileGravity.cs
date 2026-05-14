// File: Assets/_CyberPickle/Code/DOTS/Components/ProjectileGravity.cs
// Namespace: CyberPickle.DOTS.Components
//
// Per-projectile gravity vector. Stamped at fire time by WeaponFiring for
// projectiles with WeaponData.trajectory == Parabolic. Applied each tick
// by ProjectileMovementSystem: velocity += Acceleration × dt → projectile
// follows a ballistic arc.
//
// Absent from straight-flight projectiles (pistol/shotgun/sniper) — they
// fly along their velocity vector with no acceleration.
//
// Default for parabolic = (0, -9.81, 0). Per-projectile because future
// designs might want different gravity per weapon (heavy mortar → -15;
// slow-mo grenade → -3; reverse-gravity meme → +9.81).

using Unity.Entities;
using Unity.Mathematics;

namespace CyberPickle.DOTS.Components
{
    public struct ProjectileGravity : IComponentData
    {
        /// <summary>Acceleration applied to the projectile's velocity each tick. Typically <c>(0, -9.81, 0)</c> — straight down at 1g.</summary>
        public float3 Acceleration;
    }
}
