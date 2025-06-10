// File: UI/Screens/EquipmentHub/Loadout/InventorySlotController.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using CyberPickle.Shop.Equipment.Data;
using CyberPickle.UI.EquipmentHub.DragDrop;

namespace CyberPickle.UI.EquipmentHub
{
    public class InventorySlotController : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDraggable,
        IDropTarget,
        IDropHandler
    {
        [Header("UI References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private GameObject highlightBorder;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Visual Settings")]
        [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color occupiedSlotColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float clickScale = 0.95f;

        private InventoryUIController inventoryController;
        private EquipmentData currentEquipment;
        private int currentQuantity = 1;
        private bool isOccupied = false;
        private bool isDragging = false;
        private Vector3 originalScale;
        private DragDropManager dragDropManager;
        private int slotIndex;
        public EquipmentData CurrentEquipment => currentEquipment;
        public bool IsOccupied => isOccupied;
        public int Quantity => currentQuantity;

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
            inventoryController = GetComponentInParent<InventoryUIController>();
        }

        public void Initialize(int index, InventoryUIController controller)
        {
            slotIndex = index;
            inventoryController = controller;
            SetEmpty();

            // Cache components
            if (backgroundImage == null) backgroundImage = GetComponent<Image>();
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            originalScale = transform.localScale;

            // Hide optional elements
            if (highlightBorder != null) highlightBorder.SetActive(false);
            // If you use rarityGlow, handle it here as well.
        }

        public void SetItem(EquipmentData equipment, int quantity = 1)
        {
            if (equipment == null)
            {
                SetEmpty();
                return;
            }

            currentEquipment = equipment;
            currentQuantity = quantity;
            isOccupied = true;

            if (itemIcon != null)
            {
                itemIcon.sprite = equipment.equipmentIcon;
                itemIcon.enabled = true;
                itemIcon.DOFade(1f, 0.2f);
            }

            if (quantityText != null)
            {
                quantityText.text = quantity > 1 ? quantity.ToString() : "";
                quantityText.enabled = quantity > 1;
            }

            if (backgroundImage != null)
            {
                backgroundImage.DOColor(occupiedSlotColor, 0.2f);
            }
        }

        public void SetEmpty()
        {
            currentEquipment = null;
            currentQuantity = 0;
            isOccupied = false;

            if (itemIcon != null)
            {
                itemIcon.enabled = false;
            }

            if (quantityText != null)
            {
                quantityText.enabled = false;
            }

            if (backgroundImage != null)
            {
                backgroundImage.DOColor(emptySlotColor, 0.2f);
            }
        }

        public void ClearSlot()
        {
            SetEmpty();
        }

        #endregion

        #region IDraggable Implementation

        public EquipmentData GetDraggedEquipment() => currentEquipment;

        public DragSourceType GetDragSourceType() => DragSourceType.Inventory;

        public bool CanDrag() => isOccupied && currentEquipment != null;

        public void OnDragStarted()
        {
            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false;
            inventoryController?.OnItemDragStart(this);
        }

        public void OnDragEnded(bool successful)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            if (!successful)
            {
                // Shake animation for failed drop
                transform.DOShakePosition(0.3f, 5f, 10, 90, false, true);
            }
        }

        public Sprite GetDragIcon() => itemIcon?.sprite;

        public GameObject GetSourceObject() => gameObject;

        #endregion

        #region IDropTarget Implementation

        public DropTargetType GetDropTargetType() => DropTargetType.InventorySlot;

        public bool CanAcceptDrop(IDraggable draggable)
        {
            // Inventory slots can accept items from anywhere if empty
            // Or if the dragged item can stack with current item
            if (!isOccupied) return true;

            var draggedEquipment = draggable.GetDraggedEquipment();
            if (draggedEquipment != null && currentEquipment != null)
            {
                // Check if items can stack (same equipment ID)
                return draggedEquipment.equipmentId == currentEquipment.equipmentId;
            }

            return false;
        }

