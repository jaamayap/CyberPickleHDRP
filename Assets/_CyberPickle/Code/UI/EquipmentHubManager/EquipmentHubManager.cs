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

            Debug.Log("[EquipmentHubManager] Initializing");

            // Get manager dependencies
            profileManager = ProfileManager.Instance;
            if (profileManager == null)
            {
                Debug.LogError("[EquipmentHubManager] Profile Manager not found!");
                return;
            }

            // Set initial section visibility
            SetInitialVisibility();

            // Setup navigation buttons
            SetupNavigationButtons();

            // Load character model
            StartCoroutine(LoadCharacterModel());

            isInitialized = true;
            Debug.Log("[EquipmentHubManager] Initialized successfully");
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
                fadeController.FadeFromBlack();
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

        /// <summary>
        /// Loads the selected character model
        /// </summary>
        private IEnumerator LoadCharacterModel()
        {
            // Get the character ID from profile (for now we'll use a placeholder)
            // Later this should come from the profile's selected character
            currentCharacterId = "default_character";

            // When we have profile integration, use this:
            // var profile = profileManager.ActiveProfile;
            // if (profile != null)
            //    currentCharacterId = profile.LastSelectedCharacterId;

            // Load character data - update path as needed for your project
            currentCharacterData = Resources.Load<CharacterData>($"Characters/{currentCharacterId}");

            if (currentCharacterData == null)
            {
                Debug.LogError($"[EquipmentHubManager] Character data not found for ID: {currentCharacterId}");
                yield break;
            }

            // Make sure we have a spawn point
            if (characterSpawnPoint == null)
            {
                Debug.LogError("[EquipmentHubManager] Character spawn point is null!");
                yield break;
            }

            // Instantiate the character model
            spawnedCharacter = Instantiate(currentCharacterData.characterPrefab, characterSpawnPoint);
            spawnedCharacter.transform.localPosition = Vector3.zero;
            spawnedCharacter.transform.localRotation = Quaternion.Euler(0, 180, 0); // Face forward

            // Setup character animations
            var animator = spawnedCharacter.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetTrigger("Idle");
            }

            Debug.Log($"[EquipmentHubManager] Character model loaded: {currentCharacterData.displayName}");

            // Initialize sections that need the character reference
            InitializeEquipmentSection();
        }

        /// <summary>
        /// Initializes the equipment section with character data
        /// </summary>
        private void InitializeEquipmentSection()
        {
            // Initialize equipment UI with character data
            // This would be expanded with your equipment UI logic
        }

        /// <summary>
        /// Switches the active section in the hub
        /// </summary>
        /// <param name="sectionName">The name of the section to activate</param>
        public void SwitchToSection(string sectionName)
        {
            // Hide all sections first
            if (equipmentSection != null) equipmentSection.SetActive(false);
            if (shopSection != null) shopSection.SetActive(false);
            if (miningSection != null) miningSection.SetActive(false);

            // Show the requested section
            switch (sectionName)
            {
                case "Equipment":
                    if (equipmentSection != null)
                    {
                        equipmentSection.SetActive(true);
                        Debug.Log("[EquipmentHubManager] Switched to Equipment section");
                    }
                    break;
                case "Shop":
                    if (shopSection != null)
                    {
                        shopSection.SetActive(true);
                        Debug.Log("[EquipmentHubManager] Switched to Shop section");
                    }
                    break;
                case "Mining":
                    if (miningSection != null)
                    {
                        miningSection.SetActive(true);
                        Debug.Log("[EquipmentHubManager] Switched to Mining section");
                    }
                    break;
                default:
                    Debug.LogWarning($"[EquipmentHubManager] Unknown section: {sectionName}");
                    break;
            }
        }

        /// <summary>
        /// Starts the game and transitions to the level select screen
        /// </summary>
        public void StartGame()
        {
            StartCoroutine(TransitionToLevelSelect());
        }

        /// <summary>
        /// Returns to the character selection screen
        /// </summary>
        public void ReturnToCharacterSelect()
        {
            StartCoroutine(TransitionToCharacterSelect());
        }

        /// <summary>
        /// Transitions to the level selection scene
        /// </summary>
        private IEnumerator TransitionToLevelSelect()
        {
            // Fade out
            if (fadeController != null)
            {
                fadeController.FadeToBlack();
                yield return new WaitForSeconds(fadeController.FadeDuration);
            }

            // Change game state
            GameEvents.OnGameStateChanged.Invoke(GameState.LevelSelect);

            // Load level select scene
            SceneManager.LoadScene("LevelSelect");
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
            // Clean up button listeners
            if (equipmentButton != null)
                equipmentButton.onClick.RemoveAllListeners();

            if (shopButton != null)
                shopButton.onClick.RemoveAllListeners();

            if (miningButton != null)
                miningButton.onClick.RemoveAllListeners();

            if (startGameButton != null)
                startGameButton.onClick.RemoveAllListeners();

            if (backButton != null)
                backButton.onClick.RemoveAllListeners();

            // Call base method
            base.OnManagerDestroyed();
        }
    }
}
