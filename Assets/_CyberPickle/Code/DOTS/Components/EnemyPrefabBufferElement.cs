// File: Assets/_CyberPickle/Code/DOTS/Components/EnemyPrefabBufferElement.cs
// Namespace: CyberPickle.DOTS.Components
//
// One entry in the enemy prefab registry — maps an EnemyTypeId hash
// (Animator.StringToHash of EnemyData.enemyId) to a baked entity prefab
// the spawner can Instantiate.
//
// Lives on the singleton "registry" entity baked from
// EnemyPrefabRegistryAuthoring in EnemisSubScene. Replaces
// EnemyPrefabSingleton (which carried only ONE prefab) — the singleton
// is kept around for backward compat with one-prefab test setups.
//
// Lookup is O(n). For 5–20 enemy types this is faster than a HashMap;
// when the registry grows past ~50 entries we'll swap to NativeHashMap
// or a sorted+binary-searched buffer. Not a concern at survivors-like
// enemy variety counts.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    [InternalBufferCapacity(16)]
    public struct EnemyPrefabBufferElement : IBufferElementData
    {
        /// <summary>Animator.StringToHash of the EnemyData.enemyId string.</summary>
        public int Hash;

        /// <summary>Baked entity prefab to Instantiate. Carries Prefab tag.</summary>
        public Entity Prefab;
    }
}
