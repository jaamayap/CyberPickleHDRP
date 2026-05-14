// File: Assets/_CyberPickle/Code/Gameplay/Progression/UpgradePoolSO.cs
// Namespace: CyberPickle.Gameplay.Progression
//
// Container for the set of UpgradeCardSO assets the player can be offered
// during a run. The level-up coordinator queries this pool for the cards
// to display each level-up.
//
// Drawing rules (post-M8 step 2):
//   - LOADOUT-AWARE FILTER (the new rule): cards are filtered by the
//     player's current loadout state. NewWeapon cards only appear when
//     a weapon axis is empty; LevelUpWeapon cards only appear when their
//     target weapon is equipped and below L5; same for power-up variants.
//     This is the user's "only show what's relevant" rule from chat
//     2026-05-11 — once your weapons are full, the pool stops offering
//     new ones and shifts entirely to upgrade cards.
//   - Banished cards (per-run banish list, applied via Banish button on
//     level-up screen)
//   - Prerequisites (a card with prereqs only appears once all listed
//     cards are owned)
//   - Weighted by rarity, with Luck modulating the weights toward
//     higher rarities (computed by RarityRollService elsewhere; kept here
//     because the pool already had local weight defaults)
//
// Output: List<DraftedCard>. The DraftedCard wrapper carries the rolled
// rarity (so re-applies are deterministic) AND the rolled element (only
// meaningful for NewPowerUp cards — others use ElementId.None). The
// element roll is uniform across the 7 element identities — Fire,
// Lightning, Ice, Earth, Plasma, Light, Dark.
//
// Why a separate pool SO instead of "all UpgradeCardSO assets in the
// project": pools are scoped per-character / per-run / per-level. Pik's
// pool may be different from a future archetype's. Boss treasure pools
// only contain Legendary cards. Centralized pools also let the team
// balance weight tuning in one place per pool, not per-card.

