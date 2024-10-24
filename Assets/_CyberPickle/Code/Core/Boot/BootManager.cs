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

namespace CyberPickle.Core.Boot
{
    public class BootManager : Manager<BootManager>, IInitializable
    {
        [Header("Configuration")]
        [SerializeField] private BootConfig bootConfig;
        [SerializeField] private BootUIController uiController;
        [SerializeField] private float minimumLoadTime = 2f; // Minimum time to show the boot screen
        [SerializeField] private float delayBeforeScene = 0.5f; // Short delay before loading main scene

        private float startTime;

        protected override void OnManagerAwake()
        {
            Debug.Log("<color=yellow>[BootManager] Awake called</color>");
            if (uiController == null)
            {
                Debug.LogError("<color=red>[BootManager] UI Controller is not assigned!</color>");
                return;
            }
            startTime = Time.time;
            Initialize();
        }



        public void Initialize()
        {
            Debug.Log("<color=yellow>[BootManager] Starting initialization sequence...</color>");
            // Verify UI Controller again
            if (uiController == null)
            {
                Debug.LogError("<color=red>[BootManager] UI Controller is not assigned!</color>");
                return;
            }

            // Test UI Controller
            uiController.UpdateLoadingText("Starting initialization...");
            uiController.UpdateProgress(0f);

            StartCoroutine(InitializeGameSystems());
        }

        private IEnumerator InitializeGameSystems()
        {
            Debug.Log("<color=yellow>[BootManager] Starting initialization sequence...</color>");

            // Initialize core systems
            yield return StartCoroutine(InitializeCore());

            // Initialize services
            yield return StartCoroutine(InitializeServices());

            // Initialize gameplay
            yield return StartCoroutine(InitializeGameplay());

            // Ensure loading bar reaches 100%
            uiController.UpdateProgress(1f);
            uiController.UpdateLoadingText("Loading Complete");

            // Ensure minimum display time
            float elapsedTime = Time.time - startTime;
            if (elapsedTime < minimumLoadTime)
            {
                yield return new WaitForSeconds(minimumLoadTime - elapsedTime);
            }

            // Small delay before scene transition
            yield return new WaitForSeconds(delayBeforeScene);

            // Load main scene
            LoadMainMenuScene();
        }

        private IEnumerator InitializeCore()
        {
            uiController.UpdateLoadingText("Initializing Core Systems...");
            float progressStart = 0f;
            float progressEnd = 0.3f;
            float step = (progressEnd - progressStart) / 4f; // 4 systems

            yield return InitializeManager<AudioManager>("Audio System", progressStart, progressStart + step);
            yield return InitializeManager<SaveManager>("Save System", progressStart + step, progressStart + (step * 2));
            yield return InitializeManager<InputManager>("Input System", progressStart + (step * 2), progressStart + (step * 3));
            yield return InitializeManager<PoolManager>("Pool System", progressStart + (step * 3), progressEnd);
        }

        private IEnumerator InitializeServices()
        {
            uiController.UpdateLoadingText("Initializing Services...");
            float progressStart = 0.3f;
            float progressEnd = 0.6f;
            float step = (progressEnd - progressStart) / 4f; // 4 systems

            yield return InitializeManager<AuthenticationManager>("Authentication", progressStart, progressStart + step);
            yield return InitializeManager<LeaderboardManager>("Leaderboard", progressStart + step, progressStart + (step * 2));
            yield return InitializeManager<SteamManager>("Steam", progressStart + (step * 2), progressStart + (step * 3));
            yield return InitializeManager<AnalyticsManager>("Analytics", progressStart + (step * 3), progressEnd);
        }

        private IEnumerator InitializeGameplay()
        {
            uiController.UpdateLoadingText("Initializing Gameplay Systems...");
            float progressStart = 0.6f;
            float progressEnd = 1f;
            float step = (progressEnd - progressStart) / 4f; // 4 systems

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

            // Get manager instance
            try
            {
                manager = Manager<M>.Instance;
                manager.Initialize();
                Debug.Log($"<color=green>[BootManager] {systemName} initialized successfully!</color>");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"<color=red>[BootManager] Failed to initialize {systemName}: {e.Message}</color>");
                yield break;
            }

            // Update progress after initialization
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

        private void LoadMainMenuScene()
        {
            Debug.Log("<color=green>[BootManager] Loading main menu scene...</color>");
            SceneManager.LoadScene(bootConfig.mainMenuSceneName);
        }
    }
}


