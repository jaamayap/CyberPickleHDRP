// File: Assets/Code/Shop/Currency/CurrencyManager.cs
//
// Purpose: Manages the game's currency system including Neural Credits and CyberCoins.
// Handles currency transactions, persistence, and provides events for UI updates.
// Works with the profile system to persist currency data.
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
using CyberPickle.Core.Events;

namespace CyberPickle.Shop.Currency
{
    /// <summary>
    /// Manages the game's currency system including earning, spending, and displaying currencies
    /// </summary>
    public class CurrencyManager : Manager<CurrencyManager>, IInitializable
    {
        [Header("Currency Settings")]
        [SerializeField] private float neuralCreditStartAmount = 500f;
        [SerializeField] private float cyberCoinStartAmount = 50f;

        // Events
        public event Action<CurrencyType, float, float> OnCurrencyChanged;  // Type, OldAmount, NewAmount
        public event Action<CurrencyType, float> OnCurrencyAdded;           // Type, Amount
        public event Action<CurrencyType, float> OnCurrencySpent;           // Type, Amount
        public event Action<CurrencyType> OnInsufficientFunds;              // Type

        // Dependencies
        private ProfileManager profileManager;

        // Runtime cache
        private float cachedNeuralCredits;
        private float cachedCyberCoins;
        private bool isInitialized = false;

        /// <summary>
        /// Get the current amount of Neural Credits
        /// </summary>
        public float NeuralCredits => cachedNeuralCredits;

        /// <summary>
        /// Get the current amount of CyberCoins
        /// </summary>
        public float CyberCoins => cachedCyberCoins;

        /// <summary>
        /// Initialize the Currency Manager
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            Debug.Log("[CurrencyManager] Initializing...");

            // Get profile manager reference
            profileManager = ProfileManager.Instance;
            if (profileManager == null)
            {
                Debug.LogError("[CurrencyManager] ProfileManager not found!");
                return;
            }

            // Subscribe to profile events
            profileManager.SubscribeToProfileSwitched(OnProfileSwitched);
            profileManager.SubscribeToNewProfileCreated(OnNewProfileCreated);

            // Initialize cache from current profile
            RefreshCurrencyCache();

            isInitialized = true;
            Debug.Log("[CurrencyManager] Initialized successfully");
        }

        /// <summary>
        /// Refresh the currency cache from the current profile
        /// </summary>
        private void RefreshCurrencyCache()
        {
            var profile = profileManager.ActiveProfile;
            if (profile != null)
            {
                cachedNeuralCredits = profile.NeuralCredits;
                cachedCyberCoins = profile.CyberCoins;
                Debug.Log($"[CurrencyManager] Refreshed currency cache: NC: {cachedNeuralCredits}, CC: {cachedCyberCoins}");
            }
            else
            {
                cachedNeuralCredits = 0;
                cachedCyberCoins = 0;
                Debug.LogWarning("[CurrencyManager] No active profile found during cache refresh");
            }
        }

        /// <summary>
        /// Event handler for when a profile is switched
        /// </summary>
        private void OnProfileSwitched(string profileId)
        {
            Debug.Log($"[CurrencyManager] Profile switched to {profileId}, refreshing currency");
            RefreshCurrencyCache();
        }

        /// <summary>
        /// Event handler for when a new profile is created
        /// </summary>
        private void OnNewProfileCreated(string profileId)
        {
            Debug.Log($"[CurrencyManager] New profile created: {profileId}, initializing starting currency");
            SetupInitialCurrency(profileId);
        }

        /// <summary>
        /// Sets up the initial currency for a new profile
        /// </summary>
        private async void SetupInitialCurrency(string profileId)
        {
            var profile = profileManager.GetProfile(profileId);
            if (profile == null) return;

            // Add starting currency
            profile.AddCurrency(neuralCreditStartAmount, CurrencyType.NeuralCredits);
            profile.AddCurrency(cyberCoinStartAmount, CurrencyType.CyberCoins);

            // Update profile
            await profileManager.UpdateProfileAsync(profile);

            // Refresh cache if this is the active profile
            if (profileManager.ActiveProfile?.ProfileId == profileId)
            {
                RefreshCurrencyCache();
            }

            Debug.Log($"[CurrencyManager] Initial currency setup for profile {profileId}: NC: {neuralCreditStartAmount}, CC: {cyberCoinStartAmount}");
        }

        #region Currency Operations

