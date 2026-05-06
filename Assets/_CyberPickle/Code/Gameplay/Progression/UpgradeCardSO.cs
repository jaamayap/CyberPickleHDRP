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
using CyberPickle.Gameplay.Stats;

namespace CyberPickle.Gameplay.Progression
{
    /// <summary>
    /// Rarity tier for level-up cards. Drives draw weighting (Luck stat
    /// modulates the weights — more Luck = higher chance of better rarities).
    /// </summary>
    public enum CardRarity : byte
    {
        Common    = 0,
        Uncommon  = 1,
        Rare      = 2,
        Epic      = 3,
        Legendary = 4,
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
        [Tooltip("Rarity tier — drives the draw weighting in UpgradePoolSO.")]
        public CardRarity rarity = CardRarity.Common;

        [Header("Effect")]
        [Tooltip("Stat modifiers applied when this card is picked. ALL modifiers in the array are applied together via PlayerStats.AddModifier. Their sourceIds are prefixed at apply-time with this card's RuntimeSourceId so they can be batch-removed if the card is ever 'forgotten' (future feature).")]
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

        // ─── Editor validation ────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                cardId = name?.ToLower().Replace(" ", "_") ?? "unnamed_card";
            }
        }
#endif
    }
}
