// File: Assets/_CyberPickle/Code/DOTS/Components/EnemyPrefabSingleton.cs
// Namespace: CyberPickle.DOTS.Components
//
// Singleton component pointing at a baked entity prefab that runtime
// spawners can Instantiate(). Created at bake time by EnemyPrefabAuthoring
// in the SubScene; the Value field is the entity-prefab Entity (with
// Prefab tag) that lives in the entity world after SubScene load.
//
// Single-prefab MVP for the Milestone 5 hybrid-bridge perf test.
// Replaced by EnemyPrefabRegistry (multi-entry, keyed by EnemyTypeId)
// in chunk 5b.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct EnemyPrefabSingleton : IComponentData
    {
        public Entity Value;
    }
}
