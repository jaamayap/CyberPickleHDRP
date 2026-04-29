using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using CyberPickle.Shop.Equipment;
using CyberPickle.Shop.Equipment.Data;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Services.Authentication.Data;

namespace CyberPickle.UI.EquipmentHub
{
    public enum SortType
    {
        Name,
        Level,
        Type,
        Recent
    }

    public class InventoryUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform inventoryGrid;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private InventoryTabController tabController;
        [SerializeField] private TMP_Dropdown sortDropdown;
        [SerializeField] private TextMeshProUGUI inventoryCountText;
        [SerializeField] private GameObject inventorySlotPrefab;

        [Header("Inventory Settings")]
        [SerializeField] private int maxInventorySlots = 100;
        [SerializeField] private int slotsPerRow = 10;
        [SerializeField] private float slotSize = 80f;
        [SerializeField] private float slotSpacing = 10f;

        [Header("Visual Settings")]
        [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color occupiedSlotColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        [Header("Drag & Drop")]
        [SerializeField] private Canvas dragCanvas;
        [SerializeField] private float dragAlpha = 0.6f;

        private EquipmentManager equipmentManager;
        private ProfileManager profileManager;
        private List<InventorySlotController> inventorySlots = new List<InventorySlotController>();

        // This is our source of truth for inventory items - slot-based array to preserve positions
        private InventoryItemData[] inventoryItems;

        private EquipmentSlotType? currentFilter = null;
        private SortType currentSortType = SortType.Type;
        private bool isInitialized = false;
        private InventorySlotController draggedSlot;
        private GameObject draggedIcon;

        public event System.Action<EquipmentData> OnItemSelected;
        public event System.Action<EquipmentData> OnItemHovered;
        public event System.Action OnItemHoverExit;

        // Helper class to store item data with quantity
        [System.Serializable]
        private class InventoryItemData
        {
            public EquipmentData equipment;
            public int quantity;
            public int slotIndex; // Track which slot this item is in

            public InventoryItemData(EquipmentData equipment, int quantity = 1, int slotIndex = -1)
            {
                this.equipment = equipment;
                this.quantity = quantity;
                this.slotIndex = slotIndex;
            }
        }

        #region Initialization

        public void Initialize()
        {
            if (isInitialized) return;

            equipmentManager = EquipmentManager.Instance;
            profileManager = ProfileManager.Instance;

            if (equipmentManager == null || profileManager == null)
            {
                Debug.LogError("[InventoryUIController] Required managers not found!");
                return;
            }

            if (dragCanvas == null)
            {
                dragCanvas = GetComponentInParent<Canvas>();
            }

            // Initialize inventory array
            inventoryItems = new InventoryItemData[maxInventorySlots];
            
            SetupInventoryGrid();
            CreateInventorySlots();
            SetupSortDropdown();

            if (tabController != null)
            {
                tabController.Initialize(this);
            }

            LoadInventory(false); // Don't animate during initialization  
            isInitialized = true;
        }

        private void SetupInventoryGrid()
        {
            if (inventoryGrid == null) return;

            var gridLayout = inventoryGrid.GetComponent<GridLayoutGroup>();
            if (gridLayout == null)
            {
                gridLayout = inventoryGrid.gameObject.AddComponent<GridLayoutGroup>();
            }

            gridLayout.cellSize = new Vector2(slotSize, slotSize);
            gridLayout.spacing = new Vector2(slotSpacing, slotSpacing);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = slotsPerRow;
        }

        private void CreateInventorySlots()
        {
            if (inventorySlotPrefab == null || inventoryGrid == null) return;

            // Kill all tweens on old slots before destroying them
            foreach (var slot in inventorySlots)
            {
                if (slot != null) 
                {
                    DOTween.Kill(slot.gameObject);
                    Destroy(slot.gameObject);
                }
            }
            inventorySlots.Clear();

            for (int i = 0; i < maxInventorySlots; i++)
            {
                GameObject slotObj = Instantiate(inventorySlotPrefab, inventoryGrid);
                slotObj.name = $"InventorySlot_{i}";

                var slotController = slotObj.GetComponent<InventorySlotController>();
                if (slotController == null)
                {
                    slotController = slotObj.AddComponent<InventorySlotController>();
                }

                slotController.Initialize(i, this);
                inventorySlots.Add(slotController);
            }
        }


        private void SetupSortDropdown()
        {
            if (sortDropdown == null) return;

            sortDropdown.ClearOptions();
            var options = new List<string> { "Name", "Level", "Type", "Recent" };
            sortDropdown.AddOptions(options);
            sortDropdown.value = (int)currentSortType;
            sortDropdown.onValueChanged.AddListener(OnSortChanged);
        }

        #endregion

        #region Inventory Loading

        public void LoadInventory()
        {
            LoadInventory(true);
        }

        public void LoadInventory(bool animate)
        {
            if (!isInitialized && animate) return; // Allow non-animated loading during initialization

            var profile = profileManager.ActiveProfile;
            if (profile == null)
            {
                Debug.LogError("[InventoryUIController] No active profile!");
                return;
            }

            // Get unlocked equipment
            var unlockedEquipment = equipmentManager.GetUnlockedEquipment();
            var equippedItems = equipmentManager.GetEquippedEquipment();

            // Create a set of equipped item IDs for faster lookup
            var equippedIds = new HashSet<string>();
            foreach (var slotItems in equippedItems.Values)
            {
                foreach (var item in slotItems)
                {
                    equippedIds.Add(item.equipmentId);
                }
            }

            // Clear and rebuild inventory items
            for (int i = 0; i < inventoryItems.Length; i++)
            {
                inventoryItems[i] = null;
            }
            
            int slotIndex = 0;
            foreach (var equipment in unlockedEquipment)
            {
                if (!equippedIds.Contains(equipment.equipmentId) && slotIndex < inventoryItems.Length)
                {
                    // Get the level/quantity from profile
                    int level = profile.GetEquipmentLevel(equipment.equipmentId);
                    inventoryItems[slotIndex] = new InventoryItemData(equipment, level > 0 ? level : 1, slotIndex);
                    slotIndex++;
                }
            }

            RefreshDisplay(animate);
        }

        public void RefreshDisplay()
        {
            RefreshDisplay(true);
        }

        public void RefreshDisplay(bool animate)
        {
            // Clear all slots first
            foreach (var slot in inventorySlots)
            {
                slot.ClearSlot(animate);
            }

            // Assign items to slots based on their actual positions
            int itemCount = 0;
            for (int i = 0; i < inventoryItems.Length && i < inventorySlots.Count; i++)
            {
                var itemData = inventoryItems[i];
                if (itemData != null && ShouldDisplayItem(itemData))
                {
                    inventorySlots[i].SetItem(itemData.equipment, itemData.quantity);
                    itemCount++;
                }
            }

            UpdateInventoryCount(itemCount);
        }

        private bool ShouldDisplayItem(InventoryItemData itemData)
        {
            if (itemData?.equipment == null) return false;
            
            // Apply filter
            if (currentFilter.HasValue)
            {
                return itemData.equipment.slotType == currentFilter.Value;
            }
            
            return true;
        }


        #endregion

        #region Filtering and Sorting

        public void SetActiveTab(EquipmentSlotType? type)
        {
            currentFilter = type;
            RefreshDisplay();

            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }
        public void InvokeItemSelected(EquipmentData equipment)
        {
            OnItemSelected?.Invoke(equipment);
        }

        public void InvokeItemHovered(EquipmentData equipment)
        {
            OnItemHovered?.Invoke(equipment);
        }

        public void InvokeItemHoverExit()
        {
            OnItemHoverExit?.Invoke();
        }
        public void FilterByType(EquipmentSlotType type)
        {
            SetActiveTab(type);
        }

        public void ClearFilter()
        {
            SetActiveTab(null);
        }

        private void OnSortChanged(int index)
        {
            currentSortType = (SortType)index;
            RefreshDisplay();
        }

        public void SortInventory(SortType sortType)
        {
            currentSortType = sortType;
            if (sortDropdown != null)
            {
                sortDropdown.value = (int)sortType;
            }
            RefreshDisplay();
        }

        #endregion

        #region Drag & Drop

        public void OnItemDragStart(InventorySlotController slot)
        {
            if (slot == null || slot.CurrentEquipment == null) return;
            draggedSlot = slot;
        }

        public void OnItemDragEnd()
        {
            draggedSlot = null;
        }

        // Called when an item is moved between slots
        public void OnItemMoved(InventorySlotController fromSlot, InventorySlotController toSlot)
        {
            if (fromSlot == null || toSlot == null) return;

            int fromIndex = fromSlot.SlotIndex;
            int toIndex = toSlot.SlotIndex;

            if (fromIndex >= 0 && fromIndex < inventoryItems.Length && 
                toIndex >= 0 && toIndex < inventoryItems.Length)
            {
                // Swap the items in the array
                var temp = inventoryItems[fromIndex];
                inventoryItems[fromIndex] = inventoryItems[toIndex];
                inventoryItems[toIndex] = temp;

                // Update slot indices if items exist
                if (inventoryItems[fromIndex] != null)
                    inventoryItems[fromIndex].slotIndex = fromIndex;
                if (inventoryItems[toIndex] != null)
                    inventoryItems[toIndex].slotIndex = toIndex;
            }
        }

        // Called when an item is removed from inventory (equipped, sold, etc.)
        public void OnItemRemoved(string equipmentId)
        {
            OnItemRemoved(equipmentId, true);
        }

        // Called when an item is removed from inventory with option to refresh display
        public void OnItemRemoved(string equipmentId, bool refreshDisplay)
        {
            // Find and remove the item from the array
            for (int i = 0; i < inventoryItems.Length; i++)
            {
                if (inventoryItems[i]?.equipment?.equipmentId == equipmentId)
                {
                    inventoryItems[i] = null;
                    if (refreshDisplay)
                    {
                        RefreshDisplay();
                    }
                    break;
                }
            }
        }

        // Called when an item is added to inventory
        public void OnItemAdded(EquipmentData equipment, int quantity = 1)
        {
            OnItemAdded(equipment, quantity, true);
        }

        // Called when an item is added to inventory with option to refresh display
        public void OnItemAdded(EquipmentData equipment, int quantity, bool refreshDisplay)
        {
            if (equipment == null) return;

            // Try to find existing item to stack
            for (int i = 0; i < inventoryItems.Length; i++)
            {
                if (inventoryItems[i]?.equipment?.equipmentId == equipment.equipmentId)
                {
                    inventoryItems[i].quantity += quantity;
                    if (refreshDisplay)
                    {
                        RefreshDisplay();
                    }
                    return;
                }
            }

            // Find first empty slot for new item
            for (int i = 0; i < inventoryItems.Length; i++)
            {
                if (inventoryItems[i] == null)
                {
                    inventoryItems[i] = new InventoryItemData(equipment, quantity, i);
                    if (refreshDisplay)
                    {
                        RefreshDisplay();
                    }
                    return;
                }
            }

            // No space available
            Debug.LogWarning("Inventory is full, cannot add item: " + equipment.displayName);
        }

        public void UpdateDragPosition(Vector2 position)
        {
            // Implementation for drag visual
        }

        private void CreateDragIcon(Sprite icon)
        {
            // Implementation for drag icon
        }

        #endregion

        #region Inventory Management

        public void AddItemToInventory(EquipmentData equipment)
        {
            OnItemAdded(equipment, 1);
        }

        public void RemoveItemFromInventory(string equipmentId)
        {
            OnItemRemoved(equipmentId);
        }

        private void UpdateInventoryCount(int count)
        {
            if (inventoryCountText != null)
            {
                inventoryCountText.text = $"{count}/{maxInventorySlots}";

                if (count >= maxInventorySlots * 0.9f)
                {
                    inventoryCountText.color = Color.red;
                }
                else if (count >= maxInventorySlots * 0.7f)
                {
                    inventoryCountText.color = Color.yellow;
                }
                else
                {
                    inventoryCountText.color = Color.white;
                }
            }
        }

        #endregion

        #region Public Properties

        public int MaxSlots => maxInventorySlots;
        public int UsedSlots => inventoryItems.Count(item => item != null);
        public bool IsFull => UsedSlots >= MaxSlots;
        public InventorySlotController DraggedSlot => draggedSlot;

        public InventorySlotController FindFirstEmptySlot()
        {
            return inventorySlots.FirstOrDefault(slot => !slot.IsOccupied);
        }

        #endregion
    }
}