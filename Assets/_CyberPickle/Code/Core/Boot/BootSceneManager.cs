using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using CyberPickle.Core.Management;

namespace CyberPickle.Core.Boot
{
    public class BootSceneManager : Manager<BootSceneManager>
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup logoCanvasGroup;
        [SerializeField] private TextMeshProUGUI companyNameText;
        [SerializeField] private Image loadingBarFill;
        [SerializeField] private TextMeshProUGUI loadingText;

        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 1.5f;
        [SerializeField] private float displayDuration = 2.0f;
        [SerializeField] private float fadeOutDuration = 1.5f;

        [Header("Scene Settings")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private float minimumLoadingTime = 3.0f;
        [SerializeField] private bool waitForInput = true; // Add this to make input wait optional

        private float startTime;

        protected override void OnManagerAwake()
        {
            // Ensure canvas group starts invisible
            if (logoCanvasGroup != null)
                logoCanvasGroup.alpha = 0f;

            if (loadingBarFill != null)
                loadingBarFill.fillAmount = 0f;

            startTime = Time.time;
            StartCoroutine(BootSequence());
        }

        private IEnumerator BootSequence()
        {
            // Initial setup
            UpdateLoadingText("Initializing...");
            yield return StartCoroutine(FadeInLogo());

            // Core systems initialization
            yield return StartCoroutine(InitializeSystems());

            // Ensure minimum loading time
            float elapsedTime = Time.time - startTime;
            if (elapsedTime < minimumLoadingTime)
            {
                yield return new WaitForSeconds(minimumLoadingTime - elapsedTime);
            }

            // Display completion
            UpdateLoadingText("Press Any Key to Continue");

            // Wait for display duration
            yield return new WaitForSeconds(displayDuration);

            // Wait for input if enabled
            if (waitForInput)
            {
                yield return StartCoroutine(WaitForAnyKey());
            }

            // Transition out
            yield return StartCoroutine(FadeOutLogo());

            // Load main menu
            LoadMainMenu();
        }

        private IEnumerator WaitForAnyKey()
        {
            bool keyPressed = false;
            while (!keyPressed)
            {
                // Check for any key press or mouse click
                if (UnityEngine.Input.anyKeyDown || UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.GetMouseButtonDown(1))
                {
                    keyPressed = true;
                }
                yield return null;
            }
        }

        private IEnumerator InitializeSystems()
        {
            float currentProgress = 0f;
            float targetProgress = 0.2f;

            // Audio System
            UpdateLoadingText("Initializing Audio System...");
            yield return StartCoroutine(UpdateProgressBar(currentProgress, targetProgress));
            currentProgress = targetProgress;
            targetProgress = 0.4f;

            // Save System
            UpdateLoadingText("Initializing Save System...");
            yield return StartCoroutine(UpdateProgressBar(currentProgress, targetProgress));
            currentProgress = targetProgress;
            targetProgress = 0.6f;

            // Input System
            UpdateLoadingText("Initializing Input System...");
            yield return StartCoroutine(UpdateProgressBar(currentProgress, targetProgress));
            currentProgress = targetProgress;
            targetProgress = 0.8f;

            // Game Systems
            UpdateLoadingText("Initializing Game Systems...");
            yield return StartCoroutine(UpdateProgressBar(currentProgress, targetProgress));
            currentProgress = targetProgress;
            targetProgress = 1f;

            // Final Setup
            UpdateLoadingText("Completing Setup...");
            yield return StartCoroutine(UpdateProgressBar(currentProgress, targetProgress));
        }

        private IEnumerator FadeInLogo()
        {
            float elapsed = 0f;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = elapsed / fadeInDuration;

                if (logoCanvasGroup != null)
                    logoCanvasGroup.alpha = Mathf.Lerp(0f, 1f, normalizedTime);

                yield return null;
            }

            if (logoCanvasGroup != null)
                logoCanvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOutLogo()
        {
            float elapsed = 0f;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = elapsed / fadeOutDuration;

                if (logoCanvasGroup != null)
                    logoCanvasGroup.alpha = Mathf.Lerp(1f, 0f, normalizedTime);

                yield return null;
            }

            if (logoCanvasGroup != null)
                logoCanvasGroup.alpha = 0f;
        }

        private IEnumerator UpdateProgressBar(float from, float to)
        {
            float elapsed = 0f;
            float duration = 0.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = elapsed / duration;

                if (loadingBarFill != null)
                    loadingBarFill.fillAmount = Mathf.Lerp(from, to, normalizedTime);

                yield return null;
            }

            if (loadingBarFill != null)
                loadingBarFill.fillAmount = to;
        }

        private void UpdateLoadingText(string text)
        {
            if (loadingText != null)
                loadingText.text = text;
        }

        private void LoadMainMenu()
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}