// File: Assets/_CyberPickle/Code/DOTS/Components/HitVFXPrefabRef.cs
// Namespace: CyberPickle.DOTS.Components
//
// Carried by projectile entities. Holds an Entity reference to the hit
// VFX prefab to instantiate when this projectile collides with an enemy.
// Set at bake time by ProjectileAuthoring.Baker via Baker.GetEntity()
// of the linked Hit VFX GameObject prefab.
//
// Decoupled from ProjectileTag so different projectile types can carry
// different hit VFX (laser sparks, plasma burst, ice shatter, etc.) just
// by editing the inspector field on each ProjectileAuthoring.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct HitVFXPrefabRef : IComponentData
    {
        public Entity Value;
    }
}
