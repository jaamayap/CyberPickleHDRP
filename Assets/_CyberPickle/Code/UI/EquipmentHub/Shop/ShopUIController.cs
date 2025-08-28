using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using CyberPickle.Shop;
using CyberPickle.Shop.Equipment;
using CyberPickle.Shop.Equipment.Data;
using CyberPickle.Shop.Currency;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.UI.EquipmentHub.DragDrop;

namespace CyberPickle.UI.EquipmentHub.Shop
{
    public enum ShopSortType
    {
        Name,
        Price,
        Level,
        Type,
        Rarity
    }

    public class ShopUIController : MonoBehaviour
    {
        [Header("Managers")]
        private ShopManager shopManager;
        private EquipmentManager equipmentManager;
        private CurrencyManager currencyManager;
        private ProfileManager profileManager;
        private DragDropManager dragDropManager;

        [Header("UI References")]
        [SerializeField] private Transform shopGrid;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private ShopTabController tabController;
        [SerializeField] private TMP_Dropdown sortDropdown;
        [SerializeField] private GameObject shopItemPrefab;
        [SerializeField] private TextMeshProUGUI shopTitle;

        [Header("Currency Display")]
        [SerializeField] private TextMeshProUGUI neuralCreditsText;
        [SerializeField] private TextMeshProUGUI cyberCoinsText;

        [Header("Shop Settings")]
        [SerializeField] private int itemsPerRow = 5;
        [SerializeField] private float itemSize = 180f;
        [SerializeField] private float itemSpacing = 20f;

