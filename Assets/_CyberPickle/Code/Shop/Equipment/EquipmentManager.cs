// File: Assets/Code/Shop/Equipment/EquipmentManager.cs
//
// Purpose: Manages all equipment data and operations in Cyber Pickle.
// Handles equipping, unequipping, loading, and equipment state tracking.
// Works with the profile system to persist equipment data.
//
// Created: 2025-02-25
// Updated: 2025-02-25

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Shop.Equipment.Data;
using CyberPickle.Core.Events;

namespace CyberPickle.Shop.Equipment
{
    /// <summary>
    /// Manager class for all equipment operations in the game.
    /// Provides methods for loading, equipping, and managing equipment.
    /// </summary>
    public class EquipmentManager : Manager<EquipmentManager>, IInitializable
    {
        [Header("Equipment Data")]
        [SerializeField] private WeaponData[] availableWeapons;
        [SerializeField] private PowerUpData[] availablePowerUps;
        [SerializeField] private ArmorData[] availableArmors;
        [SerializeField] private AmuletData[] availableAmulets;

        [Header("Default Equipment")]
        [SerializeField] private WeaponData defaultHandWeapon;
        [SerializeField] private ArmorData defaultArmor;

        // Events
        public event Action<EquipmentData> OnEquipmentEquipped;
        public event Action<EquipmentData> OnEquipmentUnequipped;
        public event Action<EquipmentData> OnEquipmentUnlocked;
        public event Action<EquipmentData, int> OnEquipmentUpgraded;

        // Dependencies
        private ProfileManager profileManager;

        // Cached data lookups
        private Dictionary<string, EquipmentData> equipmentDataLookup = new Dictionary<string, EquipmentData>();
        private Dictionary<EquipmentSlotType, List<EquipmentData>> equipmentBySlotType = new Dictionary<EquipmentSlotType, List<EquipmentData>>();

        // Initialization flag
        private bool isInitialized = false;

        /// <summary>
        /// Initialize the Equipment Manager
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            Debug.Log("[EquipmentManager] Initializing...");

            // Get profile manager reference
            profileManager = ProfileManager.Instance;
            if (profileManager == null)
            {
                Debug.LogError("[EquipmentManager] ProfileManager not found!");
                return;
            }

            // Setup data lookups
            BuildEquipmentLookups();

            // Subscribe to profile events
            profileManager.SubscribeToProfileSwitched(OnProfileSwitched);

            isInitialized = true;
            Debug.Log("[EquipmentManager] Initialized successfully");
        }

        /// <summary>
        /// Builds the equipment lookup dictionaries for quick access
        /// </summary>
        private void BuildEquipmentLookups()
        {
            equipmentDataLookup.Clear();
            equipmentBySlotType.Clear();

            // Initialize slot type lists
            foreach (EquipmentSlotType slotType in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                equipmentBySlotType[slotType] = new List<EquipmentData>();
            }

            // Add all weapons
            foreach (var weapon in availableWeapons)
            {
                if (weapon == null) continue;
                AddToLookups(weapon);
            }

            // Add all power-ups
            foreach (var powerUp in availablePowerUps)
            {
                if (powerUp == null) continue;
                AddToLookups(powerUp);
            }

            // Add all armors
            foreach (var armor in availableArmors)
            {
                if (armor == null) continue;
                AddToLookups(armor);
            }

            // Add all amulets
            foreach (var amulet in availableAmulets)
            {
                if (amulet == null) continue;
                AddToLookups(amulet);
            }

            Debug.Log($"[EquipmentManager] Built equipment lookups with {equipmentDataLookup.Count} items");
        }

        /// <summary>
        /// Adds an equipment item to the lookup dictionaries
        /// </summary>
        private void AddToLookups(EquipmentData equipment)
        {
            if (equipment == null || string.IsNullOrEmpty(equipment.equipmentId)) return;

            // Add to ID lookup
            equipmentDataLookup[equipment.equipmentId] = equipment;

            // Add to slot type lookup
            if (equipmentBySlotType.TryGetValue(equipment.slotType, out var slotList))
            {
                slotList.Add(equipment);
            }
        }

        /// <summary>
        /// Event handler for when the profile is switched
        /// </summary>
        private void OnProfileSwitched(string profileId)
        {
            Debug.Log($"[EquipmentManager] Profile switched to {profileId}");
            // Nothing specific needed here, equipment state is stored in profile
        }

        #region Equipment Operations

