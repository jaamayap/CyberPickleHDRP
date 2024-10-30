// File: Assets/Code/UI/Windows/Auth/AuthPanelController.cs
// Namespace: CyberPickle.UI.Windows.Auth
//
// Purpose: Controls the authentication panel UI and process.
// Responds to authentication events and manages the terminal display.
//
// Created: 2024-01-13
// Updated: 2024-01-14

using UnityEngine;
using TMPro;
using UnityEngine.UI;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Events;
using System.Collections;

namespace CyberPickle.UI.Windows.Auth
{
    public class AuthPanelController : MonoBehaviour
    {
        [Header("Panel References")]
        [SerializeField] private GameObject terminalWindowPanel;
        [SerializeField] private TextMeshProUGUI debugConsole;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Loading Indicator")]
        [SerializeField] private Slider loadingSlider;
        [SerializeField] private float loadingAnimationSpeed = 1f;

        [Header("Animation Settings")]
        [SerializeField] private float typewriterSpeed = 0.05f;
        [SerializeField] private float messageDisplayDuration = 2f;
        [SerializeField] private float panelFadeInDuration = 0.5f;

        private AuthenticationManager authManager;
        private ProfileManager profileManager;
        private bool isInitialized;
        private Coroutine currentTypewriterCoroutine;
        private Coroutine loadingCoroutine;
        private CanvasGroup panelCanvasGroup;

        private void Awake()
        {
            authManager = AuthenticationManager.Instance;
            profileManager = ProfileManager.Instance;
            SetupCanvasGroup();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void SetupCanvasGroup()
        {
            panelCanvasGroup = GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            panelCanvasGroup.alpha = 0f;
        }

        private void SubscribeToEvents()
        {
            GameEvents.OnAuthenticationRequested.AddListener(InitializeAuthentication);

            if (authManager != null)
            {
                authManager.SubscribeToAuthenticationCompleted(OnAuthenticationCompleted);
                authManager.SubscribeToAuthenticationFailed(OnAuthenticationFailed);
            }

            if (profileManager != null)
            {
                profileManager.SubscribeToProfileSwitched(OnProfileSwitched);
            }
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnAuthenticationRequested.RemoveListener(InitializeAuthentication);

            if (authManager != null)
            {
                authManager.UnsubscribeFromAuthenticationCompleted(OnAuthenticationCompleted);
                authManager.UnsubscribeFromAuthenticationFailed(OnAuthenticationFailed);
            }

            if (profileManager != null)
            {
                profileManager.UnsubscribeFromProfileSwitched(OnProfileSwitched);
            }
        }

        public void InitializeAuthentication()
        {
            if (!isInitialized)
            {
                StartCoroutine(InitializationSequence());
                isInitialized = true;
            }
        }

        private IEnumerator InitializationSequence()
        {
            // Reset state
            isInitialized = false;
            ClearConsole();
            loadingSlider.value = 0f;

            // Fade in the panel
            yield return StartCoroutine(FadeInPanel());

            // Initial boot sequence
            yield return StartCoroutine(TypewriterEffect(debugConsole, "> Initializing terminal..."));
            yield return new WaitForSeconds(messageDisplayDuration);

            yield return StartCoroutine(TypewriterEffect(debugConsole, "> Checking authentication service..."));

            // Start loading animation
            if (loadingCoroutine != null) StopCoroutine(loadingCoroutine);
            loadingCoroutine = StartCoroutine(AnimateLoading());

            // Begin authentication
            StartAuthentication();
        }

        private IEnumerator FadeInPanel()
        {
            float elapsedTime = 0f;
            while (elapsedTime < panelFadeInDuration)
            {
                elapsedTime += Time.deltaTime;
                panelCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / panelFadeInDuration);
                yield return null;
            }
            panelCanvasGroup.alpha = 1f;
        }

        private void StartAuthentication()
        {
            UpdateStatus("Authenticating...");
            _ = authManager.SignInAnonymouslyAsync();
        }

        private IEnumerator AnimateLoading()
        {
            while (true)
            {
                loadingSlider.value = Mathf.PingPong(Time.time * loadingAnimationSpeed, 1f);
                yield return null;
            }
        }

        private IEnumerator TypewriterEffect(TextMeshProUGUI textComponent, string message)
        {
            if (currentTypewriterCoroutine != null)
            {
                StopCoroutine(currentTypewriterCoroutine);
            }

            // Store the existing text
            string existingText = textComponent.text;

            // Add a new line if there's existing text
            if (!string.IsNullOrEmpty(existingText) && !existingText.EndsWith("\n"))
            {
                existingText += "\n";
            }

            // Build the new line character by character
            string currentMessage = "";
            foreach (char c in message)
            {
                currentMessage += c;
                textComponent.text = existingText + currentMessage;
                yield return new WaitForSeconds(typewriterSpeed);
            }

            // Add a new line at the end for the next message
            textComponent.text += "\n";
        }

        #region Event Handlers

        private void OnAuthenticationCompleted(string playerId)
        {
            if (loadingCoroutine != null) StopCoroutine(loadingCoroutine);
            loadingSlider.value = 1f;

            StartCoroutine(TypewriterEffect(debugConsole, $"> Authentication successful. PlayerID: {playerId}"));
            UpdateStatus("Authentication Complete");

            // Trigger profile selection panel or next step
            GameEvents.OnProfileLoadRequested.Invoke();
        }

        private void OnAuthenticationFailed(string error)
        {
            if (loadingCoroutine != null) StopCoroutine(loadingCoroutine);
            loadingSlider.value = 0f;

            StartCoroutine(TypewriterEffect(debugConsole, $"> Authentication failed: {error}"));
            UpdateStatus("Authentication Failed");
        }

        private void OnProfileSwitched(string profileId)
        {
            StartCoroutine(TypewriterEffect(debugConsole, $"> Profile switched to: {profileId}"));
        }

        #endregion

        private void UpdateStatus(string status)
        {
            if (statusText != null)
                statusText.text = $"> {status}";
        }

        private void ClearConsole()
        {
            if (debugConsole != null) debugConsole.text = "";
            if (statusText != null) statusText.text = "";
            if (resultText != null) resultText.text = "";
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }
    }
}
