// File: Assets/_CyberPickle/Code/Characters/CharacterSelectionManager.cs
//
// Purpose: Manages the character selection screen functionality in Cyber Pickle.
// This manager coordinates character display, UI interactions, profile data handling,
// and state transitions. It acts as the central coordinator for the character
// selection flow and maintains the state of displayed characters.
//
// Dependencies:
// - CharacterDisplayManager: Handles visual presentation of characters
// - CharacterUIManager: Manages UI elements and interactions
// - ProfileManager: Handles profile data and persistence
// - GameManager: Controls game state transitions
// - InputManager: Processes player input
// - CameraManager: Controls camera transitions and effects
//
// Created: 2024-02-11
// Updated: 2024-02-29

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CyberPickle.Core.Management;
using CyberPickle.Core.Events;
using CyberPickle.Core.States;
using CyberPickle.Core.Camera;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Characters.Data;
using CyberPickle.Core.Input;
using CyberPickle.Core;
using DG.Tweening;

namespace CyberPickle.Characters
{
    /// <summary>
    /// Defines possible display states for characters in the selection screen
    /// </summary>
    public enum CharacterDisplayState
    {
        /// <summary>Character is not visible</summary>
        Hidden,
        /// <summary>Character is visible but not unlocked</summary>
        Locked,
        /// <summary>Character is in default display state</summary>
        Idle,
        /// <summary>Character is being hovered over</summary>
        Hover,
        /// <summary>Character has been selected</summary>
        Selected,
        /// <summary>Character is being previewed</summary>
        Previewing
    }

    /// <summary>
    /// Central manager for the character selection screen functionality.
    /// Coordinates between display, UI, and game state systems.
    /// </summary>
    public class CharacterSelectionManager : Manager<CharacterSelectionManager>
    {
        #region Serialized Fields

        [Header("Scene References")]
        [SerializeField] private Transform[] characterPositions;
        [SerializeField] private Light spotLight;
        [SerializeField] private float spotlightRotationDuration = 0.5f;

        [Header("Character References")]
        [SerializeField] private CharacterData[] availableCharacters;

        [Header("Managers")]
        [SerializeField] private CharacterDisplayManager displayManager;
        [SerializeField] private CharacterUIManager uiManager;

        #endregion

        #region Private Fields

        // Core service references
        private ProfileManager profileManager;
        private GameManager gameManager;
        private InputManager inputManager;
        private CameraManager cameraManager;

        // State tracking
        private Dictionary<string, GameObject> spawnedCharacters = new Dictionary<string, GameObject>();
        private Dictionary<string, CharacterDisplayState> characterStates = new Dictionary<string, CharacterDisplayState>();
        private string currentlySelectedCharacterId;
        private string currentlyHoveredCharacterId;
        private bool isTransitioning;
        private int currentSpotlightIndex;

        #endregion

        #region Public Methods

        /// <summary>
        /// Retrieves character data for the specified character ID
        /// </summary>
        /// <param name="characterId">The ID of the character to retrieve</param>
        /// <returns>CharacterData if found, null otherwise</returns>
        public CharacterData GetCharacterData(string characterId)
        {
            return Array.Find(availableCharacters, c => c.characterId == characterId);
        }

        /// <summary>
        /// Checks if a character is unlocked for the current profile
        /// </summary>
        /// <param name="characterId">The ID of the character to check</param>
        /// <returns>True if the character is unlocked, false otherwise</returns>
        public bool IsCharacterUnlocked(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return false;

            var characterData = Array.Find(availableCharacters, c => c.characterId == characterId);
            if (characterData == null) return false;

            // Check if unlocked by default
            if (characterData.unlockedByDefault) return true;

            // Check profile data for unlock status
            var profile = profileManager.ActiveProfile;
            if (profile?.CharacterProgress == null) return false;

            return profile.CharacterProgress.ContainsKey(characterId);
        }

