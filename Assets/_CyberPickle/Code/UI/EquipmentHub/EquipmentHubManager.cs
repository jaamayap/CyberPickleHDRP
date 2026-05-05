// File: Assets/Code/UI/EquipmentHub/EquipmentHubManager.cs
//
// Purpose: Manages the Equipment Hub scene where the player configures character equipment, 
// accesses the shop, and manages mining operations before starting a game. Controls the
// scene's UI sections and handles transitions between different parts of the Equipment Hub.
//
// Created: 2025-02-26
// Updated: 2025-02-26

using CyberPickle.Characters;
using CyberPickle.Characters.Data;
using CyberPickle.Core.Events;
using CyberPickle.Core.Interfaces;
using CyberPickle.Core.Management;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Core.States;
using CyberPickle.Core.UI;
using CyberPickle.UI.Transitions;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        [Header("Inventory References")]
        [SerializeField] private InventoryUIController inventoryController;

        [Header("Loadout Display")]
        [SerializeField] private LoadoutDisplayController loadoutDisplay;

        // Scene-bound: every serialized field above (spawn point, fade
        // controller, UI sections, buttons, inventory, loadout) points at
        // GameObjects authored inside the EquipmentHub scene. When the scene
        // unloads those references die. Persisting the manager across scenes
        // would leave a zombie holding dead refs and reject every fresh
        // re-entry as a "duplicate". Always re-create per scene load.
        protected override bool PersistAcrossScenes => false;

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
            EventSystemEnsurer.EnsureEventSystem();
            // Assign the ProfileManager instance
            profileManager = ProfileManager.Instance;

            // It's good practice to check if the instance was successfully retrieved
            if (profileManager == null)
            {
                Debug.LogError("[EquipmentHubManager] ProfileManager.Instance is null! Cannot proceed with character loading.");
                // Optionally, handle this error more gracefully, e.g., by returning or switching state
                isInitialized = true; // Mark as initialized to prevent re-entry, but it's in a bad state.
                return;
            }

            SetInitialVisibility();
            SetupNavigationButtons();
            StartCoroutine(LoadCharacterFromProfile());
            var navigationController = GetComponentInChildren<NavigationController>();
            if (navigationController != null)
            {
                navigationController.Initialize(this);
            }

            if (inventoryController != null)
            {
                inventoryController.Initialize();
            }

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
        public void ShowSection(HubSection section)
        {
            // Hide all sections first
            if (equipmentSection != null) equipmentSection.SetActive(false);
            if (shopSection != null) shopSection.SetActive(false);
            if (miningSection != null) miningSection.SetActive(false);

            // For now, Skills section doesn't exist, so we'll show a placeholder

            // Show the requested section
            switch (section)
            {
                case HubSection.Loadout:
                    if (equipmentSection != null) equipmentSection.SetActive(true);
                    break;

                case HubSection.Shop:
                    if (shopSection != null) shopSection.SetActive(true);
                    break;

                case HubSection.Skills:
                    // TODO: Show skills placeholder
                    Debug.Log("[EquipmentHub] Skills section coming soon!");
                    break;

                case HubSection.Mining:
                    if (miningSection != null) miningSection.SetActive(true);
                    break;
            }
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
                // IMPORTANT: pass the rotation to Instantiate as an argument so the
                // Animator's Awake captures 180° as its rotation baseline. If we set
                // localRotation AFTER Instantiate, the Animator records the prefab's
                // default rotation (0) as baseline and snaps the GameObject back to
                // it on the next frame.
                spawnedCharacter = Instantiate(
                    currentCharacterData.characterPrefab,
                    characterSpawnPoint.position,
                    characterSpawnPoint.rotation * Quaternion.Euler(0, 180, 0),
                    characterSpawnPoint);
                spawnedCharacter.transform.localPosition = Vector3.zero;

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
            // Initialize the loadout display with the current character
            if (loadoutDisplay != null && !string.IsNullOrEmpty(currentCharacterId))
            {
                loadoutDisplay.Initialize(currentCharacterId);
            }
            else
            {
                Debug.LogWarning("[EquipmentHubManager] LoadoutDisplay not assigned or no character ID!");
            }
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
                // Use unscaled time so the transition still completes if a
                // previous scene left Time.timeScale at 0 (e.g., the GameOver
                // phase before our PersistAcrossScenes fix made RunStateManager
                // restore the time scale on unload). UI navigation must never
                // depend on Time.timeScale.
                yield return new WaitForSecondsRealtime(fadeController.FadeDuration);
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
                yield return new WaitForSecondsRealtime(fadeController.FadeDuration);
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
