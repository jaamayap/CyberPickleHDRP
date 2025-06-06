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
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color occupiedSlotColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        [Header("Drag & Drop")]
        [SerializeField] private Canvas dragCanvas;
        [SerializeField] private float dragAlpha = 0.6f;

        private EquipmentManager equipmentManager;
        private ProfileManager profileManager;
        private List<InventorySlotController> inventorySlots = new List<InventorySlotController>();
        private Dictionary<string, EquipmentData> inventoryItems = new Dictionary<string, EquipmentData>();
        private EquipmentSlotType? currentFilter = null;
        private SortType currentSortType = SortType.Type;
        private bool isInitialized = false;

        // Drag & Drop state
        private InventorySlotController draggedSlot;
        private GameObject draggedIcon;

        public event System.Action<EquipmentData> OnItemSelected;
        public event System.Action<EquipmentData> OnItemHovered;
        public event System.Action OnItemHoverExit;

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

            // Find drag canvas if not assigned
            if (dragCanvas == null)
            {
                dragCanvas = GetComponentInParent<Canvas>();
            }

            SetupInventoryGrid();
            CreateInventorySlots();
            SetupSortDropdown();

            if (tabController != null)
            {
                tabController.Initialize(this);
            }

            LoadInventory();

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

            // Clear existing slots
            foreach (var slot in inventorySlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            inventorySlots.Clear();

            // Create new slots
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

                // Fade in animation
                var canvasGroup = slotObj.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = slotObj.AddComponent<CanvasGroup>();
                }
                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, fadeInDuration).SetDelay(i * 0.01f);
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
            if (!isInitialized) return;

            var profile = profileManager.ActiveProfile;
            if (profile == null)
            {
                Debug.LogError("[InventoryUIController] No active profile!");
                return;
            }

            // Get all unlocked equipment
            var unlockedEquipment = equipmentManager.GetUnlockedEquipment();

            // Get equipped items to exclude them from inventory
            var equippedItems = equipmentManager.GetEquippedEquipment();
            var equippedIds = new HashSet<string>();

            foreach (var slotItems in equippedItems.Values)
            {
                foreach (var item in slotItems)
                {
                    equippedIds.Add(item.equipmentId);
                }
            }

            // Filter out equipped items
            inventoryItems.Clear();
            foreach (var equipment in unlockedEquipment)
            {
                if (!equippedIds.Contains(equipment.equipmentId))
                {
                    inventoryItems[equipment.equipmentId] = equipment;
                }
            }

            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            // Clear all slots first
            foreach (var slot in inventorySlots)
            {
                slot.ClearSlot();
            }

            // Get filtered and sorted items
            var itemsToDisplay = GetFilteredAndSortedItems();

            // Assign items to slots
            for (int i = 0; i < itemsToDisplay.Count && i < inventorySlots.Count; i++)
            {
                var equipment = itemsToDisplay[i];
                var profile = profileManager.ActiveProfile;
                int level = profile.GetEquipmentLevel(equipment.equipmentId);

                inventorySlots[i].SetItem(equipment, level > 0 ? level : 1);
            }

            // Update inventory count
            UpdateInventoryCount(itemsToDisplay.Count);
        }

        private List<EquipmentData> GetFilteredAndSortedItems()
        {
            var items = inventoryItems.Values.AsEnumerable();

            // Apply filter
            if (currentFilter.HasValue)
            {
                items = items.Where(item => item.slotType == currentFilter.Value);
            }

            // Apply sorting
            switch (currentSortType)
            {
                case SortType.Name:
                    items = items.OrderBy(item => item.displayName);
                    break;
                case SortType.Level:
                    var profile = profileManager.ActiveProfile;
                    items = items.OrderByDescending(item => profile.GetEquipmentLevel(item.equipmentId));
                    break;
                case SortType.Type:
                    items = items.OrderBy(item => item.slotType).ThenBy(item => item.displayName);
                    break;
                case SortType.Recent:
                    // TODO: Track acquisition time
                    items = items.Reverse();
                    break;
            }

            return items.ToList();
        }

        #endregion

        #region Filtering and Sorting

        public void SetActiveTab(EquipmentSlotType? type)
        {
            currentFilter = type;
            RefreshDisplay();

            // Scroll to top when changing tabs
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
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

            // Create drag icon
            CreateDragIcon(slot.CurrentEquipment.equipmentIcon);

            // Make the slot semi-transparent
            var canvasGroup = slot.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = dragAlpha;
            }

            // Notify equipment hub manager
            var hubManager = GetComponentInParent<EquipmentHubManager>();
            if (hubManager != null)
            {
                // TODO: Notify that drag started
            }
        }

        public void OnItemDragEnd()
        {
            if (draggedSlot == null) return;

            // Restore slot opacity
            var canvasGroup = draggedSlot.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            // Destroy drag icon
            if (draggedIcon != null)
            {
                Destroy(draggedIcon);
                draggedIcon = null;
            }

            draggedSlot = null;
        }

        public void UpdateDragPosition(Vector2 position)
        {
            if (draggedIcon != null)
            {
                draggedIcon.transform.position = position;
            }
        }

        private void CreateDragIcon(Sprite icon)
        {
            if (dragCanvas == null || icon == null) return;

            draggedIcon = new GameObject("DragIcon");
            draggedIcon.transform.SetParent(dragCanvas.transform, false);

            var image = draggedIcon.AddComponent<Image>();
            image.sprite = icon;
            image.raycastTarget = false;

            var rect = draggedIcon.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(slotSize, slotSize);

            var group = draggedIcon.AddComponent<CanvasGroup>();
            group.alpha = 0.8f;
            group.blocksRaycasts = false;
        }

        #endregion

        #region Inventory Management

        public void AddItemToInventory(EquipmentData equipment)
        {
            if (equipment == null) return;

            if (!inventoryItems.ContainsKey(equipment.equipmentId))
            {
                inventoryItems[equipment.equipmentId] = equipment;
                RefreshDisplay();
            }
        }

        public void RemoveItemFromInventory(string equipmentId)
        {
            if (inventoryItems.ContainsKey(equipmentId))
            {
                inventoryItems.Remove(equipmentId);
                RefreshDisplay();
            }
        }

        private void UpdateInventoryCount(int count)
        {
            if (inventoryCountText != null)
            {
                inventoryCountText.text = $"{count}/{maxInventorySlots}";

                // Change color if inventory is getting full
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

        #region Item Interaction

        public void OnItemClicked(InventorySlotController slot)
        {
            if (slot == null || slot.CurrentEquipment == null) return;

            OnItemSelected?.Invoke(slot.CurrentEquipment);
        }

        public void OnItemHoverEnter(InventorySlotController slot)
        {
            if (slot == null || slot.CurrentEquipment == null) return;

            OnItemHovered?.Invoke(slot.CurrentEquipment);
        }

        public void HandleItemHoverExit(InventorySlotController slot)
        {
            OnItemHoverExit?.Invoke();
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            if (sortDropdown != null)
            {
                sortDropdown.onValueChanged.RemoveAllListeners();
            }

            // Kill any active tweens
            DOTween.Kill(this);
        }

        #endregion
    }
}