// File: Assets/Code/UI/Screens/MainMenu/CreateProfileCardController.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using CyberPickle.Core.Services.Authentication;
using System;
using CyberPickle.Core.Services.Authentication.Data;
using System.Linq;

namespace CyberPickle.UI.Screens.MainMenu
{
    public class CreateProfileCardController : MonoBehaviour
    {
        [Header("Card Content")]
        [SerializeField] private GameObject cardContent;
        [SerializeField] private TextMeshProUGUI plusIcon;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("Terminal Interface")]
        [SerializeField] private CanvasGroup terminalInterface;
        [SerializeField] private Image scanlineEffect;
        [SerializeField] private Image glitchOverlay;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Input Section")]
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Image blinkingCursor;

        [Header("Validation")]
        [SerializeField] private GameObject validationSection;
        [SerializeField] private TextMeshProUGUI[] validationTexts;
        [SerializeField] private Image[] validationIcons;

        [Header("Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        [Header("Effects")]
        [SerializeField] private TextMeshProUGUI compilationText;
        [SerializeField] private Image progressBar;

        [Header("Settings")]
        [SerializeField] private float typewriterSpeed = 0.05f;
        [SerializeField] private float validationCheckDelay = 0.5f;

        private AuthenticationManager authManager;
        private bool isProcessing;
        private Coroutine currentTypewriter;

        // Event to notify when profile is created
        public event Action<string, string> OnProfileCreated; // profileId, displayName

        private void Awake()
        {
            authManager = AuthenticationManager.Instance;
            InitializeUI();
        }

        private void InitializeUI()
        {
            // Hide terminal interface initially
            if (terminalInterface != null)
            {
                terminalInterface.alpha = 0;
                terminalInterface.gameObject.SetActive(false);
            }

            // Hide validation section
            if (validationSection != null)
            {
                validationSection.SetActive(false);
            }

            // Setup input field
            if (inputField != null)
            {
                inputField.onValueChanged.AddListener(OnInputChanged);
                inputField.gameObject.SetActive(false);
            }

            // Setup buttons
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmProfile);
                confirmButton.gameObject.SetActive(false);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancelProfile);
                cancelButton.gameObject.SetActive(false);
            }
        }

        public void StartProfileCreation()
        {
            if (isProcessing) return;
            isProcessing = true;

            StartCoroutine(ProfileCreationSequence());
        }

        private IEnumerator ProfileCreationSequence()
        {
            // Show terminal interface
            terminalInterface.gameObject.SetActive(true);

            // Fade in terminal interface
            float elapsed = 0;
            while (elapsed < 1f)
            {
                elapsed += Time.deltaTime;
                terminalInterface.alpha = elapsed;
                yield return null;
            }

            // Initial messages
            yield return TypewriterEffect(statusText, "INITIALIZING NEW AGENT PROFILE...");
            yield return new WaitForSeconds(0.5f);
            yield return TypewriterEffect(promptText, "ENTER CODENAME:");

            // Show input field
            inputField.gameObject.SetActive(true);
            inputField.ActivateInputField();

            isProcessing = false;
        }

        private void OnInputChanged(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            if (!validationSection.activeSelf)
            {
                validationSection.SetActive(true);
            }

            // Start validation checks
            StartCoroutine(ValidateInput(value));
        }

        private IEnumerator ValidateInput(string input)
        {
            // Length check
            validationTexts[0].text = "ANALYZING CODENAME LENGTH...";
            yield return new WaitForSeconds(validationCheckDelay);
            bool lengthValid = input.Length >= 3 && input.Length <= 20;
            UpdateValidation(0, lengthValid,
                lengthValid ? "LENGTH: VALID" : "LENGTH: INVALID (3-20 CHARS)");

            // Character check
            validationTexts[1].text = "VALIDATING CHARACTERS...";
            yield return new WaitForSeconds(validationCheckDelay);
            bool charsValid = System.Text.RegularExpressions.Regex.IsMatch(input, "^[a-zA-Z0-9_]+$");
            UpdateValidation(1, charsValid,
                charsValid ? "CHARACTERS: VALID" : "CHARACTERS: INVALID (A-Z, 0-9, _)");

            // Uniqueness check
            validationTexts[2].text = "CHECKING DATABASE...";
            yield return new WaitForSeconds(validationCheckDelay);
            bool isUnique = !authManager.GetAllProfiles().Any(p =>
                p.DisplayName.Equals(input, StringComparison.OrdinalIgnoreCase));
            UpdateValidation(2, isUnique,
                isUnique ? "DATABASE: UNIQUE" : "DATABASE: NAME EXISTS");

            // Show/hide confirm button based on all validations
            confirmButton.gameObject.SetActive(lengthValid && charsValid && isUnique);
        }

        private void UpdateValidation(int index, bool isValid, string message)
        {
            if (validationIcons != null && index < validationIcons.Length)
            {
                validationIcons[index].color = isValid ? Color.green : Color.red;
            }

            if (validationTexts != null && index < validationTexts.Length)
            {
                validationTexts[index].text = message;
            }
        }

        private void OnConfirmProfile()
        {
            if (isProcessing) return;
            isProcessing = true;

            StartCoroutine(CreateProfileSequence());
        }

        private IEnumerator CreateProfileSequence()
        {
            // Hide input and validation
            inputField.gameObject.SetActive(false);
            validationSection.SetActive(false);

            yield return TypewriterEffect(statusText, "COMPILING AGENT DATA...");

            string displayName = inputField.text.Trim();
            string profileId = $"{displayName.ToLower()}_{DateTime.UtcNow.Ticks}";

            bool success = true;
            string errorMessage = string.Empty;

            try
            {
                // Create profile
                var newProfile = new ProfileData(profileId, authManager.CurrentPlayerId);
                newProfile.UpdateDisplayName(displayName);

                // Notify listeners
                OnProfileCreated?.Invoke(profileId, displayName);
            }
            catch (Exception e)
            {
                success = false;
                errorMessage = e.Message;
                Debug.LogError($"Failed to create profile: {errorMessage}");
            }

            // Show appropriate message based on success
            yield return TypewriterEffect(statusText,
                success ? "PROFILE CREATION SUCCESSFUL" : "ERROR: PROFILE CREATION FAILED");

            yield return new WaitForSeconds(1f);
            ResetCard();
        }

        private void OnCancelProfile()
        {
            if (isProcessing) return;
            ResetCard();
        }

        private void ResetCard()
        {
            StopAllCoroutines();
            isProcessing = false;

            // Reset UI elements
            terminalInterface.gameObject.SetActive(false);
            validationSection.SetActive(false);
            inputField.text = "";
            inputField.gameObject.SetActive(false);
            confirmButton.gameObject.SetActive(false);
            cancelButton.gameObject.SetActive(false);

            // Reset card content
            cardContent.SetActive(true);
        }

        private IEnumerator TypewriterEffect(TextMeshProUGUI textComponent, string message)
        {
            if (currentTypewriter != null)
            {
                StopCoroutine(currentTypewriter);
            }

            textComponent.text = "";
            foreach (char c in message)
            {
                textComponent.text += c;
                yield return new WaitForSeconds(typewriterSpeed);
            }
        }

        private void OnDestroy()
        {
            // Clean up event listeners
            if (inputField != null)
            {
                inputField.onValueChanged.RemoveListener(OnInputChanged);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmProfile);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnCancelProfile);
            }
        }
    }
}