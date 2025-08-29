// File: UI/EquipmentHub/DragDrop/DragDropManager.cs
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using CyberPickle.Core.Management;
using CyberPickle.Shop.Equipment.Data;
using DG.Tweening;

namespace CyberPickle.UI.EquipmentHub.DragDrop
{
    /// <summary>
    /// Centralized manager for all drag and drop operations in the Equipment Hub
    /// </summary>
    public class DragDropManager : Manager<DragDropManager>
    {
        [Header("Drag Visual Settings")]
        [SerializeField] private float dragIconAlpha = 0.8f;
        [SerializeField] private Vector2 dragIconOffset = new Vector2(0, -20f);
        [SerializeField] private float dragIconScale = 1.1f;

        [Header("Drop Validation Colors")]
        [SerializeField] private Color validDropColor = new Color(0.2f, 1f, 0.2f, 0.5f);
        [SerializeField] private Color invalidDropColor = new Color(1f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color neutralColor = new Color(1f, 1f, 1f, 0.3f);

        // Events
        public event Action<IDraggable> OnDragStarted;
        public event Action<IDraggable, bool> OnDragEnded;
        public event Action<IDraggable, IDropTarget> OnDropCompleted;
        public event Action<DragSourceType, DropTargetType> OnInvalidDrop;

        // Current drag state
        private IDraggable currentDraggable;
        private GameObject dragVisual;
        private Canvas dragCanvas;
        private List<IDropTarget> highlightedTargets = new List<IDropTarget>();

        // Validation rules
        private Dictionary<(DragSourceType, DropTargetType), Func<IDraggable, IDropTarget, bool>> validationRules;

        #region Initialization

        protected override void Awake()
        {
            base.Awake();
            InitializeValidationRules();
            FindOrCreateDragCanvas();
        }

        private void InitializeValidationRules()
        {
            validationRules = new Dictionary<(DragSourceType, DropTargetType), Func<IDraggable, IDropTarget, bool>>();

            // Shop to Inventory (purchase)
            validationRules[(DragSourceType.Shop, DropTargetType.InventorySlot)] = ValidateShopToInventory;

            // Inventory to Equipment (equip)
            validationRules[(DragSourceType.Inventory, DropTargetType.EquipmentSlot)] = ValidateInventoryToEquipment;

            // Equipment to Inventory (unequip)
            validationRules[(DragSourceType.Equipment, DropTargetType.InventorySlot)] = ValidateEquipmentToInventory;

            // Inventory to Inventory (rearrange)
            validationRules[(DragSourceType.Inventory, DropTargetType.InventorySlot)] = ValidateInventoryToInventory;

            // Equipment to Equipment (swap)
            validationRules[(DragSourceType.Equipment, DropTargetType.EquipmentSlot)] = ValidateEquipmentToEquipment;
        }

        private void FindOrCreateDragCanvas()
        {
            // Try to find existing drag canvas
            var existingCanvas = GameObject.Find("DragCanvas");
            if (existingCanvas != null)
            {
                dragCanvas = existingCanvas.GetComponent<Canvas>();
            }
            else
            {
                // Create drag canvas
                var dragCanvasGO = new GameObject("DragCanvas");
                dragCanvas = dragCanvasGO.AddComponent<Canvas>();
                dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                dragCanvas.sortingOrder = 999; // Ensure it's on top

                dragCanvasGO.AddComponent<CanvasScaler>();
                dragCanvasGO.AddComponent<GraphicRaycaster>();

                DontDestroyOnLoad(dragCanvasGO);
            }
        }

        #endregion

        #region Drag Operations

        public bool StartDrag(IDraggable draggable, Vector2 pointerPosition)
        {
            if (draggable == null || !draggable.CanDrag())
                return false;

            if (currentDraggable != null)
            {
                CancelDrag();
            }

            currentDraggable = draggable;
            CreateDragVisual(draggable.GetDragIcon(), pointerPosition);

            draggable.OnDragStarted();
            OnDragStarted?.Invoke(draggable);

            return true;
        }

        public void UpdateDrag(Vector2 pointerPosition)
        {
            if (dragVisual != null)
            {
                dragVisual.transform.position = pointerPosition + dragIconOffset;
            }
        }

        public bool CompleteDrop(IDropTarget dropTarget, Vector2 pointerPosition)
        {
            if (currentDraggable == null || dropTarget == null)
            {
                CancelDrag();
                return false;
            }

            bool success = false;

            // Validate drop
            if (ValidateDrop(currentDraggable, dropTarget))
            {
                // Perform the drop
                success = dropTarget.OnDropReceived(currentDraggable);

                if (success)
                {
                    OnDropCompleted?.Invoke(currentDraggable, dropTarget);
                }
            }
            else
            {
                OnInvalidDrop?.Invoke(currentDraggable.GetDragSourceType(), dropTarget.GetDropTargetType());
            }

            EndDrag(success);
            return success;
        }

        public void CancelDrag()
        {
            if (currentDraggable != null)
            {
                EndDrag(false);
            }
        }

        public void EndDrag(bool successful)
        {
            ClearHighlightedTargets();
            DestroyDragVisual();

            if (currentDraggable != null)
            {
                currentDraggable.OnDragEnded(successful);
                OnDragEnded?.Invoke(currentDraggable, successful);
                currentDraggable = null;
            }
        }

        #endregion

        #region Visual Management

        private void CreateDragVisual(Sprite icon, Vector2 position)
        {
            if (dragCanvas == null || icon == null)
                return;

            dragVisual = new GameObject("DragIcon");
            dragVisual.transform.SetParent(dragCanvas.transform, false);
            dragVisual.transform.position = position + dragIconOffset;

            var image = dragVisual.AddComponent<Image>();
            image.sprite = icon;
            image.raycastTarget = false;

            var canvasGroup = dragVisual.AddComponent<CanvasGroup>();
            canvasGroup.alpha = dragIconAlpha;
            canvasGroup.blocksRaycasts = false;

            // Add subtle animation
            dragVisual.transform.localScale = Vector3.one * dragIconScale;
            dragVisual.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
        }

        private void DestroyDragVisual()
        {
            if (dragVisual != null)
            {
                // Fade out before destroying
                var canvasGroup = dragVisual.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.DOFade(0f, 0.1f).OnComplete(() => {
                        Destroy(dragVisual);
                        dragVisual = null;
                    });
                }
                else
                {
                    Destroy(dragVisual);
                    dragVisual = null;
                }
            }
        }

