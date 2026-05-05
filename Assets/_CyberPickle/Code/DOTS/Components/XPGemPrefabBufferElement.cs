// File: Assets/_CyberPickle/Code/DOTS/Components/XPGemPrefabBufferElement.cs
// Namespace: CyberPickle.DOTS.Components
//
// One entry in the XP gem prefab registry — maps a tier index (0–4) to a
// baked entity prefab + the XP value gems of that tier should award.
// EnemyDeathSystem rolls a tier on kill, looks up the matching entry, and
// Instantiates from the Prefab field at the dying entity's position.
//
// 5-entry buffer expected. Tier 0 = trash drop; Tier 4 = jackpot.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    [InternalBufferCapacity(5)]
    public struct XPGemPrefabBufferElement : IBufferElementData
    {
        /// <summary>Tier index (0–4) — must match the registry order.</summary>
        public int Tier;

        /// <summary>Baked entity prefab to Instantiate. Carries Prefab tag.</summary>
        public Entity Prefab;

        /// <summary>XP awarded by gems of this tier (stamped onto each spawned gem).</summary>
        public int XPValue;
    }
}
