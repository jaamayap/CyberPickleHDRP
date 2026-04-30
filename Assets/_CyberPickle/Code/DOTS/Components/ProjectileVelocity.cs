// File: Assets/_CyberPickle/Code/DOTS/Components/ProjectileVelocity.cs
// Namespace: CyberPickle.DOTS.Components
//
// World-space velocity for a projectile, applied each tick by
// ProjectileMovementSystem. The vector encodes both direction (normalized
// part) and speed (magnitude) — set by WeaponFiring at spawn time as
// muzzle.forward * weaponSpeed.

using Unity.Entities;
using Unity.Mathematics;

namespace CyberPickle.DOTS.Components
{
    public struct ProjectileVelocity : IComponentData
    {
        public float3 Value;
    }
}
