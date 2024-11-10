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
using System;
using System.Threading.Tasks;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Services.Authentication.Flow.Commands;
using CyberPickle.Core.Events;
using CyberPickle.Core.Services.Authentication.Flow;
using CyberPickle.Core.States;

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
        [SerializeField] private float statusMessageDuration = 3f;

        private bool isProcessing;
        private ProfileManager profileManager;
        private AuthenticationManager authManager;
        private AuthenticationFlowManager flowManager;
        private Coroutine statusMessageCoroutine;

        private void Awake()
        {
            
            profileManager = ProfileManager.Instance;
            authManager = AuthenticationManager.Instance;
            flowManager = AuthenticationFlowManager.Instance;

            InitializeComponents();
            SetInitialState();
            SubscribeToEvents();
            Debug.Log($"[CreateProfileCardController]: Awake");
        }

        public void ResetCard()
        {
            // Cancel any ongoing transitions or coroutines
            if (statusMessageCoroutine != null)
                StopCoroutine(statusMessageCoroutine);

            // Reset processing flag
            isProcessing = false;

            // Clear input field
            if (inputField != null)
                inputField.text = string.Empty;

            // Reset status
            if (statusText != null)
                statusText.text = string.Empty;

            // Return to initial state
            SetInitialState();
            Debug.Log("CreateProfileControler : Create card reset");
        }

        private void InitializeComponents()
        {
            InitializeCanvasGroups();
            InitializeButtons();
            InitializeInputField();
        }

        private void InitializeCanvasGroups()
        {
            if (cardContentCanvasGroup == null && cardContent != null)
                cardContentCanvasGroup = cardContent.GetComponent<CanvasGroup>() ?? cardContent.gameObject.AddComponent<CanvasGroup>();

            if (terminalCanvasGroup == null && terminalInterface != null)
                terminalCanvasGroup = terminalInterface.GetComponent<CanvasGroup>() ?? terminalInterface.gameObject.AddComponent<CanvasGroup>();
        }

        private void InitializeButtons()
        {
            // Setup card button
            Button cardButton = cardContent.GetComponent<Button>() ?? cardContent.gameObject.AddComponent<Button>();
            cardButton.onClick.AddListener(StartProfileCreation);

            // Setup other buttons
            if (confirmButton != null)
                confirmButton.onClick.AddListener(HandleProfileConfirmationAsync);

            if (cancelButton != null)
                cancelButton.onClick.AddListener(CancelProfileCreation);
        }

        private void InitializeInputField()
        {
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

        private void SetInitialState()
        {
            // Show card content
            SetCardContentActive(true, 1f);
            if (cardContent != null)
            {
                cardContent.SetActive(true);
                var button = cardContent.GetComponent<Button>();
                if (button != null)
                    button.interactable = true;
            }

            // Hide and reset terminal interface
            SetTerminalInterfaceActive(false, 0f);
            if (terminalInterface != null)
            {
                terminalInterface.SetActive(false);
                if (inputField != null)
                {
                    inputField.text = string.Empty;
                    inputField.DeactivateInputField();
                }
            }

            // Hide validation section
            if (validationSection != null)
                validationSection.SetActive(false);
        }

        public void StartProfileCreation()
        {
            if (isProcessing) return;
            isProcessing = true;

            if (terminalInterface != null)
            {
                terminalInterface.SetActive(true);
                if (terminalCanvasGroup != null)
                    terminalCanvasGroup.alpha = 0f;
            }

            StartCoroutine(TransitionToTerminal());
        }

        private async void HandleProfileConfirmationAsync()
        {
            if (string.IsNullOrWhiteSpace(inputField.text)) return;

            string displayName = inputField.text.Trim();
            SetInteractable(false);
            UpdateStatus("Creating profile...");

            try
            {
                var command = new CreateProfileCommand(profileManager, displayName);
                await flowManager.ExecuteCommand(command);

                // Success will be handled in HandleProfileCreated
            }
            catch (Exception ex)
            {
                SetInteractable(true);
                ShowTemporaryStatus($"Failed to create profile: {ex.Message}", isError: true);
                Debug.LogError($"[CreateProfileCard] Error creating profile: {ex.Message}");
            }
        }

        private void HandleProfileCreated(string profileId)
        {
            if (!gameObject.activeInHierarchy) return;

            ShowTemporaryStatus("Profile created successfully!");

            ResetCard();
            
            // Transition to main menu state just like when selecting a profile
            GameEvents.OnGameStateChanged.Invoke(GameState.MainMenu);
        }

        private void ShowTemporaryStatus(string message, bool isError = false)
        {
            if (statusMessageCoroutine != null)
                StopCoroutine(statusMessageCoroutine);

            statusMessageCoroutine = StartCoroutine(ShowStatusMessageRoutine(message, isError));
        }

        private IEnumerator ShowStatusMessageRoutine(string message, bool isError)
        {
            UpdateStatus(message);
            if (statusText != null)
                statusText.color = isError ? Color.red : Color.white;

            yield return new WaitForSeconds(statusMessageDuration);

            if (statusText != null)
            {
                statusText.text = "";
                statusText.color = Color.white;
            }
        }

        private void ValidateInput(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                SetConfirmButtonState(false);
                return;
            }

            bool isValid = true;
            isValid &= input.Length >= 3 && input.Length <= 20;
            isValid &= System.Text.RegularExpressions.Regex.IsMatch(input, "^[a-zA-Z0-9_]+$");

            SetConfirmButtonState(isValid);
            UpdateValidationMessages(input);
        }

        private IEnumerator TransitionToTerminal()
        {
            Debug.Log("[CreateProfileCard] Starting transition to terminal interface");

            float elapsedTime = 0f;
            while (elapsedTime < transitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / transitionDuration;

                SetCardContentActive(false, 1 - normalizedTime);
                SetTerminalInterfaceActive(true, normalizedTime);

                yield return null;
            }

            if (cardContent != null)
                cardContent.SetActive(false);

            FinalizeTransition();
            isProcessing = false;
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
            if (cardContentCanvasGroup == null) return;

            cardContentCanvasGroup.alpha = alpha;
            cardContentCanvasGroup.interactable = active;
            cardContentCanvasGroup.blocksRaycasts = active;
        }

        private void SetTerminalInterfaceActive(bool active, float alpha = 1f)
        {
            if (terminalCanvasGroup == null) return;

            terminalCanvasGroup.alpha = alpha;
            terminalCanvasGroup.interactable = active;
            terminalCanvasGroup.blocksRaycasts = active;
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

            if (validationMessages.Length > 0)
                validationMessages[0].text = input.Length >= 3 && input.Length <= 20 ?
                    "Length: Valid" : "Length: Must be 3-20 characters";

            if (validationMessages.Length > 1)
                validationMessages[1].text = System.Text.RegularExpressions.Regex.IsMatch(input, "^[a-zA-Z0-9_]+$") ?
                    "Characters: Valid" : "Characters: Use only letters, numbers, and underscore";
        }

        private void OnDestroy()
        {
            if (statusMessageCoroutine != null)
                StopCoroutine(statusMessageCoroutine);

            UnsubscribeFromEvents();
            CleanupEventListeners();
        }

        private void UnsubscribeFromEvents()
        {
            if (profileManager != null)
            {
                profileManager.UnsubscribeFromNewProfileCreated(HandleProfileCreated);
            }
        }

        private void CleanupEventListeners()
        {
            if (cardContent != null)
            {
                var button = cardContent.GetComponent<Button>();
                if (button != null)
                    button.onClick.RemoveListener(StartProfileCreation);
            }

            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(HandleProfileConfirmationAsync);

            if (cancelButton != null)
                cancelButton.onClick.RemoveListener(CancelProfileCreation);

            if (inputField != null)
                inputField.onValueChanged.RemoveListener(ValidateInput);
        }
    }
}
