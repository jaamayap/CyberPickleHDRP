// File: Assets/_CyberPickle/Code/Core/Services/RarityRollService.cs
// Namespace: CyberPickle.Core.Services
//
// Centralized rarity-rolling service. Owns the math for:
//   - First-appearance weighted rolls (when a weapon enters the loadout
//     or a chest is opened — weapon_rarity_v1.md §3 Path A)
//   - Luck modulation of any rarity distribution (every ~100 Luck shifts
//     the distribution one tier toward Legendary — §4)
//   - Rarity upgrades (Rarity-up card, Augment Console, Resonator pickup —
//     §3 Path B / Path C)
//   - Rarity downgrades (Black Market failure — §6)
//   - Cards-visible-per-level-up count derived from Luck (§4 effect 2)
//
// All methods are static + pure (no internal state, no globals beyond
// UnityEngine.Random). Safe to call from MonoBehaviour code on the main
// thread. NOT Burst-compatible: uses managed UnityEngine.Random. If a
// Burst use case appears, mirror with a Unity.Mathematics.Random overload.
//
// Source of truth for the curve numbers: weapon_rarity_v1.md §4
// (Luck modulation table) and §3 (acquisition paths).
//
// Used by:
//   - WeaponLoadoutRuntime (M9-ish) on initial weapon draft / first appearance
//   - Future card-draw systems for Rarity-up cards
//   - Future in-level interactables (Augment Console, Black Market, Resonator)
//   - HUD: CardsVisibleForLuck() drives the level-up draft count
//
// NOT used by:
//   - UpgradePoolSO weighted-without-replacement multi-card draw — that has
//     its own distinct logic (N distinct items, not one rarity tier).
//     If we ever DRY-merge that, the merge should preserve the "without
//     replacement" semantics.

using UnityEngine;

namespace CyberPickle.Core.Services
{
    /// <summary>
    /// Per-rarity weight set. Sums to anything (only ratios matter).
    /// Used as input to <see cref="RarityRollService.Roll"/>.
    /// </summary>
    public struct RarityWeights
    {
        public float Common;
        public float Uncommon;
        public float Rare;
        public float Epic;
        public float Legendary;

        /// <summary>Read the weight for a specific tier.</summary>
        public float WeightFor(Rarity r) => r switch
        {
            Rarity.Common    => Common,
            Rarity.Uncommon  => Uncommon,
            Rarity.Rare      => Rare,
            Rarity.Epic      => Epic,
            Rarity.Legendary => Legendary,
            _                => 0f,
        };

        /// <summary>Sum of all five weights.</summary>
        public float Total => Common + Uncommon + Rare + Epic + Legendary;
    }

    /// <summary>
    /// Static rarity-rolling service. Centralizes Luck modulation, first-roll
    /// distributions, upgrade/downgrade math, and the cards-visible-per-Luck
    /// formula. See file header for usage rules.
    /// </summary>
    public static class RarityRollService
    {
        // ─── Default distributions ────────────────────────────────────────

        /// <summary>
        /// Default first-roll distribution per <c>weapon_rarity_v1.md</c> §3
        /// Path A. Sums to 100 for sanity but only ratios matter.
        ///
        ///   Common 60% / Uncommon 25% / Rare 10% / Epic 4% / Legendary 1%
        /// </summary>
        public static readonly RarityWeights DefaultFirstRoll = new RarityWeights
        {
            Common    = 60f,
            Uncommon  = 25f,
            Rare      = 10f,
            Epic      =  4f,
            Legendary =  1f,
        };

        // ─── Luck modulation ──────────────────────────────────────────────

        /// <summary>
        /// How much Luck shifts the distribution per 100 points of Luck.
        /// 0.5 means at Luck=100 the shift is half-way; at Luck=200 it's
        /// fully clamped to 1.0. Tunable; below 1.0 keeps the curve safe
        /// (rarity flooding is a real concern at higher values).
        ///
        /// This is a project-wide default. Per-pool tuning lives in
        /// <see cref="Gameplay.Progression.UpgradePoolSO"/>'s own equivalent
        /// field for card-draws-with-replacement. Future single-rarity
        /// roll consumers (drops, chests) use this constant.
        /// </summary>
        public const float DefaultLuckShiftPerHundred = 0.5f;

        // ─── Rolling API ──────────────────────────────────────────────────

        /// <summary>
        /// Rolls a single rarity from the default first-roll distribution
        /// (60/25/10/4/1) modulated by Luck. Used when a weapon first
        /// appears in a level-up draft, when a chest opens, or when a
        /// drop is rolled in-level.
        /// </summary>
        public static Rarity RollFirstAppearance(float luck)
        {
            return Roll(DefaultFirstRoll, luck);
        }

