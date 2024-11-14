// File: Assets/Code/UI/Components/ProfileCard/ProfileCardManager.cs
//
// Purpose: Manages the profile card UI component states and transitions across different game screens.
// Maintains the current profile display state and handles interactions with the card.
//
// Created: 2024-02-11

using UnityEngine;
using System.Collections;
using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;
using CyberPickle.Core.Events;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Core.GameFlow.States.ProfileCard;
using CyberPickle.Core.States;

namespace CyberPickle.UI.Components.ProfileCard
{
    public class ProfileCardManager : Manager<ProfileCardManager>, IInitializable
    {
        [Header("References")]
        [SerializeField] private GameObject minimalCardPrefab;
        [SerializeField] private GameObject expandedCardPrefab;
        [SerializeField] private Canvas mainCanvas;

        [Header("Animation Settings")]
        [SerializeField] private float transitionDuration = 0.3f;
        [SerializeField] private float cornerPadding = 20f;

        private ProfileCardState currentState = ProfileCardState.Hidden;
        private GameObject currentCardInstance;
        private ProfileData currentProfileData;
        private bool isTransitioning;
        private Vector2 minimizedPosition;
        private Vector2 expandedPosition;
        private bool isInitialized;

        public ProfileCardState CurrentState => currentState;
        public bool IsTransitioning => isTransitioning;
        public ProfileData CurrentProfileData => currentProfileData;

        public void Initialize()
        {
            if (isInitialized) return;

            ValidateReferences();
            SubscribeToEvents();
            CalculatePositions();

            isInitialized = true;
            Debug.Log("[ProfileCardManager] Initialized");
        }

        private void ValidateReferences()
        {
            if (minimalCardPrefab == null)
                Debug.LogError("[ProfileCardManager] Minimal card prefab is not assigned!");
            if (expandedCardPrefab == null)
                Debug.LogError("[ProfileCardManager] Expanded card prefab is not assigned!");
            if (mainCanvas == null)
                mainCanvas = FindObjectOfType<Canvas>();
            if (mainCanvas == null)
                Debug.LogError("[ProfileCardManager] No main canvas found in scene!");
        }

        private void SubscribeToEvents()
        {
            GameEvents.OnProfileCardClicked.AddListener(HandleCardClicked);
            GameEvents.OnProfileSelected.AddListener(HandleProfileSelected);
            GameEvents.OnGameStateChanged.AddListener(HandleGameStateChanged);
            GameEvents.OnProfileLoadRequested.AddListener(HandleProfileLoadRequested);
        }
        private void HandleProfileLoadRequested()
        {
            // When returning to profile selection, hide the card
            if (currentCardInstance != null)
            {
                Debug.Log("[ProfileCardManager] Profile load requested, hiding card");
                currentCardInstance.SetActive(false);
                currentState = ProfileCardState.Hidden;
                Debug.Log($"[ProfileCardManagerHandleprofileloadrequested] Card active hidden");
            }
        }


        private void CalculatePositions()
        {
            if (mainCanvas == null) return;

            RectTransform canvasRect = mainCanvas.GetComponent<RectTransform>();

            // Top-right corner positioning
            float padding = 20f;
            minimizedPosition = new Vector2(-padding, -padding);

            // Expanded position also in top-right
            float expandedPadding = 40f;
            expandedPosition = new Vector2(-expandedPadding, -expandedPadding);
        }

        public void SetProfile(ProfileData profileData)
        {
            if (profileData == null)
            {
                Debug.LogError("[ProfileCardManager] Attempted to set null profile data");
                return;
            }

            currentProfileData = profileData;
            UpdateCardDisplay();
        }

        private void UpdateCardDisplay()
        {
            if (currentProfileData == null || isTransitioning) return;

            switch (currentState)
            {
                case ProfileCardState.Minimized:
                    UpdateMinimalCard();
                    break;
                case ProfileCardState.Expanded:
                    UpdateExpandedCard();
                    break;
            }
        }

        private void UpdateMinimalCard()
        {
            if (currentCardInstance == null ||
                currentCardInstance.GetComponent<ProfileCardMinimal>() == null)
            {
                CreateMinimalCard();
            }

            var minimalCard = currentCardInstance.GetComponent<ProfileCardMinimal>();
            if (minimalCard != null)
            {
                minimalCard.UpdateDisplay(currentProfileData);
            }
        }

