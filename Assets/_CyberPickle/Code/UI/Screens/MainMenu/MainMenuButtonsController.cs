using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CyberPickle.Core.Events;
using CyberPickle.Core.States;
using System.Collections;
using CyberPickle.Core;

namespace CyberPickle.UI.Screens.MainMenu
{
    /// <summary>
    /// Controls the main menu buttons behavior and animations.
    /// Assumes it's attached to the MainMenuButtonsPanel GameObject.
    /// </summary>
    public class MainMenuButtonsController : MonoBehaviour
    {
        [Header("Button References")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;

        private CanvasGroup buttonsPanelCanvasGroup;
        private bool isTransitioning = false;

        private void Awake()
        {
            Debug.Log("[MainMenuButtonsController] Awake called");
            ValidateReferences();
            InitializeButtonStates();
        }

        private void OnEnable()
        {
            Debug.Log("[MainMenuButtonsController] OnEnable called - Current game state: " + GameManager.Instance.CurrentState);
            SubscribeToEvents();
            SetupButtonListeners();

            // Force check current state
            if (GameManager.Instance.CurrentState == GameState.MainMenu)
            {
                Debug.Log("[MainMenuButtonsController] Already in MainMenu state, forcing button activation");
                ForceActivateButtons();
            }
        }

        private void ValidateReferences()
        {
            buttonsPanelCanvasGroup = GetComponent<CanvasGroup>();
            if (buttonsPanelCanvasGroup == null)
            {
                buttonsPanelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            Debug.Log($"[MainMenuButtonsController] Start Button: {(startButton != null ? "Found" : "Missing")}");
            Debug.Log($"[MainMenuButtonsController] Options Button: {(optionsButton != null ? "Found" : "Missing")}");
            Debug.Log($"[MainMenuButtonsController] Quit Button: {(quitButton != null ? "Found" : "Missing")}");

            if (buttonsPanelCanvasGroup != null)
            {
                Debug.Log($"[MainMenuButtonsController] CanvasGroup - interactable: {buttonsPanelCanvasGroup.interactable}, blocksRaycasts: {buttonsPanelCanvasGroup.blocksRaycasts}, alpha: {buttonsPanelCanvasGroup.alpha}");
            }
        }

        private void InitializeButtonStates()
        {
            Debug.Log("[MainMenuButtonsController] Initializing button states");
            SetButtonsInteractable(true); // Start with buttons interactable
            if (buttonsPanelCanvasGroup != null)
            {
                buttonsPanelCanvasGroup.alpha = 1f;
                buttonsPanelCanvasGroup.interactable = true;
                buttonsPanelCanvasGroup.blocksRaycasts = true;
                Debug.Log("[MainMenuButtonsController] CanvasGroup initialized and set to interactive");
            }
        }

        private void SubscribeToEvents()
        {
            GameEvents.OnGameStateChanged.AddListener(HandleGameStateChanged);
            GameEvents.OnUIAnimationCompleted.AddListener(HandleUIAnimationCompleted);
            GameEvents.OnProfileSelected.AddListener(HandleProfileSelected);
            Debug.Log("[MainMenuButtonsController] Events subscribed");
        }

        private void SetupButtonListeners()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(OnStartClicked);
            }
            if (optionsButton != null)
            {
                optionsButton.onClick.RemoveAllListeners();
                optionsButton.onClick.AddListener(OnOptionsClicked);
            }
            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(OnQuitClicked);
            }
            Debug.Log("[MainMenuButtonsController] Button listeners setup complete");
        }

        private void HandleGameStateChanged(GameState newState)
        {
            Debug.Log($"[MainMenuButtonsController] Game state changed to: {newState}");

            if (newState == GameState.MainMenu)
            {
                Debug.Log("[MainMenuButtonsController] Transitioning to MainMenu state");
                ForceActivateButtons();
            }
        }

        private void HandleUIAnimationCompleted()
        {
            Debug.Log("[MainMenuButtonsController] UI Animation completed");
            if (GameManager.Instance.CurrentState == GameState.MainMenu)
            {
                Debug.Log("[MainMenuButtonsController] UI Animation completed in MainMenu state - Activating buttons");
                ForceActivateButtons();
            }
        }

        private void HandleProfileSelected(string profileId)
        {
            Debug.Log($"[MainMenuButtonsController] Profile selected: {profileId}");
            if (!string.IsNullOrEmpty(profileId) && GameManager.Instance.CurrentState == GameState.MainMenu)
            {
                ForceActivateButtons();
            }
        }

        private void ForceActivateButtons()
        {
            Debug.Log("[MainMenuButtonsController] Force activating buttons");

            // Log button states before activation
            LogButtonStates("Before activation");

            if (buttonsPanelCanvasGroup != null)
            {
                buttonsPanelCanvasGroup.alpha = 1f;
                buttonsPanelCanvasGroup.interactable = true;
                buttonsPanelCanvasGroup.blocksRaycasts = true;
            }

            SetButtonsInteractable(true);

            // Log button states after activation
            LogButtonStates("After activation");
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (startButton != null)
            {
                startButton.interactable = interactable;
                Debug.Log($"[MainMenuButtonsController] Set Start button interactable: {interactable}");
            }
            if (optionsButton != null)
            {
                optionsButton.interactable = interactable;
            }
            if (quitButton != null)
            {
                quitButton.interactable = interactable;
            }

            if (buttonsPanelCanvasGroup != null)
            {
                buttonsPanelCanvasGroup.interactable = interactable;
                buttonsPanelCanvasGroup.blocksRaycasts = interactable;
                Debug.Log($"[MainMenuButtonsController] Set CanvasGroup - interactable: {interactable}, blocksRaycasts: {interactable}");
            }
        }

        private void LogButtonStates(string context)
        {
            Debug.Log($"[MainMenuButtonsController] Button States ({context}):");
            if (startButton != null)
                Debug.Log($"[MainMenuButtonsController] Start button interactable: {startButton.interactable}");
            if (optionsButton != null)
                Debug.Log($"[MainMenuButtonsController] Options button interactable: {optionsButton.interactable}");
            if (quitButton != null)
                Debug.Log($"[MainMenuButtonsController] Quit button interactable: {quitButton.interactable}");
            if (buttonsPanelCanvasGroup != null)
                Debug.Log($"[MainMenuButtonsController] CanvasGroup - interactable: {buttonsPanelCanvasGroup.interactable}, blocksRaycasts: {buttonsPanelCanvasGroup.blocksRaycasts}, alpha: {buttonsPanelCanvasGroup.alpha}");
        }

        private void OnStartClicked()
        {
            Debug.Log("[MainMenuButtonsController] Start button clicked");
            GameEvents.OnGameStateChanged.Invoke(GameState.CharacterSelect);
        }

        private void OnOptionsClicked()
        {
            Debug.Log("[MainMenuButtonsController] Options button clicked");
            // TODO: Implement options menu
        }

        private void OnQuitClicked()
        {
            Debug.Log("[MainMenuButtonsController] Quit button clicked");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDisable()
        {
            GameEvents.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
            GameEvents.OnUIAnimationCompleted.RemoveListener(HandleUIAnimationCompleted);
            GameEvents.OnProfileSelected.RemoveListener(HandleProfileSelected);
            Debug.Log("[MainMenuButtonsController] Events unsubscribed");
        }
    }
}