// File: Assets/_CyberPickle/Code/Shop/Equipment/Data/PowerUpData.cs
// Namespace: CyberPickle.Shop.Equipment.Data
//
// Defines a power-up template — the design data for one type of stat
// boost. Each power-up has TWO orthogonal characteristics:
//
//   1. Stat type + magnitude curve (authored on the SO)
//        Which PlayerStatType this power-up boosts, and by how much at
//        each rarity tier. Magnitudes are decimal fractions (0.10 = +10%)
//        per the AddPercent foot-gun rule (CLAUDE.md).
//
//   2. Element (rolled at draft time)
//        Which of the 7 elements this card-instance carries. Same
//        template ("Fire-Rate Boost") shows up as Fire / Lightning /
//        Ice / etc. variants in the draft pool. The element confers to
//        the WEAPON on the same loadout axis when slotted (and only
//        applies if a weapon is present).
//
// The stat bonus is GLOBAL — applies to all weapons regardless of which
// axis the power-up sits on. Element coupling is LOCAL to the axis.
//
// 2026-05-10 refactor (M8): the previous shape (effectType,
// baseDuration / baseCooldown, GetXForLevel multipliers, IsCompatibleWithWeapon)
// was the "old amulet/synergy" model dropped in GDD V0.7. All replaced
// by: PlayerStatType + 5-element magnitudesByRarity[]. Element no longer
// authored — rolled at draft time per weapon_rarity_v1.md.

using UnityEngine;
using CyberPickle.Core;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Gameplay.Stats;

namespace CyberPickle.Shop.Equipment.Data
{
    /// <summary>
    /// ScriptableObject defining one power-up template. The asset stores
    /// the stat target + per-rarity magnitude curve; the element is rolled
    /// at draft time onto a runtime <c>PowerUpInstanceData</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "PowerUp", menuName = "CyberPickle/Equipment/PowerUpData")]
    public class PowerUpData : EquipmentData
    {
        [Header("Stat Boost (the GLOBAL effect applied to all weapons)")]
        [Tooltip("Which player stat this power-up boosts. The bonus is global — it applies to every weapon, not just the weapon on the same axis. Element coupling (which IS per-axis) is rolled at draft time and lives on the runtime PowerUpInstanceData, not here.")]
        public PlayerStatType affectedStat = PlayerStatType.Power;

        [Tooltip(
            "Magnitude per rarity tier as a DECIMAL FRACTION (0.10 = +10%).\n" +
            "Index 0 = Common, 1 = Uncommon, 2 = Rare, 3 = Epic, 4 = Legendary.\n\n" +
            "Per-stat curves are intentional: a Fire-Rate power-up at Legendary " +
            "(+25%) is fine, but a Crit-Chance power-up at Legendary (+25%) is " +
            "absurd. Each stat gets its own progression authored independently.\n\n" +
            "If unset, defaults to {0.05, 0.08, 0.12, 0.18, 0.25} — a reasonable " +
            "starting curve. Override per asset for stat-specific tuning.")]
        public float[] magnitudesByRarity = new float[]
        {
            0.05f, // Common
            0.08f, // Uncommon
            0.12f, // Rare
            0.18f, // Epic
            0.25f, // Legendary
        };

        [Header("Visual & Audio (optional)")]
        [Tooltip("VFX prefab spawned briefly when this power-up is slotted onto an axis. Optional — placeholder visuals work for prototyping.")]
        public GameObject slotEffectPrefab;

        [Tooltip("Sound played when this power-up is slotted. Optional.")]
        public AudioClip slotSound;

        // ─── API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the magnitude (decimal fraction; 0.10 = +10%) for the
        /// given rarity tier. Falls back to a sensible default if the
        /// authored array is the wrong length.
        /// </summary>
        public float GetMagnitudeForRarity(Rarity rarity)
        {
            int idx = (int)rarity;
            if (magnitudesByRarity != null && idx >= 0 && idx < magnitudesByRarity.Length)
                return magnitudesByRarity[idx];

            // Defensive fallback if the asset wasn't migrated to the new
            // 5-entry format (e.g., post-refactor, before re-author).
            return rarity switch
            {
                Rarity.Common    => 0.05f,
                Rarity.Uncommon  => 0.08f,
                Rarity.Rare      => 0.12f,
                Rarity.Epic      => 0.18f,
                Rarity.Legendary => 0.25f,
                _                => 0.05f,
            };
        }

        /// <summary>
        /// Returns the stats this power-up would impart at the given rarity,
        /// for tooltip / hover display. The "Level" parameter is unused for
        /// now — power-up scaling is rarity-only in the M8 model. Kept on
        /// the override for EquipmentData parity.
        /// </summary>
        public override StatDescriptor[] GetStatsForLevel(int upgradeLevel)
        {
            // 2026-05-10: rarity is the runtime axis, not authored on the asset.
            // For preview-purposes (e.g., the equipment hub showing what a card
            // *could* roll), we display the Common-tier magnitude as the
            // baseline. Actual in-run cards display the rolled rarity's value.
            float magnitude = GetMagnitudeForRarity(Rarity.Common);
            return new[]
            {
                new StatDescriptor(
                    affectedStat.ToString(),
                    magnitude * 100f, // display as percentage
                    isPercentage: true,
                    higherIsBetter: true),
            };
        }

        // ─── Editor validation ────────────────────────────────────────────

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            slotType = EquipmentSlotType.PowerUp;
            base.OnValidate();

            // Magnitudes are decimal fractions — anything > 1.0 is almost
            // always a designer who typed "10" thinking "10%". Catch at
            // edit time. Same foot-gun guard as UpgradeCardSO.OnValidate.
            if (magnitudesByRarity == null) return;
            for (int i = 0; i < magnitudesByRarity.Length; i++)
            {
                if (Mathf.Abs(magnitudesByRarity[i]) > 1.0f)
                {
                    Debug.LogWarning(
                        $"[PowerUpData] '{name}' magnitudesByRarity[{i}]={magnitudesByRarity[i]}. " +
                        $"This is a DECIMAL FRACTION (0.10 = +10%); a value > 1.0 means > +100%, " +
                        $"which is rarely intentional. If you meant +{magnitudesByRarity[i]}%, " +
                        $"set the value to {magnitudesByRarity[i] / 100f:F2} instead.",
                        this);
                }
            }

            if (magnitudesByRarity.Length != 5)
            {
                Debug.LogWarning(
                    $"[PowerUpData] '{name}' magnitudesByRarity has {magnitudesByRarity.Length} entries; " +
                    $"5 are expected (one per rarity tier Common..Legendary). The runtime will fall " +
                    $"back to the default curve for missing entries.",
                    this);
            }
        }
#endif
    }
}
