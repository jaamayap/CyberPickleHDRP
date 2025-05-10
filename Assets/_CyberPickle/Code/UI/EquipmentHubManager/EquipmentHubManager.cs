// File: Assets/Code/UI/EquipmentHub/EquipmentHubManager.cs
//
// Purpose: Manages the Equipment Hub scene where the player configures character equipment, 
// accesses the shop, and manages mining operations before starting a game. Controls the
// scene's UI sections and handles transitions between different parts of the Equipment Hub.
//
// Created: 2025-02-26
// Updated: 2025-02-26

using UnityEngine;
using System.Collections;
using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;
using CyberPickle.Core.Events;
using CyberPickle.Core.States;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Characters.Data;
using CyberPickle.UI.Transitions;
using UnityEngine.SceneManagement;
using CyberPickle.Characters;

namespace CyberPickle.UI.EquipmentHub
{
    /// <summary>
    /// Manages the Equipment Hub scene where the player configures character equipment, 
    /// accesses the shop, and manages mining operations before starting a game
    /// </summary>
    public class EquipmentHubManager : Manager<EquipmentHubManager>, IInitializable
    {
        [Header("Scene References")]
        [SerializeField] private Transform characterSpawnPoint;
        [SerializeField] private FadeScreenController fadeController;

        [Header("UI Sections")]
        [SerializeField] private GameObject equipmentSection;
        [SerializeField] private GameObject shopSection;
        [SerializeField] private GameObject miningSection;
        [SerializeField] private GameObject navigationPanel;

        [Header("Navigation Buttons")]
        [SerializeField] private UnityEngine.UI.Button equipmentButton;
        [SerializeField] private UnityEngine.UI.Button shopButton;
        [SerializeField] private UnityEngine.UI.Button miningButton;
        [SerializeField] private UnityEngine.UI.Button startGameButton;
        [SerializeField] private UnityEngine.UI.Button backButton;

        // Manager dependencies
        private ProfileManager profileManager;

        // Runtime data
        private GameObject spawnedCharacter;
        private CharacterData currentCharacterData;
        private string currentCharacterId;
        private bool isInitialized = false;

        /// <summary>
        /// Initializes the Equipment Hub manager and UI
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;
            Debug.Log("[EquipmentHubManager] Initializing...");

            // Don't trigger any game state changes here
            // Just initialize the scene components

            SetInitialVisibility();
            SetupNavigationButtons();
            StartCoroutine(LoadCharacterFromProfile());

            isInitialized = true;
            Debug.Log("[EquipmentHubManager] Initialized successfully.");
        }

        /// <summary>
        /// Sets the initial visibility of UI sections
        /// </summary>
        private void SetInitialVisibility()
        {
            // Show equipment section by default, hide others
            if (equipmentSection != null) equipmentSection.SetActive(true);
            if (shopSection != null) shopSection.SetActive(false);
            if (miningSection != null) miningSection.SetActive(false);

            // Ensure navigation panel is visible
            if (navigationPanel != null) navigationPanel.SetActive(true);

            // Fade in the scene if fade controller exists
            if (fadeController != null)
            {
                // Assuming FadeScreenController is DontDestroyOnLoad or already in scene
                fadeController.FadeFromBlack();
            }
        }

        /// <summary>
        /// Sets up button listeners for navigation
        /// </summary>
        private void SetupNavigationButtons()
        {
            if (equipmentButton != null)
                equipmentButton.onClick.AddListener(() => SwitchToSection("Equipment"));

            if (shopButton != null)
                shopButton.onClick.AddListener(() => SwitchToSection("Shop"));

            if (miningButton != null)
                miningButton.onClick.AddListener(() => SwitchToSection("Mining"));

            if (startGameButton != null)
                startGameButton.onClick.AddListener(StartGame);

            if (backButton != null)
                backButton.onClick.AddListener(ReturnToCharacterSelect);
        }


