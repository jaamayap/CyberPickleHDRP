// File: Assets/Code/UI/Screens/MainMenu/ProfileSelectionController.cs
//
// Purpose: Handles the profile selection interface and transitions to main menu.
// Manages profile card creation, selection, and animation.
//
// Created: 2024-01-13
// Updated: 2024-01-15
//
// Dependencies:
// - CyberPickle.Core.Services.Authentication for authentication management
// - CyberPickle.Core.Services.Authentication for profile management
// - CyberPickle.Core.Events for game-wide event system

using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Core.Services.Authentication.Flow;
using CyberPickle.Core.Services.Authentication.Flow.Commands;
using CyberPickle.Core.Events;
using CyberPickle.Core.States;
using System;
using CyberPickle.Core.Services.Authentication.Flow.States;
using CyberPickle.UI.Components.ProfileCard;
using CyberPickle.Core.GameFlow.States.ProfileCard;

namespace CyberPickle.UI.Screens.MainMenu
{
    public class ProfileSelectionController : MonoBehaviour
    {
        [Header("Panel References")]
        [SerializeField] private GameObject profileSelectionPanel;
        [SerializeField] private Transform profilesContainer;
        [SerializeField] private GameObject profileCardPrefab;
        [SerializeField] private GameObject createProfileCardPrefab;

        [Header("Header Elements")]
        [SerializeField] private TextMeshProUGUI playerIdText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("UI Elements")]
        [SerializeField] private Button backButton;
        [SerializeField] private CanvasGroup mainMenuButtonsGroup;
        [SerializeField] private CanvasGroup deleteConfirmationDialog;

        [Header("Animation Settings")]
        [SerializeField] private float transitionDuration = 0.5f;
        [SerializeField] private Vector3 cornerPosition = new Vector3(800f, 400f, 0f);
        [SerializeField] private Vector3 cornerScale = new Vector3(0.7f, 0.7f, 0.7f);
        [SerializeField] private float statusMessageDuration = 3f;

        [Header("Canvas References")]
        [SerializeField] private Canvas mainCanvas;
        

        private AuthenticationManager authManager;
        private ProfileManager profileManager;
        private ProfileCardManager profileCardManager;
        private AuthenticationFlowManager flowManager;
        private List<GameObject> instantiatedCards = new List<GameObject>();
        private CreateProfileCardController createProfileCard;
        private bool isTransitioning;
        private Coroutine statusMessageCoroutine;
        

        #region Initialization

        private void Awake()
        {
            if (deleteConfirmationDialog != null)
            {
                deleteConfirmationDialog.alpha = 0f;
                deleteConfirmationDialog.interactable = false;
                deleteConfirmationDialog.blocksRaycasts = false;
            }
            Debug.Log("[ProfileSelection] Awake called");
            InitializeManagers();
            ValidateReferences();
        }

        private void Start()
        {
            Debug.Log("[ProfileSelection] Start called");
            if (!ValidateManagers()) return;

            InitializeUI();
            SubscribeToEvents();
        }

        private void InitializeManagers()
        {
            authManager = AuthenticationManager.Instance;
            profileManager = ProfileManager.Instance;
            flowManager = AuthenticationFlowManager.Instance;
            profileCardManager = ProfileCardManager.Instance;
        }

        private bool ValidateManagers()
        {
            if (authManager == null || profileManager == null || flowManager == null || profileCardManager == null)
            {
                Debug.LogError("[ProfileSelection] Required managers are null!");
                return false;
            }
            return true;
        }

        private void ValidateReferences()
        {
            if (profilesContainer == null)
                Debug.LogError("[ProfileSelection] profilesContainer is null!");
            if (profileCardPrefab == null)
                Debug.LogError("[ProfileSelection] profileCardPrefab is null!");
            if (createProfileCardPrefab == null)
                Debug.LogError("[ProfileSelection] createProfileCardPrefab is null!");
        }

        private void InitializeUI()
        {
            if (!ValidateManagers()) return;

            Debug.Log("[ProfileSelection] Initializing UI");

            // Only handle the profile-specific UI elements
            mainMenuButtonsGroup.alpha = 0f;
            mainMenuButtonsGroup.interactable = false;

            UpdateHeaderInfo();
            backButton.onClick.AddListener(HandleBackButton);

            // Initialize cards container
            InitializeCreateProfileCard();
        }

