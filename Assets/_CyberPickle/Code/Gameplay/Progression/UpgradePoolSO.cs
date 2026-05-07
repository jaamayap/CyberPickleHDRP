// File: Assets/_CyberPickle/Code/Gameplay/Progression/UpgradePoolSO.cs
// Namespace: CyberPickle.Gameplay.Progression
//
// Container for the set of UpgradeCardSO assets the player can be offered
// during a run. The level-up coordinator queries this pool for the 3 cards
// to display each level-up.
//
// Drawing rules:
//   - Filtered by banished cards (per-run banish list, applied via Banish
//     button on level-up screen — wired in M9 economy work)
//   - Filtered by prerequisite cards (a card with prereqs only appears
//     once all listed cards are owned)
//   - Weighted by rarity, with Luck stat modulating the weights toward
//     higher rarities
//
// Why a separate pool SO instead of "all UpgradeCardSO assets in the
// project": pools are scoped per-character / per-run / per-level. Pik's
// pool may be different from a future archetype's. Boss treasure pools
// only contain Legendary cards. Centralized pools also let the team
// balance weight tuning in one place per pool, not per-card.

using System.Collections.Generic;
using UnityEngine;

namespace CyberPickle.Gameplay.Progression
{
    [CreateAssetMenu(menuName = "CyberPickle/Progression/Upgrade Pool", fileName = "Pool_")]
    public class UpgradePoolSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Pool id for diagnostics. Convention: 'default' for the global default pool; '<character_id>' for per-character pools.")]
        public string poolId = "default";

        [Header("Cards")]
        [Tooltip("Every card available in this pool. Order doesn't matter — drawing is weighted by rarity.")]
        public UpgradeCardSO[] cards;

        [Header("Rarity Weights (default; Luck modulates these at draw time)")]
        [Tooltip("Relative draw weight for Common cards. Higher = more frequent. Defaults sum to 100 for sanity but absolute values don't matter — only ratios.")]
        [Min(0f)] public float weightCommon    = 60f;
        [Min(0f)] public float weightUncommon  = 25f;
        [Min(0f)] public float weightRare      = 10f;
        [Min(0f)] public float weightEpic      = 4f;
        [Min(0f)] public float weightLegendary = 1f;

        [Header("Luck Modulation")]
        [Tooltip("How aggressively Luck shifts weight from Common→Legendary. At Luck=0 weights are unchanged. At Luck=100 + this multiplier 0.5, weights for Rare/Epic/Legendary are 50% higher and Common 50% lower (tunable). Ship safely below 1 to avoid game-breaking-rarity-flooding.")]
        [Range(0f, 1f)] public float luckShiftPerHundred = 0.5f;

        // ─── Drawing API ──────────────────────────────────────────────────

        /// <summary>
        /// Draws up to <paramref name="count"/> distinct cards from the pool,
        /// filtered by the supplied predicates and weighted by rarity (modulated
        /// by Luck). Returns fewer than <paramref name="count"/> cards if the
        /// pool can't satisfy the request (e.g., everything banished, low pool size).
        /// </summary>
        /// <param name="count">How many cards to draw (typically 3).</param>
        /// <param name="luck">Player Luck stat. 0+ — modulates rarity weights.</param>
        /// <param name="banishedCardIds">CardIds banished this run; will be skipped.</param>
        /// <param name="ownedCardIds">CardIds the player already has; used for prerequisite checks.</param>
        public List<UpgradeCardSO> DrawCards(
            int count,
            float luck,
            HashSet<string> banishedCardIds,
            HashSet<string> ownedCardIds)
        {
            var result = new List<UpgradeCardSO>(count);
            if (cards == null || cards.Length == 0) return result;

            // Build the eligible list once (filters applied).
            var eligible = new List<UpgradeCardSO>(cards.Length);
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c == null) continue;
                if (banishedCardIds != null && banishedCardIds.Contains(c.cardId)) continue;
                if (!PrerequisitesMet(c, ownedCardIds)) continue;
                eligible.Add(c);
            }

            if (eligible.Count == 0) return result;

            // Compute per-card weights (rarity weight × Luck modulation).
            var weights = new float[eligible.Count];
            float totalWeight = 0f;
            for (int i = 0; i < eligible.Count; i++)
            {
                weights[i] = ComputeWeight(eligible[i].rarity, luck);
                totalWeight += weights[i];
            }

            // Sample without replacement: pick, remove from local pool, repeat.
            for (int draw = 0; draw < count && eligible.Count > 0; draw++)
            {
                if (totalWeight <= 0f) break;

                float roll = UnityEngine.Random.value * totalWeight;
                float running = 0f;
                int picked = 0;
                for (int i = 0; i < eligible.Count; i++)
                {
                    running += weights[i];
                    if (roll <= running)
                    {
                        picked = i;
                        break;
                    }
                }

                result.Add(eligible[picked]);
                totalWeight -= weights[picked];
                eligible.RemoveAt(picked);
                // O(n) array shift; weights array kept in sync via swap-and-pop.
                weights[picked] = weights[eligible.Count];
            }

            return result;
        }

        // ─── Internals ────────────────────────────────────────────────────

        private float ComputeWeight(CardRarity rarity, float luck)
        {
            float baseWeight = rarity switch
            {
                CardRarity.Common    => weightCommon,
                CardRarity.Uncommon  => weightUncommon,
                CardRarity.Rare      => weightRare,
                CardRarity.Epic      => weightEpic,
                CardRarity.Legendary => weightLegendary,
                _                    => 0f,
            };

            // Luck modulation: shift weight from Common toward higher rarities.
            // shiftAmount in [0..1]; lower rarities scale down, higher scale up.
            float shiftAmount = Mathf.Clamp01((luck * 0.01f) * luckShiftPerHundred);
            float multiplier = rarity switch
            {
                CardRarity.Common    => 1f - shiftAmount,
                CardRarity.Uncommon  => 1f - shiftAmount * 0.4f,  // slightly down
                CardRarity.Rare      => 1f + shiftAmount * 0.5f,  // up
                CardRarity.Epic      => 1f + shiftAmount * 1.0f,
                CardRarity.Legendary => 1f + shiftAmount * 1.5f,
                _                    => 1f,
            };

            return Mathf.Max(0f, baseWeight * multiplier);
        }

        private static bool PrerequisitesMet(UpgradeCardSO card, HashSet<string> ownedCardIds)
        {
            if (card.prerequisiteCardIds == null || card.prerequisiteCardIds.Count == 0)
                return true;
            if (ownedCardIds == null) return false;
            for (int i = 0; i < card.prerequisiteCardIds.Count; i++)
            {
                if (!ownedCardIds.Contains(card.prerequisiteCardIds[i]))
                    return false;
            }
            return true;
        }
    }
}
