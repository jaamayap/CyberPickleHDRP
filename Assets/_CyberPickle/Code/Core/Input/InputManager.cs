using UnityEngine;
using CyberPickle.Core.Management;
using CyberPickle.Core.Events;
using CyberPickle.Core.Interfaces;
using CyberPickle.Core.States;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace CyberPickle.Core.Input
{
    public class InputManager : Manager<InputManager>, IInitializable
    {
        [Header("Input Settings")]
        [SerializeField, Tooltip("Mouse sensitivity multiplier for horizontal movement")]
        private float mouseSensitivity = 1.5f;

        [SerializeField, Tooltip("Touch sensitivity multiplier for horizontal movement")]
        private float touchSensitivity = 1.5f;

        [SerializeField, Tooltip("Keyboard sensitivity multiplier for horizontal movement")]
        private float keyboardSensitivity = 0.1f;

        [Header("Dead Zones")]
        [SerializeField, Tooltip("Minimum mouse movement required to trigger input")]
        private float mouseDeadZone = 0.01f;

        [SerializeField, Tooltip("Minimum touch movement required to trigger input")]
        private float touchDeadZone = 0.001f;

        private GameState currentGameState;
        private bool isInputEnabled = true;
        private bool isInitialized;

#if ENABLE_INPUT_SYSTEM
        private InputAction moveAction;
        private InputAction anyKeyAction;
        private InputAction touchPositionAction;
        private InputAction touchPressAction;
        private InputAction mousePositionAction;
        private InputAction mouseButtonAction;
        private InputAction profileNavigationAction;
        private Vector2 previousTouchPosition;
        private Vector2 previousMousePosition;
        private bool isMousePressed;
#endif

        public void Initialize()
        {
            if (isInitialized) return;

            SetupInputActions();
            RegisterEvents();
            EnableInput();

            isInitialized = true;
            Debug.Log("InputManager initialized");
        }

        private void SetupInputActions()
        {
#if ENABLE_INPUT_SYSTEM
            // Movement input
            moveAction = new InputAction("Move");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            moveAction.performed += ctx => HandleMovement(ctx.ReadValue<Vector2>());
            moveAction.canceled += ctx => HandleMovement(Vector2.zero);

            // Any key input
            anyKeyAction = new InputAction("AnyKey", InputActionType.Button);
            anyKeyAction.AddBinding("<Keyboard>/anyKey");
            anyKeyAction.AddBinding("<Mouse>/leftButton");
            anyKeyAction.performed += ctx => HandleAnyKeyPress();

            // Mouse input
            mousePositionAction = new InputAction("MousePosition", InputActionType.Value, "<Mouse>/position");
            mouseButtonAction = new InputAction("MouseButton", InputActionType.Button, "<Mouse>/leftButton");

            mouseButtonAction.started += _ => { isMousePressed = true; previousMousePosition = mousePositionAction.ReadValue<Vector2>(); };
            mouseButtonAction.canceled += _ => isMousePressed = false;

            // Touch input
            touchPositionAction = new InputAction("TouchPosition");
            touchPositionAction.AddBinding("<Touchscreen>/position");

            touchPressAction = new InputAction("TouchPress");
            touchPressAction.AddBinding("<Touchscreen>/primaryTouch/press");
            touchPressAction.performed += ctx => HandleTouchPress(ctx.ReadValue<float>());

            // Add this new action for profile navigation
            profileNavigationAction = new InputAction("ProfileNavigation");
            profileNavigationAction.AddCompositeBinding("1DAxis")
                .With("Positive", "<Keyboard>/downArrow")
                .With("Negative", "<Keyboard>/upArrow");
            profileNavigationAction.performed += ctx => HandleProfileNavigation(ctx.ReadValue<float>());
#endif
        }
        private void HandleProfileNavigation(float direction)
        {
            if (!isInputEnabled) return;

            if (currentGameState == GameState.ProfileSelection)
            {
                GameEvents.OnProfileNavigationInput.Invoke(direction);
            }
        }
        protected override void OnManagerEnabled()
        {
#if ENABLE_INPUT_SYSTEM
            moveAction?.Enable();
            anyKeyAction?.Enable();
            touchPositionAction?.Enable();
            touchPressAction?.Enable();
            mousePositionAction?.Enable();
            mouseButtonAction?.Enable();
            profileNavigationAction?.Enable();
#endif
        }

        protected override void OnManagerDisabled()
        {
#if ENABLE_INPUT_SYSTEM
            moveAction?.Disable();
            anyKeyAction?.Disable();
            touchPositionAction?.Disable();
            touchPressAction?.Disable();
            mousePositionAction?.Disable();
            mouseButtonAction?.Disable();
            profileNavigationAction?.Enable();
#endif
        }

        private void HandleMovement(Vector2 movement)
        {
            if (!isInputEnabled || currentGameState != GameState.Playing) return;

            float horizontalInput = movement.x * keyboardSensitivity;
            if (Mathf.Abs(horizontalInput) > mouseDeadZone)
            {
                GameEvents.OnHorizontalInput.Invoke(horizontalInput);
            }
        }

        private void HandleAnyKeyPress()
        {
            if (!isInputEnabled) return;

            switch (currentGameState)
            {
                case GameState.MainMenu:
                    GameEvents.OnMainMenuInput.Invoke();
                    break;
            }
        }

#if ENABLE_INPUT_SYSTEM
        private void HandleTouchPress(float pressed)
        {
            if (!isInputEnabled) return;

            if (pressed > 0)
            {
                previousTouchPosition = touchPositionAction.ReadValue<Vector2>();
            }
            else if (currentGameState == GameState.MainMenu)
            {
                GameEvents.OnMainMenuInput.Invoke();
            }
        }

        private void HandleMouseInput()
        {
            if (!isMousePressed || !isInputEnabled) return;

            Vector2 currentMousePosition = mousePositionAction.ReadValue<Vector2>();
            Vector2 mouseDelta = currentMousePosition - previousMousePosition;
            float normalizedDelta = mouseDelta.x / Screen.width * mouseSensitivity;

            if (Mathf.Abs(normalizedDelta) > mouseDeadZone && currentGameState == GameState.Playing)
            {
                GameEvents.OnHorizontalInput.Invoke(normalizedDelta);
            }

            previousMousePosition = currentMousePosition;
        }

        private void Update()
        {
            if (!isInputEnabled) return;

            // Handle mouse movement
            HandleMouseInput();

            // Handle touch movement
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                Vector2 currentPosition = touchPositionAction.ReadValue<Vector2>();
                Vector2 delta = currentPosition - previousTouchPosition;
                float normalizedDelta = delta.x / Screen.width * touchSensitivity;

                if (Mathf.Abs(normalizedDelta) > touchDeadZone && currentGameState == GameState.Playing)
                {
                    GameEvents.OnHorizontalInput.Invoke(normalizedDelta);
                }

                previousTouchPosition = currentPosition;
            }

            // Check for pause input
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                switch (currentGameState)
                {
                    case GameState.Playing:
                        GameEvents.OnPauseRequested.Invoke();
                        break;
                    case GameState.Paused:
                        GameEvents.OnResumeRequested.Invoke();
                        break;
                }
            }
        }
