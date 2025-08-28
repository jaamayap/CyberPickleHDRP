using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using CyberPickle.Core.Management;
using CyberPickle.Core.Events;
using CyberPickle.Core.States;
using CyberPickle.Core.Config;
using CyberPickle.Core.Interfaces;

namespace CyberPickle.Core
{
    public class GameManager : Manager<GameManager>, IInitializable
    {

        private GameConfig gameConfig;
        private GameState currentState = GameState.None;
        private GameState previousState = GameState.None;
        private bool isInitialized;
        private bool isPaused;

        public GameState CurrentState => currentState;
        public bool IsPaused => isPaused;

        #region Initialization

        public void Initialize()
        {
            if (isInitialized) return;

            LoadGameConfig();
            if (gameConfig == null)
            {
                Debug.LogError("<color=red>[GameManager] Failed to load GameConfig. Cannot initialize!</color>");
                return;
            }

            RegisterEventListeners();
            isInitialized = true;

            GameEvents.OnGameInitialized.Invoke();
        }

        private void RegisterEventListeners()
        {
            // Register for events that might affect game state
            GameEvents.OnPlayerDied.AddListener(HandlePlayerDeath);
            GameEvents.OnLevelCompleted.AddListener(HandleLevelCompleted);
            GameEvents.OnGameStateChanged.AddListener(OnExternalGameStateChangeRequest);
        }

        private void OnExternalGameStateChangeRequest(GameState newState)
        {
            Debug.Log($"[GameManager] Received external GameStateChanged event: {newState}. Current state: {currentState}");
            ChangeState(newState);
        }
        private void LoadGameConfig()
        {
            try
            {
                var configRegistry = ConfigRegistry.Instance;
                if (configRegistry == null)
                {
                    throw new System.Exception("ConfigRegistry not initialized!");
                }

                gameConfig = configRegistry.GetConfig<GameConfig>();
                if (gameConfig == null)
                {
                    throw new System.Exception("GameConfig not found in registry!");
                }

                Debug.Log("<color=green>[GameManager] GameConfig loaded successfully!</color>");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"<color=red>[GameManager] Failed to load GameConfig: {e.Message}</color>");
            }
        }

        #endregion

        #region Game Flow Control

        public void StartNewGame()
        {
            StartCoroutine(StartNewGameSequence());
        }

        private IEnumerator StartNewGameSequence()
        {
            // 1. Set a loading state (optional, depends if you need a visual transition)
            // ChangeState(GameState.Loading);
            // yield return null; // Wait a frame if needed for UI

            // 2. Ensure we are in the MainMenu scene.
            //    If StartNewGame is ONLY ever called *from* the MainMenu, this check might be redundant.
            //    But it's safer if StartNewGame could theoretically be called from elsewhere.
            if (SceneManager.GetActiveScene().name != gameConfig.mainMenuSceneName)
            {
                Debug.Log($"[GameManager] StartNewGame called outside MainMenu. Loading MainMenu scene first.");
                yield return StartCoroutine(LoadSceneCoroutine(gameConfig.mainMenuSceneName));
            }
            else
            {
                Debug.Log($"[GameManager] StartNewGame called within MainMenu scene.");
            }


            // 3. Change the state to CharacterSelect.
            //    MainMenuController and CharacterSelectionManager will handle showing the UI.
            ChangeState(GameState.CharacterSelect);

            // No scene loading needed *for* CharacterSelect itself anymore.
        }

        public void StartLevel(string levelId)
        {
            StartCoroutine(StartLevelSequence(levelId));
        }

        private IEnumerator StartLevelSequence(string levelId)
        {
            ChangeState(GameState.Loading);

            // Load game scene
            yield return LoadSceneCoroutine(gameConfig.gameSceneName);

            // Initialize level
            yield return new WaitForSeconds(gameConfig.gameStartDelay);

            ChangeState(GameState.Playing);
            GameEvents.OnGameStarted.Invoke();
        }

        public void PauseGame()
        {
            if (currentState != GameState.Playing) return;

            isPaused = true;
            Time.timeScale = 0f;
            ChangeState(GameState.Paused);
            GameEvents.OnGamePaused.Invoke();
        }

