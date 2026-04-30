// File: Assets/_CyberPickle/Code/DOTS/Components/EnemyTypeId.cs
// Namespace: CyberPickle.DOTS.Components
//
// Stable integer hash of EnemyData.enemyId (via Animator.StringToHash),
// baked onto each enemy entity. Used by:
//   - The EnemyPrefabRegistry for fast prefab lookup at spawn time
//   - The drops system (M6) to find the right LootDrop table
//   - Analytics / kill counters (per-type stats)
// Burst-friendly because it's just an int.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct EnemyTypeId : IComponentData
    {
        public int Value;
    }
}
