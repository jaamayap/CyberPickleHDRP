// File: Assets/Code/UI/Screens/MainMenu/CreateProfileCardController.cs
//
// Purpose: Controls the create profile card UI and transitions between card content and terminal interface.
// This controller focuses solely on UI handling and delegates profile creation to the ProfileManager.
//
// Created: 2024-01-13
// Updated: 2024-01-14

using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Events;
using System;

namespace CyberPickle.UI.Screens.MainMenu
{
    public class CreateProfileCardController : MonoBehaviour
    {
        [Header("Card Content")]
        [SerializeField] private GameObject cardContent;
        [SerializeField] private CanvasGroup cardContentCanvasGroup;

        [Header("Terminal Interface")]
        [SerializeField] private GameObject terminalInterface;
        [SerializeField] private CanvasGroup terminalCanvasGroup;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Validation")]
        [SerializeField] private GameObject validationSection;
        [SerializeField] private TextMeshProUGUI[] validationMessages;

        [Header("Animation Settings")]
        [SerializeField] private float transitionDuration = 0.5f;

        private bool isProcessing;
        private ProfileManager profileManager;
        private AuthenticationManager authManager;
        private string pendingDisplayName;

        private void Awake()
        {
            profileManager = ProfileManager.Instance;
            authManager = AuthenticationManager.Instance;
            InitializeComponents();
            SetInitialState();
            SubscribeToEvents();
        }

        private void InitializeComponents()
        {
            // Setup CanvasGroups
            if (cardContentCanvasGroup == null && cardContent != null)
                cardContentCanvasGroup = cardContent.GetComponent<CanvasGroup>() ?? cardContent.gameObject.AddComponent<CanvasGroup>();

            if (terminalCanvasGroup == null && terminalInterface != null)
                terminalCanvasGroup = terminalInterface.GetComponent<CanvasGroup>() ?? terminalInterface.gameObject.AddComponent<CanvasGroup>();

            // Setup button listeners
            Button cardButton = cardContent.GetComponent<Button>();
            if (cardButton == null)
                cardButton = cardContent.gameObject.AddComponent<Button>();

            cardButton.onClick.AddListener(StartProfileCreation);

            if (confirmButton != null)
                confirmButton.onClick.AddListener(HandleProfileConfirmation);

            if (cancelButton != null)
                cancelButton.onClick.AddListener(CancelProfileCreation);

            if (inputField != null)
                inputField.onValueChanged.AddListener(ValidateInput);
        }

        private void SubscribeToEvents()
        {
            if (profileManager != null)
            {
                profileManager.SubscribeToNewProfileCreated(HandleProfileCreated);
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (profileManager != null)
            {
                profileManager.UnsubscribeFromNewProfileCreated(HandleProfileCreated);
            }
        }

        private void SetInitialState()
        {
            // Ensure CardContent is visible and active
            SetCardContentActive(true);
            if (cardContent != null)
                cardContent.SetActive(true);

            // Ensure Terminal Interface is hidden initially
            SetTerminalInterfaceActive(false);
            if (terminalInterface != null)
                terminalInterface.SetActive(false);

            if (validationSection != null)
                validationSection.SetActive(false);
        }

        public void StartProfileCreation()
        {
            if (isProcessing) return;
            isProcessing = true;

            // Activate terminal interface but with 0 alpha
            if (terminalInterface != null)
            {
                terminalInterface.SetActive(true);
                if (terminalCanvasGroup != null)
                    terminalCanvasGroup.alpha = 0f;
            }

            StartCoroutine(TransitionToTerminal());
        }

        private IEnumerator TransitionToTerminal()
        {
            Debug.Log("Starting transition to terminal interface");

            float elapsedTime = 0f;
            while (elapsedTime < transitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / transitionDuration;

                SetCardContentActive(false, 1 - normalizedTime);
                SetTerminalInterfaceActive(true, normalizedTime);

                yield return null;
            }

            // Ensure final states
            if (cardContent != null)
                cardContent.SetActive(false);

            FinalizeTransition();

            isProcessing = false;
            Debug.Log("Terminal interface transition completed");
        }

        private void FinalizeTransition()
        {
            SetCardContentActive(false);
            SetTerminalInterfaceActive(true);

            if (inputField != null)
            {
                inputField.text = string.Empty;
                inputField.Select();
                inputField.ActivateInputField();
            }

            if (validationSection != null)
                validationSection.SetActive(true);
        }

        private void ValidateInput(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                SetConfirmButtonState(false);
                return;
            }

            bool isValid = true;

            // Length check (3-20 characters)
            isValid &= input.Length >= 3 && input.Length <= 20;

            // Character check (letters, numbers, underscore)
            isValid &= System.Text.RegularExpressions.Regex.IsMatch(input, "^[a-zA-Z0-9_]+$");

            SetConfirmButtonState(isValid);
            UpdateValidationMessages(input);
        }