        public void ResumeGame()
        {
            if (currentState != GameState.Paused) return;

            isPaused = false;
            Time.timeScale = 1f;
            ChangeState(GameState.Playing);
            GameEvents.OnGameResumed.Invoke();
        }

        public void GameOver()
        {
            StartCoroutine(GameOverSequence());
        }

        private IEnumerator GameOverSequence()
        {
            yield return new WaitForSeconds(gameConfig.gameOverDelay);

            ChangeState(GameState.GameOver);
            GameEvents.OnGameOver.Invoke();

            // Load post-game scene
            yield return LoadSceneCoroutine(gameConfig.postGameSceneName);
            ChangeState(GameState.PostGame);
        }

        #endregion

        #region State Management

        private void ChangeState(GameState newState)
        {
            // Allow re-entering scene-loading states if the current scene is different or it's a forced reload
            bool isSceneLoadState = newState == GameState.CharacterSelect ||
                                    newState == GameState.EquipmentSelect ||
                                    newState == GameState.LevelSelect ||
                                    newState == GameState.MainMenu; // MainMenu can also be a scene load

            if (currentState == newState && !isSceneLoadState && newState != GameState.Loading)
            {
                Debug.LogWarning($"[GameManager] Attempting to change to the same non-scene-loading state: {newState}. Current: {currentState}. Skipping.");
                // return; // This might be too restrictive. Let's allow it for now and see.
            }

            Debug.Log($"[GameManager] Changing state from {currentState} to {newState}");
            previousState = currentState;
            currentState = newState;

            // Important: Invoke the event AFTER setting the new state,
            // so listeners querying CurrentState get the new one.
            // However, HandleStateChange might load a scene, which is async.
            // The event should ideally be invoked by the system that *requests* the change.
            // Here, GameManager is *reacting* to a state change (either internal or external).
            // GameEvents.OnGameStateChanged.Invoke(currentState); // This was moved to OnExternalGameStateChangeRequest or similar initiators.
            // The GameManager primarily *acts* on state changes.

            HandleStateChangeInternal();
        }

        private void HandleStateChangeInternal() // Renamed to avoid confusion with event handlers
        {
            Debug.Log($"[GameManager] Handling internal state change actions for: {currentState}");
            switch (currentState)
            {
                case GameState.MainMenu:
                    Time.timeScale = 1f;
                    if (SceneManager.GetActiveScene().name != gameConfig.mainMenuSceneName)
                    {
                        StartCoroutine(LoadSceneCoroutine(gameConfig.mainMenuSceneName));
                    }
                    // MainMenuController will handle showing appropriate panels.
                    break;

                case GameState.ProfileSelection:
                    Time.timeScale = 1f;
                    // This state implies we are already in the MainMenu scene.
                    // MainMenuController handles showing the profile selection panel.
                    if (SceneManager.GetActiveScene().name != gameConfig.mainMenuSceneName)
                    {
                        Debug.LogWarning($"[GameManager] GameState.ProfileSelection requested but not in MainMenu scene. Loading MainMenu scene.");
                        StartCoroutine(LoadSceneCoroutine(gameConfig.mainMenuSceneName));
                    }
                    break;

                case GameState.CharacterSelect:
                    Time.timeScale = 1f;
                    // Character Selection is now an area within the MainMenu scene.
                    // Ensure MainMenu scene is loaded. MainMenuController will handle UI.
                    if (SceneManager.GetActiveScene().name != gameConfig.mainMenuSceneName)
                    {
                        Debug.LogWarning($"[GameManager] GameState.CharacterSelect requested but not in MainMenu scene. Loading MainMenu scene.");
                        StartCoroutine(LoadSceneCoroutine(gameConfig.mainMenuSceneName));
                    }
                    // The MainMenuController is responsible for activating the CharacterSelection UI area.
                    // CharacterSelectionManager will be activated by its own GameStateChanged listener.
                    Debug.Log($"[GameManager] State set to CharacterSelect. MainMenuController should now activate the UI area.");
                    break;

                case GameState.EquipmentSelect: // <<< FIXED CASE
                                                // Check if we're already in the equipment scene
                    if (SceneManager.GetActiveScene().name != gameConfig.equipmentSelectSceneName)
                    {
                        StartCoroutine(LoadSceneCoroutine(gameConfig.equipmentSelectSceneName));
                    }
                    else
                    {
                        Debug.Log($"[GameManager] Already in Equipment Hub scene. No need to reload.");
                    }
                    break;

                case GameState.LevelSelect:
                    StartCoroutine(LoadSceneCoroutine(gameConfig.levelSelectSceneName));
                    break;

                case GameState.Playing:
                    Time.timeScale = 1f;
                    // Scene for 'Playing' is loaded via StartLevelSequence -> LoadSceneCoroutine(gameConfig.gameSceneName)
                    break;

                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;

                case GameState.GameOver:
                    Time.timeScale = 1f; // Or 0f, depends on if you have animations in game over state
                                         // Scene for 'PostGame' is loaded via GameOverSequence -> LoadSceneCoroutine(gameConfig.postGameSceneName)
                    break;

                case GameState.PostGame:
                    // Scene for 'PostGame' is loaded via LevelCompleteSequence or GameOverSequence
                    break;

                case GameState.Loading:
                    // This is a transient state. Actual scene loading is in LoadSceneCoroutine.
                    break;
            }
        }

