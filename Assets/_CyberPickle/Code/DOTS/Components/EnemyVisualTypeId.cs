// File: Assets/_CyberPickle/Code/DOTS/Components/EnemyVisualTypeId.cs
// Namespace: CyberPickle.DOTS.Components
//
// Visual classification baked from EnemyData.visualType. Stored as a
// plain int (matching the EnemyVisualType enum's underlying int value)
// so it maps 1:1 to the Animator's "EnemyType" int parameter without
// any conversion at runtime.
//
// Read by:
//   - EnemyVisualBindingSystem: writes the int to the Animator on spawn
//     so Walk/Run transitions branch by character type.
//   - EnemyDeathSystem: picks the correct DeathVariant (Big = 2, normal
//     = random 0/1).
//   - Future drops/balance systems (M6+): could scale rewards by size.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct EnemyVisualTypeId : IComponentData
    {
        /// <summary>
        /// Matches the int value of the EnemyVisualType enum on EnemyData.
        /// 0 = StandardHumanoid, 1 = BigHumanoid, etc.
        /// </summary>
        public int Value;
    }
}
