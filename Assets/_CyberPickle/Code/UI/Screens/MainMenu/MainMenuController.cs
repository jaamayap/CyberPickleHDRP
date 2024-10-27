// File: Assets/Code/UI/Screens/MainMenu/MainMenuController.cs
// Namespace: CyberPickle.UI.Screens.MainMenu
//
// Purpose: Controls the main menu flow and transitions to authentication
// Handles the "Press Any Button" state and triggers authentication process via events

using UnityEngine;
using TMPro;
using CyberPickle.Core.Events;
using CyberPickle.Core.States;
using CyberPickle.UI.Effects;
using System.Collections;

namespace CyberPickle.UI.Screens.MainMenu
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI pressAnyButtonText;
        [SerializeField] private GameObject authPanel;
        [SerializeField] private GameObject profileSelectionPanel;
        [SerializeField] private GameObject mainMenuButtonsPanel;

        [Header("Animation")]
        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private float panelTransitionDuration = 0.3f;

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
            InitializeUI();
            SubscribeToEvents();
        }

        private void InitializeUI()
        {
            // Ensure proper initial state
            pressButtonCanvasGroup.alpha = 1f;
            isWaitingForInput = true;

            // Hide all panels initially
            if (authPanel != null) authPanel.SetActive(false);
            if (profileSelectionPanel != null) profileSelectionPanel.SetActive(false);
            if (mainMenuButtonsPanel != null) mainMenuButtonsPanel.SetActive(false);
        }

        private void SubscribeToEvents()
        {
            GameEvents.OnProfileLoadRequested.AddListener(HandleProfileLoadRequested);
            GameEvents.OnGameStateChanged.AddListener(HandleGameStateChanged);
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

        private IEnumerator TransitionToAuth()
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

        private void HandleProfileLoadRequested()
        {
            StartCoroutine(TransitionToPanels(authPanel, profileSelectionPanel));
        }

        private void HandleGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.MainMenu:
                    StartCoroutine(TransitionToPanels(profileSelectionPanel, mainMenuButtonsPanel));
                    break;

                case GameState.CharacterSelect:
                case GameState.EquipmentSelect:
                case GameState.LevelSelect:
                    HideAllPanels();
                    break;
            }
        }

        private IEnumerator TransitionToPanels(GameObject panelToHide, GameObject panelToShow)
        {
            if (panelToHide != null)
            {
                // Get or add CanvasGroup to the panel to hide
                CanvasGroup hideCanvasGroup = panelToHide.GetComponent<CanvasGroup>();
                if (hideCanvasGroup == null)
                {
                    hideCanvasGroup = panelToHide.AddComponent<CanvasGroup>();
                }

                // Fade out
                float elapsedTime = 0f;
                while (elapsedTime < panelTransitionDuration)
                {
                    elapsedTime += Time.deltaTime;
                    hideCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / panelTransitionDuration);
                    yield return null;
                }

                panelToHide.SetActive(false);
            }

            if (panelToShow != null)
            {
                // Get or add CanvasGroup to the panel to show
                CanvasGroup showCanvasGroup = panelToShow.GetComponent<CanvasGroup>();
                if (showCanvasGroup == null)
                {
                    showCanvasGroup = panelToShow.AddComponent<CanvasGroup>();
                }

                // Show and fade in
                panelToShow.SetActive(true);
                showCanvasGroup.alpha = 0f;

                float elapsedTime = 0f;
                while (elapsedTime < panelTransitionDuration)
                {
                    elapsedTime += Time.deltaTime;
                    showCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / panelTransitionDuration);
                    yield return null;
                }
            }
        }

        private void HideAllPanels()
        {
            if (pressAnyButtonText != null) pressAnyButtonText.gameObject.SetActive(false);
            if (authPanel != null) authPanel.SetActive(false);
            if (profileSelectionPanel != null) profileSelectionPanel.SetActive(false);
            if (mainMenuButtonsPanel != null) mainMenuButtonsPanel.SetActive(false);
        }

        // Public methods for button callbacks
        public void OnPlayButtonClicked()
        {
            GameEvents.OnGameStateChanged.Invoke(GameState.CharacterSelect);
        }

        public void OnOptionsButtonClicked()
        {
            // TODO: Implement options menu
            Debug.Log("Options clicked");
        }

        public void OnQuitButtonClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            // Clean up event subscriptions
            GameEvents.OnProfileLoadRequested.RemoveListener(HandleProfileLoadRequested);
            GameEvents.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
            StopAllCoroutines();
        }
    }
}