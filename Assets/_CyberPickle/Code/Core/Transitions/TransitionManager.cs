using UnityEngine;
using System.Collections;
using CyberPickle.Core.Management;
using CyberPickle.Core.Events;
using CyberPickle.Core.States;

namespace CyberPickle.Core.Transitions
{
    public class TransitionManager : Manager<TransitionManager>
    {
        [Header("Scene References")]
        [SerializeField] private GameObject menuArea;
        [SerializeField] private GameObject characterSelectArea;
        [SerializeField] private Light bonfireLight;
        [SerializeField] private float maxBonfireIntensity = 2f;

        [Header("Transition Settings")]
        [SerializeField] private float transitionDuration = 1f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Coroutine currentTransition;
        private GameState currentState;

        protected override void OnManagerAwake()
        {
            base.OnManagerAwake();
            ValidateReferences();
            InitializeScene();
        }

        private void ValidateReferences()
        {
            if (characterSelectArea == null)
                Debug.LogError("[TransitionManager] Character select area is missing!");

            if (bonfireLight == null)
                Debug.LogError("[TransitionManager] Bonfire light is missing!");
        }

        private void InitializeScene()
        {
            Debug.Log("[TransitionManager] Initializing scene");

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
            if (characterSelectArea != null)
            {
                characterSelectArea.SetActive(true);
                Debug.Log("[TransitionManager] Character select area activated");
            }

            // Fade in the bonfire light
            if (bonfireLight != null)
            {
                yield return StartCoroutine(AnimateBonfireLight(maxBonfireIntensity, transitionDuration));
            }

            GameEvents.OnCameraTransitionComplete.Invoke();
        }

        private IEnumerator MainMenuTransitionRoutine()
        {
            if (menuArea != null)
                menuArea.SetActive(true);

            // Fade out bonfire light
            if (bonfireLight != null)
            {
                yield return StartCoroutine(AnimateBonfireLight(0f, transitionDuration));
            }

            if (characterSelectArea != null)
                characterSelectArea.SetActive(false);

            Debug.Log("[TransitionManager] Main menu transition complete");
        }

        private IEnumerator AnimateBonfireLight(float targetIntensity, float duration)
        {
            if (bonfireLight == null) yield break;

            float startIntensity = bonfireLight.intensity;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                // Add a null check for bonfireLight inside the loop
                if (bonfireLight == null) yield break;

                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / duration;
                float curveValue = transitionCurve.Evaluate(normalizedTime);

                bonfireLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, curveValue);
                yield return null;
            }

            // Final check before setting intensity
            if (bonfireLight != null)
            {
                bonfireLight.intensity = targetIntensity;
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