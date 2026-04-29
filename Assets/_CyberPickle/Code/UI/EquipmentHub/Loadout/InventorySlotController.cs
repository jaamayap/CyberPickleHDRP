using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using CyberPickle.Shop.Equipment.Data;
using CyberPickle.UI.EquipmentHub.DragDrop;

namespace CyberPickle.UI.EquipmentHub
{
    public class InventorySlotController : MonoBehaviour, IDraggable, IDropTarget,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [Header("UI References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private GameObject highlightBorder;
        [SerializeField] private GameObject rarityGlow;

        [Header("Visual Settings")]
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private Color hoverColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color occupiedSlotColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        private int slotIndex;
        private EquipmentData currentEquipment;
        private int currentQuantity;
        private bool isOccupied;
        private bool isDragging;
        private Vector3 originalScale;
        private Button button;
        private InventoryUIController inventoryController;
        private DragDropManager dragDropManager;
        private CanvasGroup canvasGroup;

        public int SlotIndex => slotIndex;
        public EquipmentData CurrentEquipment => currentEquipment;
        public int Quantity => currentQuantity;
        public bool IsOccupied => isOccupied;

        #region Initialization

        private void Awake()
        {
            originalScale = transform.localScale;
            SetEmpty(false); // Don't animate during initialization
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
            SetEmpty(false); // Don't animate during initialization

            if (backgroundImage == null) backgroundImage = GetComponent<Image>();
            originalScale = transform.localScale;
            if (highlightBorder != null) highlightBorder.SetActive(false);
        }

        public void OnDrop(PointerEventData eventData)
        {
            // This is called when something is dropped on this slot
            // The actual drop handling is done through DragDropManager
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

        #region Slot Management

        public void SetItem(EquipmentData equipment, int quantity = 1)
        {
            SetItem(equipment, quantity, true);
        }

        public void SetItem(EquipmentData equipment, int quantity, bool animate)
        {
            if (equipment == null)
            {
                SetEmpty(animate);
                return;
            }

            currentEquipment = equipment;
            currentQuantity = quantity;
            isOccupied = true;

            if (itemIcon != null)
            {
                itemIcon.sprite = equipment.equipmentIcon;
                itemIcon.enabled = true;
                if (animate)
                {
                    itemIcon.DOFade(1f, 0.2f);
                }
                else
                {
                    var color = itemIcon.color;
                    itemIcon.color = new Color(color.r, color.g, color.b, 1f);
                }
            }

            if (quantityText != null)
            {
                quantityText.text = quantity > 1 ? quantity.ToString() : "";
                quantityText.enabled = quantity > 1;
            }
            
            if (levelText != null)
            {
                levelText.gameObject.SetActive(true);
                levelText.text = $"Lv.{equipment.requiredPlayerLevel}";
            }
            
            if (rarityGlow != null)
            {
                rarityGlow.SetActive(true);
                // You can add rarity-based color logic here if needed
            }

            if (backgroundImage != null)
            {
                if (animate)
                {
                    backgroundImage.DOColor(occupiedSlotColor, 0.2f);
                }
                else
                {
                    backgroundImage.color = occupiedSlotColor;
                }
            }
        }

        public void SetEmpty()
        {
            SetEmpty(true);
        }

        public void SetEmpty(bool animate = true)
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
            
            if (levelText != null)
            {
                levelText.gameObject.SetActive(false);
            }
            
            if (rarityGlow != null)
            {
                rarityGlow.SetActive(false);
            }

            // Clear highlight border when slot becomes empty
            if (highlightBorder != null)
            {
                highlightBorder.SetActive(false);
            }

            if (backgroundImage != null)
            {
                if (animate)
                {
                    backgroundImage.DOColor(emptySlotColor, 0.2f);
                }
                else
                {
                    backgroundImage.color = emptySlotColor;
                }
            }
        }

        public void ClearSlot()
        {
            SetEmpty();
        }

        public void ClearSlot(bool animate)
        {
            SetEmpty(animate);
        }

        #endregion

        #region Drag & Drop Implementation

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isOccupied || currentEquipment == null) return;

            if (dragDropManager == null)
            {
                dragDropManager = DragDropManager.Instance;
            }

            if (dragDropManager != null && dragDropManager.StartDrag(this, eventData.position))
            {
                isDragging = true;
                
                // Create CanvasGroup only for drag visual feedback
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
                canvasGroup.alpha = 0.6f;
                canvasGroup.blocksRaycasts = false;
                
                inventoryController?.OnItemDragStart(this);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || dragDropManager == null) return;
            dragDropManager.UpdateDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            isDragging = false;
            
            // Clean up CanvasGroup used for drag feedback
            if (canvasGroup != null)
            {
                Destroy(canvasGroup);
                canvasGroup = null;
            }

            inventoryController?.OnItemDragEnd();

            if (dragDropManager != null)
            {
                // Let DragDropManager handle the drop detection
                GameObject dropTarget = eventData.pointerCurrentRaycast.gameObject;
                if (dropTarget != null)
                {
                    var dropTargetComponent = dropTarget.GetComponentInParent<IDropTarget>();
                    if (dropTargetComponent != null && !ReferenceEquals(dropTargetComponent, this))
                    {
                        dragDropManager.CompleteDrop(dropTargetComponent, eventData.position);
                        return;
                    }

                }

                dragDropManager.CancelDrag();
                transform.DOShakePosition(0.3f, 5f, 10, 90, false, true);
            }
        }

