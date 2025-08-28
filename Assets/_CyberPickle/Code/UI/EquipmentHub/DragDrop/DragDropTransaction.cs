// File: UI/EquipmentHub/DragDrop/DragDropTransaction.cs
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.UI.EquipmentHub.DragDrop
{
    /// <summary>
    /// Represents a drag and drop transaction for undo/redo functionality
    /// </summary>
    public class DragDropTransaction
    {
        public DragSourceType SourceType { get; set; }
        public DropTargetType TargetType { get; set; }
        public EquipmentData Equipment { get; set; }
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public bool WasSuccessful { get; set; }
        public float TransactionCost { get; set; }
        public System.DateTime Timestamp { get; set; }

        public DragDropTransaction()
        {
            Timestamp = System.DateTime.Now;
        }
    }
}