// File: Assets/Code/Characters/Data/CharacterData.cs
//
// Purpose: Defines the base data structure for playable characters in Cyber Pickle.
// This ScriptableObject stores character attributes, stats, unlock requirements,
// and visual references. Stats are aligned with the core gameplay design and 
// character progression systems.
//
// Created: 2024-02-11
// Updated: 2024-02-11

using UnityEngine;
using System;

namespace CyberPickle.Characters.Data
{
    /// <summary>
    /// ScriptableObject that defines the base data for a playable character.
    /// Contains all permanent character data including base stats, unlock requirements,
    /// and visual references. Runtime character state and progression are stored separately
    /// in ProfileData.
    /// </summary>
    [CreateAssetMenu(fileName = "Character", menuName = "CyberPickle/Characters/CharacterData")]
    public class CharacterData : ScriptableObject
    {
        [Header("Basic Info")]
        [Tooltip("Unique identifier for the character")]
        public string characterId;

        [Tooltip("Display name shown in the UI")]
        public string displayName;

        [Tooltip("Short description for character selection screen")]
        [TextArea(3, 5)]
        public string description;

        [Tooltip("Extended character backstory and lore")]
        [TextArea(5, 10)]
        public string lore;

        [Header("Visual References")]
        [Tooltip("The character's prefab containing model and required components")]
        public GameObject characterPrefab;

        [Tooltip("2D icon for UI elements")]
        public Sprite characterIcon;

        [Tooltip("Material applied when character is locked")]
        public Material lockedMaterial;

        [Header("Base Stats")]
        [Tooltip("Maximum health points")]
        public float maxHealth = 100f;

        [Tooltip("Rate of health recovery over time")]
        public float healthRegeneration = 1f;

        [Tooltip("Reduces incoming damage")]
        public float defense = 10f;

        [Tooltip("Enhances weapon damage")]
        public float power = 10f;

        [Tooltip("Affects movement speed")]
        public float speed = 5f;

        [Tooltip("Increases item attraction radius")]
        public float magneticField = 1f;

        [Tooltip("Influences rate of fire")]
        public float dexterity = 10f;

        [Tooltip("Affects drop rates and item rarity")]
        public float luck = 1f;

        [Tooltip("Increases explosion radius and effect areas")]
        public float areaOfEffect = 1f;

        [Header("Unlock Requirements")]
        [Tooltip("If true, character is available from the start")]
        public bool unlockedByDefault;

        [Tooltip("Minimum player level required to unlock")]
        public int requiredPlayerLevel;

        [Tooltip("Achievement IDs required to unlock this character")]
        public string[] requiredAchievements;

        [Header("Animation Parameters")]
        [Tooltip("Trigger parameter for idle animation")]
        public string idleAnimationTrigger = "Idle";

        [Tooltip("Trigger parameter for hover/preview animation")]
        public string hoverAnimationTrigger = "Dance";

        [Tooltip("Trigger parameter for selection animation")]
        public string selectAnimationTrigger = "Selected";

        [Tooltip("Trigger parameter for locked state animation")]
        public string lockedAnimationTrigger = "Locked";

        /// <summary>
        /// Validates the CharacterData when it's created or modified in the editor.
        /// Automatically generates a characterId if none is provided.
        /// </summary>
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(characterId))
            {
                characterId = displayName?.ToLower().Replace(" ", "_") ?? "undefined";
                Debug.Log($"[CharacterData] Auto-generated characterId: {characterId}");
            }

            ValidateStats();
        }

        /// <summary>
        /// Ensures all stats are within valid ranges
        /// </summary>
        private void ValidateStats()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            healthRegeneration = Mathf.Max(0f, healthRegeneration);
            defense = Mathf.Max(0f, defense);
            power = Mathf.Max(0f, power);
            speed = Mathf.Max(0.1f, speed);
            magneticField = Mathf.Max(0.1f, magneticField);
            dexterity = Mathf.Max(0f, dexterity);
            luck = Mathf.Max(0f, luck);
            areaOfEffect = Mathf.Max(0.1f, areaOfEffect);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Validates required references are assigned in the editor
        /// </summary>
        public bool ValidateReferences()
        {
            if (characterPrefab == null)
            {
                Debug.LogError($"[CharacterData] Character prefab is missing for {displayName}");
                return false;
            }

            if (characterIcon == null)
            {
                Debug.LogError($"[CharacterData] Character icon is missing for {displayName}");
                return false;
            }

            if (lockedMaterial == null)
            {
                Debug.LogError($"[CharacterData] Locked material is missing for {displayName}");
                return false;
            }

            return true;
        }
#endif
    }
}