        #endregion

        #region IDraggable Implementation

        public EquipmentData GetDraggedEquipment() => currentEquipment;
        public DragSourceType GetDragSourceType() => DragSourceType.Inventory;
        public bool CanDrag() => isOccupied && currentEquipment != null;

        public void OnDragStarted()
        {
            // Already handled in OnBeginDrag
        }

        public void OnDragEnded(bool successful)
        {
            // Already handled in OnEndDrag
        }

        public Sprite GetDragIcon() => itemIcon?.sprite;
        public GameObject GetSourceObject() => gameObject;

        #endregion

        #region IDropTarget Implementation

        public DropTargetType GetDropTargetType() => DropTargetType.InventorySlot;

        public bool CanAcceptDrop(IDraggable draggable)
        {
            if (draggable == null) return false;

            // Can't drop on self
            if (draggable.GetSourceObject() == gameObject) return false;

            var draggedEquipment = draggable.GetDraggedEquipment();
            if (draggedEquipment == null) return false;

            switch (draggable.GetDragSourceType())
            {
                case DragSourceType.Inventory:
                    // Always allow inventory-to-inventory drops
                    return true;

                case DragSourceType.Equipment:
                    // Allow unequipping to inventory if there's space
                    return !isOccupied || (currentEquipment != null &&
                           currentEquipment.equipmentId == draggedEquipment.equipmentId);

                case DragSourceType.Shop:
                    // Don't allow direct shop drops
                    return false;

                default:
                    return false;
            }
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
            if (draggable == null) return false;

            var draggedEquipment = draggable.GetDraggedEquipment();
            if (draggedEquipment == null) return false;

            switch (draggable.GetDragSourceType())
            {
                case DragSourceType.Inventory:
                    return HandleInventoryToInventoryDrop(draggable);

                case DragSourceType.Equipment:
                    return HandleEquipmentToInventoryDrop(draggable);

                default:
                    return false;
            }
        }

        private bool HandleInventoryToInventoryDrop(IDraggable draggable)
        {
            if (!(draggable is InventorySlotController sourceSlot)) return false;

            var draggedEquipment = sourceSlot.CurrentEquipment;
            var draggedQuantity = sourceSlot.Quantity;

            if (!isOccupied)
            {
                // Moving to empty slot
                SetItem(draggedEquipment, draggedQuantity);
                sourceSlot.SetEmpty();
                inventoryController?.OnItemMoved(sourceSlot, this);
                return true;
            }
            else if (currentEquipment != null && draggedEquipment != null &&
                     currentEquipment.equipmentId == draggedEquipment.equipmentId)
            {
                // Stacking same items
                currentQuantity += draggedQuantity;
                quantityText.text = currentQuantity.ToString();
                quantityText.enabled = true;
                sourceSlot.SetEmpty();
                inventoryController?.OnItemMoved(sourceSlot, this);
                return true;
            }
            else
            {
                // Swapping different items
                var tempEquipment = currentEquipment;
                var tempQuantity = currentQuantity;

                SetItem(draggedEquipment, draggedQuantity);
                sourceSlot.SetItem(tempEquipment, tempQuantity);
                inventoryController?.OnItemMoved(sourceSlot, this);
                return true;
            }
        }

        private bool HandleEquipmentToInventoryDrop(IDraggable draggable)
        {
            var draggedEquipment = draggable.GetDraggedEquipment();

            if (!isOccupied)
            {
                // Unequipping to empty slot - update visual and data without refresh
                SetItem(draggedEquipment, 1);
                inventoryController?.OnItemAdded(draggedEquipment, 1, false); // Don't refresh display
                return true;
            }
            else if (currentEquipment != null && draggedEquipment != null &&
                     currentEquipment.equipmentId == draggedEquipment.equipmentId)
            {
                // Stacking same equipment - update visual and data without refresh
                currentQuantity++;
                quantityText.text = currentQuantity.ToString();
                quantityText.enabled = true;
                inventoryController?.OnItemAdded(draggedEquipment, 1, false); // Don't refresh display
                return true;
            }
            else
            {
                // Slot is occupied with different equipment - try to find an empty slot
                if (inventoryController != null)
                {
                    var emptySlot = inventoryController.FindFirstEmptySlot();
                    if (emptySlot != null && emptySlot != this)
                    {
                        // Place in the empty slot instead - update visual and data without refresh
                        emptySlot.SetItem(draggedEquipment, 1);
                        inventoryController.OnItemAdded(draggedEquipment, 1, false); // Don't refresh display
                        return true;
                    }
                }
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
                if (isOccupied && currentEquipment != null)
                {
                    inventoryController?.InvokeItemHovered(currentEquipment);
                }
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isDragging)
            {
                transform.DOScale(originalScale, 0.2f);
                inventoryController?.InvokeItemHoverExit();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (isOccupied && currentEquipment != null && eventData.button == PointerEventData.InputButton.Left)
            {
                inventoryController?.InvokeItemSelected(currentEquipment);
            }
        }

        #endregion
    }
}