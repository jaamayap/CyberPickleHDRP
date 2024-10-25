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
        [SerializeField] private float mouseSensitivity = 1.5f;
        [SerializeField] private float touchSensitivity = 1.5f;
        [SerializeField] private float keyboardSensitivity = 0.1f;

        private GameState currentGameState;
        private bool isInputEnabled = true;
        private bool isInitialized;

#if ENABLE_INPUT_SYSTEM
        private InputAction moveAction;
        private InputAction anyKeyAction;
        private InputAction touchPositionAction;
        private InputAction touchPressAction;
        private Vector2 previousTouchPosition;
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

            // Touch input
            touchPositionAction = new InputAction("TouchPosition");
            touchPositionAction.AddBinding("<Touchscreen>/position");

            touchPressAction = new InputAction("TouchPress");
            touchPressAction.AddBinding("<Touchscreen>/primaryTouch/press");
            touchPressAction.performed += ctx => HandleTouchPress(ctx.ReadValue<float>());
#endif
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            moveAction?.Enable();
            anyKeyAction?.Enable();
            touchPositionAction?.Enable();
            touchPressAction?.Enable();
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            moveAction?.Disable();
            anyKeyAction?.Disable();
            touchPositionAction?.Disable();
            touchPressAction?.Disable();
#endif
        }

        private void HandleMovement(Vector2 movement)
        {
            if (!isInputEnabled || currentGameState != GameState.Playing) return;

            float horizontalInput = movement.x * keyboardSensitivity;
            if (Mathf.Abs(horizontalInput) > 0.01f)
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

        private void Update()
        {
            if (!isInputEnabled) return;

            // Handle touch movement
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                Vector2 currentPosition = touchPositionAction.ReadValue<Vector2>();
                Vector2 delta = currentPosition - previousTouchPosition;
                float normalizedDelta = delta.x / Screen.width * touchSensitivity;

                if (Mathf.Abs(normalizedDelta) > 0.001f && currentGameState == GameState.Playing)
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
#endif
        }

        private void OnDestroy()
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
#endif
        }
    }
}
