// File: Assets/_CyberPickle/Code/UI/Screens/ResultsScreen/ResultsScreenController.cs
// Namespace: CyberPickle.UI.Screens.ResultsScreen
//
// Drives the post-run results screen UI. Listens for
// RunStateManager.OnPhaseChanged → when phase becomes GameOver, populates
// stat rows from RunStatsTracker and fades the panel in. Wires the two
// buttons (Try Again / Return to Hub) to scene transitions.
//
// UI panel itself is authored in the Game.unity scene as a CanvasGroup
// hidden by default. Drag the field references in the inspector. Black
// background, white text, two buttons — this is "minimum viable results
// screen"; visual polish is a later milestone.
//
// "Try Again" reloads the Game scene, which re-runs GameSceneBootstrap
// and starts a fresh run. Cleanest reset path — all per-run ECS state
// (corpses, XP gems, gem registry references, wave-spawner timer) is
// disposed by the scene unload, no manual cleanup code needed.

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CyberPickle.Gameplay.RunState;

namespace CyberPickle.UI.Screens.ResultsScreen
{
    [DisallowMultipleComponent]
    public class ResultsScreenController : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("CanvasGroup controlling visibility + interactability of the entire results panel. Should start with alpha 0, interactable=false, blocksRaycasts=false.")]
        [SerializeField] private CanvasGroup panel;

        [Tooltip("Seconds to fade the panel in / out.")]
        [Min(0f)] [SerializeField] private float fadeDuration = 0.5f;

        [Header("Stat Display")]
        [Tooltip("Title shown at the top — 'YOU DIED', 'RUN COMPLETE', etc.")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Tooltip("Stat rows. Order: Time Survived, Enemies Killed, Level Reached.")]
        [SerializeField] private TextMeshProUGUI timeSurvivedValueText;
        [SerializeField] private TextMeshProUGUI enemiesKilledValueText;
        [SerializeField] private TextMeshProUGUI levelReachedValueText;

        [Header("Buttons")]
        [SerializeField] private Button tryAgainButton;
        [SerializeField] private Button returnToHubButton;

        [Header("Scenes")]
        [Tooltip("Scene to load when 'Try Again' is clicked. Usually the current Game scene.")]
        [SerializeField] private string gameSceneName = "Game";

        [Tooltip("Scene to load when 'Return to Hub' is clicked. Usually EquipmentHub or character selection.")]
        [SerializeField] private string hubSceneName = "EquipmentHub";

        [Header("Diagnostics")]
        [SerializeField] private bool verbose = true;

        // ─── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            // Hidden by default — wait for OnPhaseChanged → GameOver to show.
            SetPanelVisibility(0f, interactable: false);

            if (tryAgainButton != null)
                tryAgainButton.onClick.AddListener(HandleTryAgain);
            if (returnToHubButton != null)
                returnToHubButton.onClick.AddListener(HandleReturnToHub);
        }

        private void OnEnable()
        {
            if (RunStateManager.Instance != null)
                RunStateManager.Instance.OnPhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            if (RunStateManager.Instance != null)
                RunStateManager.Instance.OnPhaseChanged -= HandlePhaseChanged;

            if (tryAgainButton != null)
                tryAgainButton.onClick.RemoveListener(HandleTryAgain);
            if (returnToHubButton != null)
                returnToHubButton.onClick.RemoveListener(HandleReturnToHub);
        }

        // ─── State change handler ─────────────────────────────────────────

        private void HandlePhaseChanged(RunStatePhase phase)
        {
            if (phase == RunStatePhase.GameOver)
            {
                ShowResults();
            }
            else if (phase == RunStatePhase.Running)
            {
                // Hide if a fresh run started (e.g., during retry transitions).
                SetPanelVisibility(0f, interactable: false);
            }
        }

        // ─── Display ──────────────────────────────────────────────────────

        private void ShowResults()
        {
            PopulateStats();
            StartCoroutine(FadeIn());
            if (verbose) Debug.Log("[ResultsScreen] Showing results.");
        }

        private void PopulateStats()
        {
            if (titleText != null)
                titleText.text = "RUN OVER";

            var tracker = RunStatsTracker.Instance;

            if (timeSurvivedValueText != null)
                timeSurvivedValueText.text = FormatTime(tracker != null ? tracker.TimeSurvived : 0f);

            if (enemiesKilledValueText != null)
                enemiesKilledValueText.text = (tracker != null ? tracker.EnemiesKilled : 0).ToString();

            if (levelReachedValueText != null)
                levelReachedValueText.text = (tracker != null ? tracker.LevelReached : 1).ToString();
        }

        private static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int wholeSeconds = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes:0}:{wholeSeconds:00}";
        }

        private IEnumerator FadeIn()
        {
            if (panel == null) yield break;

            // Fade in using unscaled time so it animates while game is paused.
            panel.interactable = true;
            panel.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                panel.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            panel.alpha = 1f;
        }

        private void SetPanelVisibility(float alpha, bool interactable)
        {
            if (panel == null) return;
            panel.alpha = alpha;
            panel.interactable = interactable;
            panel.blocksRaycasts = interactable;
        }

        // ─── Button handlers ──────────────────────────────────────────────

        private void HandleTryAgain()
        {
            if (verbose) Debug.Log($"[ResultsScreen] Try Again — reloading scene '{gameSceneName}'.");
            // RunStateManager.OnDestroy restores Time.timeScale to 1 when the
            // scene unloads, so the fresh scene loads at normal speed.
            SceneManager.LoadScene(gameSceneName);
        }

        private void HandleReturnToHub()
        {
            if (verbose) Debug.Log($"[ResultsScreen] Return to Hub — loading scene '{hubSceneName}'.");
            SceneManager.LoadScene(hubSceneName);
        }
    }
}
