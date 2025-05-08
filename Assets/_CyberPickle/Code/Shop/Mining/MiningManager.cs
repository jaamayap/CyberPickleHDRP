// File: Assets/Code/Shop/Mining/MiningManager.cs
//
// Purpose: Manages the Neural Mining System in Cyber Pickle.
// Handles node management, passive income generation, collection, and upgrades.
// Works with the profile system to persist mining data.
//
// Created: 2025-02-25
// Updated: 2025-02-25

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Shop.Currency;

namespace CyberPickle.Shop.Mining
{
    /// <summary>
    /// Manager for the Neural Mining System
    /// </summary>
    public class MiningManager : Manager<MiningManager>, IInitializable
    {
        [Header("Mining Settings")]
        [SerializeField] private float basePassiveRate = 10f;        // CyberCoins per hour per node
        [SerializeField] private float activePlayRate = 30f;         // CyberCoins per hour per node during active play
        [SerializeField] private float levelUpgradeMultiplier = 0.2f; // 20% increase per level
        [SerializeField] private int maxNodeLevel = 10;
        [SerializeField] private float maxOfflineHours = 24f;        // Maximum hours of offline collection

        [Header("Node Costs")]
        [SerializeField] private int firstNodeCost = 0;              // First node is free
        [SerializeField] private int baseNodeCost = 500;             // Base cost for new nodes
        [SerializeField] private float nodeScalingFactor = 1.5f;     // Cost multiplier per node
        [SerializeField] private int baseUpgradeCost = 250;          // Base cost for upgrading nodes
        [SerializeField] private float upgradeLevelScalingFactor = 2f; // Cost multiplier per level

        // Events
        public event Action<float> OnCyberCoinsCollected;
        public event Action<string, int> OnNodeUpgraded;
        public event Action<string> OnNodePurchased;
        public event Action<float> OnPerformanceMultiplierApplied;
        public event Action<Dictionary<string, MiningNodeData>> OnMiningStatsUpdated;

        // Dependencies
        private ProfileManager profileManager;
        private CurrencyManager currencyManager;

        // Runtime tracking
        private DateTime lastCollectionTime;
        private float currentPerformanceMultiplier = 1f;
        private bool isPlaying = false;

        // Initialization flag
        private bool isInitialized = false;

        /// <summary>
        /// Initialize the Mining Manager
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            Debug.Log("[MiningManager] Initializing...");

            // Get dependencies
            profileManager = ProfileManager.Instance;
            currencyManager = CurrencyManager.Instance;

            if (profileManager == null)
            {
                Debug.LogError("[MiningManager] ProfileManager not found!");
                return;
            }

            if (currencyManager == null)
            {
                Debug.LogError("[MiningManager] CurrencyManager not found!");
                return;
            }

            // Subscribe to profile events
            profileManager.SubscribeToProfileSwitched(OnProfileSwitched);
            profileManager.SubscribeToNewProfileCreated(OnNewProfileCreated);

            // Set last collection time to now
            lastCollectionTime = DateTime.UtcNow;

            isInitialized = true;
            Debug.Log("[MiningManager] Initialized successfully");
        }

