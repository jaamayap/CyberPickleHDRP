// File: Shop/Currency/CurrencyData.cs
//
// Purpose: Defines the currency data structure for the game's economy system.
// Manages Neural Credits and CyberCoins, the two main currencies in Cyber Pickle.
//
// Created: 2024-02-24
// Updated: 2024-02-24

using Newtonsoft.Json;
using System;
using UnityEngine;

namespace CyberPickle.Shop.Currency
{
    /// <summary>
    /// Manages the player's currency data for both Neural Credits and CyberCoins
    /// </summary>
    [Serializable]
    public class CurrencyData
    {
        [JsonProperty("neuralCredits")]
        private float neuralCredits;

        [JsonProperty("cyberCoins")]
        private float cyberCoins;

        [JsonIgnore] public float NeuralCredits => neuralCredits;
        [JsonIgnore] public float CyberCoins => cyberCoins;

        /// <summary>
        /// Creates a new CurrencyData instance with zero balance
        /// </summary>
        public CurrencyData()
        {
            neuralCredits = 0f;
            cyberCoins = 0f;
        }

        /// <summary>
        /// Creates a new CurrencyData instance with specified initial values
        /// </summary>
        /// <param name="initialNeuralCredits">Initial Neural Credits amount</param>
        /// <param name="initialCyberCoins">Initial CyberCoins amount</param>
        public CurrencyData(float initialNeuralCredits, float initialCyberCoins)
        {
            neuralCredits = Mathf.Max(0, initialNeuralCredits);
            cyberCoins = Mathf.Max(0, initialCyberCoins);
        }

        /// <summary>
        /// Adds Neural Credits to the player's balance
        /// </summary>
        /// <param name="amount">Amount to add (must be positive)</param>
        public void AddNeuralCredits(float amount)
        {
            if (amount > 0)
            {
                neuralCredits += amount;
                Debug.Log($"[CurrencyData] Added {amount} Neural Credits. New balance: {neuralCredits}");
            }
        }

        /// <summary>
        /// Adds CyberCoins to the player's balance
        /// </summary>
        /// <param name="amount">Amount to add (must be positive)</param>
        public void AddCyberCoins(float amount)
        {
            if (amount > 0)
            {
                cyberCoins += amount;
                Debug.Log($"[CurrencyData] Added {amount} CyberCoins. New balance: {cyberCoins}");
            }
        }

        /// <summary>
        /// Attempts to spend Neural Credits from the player's balance
        /// </summary>
        /// <param name="amount">Amount to spend</param>
        /// <returns>True if sufficient funds were available and spent, false otherwise</returns>
        public bool SpendNeuralCredits(float amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning("[CurrencyData] Attempted to spend non-positive amount of Neural Credits");
                return false;
            }

            if (neuralCredits >= amount)
            {
                neuralCredits -= amount;
                Debug.Log($"[CurrencyData] Spent {amount} Neural Credits. Remaining balance: {neuralCredits}");
                return true;
            }

            Debug.Log($"[CurrencyData] Insufficient Neural Credits. Required: {amount}, Available: {neuralCredits}");
            return false;
        }

        /// <summary>
        /// Attempts to spend CyberCoins from the player's balance
        /// </summary>
        /// <param name="amount">Amount to spend</param>
        /// <returns>True if sufficient funds were available and spent, false otherwise</returns>
        public bool SpendCyberCoins(float amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning("[CurrencyData] Attempted to spend non-positive amount of CyberCoins");
                return false;
            }

            if (cyberCoins >= amount)
            {
                cyberCoins -= amount;
                Debug.Log($"[CurrencyData] Spent {amount} CyberCoins. Remaining balance: {cyberCoins}");
                return true;
            }

            Debug.Log($"[CurrencyData] Insufficient CyberCoins. Required: {amount}, Available: {cyberCoins}");
            return false;
        }

        /// <summary>
        /// Sets exact Neural Credits amount (for debugging or admin purposes)
        /// </summary>
        /// <param name="amount">Amount to set</param>
        internal void SetNeuralCredits(float amount)
        {
            neuralCredits = Mathf.Max(0, amount);
            Debug.Log($"[CurrencyData] Neural Credits set to {neuralCredits}");
        }

        /// <summary>
        /// Sets exact CyberCoins amount (for debugging or admin purposes)
        /// </summary>
        /// <param name="amount">Amount to set</param>
        internal void SetCyberCoins(float amount)
        {
            cyberCoins = Mathf.Max(0, amount);
            Debug.Log($"[CurrencyData] CyberCoins set to {cyberCoins}");
        }
    }
}
