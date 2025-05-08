// File: Assets/Code/Shop/ShopManager.cs
//
// Purpose: Manages the game's shop system, handling purchase requests,
// equipment upgrades, and shop interface integration.
// Integrates with EquipmentManager and CurrencyManager.
//
// Created: 2025-02-25
// Updated: 2025-02-25

using UnityEngine;
using System;
using System.Threading.Tasks;
using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Shop.Equipment;
using CyberPickle.Shop.Equipment.Data;
using CyberPickle.Shop.Currency;
using System.Collections.Generic;

namespace CyberPickle.Shop
{
    /// <summary>
    /// Result of a shop transaction
    /// </summary>
    public class ShopTransactionResult
    {
        /// <summary>
        /// Whether the transaction was successful
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// Description of the result
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// The cost of the transaction
        /// </summary>
        public float Cost { get; private set; }

        /// <summary>
        /// The type of currency used
        /// </summary>
        public CurrencyType CurrencyType { get; private set; }

        /// <summary>
        /// The equipment that was purchased/upgraded
        /// </summary>
        public EquipmentData Equipment { get; private set; }

        private ShopTransactionResult(bool success, string message, float cost = 0, CurrencyType currencyType = CurrencyType.NeuralCredits, EquipmentData equipment = null)
        {
            Success = success;
            Message = message;
            Cost = cost;
            CurrencyType = currencyType;
            Equipment = equipment;
        }

        /// <summary>
        /// Create a successful transaction result
        /// </summary>
        public static ShopTransactionResult Succeeded(string message, float cost, CurrencyType currencyType, EquipmentData equipment = null)
        {
            return new ShopTransactionResult(true, message, cost, currencyType, equipment);
        }

        /// <summary>
        /// Create a failed transaction result
        /// </summary>
        public static ShopTransactionResult Failed(string message)
        {
            return new ShopTransactionResult(false, message);
        }
    }

    /// <summary>
    /// Type of shop transaction
    /// </summary>
    public enum ShopTransactionType
    {
        Purchase,
        Upgrade,
        Unlock
    }

    /// <summary>
    /// Manager class for the shop system
    /// </summary>
    public class ShopManager : Manager<ShopManager>, IInitializable
    {
        [Header("Shop Settings")]
        [SerializeField] private float upgradeBaseCostMultiplier = 0.5f;
        [SerializeField] private float upgradeLevelCostMultiplier = 1.5f;

        // Events
        public event Action<EquipmentData, ShopTransactionType> OnPurchaseCompleted;
        public event Action<EquipmentData, ShopTransactionType, string> OnPurchaseFailed;

        // Dependencies
        private EquipmentManager equipmentManager;
        private CurrencyManager currencyManager;
        private ProfileManager profileManager;

        // Initialization flag
        private bool isInitialized = false;

        /// <summary>
        /// Initialize the Shop Manager
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            Debug.Log("[ShopManager] Initializing...");

            // Get dependencies
            equipmentManager = EquipmentManager.Instance;
            currencyManager = CurrencyManager.Instance;
            profileManager = ProfileManager.Instance;

            if (equipmentManager == null)
            {
                Debug.LogError("[ShopManager] EquipmentManager not found!");
                return;
            }

            if (currencyManager == null)
            {
                Debug.LogError("[ShopManager] CurrencyManager not found!");
                return;
            }

            if (profileManager == null)
            {
                Debug.LogError("[ShopManager] ProfileManager not found!");
                return;
            }

            isInitialized = true;
            Debug.Log("[ShopManager] Initialized successfully");
        }

        #region Purchase Methods

        /// <summary>
        /// Purchase a new equipment item
        /// </summary>
        /// <param name="equipmentId">ID of the equipment to purchase</param>
        /// <param name="currencyType">Type of currency to use</param>
        /// <returns>Result of the transaction</returns>
        public async Task<ShopTransactionResult> PurchaseEquipment(string equipmentId, CurrencyType currencyType)
        {
            if (!isInitialized)
                return ShopTransactionResult.Failed("Shop manager not initialized");

            // Get equipment data
            var equipment = equipmentManager.GetEquipmentById(equipmentId);
            if (equipment == null)
                return ShopTransactionResult.Failed($"Equipment with ID {equipmentId} not found");

            // Check if already unlocked
            var profile = profileManager.ActiveProfile;
            if (profile == null)
                return ShopTransactionResult.Failed("No active profile");

            if (profile.IsEquipmentUnlocked(equipmentId))
                return ShopTransactionResult.Failed($"{equipment.displayName} is already unlocked");

            // Calculate cost based on currency type
            float cost = currencyType == CurrencyType.NeuralCredits ? equipment.neuralCreditCost : equipment.cyberCoinCost;
            if (cost <= 0)
                return ShopTransactionResult.Failed($"{equipment.displayName} cannot be purchased with {currencyType}");

            // Check if player has enough currency
            if (!currencyManager.HasSufficientFunds(cost, currencyType))
            {
                string message = $"Insufficient {currencyType} for {equipment.displayName}. Required: {cost}, Available: {(currencyType == CurrencyType.NeuralCredits ? currencyManager.NeuralCredits : currencyManager.CyberCoins)}";
                OnPurchaseFailed?.Invoke(equipment, ShopTransactionType.Purchase, message);
                return ShopTransactionResult.Failed(message);
            }

            // Spend currency
            bool spentSuccess = await currencyManager.SpendCurrency(cost, currencyType);
            if (!spentSuccess)
                return ShopTransactionResult.Failed($"Failed to spend {currencyType} for {equipment.displayName}");

            // Unlock equipment
            bool unlockSuccess = equipmentManager.UnlockEquipment(equipmentId);
            if (!unlockSuccess)
            {
                // Refund currency if unlock fails
                await currencyManager.AddCurrency(cost, currencyType);
                return ShopTransactionResult.Failed($"Failed to unlock {equipment.displayName}");
            }

            // Notify purchase completed
            OnPurchaseCompleted?.Invoke(equipment, ShopTransactionType.Purchase);

            return ShopTransactionResult.Succeeded(
                $"Successfully purchased {equipment.displayName}",
                cost,
                currencyType,
                equipment
            );
        }

