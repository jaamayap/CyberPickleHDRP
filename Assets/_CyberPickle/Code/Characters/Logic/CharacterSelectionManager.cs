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
using CyberPickle.Characters.UI;     // For CharacterUIManager
using DG.Tweening;
using System.Collections;
using CyberPickle.Core.Config;

namespace CyberPickle.Characters.Logic
{
    /// <summary>
    /// Manages the character selection flow: spawns characters, checks unlock status,
    /// updates display states, and coordinates with UI/Display managers. 
    /// </summary>
    public class CharacterSelectionManager : Manager<CharacterSelectionManager>
    {
        [Header("Scene References")]
        [SerializeField] private Transform[] characterPositions;
        [SerializeField] private Light spotLight;

        [Header("Character References")]
        [SerializeField] private CharacterData[] availableCharacters;

        [Header("Managers")]
        [SerializeField] private CharacterDisplayManager displayManager;
        [SerializeField] private CharacterUIManager uiManager;

        [Header("Camera Focus Settings")]
        [SerializeField] private Vector3 cameraFocusOffset = new Vector3(0, 2, -3);
        [SerializeField] private float cameraFocusFieldOfView = 40f;
        [SerializeField] private float focusTransitionDuration = 1f;
        [SerializeField] private float focusDistance = -5f;
        [SerializeField] private float focusHeightOffset = 0f;
        [SerializeField] private float lookOffset = 2f;
        [SerializeField] private float panelTransitionDuration = 0.8f; // add to the top
        [SerializeField] private float defaultFieldOfView = 60f; // fallback FOV

        // Core references
        private ProfileManager profileManager;
        private CameraManager cameraManager;
        private Coroutine spotlightFollowCoroutine;
        // State
        private Dictionary<string, GameObject> spawnedCharacters = new Dictionary<string, GameObject>();
        private Dictionary<string, CharacterDisplayState> characterStates = new Dictionary<string, CharacterDisplayState>();
        private string currentlySelectedCharacterId;
        private string currentlyHoveredCharacterId;
        private bool isTransitioning;
        private bool cameraFocused;
        private GameConfig gameConfig;
        #region Manager Overrides

        protected override void OnManagerAwake()
        {
            base.OnManagerAwake();
            if (!ValidateManagers()) return;
            var configRegistry = ConfigRegistry.Instance;
            if (configRegistry != null)
            {
                gameConfig = configRegistry.GetConfig<GameConfig>();
                if (gameConfig == null)
                {
                    Debug.LogError("[CharacterSelectionManager] GameConfig not found in ConfigRegistry!");
                }
            }
            else
            {
                Debug.LogError("[CharacterSelectionManager] ConfigRegistry instance not found!");
            }
            displayManager.Initialize();
            InitializeEvents();
        }

        protected override void OnManagerDestroyed()
        {
            base.OnManagerDestroyed();
            CleanupEvents();
            CleanupCharacterSelection();
        }

        #endregion

        #region Initialization

        private bool ValidateManagers()
        {
            profileManager = ProfileManager.Instance;
            cameraManager = CameraManager.Instance;

            bool valid = true;
            if (!profileManager) { Debug.LogError("[CharacterSelectionManager] Missing ProfileManager"); valid = false; }
            if (!cameraManager) { Debug.LogError("[CharacterSelectionManager] Missing CameraManager"); valid = false; }
            if (!displayManager) { Debug.LogError("[CharacterSelectionManager] Missing DisplayManager"); valid = false; }
            if (!uiManager) { Debug.LogError("[CharacterSelectionManager] Missing UIManager"); valid = false; }
            if (characterPositions == null || characterPositions.Length == 0)
            {
                Debug.LogError("[CharacterSelectionManager] No character positions assigned!");
                valid = false;
            }
            if (availableCharacters == null || availableCharacters.Length == 0)
            {
                Debug.LogError("[CharacterSelectionManager] No available characters configured!");
                valid = false;
            }
            return valid;
        }

