using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.UI.EquipmentHub.Shop
{
    public class ShopPurchaseConfirmation : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject confirmationPanel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescriptionText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI balanceText;
        [SerializeField] private Image itemIcon;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;

        private Action<bool> currentCallback;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (confirmationPanel != null)
                confirmationPanel.SetActive(false);

            SetupButtons();
        }

        private void SetupButtons()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(() => HandleConfirmation(true));

            if (cancelButton != null)
                cancelButton.onClick.AddListener(() => HandleConfirmation(false));
        }

        public void Show(EquipmentData equipment, float currentBalance, Action<bool> onComplete)
        {
            if (equipment == null)
            {
                onComplete?.Invoke(false);
                return;
            }

            currentCallback = onComplete;
            UpdateDisplay(equipment, currentBalance);
            ShowPanel();
        }

        private void UpdateDisplay(EquipmentData equipment, float currentBalance)
        {
            if (itemNameText != null)
                itemNameText.text = equipment.displayName;

            if (itemDescriptionText != null)
                itemDescriptionText.text = equipment.description;

            int equipmentPrice = equipment.neuralCreditCost > 0 ? equipment.neuralCreditCost : equipment.cyberCoinCost;

            if (priceText != null)
                priceText.text = $"{equipmentPrice:F0}";

            if (balanceText != null)
                balanceText.text = $"Balance: {currentBalance:F0}";

            if (itemIcon != null && equipment.equipmentIcon != null)
                itemIcon.sprite = equipment.equipmentIcon;

            bool canAfford = currentBalance >= equipmentPrice;
            if (confirmButton != null)
                confirmButton.interactable = canAfford;
        }

        private void ShowPanel()
        {
            if (confirmationPanel != null)
                confirmationPanel.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, fadeInDuration);
            }
        }

        private void HidePanel()
        {
            if (canvasGroup != null)
            {
                canvasGroup.DOFade(0f, fadeOutDuration).OnComplete(() =>
                {
                    if (confirmationPanel != null)
                        confirmationPanel.SetActive(false);
                });
            }
            else if (confirmationPanel != null)
            {
                confirmationPanel.SetActive(false);
            }
        }

        private void HandleConfirmation(bool confirmed)
        {
            HidePanel();
            currentCallback?.Invoke(confirmed);
            currentCallback = null;
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }
    }
}
