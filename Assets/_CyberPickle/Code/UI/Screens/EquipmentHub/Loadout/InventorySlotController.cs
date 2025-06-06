using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.UI.EquipmentHub
{
    public class InventorySlotController : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        [Header("UI References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private GameObject levelBadge;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private GameObject highlightBorder;
        [SerializeField] private GameObject rarityGlow;

        [Header("Visual Settings")]
        [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color occupiedSlotColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float clickScale = 0.95f;

        [Header("Rarity Colors")]
        [SerializeField] private Color commonColor = Color.gray;
        [SerializeField] private Color uncommonColor = Color.green;
        [SerializeField] private Color rareColor = Color.blue;
        [SerializeField] private Color epicColor = new Color(0.5f, 0f, 0.5f, 1f); // Purple
        [SerializeField] private Color legendaryColor = new Color(1f, 0.5f, 0f, 1f); // Orange

        private InventoryUIController inventoryController;
        private EquipmentData currentEquipment;
        private int slotIndex;
        private int itemLevel = 1;
        private bool isOccupied = false;
        private bool isDragging = false;
        private Vector3 originalScale;
        private CanvasGroup canvasGroup;

        public EquipmentData CurrentEquipment => currentEquipment;
        public bool IsOccupied => isOccupied;
        public int SlotIndex => slotIndex;

        #region Initialization

        public void Initialize(int index, InventoryUIController controller)
        {
            slotIndex = index;
            inventoryController = controller;

            // Cache components
            if (backgroundImage == null) backgroundImage = GetComponent<Image>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            originalScale = transform.localScale;

            // Set initial state
            ClearSlot();

            // Hide optional elements
            if (highlightBorder != null) highlightBorder.SetActive(false);
            if (rarityGlow != null) rarityGlow.SetActive(false);
        }

        #endregion

        #region Slot Management

        public void SetItem(EquipmentData item, int level = 1)
        {
            if (item == null)
            {
                ClearSlot();
                return;
            }

            currentEquipment = item;
            itemLevel = level;
            isOccupied = true;

            // Update visuals
            if (itemIcon != null)
            {
                itemIcon.sprite = item.equipmentIcon;
                itemIcon.enabled = true;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = occupiedSlotColor;
            }

            // Update level display
            if (levelBadge != null && levelText != null)
            {
                bool showLevel = level > 1;
                levelBadge.SetActive(showLevel);
                if (showLevel)
                {
                    levelText.text = level.ToString();
                }
            }

            // Update quantity (for stackable items in the future)
            UpdateQuantityDisplay(1);

            // Set rarity glow
            SetRarityVisuals(item);

            // Animation
            transform.DOScale(originalScale * 1.1f, 0.1f)
                .OnComplete(() => transform.DOScale(originalScale, 0.1f));
        }

        public void ClearSlot()
        {
            currentEquipment = null;
            itemLevel = 1;
            isOccupied = false;

            if (itemIcon != null)
            {
                itemIcon.sprite = null;
                itemIcon.enabled = false;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = emptySlotColor;
            }

            if (levelBadge != null)
            {
                levelBadge.SetActive(false);
            }

            if (quantityText != null)
            {
                quantityText.gameObject.SetActive(false);
            }

            if (rarityGlow != null)
            {
                rarityGlow.SetActive(false);
            }
        }

        public void UpdateQuantityDisplay(int quantity = 1)
        {
            if (quantityText != null)
            {
                bool showQuantity = quantity > 1;
                quantityText.gameObject.SetActive(showQuantity);
                if (showQuantity)
                {
                    quantityText.text = quantity.ToString();
                }
            }
        }

        private void SetRarityVisuals(EquipmentData item)
        {
            if (rarityGlow == null) return;

            // Determine rarity based on item properties
            Color glowColor = commonColor;

            // Simple rarity determination based on cost
            if (item.cyberCoinCost > 0)
            {
                glowColor = legendaryColor;
            }
            else if (item.neuralCreditCost >= 10000)
            {
                glowColor = epicColor;
            }
            else if (item.neuralCreditCost >= 5000)
            {
                glowColor = rareColor;
            }
            else if (item.neuralCreditCost >= 1000)
            {
                glowColor = uncommonColor;
            }

            var glowImage = rarityGlow.GetComponent<Image>();
            if (glowImage != null)
            {
                glowImage.color = glowColor;
                rarityGlow.SetActive(true);

                // Pulse animation for epic and legendary
                if (glowColor == epicColor || glowColor == legendaryColor)
                {
                    glowImage.DOFade(0.5f, 1f).SetLoops(-1, LoopType.Yoyo);
                }
            }
        }

        #endregion

        #region Pointer Events

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isDragging) return;

            // Visual feedback
            transform.DOScale(originalScale * hoverScale, 0.2f).SetEase(Ease.OutQuad);

            if (backgroundImage != null)
            {
                backgroundImage.DOColor(hoverColor, 0.2f);
            }

            if (highlightBorder != null)
            {
                highlightBorder.SetActive(true);
            }

            // Notify inventory controller
            inventoryController?.OnItemHoverEnter(this);

            // Play hover sound
            //var audioController = GetComponentInParent<AudioFeedbackController>();
           // audioController?.PlayHoverSound();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isDragging) return;

            // Reset visual feedback
            transform.DOScale(originalScale, 0.2f).SetEase(Ease.OutQuad);

            if (backgroundImage != null)
            {
                Color targetColor = isOccupied ? occupiedSlotColor : emptySlotColor;
                backgroundImage.DOColor(targetColor, 0.2f);
            }

            if (highlightBorder != null)
            {
                highlightBorder.SetActive(false);
            }

            // Notify inventory controller
            inventoryController?.HandleItemHoverExit(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isOccupied || currentEquipment == null) return;

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // Click animation
                transform.DOScale(originalScale * clickScale, 0.1f)
                    .OnComplete(() => transform.DOScale(originalScale, 0.1f));

                // Notify inventory controller
                inventoryController?.OnItemClicked(this);
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                // Right-click for quick equip or context menu
                // TODO: Implement quick equip
            }
        }

        #endregion

        #region Drag & Drop

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isOccupied || currentEquipment == null) return;

            isDragging = true;
            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false;

            // Notify inventory controller
            inventoryController?.OnItemDragStart(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            // Update drag visual position
            inventoryController?.UpdateDragPosition(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            isDragging = false;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            // Check if dropped on valid target
            GameObject dropTarget = eventData.pointerCurrentRaycast.gameObject;
            if (dropTarget != null)
            {
                var equipmentSlot = dropTarget.GetComponentInParent<EquipmentSlotController>();
                if (equipmentSlot != null && IsCompatibleWith(equipmentSlot))
                {
                    // TODO: Handle equipment swap
                }
            }

            // Notify inventory controller
            inventoryController?.OnItemDragEnd();
        }

        public bool IsCompatibleWith(EquipmentSlotController targetSlot)
        {
            if (targetSlot == null || currentEquipment == null) return false;

            return targetSlot.SlotType == currentEquipment.slotType;
        }

        #endregion

        #region Visual Effects

        public void PlayEquipAnimation()
        {
            // Flash effect
            if (backgroundImage != null)
            {
                backgroundImage.DOColor(Color.white, 0.1f)
                    .OnComplete(() => backgroundImage.DOColor(occupiedSlotColor, 0.2f));
            }

            // Scale punch
            transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 1, 0.5f);
        }

        public void PlayErrorAnimation()
        {
            // Shake effect
            transform.DOShakePosition(0.3f, 10f, 10, 90, false, true);

            // Red flash
            if (backgroundImage != null)
            {
                backgroundImage.DOColor(Color.red, 0.1f)
                    .OnComplete(() => backgroundImage.DOColor(occupiedSlotColor, 0.2f));
            }
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }

        #endregion
    }
}