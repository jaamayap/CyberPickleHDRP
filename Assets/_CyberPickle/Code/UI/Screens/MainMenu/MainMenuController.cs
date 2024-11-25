// File: Assets/Code/UI/Screens/MainMenu/MainMenuController.cs
// Namespace: CyberPickle.UI.Screens.MainMenu
//
// Purpose: Controls the main menu flow and transitions to authentication
// Handles the "Press Any Button" state and triggers authentication process via events

using UnityEngine;
using TMPro;
using CyberPickle.Core.Events;
using CyberPickle.Core.States;
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
        [SerializeField] private Vector2 titleMainMenuPosition = new Vector2(0, 500);
        [SerializeField] private Vector2 titleProfileSelectPosition = new Vector2(0, 954);

        private bool isWaitingForInput = true;
        private CanvasGroup pressButtonCanvasGroup;

        private void Awake()
        {
            Debug.Log("[MainMenuController] Awake");
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
            Debug.Log("[MainMenuController] Start completed");
        }

        private void InitializeUI()
        {
            Debug.Log("[MainMenuController] Initializing UI");
            pressButtonCanvasGroup.alpha = 1f;
            isWaitingForInput = true;

            // Hide all panels initially
            if (authPanel != null)
            {
                authPanel.SetActive(false);
                Debug.Log("[MainMenuController] Auth panel hidden");
            }
            if (profileSelectionPanel != null)
            {
                profileSelectionPanel.SetActive(false);
                Debug.Log("[MainMenuController] Profile selection panel hidden");
            }
            if (mainMenuButtonsPanel != null)
            {
                mainMenuButtonsPanel.SetActive(false);
                Debug.Log("[MainMenuController] Main menu buttons panel hidden");
            }
        }

        private void SubscribeToEvents()
        {
            GameEvents.OnProfileLoadRequested.AddListener(HandleProfileLoadRequested);
            GameEvents.OnGameStateChanged.AddListener(HandleGameStateChanged);
            Debug.Log("[MainMenuController] Events subscribed");
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
            Debug.Log("[MainMenuController] Main menu input detected");
            isWaitingForInput = false;
            StartCoroutine(TransitionToAuth());
        }

        private IEnumerator TransitionToAuth()
        {
            Debug.Log("[MainMenuController] Starting transition to auth");
            GameEvents.OnUIAnimationStarted.Invoke();

            float elapsedTime = 0f;
            while (elapsedTime < fadeOutDuration)
            {
                elapsedTime += Time.deltaTime;
                pressButtonCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeOutDuration);
                yield return null;
            }

            pressAnyButtonText.gameObject.SetActive(false);
            if (authPanel != null)
            {
                authPanel.SetActive(true);
                Debug.Log("[MainMenuController] Auth panel activated");
            }

            Debug.Log("[MainMenuController] Auth transition complete");
            GameEvents.OnUIAnimationCompleted.Invoke();
            GameEvents.OnAuthenticationRequested.Invoke();
        }

        private void HandleProfileLoadRequested()
        {
            Debug.Log("[MainMenuController] Profile load requested");
            StartCoroutine(TransitionToPanels(authPanel, profileSelectionPanel));
            StartCoroutine(AnimateTitlePosition(titleProfileSelectPosition));
        }

        private void HandleGameStateChanged(GameState newState)
        {
            Debug.Log($"[MainMenuController] Game state changed to: {newState}");
            switch (newState)
            {
                case GameState.MainMenu:
                    StartCoroutine(AnimateTitlePosition(titleMainMenuPosition));
                    StartCoroutine(TransitionToPanels(profileSelectionPanel, mainMenuButtonsPanel));
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

            Debug.Log($"[MainMenuController] Starting title animation to position: {targetPosition}");
            Vector2 startPosition = titleRectTransform.anchoredPosition;
            float elapsedTime = 0f;

            while (elapsedTime < titleAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / titleAnimationDuration;
                float smoothProgress = Mathf.SmoothStep(0, 1, progress);
                titleRectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, smoothProgress);
                yield return null;
            }

            titleRectTransform.anchoredPosition = targetPosition;
            Debug.Log("[MainMenuController] Title animation completed");
        }
        //ASDF
        private IEnumerator TransitionToPanels(GameObject panelToHide, GameObject panelToShow)
        {
            Debug.Log($"[MainMenuController] Starting panel transition - Hide: {panelToHide?.name}, Show: {panelToShow?.name}");
            GameEvents.OnUIAnimationStarted.Invoke();

            if (panelToHide != null)
            {
                CanvasGroup hideCanvasGroup = panelToHide.GetComponent<CanvasGroup>();
                if (hideCanvasGroup == null)
                {
                    hideCanvasGroup = panelToHide.AddComponent<CanvasGroup>();
                    Debug.Log($"[MainMenuController] Added CanvasGroup to {panelToHide.name}");
                }

                float elapsedTime = 0f;
                while (elapsedTime < panelTransitionDuration)
                {
                    elapsedTime += Time.deltaTime;
                    hideCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / panelTransitionDuration);
                    yield return null;
                }

                panelToHide.SetActive(false);
                Debug.Log($"[MainMenuController] Panel {panelToHide.name} hidden");
            }

            if (panelToShow != null)
            {
                CanvasGroup showCanvasGroup = panelToShow.GetComponent<CanvasGroup>();
                if (showCanvasGroup == null)
                {
                    showCanvasGroup = panelToShow.AddComponent<CanvasGroup>();
                    Debug.Log($"[MainMenuController] Added CanvasGroup to {panelToShow.name}");
                }

                showCanvasGroup.alpha = 0f;
                panelToShow.SetActive(true);

                // Important: Set these immediately when showing the panel
                showCanvasGroup.interactable = true;
                showCanvasGroup.blocksRaycasts = true;

                float elapsedTime = 0f;
                while (elapsedTime < panelTransitionDuration)
                {
                    elapsedTime += Time.deltaTime;
                    showCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / panelTransitionDuration);
                    yield return null;
                }

                showCanvasGroup.alpha = 1f;
                Debug.Log($"[MainMenuController] Panel {panelToShow.name} shown and activated");
            }

            GameEvents.OnUIAnimationCompleted.Invoke();
            Debug.Log("[MainMenuController] Panel transition complete");
        }

        private void HideAllPanels()
        {
            Debug.Log("[MainMenuController] Hiding all panels");
            if (pressAnyButtonText != null) pressAnyButtonText.gameObject.SetActive(false);
            if (authPanel != null) authPanel.SetActive(false);
            if (profileSelectionPanel != null) profileSelectionPanel.SetActive(false);
            if (mainMenuButtonsPanel != null) mainMenuButtonsPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            GameEvents.OnProfileLoadRequested.RemoveListener(HandleProfileLoadRequested);
            GameEvents.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
            StopAllCoroutines();
            Debug.Log("[MainMenuController] Cleaned up and destroyed");
        }
    }
}