        /// <summary>
        /// Unlocks equipment without spending currency (for rewards, achievements, etc.)
        /// </summary>
        /// <param name="equipmentId">ID of the equipment to unlock</param>
        /// <returns>Result of the transaction</returns>
        public async Task<ShopTransactionResult> UnlockEquipmentFree(string equipmentId)
        {
            if (!isInitialized)
                return ShopTransactionResult.Failed("Shop manager not initialized");

            // Get equipment data
            var equipment = equipmentManager.GetEquipmentById(equipmentId);
            if (equipment == null)
                return ShopTransactionResult.Failed($"Equipment with ID {equipmentId} not found");

            // Check if already unlocked
            var profile = profileManager.ActiveProfile;
            if (profile == null)
                return ShopTransactionResult.Failed("No active profile");

            if (profile.IsEquipmentUnlocked(equipmentId))
                return ShopTransactionResult.Failed($"{equipment.displayName} is already unlocked");

            // Unlock equipment
            bool unlockSuccess = equipmentManager.UnlockEquipment(equipmentId);
            if (!unlockSuccess)
                return ShopTransactionResult.Failed($"Failed to unlock {equipment.displayName}");

            // Notify purchase completed (as an unlock)
            OnPurchaseCompleted?.Invoke(equipment, ShopTransactionType.Unlock);

            return ShopTransactionResult.Succeeded(
                $"Successfully unlocked {equipment.displayName}",
                0,
                CurrencyType.NeuralCredits,
                equipment
            );
        }

        /// <summary>
        /// Calculates the cost to upgrade a piece of equipment
        /// </summary>
        /// <param name="equipment">The equipment to upgrade</param>
        /// <param name="currentLevel">Current level of the equipment</param>
        /// <param name="targetLevel">Target level to upgrade to</param>
        /// <returns>The cost in Neural Credits</returns>
        public int CalculateUpgradeCost(EquipmentData equipment, int currentLevel, int targetLevel)
        {
            if (equipment == null || currentLevel >= targetLevel || currentLevel < 1 || targetLevel > equipment.maxUpgradeLevel)
                return 0;

            return equipment.GetUpgradeCost(currentLevel, targetLevel);
        }

