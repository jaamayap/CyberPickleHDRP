// File: Assets/Code/Core/Boot/BootManager.cs
//
// Purpose: Manages the game's boot sequence, initializing all core systems, services,
// and gameplay managers in the correct order while providing visual feedback.
//
// Created: 2024-02-11
// Updated: 2024-02-11

using UnityEngine;
using System.Collections;
using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;
using CyberPickle.Core.Config;
using CyberPickle.Core.Boot.UI;
using CyberPickle.Core.Audio;
using CyberPickle.Core.SaveSystem;
using CyberPickle.Core.Input;
using CyberPickle.Core.Pool;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Services.Leaderboard;
using CyberPickle.Core.Services.Steam;
using CyberPickle.Core.Analytics;
using CyberPickle.Characters;
using CyberPickle.Progression;
using CyberPickle.Achievements;
using UnityEngine.SceneManagement;
using CyberPickle.Core.Services.Authentication.Flow;



namespace CyberPickle.Core.Boot
{
    public class BootManager : Manager<BootManager>, IInitializable
    {
        [Header("UI")]
        [SerializeField] private BootUIController uiController;

        [Header("Timing")]
        [SerializeField] private float minimumLoadTime = 2f;
        [SerializeField] private float delayBeforeScene = 0.5f;

        private float startTime;
        private ConfigRegistry configRegistry;

        protected override void OnManagerAwake()
        {
            Debug.Log("<color=yellow>[BootManager] Awake called</color>");
            ValidateRequirements();
            startTime = Time.time;
            Initialize();
        }

        private void ValidateRequirements()
        {
            if (uiController == null)
            {
                throw new System.Exception("<color=red>[BootManager] UI Controller is not assigned!</color>");
            }
        }

        public void Initialize()
        {
            Debug.Log("<color=yellow>[BootManager] Starting initialize sequence...</color>");
            uiController.UpdateLoadingText("Starting initialization...");
            uiController.UpdateProgress(0f);
            StartCoroutine(InitializeGameSystems());
        }

        private IEnumerator InitializeGameSystems()
        {
            Debug.Log("<color=yellow>[BootManager] Starting initialization sequence...</color>");

            // Initialize ConfigRegistry first
            bool configSuccess = false;
            yield return StartCoroutine(InitializeConfigRegistry(success => configSuccess = success));

            if (!configSuccess)
            {
                Debug.LogError("<color=red>[BootManager] Failed to initialize configs. Aborting boot sequence.</color>");
                yield break;
            }

            // Initialize core systems
            yield return StartCoroutine(InitializeCore());

            // Initialize services
            yield return StartCoroutine(InitializeServices());

            // Initialize gameplay
            yield return StartCoroutine(InitializeGameplay());

            yield return StartCoroutine(CompleteInitialization());
        }

        // Custom class to handle initialization result
        private class ConfigInitializationResult : CustomYieldInstruction
        {
            public bool Success { get; private set; }

            public ConfigInitializationResult(bool success)
            {
                Success = success;
            }

            public override bool keepWaiting => false;
        }

