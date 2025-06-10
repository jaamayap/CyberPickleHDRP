// File: UI/EquipmentHub/DragDrop/IDropTarget.cs
using UnityEngine;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.UI.EquipmentHub.DragDrop
{
    /// <summary>
    /// Interface for objects that can receive dropped items
    /// </summary>
    public interface IDropTarget
    {
        /// <summary>
        /// The type of drop target
        /// </summary>
        DropTargetType GetDropTargetType();

        /// <summary>
        /// Can this target accept the dragged item?
        /// </summary>
        bool CanAcceptDrop(IDraggable draggable);

        /// <summary>
        /// Preview what would happen if item is dropped (visual feedback)
        /// </summary>
        void OnDropPreview(IDraggable draggable);

        /// <summary>
        /// Cancel drop preview
        /// </summary>
        void OnDropPreviewEnd();

        /// <summary>
        /// Handle the actual drop
        /// </summary>
        bool OnDropReceived(IDraggable draggable);

        /// <summary>
        /// Get the current equipment in this slot (if any)
        /// </summary>
        EquipmentData GetCurrentEquipment();

        /// <summary>
        /// Get the target GameObject
        /// </summary>
        GameObject GetTargetObject();
    }

    /// <summary>
    /// Identifies the type of drop target
    /// </summary>
    public enum DropTargetType
    {
        InventorySlot,
        EquipmentSlot,
        TrashBin  // For future implementation
    }
}