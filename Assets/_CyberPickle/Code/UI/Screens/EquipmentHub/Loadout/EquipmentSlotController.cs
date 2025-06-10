// File: UI/Screens/EquipmentHub/Loadout/EquipmentSlotController.cs
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Shop.Equipment;
using CyberPickle.Shop.Equipment.Data;
using CyberPickle.UI.EquipmentHub.DragDrop;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CyberPickle.UI.EquipmentHub
{
    public class EquipmentSlotController : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDropHandler,
        IDraggable,
        IDropTarget
    {
        [Header("UI References")]
        [SerializeField] private Image slotFrame;
        [SerializeField] private Image slotIcon;
        [SerializeField] private Image equipmentIcon;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private GameObject equippedGlow;
        [SerializeField] private GameObject highlightEffect;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Slot Configuration")]
        [SerializeField] private EquipmentSlotType slotType;
        [SerializeField] private int slotIndex = 0;

        [Header("Visual Settings")]
        [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.7f);
        [SerializeField] private Color occupiedSlotColor = Color.white;
        [SerializeField] private Color highlightColor = new Color(1f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color validDropColor = new Color(0.2f, 1f, 0.2f, 0.8f);
        [SerializeField] private Color invalidDropColor = new Color(1f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float clickScale = 0.9f;

        private EquipmentData currentEquipment;
        private LoadoutDisplayController loadoutController;
        private bool isOccupied = false;
        private bool isDragging = false;
        private Vector3 originalScale;
        private DragDropManager dragDropManager;

        public EquipmentSlotType SlotType => slotType;
        public bool IsOccupied => isOccupied;
        public EquipmentData CurrentEquipment => currentEquipment;

        #region Initialization

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            originalScale = transform.localScale;
            SetEmpty();
        }

        private void Start()
        {
            dragDropManager = DragDropManager.Instance;
            loadoutController = GetComponentInParent<LoadoutDisplayController>();

            if (equippedGlow != null)
            {
                equippedGlow.SetActive(false);
            }
        }
        
        public void Initialize(EquipmentSlotType type, int index = 0)
        {
            slotType = type;
            slotIndex = index;
            UpdateSlotVisual();
        }

        #endregion

        #region Equipment Management

        public void SetEquipment(EquipmentData equipment)
        {
            if (equipment != null && equipment.slotType != slotType)
            {
                Debug.LogWarning($"[EquipmentSlotController] Wrong equipment type {equipment.slotType} for slot {slotType}");
                return;
            }

            currentEquipment = equipment;
            isOccupied = equipment != null;
            UpdateVisual();

            if (isOccupied)
            {
                PlayEquipAnimation();
            }
        }

        public void SetEmpty()
        {
            currentEquipment = null;
            isOccupied = false;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (equipmentIcon != null)
            {
                equipmentIcon.enabled = isOccupied;
                if (isOccupied && currentEquipment != null)
                {
                    equipmentIcon.sprite = currentEquipment.equipmentIcon;
                    equipmentIcon.DOFade(1f, 0.2f);
                }
            }

            if (slotIcon != null)
            {
                slotIcon.DOFade(isOccupied ? 0.3f : 0.7f, 0.2f);
            }

            if (levelText != null)
            {
                levelText.gameObject.SetActive(isOccupied && currentEquipment != null);
                if (isOccupied && currentEquipment != null)
                {
                    levelText.text = $"Lv.{currentEquipment.requiredPlayerLevel}";
                }
            }

            if (equippedGlow != null)
            {
                equippedGlow.SetActive(isOccupied);
                if (isOccupied)
                {
                    AnimateEquipGlow();
                }
            }

            var targetColor = isOccupied ? occupiedSlotColor : emptySlotColor;
            if (slotFrame != null)
            {
                slotFrame.DOColor(targetColor, 0.3f);
            }
        }

        #endregion

        #region IDraggable Implementation

        public EquipmentData GetDraggedEquipment() => currentEquipment;

        public DragSourceType GetDragSourceType() => DragSourceType.Equipment;

        public bool CanDrag() => isOccupied && currentEquipment != null;

        public void OnDragStarted()
        {
            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false;
        }

        public void OnDragEnded(bool successful)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            if (successful && isOccupied)
            {
                // Item was moved out
                SetEmpty();
            }
            else if (!successful)
            {
                // Failed drop
                ShakeAnimation();
            }
        }

        public Sprite GetDragIcon() => equipmentIcon?.sprite;

        public GameObject GetSourceObject() => gameObject;

        #endregion

        #region IDropTarget Implementation

        public DropTargetType GetDropTargetType() => DropTargetType.EquipmentSlot;

        public bool CanAcceptDrop(IDraggable draggable)
        {
            var equipment = draggable.GetDraggedEquipment();
            if (equipment == null) return false;

            // Check if equipment type matches this slot
            return equipment.slotType == slotType;
        }

        public void OnDropPreview(IDraggable draggable)
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(true);
            }

            bool canAccept = CanAcceptDrop(draggable);
            HighlightDropFeedback(canAccept);
        }

        public void OnDropPreviewEnd()
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(false);
            }

            if (slotFrame != null)
            {
                slotFrame.DOColor(isOccupied ? occupiedSlotColor : emptySlotColor, 0.3f);
            }
        }

        public bool OnDropReceived(IDraggable draggable)
        {
            var draggedEquipment = draggable.GetDraggedEquipment();
            if (draggedEquipment == null || !CanAcceptDrop(draggable))
                return false;

            // Handle equipment swapping if slot is occupied
            EquipmentData previousEquipment = currentEquipment;

            // Equip the new item
            SetEquipment(draggedEquipment);

            // If we had equipment and the source was also an equipment slot, swap
            if (previousEquipment != null && draggable.GetDragSourceType() == DragSourceType.Equipment)
            {
                if (draggable is EquipmentSlotController sourceSlot)
                {
                    sourceSlot.SetEquipment(previousEquipment);
                }
            }

            // Update equipment in profile through manager
            if (EquipmentManager.Instance != null)
            {
                // This will be implemented when integrating with EquipmentManager
            }

            return true;
        }

        public EquipmentData GetCurrentEquipment() => currentEquipment;

        public GameObject GetTargetObject() => gameObject;

        #endregion

        #region Pointer Events

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isDragging)
            {
                transform.DOScale(originalScale * hoverScale, 0.2f);
                if (highlightEffect != null && !highlightEffect.activeSelf)
                {
                    highlightEffect.SetActive(true);
                    highlightEffect.GetComponent<Image>()?.DOFade(0.3f, 0.2f);
                }
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isDragging)
            {
                transform.DOScale(originalScale, 0.2f);
                if (highlightEffect != null)
                {
                    highlightEffect.GetComponent<Image>()?.DOFade(0f, 0.2f)
                        .OnComplete(() => highlightEffect.SetActive(false));
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                transform.DOScale(originalScale * clickScale, 0.1f);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.DOScale(originalScale, 0.1f);

            if (eventData.button == PointerEventData.InputButton.Right && isOccupied)
            {
                UnequipItem();
            }
        }

        #endregion

        #region Drag & Drop Events

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isOccupied || currentEquipment == null || dragDropManager == null) return;

            isDragging = dragDropManager.StartDrag(this, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isDragging && dragDropManager != null)
            {
                dragDropManager.UpdateDrag(eventData.position);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging || dragDropManager == null) return;

            isDragging = false;

            GameObject dropTarget = eventData.pointerCurrentRaycast.gameObject;
            if (dropTarget != null)
            {
                var dropTargetComponent = dropTarget.GetComponentInParent<IDropTarget>();
                if (dropTargetComponent != null)
                {
                    dragDropManager.CompleteDrop(dropTargetComponent, eventData.position);
                    return;
                }
            }

            dragDropManager.CancelDrag();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (dragDropManager != null && dragDropManager.IsDragging())
            {
                var draggable = dragDropManager.GetCurrentDraggable();
                if (draggable != null)
                {
                    HighlightDropFeedback(CanAcceptDrop(draggable));
                }
            }
        }

        #endregion

        #region Helper Methods

        private void UpdateSlotVisual()
        {
            // Update slot icon based on type
            // This would load appropriate icons for each slot type
        }

        private void UnequipItem()
        {
            if (!IsOccupied) return;

            // TODO: Call equipment manager to unequip
            SetEmpty();
            transform.DOPunchScale(Vector3.one * 0.1f, 0.3f, 5);
        }

        private void HighlightDropFeedback(bool isValid)
        {
            if (slotFrame != null)
            {
                Color targetColor = isValid ? validDropColor : invalidDropColor;
                slotFrame.DOColor(targetColor, 0.2f);
            }
        }

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

        private void ShakeAnimation()
        {
            transform.DOShakePosition(0.3f, 10f, 10, 90, false, true);
        }

        private void PlayEquipAnimation()
        {
            transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 5);

            if (equippedGlow != null)
            {
                var glowImage = equippedGlow.GetComponent<Image>();
                if (glowImage != null)
                {
                    glowImage.DOFade(1f, 0.2f).OnComplete(() => {
                        glowImage.DOFade(0.5f, 0.3f);
                    });
                }
            }
        }

        #endregion

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }
    }
}