        private IEnumerator InitializeConfigRegistry(System.Action<bool> onComplete)
        {
            uiController.UpdateLoadingText("Loading Configurations...");
            float progress = 0f;
            uiController.UpdateProgress(progress);

            configRegistry = ConfigRegistry.Instance;
            if (configRegistry == null)
            {
                Debug.LogError("<color=red>[BootManager] Failed to create ConfigRegistry!</color>");
                onComplete?.Invoke(false);
                yield break;
            }

            bool initComplete = false;
            bool initSuccess = false;

            // Start async initialization
            configRegistry.InitializeAsync().ContinueWith(task =>
            {
                initComplete = true;
                initSuccess = !task.IsFaulted;

                if (task.IsFaulted)
                {
                    Debug.LogError($"<color=red>[BootManager] Config initialization failed: {task.Exception.GetBaseException().Message}</color>");
                }
            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());

            // Show progress while waiting
            while (!initComplete)
            {
                progress = Mathf.PingPong(Time.time * 0.5f, 0.1f);
                uiController.UpdateProgress(progress);
                yield return null;
            }

            if (initSuccess)
            {
                Debug.Log("<color=green>[BootManager] Configurations loaded successfully!</color>");
                uiController.UpdateProgress(0.1f); // Move to 10% progress
            }

            onComplete?.Invoke(initSuccess);
        }

        private IEnumerator InitializeCore()
        {
            uiController.UpdateLoadingText("Initializing Core Systems...");
            float progressStart = 0.1f;
            float progressEnd = 0.4f;
            float step = (progressEnd - progressStart) / 4f;

            // Initialize Input Manager first
            yield return InitializeManager<InputManager>("Input System", progressStart, progressStart + step);

            // Then other systems
            yield return InitializeManager<AudioManager>("Audio System", progressStart + step, progressStart + (step * 2));
            yield return InitializeManager<SaveManager>("Save System", progressStart + (step * 2), progressStart + (step * 3));
            yield return InitializeManager<PoolManager>("Pool System", progressStart + (step * 3), progressEnd);
        }

        private IEnumerator InitializeServices()
        {
            uiController.UpdateLoadingText("Initializing Services...");
            float progressStart = 0.4f;
            float progressEnd = 0.7f;
            float step = (progressEnd - progressStart) / 5f;

            // Initialize AuthFlowManager first
            yield return InitializeManager<AuthenticationFlowManager>("Auth Flow", progressStart, progressStart + step);

            // Then the rest of the services
            yield return InitializeManager<AuthenticationManager>("Authentication", progressStart + step, progressStart + (step * 2));
            yield return InitializeManager<ProfileManager>("Profile Manager", progressStart + (step * 2), progressStart + (step * 3));
            yield return InitializeManager<LeaderboardManager>("Leaderboard", progressStart + (step * 3), progressStart + (step * 4));
            yield return InitializeManager<SteamManager>("Steam", progressStart + (step * 4), progressEnd);
            yield return InitializeManager<AnalyticsManager>("Analytics", progressStart + (step * 4), progressEnd);
        }

        private IEnumerator InitializeGameplay()
        {
            uiController.UpdateLoadingText("Initializing Gameplay Systems...");
            float progressStart = 0.7f;
            float progressEnd = 0.9f;
            float step = (progressEnd - progressStart) / 4f;

            yield return InitializeManager<GameManager>("Game System", progressStart, progressStart + step);
            yield return InitializeManager<CharacterManager>("Character System", progressStart + step, progressStart + (step * 2));
            yield return InitializeManager<ProgressionManager>("Progression System", progressStart + (step * 2), progressStart + (step * 3));
            yield return InitializeManager<AchievementManager>("Achievement System", progressStart + (step * 3), progressEnd);
        }

        private IEnumerator InitializeManager<M>(string systemName, float startProgress, float endProgress)
            where M : Manager<M>, IInitializable
        {
            Debug.Log($"<color=yellow>[BootManager] Initializing {systemName}...</color>");

            M manager = null;
            bool success = false;

            try
            {
                manager = Manager<M>.Instance;
                manager.Initialize();
                success = true;
                Debug.Log($"<color=green>[BootManager] {systemName} initialized successfully!</color>");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"<color=red>[BootManager] Failed to initialize {systemName}: {e.Message}</color>");
                yield break;
            }

            if (success)
            {
                float elapsed = 0f;
                float duration = 0.5f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float normalizedTime = elapsed / duration;
                    float currentProgress = Mathf.Lerp(startProgress, endProgress, normalizedTime);
                    uiController.UpdateProgress(currentProgress);
                    yield return null;
                }
            }
        }

        private IEnumerator CompleteInitialization()
        {
            uiController.UpdateLoadingText("Loading Complete");
            uiController.UpdateProgress(1f);

            // Ensure minimum display time
            float elapsedTime = Time.time - startTime;
            if (elapsedTime < minimumLoadTime)
            {
                yield return new WaitForSeconds(minimumLoadTime - elapsedTime);
            }

            yield return new WaitForSeconds(delayBeforeScene);

            // Get mainMenuSceneName from config
            var bootConfig = configRegistry.GetConfig<BootConfig>();
            LoadMainMenuScene(bootConfig.mainMenuSceneName);
        }

        private void LoadMainMenuScene(string sceneName)
        {
            Debug.Log($"<color=green>[BootManager] Loading main menu scene: {sceneName}</color>");
            SceneManager.LoadScene(sceneName);
        }
    }
}