        private void HandleProfileConfirmation()
        {
            if (string.IsNullOrWhiteSpace(inputField.text)) return;

            pendingDisplayName = inputField.text.Trim();

            // Disable input during profile creation
            SetInteractable(false);
            UpdateStatus("Creating profile...");

            try
            {
                // Generate profileId
                string profileId = $"{pendingDisplayName.ToLower()}_{DateTime.UtcNow.Ticks}";

                // Get playerId from AuthenticationManager
                string playerId = authManager.CurrentPlayerId;

                // Create the profile
                profileManager.CreateProfile(profileId, playerId, pendingDisplayName);

                // Success will be handled in HandleProfileCreated
            }
            catch (Exception ex)
            {
                SetInteractable(true);
                UpdateStatus("Failed to create profile. Please try again.");
                Debug.LogError($"Error creating profile: {ex.Message}");
            }
        }

        private void HandleProfileCreated(string profileId)
        {
            // Only process if we have a valid status text component
            if (statusText == null) return;

            UpdateStatus("Profile created successfully!");
            if (gameObject != null && gameObject.activeInHierarchy)
            {
                // Instead of immediately starting the coroutine, delay it slightly
                StartCoroutine(DelayedTransition());
            }
        }

        private IEnumerator DelayedTransition()
        {
            // Small delay to ensure all state changes are completed
            yield return new WaitForSeconds(0.1f);

            // Check again if the object is still active
            if (gameObject != null && gameObject.activeInHierarchy)
            {
                StartCoroutine(TransitionToCard());
            }
        }

        private void CancelProfileCreation()
        {
            StartCoroutine(TransitionToCard());
        }

        private IEnumerator TransitionToCard()
        {
            float elapsedTime = 0f;
            while (elapsedTime < transitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / transitionDuration;

                SetCardContentActive(true, normalizedTime);
                SetTerminalInterfaceActive(false, 1 - normalizedTime);

                yield return null;
            }

            SetInitialState();
            isProcessing = false;
        }

        private void SetCardContentActive(bool active, float alpha = 1f)
        {
            if (cardContentCanvasGroup != null)
            {
                cardContentCanvasGroup.alpha = alpha;
                cardContentCanvasGroup.interactable = active;
                cardContentCanvasGroup.blocksRaycasts = active;
            }
        }

        private void SetTerminalInterfaceActive(bool active, float alpha = 1f)
        {
            if (terminalCanvasGroup != null)
            {
                terminalCanvasGroup.alpha = alpha;
                terminalCanvasGroup.interactable = active;
                terminalCanvasGroup.blocksRaycasts = active;
            }
        }

        private void SetConfirmButtonState(bool enabled)
        {
            if (confirmButton != null)
                confirmButton.interactable = enabled;
        }

        private void SetInteractable(bool interactable)
        {
            if (inputField != null)
                inputField.interactable = interactable;
            if (confirmButton != null)
                confirmButton.interactable = interactable;
            if (cancelButton != null)
                cancelButton.interactable = interactable;
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        private void UpdateValidationMessages(string input)
        {
            if (validationMessages == null || validationMessages.Length == 0)
                return;

            // Update validation messages based on current input
            if (validationMessages.Length > 0)
                validationMessages[0].text = input.Length >= 3 && input.Length <= 20 ?
                    "Length: Valid" : "Length: Must be 3-20 characters";

            if (validationMessages.Length > 1)
                validationMessages[1].text = System.Text.RegularExpressions.Regex.IsMatch(input, "^[a-zA-Z0-9_]+$") ?
                    "Characters: Valid" : "Characters: Use only letters, numbers, and underscore";
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();

            if (cardContent != null)
            {
                var button = cardContent.GetComponent<Button>();
                if (button != null)
                    button.onClick.RemoveListener(StartProfileCreation);
            }

            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(HandleProfileConfirmation);

            if (cancelButton != null)
                cancelButton.onClick.RemoveListener(CancelProfileCreation);

            if (inputField != null)
                inputField.onValueChanged.RemoveListener(ValidateInput);
        }
    }
}
