// File: Assets/Scripts/UI/Screens/MainMenu/MainMenuController.cs
// Namespace: CyberPickle.UI.Screens.MainMenu
//
// Purpose: Controls the main menu flow and transitions to authentication
// Handles the "Press Any Button" state and triggers authentication process via events

using UnityEngine;
using TMPro;
using CyberPickle.Core.Events;
using CyberPickle.UI.Effects;

namespace CyberPickle.UI.Screens.MainMenu
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI pressAnyButtonText;
        [SerializeField] private GameObject authPanel;
        [SerializeField] private float fadeOutDuration = 0.5f;

        private bool isWaitingForInput = true;
        private CanvasGroup pressButtonCanvasGroup;

        private void Awake()
        {
            // Get or add CanvasGroup for fade effect
            pressButtonCanvasGroup = pressAnyButtonText.gameObject.GetComponent<CanvasGroup>();
            if (pressButtonCanvasGroup == null)
            {
                pressButtonCanvasGroup = pressAnyButtonText.gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void Start()
        {
            // Ensure proper initial state
            if (authPanel != null)
            {
                authPanel.SetActive(false);
            }
            pressButtonCanvasGroup.alpha = 1f;
            isWaitingForInput = true;
        }

        private void Update()
        {
            if (isWaitingForInput && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
            {
                HandleMainMenuInput();
            }
        }

        private void HandleMainMenuInput()
        {
            isWaitingForInput = false;
            StartCoroutine(TransitionToAuth());
        }

        private System.Collections.IEnumerator TransitionToAuth()
        {
            // Fade out "Press Any Button" text
            float elapsedTime = 0f;
            while (elapsedTime < fadeOutDuration)
            {
                elapsedTime += Time.deltaTime;
                pressButtonCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeOutDuration);
                yield return null;
            }

            // Disable the text and show auth panel
            pressAnyButtonText.gameObject.SetActive(false);
            if (authPanel != null)
            {
                authPanel.SetActive(true);
            }

            // Trigger authentication start event
            GameEvents.OnAuthenticationRequested.Invoke();
        }

        private void OnDestroy()
        {
            // Clean up if needed
            StopAllCoroutines();
        }
    }
}