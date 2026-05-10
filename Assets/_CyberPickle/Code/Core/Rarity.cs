// File: Assets/_CyberPickle/Code/Core/Rarity.cs
// Namespace: CyberPickle.Core
//
// SINGLE SOURCE OF TRUTH for the project's rarity system.
//
// Used by:
//   - Upgrade cards (level-up draft picks)
//   - Weapons (the Rarity axis of the dual-axis Level + Rarity model — see weapon_rarity_v1.md)
//   - Implants                (TBD M10)
//   - Mining Rig parts        (TBD M13 — see economy_design_v1.md §4)
//   - Cosmetics               (TBD)
//   - Boss-drop legendary cards
//   - In-run chest / drop quality
//
// XP Gem Tiers are a SEPARATE numeric concept (Tier 0..4 in EnemyXPDropChances)
// but conceptually aligned — Tier 0 = "Data Fragment" = Common; Tier 4 =
// "Sentinel Core" = Legendary. Use Rarity.XPGemDisplayName() to get the
// gem-flavored display name when needed.
//
// Achievement tiers (Bronze / Silver / Gold / Platinum) are a DIFFERENT
// concept and use their own enum — see progression_design_v1.md §9.
//
// ─── CENTRALIZATION RULE (DO NOT DEVIATE) ─────────────────────────────────
//
//   - All rarity-bearing systems MUST use this enum.
//   - DO NOT define new rarity enums (CardRarity, WeaponRarity, ItemRarity,
//     DropRarity, etc.). These are forbidden by design.
//   - DO NOT shadow this enum in another namespace.
//   - If a system needs a 5-tier item-quality concept, use this enum.
//     If it doesn't, it shouldn't have one.
//
// Why this matters: stat scalars, drop weights, visual treatments (color,
// glow, pity timers, gold flash), audio stings — all share infrastructure
// when rarity is unified. Splitting rarity per-system means re-implementing
// the same logic in N places, which guarantees drift over time.
//
// History:
//   - 2025-XX: CardRarity introduced in UpgradeCardSO.cs (card-only).
//   - 2026-05-10: Centralized as CyberPickle.Core.Rarity. CardRarity
//     removed; UpgradeCardSO + UpgradePoolSO migrated to this enum.
//
// Cross-references:
//   - weapon_rarity_v1.md §2 — damage multipliers per tier (×1.0 → ×4.0)
//   - economy_design_v1.md §4 — Mining Rig part rarity
//   - progression_design_v1.md §9 — Achievement tiers (NOT this enum)

using UnityEngine;

namespace CyberPickle.Core
{
    /// <summary>
    /// Unified rarity tier for all item-quality systems in CyberPickle.
    /// Byte-typed for ECS IComponentData compatibility (1-byte storage in
    /// chunk memory; safe to put in <see cref="Unity.Entities.IComponentData"/>).
    ///
    /// Tier values are stable contracts — DO NOT renumber. They're persisted
    /// in save data, ScriptableObject assets, and ECS chunk data. Renumbering
    /// invalidates all of those.
    /// </summary>
    public enum Rarity : byte
    {
        Common    = 0,
        Uncommon  = 1,
        Rare      = 2,
        Epic      = 3,
        Legendary = 4,
    }

    /// <summary>
    /// Extension methods exposing the canonical numeric / visual / textual
    /// constants associated with each rarity tier. Per the centralization
    /// rule above, these are the ONLY source of these values — system-
    /// specific code reads from here, never re-derives.
    /// </summary>
    public static class RarityExtensions
    {
        // ─── Mechanical scalars ───────────────────────────────────────────

        /// <summary>
        /// Damage multiplier for weapons of this rarity, per weapon_rarity_v1.md §2.
        /// Each tier is a meaningful jump (~30%–60% per step) so 5 tiers feel
        /// distinct in moment-to-moment play.
        ///
        ///   Common    1.0×    (baseline)
        ///   Uncommon  1.3×
        ///   Rare      1.7×
        ///   Epic      2.5×
        ///   Legendary 4.0×    (the brass ring at this axis)
        /// </summary>
        public static float DamageMultiplier(this Rarity r) => r switch
        {
            Rarity.Common    => 1.0f,
            Rarity.Uncommon  => 1.3f,
            Rarity.Rare      => 1.7f,
            Rarity.Epic      => 2.5f,
            Rarity.Legendary => 4.0f,
            _                => 1.0f,
        };

