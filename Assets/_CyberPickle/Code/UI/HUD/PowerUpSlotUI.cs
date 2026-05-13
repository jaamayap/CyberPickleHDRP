// File: Assets/_CyberPickle/Code/UI/HUD/PowerUpSlotUI.cs
// Namespace: CyberPickle.UI.HUD
//
// One power-up slot display in the cross HUD. Mirrors WeaponSlotUI's
// shape but renders PowerUpInstanceData (stat target + magnitude +
// rolled element) instead of weapon data. Surfaces a hover tooltip via
// the same HoverableElement base — keeps the M7.4 tooltip behavior the
// user explicitly asked to preserve in the cross UI.
//
// The data sources read on every refresh / tooltip build:
//   - WeaponLoadoutRuntime.GetPowerUp(axisIndex) for current rarity / element
//   - PowerUpData fields (affectedStat + magnitudesByRarity) for the curve
//   - WeaponLoadoutRuntime.GetSlot(axisIndex) for the coupled weapon name
//     (so the tooltip can describe what this power-up is conferring its
//     element to right now)
//
// Empty axes render with the rarity / element frames dimmed and a faint
// placeholder icon — same convention as WeaponSlotUI so the cross layout
// cell shape stays consistent regardless of fill state.

using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CyberPickle.Core;
using CyberPickle.Gameplay.Weapons;
using CyberPickle.Shop.Equipment.Data;
using CyberPickle.UI.Tooltip;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    public class PowerUpSlotUI : HoverableElement
    {
        [Header("Display")]
        [Tooltip("Headline label — e.g. 'Power Boost (Rare)'. '—' for empty. Required.")]
        [SerializeField] private TextMeshProUGUI labelText;

        [Tooltip("Compact stat readout — e.g. '+12% Power'. Optional.")]
        [SerializeField] private TextMeshProUGUI statText;

        [Tooltip("Image tinted by the power-up's rarity (frame, glow, fill — designer's call). Optional.")]
        [SerializeField] private Image rarityFrame;

        [Tooltip("Image tinted by the rolled element. Optional.")]
        [SerializeField] private Image elementFrame;

        [Tooltip("Image showing the power-up's sprite icon (from PowerUpData.equipmentIcon). Hidden when slot is empty. Optional.")]
        [SerializeField] private Image iconImage;

        // Axis index is set by the parent LoadoutCrossPanel based on array position.
        private int _axisIndex;

        public void SetAxisIndex(int idx) => _axisIndex = idx;

        // ─── Refresh from current loadout state ───────────────────────────

        public void Refresh(PowerUpInstanceData instance)
        {
            bool valid = instance != null && instance.IsValid;

            if (labelText != null)
            {
                labelText.text = valid
                    ? $"{instance.powerUpData.displayName} ({instance.rarity.DisplayName()})"
                    : "—";
            }

            if (statText != null)
            {
                if (valid)
                {
                    float magnitude = instance.CurrentMagnitude;
                    string sign = magnitude >= 0 ? "+" : string.Empty;
                    statText.text = $"{sign}{magnitude * 100f:F0}% {instance.powerUpData.affectedStat}";
                }
                else
                {
                    statText.text = string.Empty;
                }
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
                Sprite sprite = (valid && instance.powerUpData != null) ? instance.powerUpData.equipmentIcon : null;
                if (sprite != null)
                {
                    iconImage.sprite = sprite;
                    iconImage.color = Color.white;
                    iconImage.enabled = true;
                }
                else
                {
                    // Empty axis or no icon — faint placeholder so the cell shape stays consistent.
                    iconImage.sprite = null;
                    iconImage.color = new Color(1f, 1f, 1f, 0.10f);
                    iconImage.enabled = true;
                }
            }
        }

        // ─── Tooltip content ──────────────────────────────────────────────

        public override TooltipContent BuildContent()
        {
            var loadout = WeaponLoadoutRuntime.Instance;
            var instance = loadout != null ? loadout.GetPowerUp(_axisIndex) : null;

            if (instance == null || !instance.IsValid)
            {
                // Empty axis — describe what coupling could happen here.
                var coupledWeapon = loadout != null ? loadout.GetSlot(_axisIndex) : null;
                string body;
                if (coupledWeapon != null && coupledWeapon.IsValid)
                {
                    body = $"<i>Empty — no power-up slotted.</i>\n\nA power-up here would confer its element to <b>{coupledWeapon.weaponData.displayName}</b> (currently neutral).";
                }
                else
                {
                    body = "<i>Empty — no power-up slotted.</i>\n\nA power-up here would still apply its global stat boost.";
                }
                return new TooltipContent { title = $"Axis {_axisIndex} — Power-up", body = body };
            }

            var sb = new StringBuilder(384);
            string rarityHex = ColorUtility.ToHtmlStringRGB(instance.rarity.DisplayColor());
            string elementHex = ColorUtility.ToHtmlStringRGB(instance.element.DisplayColor());

            sb.AppendLine($"<color=#{rarityHex}>{instance.rarity.DisplayName()}</color>  •  L{instance.level}");
            if (instance.element != ElementId.None)
                sb.AppendLine($"<color=#{elementHex}>{instance.element.DisplayName()}</color>");
            sb.AppendLine();

            // Global stat boost.
            float magnitude = instance.CurrentMagnitude;
            sb.AppendLine("<b>Global Effect</b>");
            sb.AppendLine($"<color=#88c8ff>{instance.powerUpData.affectedStat}</color>  +{magnitude * 100f:F0}%  <size=80%>(applies to all weapons)</size>");

            // Element coupling.
            if (instance.element != ElementId.None)
            {
                sb.AppendLine();
                sb.AppendLine("<b>Element Coupling</b>");
                var coupledWeapon = loadout.GetSlot(_axisIndex);
                if (coupledWeapon != null && coupledWeapon.IsValid)
                {
                    sb.AppendLine($"Confers <color=#{elementHex}>{instance.element.DisplayName()}</color> to <b>{coupledWeapon.weaponData.displayName}</b> on this axis.");
                }
                else
                {
                    sb.AppendLine($"<size=80%><i>No weapon on this axis — element will apply once a weapon is slotted.</i></size>");
                }
            }

            // Rarity progression preview (so the player sees the curve).
            if (instance.powerUpData.magnitudesByRarity != null && instance.powerUpData.magnitudesByRarity.Length == 5)
            {
                sb.AppendLine();
                sb.AppendLine("<b>Rarity Curve</b>");
                var mags = instance.powerUpData.magnitudesByRarity;
                int currentIdx = (int)instance.rarity;
                for (int i = 0; i < 5; i++)
                {
                    string rarityName = ((Rarity)i).DisplayName();
                    string highlight = (i == currentIdx) ? "<b>" : "<color=#888888>";
                    string close     = (i == currentIdx) ? "</b>" : "</color>";
                    sb.AppendLine($"  {highlight}{rarityName} +{mags[i] * 100f:F0}%{close}");
                }
            }

            return new TooltipContent
            {
                title = instance.powerUpData.displayName,
                body  = sb.ToString(),
            };
        }
    }
}