using System.Collections.Generic;
using UnityEngine;
using CyberPickle.Core;
using CyberPickle.Gameplay.Weapons;
using CyberPickle.Shop.Equipment.Data;

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

        [Header("Template Resolution — Weapon Pool")]
        [Tooltip("The universe of weapons that TEMPLATE cards (NewWeapon / LevelUpWeapon / RarityUpWeapon with targetWeaponData = null) can resolve to. " +
                 "For NewWeapon templates: random pick from this list excluding currently-equipped. " +
                 "For LevelUp / RarityUp templates: resolves to a random equipped weapon from this list. " +
                 "Set this to the full roster of unlockable weapons for the run; if empty, weapon templates are filtered out of the draft.")]
        public WeaponData[] weaponTemplatePool;

        [Header("Template Resolution — Power-Up Pool")]
        [Tooltip("The universe of power-ups that TEMPLATE cards (NewPowerUp / LevelUpPowerUp / RarityUpPowerUp with targetPowerUpData = null) can resolve to. " +
                 "Same pattern as weaponTemplatePool. Set when M10 power-up cards land; safe to leave empty until then.")]
        public PowerUpData[] powerUpTemplatePool;

        [Header("Rarity Weights (default; Luck modulates these at draw time)")]
        [Tooltip("Relative draw weight for Common cards. Higher = more frequent. Defaults sum to 100 for sanity but absolute values don't matter — only ratios.")]
        [Min(0f)] public float weightCommon    = 60f;
        [Min(0f)] public float weightUncommon  = 25f;
        [Min(0f)] public float weightRare      = 10f;
        [Min(0f)] public float weightEpic      = 4f;
        [Min(0f)] public float weightLegendary = 1f;

        [Header("Card Type Weights (project defaults per weapon_rarity_v1.md §3 Path B)")]
        [Tooltip("Relative weight for StatModifier (Buff) cards. Always eligible — no loadout-state filter.")]
        [Min(0f)] public float weightTypeStatModifier = 30f;

        [Tooltip("Relative weight for LevelUpWeapon cards. Filtered to only equipped weapons below L5. The bread-and-butter of the draft once weapons are slotted.")]
        [Min(0f)] public float weightTypeLevelUpWeapon = 50f;

        [Tooltip("Relative weight for NewPowerUp cards (replaces the old PowerUp stub). Filtered to require an empty power-up axis.")]
        [Min(0f)] public float weightTypeNewPowerUp = 25f;

        [Tooltip("Relative weight for RarityUpWeapon cards. Filtered to equipped weapons below Legendary.")]
        [Min(0f)] public float weightTypeRarityUpWeapon = 15f;

        [Tooltip("Relative weight for SkillUnlock cards. M11 work; cards typed SkillUnlock are stubs until then.")]
        [Min(0f)] public float weightTypeSkillUnlock = 10f;

        [Tooltip("Relative weight for Cosmetic cards. 0 by default (cosmetics don't appear in level-up drafts; they're shop-only).")]
        [Min(0f)] public float weightTypeCosmetic = 0f;

        [Tooltip("Relative weight for NewWeapon cards. Filtered to require an empty weapon axis. Once axes fill up, the pool stops offering these and tilts toward upgrade cards.")]
        [Min(0f)] public float weightTypeNewWeapon = 30f;

        [Tooltip("Relative weight for LevelUpPowerUp cards. Filtered to equipped power-ups below L5.")]
        [Min(0f)] public float weightTypeLevelUpPowerUp = 30f;

        [Tooltip("Relative weight for RarityUpPowerUp cards. Filtered to equipped power-ups below Legendary.")]
        [Min(0f)] public float weightTypeRarityUpPowerUp = 12f;

        [Header("Luck Modulation")]
        [Tooltip("How aggressively Luck shifts weight from Common→Legendary. At Luck=0 weights are unchanged. At Luck=100 + this multiplier 0.5, weights for Rare/Epic/Legendary are 50% higher and Common 50% lower (tunable). Ship safely below 1 to avoid game-breaking-rarity-flooding.")]
        [Range(0f, 1f)] public float luckShiftPerHundred = 0.5f;

        // Element pool used when rolling a NewPowerUp card's element. We
        // skip ElementId.None — neutral power-ups don't make sense (their
        // whole point is to confer an element to a weapon).
        private static readonly ElementId[] ROLLABLE_ELEMENTS = new[]
        {
            ElementId.Fire, ElementId.Lightning, ElementId.Ice, ElementId.Earth,
            ElementId.Plasma, ElementId.Light, ElementId.Dark,
        };

        // ─── Drawing API ──────────────────────────────────────────────────

        /// <summary>
        /// Draws up to <paramref name="count"/> distinct cards from the pool,
        /// filtered by current loadout state + banish + prerequisites, and
        /// weighted by rarity (Luck-modulated). Returns fewer than
        /// <paramref name="count"/> cards if the filtered pool is too small.
        ///
        /// Each returned <see cref="DraftedCard"/> carries the rolled rarity
        /// (used by Apply when committing the card) AND the rolled element
        /// (only meaningful for NewPowerUp cards).
        /// </summary>
        /// <param name="count">How many cards to draw.</param>
        /// <param name="luck">Player Luck stat — modulates rarity weights.</param>
        /// <param name="loadout">Current loadout — used to filter cards by axis state. May be null (all loadout-dependent cards then filtered out conservatively).</param>
        /// <param name="banishedCardIds">CardIds banished this run; will be skipped.</param>
        /// <param name="ownedCardIds">CardIds the player already has; used for prerequisite checks.</param>
        public List<DraftedCard> DrawCards(
            int count,
            float luck,
            WeaponLoadoutRuntime loadout,
            HashSet<string> banishedCardIds,
            HashSet<string> ownedCardIds)
        {
            var result = new List<DraftedCard>(count);
            if (cards == null || cards.Length == 0) return result;

            // Build the eligible list once (filters applied).
            var eligible = new List<UpgradeCardSO>(cards.Length);
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c == null) continue;
                if (banishedCardIds != null && banishedCardIds.Contains(c.cardId)) continue;
                if (!PrerequisitesMet(c, ownedCardIds)) continue;
                if (!IsEligibleForLoadout(c, loadout)) continue;
                eligible.Add(c);
            }

            if (eligible.Count == 0) return result;

            // Compute per-card weights (type weight × rarity weight × Luck modulation).
            var weights = new float[eligible.Count];
            float totalWeight = 0f;
            for (int i = 0; i < eligible.Count; i++)
            {
                var c = eligible[i];
                weights[i] = TypeWeight(c.cardType) * ComputeWeight(c.rarity, luck);
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

                var pickedSo = eligible[picked];

                // Template resolution: if the card is a weapon template
                // (targetWeaponData == null), pick a concrete weapon from
                // the loadout / template pool now. The same templated
                // card SO can appear multiple times in a draft pulling
                // DIFFERENT resolved weapons each draw — natural variance
                // without authoring N variants. Returns null if no valid
                // target — we filter out and try the next pick.
                WeaponData resolvedWeapon = ResolveTemplateWeapon(pickedSo, loadout);
                if (IsWeaponTemplate(pickedSo) && resolvedWeapon == null)
                {
                    // No valid target after eligibility passed (race condition
                    // between filter + roll). Skip this pick, retry next loop.
                    totalWeight -= weights[picked];
                    eligible.RemoveAt(picked);
                    weights[picked] = picked < eligible.Count ? weights[eligible.Count] : 0f;
                    draw--; // give back the draw slot
                    continue;
                }

                PowerUpData resolvedPowerUp = ResolveTemplatePowerUp(pickedSo, loadout);

                var drafted = new DraftedCard
                {
                    source                = pickedSo,
                    rolledRarity          = pickedSo.rarity, // authored rarity is the roll for now; future: re-roll on draft
                    rolledElement         = pickedSo.cardType == CardType.NewPowerUp
                                                ? ROLLABLE_ELEMENTS[Random.Range(0, ROLLABLE_ELEMENTS.Length)]
                                                : ElementId.None,
                    resolvedTargetWeapon  = resolvedWeapon,
                    resolvedTargetPowerUp = resolvedPowerUp,
                };
                result.Add(drafted);

                totalWeight -= weights[picked];
                eligible.RemoveAt(picked);
                weights[picked] = weights[eligible.Count]; // O(1) swap-and-pop
            }

            return result;
        }

        // ─── Eligibility — loadout-aware filter ──────────────────────────

        /// <summary>
        /// Per-type filter: returns true if the card would land on a valid
        /// target given the current loadout. Implements the user's "only
        /// show what's relevant" rule.
        ///
        /// Now also handles TEMPLATE cards (target* fields null) — checks
        /// the corresponding weaponTemplatePool / powerUpTemplatePool for
        /// at least one eligible candidate.
        /// </summary>
        private bool IsEligibleForLoadout(UpgradeCardSO card, WeaponLoadoutRuntime loadout)
        {
            // Without a loadout reference (e.g., test code), everything that
            // doesn't strictly require one passes through. Loadout-dependent
            // types are conservatively rejected.
            if (loadout == null)
            {
                return card.cardType == CardType.StatModifier
                    || card.cardType == CardType.SkillUnlock
                    || card.cardType == CardType.Cosmetic;
            }

            switch (card.cardType)
            {
                case CardType.StatModifier:
                case CardType.SkillUnlock:
                case CardType.Cosmetic:
                    return true; // no loadout dependency

                case CardType.NewWeapon:
                    if (loadout.AreWeaponSlotsFull) return false;
                    if (card.targetWeaponData != null)
                        return loadout.FindByWeaponData(card.targetWeaponData) == null;
                    // Template: at least one weapon in the pool not yet equipped
                    return HasAnyUnequippedWeaponInPool(loadout);

                case CardType.LevelUpWeapon:
                {
                    if (card.targetWeaponData != null)
                    {
                        var w = loadout.FindByWeaponData(card.targetWeaponData);
                        return w != null && w.level < 5;
                    }
                    return HasAnyEquippedWeaponBelowLevel(loadout, 5);
                }

                case CardType.RarityUpWeapon:
                {
                    if (card.targetWeaponData != null)
                    {
                        var w = loadout.FindByWeaponData(card.targetWeaponData);
                        return w != null && w.rarity != Rarity.Legendary;
                    }
                    return HasAnyEquippedWeaponBelowRarity(loadout, Rarity.Legendary);
                }

                case CardType.NewPowerUp:
                    if (loadout.ArePowerUpSlotsFull) return false;
                    if (card.targetPowerUpData != null)
                        return loadout.FindByPowerUpData(card.targetPowerUpData) == null;
                    return HasAnyUnequippedPowerUpInPool(loadout);

                case CardType.LevelUpPowerUp:
                {
                    if (card.targetPowerUpData != null)
                    {
                        var p = loadout.FindByPowerUpData(card.targetPowerUpData);
                        return p != null && p.level < 5;
                    }
                    return HasAnyEquippedPowerUpBelowLevel(loadout, 5);
                }

                case CardType.RarityUpPowerUp:
                {
                    if (card.targetPowerUpData != null)
                    {
                        var p = loadout.FindByPowerUpData(card.targetPowerUpData);
                        return p != null && p.rarity != Rarity.Legendary;
                    }
                    return HasAnyEquippedPowerUpBelowRarity(loadout, Rarity.Legendary);
                }

                default:
                    return false;
            }
        }

        // ─── Template resolution + eligibility helpers ──────────────────

        private static bool IsWeaponTemplate(UpgradeCardSO card)
        {
            return card.targetWeaponData == null && (
                card.cardType == CardType.NewWeapon ||
                card.cardType == CardType.LevelUpWeapon ||
                card.cardType == CardType.RarityUpWeapon);
        }

        private static bool IsPowerUpTemplate(UpgradeCardSO card)
        {
            return card.targetPowerUpData == null && (
                card.cardType == CardType.NewPowerUp ||
                card.cardType == CardType.LevelUpPowerUp ||
                card.cardType == CardType.RarityUpPowerUp);
        }

        /// <summary>Pick a concrete weapon for a template card from the loadout / weaponTemplatePool. Returns null for non-templates or when no valid target exists.</summary>
        private WeaponData ResolveTemplateWeapon(UpgradeCardSO card, WeaponLoadoutRuntime loadout)
        {
            if (!IsWeaponTemplate(card) || loadout == null) return null;

            switch (card.cardType)
            {
                case CardType.NewWeapon:
                {
                    // Pool entries not currently equipped.
                    if (weaponTemplatePool == null || weaponTemplatePool.Length == 0) return null;
                    var candidates = new List<WeaponData>(weaponTemplatePool.Length);
                    for (int i = 0; i < weaponTemplatePool.Length; i++)
                    {
                        var w = weaponTemplatePool[i];
                        if (w == null) continue;
                        if (loadout.FindByWeaponData(w) != null) continue; // already equipped
                        candidates.Add(w);
                    }
                    return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
                }

                case CardType.LevelUpWeapon:
                {
                    // Currently equipped weapons below L5.
                    var candidates = new List<WeaponData>(loadout.AxisCount);
                    for (int i = 0; i < loadout.AxisCount; i++)
                    {
                        var w = loadout.GetSlot(i);
                        if (w != null && w.IsValid && w.weaponData != null && w.level < 5)
                            candidates.Add(w.weaponData);
                    }
                    return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
                }

                case CardType.RarityUpWeapon:
                {
                    var candidates = new List<WeaponData>(loadout.AxisCount);
                    for (int i = 0; i < loadout.AxisCount; i++)
                    {
                        var w = loadout.GetSlot(i);
                        if (w != null && w.IsValid && w.weaponData != null && w.rarity != Rarity.Legendary)
                            candidates.Add(w.weaponData);
                    }
                    return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
                }
            }
            return null;
        }

        /// <summary>Same as ResolveTemplateWeapon but for power-up templates. Mirrors the three branches.</summary>
        private PowerUpData ResolveTemplatePowerUp(UpgradeCardSO card, WeaponLoadoutRuntime loadout)
        {
            if (!IsPowerUpTemplate(card) || loadout == null) return null;

            switch (card.cardType)
            {
                case CardType.NewPowerUp:
                {
                    if (powerUpTemplatePool == null || powerUpTemplatePool.Length == 0) return null;
                    var candidates = new List<PowerUpData>(powerUpTemplatePool.Length);
                    for (int i = 0; i < powerUpTemplatePool.Length; i++)
                    {
                        var p = powerUpTemplatePool[i];
                        if (p == null) continue;
                        if (loadout.FindByPowerUpData(p) != null) continue;
                        candidates.Add(p);
                    }
                    return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
                }

                case CardType.LevelUpPowerUp:
                {
                    var candidates = new List<PowerUpData>(loadout.AxisCount);
                    for (int i = 0; i < loadout.AxisCount; i++)
                    {
                        var p = loadout.GetPowerUp(i);
                        if (p != null && p.IsValid && p.powerUpData != null && p.level < 5)
                            candidates.Add(p.powerUpData);
                    }
                    return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
                }

                case CardType.RarityUpPowerUp:
                {
                    var candidates = new List<PowerUpData>(loadout.AxisCount);
                    for (int i = 0; i < loadout.AxisCount; i++)
                    {
                        var p = loadout.GetPowerUp(i);
                        if (p != null && p.IsValid && p.powerUpData != null && p.rarity != Rarity.Legendary)
                            candidates.Add(p.powerUpData);
                    }
                    return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
                }
            }
            return null;
        }

        // Cheap eligibility checks used by IsEligibleForLoadout for templates.
        private bool HasAnyUnequippedWeaponInPool(WeaponLoadoutRuntime loadout)
        {
            if (weaponTemplatePool == null) return false;
            for (int i = 0; i < weaponTemplatePool.Length; i++)
            {
                var w = weaponTemplatePool[i];
                if (w != null && loadout.FindByWeaponData(w) == null) return true;
            }
            return false;
        }

        private static bool HasAnyEquippedWeaponBelowLevel(WeaponLoadoutRuntime loadout, int maxLevel)
        {
            for (int i = 0; i < loadout.AxisCount; i++)
            {
                var w = loadout.GetSlot(i);
                if (w != null && w.IsValid && w.level < maxLevel) return true;
            }
            return false;
        }

        private static bool HasAnyEquippedWeaponBelowRarity(WeaponLoadoutRuntime loadout, Rarity maxRarity)
        {
            for (int i = 0; i < loadout.AxisCount; i++)
            {
                var w = loadout.GetSlot(i);
                if (w != null && w.IsValid && w.rarity != maxRarity) return true;
            }
            return false;
        }

        private bool HasAnyUnequippedPowerUpInPool(WeaponLoadoutRuntime loadout)
        {
            if (powerUpTemplatePool == null) return false;
            for (int i = 0; i < powerUpTemplatePool.Length; i++)
            {
                var p = powerUpTemplatePool[i];
                if (p != null && loadout.FindByPowerUpData(p) == null) return true;
            }
            return false;
        }

        private static bool HasAnyEquippedPowerUpBelowLevel(WeaponLoadoutRuntime loadout, int maxLevel)
        {
            for (int i = 0; i < loadout.AxisCount; i++)
            {
                var p = loadout.GetPowerUp(i);
                if (p != null && p.IsValid && p.level < maxLevel) return true;
            }
            return false;
        }

        private static bool HasAnyEquippedPowerUpBelowRarity(WeaponLoadoutRuntime loadout, Rarity maxRarity)
        {
            for (int i = 0; i < loadout.AxisCount; i++)
            {
                var p = loadout.GetPowerUp(i);
                if (p != null && p.IsValid && p.rarity != maxRarity) return true;
            }
            return false;
        }

        // ─── Internals ────────────────────────────────────────────────────

        private float ComputeWeight(Rarity rarity, float luck)
        {
            float baseWeight = rarity switch
            {
                Rarity.Common    => weightCommon,
                Rarity.Uncommon  => weightUncommon,
                Rarity.Rare      => weightRare,
                Rarity.Epic      => weightEpic,
                Rarity.Legendary => weightLegendary,
                _                => 0f,
            };

            // Luck modulation: shift weight from Common toward higher rarities.
            float shiftAmount = Mathf.Clamp01((luck * 0.01f) * luckShiftPerHundred);
            float multiplier = rarity switch
            {
                Rarity.Common    => 1f - shiftAmount,
                Rarity.Uncommon  => 1f - shiftAmount * 0.4f,
                Rarity.Rare      => 1f + shiftAmount * 0.5f,
                Rarity.Epic      => 1f + shiftAmount * 1.0f,
                Rarity.Legendary => 1f + shiftAmount * 1.5f,
                _                => 1f,
            };

            return Mathf.Max(0f, baseWeight * multiplier);
        }

        private float TypeWeight(CardType type) => type switch
        {
            CardType.StatModifier    => weightTypeStatModifier,
            CardType.LevelUpWeapon   => weightTypeLevelUpWeapon,
            CardType.NewPowerUp      => weightTypeNewPowerUp,
            CardType.RarityUpWeapon  => weightTypeRarityUpWeapon,
            CardType.SkillUnlock     => weightTypeSkillUnlock,
            CardType.Cosmetic        => weightTypeCosmetic,
            CardType.NewWeapon       => weightTypeNewWeapon,
            CardType.LevelUpPowerUp  => weightTypeLevelUpPowerUp,
            CardType.RarityUpPowerUp => weightTypeRarityUpPowerUp,
            _                        => 0f,
        };

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