        /// <summary>
        /// Default draw weight for items of this rarity in random draws
        /// (cards, drops, chests). Per-pool tuning lives in the pool SO;
        /// this is the project-wide fallback distribution.
        ///
        /// Sums to 100 for sanity but only ratios matter mathematically.
        ///   Common 60 / Uncommon 25 / Rare 10 / Epic 4 / Legendary 1
        /// </summary>
        public static float BaseDrawWeight(this Rarity r) => r switch
        {
            Rarity.Common    => 60f,
            Rarity.Uncommon  => 25f,
            Rarity.Rare      => 10f,
            Rarity.Epic      =>  4f,
            Rarity.Legendary =>  1f,
            _                =>  0f,
        };

        // ─── Display values ───────────────────────────────────────────────

        /// <summary>
        /// User-facing display name — "Common", "Legendary", etc.
        /// Used in tooltips, log strings, achievement notifications.
        /// </summary>
        public static string DisplayName(this Rarity r) => r switch
        {
            Rarity.Common    => "Common",
            Rarity.Uncommon  => "Uncommon",
            Rarity.Rare      => "Rare",
            Rarity.Epic      => "Epic",
            Rarity.Legendary => "Legendary",
            _                => "?",
        };

        /// <summary>
        /// Cyberpunk-flavored XP gem name corresponding to this rarity tier.
        /// Maps EnemyXPDropChances tiers (0..4) onto user-facing names.
        /// Used by HUD pickup notifications and the gem display, NOT by
        /// other item types — weapons / cards / etc. use <see cref="DisplayName"/>.
        /// </summary>
        public static string XPGemDisplayName(this Rarity r) => r switch
        {
            Rarity.Common    => "Data Fragment",
            Rarity.Uncommon  => "Code Crystal",
            Rarity.Rare      => "Neural Shard",
            Rarity.Epic      => "Synth Spark",
            Rarity.Legendary => "Sentinel Core",
            _                => "?",
        };

        /// <summary>
        /// Canonical UI accent color per tier. Card frames, weapon icons,
        /// chest flashes, drop notifications — all read from here so the
        /// visual language is consistent.
        ///
        /// Designers may override per-asset (e.g., element-tinted cards),
        /// but defaults come from here.
        ///
        /// Rationale:
        ///   Common    — neutral grey   (calm; common drops shouldn't pop)
        ///   Uncommon  — green          (subtle quality)
        ///   Rare      — blue           (clear quality)
        ///   Epic      — purple         (loud quality)
        ///   Legendary — gold           (dopamine peak; pair with screen flash + audio sting)
        /// </summary>
        public static Color DisplayColor(this Rarity r) => r switch
        {
            Rarity.Common    => new Color(0.70f, 0.70f, 0.75f, 1f),  // grey
            Rarity.Uncommon  => new Color(0.30f, 0.85f, 0.30f, 1f),  // green
            Rarity.Rare      => new Color(0.30f, 0.55f, 0.95f, 1f),  // blue
            Rarity.Epic      => new Color(0.65f, 0.30f, 0.95f, 1f),  // purple
            Rarity.Legendary => new Color(1.00f, 0.78f, 0.20f, 1f),  // gold
            _                => Color.white,
        };

        /// <summary>
        /// Whether this rarity should trigger the "celebration moment" —
        /// slow-mo, screen flash, audio sting. Used by drop systems and
        /// card draws to gate the dopamine reaction.
        ///
        /// Currently Epic and Legendary only. Tunable later per-system if
        /// needed (e.g., a system might choose Legendary-only).
        /// </summary>
        public static bool IsCelebrated(this Rarity r) =>
            r == Rarity.Epic || r == Rarity.Legendary;
    }
}
