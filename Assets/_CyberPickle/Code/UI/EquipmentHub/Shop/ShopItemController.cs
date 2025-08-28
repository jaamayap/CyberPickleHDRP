using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using CyberPickle.Shop.Equipment.Data;
using CyberPickle.Shop.Currency;
using CyberPickle.UI.EquipmentHub.DragDrop;
using CyberPickle.Core.Services.Authentication.Data;

namespace CyberPickle.UI.EquipmentHub.Shop
{
    public class ShopItemController : MonoBehaviour, IDraggable, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [Header("UI Elements")]
        [SerializeField] private Image itemIcon;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image rarityFrame;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI levelRequirementText;
        [SerializeField] private Image currencyIcon;
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private GameObject ownedIndicator;
        [SerializeField] private GameObject unaffordableOverlay;
        [SerializeField] private Button purchaseButton;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Visual Settings")]
        [SerializeField] private Color affordableColor = Color.white;
        [SerializeField] private Color unaffordableColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private Color lockedColor = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float hoverDuration = 0.2f;

        [Header("Currency Icons")]
        [SerializeField] private Sprite neuralCreditsIcon;
        [SerializeField] private Sprite cyberCoinsIcon;

        // Data
        private EquipmentData equipmentData;
        private ShopUIController shopController;
        private DragDropManager dragDropManager;
        private bool isAffordable;
        private bool meetsLevelRequirement;
        private bool isOwned;
        private bool isDragging;
        private Vector3 originalScale;

        // Properties
        public EquipmentData Equipment => equipmentData;
        public bool IsOwned => isOwned;
        public bool IsAffordable => isAffordable && meetsLevelRequirement && !isOwned;

        #region Initialization

        private void Awake()
        {
            originalScale = transform.localScale;
            dragDropManager = DragDropManager.Instance;

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        public void Initialize(EquipmentData equipment, ShopUIController controller)
        {
            equipmentData = equipment;
            shopController = controller;

            UpdateDisplay();
            SetupPurchaseButton();
        }

        private void UpdateDisplay()
        {
            if (equipmentData == null) return;

            // Item icon
            if (itemIcon != null && equipmentData.equipmentIcon != null)
            {
                itemIcon.sprite = equipmentData.equipmentIcon;
                itemIcon.enabled = true;
            }

            // Item name
            if (itemNameText != null)
            {
                itemNameText.text = equipmentData.displayName;
            }

            // Price
            if (priceText != null)
            {
                int price = equipmentData.neuralCreditCost > 0 ? equipmentData.neuralCreditCost : equipmentData.cyberCoinCost;
                priceText.text = price.ToString("N0");
            }

            // Currency icon
            if (currencyIcon != null)
            {
                currencyIcon.sprite = equipmentData.neuralCreditCost > 0 ?
                    neuralCreditsIcon : cyberCoinsIcon;
            }

            // Level requirement
            if (levelRequirementText != null)
            {
                levelRequirementText.text = $"Lv.{equipmentData.requiredPlayerLevel}";
            }

            // Rarity frame
            UpdateRarityVisual();
        }

        private void SetupPurchaseButton()
        {
            if (purchaseButton != null)
            {
                purchaseButton.onClick.RemoveAllListeners();
                purchaseButton.onClick.AddListener(OnPurchaseClicked);
            }
        }

        #endregion

        #region State Updates

        public void UpdateAffordability(bool canAfford, bool meetsLevel)
        {
            isAffordable = canAfford;
            meetsLevelRequirement = meetsLevel;

            UpdateVisualState();
        }

        public void UpdateOwnership(bool owned)
        {
            isOwned = owned;
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            // Owned state
            if (ownedIndicator != null)
            {
                ownedIndicator.SetActive(isOwned);
            }

            // Locked state (level requirement)
            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(!meetsLevelRequirement && !isOwned);
            }

            // Unaffordable state
            if (unaffordableOverlay != null)
            {
                unaffordableOverlay.SetActive(!isAffordable && meetsLevelRequirement && !isOwned);
            }

            // Price text color
            if (priceText != null)
            {
                if (isOwned)
                {
                    priceText.color = affordableColor;
                    priceText.text = "Owned";
                }
                else if (!meetsLevelRequirement)
                {
                    priceText.color = lockedColor;
                }
                else
                {
                    priceText.color = isAffordable ? affordableColor : unaffordableColor;
                }
            }

            // Background tint
            if (backgroundImage != null)
            {
                if (isOwned)
                {
                    backgroundImage.color = new Color(0.7f, 1f, 0.7f, 1f);
                }
                else if (!meetsLevelRequirement)
                {
                    backgroundImage.color = lockedColor;
                }
                else
                {
                    backgroundImage.color = Color.white;
                }
            }

            // Purchase button
            if (purchaseButton != null)
            {
                purchaseButton.interactable = !isOwned && meetsLevelRequirement && isAffordable;
            }

            // Canvas group for drag
            if (canvasGroup != null)
            {
                canvasGroup.alpha = (isOwned || !meetsLevelRequirement) ? 0.6f : 1f;
            }
        }

