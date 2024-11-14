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

        [Header("Title Animation")]
        [SerializeField] private RectTransform titleRectTransform;
        [SerializeField] private float titleAnimationDuration = 0.5f;
        [SerializeField] private Vector2 titleMainMenuPosition = new Vector2(0, 500); // Your default position
        [SerializeField] private Vector2 titleProfileSelectPosition = new Vector2(0, 954); // Your desired position

        private bool isWaitingForInput = true;
        private CanvasGroup pressButtonCanvasGroup;

        private void Awake()
        {
            Debug.Log("MainMenuControllerAwake");
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
            Debug.Log("MainMenuControllerStart");
        }

        private void InitializeUI()
        {
            Debug.Log("MainMenuController: InitializeUI Called");
            // Ensure proper initial state
            pressButtonCanvasGroup.alpha = 1f;
            isWaitingForInput = true;

            // Hide all panels initially
            if (authPanel != null) authPanel.SetActive(false);
            if (profileSelectionPanel != null) profileSelectionPanel.SetActive(false);
            if (mainMenuButtonsPanel != null) mainMenuButtonsPanel.SetActive(false);
            Debug.Log("MainMenuController: PanelsHidden");
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
            // Signal that UI transition is starting
            GameEvents.OnUIAnimationStarted.Invoke();

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

            // Signal that UI transition is complete
            Debug.Log("[MainMenu] UI transition complete");
            GameEvents.OnUIAnimationCompleted.Invoke();

            // Trigger authentication start event
            GameEvents.OnAuthenticationRequested.Invoke();
        }

        private void HandleProfileLoadRequested()
        {
            Debug.Log("[MainMenu] Profile load requested, transitioning to profile selection panel");
            StartCoroutine(TransitionToPanels(authPanel, profileSelectionPanel));
            StartCoroutine(AnimateTitlePosition(titleProfileSelectPosition));
        }

        private void HandleGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.MainMenu:

                    StartCoroutine(AnimateTitlePosition(titleMainMenuPosition));
                    StartCoroutine(TransitionToPanels(profileSelectionPanel, mainMenuButtonsPanel));
                    
                    if (mainMenuButtonsPanel != null)
                    {
                        var canvasGroup = mainMenuButtonsPanel.GetComponent<CanvasGroup>();
                        if (canvasGroup != null)
                        {
                            canvasGroup.interactable = true;
                            canvasGroup.blocksRaycasts = true;
                        }
                    }
                    break;
                case GameState.ProfileSelection:
                    StartCoroutine(AnimateTitlePosition(titleProfileSelectPosition));
                    StartCoroutine(TransitionToPanels(mainMenuButtonsPanel, profileSelectionPanel));
                    
                    break;
                case GameState.CharacterSelect:
                case GameState.EquipmentSelect:
                case GameState.LevelSelect:
                    HideAllPanels();
                    break;
            }
        }

        private IEnumerator AnimateTitlePosition(Vector2 targetPosition)
        {
            if (titleRectTransform == null) yield break;

            Vector2 startPosition = titleRectTransform.anchoredPosition;
            float elapsedTime = 0f;

            while (elapsedTime < titleAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / titleAnimationDuration;

                // Use smooth step for more polished animation
                float smoothProgress = Mathf.SmoothStep(0, 1, progress);

                titleRectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, smoothProgress);

                yield return null;
            }

            // Ensure we reach the exact target position
            titleRectTransform.anchoredPosition = targetPosition;
        }
        private IEnumerator TransitionToPanels(GameObject panelToHide, GameObject panelToShow)
        {
            // Signal start of transition
            GameEvents.OnUIAnimationStarted.Invoke();

            if (panelToHide != null)
            {
                // Existing fade out code...
                CanvasGroup hideCanvasGroup = panelToHide.GetComponent<CanvasGroup>();
                if (hideCanvasGroup == null)
                {
                    hideCanvasGroup = panelToHide.AddComponent<CanvasGroup>();
                }

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
                // Existing fade in code...
                CanvasGroup showCanvasGroup = panelToShow.GetComponent<CanvasGroup>();
                if (showCanvasGroup == null)
                {
                    showCanvasGroup = panelToShow.AddComponent<CanvasGroup>();
                }

                showCanvasGroup.alpha = 0f;
                panelToShow.SetActive(true);

                float elapsedTime = 0f;
                while (elapsedTime < panelTransitionDuration)
                {
                    elapsedTime += Time.deltaTime;
                    showCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / panelTransitionDuration);
                    yield return null;
                }

                showCanvasGroup.alpha = 1f;

                // Signal completion of transition and which panel is now active
                Debug.Log($"[MainMenu] Panel transition complete: {panelToShow.name} now active");
                GameEvents.OnUIAnimationCompleted.Invoke();
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