        /// <summary>
        /// Handles the selection of a character and triggers the transition to level selection
        /// </summary>
        /// <param name="characterId">The ID of the character to select</param>
        /// <returns>True if selection was successful, false otherwise</returns>
        public async Task<bool> SelectCharacter(string characterId)
        {
            if (!IsCharacterUnlocked(characterId) || isTransitioning) return false;

            isTransitioning = true;

            try
            {
                currentlySelectedCharacterId = characterId;
                SetCharacterState(characterId, CharacterDisplayState.Selected);

                var profile = profileManager.ActiveProfile;
                if (profile != null)
                {
                    await profileManager.UpdateProfileAsync(profile);
                }

                GameEvents.OnGameStateChanged.Invoke(GameState.LevelSelect);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CharacterSelectionManager] Failed to select character: {e.Message}");
                return false;
            }
            finally
            {
                isTransitioning = false;
            }
        }

        #endregion

        #region Protected Methods

        protected override void OnManagerAwake()
        {
            base.OnManagerAwake();
            Debug.Log($"[CharacterSelectionManager] OnManagerAwake called. Instance ID: {GetInstanceID()}");
            InitializeManagerReferences();
            ValidateReferences();
            displayManager.Initialize();  // Initialize display manager early
        }

        protected override void OnManagerEnabled()
        {
            base.OnManagerEnabled();
            Debug.Log($"[CharacterSelectionManager] OnEnable called. Instance ID: {GetInstanceID()}");

            // Ensure manager references are valid on enable
            if (!EnsureManagerReferences())
            {
                Debug.LogError("[CharacterSelectionManager] Failed to get manager references in OnEnable!");
                return;
            }

            SubscribeToEvents();
        }

        protected override void OnManagerDisabled()
        {
            base.OnManagerDisabled();
            UnsubscribeFromEvents();
        }

        protected override void OnManagerDestroyed()
        {
            base.OnManagerDestroyed();
            CleanupCharacterSelection();
            UnsubscribeFromEvents();
        }

        #endregion

        #region Manager Reference Handling

        private void InitializeManagerReferences()
        {
            Debug.Log("[CharacterSelectionManager] Initializing manager references");
            EnsureManagerReferences();
        }

        private bool EnsureManagerReferences()
        {
            if (profileManager == null)
            {
                profileManager = ProfileManager.Instance;
                Debug.Log($"[CharacterSelectionManager] Got ProfileManager instance: {(profileManager != null ? "Success" : "Failed")}");
            }

            if (gameManager == null)
            {
                gameManager = GameManager.Instance;
            }

            if (inputManager == null)
            {
                inputManager = InputManager.Instance;
            }

            if (cameraManager == null)
            {
                cameraManager = CameraManager.Instance;
            }

            bool referencesValid = ValidateManagerReferences();
            Debug.Log($"[CharacterSelectionManager] Manager references valid: {referencesValid}");
            return referencesValid;
        }

        private bool ValidateManagerReferences()
        {
            if (profileManager == null)
            {
                Debug.LogError("[CharacterSelectionManager] ProfileManager reference is null!");
                return false;
            }

            if (gameManager == null)
            {
                Debug.LogError("[CharacterSelectionManager] GameManager reference is null!");
                return false;
            }

            if (inputManager == null)
            {
                Debug.LogError("[CharacterSelectionManager] InputManager reference is null!");
                return false;
            }

            if (cameraManager == null)
            {
                Debug.LogError("[CharacterSelectionManager] CameraManager reference is null!");
                return false;
            }

            if (displayManager == null)
            {
                Debug.LogError("[CharacterSelectionManager] DisplayManager reference is null!");
                return false;
            }

            if (uiManager == null)
            {
                Debug.LogError("[CharacterSelectionManager] UIManager reference is null!");
                return false;
            }

            return true;
        }

        #endregion






        #region Private Methods

       

