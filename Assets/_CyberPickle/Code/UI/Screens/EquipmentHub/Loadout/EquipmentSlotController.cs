using CyberPickle.Core.Events;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Shop.Equipment.Data;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CyberPickle.UI.EquipmentHub
{
    /// <summary>
    /// Controls individual equipment slot UI elements in the Equipment Hub.
    /// Handles drag-drop, visual states, and equipment display.
    /// </summary>
    public class EquipmentSlotController : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDropHandler
    {
        #region Serialized Fields

        [Header("Slot Configuration")]
        [SerializeField] private EquipmentSlotType slotType;
        [SerializeField] private int slotIndex = 0; // For multiple slots of same type

        [Header("UI References")]
        [SerializeField] private Image slotBackground;
        [SerializeField] private Image slotFrame;
        [SerializeField] private Image equipmentIcon;
        [SerializeField] private GameObject emptySlotOverlay;
        [SerializeField] private TextMeshProUGUI slotTypeText;
        [SerializeField] private GameObject equippedGlow;
        [SerializeField] private GameObject levelBadge;
        [SerializeField] private TextMeshProUGUI levelText;

        [Header("Visual Settings")]
        [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color occupiedSlotColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color validDropColor = new Color(0.2f, 1f, 0.2f, 0.5f);
        [SerializeField] private Color invalidDropColor = new Color(1f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float clickScale = 0.95f;

        [Header("Drag Settings")]
        [SerializeField] private float dragAlpha = 0.6f;
        [SerializeField] private Canvas dragCanvas; // Assign your main canvas

        #endregion

        #region Private Fields

        private EquipmentData currentEquipment;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Vector3 originalScale;
        private bool isDragging;
        private GameObject draggedIcon;
        private bool isValidDropTarget;

        #endregion

        #region Properties

        public EquipmentSlotType SlotType => slotType;
        public bool IsOccupied => currentEquipment != null;
        public EquipmentData CurrentEquipment => currentEquipment;
        public int SlotIndex => slotIndex;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            originalScale = transform.localScale;
            InitializeSlot();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the slot with default visual state
        /// </summary>
        private void InitializeSlot()
        {
            // Set slot type text
            if (slotTypeText != null)
            {
                slotTypeText.text = GetSlotTypeDisplayName();
            }

            // Set empty state
            SetEmpty();

            // Find canvas if not assigned
            if (dragCanvas == null)
            {
                dragCanvas = GetComponentInParent<Canvas>();
            }
        }

        /// <summary>
        /// Gets display-friendly name for slot type
        /// </summary>
        private string GetSlotTypeDisplayName()
        {
            switch (slotType)
            {
                case EquipmentSlotType.HandWeapon:
                    return $"Hand Weapon {slotIndex + 1}";
                case EquipmentSlotType.BodyWeapon:
                    return "Body Weapon";
                case EquipmentSlotType.PowerUp:
                    return $"Power-Up {slotIndex + 1}";
                case EquipmentSlotType.Armor:
                    return "Armor";
                case EquipmentSlotType.Amulet:
                    return "Amulet";
                default:
                    return slotType.ToString();
            }
        }

        #endregion

        #region Equipment Management

        /// <summary>
        /// Sets equipment in this slot
        /// </summary>
        public void SetEquipment(EquipmentData equipment, int level = 1)
        {
            if (equipment == null)
            {
                SetEmpty();
                return;
            }

            currentEquipment = equipment;

            // Update visuals
            if (equipmentIcon != null)
            {
                equipmentIcon.sprite = equipment.equipmentIcon;
                equipmentIcon.enabled = true;
            }

            if (emptySlotOverlay != null)
            {
                emptySlotOverlay.SetActive(false);
            }

            if (slotBackground != null)
            {
                slotBackground.color = occupiedSlotColor;
            }

            // Update level badge
            if (levelBadge != null && levelText != null)
            {
                levelBadge.SetActive(level > 1);
                levelText.text = level.ToString();
            }

            // Show equipped glow
            if (equippedGlow != null)
            {
                equippedGlow.SetActive(true);
                AnimateEquipGlow();
            }
        }

        /// <summary>
        /// Clears the slot
        /// </summary>
        public void SetEmpty()
        {
            currentEquipment = null;

            if (equipmentIcon != null)
            {
                equipmentIcon.enabled = false;
            }

            if (emptySlotOverlay != null)
            {
                emptySlotOverlay.SetActive(true);
            }

            if (slotBackground != null)
            {
                slotBackground.color = emptySlotColor;
            }

            if (levelBadge != null)
            {
                levelBadge.SetActive(false);
            }

            if (equippedGlow != null)
            {
                equippedGlow.SetActive(false);
            }
        }

        #endregion

        #region Pointer Events

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isDragging) return;

            // Scale up
            transform.DOScale(originalScale * hoverScale, 0.2f).SetEase(Ease.OutQuad);

            // Show tooltip if equipped
            if (IsOccupied && currentEquipment != null)
            {
                // TODO: Show equipment tooltip
                Debug.Log($"Hovering over: {currentEquipment.displayName}");
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isDragging) return;

            // Scale back
            transform.DOScale(originalScale, 0.2f).SetEase(Ease.OutQuad);

            // Hide tooltip
            // TODO: Hide equipment tooltip
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && IsOccupied)
            {
                // Right-click to unequip
                UnequipItem();
            }
            else if (eventData.button == PointerEventData.InputButton.Left)
            {
                // Animate click
                transform.DOScale(originalScale * clickScale, 0.1f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => {
                        transform.DOScale(originalScale * hoverScale, 0.1f).SetEase(Ease.OutQuad);
                    });
            }
        }

        #endregion

        #region Drag and Drop

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsOccupied || currentEquipment == null) return;

            isDragging = true;

            // Create drag icon
            CreateDragIcon();

            // Make slot semi-transparent
            canvasGroup.alpha = dragAlpha;

            // Disable raycast during drag
            canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || draggedIcon == null) return;

            // Update drag icon position
            draggedIcon.transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            isDragging = false;

            // Restore opacity
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            // Destroy drag icon
            if (draggedIcon != null)
            {
                Destroy(draggedIcon);
                draggedIcon = null;
            }

            // Check if dropped on valid target
            GameObject dropTarget = eventData.pointerCurrentRaycast.gameObject;
            if (dropTarget != null)
            {
                var targetSlot = dropTarget.GetComponentInParent<EquipmentSlotController>();
                if (targetSlot != null && targetSlot != this)
                {
                    TrySwapEquipment(targetSlot);
                }
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            // This is called on the slot being dropped onto
            var sourceSlot = eventData.pointerDrag?.GetComponent<EquipmentSlotController>();
            if (sourceSlot != null && sourceSlot != this)
            {
                // Visual feedback
                HighlightDropFeedback(CanAcceptEquipment(sourceSlot.CurrentEquipment));
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a draggable icon
        /// </summary>
        private void CreateDragIcon()
        {
            if (dragCanvas == null || equipmentIcon == null) return;

            draggedIcon = new GameObject("DragIcon");
            draggedIcon.transform.SetParent(dragCanvas.transform, false);

            var dragImage = draggedIcon.AddComponent<Image>();
            dragImage.sprite = equipmentIcon.sprite;
            dragImage.raycastTarget = false;

            var dragRect = draggedIcon.GetComponent<RectTransform>();
            dragRect.sizeDelta = equipmentIcon.rectTransform.sizeDelta;

            // Add canvas group for transparency
            var dragCanvasGroup = draggedIcon.AddComponent<CanvasGroup>();
            dragCanvasGroup.alpha = 0.8f;
        }

        /// <summary>
        /// Checks if this slot can accept the given equipment
        /// </summary>
        private bool CanAcceptEquipment(EquipmentData equipment)
        {
            if (equipment == null) return true; // Can always accept empty
            return equipment.slotType == slotType;
        }

        /// <summary>
        /// Attempts to swap equipment with another slot
        /// </summary>
        private void TrySwapEquipment(EquipmentSlotController targetSlot)
        {
            if (!CanAcceptEquipment(targetSlot.CurrentEquipment) ||
                !targetSlot.CanAcceptEquipment(this.CurrentEquipment))
            {
                // Invalid swap
                ShakeAnimation();
                return;
            }

            // Perform swap
            var tempEquipment = targetSlot.CurrentEquipment;
            targetSlot.SetEquipment(this.CurrentEquipment);
            this.SetEquipment(tempEquipment);

            // Notify equipment manager
            // TODO: Update equipment in profile
        }

        /// <summary>
        /// Unequips the current item
        /// </summary>
        private void UnequipItem()
        {
            if (!IsOccupied) return;

            // TODO: Call equipment manager to unequip
            SetEmpty();

            // Animation
            transform.DOPunchScale(Vector3.one * 0.1f, 0.3f, 5);
        }

        /// <summary>
        /// Shows drop feedback
        /// </summary>
        private void HighlightDropFeedback(bool isValid)
        {
            if (slotFrame != null)
            {
                Color targetColor = isValid ? validDropColor : invalidDropColor;
                slotFrame.DOColor(targetColor, 0.2f)
                    .OnComplete(() => {
                        slotFrame.DOColor(Color.white, 0.3f);
                    });
            }
        }

        /// <summary>
        /// Animates the equipped glow effect
        /// </summary>
        private void AnimateEquipGlow()
        {
            if (equippedGlow == null) return;

            var glowImage = equippedGlow.GetComponent<Image>();
            if (glowImage != null)
            {
                glowImage.DOFade(0.5f, 1f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }
        }

        /// <summary>
        /// Shakes the slot for error feedback
        /// </summary>
        private void ShakeAnimation()
        {
            transform.DOShakePosition(0.3f, 10f, 10, 90, false, true);
        }

        #endregion
    }
}