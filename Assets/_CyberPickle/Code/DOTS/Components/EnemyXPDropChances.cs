// File: Assets/_CyberPickle/Code/DOTS/Components/EnemyXPDropChances.cs
// Namespace: CyberPickle.DOTS.Components
//
// Per-enemy XP drop probability table, baked from EnemyData.xpDropTable.
// EnemyDeathSystem reads this on kill and rolls the cascade:
//
//   roll = random(0..1)
//   if roll < Tier4Chance       -> drop Tier 4 (Sentinel Core)
//   else if < Tier4 + Tier3     -> drop Tier 3 (Synth Spark)
//   else if < Tier4 + Tier3 + Tier2  -> drop Tier 2 (Neural Shard)
//   else if < ... + Tier1       -> drop Tier 1 (Code Crystal)
//   else                        -> drop Tier 0 (Data Fragment) — fallback
//
// Boss-tagged entities ignore this cascade and use BossMultiDropCount
// to spawn a burst of Tier 4 gems in a circle.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct EnemyXPDropChances : IComponentData
    {
        public float Tier1Chance;
        public float Tier2Chance;
        public float Tier3Chance;
        public float Tier4Chance;

        /// <summary>Bosses spawn this many gems in a burst around their body, all Tier 4. 0 = no bonus burst.</summary>
        public int BossMultiDropCount;
    }
}