        private void InitializeCreateProfileCard()
        {
            GameObject createCardObject = Instantiate(createProfileCardPrefab, profilesContainer);
            createProfileCard = createCardObject.GetComponent<CreateProfileCardController>();
        }

        #endregion

        #region Event Handling

        private void SubscribeToEvents()
        {
            Debug.Log("[ProfileSelectionController] Subscribing to events");

            if (authManager != null)
            {
                Debug.Log("[ProfileSelectionController] Subscribing to AuthManager events");
                authManager.SubscribeToAuthenticationStateChanged(HandleAuthStateChanged);
                authManager.SubscribeToAuthenticationCompleted(HandleAuthenticationCompleted);
            }

            if (profileManager != null)
            {
                Debug.Log("[ProfileSelectionController] Subscribing to ProfileManager events");
                profileManager.SubscribeToNewProfileCreated(HandleProfileCreated);
                profileManager.SubscribeToProfileSwitched(HandleProfileSwitched);
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (profileManager != null)
            {
                profileManager.UnsubscribeFromNewProfileCreated(HandleProfileCreated);
                profileManager.UnsubscribeFromProfileSwitched(HandleProfileSwitched);
            }

            if (authManager != null)
            {
                authManager.UnsubscribeFromAuthenticationStateChanged(HandleAuthStateChanged);
                authManager.UnsubscribeFromAuthenticationCompleted(HandleAuthenticationCompleted);
            }
        }

        private void HandleAuthStateChanged(AuthenticationState state)
        {
            Debug.Log($"[ProfileSelectionController] Auth state changed to: {state}");

            if (state == AuthenticationState.Authenticated)
            {
                Debug.Log("[ProfileSelectionController] Authentication completed, waiting for UI readiness");
                GameEvents.OnUIAnimationCompleted.AddListener(OnPanelTransitionComplete);
            }
        }

        private void OnPanelTransitionComplete()
        {
            GameEvents.OnUIAnimationCompleted.RemoveListener(OnPanelTransitionComplete);

            // Only load profiles if our container is active
            if (profilesContainer != null && profilesContainer.gameObject.activeInHierarchy)
            {
                Debug.Log("[ProfileSelection] Panel transition complete, loading profiles");
                LoadProfiles();
            }
        }
       


        private IEnumerator ExecuteCommandCoroutine(IAuthCommand command)
        {
            Task commandTask = flowManager.ExecuteCommand(command);

            while (!commandTask.IsCompleted)
            {
                yield return null;
            }

            if (commandTask.IsFaulted)
            {
                Debug.LogError($"[ProfileSelection] Failed to load profiles: {commandTask.Exception?.InnerException?.Message}");
                ShowTemporaryStatus("Failed to load profiles", true);
                yield break;
            }

            // After profiles are loaded successfully, update UI
            UpdateUIState();
        }
        private void HandleAuthenticationCompleted(string playerId)
        {
            Debug.Log($"[ProfileSelection] Authentication completed for player: {playerId}");
            // Remove this LoadProfiles() call since HandleAuthStateChanged will handle it
        }

        private void HandleProfileCreated(string profileId)
        {
            if (!gameObject.activeInHierarchy) return;

            Debug.Log($"[ProfileSelection] HandleProfileCreated called for profile: {profileId}");

            var profiles = profileManager.GetAllProfiles();
            var newProfile = profiles.FirstOrDefault(p => p.ProfileId == profileId);

            if (newProfile != null)
            {
                
                profileCardManager.SetProfile(newProfile);
                profileCardManager.TransitionToState(ProfileCardState.Minimized);
            }

            if (profileSelectionPanel != null)
            {
                profileSelectionPanel.SetActive(false);
            }
            GameEvents.OnGameStateChanged.Invoke(GameState.MainMenu);
            StartCoroutine(FadeInMainMenuButtons());
        }

        private void HandleProfileSwitched(string profileId)
        {
            if (isTransitioning) return;
            UpdateUIState();
        }

        #endregion

        #region Profile Management

        private void LoadProfiles()
        {
            if (isTransitioning)
            {
                Debug.LogWarning("[ProfileSelectionController] Cannot load profiles - transitioning");
                return;
            }

            Debug.Log("[ProfileSelectionController] Starting LoadProfiles");
            ClearExistingProfiles();

            var profiles = profileManager.GetAllProfiles();
            if (profiles == null)
            {
                Debug.LogError("[ProfileSelectionController] GetAllProfiles returned null!");
                return;
            }

            Debug.Log($"[ProfileSelectionController] Found {profiles.Count} profiles");

            var sortedProfiles = profiles
                .OrderBy(p => p.ProfileId == "default" ? 0 : 1)
                .ThenBy(p => p.CreatedAt)
                .ToList();

            foreach (var profile in sortedProfiles)
            {
                CreateProfileCard(profile);
            }
        }



        private async void HandleProfileSelection(ProfileData profile)
        {
            if (isTransitioning) return;

            try
            {
                isTransitioning = true;
                var command = new SelectProfileCommand(profileManager, profile.ProfileId);
                await flowManager.ExecuteCommand(command);
                profileCardManager.SetProfile(profile);
                profileCardManager.TransitionToState(ProfileCardState.Minimized);

                // Let's let the MainMenuController handle the state change
                GameEvents.OnGameStateChanged.Invoke(GameState.MainMenu);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileSelection] Error selecting profile: {ex.Message}");
                ShowTemporaryStatus("Failed to select profile", true);
            }
            finally
            {
                isTransitioning = false;
            }
        }

