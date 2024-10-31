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
        [Header("Configuration")]
        [SerializeField] private GameConfig gameConfig;

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

            RegisterEventListeners();
            LoadGameConfig();
            isInitialized = true;

            GameEvents.OnGameInitialized.Invoke();
        }

        private void RegisterEventListeners()
        {
            // Register for events that might affect game state
            GameEvents.OnPlayerDied.AddListener(HandlePlayerDeath);
            GameEvents.OnLevelCompleted.AddListener(HandleLevelCompleted);
        }

        private void LoadGameConfig()
        {
            if (gameConfig == null)
            {
                gameConfig = Resources.Load<GameConfig>("Configs/GameConfig");
                if (gameConfig == null)
                {
                    Debug.LogError("GameConfig not found! Please create a GameConfig asset.");
                }
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
            ChangeState(GameState.Loading);

            // Load character select scene
            yield return LoadSceneAsync(gameConfig.characterSelectSceneName);
            ChangeState(GameState.CharacterSelect);
        }

        public void StartLevel(string levelId)
        {
            StartCoroutine(StartLevelSequence(levelId));
        }

        private IEnumerator StartLevelSequence(string levelId)
        {
            ChangeState(GameState.Loading);

            // Load game scene
            yield return LoadSceneAsync(gameConfig.gameSceneName);

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
            yield return LoadSceneAsync(gameConfig.postGameSceneName);
            ChangeState(GameState.PostGame);
        }

        #endregion

        #region State Management

        private void ChangeState(GameState newState)
        {
            if (currentState == newState) return;

            previousState = currentState;
            currentState = newState;

            Debug.Log($"Game State Changed: {previousState} -> {currentState}");

            HandleStateChange();
        }

        private void HandleStateChange()
        {
            switch (currentState)
            {
                case GameState.MainMenu:
                    Time.timeScale = 1f;
                    break;

                case GameState.Playing:
                    Time.timeScale = 1f;
                    break;

                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;

                case GameState.GameOver:
                    Time.timeScale = 0f;
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
            yield return LoadSceneAsync(gameConfig.postGameSceneName);
            ChangeState(GameState.PostGame);
        }

        #endregion

        #region Utility Methods

        private void SaveProgress()
        {
            // This will be implemented when we create the SaveSystem
            Debug.Log("Saving game progress...");
        }

        private IEnumerator LoadSceneAsync(string sceneName)
        {
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

            while (!asyncLoad.isDone)
            {
                float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
                // Could dispatch loading progress event here
                yield return null;
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
            Initialize();
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
            if (!IsActiveInstance) return;

            try
            {
                SaveProgress();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error saving progress during shutdown: {e.Message}");
            }

            // Cleanup event listeners
            GameEvents.OnPlayerDied.RemoveListener(HandlePlayerDeath);
            GameEvents.OnLevelCompleted.RemoveListener(HandleLevelCompleted);
        }
    }
}
