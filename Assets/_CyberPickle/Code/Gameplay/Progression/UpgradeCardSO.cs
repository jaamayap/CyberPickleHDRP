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
    /// §3 Path B, the draft pool offers a mix of types and the loadout-aware
    /// filter in <see cref="UpgradePoolSO"/> hides any card that wouldn't
    /// land on a valid target (e.g., NewWeapon when all weapon slots are
    /// full, LevelUpWeapon when the target weapon isn't equipped).
    ///
    /// Stable byte values — DO NOT renumber. Existing card .asset files
    /// default to <see cref="StatModifier"/> (value 0).
    ///
    /// 2026-05-11 (M8 step 2): added NewWeapon / LevelUpPowerUp /
    /// RarityUpPowerUp. Renamed LevelUp → LevelUpWeapon and RarityUp →
    /// RarityUpWeapon for clarity (byte values unchanged so existing
    /// assets continue to deserialize correctly). PowerUp (byte 2) was a
    /// stub; repurposed as NewPowerUp now that PowerUpData has a real
    /// shape.
    /// </summary>
    public enum CardType : byte
    {
        /// <summary>Applies <c>modifiers[]</c> via <c>PlayerStats.AddModifier</c>. The "Buff" card type — global stat boost, no slot picker. Default for back-compat with existing assets.</summary>
        StatModifier = 0,

        /// <summary>Levels a specific equipped weapon by 1. References <c>targetWeaponData</c>. Filtered out by the pool if the target isn't equipped or already at L5 (use Evolve for L5 → Evolved).</summary>
        LevelUpWeapon = 1,

        /// <summary>Adds a power-up to a chosen empty axis. Slot-picker required. References <c>targetPowerUpData</c>. Element + Rarity rolled at draft time onto the <c>DraftedCard</c> wrapper.</summary>
        NewPowerUp = 2,

        /// <summary>Bumps a specific equipped weapon's rarity by +1 tier (clamped at Legendary). References <c>targetWeaponData</c>. Filtered out by the pool if the target isn't equipped or already Legendary.</summary>
        RarityUpWeapon = 3,

        /// <summary>Activates a run-scoped skill power (Banish, Lock, Forge access, etc.). References <c>targetSkillId</c>. M11 work — currently a stub that logs only.</summary>
        SkillUnlock = 4,

        /// <summary>Reserved — purely cosmetic cards (skins, palette swaps, kill-confirms). Never affects gameplay.</summary>
        Cosmetic = 5,

        /// <summary>Adds a new weapon to a chosen empty axis. Slot-picker required. References <c>targetWeaponData</c>. Filtered out by the pool if all weapon axes are full. Rarity rolled at draft time.</summary>
        NewWeapon = 6,

        /// <summary>Levels a specific equipped power-up by 1. References <c>targetPowerUpData</c>. Filtered out by the pool if the target isn't equipped or already at L5.</summary>
        LevelUpPowerUp = 7,

        /// <summary>Bumps a specific equipped power-up's rarity by +1 tier. References <c>targetPowerUpData</c>. Filtered out by the pool if the target isn't equipped or already Legendary.</summary>
        RarityUpPowerUp = 8,
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

        [Header("Targeting — Weapon")]
        [Tooltip("Weapon this card affects. REQUIRED for NewWeapon / LevelUpWeapon / RarityUpWeapon. The pool filter ensures the card only appears when its target is appropriate (equipped & below cap for level/rarity-up, empty axis available for new-weapon).")]
        public WeaponData targetWeaponData;

        [Header("Targeting — Power-up")]
        [Tooltip("Power-up this card affects. REQUIRED for NewPowerUp / LevelUpPowerUp / RarityUpPowerUp. For NewPowerUp, the rolled element is set at draft time and lives on the DraftedCard wrapper, not on the asset.")]
        public PowerUpData targetPowerUpData;

        [Header("Targeting — Skill (M11 stub)")]
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
        /// stat modifiers) and WeaponLoadoutRuntime (for weapon / power-up
        /// targeting); either may be null for cards that don't need it.
        ///
        /// For cards with <c>RequiresSlotSelection == true</c> (NewWeapon,
        /// NewPowerUp), this method picks the FIRST EMPTY axis. Use
        /// <see cref="ApplyToAxis"/> for the slot-picker UI flow where
        /// the player chose a specific axis.
        ///
        /// For NewPowerUp specifically, the rolled element is needed —
        /// callers without a rolled element (test code, retro-applies)
        /// pass <see cref="ElementId.None"/>. Production callers go
        /// through <see cref="ApplyToAxis"/> with the DraftedCard's value.
        ///
        /// Returns a short description of what was applied, useful for
        /// the level-up confirmation log and analytics.
        /// </summary>
        public string Apply(PlayerStats stats, WeaponLoadoutRuntime loadout)
            => ApplyToAxis(stats, loadout, axisIndex: -1, rolledElement: ElementId.None, rolledRarity: rarity, resolvedWeaponOverride: null, resolvedPowerUpOverride: null);

        /// <summary>
        /// Slot-picker-aware apply. Used by the level-up screen flow when
        /// the card kind requires the player to pick an axis (NewWeapon,
        /// NewPowerUp). Pre-targeted cards (LevelUpWeapon, RarityUpWeapon,
        /// LevelUpPowerUp, RarityUpPowerUp, StatModifier, SkillUnlock,
        /// Cosmetic) ignore <paramref name="axisIndex"/>.
        ///
        /// <paramref name="axisIndex"/> &lt; 0 means "first empty axis"
        /// (auto-pick fallback for non-UI callers).
        ///
        /// <paramref name="resolvedWeaponOverride"/> — when non-null,
        /// supersedes the SO's <see cref="targetWeaponData"/>. Used by
        /// TEMPLATE cards (asset has <c>targetWeaponData = null</c>); the
        /// pool's draft logic resolves a concrete weapon at draft time and
        /// stuffs it on the DraftedCard. LevelUpCoordinator passes it here
        /// when applying.
        ///
        /// <paramref name="resolvedPowerUpOverride"/> — same idea for
        /// power-up templates.
        /// </summary>
        public string ApplyToAxis(PlayerStats stats, WeaponLoadoutRuntime loadout, int axisIndex, ElementId rolledElement, Rarity rolledRarity,
                                  WeaponData resolvedWeaponOverride = null, PowerUpData resolvedPowerUpOverride = null)
        {
            // Effective targets: prefer resolved (template) over authored (specific).
            WeaponData effectiveWeapon  = resolvedWeaponOverride != null ? resolvedWeaponOverride : targetWeaponData;
            PowerUpData effectivePowerUp = resolvedPowerUpOverride != null ? resolvedPowerUpOverride : targetPowerUpData;

            switch (cardType)
            {
                case CardType.StatModifier:
                {
                    int applied = ApplyTo(stats);
                    return applied > 0 ? $"applied {applied} modifier(s)" : "no modifiers";
                }

                case CardType.NewWeapon:
                {
                    if (loadout == null || effectiveWeapon == null) return "no target weapon";
                    bool ok = axisIndex >= 0
                        ? loadout.TryAddWeaponAt(axisIndex, effectiveWeapon, rolledRarity, out var addedAt)
                        : loadout.TryAddWeapon(effectiveWeapon, rolledRarity, out addedAt);
                    return ok ? $"added '{addedAt.WeaponId}' to axis {addedAt.slotIndex} at {rolledRarity}"
                              : "no empty weapon axis";
                }

                case CardType.LevelUpWeapon:
                {
                    if (loadout == null || effectiveWeapon == null) return "no target weapon";
                    var existing = loadout.FindByWeaponData(effectiveWeapon);
                    if (existing == null) return "weapon not equipped (filter bug)";
                    if (existing.level >= 5) return $"'{existing.WeaponId}' already at L5";
                    bool ok = loadout.LevelUpWeapon(existing.slotIndex);
                    return ok ? $"'{existing.WeaponId}' L{existing.level - 1} → L{existing.level}" : "level-up failed";
                }

                case CardType.RarityUpWeapon:
                {
                    if (loadout == null || effectiveWeapon == null) return "no target weapon";
                    var existing = loadout.FindByWeaponData(effectiveWeapon);
                    if (existing == null) return "weapon not equipped (filter bug)";
                    if (existing.rarity == Rarity.Legendary) return $"'{existing.WeaponId}' already Legendary";
                    var oldRarity = existing.rarity;
                    bool ok = loadout.UpgradeRarity(existing.slotIndex, 1);
                    return ok ? $"'{existing.WeaponId}' rarity {oldRarity} → {existing.rarity}" : "rarity-up failed";
                }

                case CardType.NewPowerUp:
                {
                    if (loadout == null || effectivePowerUp == null) return "no target power-up";
                    bool ok = axisIndex >= 0
                        ? loadout.TryAddPowerUpAt(axisIndex, effectivePowerUp, rolledElement, rolledRarity, out var addedPU)
                        : loadout.TryAddPowerUp(effectivePowerUp, rolledElement, rolledRarity, out addedPU);
                    return ok ? $"added power-up '{addedPU.PowerUpId}' to axis {addedPU.axisIndex} at {rolledRarity}/{rolledElement}"
                              : "no empty power-up axis";
                }

                case CardType.LevelUpPowerUp:
                {
                    if (loadout == null || effectivePowerUp == null) return "no target power-up";
                    var existing = loadout.FindByPowerUpData(effectivePowerUp);
                    if (existing == null) return "power-up not equipped (filter bug)";
                    if (existing.level >= 5) return $"'{existing.PowerUpId}' already at L5";
                    bool ok = loadout.LevelUpPowerUp(existing.axisIndex);
                    return ok ? $"'{existing.PowerUpId}' L{existing.level - 1} → L{existing.level}" : "level-up failed";
                }

                case CardType.RarityUpPowerUp:
                {
                    if (loadout == null || effectivePowerUp == null) return "no target power-up";
                    var existing = loadout.FindByPowerUpData(effectivePowerUp);
                    if (existing == null) return "power-up not equipped (filter bug)";
                    if (existing.rarity == Rarity.Legendary) return $"'{existing.PowerUpId}' already Legendary";
                    var oldRarity = existing.rarity;
                    bool ok = loadout.UpgradePowerUpRarity(existing.axisIndex, 1);
                    return ok ? $"'{existing.PowerUpId}' rarity {oldRarity} → {existing.rarity}" : "rarity-up failed";
                }

                case CardType.SkillUnlock:
                {
                    Debug.Log($"[UpgradeCardSO] SkillUnlock card '{cardId}' picked (target='{targetSkillId}'). Stub — M11 will implement.");
                    return $"[stub] skill '{targetSkillId}'";
                }

                case CardType.Cosmetic:
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