        private void UpdateExpandedCard()
        {
            if (currentCardInstance == null ||
                currentCardInstance.GetComponent<ProfileCardExpanded>() == null)
            {
                CreateExpandedCard();
            }

            var expandedCard = currentCardInstance.GetComponent<ProfileCardExpanded>();
            if (expandedCard != null)
            {
                expandedCard.UpdateDisplay(currentProfileData);
            }
        }

        private void CreateMinimalCard()
        {
            Debug.Log("[ProfileCardManager] Creating minimal card...");

            if (currentCardInstance != null)
            {
                Debug.Log("[ProfileCardManager] Destroying previous card instance");
                Destroy(currentCardInstance);
            }

            currentCardInstance = Instantiate(minimalCardPrefab, mainCanvas.transform);
            Debug.Log($"[ProfileCardManager] Minimal card created. Active state: {currentCardInstance.activeSelf}");

            var cardRect = currentCardInstance.GetComponent<RectTransform>();
            SetupCardTransform(cardRect, minimizedPosition);

            // Ensure the card is active
            currentCardInstance.SetActive(true);
            Debug.Log($"[ProfileCardManager] After setup. Active state: {currentCardInstance.activeSelf}");

            var minimalCard = currentCardInstance.GetComponent<ProfileCardMinimal>();
            if (minimalCard != null && currentProfileData != null)
            {
                minimalCard.UpdateDisplay(currentProfileData);
                Debug.Log($"[ProfileCardManager] Updated minimal card display for profile: {currentProfileData.DisplayName}");
            }
            else
            {
                Debug.LogError("[ProfileCardManager] Failed to initialize minimal card display");
            }
        }

        private void CreateExpandedCard()
        {
            if (currentCardInstance != null)
                Destroy(currentCardInstance);

            currentCardInstance = Instantiate(expandedCardPrefab, mainCanvas.transform);
            var cardRect = currentCardInstance.GetComponent<RectTransform>();

            // For expanded card, we want it centered
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;

            // Update the card display immediately
            var expandedCard = currentCardInstance.GetComponent<ProfileCardExpanded>();
            if (expandedCard != null && currentProfileData != null)
            {
                expandedCard.UpdateDisplay(currentProfileData);
            }
            else
            {
                Debug.LogError("[ProfileCardManager] Failed to initialize expanded card display");
            }
        }

        private void SetupCardTransform(RectTransform cardRect, Vector2 position)
        {
            if (cardRect == null) return;

            // Set the anchors to top-right corner
            cardRect.anchorMin = Vector2.one;
            cardRect.anchorMax = Vector2.one;
            cardRect.pivot = Vector2.one;

            // When using top-right anchoring, negative values move in from the corner
            float padding = 20f;
            cardRect.anchoredPosition = new Vector2(-padding, -padding);
        }

        public void TransitionToState(ProfileCardState targetState)
        {
            if (currentState == targetState || isTransitioning) return;
            StartCoroutine(TransitionRoutine(targetState));
        }