        private async void HandleProfileDeletion(ProfileData profile)
        {
            if (profile == null) return;

            // Show confirmation dialog first
            var confirmed = await ShowDeleteConfirmationDialog(profile.DisplayName);
            if (!confirmed) return;

            try
            {
                // Execute the delete command
                var command = new DeleteProfileCommand(profileManager, profile.ProfileId);
                await flowManager.ExecuteCommand(command);

                // Just refresh the profiles list
                LoadProfiles();
                ShowTemporaryStatus("Profile deleted successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileSelection] Error deleting profile: {ex.Message}");
                ShowTemporaryStatus("Failed to delete profile", true);
            }
        }

        private async Task<bool> ShowDeleteConfirmationDialog(string profileName)
        {
            // Create a TaskCompletionSource to handle the async dialog result
            var tcs = new TaskCompletionSource<bool>();

            // Find the dialog in your scene
            var dialogPanel = GameObject.Find("DeleteConfirmationDialog")?.GetComponent<CanvasGroup>();
            if (dialogPanel == null)
            {
                Debug.LogError("[ProfileSelection] DeleteConfirmationDialog not found in scene!");
                return false;
            }

            // Get button references from the dialog panel
            var confirmButton = dialogPanel.GetComponentInChildren<Button>(true);
            var cancelButton = dialogPanel.GetComponentsInChildren<Button>(true)[1];
            var messageText = dialogPanel.GetComponentInChildren<TextMeshProUGUI>(true);

            // Show dialog
            dialogPanel.alpha = 1f;
            dialogPanel.interactable = true;
            dialogPanel.blocksRaycasts = true;

            // Set message
            messageText.text = $"Are you sure you want to delete profile {profileName}?";

            // Setup button listeners
            void OnConfirm()
            {
                tcs.SetResult(true);
                CleanupDialog();
            }

            void OnCancel()
            {
                tcs.SetResult(false);
                CleanupDialog();
            }

            void CleanupDialog()
            {
                // Remove listeners
                confirmButton.onClick.RemoveListener(OnConfirm);
                cancelButton.onClick.RemoveListener(OnCancel);

                // Hide dialog
                dialogPanel.alpha = 0f;
                dialogPanel.interactable = false;
                dialogPanel.blocksRaycasts = false;
            }

            // Add listeners
            confirmButton.onClick.AddListener(OnConfirm);
            cancelButton.onClick.AddListener(OnCancel);

            // Return the task
            return await tcs.Task;
        }
        private void ClearExistingProfiles()
        {
            foreach (var card in instantiatedCards.Where(card => card != null))
            {
                var cardController = card.GetComponent<ProfileCardController>();
                if (cardController != null)
                {
                    cardController.OnProfileSelected -= HandleProfileSelection;
                    cardController.OnProfileDeleted -= HandleProfileDeletion;
                }
                Destroy(card);
            }

            instantiatedCards.Clear();
            Debug.Log("[ProfileSelection] Cleared existing profile cards");
        }

