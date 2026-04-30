// File: Assets/_CyberPickle/Code/DOTS/Components/ProjectilePrefabHolder.cs
// Namespace: CyberPickle.DOTS.Components
//
// Singleton component that holds an Entity reference to the baked
// projectile prefab. Set up at bake time by ProjectilePrefabSetupAuthoring
// (which lives in a SubScene); read at runtime by WeaponFiring to know
// which entity prefab to instantiate per shot.
//
// This is the proper way to bridge MonoBehaviour-side weapon code with
// ECS-side prefab references — the Baker uses GetEntity(prefab, flags)
// to bake the prefab and its full hierarchy, returning a clean Entity
// reference that EntityManager.Instantiate can duplicate as a complete
// hierarchy (LinkedEntityGroup automatically populated).

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct ProjectilePrefabHolder : IComponentData
    {
        public Entity Value;
    }
}