        private void OnEnable()
        {
            if (isInitialized)
            {
                // Set up automatic collection check
                InvokeRepeating(nameof(UpdateMiningStats), 1f, 60f); // Check every minute
            }
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(UpdateMiningStats));
        }

        /// <summary>
        /// Event handler for when a profile is switched
        /// </summary>
        private void OnProfileSwitched(string profileId)
        {
            Debug.Log($"[MiningManager] Profile switched to {profileId}");
            // Reset last collection time
            lastCollectionTime = DateTime.UtcNow;
            // Reset performance multiplier
            currentPerformanceMultiplier = 1f;
            // Update mining stats
            UpdateMiningStats();
        }

        /// <summary>
        /// Event handler for when a new profile is created
        /// </summary>
        private void OnNewProfileCreated(string profileId)
        {
            Debug.Log($"[MiningManager] New profile created: {profileId}");
            // Add starter node for new profile
            AddStarterNode(profileId);
        }

        /// <summary>
        /// Add a starter mining node for a new profile
        /// </summary>
        private async void AddStarterNode(string profileId)
        {
            var profile = profileManager.GetProfile(profileId);
            if (profile == null) return;

            // Add first node for free
            string nodeId = $"node_{Guid.NewGuid().ToString().Substring(0, 8)}";
            MiningNodeData starterNode = new MiningNodeData(nodeId)
            {
                // Default properties are set in the constructor
            };

            // Add to profile
            if (profile.MiningNodes.Count == 0)
            {
                var miningNodes = profile.MiningNodes as Dictionary<string, MiningNodeData>;
                if (miningNodes != null)
                {
                    miningNodes[nodeId] = starterNode;
                    await profileManager.UpdateProfileAsync(profile);
                    Debug.Log($"[MiningManager] Added starter node {nodeId} to profile {profileId}");
                    OnNodePurchased?.Invoke(nodeId);
                }
            }
        }

        #region Mining Node Management

        /// <summary>
        /// Purchase a new mining node
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> PurchaseNewNode()
        {
            if (!isInitialized)
                return false;

            var profile = profileManager.ActiveProfile;
            if (profile == null)
                return false;

            // Calculate cost based on current node count
            int nodeCount = profile.MiningNodes.Count;
            int cost = CalculateNodePurchaseCost(nodeCount);

            // Check if player has enough Neural Credits
            if (!currencyManager.HasSufficientFunds(cost, CurrencyType.NeuralCredits))
            {
                Debug.Log($"[MiningManager] Insufficient Neural Credits to purchase node. Required: {cost}, Available: {currencyManager.NeuralCredits}");
                return false;
            }

            // Spend Neural Credits
            bool spentSuccess = await currencyManager.SpendCurrency(cost, CurrencyType.NeuralCredits);
            if (!spentSuccess)
                return false;

            // Create new node
            string nodeId = $"node_{Guid.NewGuid().ToString().Substring(0, 8)}";
            MiningNodeData newNode = new MiningNodeData(nodeId);

            // Add to profile
            var miningNodes = profile.MiningNodes as Dictionary<string, MiningNodeData>;
            if (miningNodes != null)
            {
                miningNodes[nodeId] = newNode;
                await profileManager.UpdateProfileAsync(profile);
                Debug.Log($"[MiningManager] Purchased new node {nodeId} for {cost} Neural Credits");
                OnNodePurchased?.Invoke(nodeId);
                return true;
            }

            // Failed to add node, refund Neural Credits
            await currencyManager.AddCurrency(cost, CurrencyType.NeuralCredits);
            return false;
        }

        /// <summary>
        /// Upgrade a mining node
        /// </summary>
        /// <param name="nodeId">ID of the node to upgrade</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> UpgradeNode(string nodeId)
        {
            if (!isInitialized || string.IsNullOrEmpty(nodeId))
                return false;

            var profile = profileManager.ActiveProfile;
            if (profile == null)
                return false;

            // Get the node
            var miningNodes = profile.MiningNodes as Dictionary<string, MiningNodeData>;
            if (miningNodes == null || !miningNodes.TryGetValue(nodeId, out var node))
            {
                Debug.LogError($"[MiningManager] Node {nodeId} not found");
                return false;
            }

            // Check if node is already at max level
            if (node.Level >= maxNodeLevel)
            {
                Debug.Log($"[MiningManager] Node {nodeId} is already at max level {maxNodeLevel}");
                return false;
            }

            // Calculate upgrade cost
            int cost = CalculateNodeUpgradeCost(node.Level);

            // Check if player has enough Neural Credits
            if (!currencyManager.HasSufficientFunds(cost, CurrencyType.NeuralCredits))
            {
                Debug.Log($"[MiningManager] Insufficient Neural Credits to upgrade node. Required: {cost}, Available: {currencyManager.NeuralCredits}");
                return false;
            }

            // Spend Neural Credits
            bool spentSuccess = await currencyManager.SpendCurrency(cost, CurrencyType.NeuralCredits);
            if (!spentSuccess)
                return false;

            // Upgrade node
            bool upgradeSuccess = node.Upgrade();
            if (!upgradeSuccess)
            {
                // Refund Neural Credits if upgrade fails
                await currencyManager.AddCurrency(cost, CurrencyType.NeuralCredits);
                return false;
            }

            // Update profile
            await profileManager.UpdateProfileAsync(profile);
            Debug.Log($"[MiningManager] Upgraded node {nodeId} to level {node.Level} for {cost} Neural Credits");
            OnNodeUpgraded?.Invoke(nodeId, node.Level);
            return true;
        }

        /// <summary>
        /// Calculate the cost to purchase a new node
        /// </summary>
        /// <param name="currentNodeCount">Current number of nodes</param>
        /// <returns>Cost in Neural Credits</returns>
        public int CalculateNodePurchaseCost(int currentNodeCount)
        {
            if (currentNodeCount == 0)
                return firstNodeCost;

            return Mathf.RoundToInt(baseNodeCost * Mathf.Pow(nodeScalingFactor, currentNodeCount - 1));
        }

        /// <summary>
        /// Calculate the cost to upgrade a node
        /// </summary>
        /// <param name="currentLevel">Current level of the node</param>
        /// <returns>Cost in Neural Credits</returns>
        public int CalculateNodeUpgradeCost(int currentLevel)
        {
            return Mathf.RoundToInt(baseUpgradeCost * Mathf.Pow(upgradeLevelScalingFactor, currentLevel - 1));
        }

        #endregion

        #region Mining Collection

        /// <summary>
        /// Collect accumulated CyberCoins
        /// </summary>
        /// <returns>Amount of CyberCoins collected</returns>
        public async Task<float> CollectMining()
        {
            if (!isInitialized)
                return 0f;

            var profile = profileManager.ActiveProfile;
            if (profile == null)
                return 0f;

            // Calculate how many hours have passed since last collection
            TimeSpan timeSinceLastCollection = DateTime.UtcNow - lastCollectionTime;
            float hoursElapsed = Mathf.Min((float)timeSinceLastCollection.TotalHours, maxOfflineHours);

            // Calculate total earnings
            float totalEarnings = await CalculatePendingEarnings();

            // If there's anything to collect
            if (totalEarnings > 0)
            {
                // Add CyberCoins to player's balance
                await currencyManager.AddCurrency(totalEarnings, CurrencyType.CyberCoins);

                // Reset collection time
                lastCollectionTime = DateTime.UtcNow;

                // Reset performance multiplier after collection
                currentPerformanceMultiplier = 1f;

                Debug.Log($"[MiningManager] Collected {totalEarnings} CyberCoins after {hoursElapsed:F1} hours");
                OnCyberCoinsCollected?.Invoke(totalEarnings);
            }

            return totalEarnings;
        }

        /// <summary>
        /// Calculate pending earnings from all mining nodes
        /// </summary>
        /// <returns>Total pending CyberCoins</returns>
        public async Task<float> CalculatePendingEarnings()
        {
            if (!isInitialized)
                return 0f;

            var profile = profileManager.ActiveProfile;
            if (profile == null)
                return 0f;

            float totalEarnings = 0f;

            // Get all nodes
            var miningNodes = profile.MiningNodes;
            foreach (var node in miningNodes.Values)
            {
                // Calculate earnings for this node
                float nodeEarnings = node.PendingCoins;
                totalEarnings += nodeEarnings;
            }

            // Apply performance multiplier
            totalEarnings *= currentPerformanceMultiplier;

            return totalEarnings;
        }

        /// <summary>
        /// Apply a performance multiplier to mining rate based on gameplay
        /// </summary>
        /// <param name="multiplier">Multiplier to apply</param>
        public void ApplyPerformanceMultiplier(float multiplier)
        {
            // Ensure multiplier is at least 1.0
            multiplier = Mathf.Max(1f, multiplier);

            // If the new multiplier is higher, use it
            if (multiplier > currentPerformanceMultiplier)
            {
                currentPerformanceMultiplier = multiplier;
                Debug.Log($"[MiningManager] Applied performance multiplier: {multiplier:F2}x");
                OnPerformanceMultiplierApplied?.Invoke(multiplier);
            }
        }

        /// <summary>
        /// Update mining stats and notify listeners
        /// </summary>
        private void UpdateMiningStats()
        {
            if (!isInitialized) return;

            var profile = profileManager.ActiveProfile;
            if (profile == null) return;

            // Notify listeners
            OnMiningStatsUpdated?.Invoke(profile.MiningNodes as Dictionary<string, MiningNodeData>);
        }

        /// <summary>
        /// Get the current mining rate
        /// </summary>
        /// <returns>Mining rate in CyberCoins per hour</returns>
        public float GetCurrentMiningRate()
        {
            if (!isInitialized)
                return 0f;

            var profile = profileManager.ActiveProfile;
            if (profile == null)
                return 0f;

            float totalRate = 0f;

            // Calculate rate from all nodes
            foreach (var node in profile.MiningNodes.Values)
            {
                // Rate depends on whether game is being played
                float nodeRate = isPlaying ? activePlayRate : basePassiveRate;

                // Apply level bonus
                nodeRate *= (1f + (node.Level - 1) * levelUpgradeMultiplier);

                totalRate += nodeRate;
            }

            // Apply performance multiplier
            totalRate *= currentPerformanceMultiplier;

            return totalRate;
        }

        /// <summary>
        /// Set the gameplay state for mining rate calculation
        /// </summary>
        /// <param name="playing">Whether the game is being played</param>
        public void SetPlayingState(bool playing)
        {
            isPlaying = playing;
            Debug.Log($"[MiningManager] Playing state set to: {playing}");
        }

        #endregion

        protected override void OnManagerDestroyed()
        {
            if (profileManager != null)
            {
                profileManager.UnsubscribeFromProfileSwitched(OnProfileSwitched);
                profileManager.UnsubscribeFromNewProfileCreated(OnNewProfileCreated);
            }

            CancelInvoke(nameof(UpdateMiningStats));

            base.OnManagerDestroyed();
        }
    }
}