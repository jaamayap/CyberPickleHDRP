// File: Assets/Scripts/UI/Screens/MainMenu/ProfileSelectionController.cs
// Purpose: Handles the profile selection interface and transitions to main menu
// Manages profile card creation, selection, and animation

// File: Assets/Scripts/UI/Screens/MainMenu/ProfileSelectionController.cs
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

        private AuthenticationManager authManager;
        private List<GameObject> instantiatedCards = new List<GameObject>();
        private CreateProfileCardController createProfileCard;
        private bool isTransitioning;

        private void Awake()
        {
            authManager = AuthenticationManager.Instance;
            mainMenuButtonsGroup.alpha = 0f;
            mainMenuButtonsGroup.interactable = false;
        }

        private void Start()
        {
            InitializeUI();
            LoadProfiles();
        }

        private void InitializeUI()
        {
            UpdateHeaderInfo();
            backButton.onClick.AddListener(HandleBackButton);

            // Initialize create profile card
            GameObject createCardObject = Instantiate(createProfileCardPrefab, profilesContainer);
            createProfileCard = createCardObject.GetComponent<CreateProfileCardController>();

            if (createProfileCard != null)
            {
                createProfileCard.OnProfileCreated += HandleProfileCreated;
            }
        }

        private void UpdateHeaderInfo()
        {
            playerIdText.text = $"ID: #{authManager.CurrentPlayerId}";
            statusText.text = "SYSTEM STATUS: ONLINE";
        }

        private void LoadProfiles()
        {
            ClearExistingProfiles();

            // Load existing profiles
            var profiles = authManager.GetAllProfiles();
            foreach (var profile in profiles)
            {
                CreateProfileCard(profile);
            }
        }

        private void CreateProfileCard(ProfileData profile)
        {
            GameObject card = Instantiate(profileCardPrefab, profilesContainer);
            ConfigureProfileCard(card, profile);
            instantiatedCards.Add(card);
        }

        private void ConfigureProfileCard(GameObject card, ProfileData profile)
        {
            // Configure card UI elements
            var nameText = card.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null) nameText.text = profile.DisplayName;

            // Add selection button listener
            var selectButton = card.GetComponentInChildren<Button>();
            if (selectButton != null)
            {
                selectButton.onClick.AddListener(() => HandleProfileSelection(card, profile));
            }

            // Configure delete button
            var deleteButton = card.GetComponentsInChildren<Button>()
                .FirstOrDefault(b => b.gameObject.name.Contains("Delete"));
            if (deleteButton != null)
            {
                deleteButton.onClick.AddListener(() => HandleProfileDeletion(profile));
            }
        }

        private void HandleProfileCreated(string profileId, string displayName)
        {
            // Refresh the profile list
            LoadProfiles();

            // Find and select the newly created profile
            var profiles = authManager.GetAllProfiles();
            var newProfile = profiles.FirstOrDefault(p => p.ProfileId == profileId);
            if (newProfile != null)
            {
                var card = instantiatedCards.Find(c =>
                    c.GetComponentInChildren<TextMeshProUGUI>().text == displayName);
                if (card != null)
                {
                    HandleProfileSelection(card, newProfile);
                }
            }
        }

        private void HandleProfileSelection(GameObject card, ProfileData profile)
        {
            if (isTransitioning) return;

            isTransitioning = true;
            StartCoroutine(TransitionToMainMenu(card, profile));
        }

        private IEnumerator TransitionToMainMenu(GameObject selectedCard, ProfileData profile)
        {
            // Set the profile as active
            yield return authManager.SwitchProfileAsync(profile.ProfileId);

            // Hide other cards
            foreach (var card in instantiatedCards)
            {
                if (card != selectedCard)
                {
                    card.SetActive(false);
                }
            }

            createProfileCard.gameObject.SetActive(false);

            // Animate selected card to corner
            Vector3 cornerPosition = new Vector3(800f, 400f, 0f); // Adjust these values
            Vector3 cornerScale = new Vector3(0.7f, 0.7f, 0.7f);  // Adjust these values

            float elapsedTime = 0f;
            float transitionDuration = 0.5f;
            Vector3 startPosition = selectedCard.transform.position;
            Vector3 startScale = selectedCard.transform.localScale;

            while (elapsedTime < transitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / transitionDuration;

                // Smooth step for nicer animation
                float smoothT = t * t * (3f - 2f * t);

                selectedCard.transform.position = Vector3.Lerp(startPosition, cornerPosition, smoothT);
                selectedCard.transform.localScale = Vector3.Lerp(startScale, cornerScale, smoothT);

                yield return null;
            }

            // Show main menu buttons
            StartCoroutine(FadeInMainMenuButtons());

            isTransitioning = false;
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

            // Change game state to MainMenu
            GameEvents.OnGameStateChanged.Invoke(GameState.MainMenu);
        }

        private void HandleProfileDeletion(ProfileData profile)
        {
            if (profile == null) return;

            // If this is the active profile, sign out first
            if (authManager.CurrentProfile?.ProfileId == profile.ProfileId)
            {
                authManager.SignOut();
            }

            // Remove from profile container
            ProfileContainer container = ProfileContainer.Load();
            container.RemoveProfile(profile.ProfileId);
            container.SaveProfiles();

            // Reload profiles
            LoadProfiles();
        }

        private void HandleBackButton()
        {
            if (isTransitioning) return;

            StartCoroutine(TransitionBackToAuth());
        }

        private IEnumerator TransitionBackToAuth()
        {
            isTransitioning = true;

            // Fade out profile cards
            float duration = 0.3f;
            float elapsed = 0f;

            // Create a CanvasGroup for the profile container if it doesn't exist
            CanvasGroup containerCanvasGroup = profilesContainer.GetComponent<CanvasGroup>();
            if (containerCanvasGroup == null)
            {
                containerCanvasGroup = profilesContainer.gameObject.AddComponent<CanvasGroup>();
            }

            // Fade out profiles
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                containerCanvasGroup.alpha = 1 - (elapsed / duration);
                yield return null;
            }

            // Hide profile selection panel
            profileSelectionPanel.SetActive(false);

            // Sign out of current profile if one is active
            authManager.SignOut();

            // Show auth panel again
            GameObject authPanel = GameObject.Find("AuthPanel");
            if (authPanel != null)
            {
                authPanel.SetActive(true);
                // Request new authentication
                GameEvents.OnAuthenticationRequested.Invoke();
            }

            isTransitioning = false;
        }

        private void ClearExistingProfiles()
        {
            foreach (var card in instantiatedCards)
            {
                Destroy(card);
            }
            instantiatedCards.Clear();
        }

        private void OnDestroy()
        {
            if (createProfileCard != null)
            {
                createProfileCard.OnProfileCreated -= HandleProfileCreated;
            }
            backButton.onClick.RemoveListener(HandleBackButton);
        }
    }
}