        private void InitializeEvents()
        {
            GameEvents.OnGameStateChanged.AddListener(HandleGameStateChanged);
            GameEvents.OnCharacterHoverEnter.AddListener(HandleCharacterHoverEnter);
            GameEvents.OnCharacterHoverExit.AddListener(HandleCharacterHoverExit);
            GameEvents.OnCharacterSelected.AddListener(HandleCharacterSelected);
            GameEvents.OnCharacterDetailsRequested.AddListener(HandleCharacterDetails);
            GameEvents.OnCharacterSelectionCancelled.AddListener(HandleSelectionCancelled);
            GameEvents.OnCharacterConfirmed.AddListener(HandleCharacterConfirmed);
            Debug.Log("[CharacterSelectionManager] Events subscribed.");
        }

        private void CleanupEvents()
        {
            GameEvents.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
            GameEvents.OnCharacterHoverEnter.RemoveListener(HandleCharacterHoverEnter);
            GameEvents.OnCharacterHoverExit.RemoveListener(HandleCharacterHoverExit);
            GameEvents.OnCharacterSelected.RemoveListener(HandleCharacterSelected);
            GameEvents.OnCharacterDetailsRequested.RemoveListener(HandleCharacterDetails);
            GameEvents.OnCharacterSelectionCancelled.RemoveListener(HandleSelectionCancelled);
            GameEvents.OnCharacterConfirmed.RemoveListener(HandleCharacterConfirmed);
            Debug.Log("[CharacterSelectionManager] Events unsubscribed.");
        }

        #endregion

        #region GameState Handling