        #endregion

        #region Drop Target Highlighting

        public void HighlightValidTargets(List<IDropTarget> targets)
        {
            ClearHighlightedTargets();

            foreach (var target in targets)
            {
                if (target != null && ValidateDrop(currentDraggable, target))
                {
                    HighlightTarget(target, true);
                    highlightedTargets.Add(target);
                }
            }
        }

        public void HighlightTarget(IDropTarget target, bool isValid)
        {
            if (target == null)
                return;

            var targetGO = target.GetTargetObject();
            if (targetGO == null)
                return;

            var image = targetGO.GetComponent<Image>();
            if (image != null)
            {
                Color targetColor = isValid ? validDropColor : invalidDropColor;
                image.DOColor(targetColor, 0.2f);
            }

            // Also handle outline or border if present
            var outline = targetGO.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = true;
                outline.effectColor = isValid ? validDropColor : invalidDropColor;
            }
        }

        private void ClearHighlightedTargets()
        {
            foreach (var target in highlightedTargets)
            {
                if (target != null)
                {
                    var targetGO = target.GetTargetObject();
                    if (targetGO != null)
                    {
                        var image = targetGO.GetComponent<Image>();
                        if (image != null)
                        {
                            image.DOColor(Color.white, 0.2f);
                        }

                        var outline = targetGO.GetComponent<Outline>();
                        if (outline != null)
                        {
                            outline.enabled = false;
                        }
                    }
                }
            }
            highlightedTargets.Clear();
        }

        #endregion

        #region Validation

        public bool ValidateDrop(IDraggable draggable, IDropTarget target)
        {
            if (draggable == null || target == null)
                return false;

            var key = (draggable.GetDragSourceType(), target.GetDropTargetType());

            if (validationRules.TryGetValue(key, out var validator))
            {
                return validator(draggable, target);
            }

            return false;
        }

        // Validation methods
        private bool ValidateShopToInventory(IDraggable draggable, IDropTarget target)
        {
            // Check if player has enough currency
            // This will be implemented when ShopManager integration is complete
            return true; // Placeholder
        }

        private bool ValidateInventoryToEquipment(IDraggable draggable, IDropTarget target)
        {
            var equipment = draggable.GetDraggedEquipment();
            if (equipment == null)
                return false;

            // Check if equipment type matches slot type
            var equipmentSlot = target as EquipmentSlotController;
            if (equipmentSlot != null)
            {
                return equipmentSlot.SlotType == equipment.slotType;
            }

            return false;
        }

        private bool ValidateEquipmentToInventory(IDraggable draggable, IDropTarget target)
        {
            // Equipment can always be unequipped to inventory
            if (target is InventorySlotController inventorySlot)
            {
                return !inventorySlot.IsOccupied || inventorySlot.CanAcceptDrop(draggable);
            }
            return false;
        }

        private bool ValidateInventoryToInventory(IDraggable draggable, IDropTarget target)
        {
            // Can always rearrange within inventory
            return true;
        }

        private bool ValidateEquipmentToEquipment(IDraggable draggable, IDropTarget target)
        {
            var sourceEquipment = draggable.GetDraggedEquipment();
            var targetEquipment = target.GetCurrentEquipment();

            if (sourceEquipment == null)
                return false;

            // Check if equipment types are compatible for swapping
            var targetSlot = target as EquipmentSlotController;
            if (targetSlot != null)
            {
                // Both slots must be compatible with each other's equipment
                bool sourceCanGoToTarget = targetSlot.SlotType == sourceEquipment.slotType;
                bool targetCanGoToSource = true; // Will need source slot reference for full validation

                return sourceCanGoToTarget && targetCanGoToSource;
            }

            return false;
        }

        #endregion

        #region Utility Methods

        public bool IsDragging()
        {
            return currentDraggable != null;
        }

        public IDraggable GetCurrentDraggable()
        {
            return currentDraggable;
        }

        public DragSourceType? GetCurrentDragSourceType()
        {
            return currentDraggable?.GetDragSourceType();
        }

        #endregion
    }
}