        /// <summary>
        /// Equips an item for the active character
        /// </summary>
        /// <param name="equipmentId">ID of the equipment to equip</param>
        /// <returns>True if equipped successfully, false otherwise</returns>
        public bool EquipItem(string equipmentId)
        {
            if (!isInitialized || string.IsNullOrEmpty(equipmentId))
                return false;

            // Get the equipment data
            if (!equipmentDataLookup.TryGetValue(equipmentId, out var equipmentData))
            {
                Debug.LogError($"[EquipmentManager] Equipment with ID {equipmentId} not found!");
                return false;
            }

            // Get active profile
            var profile = profileManager.ActiveProfile;
            if (profile == null)
            {
                Debug.LogError("[EquipmentManager] No active profile!");
                return false;
            }

            // Check if equipment is unlocked
            if (!profile.IsEquipmentUnlocked(equipmentId) && !equipmentData.unlockedByDefault)
            {
                Debug.LogError($"[EquipmentManager] Equipment {equipmentId} is not unlocked!");
                return false;
            }

            // Get active character's progression data
            string characterId = GetActiveCharacterId();
            if (string.IsNullOrEmpty(characterId) || !profile.CharacterProgress.TryGetValue(characterId, out var characterData))
            {
                Debug.LogError("[EquipmentManager] No active character or character progress not found!");
                return false;
            }

            // Equip the item based on type
            bool success = characterData.EquipItem(equipmentId, equipmentData.slotType);
            if (success)
            {
                Debug.Log($"[EquipmentManager] Successfully equipped {equipmentData.displayName} ({equipmentId})");
                OnEquipmentEquipped?.Invoke(equipmentData);

                // Update profile data
                profileManager.UpdateProfileAsync(profile).ContinueWith(_ => {
                    Debug.Log("[EquipmentManager] Profile updated after equipment change");
                });
            }
            else
            {
                Debug.LogError($"[EquipmentManager] Failed to equip {equipmentData.displayName} ({equipmentId})");
            }

            return success;
        }

        /// <summary>
        /// Unequips an item from the active character
        /// </summary>
        /// <param name="equipmentId">ID of the equipment to unequip</param>
        /// <returns>True if unequipped successfully, false otherwise</returns>
        /// <summary>
        /// Unequips an item from the active character
        /// </summary>
        /// <param name="equipmentId">ID of the equipment to unequip</param>
        /// <returns>True if unequipped successfully, false otherwise</returns>
        public bool UnequipItem(string equipmentId)
        {
            if (!isInitialized || string.IsNullOrEmpty(equipmentId))
                return false;

            // Get equipment data for event broadcast later
            if (!equipmentDataLookup.TryGetValue(equipmentId, out var equipmentData))
            {
                Debug.LogError($"[EquipmentManager] Equipment with ID {equipmentId} not found!");
                return false;
            }

            // Get active profile and character
            var profile = profileManager.ActiveProfile;
            if (profile == null)
            {
                Debug.LogError("[EquipmentManager] No active profile!");
                return false;
            }

            // Get active character's progression data
            string characterId = GetActiveCharacterId();
            if (string.IsNullOrEmpty(characterId) || !profile.CharacterProgress.TryGetValue(characterId, out var characterData))
            {
                Debug.LogError("[EquipmentManager] No active character or character progress not found!");
                return false;
            }

            // Check if the item is equipped
            if (!characterData.IsEquipped(equipmentId))
            {
                Debug.LogWarning($"[EquipmentManager] Equipment {equipmentId} is not equipped");
                return false;
            }

            // Unequip the item using our extension method
            bool success = characterData.UnequipItem(equipmentId);

            if (success)
            {
                Debug.Log($"[EquipmentManager] Successfully unequipped {equipmentData.displayName} ({equipmentId})");
                OnEquipmentUnequipped?.Invoke(equipmentData);

                // Update profile data
                profileManager.UpdateProfileAsync(profile).ContinueWith(_ => {
                    Debug.Log("[EquipmentManager] Profile updated after equipment change");
                });
            }
            else
            {
                Debug.LogError($"[EquipmentManager] Failed to unequip {equipmentData.displayName} ({equipmentId})");
            }

            return success;
        }

        /// <summary>
        /// Unlocks a piece of equipment in the profile
        /// </summary>
        /// <param name="equipmentId">ID of the equipment to unlock</param>
        /// <returns>True if unlocked successfully, false otherwise</returns>
        public bool UnlockEquipment(string equipmentId)
        {
            if (!isInitialized || string.IsNullOrEmpty(equipmentId))
                return false;

            // Get the equipment data
            if (!equipmentDataLookup.TryGetValue(equipmentId, out var equipmentData))
            {
                Debug.LogError($"[EquipmentManager] Equipment with ID {equipmentId} not found!");
                return false;
            }

            // Get active profile
            var profile = profileManager.ActiveProfile;
            if (profile == null)
            {
                Debug.LogError("[EquipmentManager] No active profile!");
                return false;
            }

            // Check if already unlocked
            if (profile.IsEquipmentUnlocked(equipmentId))
            {
                Debug.Log($"[EquipmentManager] Equipment {equipmentId} is already unlocked");
                return true;
            }

            // Unlock the equipment
            bool success = profile.UnlockEquipment(equipmentId);
            if (success)
            {
                Debug.Log($"[EquipmentManager] Successfully unlocked {equipmentData.displayName} ({equipmentId})");
                OnEquipmentUnlocked?.Invoke(equipmentData);

                // Update profile data
                profileManager.UpdateProfileAsync(profile).ContinueWith(_ => {
                    Debug.Log("[EquipmentManager] Profile updated after equipment unlock");
                });
            }
            else
            {
                Debug.LogError($"[EquipmentManager] Failed to unlock {equipmentData.displayName} ({equipmentId})");
            }

            return success;
        }

