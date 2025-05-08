// File: Assets/Code/Shop/Equipment/Data/WeaponData.cs
//
// Purpose: Defines the data structure for weapons in Cyber Pickle.
// Contains weapon-specific properties like damage, fire rate, and special effects.
// Supports hand weapons and body weapons with their unique characteristics.
//
// Created: 2025-02-25
// Updated: 2025-02-25

using UnityEngine;
using System;
using System.Collections.Generic;
using CyberPickle.Core.Services.Authentication.Data;

namespace CyberPickle.Shop.Equipment.Data
{
    /// <summary>
    /// Defines possible weapon attack types
    /// </summary>
    public enum WeaponAttackType
    {
        Projectile,
        Beam,
        Area,
        Melee
    }

    /// <summary>
    /// ScriptableObject that defines data for weapon equipment
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "CyberPickle/Equipment/WeaponData")]
    public class WeaponData : EquipmentData
    {
        [Header("Weapon Properties")]
        [Tooltip("The type of weapon - hand or body")]
        public EquipmentSlotType weaponType = EquipmentSlotType.HandWeapon;

        [Tooltip("How this weapon attacks")]
        public WeaponAttackType attackType = WeaponAttackType.Projectile;

        [Tooltip("Base damage per attack/projectile")]
        public float baseDamage = 10f;

        [Tooltip("Base fire rate in shots per second")]
        public float baseFireRate = 2f;

        [Tooltip("Base projectile speed (if applicable)")]
        public float baseProjectileSpeed = 10f;

        [Tooltip("Base area of effect radius (if applicable)")]
        public float baseAreaOfEffect = 1f;

        [Tooltip("Base pierce count (0 = no pierce)")]
        public int basePierceCount = 0;

        [Tooltip("Does this weapon have a critical hit chance?")]
        public bool canCriticalHit = false;

        [Tooltip("Base critical hit chance (if applicable)")]
        [Range(0f, 1f)]
        public float baseCriticalChance = 0.05f;

        [Tooltip("Critical hit damage multiplier")]
        public float criticalDamageMultiplier = 2f;

        [Header("Audio & VFX")]
        [Tooltip("Prefab for projectile or attack VFX")]
        public GameObject projectilePrefab;

        [Tooltip("Sound effect for firing")]
        public AudioClip fireSound;

        [Tooltip("Sound effect for hitting")]
        public AudioClip hitSound;

        [Tooltip("VFX prefab for hit effect")]
        public GameObject hitEffectPrefab;

        [Header("Upgrade Scaling")]
        [Tooltip("Damage increase per level (multiplier)")]
        [Range(1f, 2f)]
        public float damageUpgradeMultiplier = 1.2f;

        [Tooltip("Fire rate increase per level (multiplier)")]
        [Range(1f, 1.5f)]
        public float fireRateUpgradeMultiplier = 1.1f;

        [Tooltip("Other stats upgrade multiplier")]
        [Range(1f, 1.5f)]
        public float otherStatsUpgradeMultiplier = 1.15f;

        [Header("Final Form")]
        [Tooltip("Final weapon form when fully upgraded with associated power-up")]
        public WeaponData finalForm;

        [Tooltip("Required power-up ID to unlock final form")]
        public string requiredPowerUpId;

        /// <summary>
        /// Validates the weapon data when it's created or modified in the editor.
        /// </summary>
        protected override void OnValidate()
        {
            // Set the slot type based on weapon type
            slotType = weaponType;

            base.OnValidate();

            // Additional weapon-specific validation
            ValidateWeaponFields();
        }

        /// <summary>
        /// Validates weapon-specific fields
        /// </summary>
        private void ValidateWeaponFields()
        {
            baseDamage = Mathf.Max(0.1f, baseDamage);
            baseFireRate = Mathf.Max(0.1f, baseFireRate);
            baseProjectileSpeed = Mathf.Max(0.1f, baseProjectileSpeed);
            baseAreaOfEffect = Mathf.Max(0.1f, baseAreaOfEffect);
            basePierceCount = Mathf.Max(0, basePierceCount);

            if (canCriticalHit)
            {
                baseCriticalChance = Mathf.Clamp(baseCriticalChance, 0f, 1f);
                criticalDamageMultiplier = Mathf.Max(1f, criticalDamageMultiplier);
            }
        }

        /// <summary>
        /// Gets the weapon's damage for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level of the weapon</param>
        /// <returns>The damage value at that level</returns>
        public float GetDamageForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            return baseDamage * Mathf.Pow(damageUpgradeMultiplier, level - 1);
        }

        /// <summary>
        /// Gets the weapon's fire rate for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level of the weapon</param>
        /// <returns>The fire rate value at that level</returns>
        public float GetFireRateForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            return baseFireRate * Mathf.Pow(fireRateUpgradeMultiplier, level - 1);
        }

        /// <summary>
        /// Gets the weapon's projectile speed for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level of the weapon</param>
        /// <returns>The projectile speed value at that level</returns>
        public float GetProjectileSpeedForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            return baseProjectileSpeed * Mathf.Pow(otherStatsUpgradeMultiplier, level - 1);
        }

        /// <summary>
        /// Gets the weapon's area of effect for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level of the weapon</param>
        /// <returns>The area of effect value at that level</returns>
        public float GetAreaOfEffectForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            return baseAreaOfEffect * Mathf.Pow(otherStatsUpgradeMultiplier, level - 1);
        }

        /// <summary>
        /// Gets the weapon's pierce count for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level of the weapon</param>
        /// <returns>The pierce count at that level</returns>
        public int GetPierceCountForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            // Pierce increases every 2 levels
            return basePierceCount + ((level - 1) / 2);
        }

        /// <summary>
        /// Gets the weapon's critical hit chance for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level of the weapon</param>
        /// <returns>The critical hit chance at that level</returns>
        public float GetCriticalChanceForLevel(int level)
        {
            if (!canCriticalHit) return 0f;

            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            // Critical chance increases by 1% per level up to a maximum of base + 0.2
            return Mathf.Min(baseCriticalChance + (0.01f * (level - 1)), baseCriticalChance + 0.2f);
        }

        /// <summary>
        /// Gets an array of stat descriptors for the specified upgrade level
        /// </summary>
        public override StatDescriptor[] GetStatsForLevel(int upgradeLevel)
        {
            upgradeLevel = Mathf.Clamp(upgradeLevel, 1, maxUpgradeLevel);

            List<StatDescriptor> stats = new List<StatDescriptor>();

            // Add base stats
            stats.Add(new StatDescriptor("Damage", GetDamageForLevel(upgradeLevel)));
            stats.Add(new StatDescriptor("Fire Rate", GetFireRateForLevel(upgradeLevel)));

            // Add attack type specific stats
            switch (attackType)
            {
                case WeaponAttackType.Projectile:
                    stats.Add(new StatDescriptor("Projectile Speed", GetProjectileSpeedForLevel(upgradeLevel)));

                    if (basePierceCount > 0 || upgradeLevel > 2)
                    {
                        stats.Add(new StatDescriptor("Pierce Count", GetPierceCountForLevel(upgradeLevel)));
                    }
                    break;

                case WeaponAttackType.Area:
                    stats.Add(new StatDescriptor("Area of Effect", GetAreaOfEffectForLevel(upgradeLevel)));
                    break;

                case WeaponAttackType.Beam:
                    stats.Add(new StatDescriptor("Range", GetAreaOfEffectForLevel(upgradeLevel)));
                    break;
            }

            // Add critical hit stats if applicable
            if (canCriticalHit)
            {
                stats.Add(new StatDescriptor("Critical Chance", GetCriticalChanceForLevel(upgradeLevel) * 100f, true));
                stats.Add(new StatDescriptor("Critical Damage", criticalDamageMultiplier * 100f, true));
            }

            return stats.ToArray();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Validates required references are assigned in the editor
        /// </summary>
        public override bool ValidateReferences()
        {
            bool valid = base.ValidateReferences();

            if (projectilePrefab == null && attackType == WeaponAttackType.Projectile)
            {
                Debug.LogError($"[WeaponData] Projectile prefab is missing for {displayName}");
                valid = false;
            }

            if (fireSound == null)
            {
                Debug.LogWarning($"[WeaponData] Fire sound is missing for {displayName}");
            }

            return valid;
        }
#endif
    }
}
