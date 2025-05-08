// File: Assets/Code/Core/Services/Authentication/Data/CharacterProgressionData_Extension.cs
//
// Purpose: Extension methods for the CharacterProgressionData class to support equipment management.
// Adds methods for unequipping items and handling equipment slot management.
//
// Created: 2025-02-25
// Updated: 2025-02-25

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CyberPickle.Core.Services.Authentication.Data
{
    /// <summary>
    /// Extension methods for CharacterProgressionData
    /// </summary>
    public static class CharacterProgressionDataExtensions
    {
        /// <summary>
        /// Unequips an item from a character
        /// </summary>
        /// <param name="characterData">The character progression data</param>
        /// <param name="equipmentId">ID of the equipment to unequip</param>
        /// <returns>True if unequipped successfully, false otherwise</returns>
        public static bool UnequipItem(this CharacterProgressionData characterData, string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId))
            {
                Debug.LogError("[CharacterProgressionData] Cannot unequip null or empty equipment ID");
                return false;
            }

            // Check if it's a hand weapon
            if (characterData.EquippedHandWeapons.Contains(equipmentId))
            {
                // Get the private field through reflection
                var handWeaponsField = typeof(CharacterProgressionData).GetField("equippedHandWeapons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (handWeaponsField != null)
                {
                    var handWeapons = handWeaponsField.GetValue(characterData) as List<string>;
                    if (handWeapons != null)
                    {
                        handWeapons.Remove(equipmentId);
                        Debug.Log($"[CharacterProgressionData] Unequipped hand weapon: {equipmentId}");
                        return true;
                    }
                }
                Debug.LogError($"[CharacterProgressionData] Failed to access hand weapons field for unequipping {equipmentId}");
                return false;
            }

            // Check if it's a body weapon
            if (characterData.EquippedBodyWeapon == equipmentId)
            {
                var bodyWeaponField = typeof(CharacterProgressionData).GetField("equippedBodyWeapon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (bodyWeaponField != null)
                {
                    bodyWeaponField.SetValue(characterData, string.Empty);
                    Debug.Log($"[CharacterProgressionData] Unequipped body weapon: {equipmentId}");
                    return true;
                }
                Debug.LogError($"[CharacterProgressionData] Failed to access body weapon field for unequipping {equipmentId}");
                return false;
            }

            // Check if it's a power-up
            if (characterData.EquippedPowerupIds.Contains(equipmentId))
            {
                var powerUpsField = typeof(CharacterProgressionData).GetField("equippedPowerupIds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (powerUpsField != null)
                {
                    var powerUps = powerUpsField.GetValue(characterData) as List<string>;
                    if (powerUps != null)
                    {
                        powerUps.Remove(equipmentId);
                        Debug.Log($"[CharacterProgressionData] Unequipped power-up: {equipmentId}");
                        return true;
                    }
                }
                Debug.LogError($"[CharacterProgressionData] Failed to access power-ups field for unequipping {equipmentId}");
                return false;
            }

            // Check if it's armor
            if (characterData.EquippedArmor == equipmentId)
            {
                var armorField = typeof(CharacterProgressionData).GetField("equippedArmor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (armorField != null)
                {
                    armorField.SetValue(characterData, string.Empty);
                    Debug.Log($"[CharacterProgressionData] Unequipped armor: {equipmentId}");
                    return true;
                }
                Debug.LogError($"[CharacterProgressionData] Failed to access armor field for unequipping {equipmentId}");
                return false;
            }

            // Check if it's an amulet
            if (characterData.EquippedAmulet == equipmentId)
            {
                var amuletField = typeof(CharacterProgressionData).GetField("equippedAmulet", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (amuletField != null)
                {
                    amuletField.SetValue(characterData, string.Empty);
                    Debug.Log($"[CharacterProgressionData] Unequipped amulet: {equipmentId}");
                    return true;
                }
                Debug.LogError($"[CharacterProgressionData] Failed to access amulet field for unequipping {equipmentId}");
                return false;
            }

            // Item not found equipped on the character
            Debug.LogWarning($"[CharacterProgressionData] Equipment {equipmentId} is not equipped");
            return false;
        }

        /// <summary>
        /// Checks if a specific equipment is equipped
        /// </summary>
        /// <param name="characterData">The character progression data</param>
        /// <param name="equipmentId">ID of the equipment to check</param>
        /// <returns>True if equipped, false otherwise</returns>
        public static bool IsEquipped(this CharacterProgressionData characterData, string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId))
                return false;

            // Check hand weapons
            if (characterData.EquippedHandWeapons.Contains(equipmentId))
                return true;

            // Check body weapon
            if (characterData.EquippedBodyWeapon == equipmentId)
                return true;

            // Check power-ups
            if (characterData.EquippedPowerupIds.Contains(equipmentId))
                return true;

            // Check armor
            if (characterData.EquippedArmor == equipmentId)
                return true;

            // Check amulet
            if (characterData.EquippedAmulet == equipmentId)
                return true;

            return false;
        }

        /// <summary>
        /// Get the slot type where an item is equipped, or null if not equipped
        /// </summary>
        /// <param name="characterData">The character progression data</param>
        /// <param name="equipmentId">ID of the equipment to check</param>
        /// <returns>The slot type, or null if not equipped</returns>
        public static EquipmentSlotType? GetEquippedSlotType(this CharacterProgressionData characterData, string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId))
                return null;

            // Check hand weapons
            if (characterData.EquippedHandWeapons.Contains(equipmentId))
                return EquipmentSlotType.HandWeapon;

            // Check body weapon
            if (characterData.EquippedBodyWeapon == equipmentId)
                return EquipmentSlotType.BodyWeapon;

            // Check power-ups
            if (characterData.EquippedPowerupIds.Contains(equipmentId))
                return EquipmentSlotType.PowerUp;

            // Check armor
            if (characterData.EquippedArmor == equipmentId)
                return EquipmentSlotType.Armor;

            // Check amulet
            if (characterData.EquippedAmulet == equipmentId)
                return EquipmentSlotType.Amulet;

            return null;
        }
    }
}