        #endregion

        #region Profile Card Management

        private void CreateProfileCard(ProfileData profile)
        {
            if (profile == null || !gameObject.activeInHierarchy) return;

            Debug.Log($"[ProfileSelection] Creating profile card for {profile.DisplayName}");

            GameObject cardObject = Instantiate(profileCardPrefab, profilesContainer);
            if (cardObject == null)
            {
                Debug.LogError("[ProfileSelection] Failed to instantiate profile card prefab");
                return;
            }

            cardObject.name = $"ProfileCard_{profile.DisplayName}";

            var cardController = cardObject.GetComponent<ProfileCardController>();
            if (cardController != null)
            {
                cardController.Initialize(profile);
                cardController.OnProfileSelected += HandleProfileSelection;
                cardController.OnProfileDeleted += HandleProfileDeletion;
            }
            else
            {
                Debug.LogError("[ProfileSelection] ProfileCardController component not found on prefab");
            }

            instantiatedCards.Add(cardObject);
        }

        #endregion

        #region UI Updates

        private void UpdateHeaderInfo()
        {
            if (playerIdText != null)
                playerIdText.text = $"ID: #{authManager.CurrentPlayerId}";

            if (statusText != null)
                statusText.text = authManager.IsSignedIn ? "SYSTEM STATUS: ONLINE" : "SYSTEM STATUS: OFFLINE";
        }

        private void UpdateUIState()
        {
            if (isTransitioning) return;

            UpdateHeaderInfo();

            if (createProfileCard != null)
                createProfileCard.gameObject.SetActive(authManager.IsSignedIn);
        }

        private void ShowTemporaryStatus(string message, bool isError = false)
        {
            if (statusMessageCoroutine != null)
                StopCoroutine(statusMessageCoroutine);

            statusMessageCoroutine = StartCoroutine(ShowStatusMessageRoutine(message, isError));
        }

        private IEnumerator ShowStatusMessageRoutine(string message, bool isError)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = isError ? Color.red : Color.white;
            }

            yield return new WaitForSeconds(statusMessageDuration);

            if (statusText != null)
            {
                statusText.text = authManager.IsSignedIn ? "SYSTEM STATUS: ONLINE" : "SYSTEM STATUS: OFFLINE";
                statusText.color = Color.white;
            }
        }

        private void HandleBackButton()
        {
            if (isTransitioning) return;

            // If there's an active profile, ensure the card is hidden
            if (profileCardManager.CurrentState != ProfileCardState.Hidden)
            {
                profileCardManager.TransitionToState(ProfileCardState.Hidden);
            }

            GameEvents.OnProfileLoadRequested.Invoke();
        }

        #endregion

        #region UI Transitions

        private IEnumerator FadeInMainMenuButtons()
        {
            float elapsedTime = 0f;
            mainMenuButtonsGroup.gameObject.SetActive(true);

            while (elapsedTime < transitionDuration)
            {
                elapsedTime += Time.deltaTime;
                mainMenuButtonsGroup.alpha = elapsedTime / transitionDuration;
                yield return null;
            }

            mainMenuButtonsGroup.alpha = 1f;
            mainMenuButtonsGroup.interactable = true;
            mainMenuButtonsGroup.blocksRaycasts = true;

            GameEvents.OnGameStateChanged.Invoke(GameState.MainMenu);
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            if (statusMessageCoroutine != null)
                StopCoroutine(statusMessageCoroutine);

            UnsubscribeFromEvents();
            backButton.onClick.RemoveListener(HandleBackButton);

            foreach (var card in instantiatedCards)
            {
                if (card != null)
                {
                    var cardController = card.GetComponent<ProfileCardController>();
                    if (cardController != null)
                    {
                        cardController.OnProfileSelected -= HandleProfileSelection;
                        cardController.OnProfileDeleted -= HandleProfileDeletion;
                    }
                }
            }
        }

        #endregion
    }
}