        private void ValidateReferences()
        {
            if (characterPositions == null || characterPositions.Length == 0)
            {
                Debug.LogError("[CharacterSelectionManager] Character positions not assigned!");
            }

            if (availableCharacters == null || availableCharacters.Length == 0)
            {
                Debug.LogError("[CharacterSelectionManager] No available characters configured!");
            }

            if (spotLight == null)
            {
                Debug.LogError("[CharacterSelectionManager] Spotlight not assigned!");
            }
        }

        /// <summary>
        /// Subscribes to all relevant game events for character selection functionality
        /// </summary>
        private void SubscribeToEvents()
        {
            // Core game state events
            GameEvents.OnGameStateChanged.AddListener(HandleGameStateChanged);
            GameEvents.OnProfileSelected.AddListener(HandleProfileSelected);
            GameEvents.OnProfileNavigationInput.AddListener(HandleNavigationInput);

            // Character pointer interaction events
            GameEvents.OnCharacterHoverEnter.AddListener(HandleCharacterHoverEnter);
            GameEvents.OnCharacterHoverExit.AddListener(HandleCharacterHoverExit);
            GameEvents.OnCharacterSelected.AddListener(HandleCharacterSelected);
            GameEvents.OnCharacterDetailsRequested.AddListener(HandleCharacterDetails);

            Debug.Log("[CharacterSelectionManager] Subscribed to all events");
        }

        /// <summary>
        /// Unsubscribes from all events to prevent memory leaks
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            // Core game state events
            GameEvents.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
            GameEvents.OnProfileSelected.RemoveListener(HandleProfileSelected);
            GameEvents.OnProfileNavigationInput.RemoveListener(HandleNavigationInput);

            // Character pointer interaction events
            GameEvents.OnCharacterHoverEnter.RemoveListener(HandleCharacterHoverEnter);
            GameEvents.OnCharacterHoverExit.RemoveListener(HandleCharacterHoverExit);
            GameEvents.OnCharacterSelected.RemoveListener(HandleCharacterSelected);
            GameEvents.OnCharacterDetailsRequested.RemoveListener(HandleCharacterDetails);