        #endregion

        #region Equipment Queries

        /// <summary>
        /// Get the active character ID from the current profile
        /// </summary>
        private string GetActiveCharacterId()
        {
            var profile = profileManager.ActiveProfile;
            if (profile == null)
                return null;

            // The active character ID would typically come from the profile
            // This might need to be retrieved differently depending on your implementation
            return profile.ProfileId; // Using profile ID as the character ID for now
        }

        /// <summary>
        /// Gets equipment data by ID
        /// </summary>
        /// <param name="equipmentId">ID of the equipment</param>
        /// <returns>Equipment data or null if not found</returns>
        public EquipmentData GetEquipmentById(string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId) || !equipmentDataLookup.TryGetValue(equipmentId, out var equipmentData))
                return null;

            return equipmentData;
        }

        /// <summary>
        /// Gets all equipment of a specific slot type
        /// </summary>
        /// <param name="slotType">Slot type to filter by</param>
        /// <returns>List of equipment data for that slot</returns>
        public List<EquipmentData> GetEquipmentBySlotType(EquipmentSlotType slotType)
        {
            if (equipmentBySlotType.TryGetValue(slotType, out var equipmentList))
                return new List<EquipmentData>(equipmentList);

            return new List<EquipmentData>();
        }

        /// <summary>
        /// Gets all equipment that is currently unlocked for the active profile
        /// </summary>
        /// <returns>List of unlocked equipment</returns>
        public List<EquipmentData> GetUnlockedEquipment()
        {
            var profile = profileManager.ActiveProfile;
            if (profile == null)
                return new List<EquipmentData>();

            var unlockedEquipment = new List<EquipmentData>();
            foreach (var equipment in equipmentDataLookup.Values)
            {
                if (profile.IsEquipmentUnlocked(equipment.equipmentId) || equipment.unlockedByDefault)
                {
                    unlockedEquipment.Add(equipment);
                }
            }

            return unlockedEquipment;
        }

        /// <summary>
        /// Gets all equipment of a specific type that is currently unlocked
        /// </summary>
        /// <param name="slotType">Slot type to filter by</param>
        /// <returns>List of unlocked equipment of the specified type</returns>
        public List<EquipmentData> GetUnlockedEquipmentByType(EquipmentSlotType slotType)
        {
            return GetUnlockedEquipment().Where(e => e.slotType == slotType).ToList();
        }

        /// <summary>
        /// Gets all equipment currently equipped on the active character
        /// </summary>
        /// <returns>Dictionary of equipped items by slot type</returns>
        public Dictionary<EquipmentSlotType, List<EquipmentData>> GetEquippedEquipment()
        {
            var result = new Dictionary<EquipmentSlotType, List<EquipmentData>>();

            // Initialize all slot types with empty lists
            foreach (EquipmentSlotType slotType in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                result[slotType] = new List<EquipmentData>();
            }

            // Get active profile and character
            var profile = profileManager.ActiveProfile;
            if (profile == null)
                return result;

            string characterId = GetActiveCharacterId();
            if (string.IsNullOrEmpty(characterId) || !profile.CharacterProgress.TryGetValue(characterId, out var characterData))
                return result;

            // Add hand weapons
            foreach (var weaponId in characterData.EquippedHandWeapons)
            {
                if (equipmentDataLookup.TryGetValue(weaponId, out var weaponData))
                {
                    result[EquipmentSlotType.HandWeapon].Add(weaponData);
                }
            }

            // Add body weapon
            if (!string.IsNullOrEmpty(characterData.EquippedBodyWeapon) &&
                equipmentDataLookup.TryGetValue(characterData.EquippedBodyWeapon, out var bodyWeaponData))
            {
                result[EquipmentSlotType.BodyWeapon].Add(bodyWeaponData);
            }

            // Add power-ups
            foreach (var powerUpId in characterData.EquippedPowerupIds)
            {
                if (equipmentDataLookup.TryGetValue(powerUpId, out var powerUpData))
                {
                    result[EquipmentSlotType.PowerUp].Add(powerUpData);
                }
            }

            // Add armor
            if (!string.IsNullOrEmpty(characterData.EquippedArmor) &&
                equipmentDataLookup.TryGetValue(characterData.EquippedArmor, out var armorData))
            {
                result[EquipmentSlotType.Armor].Add(armorData);
            }

            // Add amulet
            if (!string.IsNullOrEmpty(characterData.EquippedAmulet) &&
                equipmentDataLookup.TryGetValue(characterData.EquippedAmulet, out var amuletData))
            {
                result[EquipmentSlotType.Amulet].Add(amuletData);
            }

            return result;
        }

        #endregion

        protected override void OnManagerDestroyed()
        {
            if (profileManager != null)
            {
                profileManager.UnsubscribeFromProfileSwitched(OnProfileSwitched);
            }

            base.OnManagerDestroyed();
        }
    }
}
