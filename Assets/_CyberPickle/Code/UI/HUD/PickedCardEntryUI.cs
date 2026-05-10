// File: Assets/_CyberPickle/Code/UI/HUD/PickedCardEntryUI.cs
// Namespace: CyberPickle.UI.HUD
//
// One entry in the PickedCardsPanel — one card the player has picked
// this run. Compact display (name + rarity dot) and a hoverable
// tooltip showing the full card effect.

using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CyberPickle.Core;
using CyberPickle.Gameplay.Progression;
using CyberPickle.Gameplay.Stats;
using CyberPickle.UI.Tooltip;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    public class PickedCardEntryUI : HoverableElement
    {
        [Header("Display")]
        [Tooltip("TMP for the card's display name. Required.")]
        [SerializeField] private TextMeshProUGUI nameText;

        [Tooltip("Image whose color reflects the card's rarity (small dot or accent). Optional.")]
        [SerializeField] private Image rarityDot;

        [Tooltip("TMP for the card's type tag (e.g., 'StatModifier', 'LevelUp', 'RarityUp'). Optional.")]
        [SerializeField] private TextMeshProUGUI typeTag;

        private UpgradeCardSO _card;

        // Picked-card details are static — the card was applied when picked,
        // its modifiers won't change. Tooltip just follows the mouse, no lock.
        public override bool IsLockable => false;

        public void Bind(UpgradeCardSO card)
        {
            _card = card;
            if (card == null) return;

            if (nameText != null)
                nameText.text = !string.IsNullOrEmpty(card.displayName) ? card.displayName : card.cardId;

            if (rarityDot != null)
                rarityDot.color = card.rarity.DisplayColor();

            if (typeTag != null)
                typeTag.text = card.cardType.ToString();
        }

        public override TooltipContent BuildContent()
        {
            if (_card == null) return TooltipContent.Empty;

            var sb = new StringBuilder(256);
            string rarityHex = ColorUtility.ToHtmlStringRGB(_card.rarity.DisplayColor());

            sb.AppendLine($"<color=#{rarityHex}>{_card.rarity.DisplayName()}</color>  •  {_card.cardType}");
            if (!string.IsNullOrEmpty(_card.description))
            {
                sb.AppendLine();
                sb.AppendLine(_card.description);
            }

            // Detail by card type.
            switch (_card.cardType)
            {
                case CardType.StatModifier:
                    AppendModifierList(sb, _card);
                    break;

                case CardType.LevelUp:
                    if (_card.targetWeaponData != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"<b>Targets:</b> {_card.targetWeaponData.displayName}");
                        sb.AppendLine("Adds the weapon at L1 if not equipped, otherwise +1 level.");
                    }
                    break;

                case CardType.RarityUp:
                    if (_card.targetWeaponData != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"<b>Targets:</b> {_card.targetWeaponData.displayName}");
                        sb.AppendLine("Bumps the equipped weapon's rarity by +1 tier.");
                    }
                    break;

                case CardType.PowerUp:
                    sb.AppendLine();
                    sb.AppendLine($"<b>Power-up:</b> <i>{_card.targetPowerUpId}</i>");
                    sb.AppendLine("<size=80%><color=#aaaaaa>(M9 — power-up system not yet wired)</color></size>");
                    break;

                case CardType.SkillUnlock:
                    sb.AppendLine();
                    sb.AppendLine($"<b>Skill:</b> <i>{_card.targetSkillId}</i>");
                    sb.AppendLine("<size=80%><color=#aaaaaa>(M11 — skill tree not yet wired)</color></size>");
                    break;

                case CardType.Cosmetic:
                    sb.AppendLine();
                    sb.AppendLine("<i>Cosmetic — no gameplay effect.</i>");
                    break;
            }

            return new TooltipContent
            {
                title = !string.IsNullOrEmpty(_card.displayName) ? _card.displayName : _card.cardId,
                body  = sb.ToString(),
            };
        }

        private static void AppendModifierList(StringBuilder sb, UpgradeCardSO card)
        {
            if (card.modifiers == null || card.modifiers.Length == 0) return;
            sb.AppendLine();
            sb.AppendLine("<b>Modifiers</b>");
            for (int i = 0; i < card.modifiers.Length; i++)
            {
                var m = card.modifiers[i];
                sb.AppendLine($"  <color=#88c8ff>{m.type}</color>  <color=#aaaaaa>{m.kind}</color>  {FormatModifierValue(m)}");
            }
        }

        private static string FormatModifierValue(StatModifier m)
        {
            switch (m.kind)
            {
                case ModifierKind.AddBase:    return $"+{m.value:F2}";
                case ModifierKind.AddPercent: return (m.value >= 0 ? "+" : "") + $"{m.value * 100f:F0}%";
                case ModifierKind.MultFinal:  return $"×{m.value:F2}";
                case ModifierKind.Override:   return $"= {m.value:F2}";
                default:                      return m.value.ToString("F2");
            }
        }
    }
}