        #endregion

        #region IDraggable Implementation

        public EquipmentData GetDraggedEquipment()
        {
            return equipmentData;
        }

        public DragSourceType GetDragSourceType()
        {
            return DragSourceType.Shop;
        }

        public bool CanDrag()
        {
            return !isOwned && isAffordable && meetsLevelRequirement && equipmentData != null;
        }

        public void OnDragStarted()
        {
            isDragging = true;
            canvasGroup.alpha = 0.6f;
        }

        public void OnDragEnded(bool successful)
        {
            isDragging = false;
            canvasGroup.alpha = (isOwned || !meetsLevelRequirement) ? 0.6f : 1f;

            if (successful)
            {
                // Purchase will be handled by the drop target
                AnimatePurchase();
            }
        }

        public Sprite GetDragIcon()
        {
            return itemIcon?.sprite;
        }

        public GameObject GetSourceObject()
        {
            return gameObject;
        }

        #endregion

        #region Drag Handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanDrag()) return;

            if (dragDropManager != null && dragDropManager.StartDrag(this, eventData.position))
            {
                OnDragStarted();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || dragDropManager == null) return;

            dragDropManager.UpdateDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging || dragDropManager == null) return;

            dragDropManager.CancelDrag();
        }

        #endregion

        #region Pointer Events

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (equipmentData == null) return;

            // Hover animation
            transform.DOScale(originalScale * hoverScale, hoverDuration)
                .SetEase(Ease.OutCubic);

            // Notify shop controller
            shopController?.InvokeItemHovered(equipmentData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Reset scale
            transform.DOScale(originalScale, hoverDuration)
                .SetEase(Ease.OutCubic);

            // Notify shop controller
            shopController?.InvokeItemHoverExit();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                ShowItemPreview();
            }
        }

        #endregion

        #region Purchase

        public void OnPurchaseClicked()
        {
            if (shopController != null && CanPurchase())
            {
                shopController.OnItemPurchaseRequested(this);
            }
        }

        private bool CanPurchase()
        {
            return !isOwned && isAffordable && meetsLevelRequirement;
        }

        public void AnimatePurchase()
        {
            // Purchase success animation
            transform.DOPunchScale(Vector3.one * 0.2f, 0.5f, 10, 1f)
                .OnComplete(() =>
                {
                    UpdateOwnership(true);
                });

            // Flash effect
            if (backgroundImage != null)
            {
                backgroundImage.DOColor(Color.yellow, 0.1f)
                    .SetLoops(2, LoopType.Yoyo);
            }
        }

        #endregion

        #region Visual Helpers

        private void UpdateRarityVisual()
        {
            if (rarityFrame == null || equipmentData == null) return;

            // Set rarity frame color based on equipment rarity
            // This is a placeholder - implement based on your rarity system
            Color rarityColor = Color.white;

            switch (equipmentData.slotType)
            {
                case EquipmentSlotType.HandWeapon:
                    rarityColor = new Color(1f, 0.5f, 0f); // Orange
                    break;
                case EquipmentSlotType.BodyWeapon:
                    rarityColor = new Color(0.5f, 0f, 1f); // Purple
                    break;
                case EquipmentSlotType.PowerUp:
                    rarityColor = new Color(0f, 1f, 0f); // Green
                    break;
                case EquipmentSlotType.Armor:
                    rarityColor = new Color(0f, 0.5f, 1f); // Blue
                    break;
                case EquipmentSlotType.Amulet:
                    rarityColor = new Color(1f, 1f, 0f); // Yellow
                    break;
            }

            rarityFrame.color = rarityColor;
        }

        public void ShowItemPreview()
        {
            // This would show a detailed preview of the item
            Debug.Log($"Showing preview for: {equipmentData?.displayName}");
        }

        #endregion

        private void OnDestroy()
        {
            if (purchaseButton != null)
            {
                purchaseButton.onClick.RemoveAllListeners();
            }

            DOTween.Kill(this);
        }
    }
}