        #endregion

        #region Event Handlers

        private void HandlePlayerDeath()
        {
            if (currentState == GameState.Playing)
            {
                GameOver();
            }
        }

        private void HandleLevelCompleted(string levelId)
        {
            if (currentState == GameState.Playing)
            {
                StartCoroutine(LevelCompleteSequence(levelId));
            }
        }

        private IEnumerator LevelCompleteSequence(string levelId)
        {
            // Save progress
            SaveProgress();

            // Show level complete UI
            yield return new WaitForSeconds(2f);

            // Load post-game scene
            yield return LoadSceneCoroutine(gameConfig.postGameSceneName);
            ChangeState(GameState.PostGame);
        }

        #endregion

        #region Utility Methods

        private void SaveProgress()
        {
            // This will be implemented when we create the SaveSystem
            Debug.Log("Saving game progress...");
        }

        private IEnumerator LoadSceneCoroutine(string sceneName)
        {
            Debug.Log($"[GameManager] Starting to load scene: {sceneName} for state {currentState}");

            // Remember target state instead of previous state
            GameState targetState = currentState;

            // Set loading state
            if (currentState != GameState.Loading)
                ChangeState(GameState.Loading);

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            asyncLoad.allowSceneActivation = false;

            while (asyncLoad.progress < 0.9f)
            {
                // Update loading progress UI if any
                Debug.Log($"[GameManager] Loading {sceneName}: {asyncLoad.progress * 100f}%");
                yield return null;
            }

            Debug.Log($"[GameManager] Scene {sceneName} almost loaded. Activating...");
            asyncLoad.allowSceneActivation = true;

            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            Debug.Log($"[GameManager] Scene {sceneName} fully loaded and active.");

            // After loading is complete, ensure we're in the correct target state
            // NOT the previous state before loading started
            if (CurrentState == GameState.Loading)
            {
                Debug.Log($"[GameManager] Restoring target state: {targetState} after loading {sceneName}");
                ChangeState(targetState);
            }
        }

        #endregion

        #region Debug Methods

        public void ToggleDebugMode()
        {
            if (!gameConfig.enableDebugMode) return;

            // Add debug functionality
            Debug.Log("Debug mode toggled");
        }

        #endregion

        protected override void OnManagerAwake()
        {
            base.OnManagerAwake(); // Base Manager calls Initialize() if IInitializable
            Debug.Log("<color=yellow>[GameManager] GameManager OnManagerAwake completed.</color>");
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && currentState == GameState.Playing)
            {
                PauseGame();
            }
        }
        protected override void OnManagerDestroyed()
        {
            // Call base method first to handle its cleanup
            base.OnManagerDestroyed();

            if (!IsActiveInstance) return;

            try
            {
                SaveProgress();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameManager] Error saving progress during shutdown: {e.Message}");
            }

            // Cleanup event listeners
            GameEvents.OnPlayerDied.RemoveListener(HandlePlayerDeath);
            GameEvents.OnLevelCompleted.RemoveListener(HandleLevelCompleted);
            GameEvents.OnGameStateChanged.RemoveListener(OnExternalGameStateChangeRequest);
            Debug.Log("[GameManager] Cleaned up GameManager listeners.");
        }
    }
}
