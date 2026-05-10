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

        private DraftedCard _drafted;

        // Picked-card details are static — the card was applied when picked,
        // its modifiers won't change. Tooltip just follows the mouse, no lock.
        public override bool IsLockable => false;

        public void Bind(DraftedCard drafted)
        {
            _drafted = drafted;
            if (!drafted.IsValid) return;

            var card = drafted.source;

            if (nameText != null)
                nameText.text = !string.IsNullOrEmpty(card.displayName) ? card.displayName : card.cardId;

            // Rarity dot uses the ROLLED rarity (the actual one applied),
            // not the asset's authored rarity. Same for the visual tint
            // on power-up cards via element coupling — element overrides
            // tint per chat 2026-05-11.
            if (rarityDot != null)
            {
                rarityDot.color = drafted.rolledElement != ElementId.None
                    ? drafted.rolledElement.DisplayColor()
                    : drafted.rolledRarity.DisplayColor();
            }

            if (typeTag != null)
                typeTag.text = card.cardType.ToString();
        }

        public override TooltipContent BuildContent()
        {
            if (!_drafted.IsValid) return TooltipContent.Empty;
            var card = _drafted.source;

            var sb = new StringBuilder(256);
            string rarityHex = ColorUtility.ToHtmlStringRGB(_drafted.rolledRarity.DisplayColor());

            sb.AppendLine($"<color=#{rarityHex}>{_drafted.rolledRarity.DisplayName()}</color>  •  {card.cardType}");
            if (_drafted.rolledElement != ElementId.None)
            {
                string elementHex = ColorUtility.ToHtmlStringRGB(_drafted.rolledElement.DisplayColor());
                sb.AppendLine($"<color=#{elementHex}>{_drafted.rolledElement.DisplayName()}</color>");
            }
            if (!string.IsNullOrEmpty(card.description))
            {
                sb.AppendLine();
                sb.AppendLine(card.description);
            }

            // Detail by card type.
            switch (card.cardType)
            {
                case CardType.StatModifier:
                    AppendModifierList(sb, card);
                    break;

                case CardType.NewWeapon:
                    if (card.targetWeaponData != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"<b>Adds:</b> {card.targetWeaponData.displayName}");
                        sb.AppendLine($"At rarity <b>{_drafted.rolledRarity}</b>.");
                    }
                    break;

                case CardType.LevelUpWeapon:
                    if (card.targetWeaponData != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"<b>Levels:</b> {card.targetWeaponData.displayName} +1");
                    }
                    break;

                case CardType.RarityUpWeapon:
                    if (card.targetWeaponData != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"<b>Rarity Up:</b> {card.targetWeaponData.displayName}");
                    }
                    break;

                case CardType.NewPowerUp:
                    if (card.targetPowerUpData != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"<b>Adds power-up:</b> {card.targetPowerUpData.displayName}");
                        sb.AppendLine($"Stat: <b>{card.targetPowerUpData.affectedStat}</b> at <b>{_drafted.rolledRarity}</b>.");
                        if (_drafted.rolledElement != ElementId.None)
                            sb.AppendLine($"Confers <b>{_drafted.rolledElement}</b> to the weapon on the same axis.");
                    }
                    break;

                case CardType.LevelUpPowerUp:
                    if (card.targetPowerUpData != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"<b>Levels power-up:</b> {card.targetPowerUpData.displayName} +1");
                    }
                    break;

                case CardType.RarityUpPowerUp:
                    if (card.targetPowerUpData != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"<b>Rarity Up power-up:</b> {card.targetPowerUpData.displayName}");
                    }
                    break;

                case CardType.SkillUnlock:
                    sb.AppendLine();
                    sb.AppendLine($"<b>Skill:</b> <i>{card.targetSkillId}</i>");
                    sb.AppendLine("<size=80%><color=#aaaaaa>(M11 — skill tree not yet wired)</color></size>");
                    break;

                case CardType.Cosmetic:
                    sb.AppendLine();
                    sb.AppendLine("<i>Cosmetic — no gameplay effect.</i>");
                    break;
            }

            return new TooltipContent
            {
                title = !string.IsNullOrEmpty(card.displayName) ? card.displayName : card.cardId,
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