        public void OnDropPreview(IDraggable draggable)
        {
            if (highlightBorder != null)
            {
                highlightBorder.SetActive(true);
            }
            backgroundImage?.DOColor(hoverColor, 0.2f);
        }

        public void OnDropPreviewEnd()
        {
            if (highlightBorder != null)
            {
                highlightBorder.SetActive(false);
            }
            backgroundImage?.DOColor(isOccupied ? occupiedSlotColor : emptySlotColor, 0.2f);
        }

        public bool OnDropReceived(IDraggable draggable)
        {
            var draggedEquipment = draggable.GetDraggedEquipment();
            if (draggedEquipment == null) return false;

            // Handle different source types
            switch (draggable.GetDragSourceType())
            {
                case DragSourceType.Shop:
                    // This will be handled by shop purchase logic
                    return false;

                case DragSourceType.Equipment:
                    // Unequipping to inventory
                    if (!isOccupied)
                    {
                        SetItem(draggedEquipment);
                        return true;
                    }
                    break;

                case DragSourceType.Inventory:
                    // Rearranging or stacking
                    if (!isOccupied)
                    {
                        // Move to empty slot
                        SetItem(draggedEquipment, 1);
                        if (draggable is InventorySlotController sourceSlot)
                        {
                            sourceSlot.SetEmpty();
                        }
                        return true;
                    }
                    else if (currentEquipment.equipmentId == draggedEquipment.equipmentId)
                    {
                        // Stack items
                        currentQuantity++;
                        quantityText.text = currentQuantity.ToString();
                        quantityText.enabled = true;
                        if (draggable is InventorySlotController sourceSlot)
                        {
                            sourceSlot.SetEmpty();
                        }
                        return true;
                    }
                    else
                    {
                        // Swap items
                        if (draggable is InventorySlotController sourceSlot)
                        {
                            var tempEquipment = currentEquipment;
                            var tempQuantity = currentQuantity;
                            SetItem(draggedEquipment, sourceSlot.Quantity);
                            sourceSlot.SetItem(tempEquipment, tempQuantity);
                            return true;
                        }
                    }
                    break;
            }

            return false;
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
                if (backgroundImage != null)
                {
                    var targetColor = isOccupied ? hoverColor : emptySlotColor;
                    backgroundImage.DOColor(targetColor, 0.2f);
                }
            }

            if (highlightBorder != null && isOccupied)
            {
                highlightBorder.SetActive(true);
            }

            inventoryController?.OnItemHoverEnter(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isDragging)
            {
                transform.DOScale(originalScale, 0.2f);
                if (backgroundImage != null)
                {
                    var targetColor = isOccupied ? occupiedSlotColor : emptySlotColor;
                    backgroundImage.DOColor(targetColor, 0.2f);
                }
            }

            if (highlightBorder != null)
            {
                highlightBorder.SetActive(false);
            }

            inventoryController?.HandleItemHoverExit(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isOccupied || currentEquipment == null) return;

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                transform.DOScale(originalScale * clickScale, 0.1f)
                    .OnComplete(() => transform.DOScale(originalScale, 0.1f));
                inventoryController?.OnItemClicked(this);
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                // TODO: Implement quick equip
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

            // Try to find drop target
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
            // Handle as drop target
            if (dragDropManager != null && dragDropManager.IsDragging())
            {
                var draggable = dragDropManager.GetCurrentDraggable();
                if (draggable != null && CanAcceptDrop(draggable))
                {
                    OnDropPreview(draggable);
                }
            }
        }

        #endregion

        #region Visual Effects

        public void PlayEquipAnimation()
        {
            if (backgroundImage != null)
            {
                backgroundImage.DOColor(Color.white, 0.1f)
                    .OnComplete(() => backgroundImage.DOColor(occupiedSlotColor, 0.2f));
            }

            transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5);
        }

        public void PlayErrorAnimation()
        {
            transform.DOShakePosition(0.3f, 10f, 10, 90, false, true);
        }

        #endregion

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }
    }
}
