// File: Assets/Code/UI/Screens/MainMenu/ProfileSelectionController.cs
//
// Purpose: Handles the profile selection interface and transitions to main menu.
// Manages profile card creation, selection, and animation.
//
// Created: 2024-01-13
// Updated: 2024-01-14
//
// Dependencies:
// - CyberPickle.Core.Services.Authentication for authentication management
// - CyberPickle.Core.Services.Authentication for profile management
// - CyberPickle.Core.Events for game-wide event system

using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Services.Authentication.Data;
using System.Collections.Generic;
using CyberPickle.Core.Events;
using CyberPickle.Core.States;
using System.Linq;

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

        [Header("Animation Settings")]
        [SerializeField] private float transitionDuration = 0.5f;
        [SerializeField] private Vector3 cornerPosition = new Vector3(800f, 400f, 0f);
        [SerializeField] private Vector3 cornerScale = new Vector3(0.7f, 0.7f, 0.7f);

        [Header("Canvas References")]
        [SerializeField] private Canvas mainCanvas; 

        private AuthenticationManager authManager;
        private ProfileManager profileManager;
        private List<GameObject> instantiatedCards = new List<GameObject>();
        private CreateProfileCardController createProfileCard;
        private bool isTransitioning;
        private GameObject currentActiveCard; // To keep track of the active card

        private void Awake()
        {
            authManager = AuthenticationManager.Instance;
            profileManager = ProfileManager.Instance;
            
        }

        private void Start()
        {
            InitializeUI();
            SubscribeToEvents();

            // Load profiles after a short delay to ensure authentication is complete
            StartCoroutine(DelayedProfileLoad());
        }

        private IEnumerator DelayedProfileLoad()
        {
            yield return new WaitForSeconds(0.5f);
            LoadProfiles();
            UpdateUIState();
        }

        private void InitializeUI()
        {
            mainMenuButtonsGroup.alpha = 0f;
            mainMenuButtonsGroup.interactable = false;

            UpdateHeaderInfo();
            backButton.onClick.AddListener(HandleBackButton);

            // Initialize create profile card
            GameObject createCardObject = Instantiate(createProfileCardPrefab, profilesContainer);
            createProfileCard = createCardObject.GetComponent<CreateProfileCardController>();
        }

        private void SubscribeToEvents()
        {
            if (profileManager != null)
            {
                profileManager.SubscribeToNewProfileCreated(HandleProfileCreated);
                profileManager.SubscribeToProfileSwitched(HandleProfileSwitched);
            }

            if (authManager != null)
            {
                authManager.SubscribeToAuthenticationStateChanged(HandleAuthStateChanged);
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
            }
        }

        private void UpdateHeaderInfo()
        {
            if (playerIdText != null)
                playerIdText.text = $"ID: #{authManager.CurrentPlayerId}";

            if (statusText != null)
                statusText.text = authManager.IsSignedIn ? "SYSTEM STATUS: ONLINE" : "SYSTEM STATUS: OFFLINE";
        }

        private void LoadProfiles()
        {
            if (isTransitioning || !gameObject.activeInHierarchy) return;

            Debug.Log("Loading profiles...");
            ClearExistingProfiles();

            var profiles = profileManager.GetAllProfiles();

            if (profiles == null || profiles.Count == 0)
            {
                Debug.Log("No profiles found");
                return;
            }

            Debug.Log($"Found {profiles.Count} profiles");

            // Sort profiles: default first, then by creation date
            var sortedProfiles = profiles.OrderBy(p => p.ProfileId == "default" ? 0 : 1)
                                        .ThenBy(p => p.CreatedAt);

            foreach (var profile in sortedProfiles)
            {
                CreateProfileCard(profile);
            }
        }

        private void CreateProfileCard(ProfileData profile)
        {
            if (profile == null || !gameObject.activeInHierarchy) return;

            Debug.Log($"Creating profile card for {profile.DisplayName}");

            GameObject card = Instantiate(profileCardPrefab, profilesContainer);
            if (card == null)
            {
                Debug.LogError("Failed to instantiate profile card prefab");
                return;
            }

            // Set a meaningful name for the GameObject
            card.name = $"ProfileCard_{profile.DisplayName}";

            ConfigureProfileCard(card, profile);
            instantiatedCards.Add(card);
        }

        private void ConfigureProfileCard(GameObject card, ProfileData profile)
        {
            // Configure card UI elements
            var nameText = card.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = profile.DisplayName;
                Debug.Log($"Set profile card name text: {profile.DisplayName}");
            }
            else
            {
                Debug.LogError("Profile card is missing TextMeshProUGUI component");
            }

            // Add selection button listener
            var selectButton = card.GetComponentInChildren<Button>();
            if (selectButton != null)
            {
                selectButton.onClick.AddListener(() => HandleProfileSelection(card, profile));
                Debug.Log($"Added selection listener for profile: {profile.DisplayName}");
            }
            else
            {
                Debug.LogError("Profile card is missing Button component");
            }

            // Configure delete button
            var deleteButton = card.GetComponentsInChildren<Button>()
                .FirstOrDefault(b => b.gameObject.name.Contains("Delete"));
            if (deleteButton != null)
            {
                deleteButton.onClick.AddListener(() => HandleProfileDeletion(profile));
                Debug.Log($"Added deletion listener for profile: {profile.DisplayName}");
            }
            else
            {
                Debug.LogWarning("Profile card is missing Delete button");
            }

            RectTransform rectTransform = card.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // Set pivot to top-right corner
                rectTransform.pivot = new Vector2(1f, 1f);

                // Set anchor to top-right corner
                rectTransform.anchorMin = new Vector2(1f, 1f);
                rectTransform.anchorMax = new Vector2(1f, 1f);

                // Optionally set offset to zero
                rectTransform.anchoredPosition = Vector2.zero;
            }
            else
            {
                Debug.LogError("Profile card is missing RectTransform component");
            }
        }

        private void HandleProfileCreated(string profileId)
        {
            if (!gameObject.activeInHierarchy) return;

            Debug.Log($"HandleProfileCreated called for profile: {profileId}");

            var profiles = profileManager.GetAllProfiles();
            var newProfile = profiles.FirstOrDefault(p => p.ProfileId == profileId);

            if (newProfile != null)
            {
                // Instead of reloading all profiles, just add the new one
                CreateProfileCard(newProfile);

                // Find the card we just created
                var card = instantiatedCards.LastOrDefault();
                if (card != null && card.activeInHierarchy)
                {
                    Debug.Log($"Starting animation for new profile card: {newProfile.DisplayName}");
                    StartCoroutine(AnimateProfileCard(card));
                }
                else
                {
                    Debug.LogError("Failed to find or animate new profile card");
                }
            }
        }

        private IEnumerator AnimateProfileCard(GameObject selectedCard)
        {
            isTransitioning = true;

            if (!gameObject.activeInHierarchy || selectedCard == null)
            {
                Debug.LogError("Cannot animate: GameObject inactive or card is null");
                isTransitioning = false;
                yield break;
            }

            Debug.Log($"Starting profile card animation for {selectedCard.name}");

            // Hide other cards
            foreach (var card in instantiatedCards)
            {
                if (card != null && card != selectedCard)
                {
                    card.SetActive(false);
                }
            }

            // Hide create profile card
            if (createProfileCard != null && createProfileCard.gameObject != null)
            {
                createProfileCard.gameObject.SetActive(false);
            }

            // Reparent the selected card to the main canvas
            selectedCard.transform.SetParent(mainCanvas.transform, false);

            // Get the RectTransform
            RectTransform rectTransform = selectedCard.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                Debug.LogError("Selected card does not have a RectTransform.");
                isTransitioning = false;
                yield break;
            }

            // Cache initial values
            Vector2 startAnchoredPosition = rectTransform.anchoredPosition;
            Vector3 startScale = rectTransform.localScale;

            // Target position and scale
            Vector2 targetAnchoredPosition = Vector2.zero; // Top-right corner
            Vector3 targetScale = new Vector3(0.7f, 0.7f, 0.7f);

            float elapsedTime = 0f;
            while (elapsedTime < transitionDuration && selectedCard != null)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / transitionDuration);
                float smoothT = t * t * (3f - 2f * t);

                rectTransform.anchoredPosition = Vector2.Lerp(startAnchoredPosition, targetAnchoredPosition, smoothT);
                rectTransform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);

                yield return null;
            }

            // Ensure final position and scale
            if (selectedCard != null)
            {
                rectTransform.anchoredPosition = targetAnchoredPosition;
                rectTransform.localScale = targetScale;

                // Keep the card active
                selectedCard.SetActive(true);

                // Store the selected card reference if needed
                currentActiveCard = selectedCard;

                Debug.Log("Profile card animation completed successfully");
            }

            // Hide the profile selection panel
            if (profileSelectionPanel != null)
            {
                profileSelectionPanel.SetActive(false);
            }

            yield return FadeInMainMenuButtons();
            isTransitioning = false;
        }


        private Vector3 GetCanvasPosition(Vector2 screenPosition)
        {
            Vector3 worldPosition;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                mainCanvas.GetComponent<RectTransform>(),
                screenPosition,
                mainCanvas.worldCamera,
                out worldPosition);
            return worldPosition;
        }

        private void HandleProfileSelection(GameObject card, ProfileData profile)
        {
            if (isTransitioning || !gameObject.activeInHierarchy) return;

            isTransitioning = true;
            StartCoroutine(TransitionToMainMenu(card, profile));
        }

        private IEnumerator TransitionToMainMenu(GameObject selectedCard, ProfileData profile)
        {
            // Switch active profile in ProfileManager
            profileManager.SwitchActiveProfile(profile.ProfileId);

            // Proceed with UI animation

            // Hide other cards
            foreach (var card in instantiatedCards)
            {
                if (card != selectedCard)
                    card.SetActive(false);
            }

            if (createProfileCard != null)
                createProfileCard.gameObject.SetActive(false);

            // Animate selected card
            Vector3 startPosition = selectedCard.transform.position;
            Vector3 startScale = selectedCard.transform.localScale;

            float elapsedTime = 0f;
            while (elapsedTime < transitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / transitionDuration;
                float smoothT = t * t * (3f - 2f * t);

                selectedCard.transform.position = Vector3.Lerp(startPosition, cornerPosition, smoothT);
                selectedCard.transform.localScale = Vector3.Lerp(startScale, cornerScale, smoothT);

                yield return null;
            }

            // Show main menu buttons
            yield return StartCoroutine(FadeInMainMenuButtons());

            isTransitioning = false;
        }

        private void UpdateUIForProfileSelection(ProfileData profile)
        {
            // Update UI elements without animation
            mainMenuButtonsGroup.gameObject.SetActive(true);
            mainMenuButtonsGroup.alpha = 1f;
            mainMenuButtonsGroup.interactable = true;

            // Change game state
            GameEvents.OnGameStateChanged.Invoke(GameState.MainMenu);
        }

        private IEnumerator FadeInMainMenuButtons()
        {
            float duration = 0.3f;
            float elapsedTime = 0f;

            mainMenuButtonsGroup.gameObject.SetActive(true);

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                mainMenuButtonsGroup.alpha = elapsedTime / duration;
                yield return null;
            }

            mainMenuButtonsGroup.alpha = 1f;
            mainMenuButtonsGroup.interactable = true;

            GameEvents.OnGameStateChanged.Invoke(GameState.MainMenu);
        }

        private void HandleAuthStateChanged(AuthenticationState state)
        {
            UpdateUIState();
        }

        private void HandleProfileSwitched(string profileId)
        {
            if (isTransitioning) return;
            UpdateUIState();
        }

        private void UpdateUIState()
        {
            if (isTransitioning) return;

            UpdateHeaderInfo();

            // Additional UI updates based on authentication state
            if (createProfileCard != null)
                createProfileCard.gameObject.SetActive(authManager.IsSignedIn);
        }


        private void HandleProfileDeletion(ProfileData profile)
        {
            if (profile == null) return;

            // If this is the active profile, handle accordingly
            if (profileManager.ActiveProfile?.ProfileId == profile.ProfileId)
            {
                // Handle active profile deletion, e.g., switch to another profile or sign out
                profileManager.DeleteProfile(profile.ProfileId);
                profileManager.ClearAllProfiles();
                authManager.SignOut();
            }
            else
            {
                // Delete the profile
                profileManager.DeleteProfile(profile.ProfileId);
            }

            LoadProfiles();
            UpdateUIState();
        }

        private void HandleBackButton()
        {
            if (isTransitioning) return;
            StartCoroutine(TransitionBackToAuth());
        }

        private IEnumerator TransitionBackToAuth()
        {
            isTransitioning = true;

            float duration = 0.3f;
            float elapsed = 0f;

            var containerCanvasGroup = profilesContainer.GetComponent<CanvasGroup>();
            if (containerCanvasGroup == null)
                containerCanvasGroup = profilesContainer.gameObject.AddComponent<CanvasGroup>();

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                containerCanvasGroup.alpha = 1 - (elapsed / duration);
                yield return null;
            }

            profileSelectionPanel.SetActive(false);
            authManager.SignOut();

            var authPanel = GameObject.Find("AuthPanel");
            if (authPanel != null)
            {
                authPanel.SetActive(true);
                GameEvents.OnAuthenticationRequested.Invoke();
            }

            isTransitioning = false;
        }

        private void ClearExistingProfiles()
        {
            var cardsToRemove = new List<GameObject>();

            foreach (var card in instantiatedCards)
            {
                if (card != null)
                {
                    cardsToRemove.Add(card);
                }
            }

            foreach (var card in cardsToRemove)
            {
                instantiatedCards.Remove(card);
                Destroy(card);
            }

            instantiatedCards.Clear();
            Debug.Log($"Cleared {cardsToRemove.Count} profile cards");
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();

            backButton.onClick.RemoveListener(HandleBackButton);
        }
    }
}
