using UnityEngine;
using System.Collections.Generic;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Shop.Equipment;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.UI.EquipmentHub
{
    public class LoadoutDisplayController : MonoBehaviour
    {
        [Header("Equipment Slot References")]
        [SerializeField] private EquipmentSlotController[] handWeaponSlots; // 2 slots
        [SerializeField] private EquipmentSlotController bodyWeaponSlot;
        [SerializeField] private EquipmentSlotController[] powerUpSlots; // 3 slots
        [SerializeField] private EquipmentSlotController armorSlot;
        [SerializeField] private EquipmentSlotController amuletSlot;

        private EquipmentManager equipmentManager;
        private ProfileManager profileManager;
        private string currentCharacterId;

        public void Initialize(string characterId)
        {
            equipmentManager = EquipmentManager.Instance;
            profileManager = ProfileManager.Instance;
            currentCharacterId = characterId;

            // Check if this is a new character and assign defaults
            CheckAndAssignDefaultEquipment();

            // Load and display current equipment
            RefreshLoadoutDisplay();
        }

        private void CheckAndAssignDefaultEquipment()
        {
            var profile = profileManager.ActiveProfile;
            if (profile == null) return;

            // Get or create character progression
            if (!profile.CharacterProgress.TryGetValue(currentCharacterId, out var characterData))
            {
                characterData = new CharacterProgressionData(currentCharacterId);
                profile.UpdateCharacterProgress(currentCharacterId, characterData);
            }

            // Check if character has any equipment
            bool hasAnyEquipment = characterData.EquippedHandWeapons.Count > 0 ||
                                   !string.IsNullOrEmpty(characterData.EquippedBodyWeapon) ||
                                   !string.IsNullOrEmpty(characterData.EquippedArmor) ||
                                   !string.IsNullOrEmpty(characterData.EquippedAmulet) ||
                                   characterData.EquippedPowerupIds.Count > 0;

            if (!hasAnyEquipment)
            {
                // Assign default equipment
                AssignDefaultEquipment(characterData);

                // Save the profile
                System.Threading.Tasks.Task<ProfileOperationResult> task = profileManager.UpdateProfileAsync(profile);
            }
        }

        private void AssignDefaultEquipment(CharacterProgressionData characterData)
        {
            // Get all available equipment
            var allWeapons = equipmentManager.GetEquipmentBySlotType(EquipmentSlotType.HandWeapon);
            var allArmor = equipmentManager.GetEquipmentBySlotType(EquipmentSlotType.Armor);
            var allAmulets = equipmentManager.GetEquipmentBySlotType(EquipmentSlotType.Amulet);
            var allPowerUps = equipmentManager.GetEquipmentBySlotType(EquipmentSlotType.PowerUp);

            // Find and equip default items
            foreach (var weapon in allWeapons)
            {
                if (weapon.unlockedByDefault)
                {
                    characterData.EquipItem(weapon.equipmentId, EquipmentSlotType.HandWeapon);

                    // Also unlock it in the profile
                    var profile = profileManager.ActiveProfile;
                    profile.UnlockEquipment(weapon.equipmentId);
                    break; // Only equip one default weapon
                }
            }

            foreach (var armor in allArmor)
            {
                if (armor.unlockedByDefault)
                {
                    characterData.EquipItem(armor.equipmentId, EquipmentSlotType.Armor);
                    var profile = profileManager.ActiveProfile;
                    profile.UnlockEquipment(armor.equipmentId);
                    break;
                }
            }

            foreach (var amulet in allAmulets)
            {
                if (amulet.unlockedByDefault)
                {
                    characterData.EquipItem(amulet.equipmentId, EquipmentSlotType.Amulet);
                    var profile = profileManager.ActiveProfile;
                    profile.UnlockEquipment(amulet.equipmentId);
                    break;
                }
            }

            foreach (var powerUp in allPowerUps)
            {
                if (powerUp.unlockedByDefault)
                {
                    characterData.EquipItem(powerUp.equipmentId, EquipmentSlotType.PowerUp);
                    var profile = profileManager.ActiveProfile;
                    profile.UnlockEquipment(powerUp.equipmentId);
                    break;
                }
            }
        }

        public void RefreshLoadoutDisplay()
        {
            var profile = profileManager.ActiveProfile;
            if (profile == null || !profile.CharacterProgress.TryGetValue(currentCharacterId, out var characterData))
            {
                Debug.LogError($"[LoadoutDisplayController] No character data found for {currentCharacterId}");
                return;
            }

            // Clear all slots first
            ClearAllSlots();

            // Populate hand weapons
            for (int i = 0; i < characterData.EquippedHandWeapons.Count && i < handWeaponSlots.Length; i++)
            {
                var weaponId = characterData.EquippedHandWeapons[i];
                var weaponData = equipmentManager.GetEquipmentById(weaponId);
                if (weaponData != null && handWeaponSlots[i] != null)
                {
                    int level = profile.GetEquipmentLevel(weaponId);
                    handWeaponSlots[i].SetEquipment(weaponData);
                }
            }

            // Populate body weapon
            if (!string.IsNullOrEmpty(characterData.EquippedBodyWeapon) && bodyWeaponSlot != null)
            {
                var weaponData = equipmentManager.GetEquipmentById(characterData.EquippedBodyWeapon);
                if (weaponData != null)
                {
                    int level = profile.GetEquipmentLevel(characterData.EquippedBodyWeapon);
                    bodyWeaponSlot.SetEquipment(weaponData);
                }
            }

            // Populate armor
            if (!string.IsNullOrEmpty(characterData.EquippedArmor) && armorSlot != null)
            {
                var armorData = equipmentManager.GetEquipmentById(characterData.EquippedArmor);
                if (armorData != null)
                {
                    int level = profile.GetEquipmentLevel(characterData.EquippedArmor);
                    armorSlot.SetEquipment(armorData);
                }
            }

            // Populate amulet
            if (!string.IsNullOrEmpty(characterData.EquippedAmulet) && amuletSlot != null)
            {
                var amuletData = equipmentManager.GetEquipmentById(characterData.EquippedAmulet);
                if (amuletData != null)
                {
                    int level = profile.GetEquipmentLevel(characterData.EquippedAmulet);
                    amuletSlot.SetEquipment(amuletData);
                }
            }

            // Populate power-ups
            for (int i = 0; i < characterData.EquippedPowerupIds.Count && i < powerUpSlots.Length; i++)
            {
                var powerUpId = characterData.EquippedPowerupIds[i];
                var powerUpData = equipmentManager.GetEquipmentById(powerUpId);
                if (powerUpData != null && powerUpSlots[i] != null)
                {
                    int level = profile.GetEquipmentLevel(powerUpId);
                    powerUpSlots[i].SetEquipment(powerUpData);
                }
            }
        }

        private void ClearAllSlots()
        {
            foreach (var slot in handWeaponSlots)
            {
                if (slot != null) slot.SetEmpty();
            }

            if (bodyWeaponSlot != null) bodyWeaponSlot.SetEmpty();

            foreach (var slot in powerUpSlots)
            {
                if (slot != null) slot.SetEmpty();
            }

            if (armorSlot != null) armorSlot.SetEmpty();
            if (amuletSlot != null) amuletSlot.SetEmpty();
        }
    }
}