        /// <summary>
        /// Add currency to the player's balance
        /// </summary>
        /// <param name="amount">Amount to add (must be positive)</param>
        /// <param name="type">Type of currency to add</param>
        /// <returns>True if currency was added successfully, false otherwise</returns>
        public async Task<bool> AddCurrency(float amount, CurrencyType type)
        {
            if (!isInitialized || amount <= 0)
                return false;

            var profile = profileManager.ActiveProfile;
            if (profile == null)
            {
                Debug.LogError("[CurrencyManager] No active profile!");
                return false;
            }

            // Cache old values for event
            float oldAmount = type == CurrencyType.NeuralCredits ? cachedNeuralCredits : cachedCyberCoins;

            // Add currency to profile
            bool success = profile.AddCurrency(amount, type);
            if (!success)
            {
                Debug.LogError($"[CurrencyManager] Failed to add {amount} {type} currency");
                return false;
            }

            // Update profile
            var result = await profileManager.UpdateProfileAsync(profile);
            if (!result.Success)
            {
                Debug.LogError($"[CurrencyManager] Failed to update profile: {result.Message}");
                return false;
            }

            // Update cache
            if (type == CurrencyType.NeuralCredits)
                cachedNeuralCredits += amount;
            else
                cachedCyberCoins += amount;

            // Fire events
            float newAmount = type == CurrencyType.NeuralCredits ? cachedNeuralCredits : cachedCyberCoins;
            OnCurrencyAdded?.Invoke(type, amount);
            OnCurrencyChanged?.Invoke(type, oldAmount, newAmount);

            GameEvents.OnCurrencyChanged?.Invoke((int)newAmount);

            Debug.Log($"[CurrencyManager] Added {amount} {type}. New balance: {newAmount}");
            return true;
        }

        /// <summary>
        /// Spend currency from the player's balance
        /// </summary>
        /// <param name="amount">Amount to spend (must be positive)</param>
        /// <param name="type">Type of currency to spend</param>
        /// <returns>True if sufficient funds were available and spent, false otherwise</returns>
        public async Task<bool> SpendCurrency(float amount, CurrencyType type)
        {
            if (!isInitialized || amount <= 0)
                return false;

            var profile = profileManager.ActiveProfile;
            if (profile == null)
            {
                Debug.LogError("[CurrencyManager] No active profile!");
                return false;
            }

            // Check if player has enough currency
            float currentAmount = type == CurrencyType.NeuralCredits ? cachedNeuralCredits : cachedCyberCoins;
            if (currentAmount < amount)
            {
                Debug.Log($"[CurrencyManager] Insufficient {type} funds. Required: {amount}, Available: {currentAmount}");
                OnInsufficientFunds?.Invoke(type);
                return false;
            }

            // Cache old value for event
            float oldAmount = currentAmount;

            // Spend currency
            bool success = profile.SpendCurrency(amount, type);
            if (!success)
            {
                Debug.LogError($"[CurrencyManager] Failed to spend {amount} {type} currency");
                OnInsufficientFunds?.Invoke(type);
                return false;
            }

            // Update profile
            var result = await profileManager.UpdateProfileAsync(profile);
            if (!result.Success)
            {
                Debug.LogError($"[CurrencyManager] Failed to update profile: {result.Message}");
                return false;
            }

            // Update cache
            if (type == CurrencyType.NeuralCredits)
                cachedNeuralCredits -= amount;
            else
                cachedCyberCoins -= amount;

            // Fire events
            float newAmount = type == CurrencyType.NeuralCredits ? cachedNeuralCredits : cachedCyberCoins;
            OnCurrencySpent?.Invoke(type, amount);
            OnCurrencyChanged?.Invoke(type, oldAmount, newAmount);

            GameEvents.OnCurrencyChanged?.Invoke((int)newAmount);

            Debug.Log($"[CurrencyManager] Spent {amount} {type}. New balance: {newAmount}");
            return true;
        }

        /// <summary>
        /// Check if the player has enough currency without spending it
        /// </summary>
        /// <param name="amount">Amount to check</param>
        /// <param name="type">Type of currency to check</param>
        /// <returns>True if player has sufficient funds, false otherwise</returns>
        public bool HasSufficientFunds(float amount, CurrencyType type)
        {
            if (!isInitialized || amount <= 0)
                return false;

            if (type == CurrencyType.NeuralCredits)
                return cachedNeuralCredits >= amount;
            else
                return cachedCyberCoins >= amount;
        }

        #endregion

        /// <summary>
        /// Cleanup when the manager is destroyed
        /// </summary>
        protected override void OnManagerDestroyed()
        {
            if (profileManager != null)
            {
                profileManager.UnsubscribeFromProfileSwitched(OnProfileSwitched);
                profileManager.UnsubscribeFromNewProfileCreated(OnNewProfileCreated);
            }

            base.OnManagerDestroyed();
        }
    }
}