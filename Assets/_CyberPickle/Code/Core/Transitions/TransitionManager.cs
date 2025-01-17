using UnityEngine;
using UnityEngine.VFX;
using System.Collections;
using CyberPickle.Core.Management;
using CyberPickle.Core.Events;
using CyberPickle.Core.States;

namespace CyberPickle.Core.Transitions
{
    public class TransitionManager : Manager<TransitionManager>
    {
        [Header("VFX References")]
        [SerializeField] private VisualEffect smokeVFX;
        [SerializeField] private VisualEffect characterRevealVFX;

        [Header("Transition Settings")]
        [SerializeField] private float smokeTransitionDuration = 2f;
        [SerializeField] private float revealTransitionDuration = 1f;
        [SerializeField] private AnimationCurve smokeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Smoke Settings")]
        [SerializeField] private float maxSmokeIntensity = 50f;
        [SerializeField] private float minSmokeIntensity = 5f;
        private const string SMOKE_DENSITY_PARAM = "SmokeDensity"; // Updated parameter name
        private const string SPAWN_RATE_PARAM = "SpawnRate";       // Added spawn rate parameter

        [Header("Scene References")]
        [SerializeField] private GameObject menuArea;
        [SerializeField] private GameObject characterSelectArea;
        [SerializeField] private Light bonfireLight;
        [SerializeField] private float maxBonfireIntensity = 2f;

        private Coroutine currentTransition;
        private float currentSmokeIntensity;
        private GameState currentState;

        protected override void OnManagerAwake()
        {
            base.OnManagerAwake();
            ValidateReferences();
            InitializeScene();
        }

        private void ValidateReferences()
        {
            if (smokeVFX == null)
                Debug.LogError("[TransitionManager] Smoke VFX is not assigned!");
            if (characterRevealVFX == null)
                Debug.LogError("[TransitionManager] Character Reveal VFX is not assigned!");
        }

        private void InitializeScene()
        {
            Debug.Log("[TransitionManager] Initializing scene");

            // Set initial states
            if (smokeVFX != null)
            {
                smokeVFX.SetFloat(SMOKE_DENSITY_PARAM, maxSmokeIntensity);
                smokeVFX.SetFloat(SPAWN_RATE_PARAM, maxSmokeIntensity);
                currentSmokeIntensity = maxSmokeIntensity;
            }

            if (characterSelectArea != null)
            {
                characterSelectArea.SetActive(false);
                Debug.Log("[TransitionManager] Character select area hidden");
            }

            if (bonfireLight != null)
            {
                bonfireLight.intensity = 0f;
                Debug.Log("[TransitionManager] Bonfire light initialized");
            }
        }

        protected override void OnManagerEnabled()
        {
            base.OnManagerEnabled();
            SubscribeToEvents();
        }

        protected override void OnManagerDisabled()
        {
            base.OnManagerDisabled();
            UnsubscribeFromEvents();
            StopAllCoroutines();
        }

        private void SubscribeToEvents()
        {
            GameEvents.OnGameStateChanged.AddListener(HandleGameStateChanged);
            GameEvents.OnCameraTransitionComplete.AddListener(HandleCameraTransitionComplete);
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
            GameEvents.OnCameraTransitionComplete.RemoveListener(HandleCameraTransitionComplete);
        }

        private void HandleGameStateChanged(GameState newState)
        {
            currentState = newState;

            switch (newState)
            {
                case GameState.CharacterSelect:
                    StartCharacterSelectTransition();
                    break;
                case GameState.MainMenu:
                    StartMainMenuTransition();
                    break;
            }
        }

        private void HandleCameraTransitionComplete()
        {
            Debug.Log("[TransitionManager] Camera transition complete");

            // Additional effects or state changes after camera movement based on current state
            switch (currentState)
            {
                case GameState.CharacterSelect:
                    // Maybe trigger some additional VFX or lighting changes
                    if (bonfireLight != null)
                    {
                        StartCoroutine(AnimateBonfireLight(maxBonfireIntensity, 0.5f));
                    }
                    break;

                case GameState.MainMenu:
                    // Return to default state or trigger menu-specific effects
                    if (bonfireLight != null)
                    {
                        StartCoroutine(AnimateBonfireLight(0f, 0.5f));
                    }
                    break;
            }
        }

