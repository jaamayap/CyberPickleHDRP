// File: Assets/_CyberPickle/Code/DOTS/Components/XPGemPrefabBufferElement.cs
// Namespace: CyberPickle.DOTS.Components
//
// One entry in the XP gem prefab registry — maps a tier index (0..N-1) to a
// baked entity prefab + the XP value gems of that tier should award.
// EnemyDeathSystem rolls a tier on kill, looks up the matching entry, and
// Instantiates from the Prefab field at the dying entity's position.
//
// 6-entry buffer expected as of the T5 jackpot addition. Tier 0 = trash drop;
// Tier 5 = Sentinel Prime (ultra-rare). XP values come from XPGemTierTableSO.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    [InternalBufferCapacity(6)]
    public struct XPGemPrefabBufferElement : IBufferElementData
    {
        /// <summary>Tier index (0..N-1) — must match the registry order.</summary>
        public int Tier;

        /// <summary>Baked entity prefab to Instantiate. Carries Prefab tag.</summary>
        public Entity Prefab;

        /// <summary>XP awarded by gems of this tier (stamped onto each spawned gem).</summary>
        public int XPValue;
    }
}