        [Header("Visual Settings")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float refreshRate = 0.5f;

        [Header("Purchase Confirmation")]
        [SerializeField] private ShopPurchaseConfirmation purchaseConfirmation;

        // Events
        public event Action<EquipmentData> OnItemPurchased;
        public event Action<EquipmentData> OnItemHovered;
        public event Action OnItemHoverExit;

        // Runtime data
        private List<ShopItemController> shopItemPool = new List<ShopItemController>();
        private List<ShopItemController> activeShopItems = new List<ShopItemController>();
        private Dictionary<string, ShopItemController> itemControllerMap = new Dictionary<string, ShopItemController>();
        private EquipmentSlotType? currentFilter = null;
        private ShopSortType currentSortType = ShopSortType.Type;
        private int playerLevel = 1;
        private bool isInitialized = false;
        private float lastRefreshTime = 0f;

        #region Initialization

        private void Awake()
        {
            shopManager = ShopManager.Instance;
            equipmentManager = EquipmentManager.Instance;
            currencyManager = CurrencyManager.Instance;
            profileManager = ProfileManager.Instance;
            dragDropManager = DragDropManager.Instance;
        }

        private void OnEnable()
        {
            if (!isInitialized)
            {
                Initialize();
            }

            // Subscribe to events
            if (currencyManager != null)
            {
                currencyManager.OnCurrencyChanged += OnCurrencyChanged;
            }

            if (shopManager != null)
            {
                shopManager.OnItemPurchaseCompleted += OnPurchaseCompleted;
            }

            LoadShopItems();
            RefreshCurrencyDisplay();
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            if (currencyManager != null)
            {
                currencyManager.OnCurrencyChanged -= OnCurrencyChanged;
            }

            if (shopManager != null)
            {
                shopManager.OnItemPurchaseCompleted -= OnPurchaseCompleted;
            }
        }

        public void Initialize()
        {
            if (isInitialized) return;

            // Initialize components
            if (tabController != null)
            {
                tabController.Initialize(this);
            }

            // Setup sort dropdown
            if (sortDropdown != null)
            {
                sortDropdown.ClearOptions();
                sortDropdown.AddOptions(Enum.GetNames(typeof(ShopSortType)).ToList());
                sortDropdown.value = (int)currentSortType;
                sortDropdown.onValueChanged.AddListener(OnSortChanged);
            }

            // Setup grid layout
            if (shopGrid != null && shopGrid.TryGetComponent<GridLayoutGroup>(out var gridLayout))
            {
                gridLayout.cellSize = new Vector2(itemSize, itemSize);
                gridLayout.spacing = new Vector2(itemSpacing, itemSpacing);
            }

            // Get player level
            if (profileManager?.ActiveProfile != null)
            {
                playerLevel = profileManager.ActiveProfile.Level;
            }

            isInitialized = true;
        }

        #endregion

        #region Shop Loading

        public void LoadShopItems()
        {
            ClearShopItems();

            // Get available items from shop manager
            var availableEquipment = shopManager.GetAvailableEquipment();
            var availableItems = currentFilter.HasValue ? 
                availableEquipment[currentFilter.Value] : 
                availableEquipment.SelectMany(kvp => kvp.Value).ToList();

            // Apply level filter
            availableItems = availableItems.Where(item => GetItemLevel(item) <= playerLevel + 5).ToList();

            // Sort items
            availableItems = SortItems(availableItems, currentSortType);

            // Create shop items
            foreach (var equipment in availableItems)
            {
                CreateShopItem(equipment);
            }

            // Fade in animation
            AnimateShopItems();

            // Update affordability
            RefreshAffordability();
        }

        private void CreateShopItem(EquipmentData equipment)
        {
            ShopItemController shopItem = GetOrCreateShopItem();

            shopItem.transform.SetParent(shopGrid, false);
            shopItem.gameObject.SetActive(true);
            shopItem.Initialize(equipment, this);

            // Check ownership
            bool isOwned = profileManager.ActiveProfile?.IsEquipmentUnlocked(equipment.equipmentId) ?? false;
            shopItem.UpdateOwnership(isOwned);

            // Check affordability
            bool hasEnoughNC = currencyManager.NeuralCredits >= equipment.neuralCreditCost;
            bool hasEnoughCC = currencyManager.CyberCoins >= equipment.cyberCoinCost;
            bool hasEnoughFunds = equipment.neuralCreditCost > 0 ? hasEnoughNC : hasEnoughCC;
            bool meetsLevel = playerLevel >= equipment.requiredPlayerLevel;
            var requirements = new { HasSufficientFunds = hasEnoughFunds, MeetsLevelRequirement = meetsLevel, IsOwned = isOwned };
            shopItem.UpdateAffordability(requirements.HasSufficientFunds, requirements.MeetsLevelRequirement);

            activeShopItems.Add(shopItem);
            itemControllerMap[equipment.equipmentId] = shopItem;
        }

        private ShopItemController GetOrCreateShopItem()
        {
            // Object pooling
            foreach (var item in shopItemPool)
            {
                if (!item.gameObject.activeInHierarchy)
                {
                    return item;
                }
            }

            // Create new item
            GameObject newItem = Instantiate(shopItemPrefab, shopGrid);
            ShopItemController controller = newItem.GetComponent<ShopItemController>();
            shopItemPool.Add(controller);
            return controller;
        }

        private void ClearShopItems()
        {
            foreach (var item in activeShopItems)
            {
                item.gameObject.SetActive(false);
            }

            activeShopItems.Clear();
            itemControllerMap.Clear();
        }

        #endregion

        #region Filtering & Sorting

        public void FilterByType(EquipmentSlotType? type)
        {
            currentFilter = type;
            LoadShopItems();

            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        public void FilterByLevel(int maxLevel)
        {
            playerLevel = maxLevel;
            LoadShopItems();
        }

        private void OnSortChanged(int index)
        {
            currentSortType = (ShopSortType)index;
            LoadShopItems();
        }

        public void SortItems(ShopSortType sortType)
        {
            currentSortType = sortType;
            if (sortDropdown != null)
            {
                sortDropdown.value = (int)sortType;
            }
            LoadShopItems();
        }

        private List<EquipmentData> SortItems(List<EquipmentData> items, ShopSortType sortType)
        {
            switch (sortType)
            {
                case ShopSortType.Name:
                    return items.OrderBy(i => i.displayName).ToList();

                case ShopSortType.Price:
                    return items.OrderBy(i => i.neuralCreditCost > 0 ? i.neuralCreditCost : i.cyberCoinCost).ToList();

                case ShopSortType.Level:
                    return items.OrderBy(i => GetItemLevel(i)).ToList();

                case ShopSortType.Type:
                    return items.OrderBy(i => i.slotType).ThenBy(i => i.displayName).ToList();

                case ShopSortType.Rarity:
                    return items.OrderByDescending(i => GetItemRarity(i)).ThenBy(i => i.displayName).ToList();

                default:
                    return items;
            }
        }

        #endregion

        #region Currency & Affordability

        public void RefreshAffordability()
        {
            if (Time.time - lastRefreshTime < refreshRate) return;
            lastRefreshTime = Time.time;

            foreach (var shopItem in activeShopItems)
            {
                if (shopItem.gameObject.activeInHierarchy)
                {
                    var eq = shopItem.Equipment;
                    bool hasEnoughNC = currencyManager.NeuralCredits >= eq.neuralCreditCost;
                    bool hasEnoughCC = currencyManager.CyberCoins >= eq.cyberCoinCost;
                    bool hasEnoughFunds = eq.neuralCreditCost > 0 ? hasEnoughNC : hasEnoughCC;
                    bool meetsLevel = playerLevel >= eq.requiredPlayerLevel;
                    var requirements = new { HasSufficientFunds = hasEnoughFunds, MeetsLevelRequirement = meetsLevel };
                    shopItem.UpdateAffordability(requirements.HasSufficientFunds, requirements.MeetsLevelRequirement);
                }
            }
        }

        private void OnCurrencyChanged(CurrencyType type, float oldAmount, float newAmount)
        {
            RefreshCurrencyDisplay();
            RefreshAffordability();
        }

        private void RefreshCurrencyDisplay()
        {
            if (neuralCreditsText != null)
            {
                neuralCreditsText.text = currencyManager.NeuralCredits.ToString("N0");
            }

            if (cyberCoinsText != null)
            {
                cyberCoinsText.text = currencyManager.CyberCoins.ToString("N0");
            }
        }

        #endregion

        #region Purchase Flow

        public void OnItemPurchaseRequested(ShopItemController item)
        {
            if (item == null || item.Equipment == null) return;

            // Check requirements
            var eq = item.Equipment;
            bool hasEnoughNC = currencyManager.NeuralCredits >= eq.neuralCreditCost;
            bool hasEnoughCC = currencyManager.CyberCoins >= eq.cyberCoinCost;
            bool hasEnoughFunds = eq.neuralCreditCost > 0 ? hasEnoughNC : hasEnoughCC;
            bool meetsLevel = playerLevel >= eq.requiredPlayerLevel;
            bool isOwned = profileManager.ActiveProfile?.IsEquipmentUnlocked(eq.equipmentId) ?? false;
            var requirements = new { HasSufficientFunds = hasEnoughFunds, MeetsLevelRequirement = meetsLevel, IsOwned = isOwned };

            if (!requirements.MeetsLevelRequirement)
            {
                ShowLevelRequirementMessage(item.Equipment);
                return;
            }

            if (!requirements.HasSufficientFunds)
            {
                ShowInsufficientFundsMessage(item.Equipment);
                return;
            }

            if (requirements.IsOwned)
            {
                ShowAlreadyOwnedMessage(item.Equipment);
                return;
            }

            // Show confirmation
            ShowPurchaseConfirmation(item.Equipment);
        }

        private void ShowPurchaseConfirmation(EquipmentData equipment)
        {
            if (purchaseConfirmation != null)
            {
                float currentBalance = equipment.neuralCreditCost > 0 ? currencyManager.NeuralCredits : currencyManager.CyberCoins;
                purchaseConfirmation.Show(equipment, currentBalance, (confirmed) =>
                {
                    if (confirmed)
                    {
                        CompletePurchase(equipment);
                    }
                });
            }
            else
            {
                // Direct purchase without confirmation
                CompletePurchase(equipment);
            }
        }

        private async void CompletePurchase(EquipmentData equipment)
        {
            var result = await shopManager.PurchaseItemAsync(equipment);

            if (result.Success)
            {
                OnItemPurchased?.Invoke(equipment);
                PlayPurchaseEffects(equipment);
            }
            else
            {
                ShowPurchaseError(result.Message);
            }
        }

        private void OnPurchaseCompleted(ShopTransactionResult result)
        {
            if (result.Success && result.Equipment != null)
            {
                // Update the shop item display
                if (itemControllerMap.TryGetValue(result.Equipment.equipmentId, out var shopItem))
                {
                    shopItem.UpdateOwnership(true);
                    shopItem.AnimatePurchase();
                }

                RefreshAffordability();
            }
        }

        #endregion

        #region Visual Effects

        private void AnimateShopItems()
        {
            for (int i = 0; i < activeShopItems.Count; i++)
            {
                var item = activeShopItems[i];
                if (item.gameObject.activeInHierarchy)
                {
                    item.transform.localScale = Vector3.zero;
                    item.transform.DOScale(Vector3.one, fadeInDuration)
                        .SetDelay(i * 0.02f)
                        .SetEase(Ease.OutBack);
                }
            }
        }

        private void PlayPurchaseEffects(EquipmentData equipment)
        {
            // This would trigger particle effects, sounds, etc.
            Debug.Log($"Purchase successful: {equipment.displayName}");
        }

        #endregion

        #region Helper Methods

        private int GetItemLevel(EquipmentData equipment)
        {
            // Implement based on your equipment level system
            return equipment.requiredPlayerLevel;
        }

        private int GetItemRarity(EquipmentData equipment)
        {
            // Implement based on your rarity system
            return 0;
        }

        private void ShowInsufficientFundsMessage(EquipmentData equipment)
        {
            string currencyType = equipment.neuralCreditCost > 0 ? "Neural Credits" : "CyberCoins";
            Debug.Log($"Insufficient {currencyType} to purchase {equipment.displayName}");
        }

        private void ShowLevelRequirementMessage(EquipmentData equipment)
        {
            Debug.Log($"Level {equipment.requiredPlayerLevel} required to purchase {equipment.displayName}");
        }

        private void ShowAlreadyOwnedMessage(EquipmentData equipment)
        {
            Debug.Log($"You already own {equipment.displayName}");
        }

        private void ShowPurchaseError(string message)
        {
            Debug.LogError($"Purchase failed: {message}");
        }

        #endregion

        #region Public Methods

        public void InvokeItemHovered(EquipmentData equipment)
        {
            OnItemHovered?.Invoke(equipment);
        }

        public void InvokeItemHoverExit()
        {
            OnItemHoverExit?.Invoke();
        }

        #endregion

        private void OnDestroy()
        {
            // Clean up
            if (sortDropdown != null)
            {
                sortDropdown.onValueChanged.RemoveAllListeners();
            }

            DOTween.Kill(this);
        }
    }
}