            Debug.Log("[CharacterSelectionManager] Unsubscribed from all events");
        }

        private void HandleGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.CharacterSelect:
                    InitializeCharacterSelection();
                    break;
                case GameState.MainMenu:
                case GameState.LevelSelect:
                    CleanupCharacterSelection();
                    break;
            }
        }

        private void HandleProfileSelected(string profileId)
        {
            var profile = profileManager.GetProfile(profileId);
            if (profile != null)
            {
                LoadCharacterProgressionData(profile);
            }
        }

        private void HandleNavigationInput(float direction)
        {
            if (isTransitioning) return;

            int newIndex = currentSpotlightIndex;
            if (direction > 0 && currentSpotlightIndex < characterPositions.Length - 1)
            {
                newIndex++;
            }
            else if (direction < 0 && currentSpotlightIndex > 0)
            {
                newIndex--;
            }

            if (newIndex != currentSpotlightIndex)
            {
                RotateSpotlightToIndex(newIndex);
            }
        }

        private async void InitializeCharacterSelection()
        {
            Debug.Log("[CharacterSelectionManager] Starting character selection initialization");

            // Ensure manager references are valid before proceeding
            if (!EnsureManagerReferences())
            {
                Debug.LogError("[CharacterSelectionManager] Cannot initialize character selection - invalid manager references!");
                return;
            }

            isTransitioning = true;

            try
            {
                Debug.Log($"[CharacterSelectionManager] ProfileManager state - Instance: {(profileManager != null ? "exists" : "null")}, " +
                    $"ActiveProfile: {(profileManager?.ActiveProfile != null ? profileManager.ActiveProfile.ProfileId : "null")}");

                CleanupCharacterSelection();

                var profile = profileManager.ActiveProfile;
                if (profile == null)
                {
                    Debug.LogError("[CharacterSelectionManager] No active profile found! Character selection cannot proceed.");
                    return;
                }

                LoadCharacterProgressionData(profile);

                // Spawn all characters

                for (int i = 0; i < availableCharacters.Length; i++)
                {
                    var characterData = availableCharacters[i];
                    await SpawnCharacter(characterData, characterPositions[i]);
                }

                uiManager.Initialize(availableCharacters);
                currentSpotlightIndex = 0;
                RotateSpotlightToIndex(currentSpotlightIndex);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CharacterSelectionManager] Error during initialization: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                isTransitioning = false;
            }
        }

        private async Task SpawnCharacter(CharacterData characterData, Transform position)
        {
            try
            {
                GameObject characterInstance = await displayManager.SpawnCharacter(
                    characterData,
                    position.position,
                    position.rotation
                );

                if (characterInstance != null)
                {
                    // Add debug logging to verify setup
                    Debug.Log($"[CharacterSelectionManager] Setting up character {characterData.characterId}");

                    // Check if pointer handler exists and is properly initialized
                    var pointerHandler = characterInstance.GetComponent<CharacterPointerHandler>();
                    if (pointerHandler == null)
                    {
                        pointerHandler = characterInstance.AddComponent<CharacterPointerHandler>();
                        Debug.Log($"[CharacterSelectionManager] Added CharacterPointerHandler to {characterData.characterId}");
                    }
                    pointerHandler.Initialize(characterData.characterId);

                    // Verify layer setup
                    characterInstance.layer = LayerMask.NameToLayer("Character");
                    Debug.Log($"[CharacterSelectionManager] Set layer for {characterData.characterId} to Character");

                    // Store references
                    spawnedCharacters[characterData.characterId] = characterInstance;

                    // Determine character state
                    bool isUnlocked = IsCharacterUnlocked(characterData.characterId);
                    characterStates[characterData.characterId] = isUnlocked ? CharacterDisplayState.Idle : CharacterDisplayState.Locked;

                    // Update interactability
                    if (isUnlocked)
                    {
                        pointerHandler.SetInteractable(true);
                    }
                    else
                    {
                        // Allow hover but disable click for locked characters
                        pointerHandler.SetInteractable(false, allowHover: true);
                        Debug.Log($"[CharacterSelectionManager] Character {characterData.characterId} is locked but hoverable");
                    }

                    Debug.Log($"[CharacterSelectionManager] Character setup complete - ID: {characterData.characterId}, Unlocked: {isUnlocked}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CharacterSelectionManager] Failed to spawn character {characterData.characterId}: {e.Message}");
            }
        }


        /// <summary>
        /// Handles mouse hover enter events for characters
        /// </summary>
        /// <param name="characterId">ID of the character being hovered</param>
        private void HandleCharacterHoverEnter(string characterId)
        {
            if (isTransitioning) return;

            // Find the hovered character and its position
            for (int i = 0; i < availableCharacters.Length; i++)
            {
                if (availableCharacters[i].characterId == characterId)
                {
                    if (currentSpotlightIndex != i)
                    {
                        RotateSpotlightToIndex(i);
                    }

                    var characterTransform = characterPositions[i];

                    // Check if the character is locked or unlocked
                    if (IsCharacterUnlocked(characterId))
                    {
                        // Update panel positions and show hover panel for unlocked character
                        uiManager.UpdatePanelPositions(characterTransform);
                        uiManager.ShowCharacterPreview(availableCharacters[i]);
                    }
                    else
                    {
                        // Hide the hover panel and only show the unlock requirements panel
                        uiManager.HideAllPanels();
                        uiManager.UpdatePanelPositions(characterTransform);
                        uiManager.ShowUnlockRequirements(availableCharacters[i]);
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Handles mouse hover exit events for characters
        /// </summary>
        /// <param name="characterId">ID of the character no longer being hovered</param>
        private void HandleCharacterHoverExit(string characterId)
        {
            if (!IsCharacterUnlocked(characterId))
            {
                uiManager.HideUnlockPanel();
            }
        }

        /// <summary>
        /// Handles character selection via mouse click
        /// </summary>
        /// <param name="characterId">ID of the selected character</param>
        private async void HandleCharacterSelected(string characterId)
        {
            if (isTransitioning) return;
            await SelectCharacter(characterId);
        }

        /// <summary>
        /// Handles requests to view character details
        /// </summary>
        /// <param name="characterId">ID of the character whose details were requested</param>
        private void HandleCharacterDetails(string characterId)
        {
            if (isTransitioning || !IsCharacterUnlocked(characterId)) return;
            uiManager.ShowDetails(characterId);
        }
        private void LoadCharacterProgressionData(ProfileData profile)
        {
            if (profile == null) return;

            foreach (var characterProgress in profile.CharacterProgress)
            {
                string characterId = characterProgress.Key;
                var progressData = characterProgress.Value;
                uiManager.UpdateCharacterProgress(characterId, progressData);
            }
        }

        private async void RotateSpotlightToIndex(int index)
        {
            if (index < 0 || index >= characterPositions.Length || isTransitioning) return;

            isTransitioning = true;
            currentSpotlightIndex = index;

            float targetRotation = (index - 1) * 50f; // Adjust spotlight rotation based on character index
            UpdateCharacterStates(index);

            // Move spotlight to the target character
            await displayManager.RotateSpotlight(spotLight, targetRotation, spotlightRotationDuration);
            isTransitioning = false;

            Debug.Log($"[CharacterSelectionManager] Spotlight moved to character index: {index}");
        }



        private void UpdateCharacterStates(int focusedIndex)
        {
            for (int i = 0; i < availableCharacters.Length; i++)
            {
                var characterData = availableCharacters[i];
                var characterId = characterData.characterId;

                if (!IsCharacterUnlocked(characterId))
                {
                    SetCharacterState(characterId, CharacterDisplayState.Locked);
                    continue;
                }

                if (i == focusedIndex)
                {
                    SetCharacterState(characterId, CharacterDisplayState.Hover);
                    currentlyHoveredCharacterId = characterId;
                }
                else
                {
                    SetCharacterState(characterId, CharacterDisplayState.Idle);
                }
            }

            if (!string.IsNullOrEmpty(currentlyHoveredCharacterId))
            {
                var characterData = Array.Find(availableCharacters, c => c.characterId == currentlyHoveredCharacterId);
                if (characterData != null)
                {
                    uiManager.ShowCharacterPreview(characterData);
                    uiManager.UpdatePanelPositions(characterPositions[focusedIndex]);
                }
            }
        }

        private void SetCharacterState(string characterId, CharacterDisplayState newState)
        {
            if (!spawnedCharacters.ContainsKey(characterId)) return;

            characterStates[characterId] = newState;
            displayManager.UpdateCharacterState(spawnedCharacters[characterId], newState);
        }

        public async Task FocusCameraOnCharacter(Transform characterTransform)
        {
            if (characterTransform == null) return;

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[CharacterSelectionManager] Main camera not found!");
                return;
            }

            // Target position and rotation for the camera
            Vector3 targetPosition = characterTransform.position + new Vector3(0, 2, -3); // Adjust for desired camera offset
            Vector3 targetRotation = characterTransform.position - mainCamera.transform.position;

            // Smoothly move and rotate the camera
            await mainCamera.transform.DOMove(targetPosition, 1f).AsyncWaitForCompletion();
            await mainCamera.transform.DOLookAt(characterTransform.position, 0.5f).AsyncWaitForCompletion();

            // Show confirmation panel
            uiManager.ShowConfirmationPanel(true);
        }


        private void CleanupCharacterSelection()
        {
            foreach (var character in spawnedCharacters.Values)
            {
                if (character != null)
                {
                    Destroy(character);
                }
            }

            spawnedCharacters.Clear();
            characterStates.Clear();
            currentlySelectedCharacterId = null;
            currentlyHoveredCharacterId = null;
            isTransitioning = false;
            uiManager.Cleanup();
        }

        #endregion
    }
}