        private void HandleGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.CharacterSelect:
                    InitializeCharacterSelection();
                    break;
                case GameState.LevelSelect:
                case GameState.MainMenu:
                    CleanupCharacterSelection();
                    break;
            }
        }

        /// <summary>
        /// Spawns and sets up all characters for selection.
        /// </summary>
        private async void InitializeCharacterSelection()
        {
            if (profileManager?.ActiveProfile == null)
            {
                Debug.LogError("[CharacterSelectionManager] No active profile, cannot init character selection.");
                return;
            }


            isTransitioning = true;
            CleanupCharacterSelection();

            // Load characters to character manager
            var characterManager = CharacterManager.Instance;
            if (characterManager != null && availableCharacters != null && availableCharacters.Length > 0)
            {
                characterManager.SetAvailableCharacters(availableCharacters);
                Debug.Log($"[CharacterSelectionManager] Loaded {availableCharacters.Length} characters into CharacterManager");
            }
            else
            {
                Debug.LogWarning("[CharacterSelectionManager] Could not load characters into CharacterManager");
            }

            // Actually spawn each character
            for (int i = 0; i < availableCharacters.Length; i++)
            {
                var cData = availableCharacters[i];
                GameObject instance = await displayManager.SpawnCharacter(
                    cData, characterPositions[i].position, characterPositions[i].rotation
                );

                if (!instance) continue;

                // Layer and name
                instance.layer = LayerMask.NameToLayer("Character");
                // Set pointer handler
                var pointerHandler = instance.GetComponent<CharacterPointerHandler>();
                if (pointerHandler == null)
                {
                    pointerHandler = instance.AddComponent<CharacterPointerHandler>();
                }
                pointerHandler.Initialize(cData.characterId);

                // Store references
                spawnedCharacters[cData.characterId] = instance;

                // Determine initial display state
                bool unlocked = IsCharacterUnlocked(cData.characterId);
                characterStates[cData.characterId] = unlocked ? CharacterDisplayState.Idle : CharacterDisplayState.Locked;

                // Let pointer handler block clicks if locked
                pointerHandler.SetInteractable(unlocked, allowHover: true);
                // Update visuals
                displayManager.UpdateCharacterState(instance, characterStates[cData.characterId]);
            }

            // Pass data to UI manager
            uiManager.Initialize(availableCharacters);

            isTransitioning = false;
        }

        private void CleanupCharacterSelection()
        {
            foreach (var characterGO in spawnedCharacters.Values)
            {
                if (characterGO) Destroy(characterGO);
            }
            spawnedCharacters.Clear();
            characterStates.Clear();

            // Reset local state
            currentlySelectedCharacterId = null;
            currentlyHoveredCharacterId = null;
            cameraFocused = false;
            isTransitioning = false;

            // UI cleanup
            uiManager?.Cleanup();
        }

        #endregion

        #region Character Unlock / Profile

        public bool IsCharacterUnlocked(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return false;
            var cData = Array.Find(availableCharacters, c => c.characterId == characterId);
            if (!cData) return false;

            // If unlocked by default
            if (cData.unlockedByDefault) return true;

            // Check profile data
            var profile = profileManager?.ActiveProfile;
            if (profile?.CharacterProgress == null) return false;
            return profile.CharacterProgress.ContainsKey(characterId);
        }

        #endregion

        #region Hover / Selection Event Handlers

        /// <summary>
        /// Pointer hovers over a character.
        /// </summary>
        private void HandleCharacterHoverEnter(string characterId)
        {
            if (spotlightFollowCoroutine != null) StopCoroutine(spotlightFollowCoroutine);
            var characterTransform = spawnedCharacters[characterId].transform;
            spotlightFollowCoroutine = StartCoroutine(SmoothlyTrackCharacter(characterTransform));

            if (isTransitioning || cameraFocused) return;

            currentlyHoveredCharacterId = characterId;
            if (!spawnedCharacters.TryGetValue(characterId, out var characterGO)) return;

            bool unlocked = IsCharacterUnlocked(characterId);
            characterStates[characterId] = CharacterDisplayState.Hover;
            displayManager.UpdateCharacterState(characterGO, characterStates[characterId]);

            // Show the preview or unlock panel
            var cData = Array.Find(availableCharacters, c => c.characterId == characterId);
            uiManager.ShowCharacterPreview(cData, unlocked);

            // <--- CRITICAL: position the hover panel over the correct character
            uiManager.UpdatePanelPositions(characterGO.transform);
        }

        private IEnumerator SmoothlyTrackCharacter(Transform characterRootTransform) // Parameter is the root transform of the character
        {
            if (characterRootTransform == null)
            {
                Debug.LogError("[CharacterSelectionManager] SmoothlyTrackCharacter: characterRootTransform is null.");
                yield break;
            }

            SkinnedMeshRenderer smr = characterRootTransform.GetComponentInChildren<SkinnedMeshRenderer>();

            if (smr == null)
            {
                Debug.LogWarning($"[CharacterSelectionManager] No SkinnedMeshRenderer found on '{characterRootTransform.name}' or its children. Spotlight will track the root transform's position. Animation movement might not be followed.");
                // Fallback to tracking the root transform's position if no SkinnedMeshRenderer is found
                while (true)
                {
                    if (characterRootTransform == null || !characterRootTransform.gameObject.activeInHierarchy) yield break; // Stop if character is destroyed

                    Vector3 targetPos = characterRootTransform.position + Vector3.up * 1.5f; // offset above character's feet
                    Quaternion targetRotation = Quaternion.LookRotation(targetPos - spotLight.transform.position);
                    spotLight.transform.rotation = Quaternion.Slerp(spotLight.transform.rotation, targetRotation, Time.deltaTime * 5f); // Increased Slerp speed slightly
                    yield return null;
                }
            }
            else
            {
                // Track the center of the SkinnedMeshRenderer's bounds
                while (true)
                {
                    // Add a null check for smr itself first
                    if (smr == null || !smr.gameObject.activeInHierarchy) yield break; // Stop if SMR or its GameObject is destroyed/inactive

                    Vector3 targetPos = smr.bounds.center;
                    Quaternion targetRotation = Quaternion.LookRotation(targetPos - spotLight.transform.position);
                    // Also check if spotLight becomes null during operation
                    if (spotLight == null) yield break;
                    spotLight.transform.rotation = Quaternion.Slerp(spotLight.transform.rotation, targetRotation, Time.deltaTime * 5f);
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Pointer no longer hovering over a character.
        /// </summary>
        private void HandleCharacterHoverExit(string characterId)
        {
            if (!spawnedCharacters.ContainsKey(characterId)) return;

            bool unlocked = IsCharacterUnlocked(characterId);
            characterStates[characterId] = unlocked ? CharacterDisplayState.Idle : CharacterDisplayState.Locked;

            displayManager.UpdateCharacterState(spawnedCharacters[characterId], characterStates[characterId]);
            uiManager.HideUnlockPanel();
            currentlyHoveredCharacterId = null;
        }

        /// <summary>
        /// Left click on character => select the character.
        /// </summary>
        private async void HandleCharacterSelected(string characterId)
        {
            // Guard clauses
            if (isTransitioning || !spawnedCharacters.ContainsKey(characterId)) return;
            if (!IsCharacterUnlocked(characterId)) return;

            isTransitioning = true;

            // If there's already a panel visible for another character, hide it immediately
            // so we don't see the old overhead tween from the previous character to the new one.
            uiManager.HideAllPanelsImmediate();

            currentlySelectedCharacterId = characterId;

            // Visually mark as selected
            characterStates[characterId] = CharacterDisplayState.Selected;
            displayManager.UpdateCharacterState(spawnedCharacters[characterId], CharacterDisplayState.Selected);

            // Grab transform for positioning
            var charTransform = spawnedCharacters[characterId].transform;

            // Fire off two asynchronous tasks in parallel:

            // 1) Camera focusing task
            var cameraTask = cameraManager.FocusCameraOnCharacter(
                charTransform,
                focusHeightOffset,
                focusDistance,
                focusTransitionDuration,
                cameraFocusFieldOfView,
                lookOffset
            );

            // 2) Panel animation task (first show details text, then slide/scale to left side)
            var cData = Array.Find(availableCharacters, c => c.characterId == characterId);
            uiManager.ShowDetails(cData);  // sets text/lore but doesn't do overhead offset
            var panelTask = uiManager.AnimatePanelToFocusedPosition(charTransform, panelTransitionDuration);

            // Wait for both the camera and panel animations to finish
            await Task.WhenAll(cameraTask, panelTask);

            // Now that both are done, show the confirmation panel on top
            uiManager.ShowConfirmationPanel(true);

            // Ensure the confirmation panel (and others) remain positioned correctly
            uiManager.UpdatePanelPositions(charTransform);

            cameraFocused = true;
            isTransitioning = false;
        }


        /// <summary>
        /// Right click => request details without fully selecting.
        /// </summary>
        private async void HandleCharacterDetails(string characterId)
        {
            if (isTransitioning || !IsCharacterUnlocked(characterId)) return;

            currentlyHoveredCharacterId = characterId;
            var cData = Array.Find(availableCharacters, c => c.characterId == characterId);
            uiManager.ShowDetails(cData);
            await uiManager.AnimatePanelOverheadPosition(spawnedCharacters[characterId].transform, 0.3f);
            // <--- CRITICAL: again, update panel position for the details panel
            if (spawnedCharacters.TryGetValue(characterId, out var characterGO))
            {
                uiManager.UpdatePanelPositions(characterGO.transform);
            }
        }

        /// <summary>
        /// The "Cancel" button or event was triggered.
        /// </summary>
        private async void HandleSelectionCancelled()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            // Hide the confirmation panel right away
            uiManager.ShowConfirmationPanel(false);

            // If there's a currently selected character, we'll animate the panel + camera in parallel
            if (!string.IsNullOrEmpty(currentlySelectedCharacterId) &&
                spawnedCharacters.TryGetValue(currentlySelectedCharacterId, out var selectedGO))
            {
                // Kick off two async tasks in parallel:
                // 1) Panel returning overhead at full scale
                var overheadTask = uiManager.AnimatePanelReturnToOverhead(selectedGO.transform, 0.5f);

                // 2) Camera returning to wide “character selection” view
                var cameraTask = cameraManager.ResetToCharacterSelectionView(focusTransitionDuration, defaultFieldOfView);

                // Wait for both to finish
                await Task.WhenAll(overheadTask, cameraTask);

                // Once both animations complete, reset display state to Idle/Locked
                bool unlocked = IsCharacterUnlocked(currentlySelectedCharacterId);
                characterStates[currentlySelectedCharacterId] = unlocked ? CharacterDisplayState.Idle : CharacterDisplayState.Locked;
                displayManager.UpdateCharacterState(selectedGO, characterStates[currentlySelectedCharacterId]);
            }
            else
            {
                // If there's no selected character, we only need to reset the camera
                await cameraManager.ResetToCharacterSelectionView(focusTransitionDuration, defaultFieldOfView);
            }

            currentlySelectedCharacterId = null;
            cameraFocused = false;
            isTransitioning = false;
        }



        /// <summary>
        /// The "Confirm" button was pressed => finalize selection.
        /// </summary>
        private async void HandleCharacterConfirmed()
        {
            if (string.IsNullOrEmpty(currentlySelectedCharacterId))
            {
                Debug.LogWarning("[CharacterSelectionManager] No character selected to confirm.");
                return;
            }
            if (isTransitioning)
            {
                Debug.LogWarning("[CharacterSelectionManager] Character confirmation already in progress.");
                return;
            }

            isTransitioning = true; // Prevent multiple calls

            Debug.Log($"[CharacterSelectionManager] Confirming character: {currentlySelectedCharacterId}");

            var profile = profileManager?.ActiveProfile;
            if (profile != null)
            {
                profile.SetLastSelectedCharacter(currentlySelectedCharacterId);
                var updateResult = await profileManager.UpdateProfileAsync(profile);
                if (!updateResult.Success)
                {
                    Debug.LogError($"[CharacterSelectionManager] Failed to save last selected character: {updateResult.Message}");
                    isTransitioning = false; // Reset flag on error
                    return;
                }
                Debug.Log($"[CharacterSelectionManager] Last selected character '{currentlySelectedCharacterId}' saved to profile '{profile.ProfileId}'.");
            }
            else
            {
                Debug.LogError("[CharacterSelectionManager] Active profile is null. Cannot save last selected character.");
                isTransitioning = false; // Reset flag on error
                return;
            }

            // Hide UI elements from character selection
            uiManager.ShowConfirmationPanel(false);
            uiManager.HideAllPanels(); // Hides details panel etc.

            // Reset camera if it was focused on a character
            if (cameraFocused)
            {
                Debug.Log("[CharacterSelectionManager] Resetting camera to character selection view before scene change.");
                // Use a default FOV or one stored in CameraManager
                await cameraManager.ResetToCharacterSelectionView(focusTransitionDuration, defaultFieldOfView);
                cameraFocused = false;
            }

            // Optional: Small delay for UI/camera animations to visually complete
            await Task.Delay(TimeSpan.FromMilliseconds(200));

            // Reset the transitioning flag before scene change
            isTransitioning = false;

            // Change game state to EquipmentSelect.
            // GameManager will listen for this and load the appropriate scene.
            Debug.Log("[CharacterSelectionManager] Invoking GameState.EquipmentSelect event.");
            GameEvents.OnGameStateChanged.Invoke(GameState.EquipmentSelect);
        }

        #endregion

        #region External Helpers

        /// <summary>
        /// For external checks: is a character ID currently selected?
        /// </summary>
        public bool IsCharacterSelected(string characterId)
        {
            return (characterId == currentlySelectedCharacterId);
        }

        /// <summary>
        /// If other scripts need the actual character instance.
        /// </summary>
        public GameObject GetCharacterGameObject(string characterId)
        {
            spawnedCharacters.TryGetValue(characterId, out var result);
            return result;
        }

        #endregion
    }
}