        private void StartCharacterSelectTransition()
        {
            Debug.Log("[TransitionManager] Starting character select transition");

            if (currentTransition != null)
                StopCoroutine(currentTransition);

            currentTransition = StartCoroutine(CharacterSelectTransitionRoutine());
        }

        private void StartMainMenuTransition()
        {
            Debug.Log("[TransitionManager] Starting main menu transition");

            if (currentTransition != null)
                StopCoroutine(currentTransition);

            currentTransition = StartCoroutine(MainMenuTransitionRoutine());
        }

        private IEnumerator CharacterSelectTransitionRoutine()
        {
            // Activate character select area
            if (characterSelectArea != null)
                characterSelectArea.SetActive(true);

            // First increase smoke to hide transition
            yield return StartCoroutine(AnimateSmokeIntensity(maxSmokeIntensity * 1.5f, smokeTransitionDuration * 0.3f));

            // Fade in the bonfire light
            if (bonfireLight != null)
            {
                yield return StartCoroutine(AnimateBonfireLight(maxBonfireIntensity, smokeTransitionDuration));
            }

            // Clear smoke to reveal character select
            yield return StartCoroutine(AnimateSmokeIntensity(minSmokeIntensity, smokeTransitionDuration * 0.7f));

            // Deactivate menu area if needed
            if (menuArea != null)
                menuArea.SetActive(false);

            Debug.Log("[TransitionManager] Character select transition complete");
        }

        private IEnumerator MainMenuTransitionRoutine()
        {
            // Activate menu area
            if (menuArea != null)
                menuArea.SetActive(true);

            // Increase smoke to hide transition
            yield return StartCoroutine(AnimateSmokeIntensity(maxSmokeIntensity, smokeTransitionDuration * 0.3f));

            // Fade out bonfire light
            if (bonfireLight != null)
            {
                yield return StartCoroutine(AnimateBonfireLight(0f, smokeTransitionDuration * 0.3f));
            }

            // Deactivate character select area
            if (characterSelectArea != null)
                characterSelectArea.SetActive(false);

            Debug.Log("[TransitionManager] Main menu transition complete");
        }

        private IEnumerator AnimateSmokeIntensity(float targetIntensity, float duration)
        {
            if (smokeVFX == null) yield break;

            float startIntensity = currentSmokeIntensity;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / duration;
                float curveValue = smokeCurve.Evaluate(normalizedTime);

                currentSmokeIntensity = Mathf.Lerp(startIntensity, targetIntensity, curveValue);
                smokeVFX.SetFloat(SMOKE_DENSITY_PARAM, currentSmokeIntensity);
                smokeVFX.SetFloat(SPAWN_RATE_PARAM, currentSmokeIntensity);

                yield return null;
            }

            currentSmokeIntensity = targetIntensity;
            smokeVFX.SetFloat(SMOKE_DENSITY_PARAM, targetIntensity);
            smokeVFX.SetFloat(SPAWN_RATE_PARAM, targetIntensity);
        }

        private IEnumerator AnimateBonfireLight(float targetIntensity, float duration)
        {
            if (bonfireLight == null) yield break;

            float startIntensity = bonfireLight.intensity;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / duration;
                float curveValue = smokeCurve.Evaluate(normalizedTime);

                bonfireLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, curveValue);
                yield return null;
            }

            bonfireLight.intensity = targetIntensity;
        }

        public void TriggerCharacterRevealEffect(Vector3 position)
        {
            if (characterRevealVFX != null)
            {
                characterRevealVFX.transform.position = position;
                characterRevealVFX.Play();
            }
        }

        protected override void OnManagerDestroyed()
        {
            base.OnManagerDestroyed();
            UnsubscribeFromEvents();
            StopAllCoroutines();
        }
    }
}