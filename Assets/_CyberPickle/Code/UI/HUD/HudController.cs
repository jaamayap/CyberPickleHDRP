// File: Assets/_CyberPickle/Code/UI/HUD/HudController.cs
// Namespace: CyberPickle.UI.HUD
//
// Top-level orchestrator for the in-run HUD. Owns the root CanvasGroup
// and shows / hides the HUD based on the active run phase:
//   - Loading      → hidden (player not spawned yet)
//   - Running      → visible
//   - LevelUpPaused → visible (overlay sits on top, HUD stays as backdrop)
//   - Paused       → visible
//   - GameOver     → faded out (results screen takes over)
//
// Why a CanvasGroup instead of SetActive: keeps every HUD child component
// alive across phase transitions, so subscriptions don't churn and timers
// don't reset. Visibility is purely an alpha + raycasts toggle.
//
// Sub-components (HealthBarUI, XpBarUI, etc.) self-wire to their data
// sources — this controller just owns lifecycle.

using System.Collections;
using UnityEngine;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.RunState;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    public class HudController : MonoBehaviour
    {
        [Header("Visibility")]
        [Tooltip("CanvasGroup driving the HUD's alpha + raycasts. Should sit on the same GameObject or a parent. Required.")]
        [SerializeField] private CanvasGroup hudGroup;

        [Tooltip("Seconds to fade in/out on phase changes. Uses unscaled time so fades work during pause states.")]
        [Min(0f)] [SerializeField] private float fadeDuration = 0.25f;

        [Header("Diagnostics")]
        [SerializeField] private bool verbose;

        private Coroutine _activeFade;

        private void Awake()
        {
            // Hidden until RunStart fires.
            if (hudGroup != null)
            {
                hudGroup.alpha = 0f;
                hudGroup.interactable = false;
                hudGroup.blocksRaycasts = false;
            }
        }

        private void OnEnable()
        {
            // Listen on the bus rather than RunStateManager directly: bus
            // subscriptions don't care about scene-init order, and the HUD
            // canvas may be authored in the scene before the manager exists.
            MusicEventBus.OnEvent += HandleMusicEvent;
        }

        private void OnDisable()
        {
            MusicEventBus.OnEvent -= HandleMusicEvent;
        }

        private void HandleMusicEvent(MusicEvent type, object payload)
        {
            switch (type)
            {
                case MusicEvent.RunStart:
                    ShowHud();
                    break;

                case MusicEvent.RunEnd:
                    HideHud();
                    break;

                case MusicEvent.PhaseChanged:
                    if (payload is RunStatePhase phase)
                        OnPhaseChanged(phase);
                    break;
            }
        }

        private void OnPhaseChanged(RunStatePhase phase)
        {
            // Run-end already hides via the dedicated RunEnd event; everything
            // else (Running / LevelUpPaused / Paused) stays visible. Loading
            // is the pre-spawn state — hide.
            switch (phase)
            {
                case RunStatePhase.Loading:
                    HideHud();
                    break;
            }
        }

        private void ShowHud()
        {
            if (verbose) Debug.Log("[HudController] Showing HUD.");
            StartFade(targetAlpha: 1f, interactive: true);
        }

        private void HideHud()
        {
            if (verbose) Debug.Log("[HudController] Hiding HUD.");
            StartFade(targetAlpha: 0f, interactive: false);
        }

        private void StartFade(float targetAlpha, bool interactive)
        {
            if (hudGroup == null) return;
            if (_activeFade != null) StopCoroutine(_activeFade);
            _activeFade = StartCoroutine(FadeTo(targetAlpha, interactive));
        }

        private IEnumerator FadeTo(float targetAlpha, bool interactive)
        {
            // Set raycast/interactive immediately on show so clicks land
            // during the fade-in. On hide, defer until alpha reaches 0 so
            // the player can still read the dying HUD.
            if (interactive)
            {
                hudGroup.interactable = true;
                hudGroup.blocksRaycasts = true;
            }

            float startAlpha = hudGroup.alpha;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                hudGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
                yield return null;
            }
            hudGroup.alpha = targetAlpha;

            if (!interactive)
            {
                hudGroup.interactable = false;
                hudGroup.blocksRaycasts = false;
            }

            _activeFade = null;
        }
    }
}