#endif

        private void RegisterEvents()
        {
            GameEvents.OnGameStateChanged.AddListener(HandleGameStateChanged);
            GameEvents.OnGamePaused.AddListener(DisableInput);
            GameEvents.OnGameResumed.AddListener(EnableInput);
        }

        private void HandleGameStateChanged(GameState newState)
        {
            currentGameState = newState;
            Debug.Log($"Input Manager state changed to: {newState}");
        }

        public void EnableInput()
        {
            isInputEnabled = true;
#if ENABLE_INPUT_SYSTEM
            moveAction?.Enable();
            anyKeyAction?.Enable();
            touchPositionAction?.Enable();
            touchPressAction?.Enable();
            mousePositionAction?.Enable();
            mouseButtonAction?.Enable();
#endif
        }

        public void DisableInput()
        {
            isInputEnabled = false;
#if ENABLE_INPUT_SYSTEM
            moveAction?.Disable();
            anyKeyAction?.Disable();
            touchPositionAction?.Disable();
            touchPressAction?.Disable();
            mousePositionAction?.Disable();
            mouseButtonAction?.Disable();
#endif
        }

        protected override void OnManagerDestroyed()
        {
            // Clean up events
            GameEvents.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
            GameEvents.OnGamePaused.RemoveListener(DisableInput);
            GameEvents.OnGameResumed.RemoveListener(EnableInput);

            // Clean up input actions
#if ENABLE_INPUT_SYSTEM
            moveAction?.Dispose();
            anyKeyAction?.Dispose();
            touchPositionAction?.Dispose();
            touchPressAction?.Dispose();
            mousePositionAction?.Dispose();
            mouseButtonAction?.Dispose();
            profileNavigationAction?.Dispose();
#endif
        }
    }
}
