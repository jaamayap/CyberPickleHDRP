// File: UI/EquipmentHub/DragDrop/IDraggable.cs
using UnityEngine;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.UI.EquipmentHub.DragDrop
{
    /// <summary>
    /// Interface for objects that can be dragged in the Equipment Hub
    /// </summary>
    public interface IDraggable
    {
        /// <summary>
        /// The equipment data being dragged
        /// </summary>
        EquipmentData GetDraggedEquipment();

        /// <summary>
        /// The source type of the drag operation
        /// </summary>
        DragSourceType GetDragSourceType();

        /// <summary>
        /// Can this item be dragged in its current state?
        /// </summary>
        bool CanDrag();

        /// <summary>
        /// Called when drag operation starts
        /// </summary>
        void OnDragStarted();

        /// <summary>
        /// Called when drag operation ends (successful or not)
        /// </summary>
        void OnDragEnded(bool successful);

        /// <summary>
        /// Get the visual representation for dragging
        /// </summary>
        Sprite GetDragIcon();

        /// <summary>
        /// Get the source GameObject
        /// </summary>
        GameObject GetSourceObject();
    }

    /// <summary>
    /// Identifies where a drag operation originated from
    /// </summary>
    public enum DragSourceType
    {
        Inventory,
        Equipment,
        Shop
    }
}