        private IEnumerator LoadCharacterFromProfile()
        {
            // Wait a frame to ensure ProfileManager.ActiveProfile is settled
            yield return null;

            if (profileManager.ActiveProfile == null)
            {
                Debug.LogError("[EquipmentHubManager] ActiveProfile is null. Cannot load character.");
                // Potentially redirect back to character selection
                GameEvents.OnGameStateChanged.Invoke(GameState.CharacterSelect);
                yield break;
            }

            currentCharacterId = profileManager.ActiveProfile.LastSelectedCharacterId;
            if (string.IsNullOrEmpty(currentCharacterId))
            {
                Debug.LogError("[EquipmentHubManager] LastSelectedCharacterId is not set in profile. Cannot load character.");
                // Redirect back to character selection
                GameEvents.OnGameStateChanged.Invoke(GameState.CharacterSelect);
                yield break;
            }

            Debug.Log($"[EquipmentHubManager] Loading character model for ID: {currentCharacterId}");

            // Get character data from CharacterManager instead of Resources.Load
            var characterManager = CharacterManager.Instance;
            if (characterManager == null)
            {
                Debug.LogError("[EquipmentHubManager] CharacterManager not found!");
                GameEvents.OnGameStateChanged.Invoke(GameState.CharacterSelect);
                yield break;
            }

            currentCharacterData = characterManager.GetCharacterDataById(currentCharacterId);

            if (currentCharacterData == null)
            {
                Debug.LogError($"[EquipmentHubManager] CharacterData not found for ID: '{currentCharacterId}'. Returning to character selection.");
                GameEvents.OnGameStateChanged.Invoke(GameState.CharacterSelect);
                yield break;
            }

            if (currentCharacterData.characterPrefab == null)
            {
                Debug.LogError($"[EquipmentHubManager] CharacterData for '{currentCharacterData.displayName}' has no characterPrefab assigned!");
                GameEvents.OnGameStateChanged.Invoke(GameState.CharacterSelect);
                yield break;
            }

            if (characterSpawnPoint == null)
            {
                Debug.LogError("[EquipmentHubManager] Character spawn point is not assigned in the inspector!");
                yield break;
            }

            // Destroy any previously spawned character
            if (spawnedCharacter != null)
            {
                Destroy(spawnedCharacter);
            }

            // Create error handling for character instantiation
            try
            {
                spawnedCharacter = Instantiate(currentCharacterData.characterPrefab, characterSpawnPoint);
                spawnedCharacter.transform.localPosition = Vector3.zero;
                spawnedCharacter.transform.localRotation = Quaternion.Euler(0, 180, 0); // Face camera

                Animator animator = spawnedCharacter.GetComponent<Animator>();
                if (animator != null && !string.IsNullOrEmpty(currentCharacterData.idleAnimationTrigger))
                {
                    animator.SetTrigger(currentCharacterData.idleAnimationTrigger);
                }
                else if (animator == null)
                {
                    Debug.LogWarning($"[EquipmentHubManager] Character '{currentCharacterData.displayName}' prefab is missing an Animator component.");
                }
                else
                {
                    Debug.LogWarning($"[EquipmentHubManager] CharacterData for '{currentCharacterData.displayName}' has no idleAnimationTrigger defined.");
                }

                Debug.Log($"[EquipmentHubManager] Character model '{currentCharacterData.displayName}' loaded and instantiated.");

                // Initialize equipment section now that character is loaded
                InitializeEquipmentSection();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EquipmentHubManager] Failed to instantiate character: {ex.Message}\n{ex.StackTrace}");
                GameEvents.OnGameStateChanged.Invoke(GameState.CharacterSelect);
            }
        }




        /// <summary>
        /// Initializes the equipment section with character data
        /// </summary>
        private void InitializeEquipmentSection()
        {
            // Placeholder for equipment UI initialization based on currentCharacterData and profile
            Debug.Log($"[EquipmentHubManager] Initializing equipment section for {currentCharacterData?.displayName ?? "Unknown Character"}");
        }

        /// <summary>
        /// Switches the active section in the hub
        /// </summary>
        /// <param name="sectionName">The name of the section to activate</param>
        public void SwitchToSection(string sectionName)
        {
            if (equipmentSection != null) equipmentSection.SetActive(false);
            if (shopSection != null) shopSection.SetActive(false);
            if (miningSection != null) miningSection.SetActive(false);

            switch (sectionName)
            {
                case "Equipment":
                    if (equipmentSection != null) equipmentSection.SetActive(true);
                    Debug.Log("[EquipmentHubManager] Switched to Equipment section");
                    break;
                case "Shop":
                    if (shopSection != null) shopSection.SetActive(true);
                    Debug.Log("[EquipmentHubManager] Switched to Shop section");
                    break;
                case "Mining":
                    if (miningSection != null) miningSection.SetActive(true);
                    Debug.Log("[EquipmentHubManager] Switched to Mining section");
                    break;
                default:
                    Debug.LogWarning($"[EquipmentHubManager] Unknown section: {sectionName}");
                    if (equipmentSection != null) equipmentSection.SetActive(true); // Default to equipment
                    break;
            }
        }

        /// <summary>
        /// Starts the game and transitions to the level select screen
        /// </summary>
        public void StartGame()
        {
            Debug.Log("[EquipmentHubManager] StartGame clicked. Transitioning to LevelSelect.");

            // Ensure character is properly loaded before transitioning
            if (currentCharacterData == null || spawnedCharacter == null)
            {
                Debug.LogError("[EquipmentHubManager] Character not loaded properly. Returning to character selection.");
                GameEvents.OnGameStateChanged.Invoke(GameState.CharacterSelect);
                return;
            }

            StartCoroutine(TransitionToSceneInternal(GameState.LevelSelect, "LevelSelect"));
        }

        /// <summary>
        /// Returns to the character selection screen
        /// </summary>
        public void ReturnToCharacterSelect()
        {
            Debug.Log("[EquipmentHubManager] ReturnToCharacterSelect clicked.");
            StartCoroutine(TransitionToSceneInternal(GameState.CharacterSelect, "CharacterSelect")); // GameConfig has "CharacterSelect"
        }

        /// <summary>
        /// Transitions to the level selection scene
        /// </summary>
        private IEnumerator TransitionToSceneInternal(GameState targetState, string sceneNameKeyInConfig)
        {
            if (fadeController != null)
            {
                fadeController.FadeToBlack();
                yield return new WaitForSeconds(fadeController.FadeDuration);
            }

            GameEvents.OnGameStateChanged.Invoke(targetState);
            // GameManager will handle loading the scene based on the GameState and GameConfig.
            // No need to call SceneManager.LoadScene here directly.
        }

        /// <summary>
        /// Transitions back to the character selection scene
        /// </summary>
        private IEnumerator TransitionToCharacterSelect()
        {
            // Fade out
            if (fadeController != null)
            {
                fadeController.FadeToBlack();
                yield return new WaitForSeconds(fadeController.FadeDuration);
            }

            // Change game state
            GameEvents.OnGameStateChanged.Invoke(GameState.CharacterSelect);

            // Load character select scene
            SceneManager.LoadScene("CharacterSelect");
        }

        /// <summary>
        /// Cleanup when the manager is destroyed
        /// </summary>
        protected override void OnManagerDestroyed()
        {
            base.OnManagerDestroyed(); // Important for base class cleanup

            if (equipmentButton != null) equipmentButton.onClick.RemoveAllListeners();
            if (shopButton != null) shopButton.onClick.RemoveAllListeners();
            if (miningButton != null) miningButton.onClick.RemoveAllListeners();
            if (startGameButton != null) startGameButton.onClick.RemoveAllListeners();
            if (backButton != null) backButton.onClick.RemoveAllListeners();

            Debug.Log("[EquipmentHubManager] Cleaned up listeners.");
        }
    }
}
