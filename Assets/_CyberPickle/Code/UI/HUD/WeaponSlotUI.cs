// File: Assets/_CyberPickle/Code/UI/HUD/WeaponSlotUI.cs
// Namespace: CyberPickle.UI.HUD
//
// One weapon-slot display in the HUD. Shows compact summary (level +
// rarity + name + DPS) and surfaces a detailed tooltip on hover via the
// HoverableElement base.
//
// Data sources read on every refresh / tooltip build:
//   - WeaponLoadoutRuntime.GetSlot(slotIndex) — current Level/Rarity/Element
//   - WeaponData fields via the instance's weaponData ref
//   - PerWeaponStatsTracker.GetStats(weaponId) — live DPS / damage / kills
//
// Empty slots render as "—". The HoverableElement still works — the
// tooltip will simply describe the slot as unequipped.

using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CyberPickle.Core;
using CyberPickle.Gameplay.Combat;
using CyberPickle.Gameplay.Player;
using CyberPickle.Gameplay.Stats;
using CyberPickle.Gameplay.Weapons;
using CyberPickle.Shop.Equipment.Data;
using CyberPickle.UI.Tooltip;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    public class WeaponSlotUI : HoverableElement
    {
        [Header("Display")]
        [Tooltip("Headline label — typically '[L2 Legendary] Plasma Lance' or '—' for empty. Required.")]
        [SerializeField] private TextMeshProUGUI labelText;

        [Tooltip("Live DPS readout — e.g. 'DPS 28.4'. Optional — leave empty for compact layouts.")]
        [SerializeField] private TextMeshProUGUI dpsText;

        [Tooltip("Image whose color is tinted by the current weapon's rarity (frame, glow, fill — designer's call). Optional.")]
        [SerializeField] private Image rarityFrame;

        [Tooltip("Image whose color is tinted by the current weapon's element. Optional — leave null if your design folds element into the rarity frame.")]
        [SerializeField] private Image elementFrame;

        [Tooltip("Image showing the weapon's sprite icon (from WeaponData.equipmentIcon). Hidden when slot is empty or weapon has no icon assigned. Optional.")]
        [SerializeField] private Image iconImage;

        // Slot index is set by the parent WeaponSlotsPanel based on array position.
        private int _slotIndex;

        public void SetSlotIndex(int idx) => _slotIndex = idx;

        // ─── Refresh from current loadout state ───────────────────────────

        public void Refresh(WeaponInstanceData instance)
        {
            bool valid = instance != null && instance.IsValid;

            if (labelText != null)
            {
                labelText.text = valid
                    ? $"L{instance.level}{(instance.evolved ? "E" : "")} {instance.rarity.DisplayName()} | {instance.weaponData.displayName}"
                    : "—";
            }

            if (dpsText != null)
            {
                dpsText.text = valid ? FormatLiveDps(instance) : string.Empty;
            }

            if (rarityFrame != null)
            {
                rarityFrame.color = valid ? instance.rarity.DisplayColor() : new Color(0.3f, 0.3f, 0.3f, 1f);
            }

            if (elementFrame != null)
            {
                elementFrame.color = valid ? instance.element.DisplayColor() : new Color(0.3f, 0.3f, 0.3f, 1f);
            }

            if (iconImage != null)
            {
                Sprite sprite = (valid && instance.weaponData != null) ? instance.weaponData.equipmentIcon : null;
                if (sprite != null)
                {
                    iconImage.sprite = sprite;
                    iconImage.color = Color.white;
                    iconImage.enabled = true;
                }
                else
                {
                    // Empty slot or no icon — show a faint placeholder so the layout cell stays consistent.
                    iconImage.sprite = null;
                    iconImage.color = new Color(1f, 1f, 1f, 0.10f);
                    iconImage.enabled = true;
                }
            }
        }

        private static string FormatLiveDps(WeaponInstanceData instance)
        {
            var tracker = PerWeaponStatsTracker.Instance;
            if (tracker == null) return string.Empty;
            var stats = tracker.GetStats(instance.WeaponId);
            return stats != null ? $"DPS {stats.RollingDps:F1}" : "DPS —";
        }

        // ─── Tooltip content ──────────────────────────────────────────────

        public override TooltipContent BuildContent()
        {
            var loadout = WeaponLoadoutRuntime.Instance;
            var instance = loadout != null ? loadout.GetSlot(_slotIndex) : null;

            if (instance == null || !instance.IsValid)
            {
                return new TooltipContent
                {
                    title = $"Slot {_slotIndex}",
                    body  = "<i>Empty — no weapon equipped.</i>",
                };
            }

            var sb = new StringBuilder(512);
            string rarityHex = ColorUtility.ToHtmlStringRGB(instance.rarity.DisplayColor());
            string elementHex = ColorUtility.ToHtmlStringRGB(instance.element.DisplayColor());

            sb.AppendLine($"<color=#{rarityHex}>{instance.rarity.DisplayName()}</color>  •  L{instance.level}{(instance.evolved ? " (Evolved)" : "")}");
            sb.AppendLine($"<color=#{elementHex}>{instance.element.DisplayName()}</color>");
            sb.AppendLine();
            sb.AppendLine("<b>Stats</b>");

            // Read live PlayerStats for the damage + fire rate breakdown.
            var playerStats = Object.FindFirstObjectByType<PlayerStats>();
            float power      = playerStats != null ? playerStats.Get(PlayerStatType.Power)      : 0f;
            float critChance = playerStats != null ? playerStats.Get(PlayerStatType.CritChance) : 0f;
            float dex        = playerStats != null ? playerStats.Get(PlayerStatType.Dexterity)  : 0f;

            float baseDmg     = instance.weaponData.baseDamage;
            float rarityMul   = instance.rarity.DamageMultiplier();
            float powerMul    = 1f + power * 0.01f;
            const float critMul = 2f;
            float perShotNoCrit = baseDmg * rarityMul * powerMul;
            float perShotCrit   = perShotNoCrit * critMul;

            // Compact damage breakdown.
            sb.AppendLine($"<color=#ffd66e>{perShotNoCrit:F1}</color> dmg/shot  <size=80%>(base {baseDmg:F0} × {rarityMul:F2} × Power {powerMul:F2})</size>");
            if (critChance > 0f)
                sb.AppendLine($"<color=#ffaaaa>{perShotCrit:F1}</color> on crit  <size=80%>({critChance * 100f:F0}% chance)</size>");

            // Pattern-driven fire rate.
            float fireRate = instance.weaponData.GetFireRateForLevel(instance.level, dex);
            float bpm      = instance.weaponData.ComputeBPM(dex);
            int activeCells = (instance.weaponData.activeCellsPerLevel != null && instance.weaponData.activeCellsPerLevel.Length > 0)
                ? instance.weaponData.activeCellsPerLevel[Mathf.Clamp(instance.level - 1, 0, instance.weaponData.activeCellsPerLevel.Length - 1)]
                : 0;
            sb.AppendLine($"{fireRate:F2}/s  <size=80%>({activeCells} cells / {instance.weaponData.barCount} bars @ {bpm:F0} BPM, Dex {dex:F0})</size>");

            float avgDmg = perShotNoCrit * (1f - critChance) + perShotCrit * critChance;
            float expectedDps = avgDmg * fireRate;
            sb.AppendLine($"<b>DPS</b>  <color=#ffd66e>{expectedDps:F1}</color>");

            // Per-tier bonus perk (if authored on the WeaponData).
            var perk = instance.weaponData.GetPerkForRarity(instance.rarity);
            if (perk != null)
            {
                sb.AppendLine();
                sb.AppendLine($"<b>{instance.rarity.DisplayName()}</b> {perk.title}");
            }

            // Live run stats — compact.
            var tracker = PerWeaponStatsTracker.Instance;
            var live = tracker != null ? tracker.GetStats(instance.WeaponId) : null;
            if (live != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Run: <b>{live.TotalKills}</b> kills · {live.TotalHits} hits · {live.RollingDps:F1} DPS");
            }

            return new TooltipContent
            {
                title = instance.weaponData.displayName,
                body  = sb.ToString(),
            };
        }
    }
}
