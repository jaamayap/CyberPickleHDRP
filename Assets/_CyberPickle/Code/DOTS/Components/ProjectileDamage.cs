// File: Assets/_CyberPickle/Code/DOTS/Components/ProjectileDamage.cs
// Namespace: CyberPickle.DOTS.Components
//
// Damage applied by a projectile when it hits an enemy. Read by
// ProjectileCollisionSystem. Set per-spawn by WeaponFiring (so the same
// projectile prefab can carry different damage values from different
// weapons / upgrades).

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct ProjectileDamage : IComponentData
    {
        public float Value;
    }
}
