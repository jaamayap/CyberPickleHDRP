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
            }
        }


        private void CalculatePositions()
        {
            if (mainCanvas == null) return;

            RectTransform canvasRect = mainCanvas.GetComponent<RectTransform>();
            minimizedPosition = new Vector2(
                canvasRect.rect.width / 2f - cornerPadding,
                canvasRect.rect.height / 2f - cornerPadding
            );

            // Expanded position will be centered
            expandedPosition = Vector2.zero;
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

        public void TransitionToState(ProfileCardState newState)
        {
            if (currentState == newState || isTransitioning) return;

            StartCoroutine(TransitionRoutine(newState));
        }

        private IEnumerator TransitionRoutine(ProfileCardState targetState)
        {
            isTransitioning = true;
            var previousState = currentState;
            currentState = ProfileCardState.Transitioning;

            GameEvents.OnProfileCardStateChanged.Invoke(ProfileCardState.Transitioning);
            GameEvents.OnProfileCardInteractionEnabled.Invoke(false);

            // Store the current card's position
            Vector3 startPosition = currentCardInstance != null ?
                currentCardInstance.transform.position : Vector3.zero;

            // Create and set up the target state card
            GameObject targetCard = null;
            RectTransform targetRect = null;
            Vector2 targetPosition;

            switch (targetState)
            {
                case ProfileCardState.Minimized:
                    Debug.Log("[ProfileCardManager] Creating minimal card in transition");
                    targetCard = Instantiate(minimalCardPrefab, mainCanvas.transform);
                    targetRect = targetCard.GetComponent<RectTransform>();
                    SetupCardTransform(targetRect, minimizedPosition);
                    var minimalCard = targetCard.GetComponent<ProfileCardMinimal>();
                    if (minimalCard != null && currentProfileData != null)
                    {
                        minimalCard.UpdateDisplay(currentProfileData);
                    }
                    targetPosition = new Vector2(-cornerPadding, -cornerPadding); // Top-right with padding
                    Debug.Log($"[ProfileCardManager] Minimal card created in transition. Active: {targetCard.activeSelf}");
                    break;

                case ProfileCardState.Expanded:
                    Debug.Log("[ProfileCardManager] Creating expanded card in transition");
                    targetCard = Instantiate(expandedCardPrefab, mainCanvas.transform);
                    targetRect = targetCard.GetComponent<RectTransform>();
                    targetRect.anchorMin = new Vector2(0.5f, 0.5f);
                    targetRect.anchorMax = new Vector2(0.5f, 0.5f);
                    targetRect.pivot = new Vector2(0.5f, 0.5f);
                    var expandedCard = targetCard.GetComponent<ProfileCardExpanded>();
                    if (expandedCard != null && currentProfileData != null)
                    {
                        expandedCard.UpdateDisplay(currentProfileData);
                    }
                    targetPosition = Vector2.zero; // Center
                    Debug.Log($"[ProfileCardManager] Expanded card created in transition. Active: {targetCard.activeSelf}");
                    break;

                default:
                    isTransitioning = false;
                    yield break;
            }

            if (targetCard != null)
            {
                // Set initial position
                targetRect.position = startPosition;

                // Perform transition
                float elapsedTime = 0f;
                while (elapsedTime < transitionDuration)
                {
                    if (!targetCard.activeSelf)
                    {
                        Debug.LogWarning("[ProfileCardManager] Card became inactive during transition! Reactivating...");
                        targetCard.SetActive(true);
                    }
                    elapsedTime += Time.deltaTime;
                    float t = elapsedTime / transitionDuration;
                    float smoothT = t * t * (3f - 2f * t);

                    targetRect.anchoredPosition = Vector2.Lerp(
                        targetRect.anchoredPosition,
                        targetPosition,
                        smoothT
                    );

                    yield return null;
                }

                // Ensure final position
                targetRect.anchoredPosition = targetPosition;

                // Cleanup old card and set new one
                if (currentCardInstance != null && currentCardInstance != targetCard)
                {
                    Debug.Log("[ProfileCardManager] Destroying old card instance");
                    Destroy(currentCardInstance);
                }
                currentCardInstance = targetCard;
                Debug.Log($"[ProfileCardManager] Transition complete. Card active state: {currentCardInstance.activeSelf}");
            }

            currentState = targetState;
            isTransitioning = false;

            GameEvents.OnProfileCardStateChanged.Invoke(currentState);
            GameEvents.OnProfileCardTransitionComplete.Invoke();
            GameEvents.OnProfileCardInteractionEnabled.Invoke(true);
            Debug.Log($"[ProfileCardManager] Final state - {currentState}. Card active: {currentCardInstance?.activeSelf}");
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

        private void HandleGameStateChanged(Core.States.GameState newState)
        {
            // Handle any specific state requirements
            // For example, hiding the card during certain game states
            UpdateCardDisplay();
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