        /// <summary>
        /// Upgrade an equipment item to a higher level
        /// </summary>
        /// <param name="equipmentId">ID of the equipment to upgrade</param>
        /// <param name="targetLevel">Target level to upgrade to</param>
        /// <returns>Result of the transaction</returns>
        /// <summary>
        /// Upgrade an equipment item to a higher level
        /// </summary>
        /// <param name="equipmentId">ID of the equipment to upgrade</param>
        /// <param name="targetLevel">Target level to upgrade to</param>
        /// <returns>Result of the transaction</returns>
        public async Task<ShopTransactionResult> UpgradeEquipment(string equipmentId, int targetLevel)
        {
            if (!isInitialized)
                return ShopTransactionResult.Failed("Shop manager not initialized");

            // Get equipment data
            var equipment = equipmentManager.GetEquipmentById(equipmentId);
            if (equipment == null)
                return ShopTransactionResult.Failed($"Equipment with ID {equipmentId} not found");

            // Check if equipment is unlocked
            var profile = profileManager.ActiveProfile;
            if (profile == null)
                return ShopTransactionResult.Failed("No active profile");

            if (!profile.IsEquipmentUnlocked(equipmentId) && !equipment.unlockedByDefault)
                return ShopTransactionResult.Failed($"{equipment.displayName} is not unlocked yet");

            // Get current equipment level
            int currentLevel = profile.GetEquipmentLevel(equipmentId);

            // If no progression data exists yet, create it (level 1)
            if (currentLevel == 0)
            {
                profile.GetOrCreateEquipmentProgression(equipmentId);
                currentLevel = 1;
            }

            // Validate target level
            if (targetLevel <= currentLevel)
                return ShopTransactionResult.Failed($"{equipment.displayName} is already at level {currentLevel}");

            if (targetLevel > equipment.maxUpgradeLevel)
                return ShopTransactionResult.Failed($"Cannot upgrade {equipment.displayName} beyond level {equipment.maxUpgradeLevel}");

            // Calculate upgrade cost
            int cost = CalculateUpgradeCost(equipment, currentLevel, targetLevel);
            if (cost <= 0)
                return ShopTransactionResult.Failed($"Invalid upgrade cost for {equipment.displayName}");

            // Check if player has enough currency (always Neural Credits for upgrades)
            if (!currencyManager.HasSufficientFunds(cost, CurrencyType.NeuralCredits))
            {
                string message = $"Insufficient Neural Credits for upgrading {equipment.displayName}. Required: {cost}, Available: {currencyManager.NeuralCredits}";
                OnPurchaseFailed?.Invoke(equipment, ShopTransactionType.Upgrade, message);
                return ShopTransactionResult.Failed(message);
            }

            // Spend currency
            bool spentSuccess = await currencyManager.SpendCurrency(cost, CurrencyType.NeuralCredits);
            if (!spentSuccess)
                return ShopTransactionResult.Failed($"Failed to spend Neural Credits for {equipment.displayName} upgrade");

            // Update equipment level in profile data
            bool upgradeSuccess = profile.UpgradeEquipment(equipmentId, targetLevel);
            if (!upgradeSuccess)
            {
                // Refund currency if upgrade fails
                await currencyManager.AddCurrency(cost, CurrencyType.NeuralCredits);
                return ShopTransactionResult.Failed($"Failed to upgrade {equipment.displayName}");
            }

            // Update the profile
            var updateResult = await profileManager.UpdateProfileAsync(profile);
            if (!updateResult.Success)
            {
                // Refund currency if profile update fails
                await currencyManager.AddCurrency(cost, CurrencyType.NeuralCredits);
                return ShopTransactionResult.Failed($"Failed to save {equipment.displayName} upgrade: {updateResult.Message}");
            }

            // Notify purchase completed
            OnPurchaseCompleted?.Invoke(equipment, ShopTransactionType.Upgrade);

            return ShopTransactionResult.Succeeded(
                $"Successfully upgraded {equipment.displayName} to level {targetLevel}",
                cost,
                CurrencyType.NeuralCredits,
                equipment
            );
        }


        #endregion

        #region Shop Query Methods

        /// <summary>
        /// Gets all equipment available for purchase (not yet unlocked)
        /// </summary>
        /// <returns>List of available equipment</returns>
        public Dictionary<EquipmentSlotType, List<EquipmentData>> GetAvailableEquipment()
        {
            Dictionary<EquipmentSlotType, List<EquipmentData>> result = new Dictionary<EquipmentSlotType, List<EquipmentData>>();

            // Initialize all slot types with empty lists
            foreach (EquipmentSlotType slotType in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                result[slotType] = new List<EquipmentData>();
            }

            if (!isInitialized || profileManager.ActiveProfile == null)
                return result;

            var profile = profileManager.ActiveProfile;

            // Get all equipment by slot type
            foreach (EquipmentSlotType slotType in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                // Get all equipment of this type
                var equipmentList = equipmentManager.GetEquipmentBySlotType(slotType);

                // Filter out already unlocked equipment
                foreach (var equipment in equipmentList)
                {
                    if (!profile.IsEquipmentUnlocked(equipment.equipmentId) && !equipment.unlockedByDefault)
                    {
                        // Check if player meets level requirement
                        if (profile.Level >= equipment.requiredPlayerLevel)
                        {
                            result[slotType].Add(equipment);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets all equipment that can be upgraded
        /// </summary>
        /// <returns>List of upgradeable equipment</returns>
        /// <summary>
        /// Gets all equipment that can be upgraded
        /// </summary>
        /// <returns>List of upgradeable equipment</returns>
        public List<EquipmentData> GetUpgradeableEquipment()
        {
            List<EquipmentData> result = new List<EquipmentData>();

            if (!isInitialized || profileManager.ActiveProfile == null)
                return result;

            var profile = profileManager.ActiveProfile;

            // Get all unlocked equipment
            var unlockedEquipment = equipmentManager.GetUnlockedEquipment();

            foreach (var equipment in unlockedEquipment)
            {
                // Check current equipment level from profile data
                int currentLevel = profile.GetEquipmentLevel(equipment.equipmentId);

                // If level is 0, it means no progression data exists yet, so it's level 1
                if (currentLevel == 0)
                    currentLevel = 1;

                // Can upgrade if current level is less than max level
                if (currentLevel < equipment.maxUpgradeLevel)
                {
                    result.Add(equipment);
                }
            }

            return result;
        }

        #endregion

        protected override void OnManagerDestroyed()
        {
            base.OnManagerDestroyed();
        }
    }
}