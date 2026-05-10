// File: Assets/_CyberPickle/Code/Gameplay/Progression/UpgradeCardSO.cs
// Namespace: CyberPickle.Gameplay.Progression
//
// One Upgrade Card asset — the unit of choice on the level-up screen.
// Authored as a ScriptableObject so designers can iterate without code
// changes; runtime instances are picked from an UpgradePoolSO and applied
// via PlayerStats.AddModifier when the player picks the card.
//
// Card is intentionally minimal at this stage. As the system matures we'll
// add: 3D preview prefab reference (per GDD §3.11.4), Wwise hover-stinger
// event id (per §3.11.4), element color tint, prerequisites (some cards
// only appear once a related card is owned), per-card synergy hints.
// Those slots are stubbed out below as TODO fields so adding them later
// is a matter of filling values, not refactoring.
//
// Why one SO per card vs a Dictionary on a single SO: addressability,
// inspector-friendliness, and per-card meta tags can be added without
// migrating data. Costs ~1 .asset file per card; with 50+ cards that's
// trivial.

using System.Collections.Generic;
using UnityEngine;
using CyberPickle.Core;
using CyberPickle.Gameplay.Stats;
using CyberPickle.Gameplay.Weapons;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.Gameplay.Progression
{
    // CardRarity enum REMOVED 2026-05-10 — migrated to the unified
    // CyberPickle.Core.Rarity enum (single source of truth for the project).
    // See Assets/_CyberPickle/Code/Core/Rarity.cs for the centralization rule.
    // Byte values are identical (Common=0..Legendary=4) so existing
    // serialized .asset data carries over without conversion.

    /// <summary>
    /// What kind of card this is. Drives how it's applied when picked AND
    /// how it's weighted in the level-up draft pool. Per <c>weapon_rarity_v1.md</c>
    /// §3 Path B, the draft pool offers a mix of types (50% LevelUp / 25%
    /// PowerUp / 15% RarityUp / 10% SkillUnlock as project defaults).
    ///
    /// Stable byte values — DO NOT renumber. Existing card .asset files
    /// default to <see cref="StatModifier"/> (value 0).
    /// </summary>
    public enum CardType : byte
    {
        /// <summary>Applies <c>modifiers[]</c> via <c>PlayerStats.AddModifier</c>. The original card behavior; default for back-compat with existing assets.</summary>
        StatModifier = 0,

        /// <summary>Levels a specific weapon by 1 (or adds it to the loadout at L1 if not yet equipped). References <c>targetWeaponData</c>.</summary>
        LevelUp = 1,

        /// <summary>Applies a power-up of a specific type+element to a weapon. References <c>targetPowerUpId</c>. M9 work — currently a stub that logs only.</summary>
        PowerUp = 2,

        /// <summary>Bumps a specific weapon's rarity by +1 tier (clamped at Legendary). References <c>targetWeaponData</c>.</summary>
        RarityUp = 3,

        /// <summary>Activates a run-scoped skill power (Banish, Lock, Forge access, etc.). References <c>targetSkillId</c>. M11 work — currently a stub that logs only.</summary>
        SkillUnlock = 4,

        /// <summary>Reserved — purely cosmetic cards (skins, palette swaps, kill-confirms). Never affects gameplay.</summary>
        Cosmetic = 5,
    }

    [CreateAssetMenu(menuName = "CyberPickle/Progression/Upgrade Card", fileName = "Card_")]
    public class UpgradeCardSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id for save data, banishment, and run-history tracking. MUST be unique across all cards. Convention: lowercase_snake_case ('power_minor', 'magnet_radius_1').")]
        public string cardId;

        [Tooltip("User-facing name shown on the card UI. Short — fits a card header.")]
        public string displayName;

        [Tooltip("User-facing one-liner. Optional with the new visual-first card UI (per GDD §3.11.4 the icon + 3D preview do most of the talking) but useful as a fallback / accessibility text.")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Sprite icon shown on the card. Will be supplemented by the 3D preview rig (TODO field below).")]
        public Sprite icon;

        [Header("Theming")]
        [Tooltip("Primary card color. Per GDD §3.11.4, color = element. Pure-stat cards use a neutral chrome color (RGB 0.7,0.7,0.75 or similar).")]
        public Color tintColor = new Color(0.7f, 0.7f, 0.75f, 1f);

        [Header("Rarity")]
        [Tooltip("Rarity tier — drives the draw weighting in UpgradePoolSO. " +
                 "Uses the unified CyberPickle.Core.Rarity enum (same byte " +
                 "values as the old CardRarity, so existing card assets carry over).")]
        public Rarity rarity = Rarity.Common;

        [Header("Card Type")]
        [Tooltip("Determines how this card is applied when picked. Default is StatModifier (the original behavior — applies the modifiers[] array). New types: LevelUp / RarityUp target a specific weapon; PowerUp / SkillUnlock are M9-M11 stubs that currently log only.")]
        public CardType cardType = CardType.StatModifier;

        [Header("Targeting (used by LevelUp / RarityUp)")]
        [Tooltip("Weapon this card affects. REQUIRED for LevelUp and RarityUp card types. LevelUp adds the weapon at L1 if not equipped, else levels it by 1. RarityUp bumps the equipped weapon's rarity by 1; if the weapon isn't equipped, the card is a no-op (won't appear in pool — see UpgradePoolSO eligibility filter).")]
        public WeaponData targetWeaponData;

        [Header("Targeting (M9-M11 stubs — wire when those systems land)")]
        [Tooltip("Power-up identifier — used by CardType.PowerUp. M9 work; for now this just logs the intent.")]
        public string targetPowerUpId;

        [Tooltip("Skill node identifier — used by CardType.SkillUnlock. M11 work; for now this just logs the intent.")]
        public string targetSkillId;

        [Header("Effect")]
        [Tooltip("Stat modifiers applied when this card is picked. All modifiers in the array are applied together via PlayerStats.AddModifier.\n\n" +
                 "VALUE CONVENTIONS (read carefully — these have bitten people):\n" +
                 "  • AddBase: a flat amount added to the base. e.g., kind=AddBase, value=1 on MagneticField → +1m radius.\n" +
                 "  • AddPercent: a DECIMAL FRACTION, NOT a percent integer.\n" +
                 "      0.10 = +10% (multiplies by 1.10)\n" +
                 "      0.50 = +50% (multiplies by 1.50)\n" +
                 "      5    = +500% (multiplies by 6) ← almost always a mistake\n" +
                 "      10   = +1000% (multiplies by 11) ← almost always a mistake\n" +
                 "  • MultFinal: a multiplier on the final value. value=1.5 means ×1.5.\n" +
                 "  • Override: replaces the value entirely. value=42 means the stat IS 42.\n\n" +
                 "OnValidate (below) warns at edit time if AddPercent > 1.0 — that's the foot-gun threshold.")]
        public StatModifier[] modifiers;

        // ─── TODO fields (stubbed for forward compatibility) ──────────────

        [Header("TODO — wire as systems mature")]
        [Tooltip("3D model preview prefab shown in the card slot's mini-camera viewport (per GDD §3.11.4). Leave empty for now — placeholder primitives during M7.3 day 3-4 UI work.")]
        public GameObject previewPrefab;

        [Tooltip("Wwise event id for the hover-stinger preview (per GDD §3.11.4). Leave empty until M9 Wwise integration; Stage 0 / 1 use placeholder UI sounds via the MusicEventBus.")]
        public string hoverStingerEventId;

        [Tooltip("Optional list of element tags this card belongs to (for color override + synergy callouts). Leave empty for pure-stat cards.")]
        public List<string> elementTags;

        [Tooltip("Optional list of prerequisite cardIds. If set, this card can only be drawn once ALL listed cards are already owned. Empty = no prerequisites.")]
        public List<string> prerequisiteCardIds;

        // ─── Runtime helpers ──────────────────────────────────────────────

        /// <summary>
        /// SourceId prefix used when applying this card's modifiers to
        /// PlayerStats. The prefix scheme aligns with StatModifier's documented
        /// convention ("run_*" for in-run upgrades). Each modifier's individual
        /// sourceId is the card's id; if the same card is picked twice (multi-stack
        /// upgrades, e.g., +10% Power × N), the modifiers stack additively because
        /// AddPercent is associative — no need to disambiguate stacks.
        /// </summary>
        public string RuntimeSourceId => $"run_{cardId}";

        /// <summary>
        /// Applies this card's modifiers to the supplied PlayerStats instance.
        /// Returns the number of modifiers actually applied (in case any are
        /// invalid / skippable).
        ///
        /// This is the legacy entry point — called only for
        /// <see cref="CardType.StatModifier"/> cards. New card types are
        /// dispatched through <see cref="Apply"/> which routes through
        /// here when appropriate.
        /// </summary>
        public int ApplyTo(PlayerStats stats)
        {
            if (stats == null || modifiers == null) return 0;
            string source = RuntimeSourceId;
            int count = 0;
            for (int i = 0; i < modifiers.Length; i++)
            {
                // Override the per-modifier sourceId at apply-time so authors
                // don't have to remember to set it correctly per card.
                var m = modifiers[i];
                m.sourceId = source;
                stats.AddModifier(m);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Universal apply dispatch. Routes the card's effect based on
        /// <see cref="cardType"/>. Caller passes both PlayerStats (for
        /// stat modifiers) and WeaponLoadoutRuntime (for level/rarity-up
        /// targeting); either may be null for cards that don't need it.
        ///
        /// Returns a short description of what was applied, useful for
        /// the level-up confirmation log and analytics. Empty string
        /// means "nothing applied" (e.g., card targeted a weapon that
        /// isn't equipped, or referenced a stub system).
        /// </summary>
        public string Apply(PlayerStats stats, WeaponLoadoutRuntime loadout)
        {
            switch (cardType)
            {
                case CardType.StatModifier:
                {
                    int applied = ApplyTo(stats);
                    return applied > 0 ? $"applied {applied} modifier(s)" : "no modifiers";
                }

                case CardType.LevelUp:
                {
                    if (loadout == null || targetWeaponData == null) return "no target weapon";
                    var existing = loadout.FindByWeaponData(targetWeaponData);
                    if (existing == null)
                    {
                        // Not yet equipped — add to loadout at Common (the
                        // first-roll could be deferred to RarityRollService
                        // with player Luck if we want richer behavior; for
                        // now LevelUp adds at Common to keep semantics simple).
                        if (loadout.TryAddWeapon(targetWeaponData, Rarity.Common, out var added))
                            return $"added '{added.WeaponId}' to loadout (slot {added.slotIndex})";
                        return "loadout full — add failed";
                    }
                    if (existing.level >= 5) return $"'{existing.WeaponId}' already at L5 (use evolution)";
                    bool ok = loadout.LevelUpWeapon(existing.slotIndex);
                    return ok ? $"'{existing.WeaponId}' L{existing.level - 1} → L{existing.level}" : "level-up failed";
                }

                case CardType.RarityUp:
                {
                    if (loadout == null || targetWeaponData == null) return "no target weapon";
                    var existing = loadout.FindByWeaponData(targetWeaponData);
                    if (existing == null) return "weapon not equipped";
                    if (existing.rarity == Rarity.Legendary) return $"'{existing.WeaponId}' already Legendary";
                    var oldRarity = existing.rarity;
                    bool ok = loadout.UpgradeRarity(existing.slotIndex, 1);
                    return ok ? $"'{existing.WeaponId}' rarity {oldRarity} → {existing.rarity}" : "rarity-up failed";
                }

                case CardType.PowerUp:
                {
                    // M9 stub — when PowerUpData lands, this will:
                    //   1. Look up PowerUpData by targetPowerUpId
                    //   2. Apply its mechanical effect to stats / loadout
                    //   3. If it triggers an evolution, call loadout.EvolveWeapon
                    //      and lock in the power-up's element
                    Debug.Log($"[UpgradeCardSO] PowerUp card '{cardId}' picked (target='{targetPowerUpId}'). Stub — M9 will implement.");
                    return $"[stub] power-up '{targetPowerUpId}'";
                }

                case CardType.SkillUnlock:
                {
                    // M11 stub — when SkillTreeAllocation lands, this will
                    // activate the named run-power (Banish, Lock, Forge access).
                    Debug.Log($"[UpgradeCardSO] SkillUnlock card '{cardId}' picked (target='{targetSkillId}'). Stub — M11 will implement.");
                    return $"[stub] skill '{targetSkillId}'";
                }

                case CardType.Cosmetic:
                    // Cosmetics never affect gameplay.
                    return "cosmetic — no gameplay effect";

                default:
                    return $"unknown card type {cardType}";
            }
        }

        // ─── Editor validation ────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                cardId = name?.ToLower().Replace(" ", "_") ?? "unnamed_card";
            }

            // Foot-gun guard: AddPercent values are decimal fractions
            // (0.10 = +10%). Anything > 1.0 is almost always a designer
            // who typed 10 thinking "10%" — we want to catch that at
            // edit time, not after a player picks a card and finds their
            // stat 10× larger than intended (real bug from M7.3 testing
            // where SpeedMinor stored 5 and ran 6 → 36 instead of 6 → 6.6).
            if (modifiers == null) return;
            for (int i = 0; i < modifiers.Length; i++)
            {
                var m = modifiers[i];
                if (m.kind == ModifierKind.AddPercent && Mathf.Abs(m.value) > 1.0f)
                {
                    Debug.LogWarning(
                        $"[UpgradeCardSO] '{name}' modifier #{i} (target {m.type}) has " +
                        $"AddPercent value={m.value}. AddPercent is a DECIMAL FRACTION " +
                        $"(0.10 = +10%); a value > 1.0 means > +100% which is rarely intentional. " +
                        $"If you meant +{m.value}%, set value={m.value / 100f:F2} instead.",
                        this);
                }
            }
        }
#endif
    }
}
