// File: Assets/_CyberPickle/Code/DOTS/Components/EnemyXPDropChances.cs
// Namespace: CyberPickle.DOTS.Components
//
// Per-enemy XP drop probability table, baked from EnemyData.xpDropTable.
// EnemyDeathSystem reads this on kill and rolls the cascade highest tier
// down:
//
//   roll = random(0..1)
//   if roll < Tier5Chance                                    -> Tier 5 (Sentinel Prime — jackpot)
//   else if < Tier5 + Tier4                                  -> Tier 4 (Sentinel Core)
//   else if < Tier5 + Tier4 + Tier3                          -> Tier 3 (Synth Spark)
//   else if < ... + Tier2                                    -> Tier 2 (Neural Shard)
//   else if < ... + Tier1                                    -> Tier 1 (Code Crystal)
//   else                                                     -> Tier 0 (Data Fragment) — fallback
//
// 2026-05-12: Tier 5 (Sentinel Prime) added — the ultra-rare jackpot drop
// (typically 0.001..0.005 base chance, multiplied by Luck) carrying a
// massive XP payload that can trigger a multi-level-up cascade. Designed
// as the "build defining moment" drop.
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

        /// <summary>Ultra-rare jackpot tier — multi-level-up XP payload. Typically 0..0.005 on non-elite enemies. Scaled by Luck downstream when implemented.</summary>
        public float Tier5Chance;

        /// <summary>Bosses spawn this many gems in a burst around their body, all Tier 4. 0 = no bonus burst.</summary>
        public int BossMultiDropCount;
    }
}