        /// <summary>
        /// Generic weighted rarity roll with Luck modulation. Use this
        /// when you have a custom weight set (boss-loot pool, special
        /// chest). Equivalent to <see cref="RollFirstAppearance"/> when
        /// passed <see cref="DefaultFirstRoll"/>.
        ///
        /// Luck curve (per <c>weapon_rarity_v1.md</c> §4):
        ///   - shiftAmount = clamp01(luck × 0.01 × DefaultLuckShiftPerHundred)
        ///   - Common multiplier:    1 - shiftAmount
        ///   - Uncommon multiplier:  1 - shiftAmount × 0.4
        ///   - Rare multiplier:      1 + shiftAmount × 0.5
        ///   - Epic multiplier:      1 + shiftAmount × 1.0
        ///   - Legendary multiplier: 1 + shiftAmount × 1.5
        ///
        /// At Luck=0, weights are unchanged. At Luck=200+ the shift is
        /// clamped at 1.0 (no further benefit from stacking Luck — keeps
        /// the system bounded).
        /// </summary>
        public static Rarity Roll(RarityWeights baseWeights, float luck)
        {
            float shiftAmount = Mathf.Clamp01(luck * 0.01f * DefaultLuckShiftPerHundred);

            var modulated = new RarityWeights
            {
                Common    = baseWeights.Common    * (1f - shiftAmount),
                Uncommon  = baseWeights.Uncommon  * (1f - shiftAmount * 0.4f),
                Rare      = baseWeights.Rare      * (1f + shiftAmount * 0.5f),
                Epic      = baseWeights.Epic      * (1f + shiftAmount * 1.0f),
                Legendary = baseWeights.Legendary * (1f + shiftAmount * 1.5f),
            };

            float total = modulated.Total;
            if (total <= 0f) return Rarity.Common;

            float roll = Random.value * total;
            float running = 0f;

            running += modulated.Common;    if (roll <= running) return Rarity.Common;
            running += modulated.Uncommon;  if (roll <= running) return Rarity.Uncommon;
            running += modulated.Rare;      if (roll <= running) return Rarity.Rare;
            running += modulated.Epic;      if (roll <= running) return Rarity.Epic;
            return Rarity.Legendary;
        }

        // ─── Upgrade / Downgrade ──────────────────────────────────────────

        /// <summary>
        /// Bump rarity up by N tiers (default 1). Clamps at Legendary —
        /// upgrading a Legendary returns Legendary (no overflow). Used by:
        ///   - Rarity-up cards (drafted on level-up)
        ///   - Augment Console interaction
        ///   - Resonator one-shot pickups
        ///   - Echo Compiler skill node (when a weapon hits Legendary,
        ///     bump all OTHER weapons +1 — caller iterates the loadout)
        /// </summary>
        public static Rarity UpgradeBy(Rarity current, int tiers = 1)
        {
            int newValue = Mathf.Clamp((int)current + tiers, (int)Rarity.Common, (int)Rarity.Legendary);
            return (Rarity)newValue;
        }

        /// <summary>
        /// Bump rarity down by N tiers (default 1). Clamps at Common.
        /// Used internally by <see cref="AttemptGamble"/> on failure;
        /// rarely needed elsewhere (the design avoids "negative numbers"
        /// — see CLAUDE.md design pillar 3 — so downgrades only show up
        /// in explicit risk-reward zones like Black Market).
        /// </summary>
        public static Rarity DowngradeBy(Rarity current, int tiers = 1)
        {
            int newValue = Mathf.Clamp((int)current - tiers, (int)Rarity.Common, (int)Rarity.Legendary);
            return (Rarity)newValue;
        }

        // ─── Black Market gamble ──────────────────────────────────────────

        /// <summary>
        /// Black Market gamble outcome. On success: <paramref name="current"/>
        /// goes up one tier (clamped at Legendary). On failure: <paramref name="current"/>
        /// goes down one tier (clamped at Common) — UNLESS the player has
        /// the "Rarity Insurance" skill-tree node active, in which case
        /// the failure is reverted (the input rarity is returned unchanged).
        ///
        /// Note: <see cref="WeaponInstanceData"/> doesn't track whether
        /// insurance was already consumed this run — that's the caller's
        /// job (typically a per-run counter on WeaponLoadoutRuntime or
        /// the skill state). Pass <paramref name="hasInsurance"/> = true
        /// only when the insurance is actually available.
        /// </summary>
        /// <param name="current">Current rarity to gamble on.</param>
        /// <param name="successChance">0..1 probability of success. Default 0.6 per <c>weapon_rarity_v1.md</c> §3 Path C; the "Black Market Auditor" notable bumps this to 0.85.</param>
        /// <param name="hasInsurance">If true and the gamble fails, returns the input rarity unchanged.</param>
        /// <returns>The new rarity after the gamble.</returns>
        public static Rarity AttemptGamble(Rarity current, float successChance = 0.6f, bool hasInsurance = false)
        {
            bool success = Random.value < successChance;
            if (success) return UpgradeBy(current, 1);
            return hasInsurance ? current : DowngradeBy(current, 1);
        }

        // ─── Cards-visible-per-Luck (level-up draft size) ─────────────────

        /// <summary>
        /// How many cards are visible to the player in a single level-up
        /// draft, as a function of Luck. Per <c>weapon_rarity_v1.md</c> §4
        /// effect 2:
        ///
        ///   Luck 0     → 3 cards (baseline)
        ///   Luck 50    → 4 cards
        ///   Luck 100   → 4 cards (one card per 50 Luck rounded down)
        ///   Luck 150   → 5 cards
        ///   Luck 200   → 5 cards
        ///   Luck 250+  → 6 cards (cap)
        ///
        /// This replaces the Choice Token reroll mechanic from V0.6 GDD;
        /// more Luck = better odds AND more visible options at the same time.
        /// </summary>
        public static int CardsVisibleForLuck(float luck)
        {
            int extra = Mathf.FloorToInt(Mathf.Max(0f, luck) / 50f);
            return Mathf.Clamp(3 + extra, 3, 6);
        }
    }
}
