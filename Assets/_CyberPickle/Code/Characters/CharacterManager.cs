using System.Collections.Generic;
using System.Linq;
using CyberPickle.Characters.Data;
using CyberPickle.Core.Management;
using UnityEngine;
using CyberPickle.Core.Interfaces;

namespace CyberPickle.Characters
{
    public class CharacterManager : Manager<CharacterManager>, IInitializable
    {
        // This can be populated either in inspector or via code
        [SerializeField] private CharacterData[] availableCharacters;

        private Dictionary<string, CharacterData> characterDataLookup = new Dictionary<string, CharacterData>();
        private bool isInitialized = false;

        public void Initialize()
        {
            if (isInitialized) return;

            // Build lookup table
            RebuildCharacterLookup();
            isInitialized = true;

            Debug.Log($"[CharacterManager] Initialized with {characterDataLookup.Count} characters");
        }

        // This method allows CharacterSelectionManager to populate the manager
        public void SetAvailableCharacters(CharacterData[] characters)
        {
            if (characters == null || characters.Length == 0)
            {
                Debug.LogWarning("[CharacterManager] Attempted to set null or empty character array");
                return;
            }

            availableCharacters = characters;
            RebuildCharacterLookup();

            Debug.Log($"[CharacterManager] Updated character list with {characterDataLookup.Count} characters");
        }

        private void RebuildCharacterLookup()
        {
            characterDataLookup.Clear();

            if (availableCharacters != null)
            {
                foreach (var character in availableCharacters)
                {
                    if (character != null)
                    {
                        characterDataLookup[character.characterId] = character;
                    }
                }
            }
        }

        public CharacterData GetCharacterDataById(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return null;

            if (characterDataLookup.TryGetValue(characterId, out var characterData))
                return characterData;

            Debug.LogError($"[CharacterManager] Character with ID '{characterId}' not found!");
            return null;
        }
    }
}