        private IEnumerator TransitionRoutine(ProfileCardState targetState)
        {
            isTransitioning = true;
            currentState = ProfileCardState.Transitioning;
            GameEvents.OnProfileCardStateChanged.Invoke(ProfileCardState.Transitioning);
            GameEvents.OnProfileCardInteractionEnabled.Invoke(false);

            Vector3 startPosition = currentCardInstance != null ?
                currentCardInstance.transform.position : Vector3.zero;

            // Create and set up the target state card
            GameObject targetCard = null;
            RectTransform targetRect = null;

            switch (targetState)
            {
                case ProfileCardState.Minimized:
                    targetCard = Instantiate(minimalCardPrefab, mainCanvas.transform);
                    targetRect = targetCard.GetComponent<RectTransform>();
                    SetupMinimizedCardPosition(targetRect);
                    var minimalCard = targetCard.GetComponent<ProfileCardMinimal>();
                    if (minimalCard != null && currentProfileData != null)
                    {
                        minimalCard.UpdateDisplay(currentProfileData);
                    }
                    break;

                case ProfileCardState.Expanded:
                    targetCard = Instantiate(expandedCardPrefab, mainCanvas.transform);
                    targetRect = targetCard.GetComponent<RectTransform>();
                    SetupExpandedCardPosition(targetRect);
                    var expandedCard = targetCard.GetComponent<ProfileCardExpanded>();
                    if (expandedCard != null && currentProfileData != null)
                    {
                        expandedCard.UpdateDisplay(currentProfileData);
                    }
                    break;
            }

            if (targetCard != null)
            {
                // Set initial position to match current card
                targetRect.position = startPosition;

                // Get target position based on state
                Vector2 targetPosition = targetState == ProfileCardState.Minimized ?
                    minimizedPosition : expandedPosition;

                // Perform transition
                float elapsedTime = 0f;
                while (elapsedTime < transitionDuration)
                {
                    elapsedTime += Time.deltaTime;
                    float t = elapsedTime / transitionDuration;
                    float smoothT = t * t * (3f - 2f * t); // Smooth interpolation

                    targetRect.anchoredPosition = Vector2.Lerp(
                        targetRect.anchoredPosition,
                        targetPosition,
                        smoothT
                    );

                    yield return null;
                }

                // Ensure final position
                targetRect.anchoredPosition = targetPosition;

                // Cleanup old card
                if (currentCardInstance != null && currentCardInstance != targetCard)
                {
                    Destroy(currentCardInstance);
                }
                currentCardInstance = targetCard;
            }

            currentState = targetState;
            isTransitioning = false;

            GameEvents.OnProfileCardStateChanged.Invoke(currentState);
            GameEvents.OnProfileCardTransitionComplete.Invoke();
            GameEvents.OnProfileCardInteractionEnabled.Invoke(true);
        }

        private void SetupMinimizedCardPosition(RectTransform cardRect)
        {
            // Set the anchors to top-right corner
            cardRect.anchorMin = Vector2.one;
            cardRect.anchorMax = Vector2.one;
            cardRect.pivot = Vector2.one;

            // Position from top-right corner with padding
            float padding = 20f;
            minimizedPosition = new Vector2(-padding, -padding);
        }

        private void SetupExpandedCardPosition(RectTransform cardRect)
        {
            // For expanded, keep it in the top-right but with more space
            cardRect.anchorMin = Vector2.one;
            cardRect.anchorMax = Vector2.one;
            cardRect.pivot = new Vector2(1f, 1f);

            // Calculate expanded position (still in top-right, but larger)
            float xPadding = 40f;  // Increased padding for expanded state
            float yPadding = 40f;
            expandedPosition = new Vector2(-xPadding, -yPadding);
        }
        private void HandleCardClicked()
        {
            if (isTransitioning) return;

            switch (currentState)
            {
                case ProfileCardState.Minimized:
                    TransitionToState(ProfileCardState.Expanded);
                    break;
                case ProfileCardState.Expanded:
                    TransitionToState(ProfileCardState.Minimized);
                    break;
            }
        }

        private void HandleProfileSelected(string profileId)
        {
            var profileManager = ProfileManager.Instance;
            if (profileManager != null)
            {
                var profile = profileManager.GetProfile(profileId);
                if (profile != null)
                {
                    SetProfile(profile);
                    if (currentState == ProfileCardState.Hidden)
                    {
                        TransitionToState(ProfileCardState.Minimized);
                    }
                }
            }
        }

        private void HandleGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.MainMenu:
                    if (currentProfileData != null && currentState == ProfileCardState.Hidden)
                    {
                        TransitionToState(ProfileCardState.Minimized);
                    }
                    break;

                case GameState.ProfileSelection:
                    // Hide the card completely
                    if (currentCardInstance != null)
                    {
                        Destroy(currentCardInstance);
                        currentCardInstance = null;
                    }
                    currentState = ProfileCardState.Hidden;
                    Debug.Log($"[ProfileCardManagerHandleGameStateChanged] Card active hidden");
                    break;
            }
        }

        protected override void OnManagerDestroyed()
        {
            GameEvents.OnProfileCardClicked.RemoveListener(HandleCardClicked);
            GameEvents.OnProfileSelected.RemoveListener(HandleProfileSelected);
            GameEvents.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
            GameEvents.OnProfileLoadRequested.RemoveListener(HandleProfileLoadRequested);

            if (currentCardInstance != null)
                Destroy(currentCardInstance);
        }
    }
}
