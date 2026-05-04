// File: Assets/_CyberPickle/Code/Characters/Data/CharacterData.cs
//
// Purpose: Defines the base data structure for playable characters in Cyber Pickle.
// This ScriptableObject stores character attributes, base stats, unlock
// requirements, and visual references. Stats are stored in a BaseStats struct
// shared with CharacterProgressionData and consumed by PlayerStats at run start.
//
// Created: 2024-02-11
// Updated: 2026-05-03 — migrated from loose stat fields to BaseStats struct
//                       (canonical 14-stat list from PlayerStatType).

using UnityEngine;
using System;
using CyberPickle.Gameplay.Stats;

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
        [Tooltip("Per-character base values for the canonical 14 player stats. " +
                 "Read by PlayerStats at run start; applied as the baseline " +
                 "before skill / equipment / implant / run-upgrade modifiers stack on top.")]
        public BaseStats baseStats = BaseStats.Defaults;

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
        /// Automatically generates a characterId if none is provided. Stat-range
        /// clamping is handled by [Min] / [Range] attributes on BaseStats fields.
        /// </summary>
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(characterId))
            {
                characterId = displayName?.ToLower().Replace(" ", "_") ?? "undefined";
                Debug.Log($"[CharacterData] Auto-generated characterId: {characterId}");
            }
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
