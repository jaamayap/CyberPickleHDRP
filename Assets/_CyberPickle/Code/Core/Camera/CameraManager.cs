using UnityEngine;
using System.Collections;
using CyberPickle.Core.Management;
using CyberPickle.Core.Events;
using CyberPickle.Core.States;
using UnityEngine.Rendering;
using DG.Tweening;
using System;
using System.Threading.Tasks;

namespace CyberPickle.Core.Camera
{
    public class CameraManager : Manager<CameraManager>
    {
        [Header("Camera References")]
        [SerializeField] private UnityEngine.Camera mainCamera;
        [SerializeField] private Transform menuCameraPosition;
        [SerializeField] private Transform characterSelectCameraPosition;

        [Header("Transition Settings")]
        [SerializeField] private float transitionDuration = 1.5f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Camera Animation Settings")]
        [SerializeField] private float menuIdleAmplitude = 0.1f;
        [SerializeField] private float menuIdleFrequency = 1f;
        [SerializeField] private float characterSelectIdleAmplitude = 0.2f;
        [SerializeField] private float characterSelectIdleFrequency = 0.5f;

        [Header("Post Processing")]
        [SerializeField] private Volume menuVolume;
        [SerializeField] private Volume characterSelectVolume;

        [Header("Animation Settings")]
        [SerializeField] private bool enableMenuIdleAnimation = false; // Set to false by default
        [SerializeField] private bool enableCharacterSelectIdleAnimation = true;


        [Header("Camera Settings")]
        [SerializeField] private float defaultFieldOfView = 60f;
        public Transform CharacterSelectCameraPosition => characterSelectCameraPosition;

        private Coroutine currentTransition;
        private Coroutine idleAnimationCoroutine;
        private Vector3 cameraVelocity;
        private Vector3 rotationVelocity;

        protected override void OnManagerAwake()
        {
            base.OnManagerAwake();

            if (mainCamera == null)
            {
                mainCamera = UnityEngine.Camera.main;
                Debug.Log("[CameraManager] Main camera assigned automatically");
            }

            ValidateReferences();
            InitializeCamera();
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

        protected override void OnManagerDestroyed()
        {
            base.OnManagerDestroyed();
            UnsubscribeFromEvents();
            StopAllCoroutines();
        }

        private void ValidateReferences()
        {
            if (menuCameraPosition == null)
                Debug.LogError("[CameraManager] Menu camera position is not assigned!");
            if (characterSelectCameraPosition == null)
                Debug.LogError("[CameraManager] Character select camera position is not assigned!");
        }


        private void InitializeCamera()
        {
            if (mainCamera != null && menuCameraPosition != null)
            {
                mainCamera.transform.position = menuCameraPosition.position;
                mainCamera.transform.rotation = menuCameraPosition.rotation;

                if (enableMenuIdleAnimation)
                {
                    StartMenuIdleAnimation();
                }
            }
        }

        private void SubscribeToEvents()
        {
            GameEvents.OnGameStateChanged.AddListener(HandleGameStateChanged);
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
        }

        private void HandleGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.CharacterSelect:
                    TransitionToCharacterSelect();
                    break;
                case GameState.MainMenu:
                    TransitionToMainMenu();
                    break;
            }
        }

        private void TransitionToCharacterSelect()
        {
            Debug.Log("[CameraManager] Starting transition to character select");
            StopIdleAnimation();

            if (currentTransition != null)
                StopCoroutine(currentTransition);

            currentTransition = StartCoroutine(TransitionCameraRoutine(
                characterSelectCameraPosition.position,
                characterSelectCameraPosition.rotation,
                () => {
                    Debug.Log("[CameraManager] Character select transition complete");
                    StartCharacterSelectIdleAnimation();
                    GameEvents.OnCameraTransitionComplete.Invoke();
                }
            ));
        }

        private void TransitionToMainMenu()
        {
            Debug.Log("[CameraManager] Starting transition to main menu");
            StopIdleAnimation();

            if (currentTransition != null)
                StopCoroutine(currentTransition);

            currentTransition = StartCoroutine(TransitionCameraRoutine(
                menuCameraPosition.position,
                menuCameraPosition.rotation,
                () => {
                    Debug.Log("[CameraManager] Main menu transition complete");
                    StartMenuIdleAnimation();
                    GameEvents.OnCameraTransitionComplete.Invoke();
                }
            ));
        }

        private IEnumerator TransitionCameraRoutine(Vector3 targetPosition, Quaternion targetRotation, System.Action onComplete = null)
        {
            float elapsedTime = 0f;
            Vector3 startPosition = mainCamera.transform.position;
            Quaternion startRotation = mainCamera.transform.rotation;

            while (elapsedTime < transitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / transitionDuration;
                float curveValue = transitionCurve.Evaluate(normalizedTime);

                mainCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, curveValue);
                mainCamera.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, curveValue);

                yield return null;
            }

            // Ensure we reach the exact target
            mainCamera.transform.position = targetPosition;
            mainCamera.transform.rotation = targetRotation;

            onComplete?.Invoke();
        }

        private void StartMenuIdleAnimation()
        {
            if (!enableMenuIdleAnimation)
            {
                Debug.Log("[CameraManager] Menu idle animation disabled");
                return;
            }

            StopIdleAnimation();
            idleAnimationCoroutine = StartCoroutine(MenuIdleAnimationRoutine());
        }

        private void StartCharacterSelectIdleAnimation()
        {
            StopIdleAnimation();
            idleAnimationCoroutine = StartCoroutine(CharacterSelectIdleAnimationRoutine());
        }

        private void StopIdleAnimation()
        {
            if (idleAnimationCoroutine != null)
            {
                StopCoroutine(idleAnimationCoroutine);
                idleAnimationCoroutine = null;
            }
        }

        private IEnumerator MenuIdleAnimationRoutine()
        {
            Vector3 startPosition = mainCamera.transform.position;

            while (true)
            {
                float time = Time.time;
                Vector3 newPosition = startPosition;
                newPosition.y += Mathf.Sin(time * menuIdleFrequency) * menuIdleAmplitude;

                mainCamera.transform.position = newPosition;
                yield return null;
            }
        }

        private IEnumerator CharacterSelectIdleAnimationRoutine()
        {
            Vector3 startPosition = mainCamera.transform.position;

            while (true)
            {
                float time = Time.time;
                Vector3 newPosition = startPosition;
                newPosition.y += Mathf.Sin(time * characterSelectIdleFrequency) * characterSelectIdleAmplitude;

                mainCamera.transform.position = newPosition;
                yield return null;
            }
        }

        public void SetPostProcessingBlend(float blend)
        {
            if (menuVolume != null)
                menuVolume.weight = 1 - blend;

            if (characterSelectVolume != null)
                characterSelectVolume.weight = blend;
        }

        public async Task FocusCameraOnCharacter(
     Transform characterTransform,
     float cameraHeightOffset,
     float focusDistance,
     float transitionDuration,
     float targetFOV,
     float lookOffset 
 )
        {
            if (characterTransform == null) return;

            StopIdleAnimation();

            // Position the camera behind & above the character
            Vector3 targetPosition = new Vector3(
                characterTransform.position.x,
                characterTransform.position.y + cameraHeightOffset,
                characterTransform.position.z + focusDistance
            );

            // Shift the look-at point higher than the character's pivot
            Vector3 lookAtPoint = characterTransform.position + Vector3.up * lookOffset;

            // Now calculate rotation toward that higher lookAtPoint
            Vector3 directionToCharacter = lookAtPoint - targetPosition;
            Quaternion targetRotation = Quaternion.LookRotation(directionToCharacter);

            // Move, rotate & set FOV
            await TransitionToPosition(
                targetPosition,
                targetRotation,
                transitionDuration,
                targetFOV
            );
        }

        public async Task TransitionToPosition(Vector3 targetPosition, Quaternion targetRotation, float duration, float targetFOV)
        {
            if (mainCamera == null)
            {
                Debug.LogError("[CameraManager] Main camera is null!");
                return;
            }

            Debug.Log($"[CameraManager] Starting camera transition to position: {targetPosition}");

            try
            {
                // Store original values for potential reset
                Vector3 originalPosition = mainCamera.transform.position;
                Quaternion originalRotation = mainCamera.transform.rotation;
                float originalFOV = mainCamera.fieldOfView;

                // Create a sequence for synchronized animations
                Sequence cameraSequence = DOTween.Sequence();

                // Add position transition
                cameraSequence.Join(mainCamera.transform.DOMove(targetPosition, duration)
                    .SetEase(transitionCurve));

                // Add rotation transition
                cameraSequence.Join(mainCamera.transform.DORotateQuaternion(targetRotation, duration)
                    .SetEase(transitionCurve));

                // Add FOV transition
                cameraSequence.Join(DOTween.To(() => mainCamera.fieldOfView,
                    x => mainCamera.fieldOfView = x,
                    targetFOV,
                    duration)
                    .SetEase(transitionCurve));

                // Wait for sequence completion
                await cameraSequence.AsyncWaitForCompletion();

                Debug.Log("[CameraManager] Camera transition completed successfully");
                GameEvents.OnCameraTransitionComplete.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CameraManager] Error during camera transition: {ex.Message}");
                throw;
            }
        }

        public async Task ResetToDefaultPosition(float duration, float newFOV)
        {
            if (mainCamera == null || menuCameraPosition == null)
            {
                Debug.LogError("[CameraManager] Required references are null!");
                return;
            }

            Debug.Log($"[CameraManager] Resetting camera to default with custom FOV {newFOV}");

            try
            {
                Vector3 defaultPosition = menuCameraPosition.position;
                Quaternion defaultRotation = menuCameraPosition.rotation;

                // Create the sequence
                Sequence resetSequence = DOTween.Sequence();

                // Move & rotate camera
                resetSequence.Join(
                    mainCamera.transform.DOMove(defaultPosition, duration)
                        .SetEase(transitionCurve)
                );
                resetSequence.Join(
                    mainCamera.transform.DORotateQuaternion(defaultRotation, duration)
                        .SetEase(transitionCurve)
                );

                // If you want to animate the FOV along with position:
                resetSequence.Join(
                    DOTween.To(
                        () => mainCamera.fieldOfView,
                        x => mainCamera.fieldOfView = x,
                        newFOV,
                        duration
                    ).SetEase(transitionCurve)
                );

                // Wait for it to finish
                await resetSequence.AsyncWaitForCompletion();

                Debug.Log("[CameraManager] Camera reset completed successfully");
                GameEvents.OnCameraTransitionComplete.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CameraManager] Error during camera reset: {ex.Message}");
                throw;
            }


        }
        public async Task ResetToCharacterSelectionView(float duration, float newFOV)
        {
            if (mainCamera == null || characterSelectCameraPosition == null)
            {
                Debug.LogError("[CameraManager] Required references are null!");
                return;
            }

            // Move & rotate camera to the selection vantage
            Sequence seq = DOTween.Sequence();
            seq.Join(mainCamera.transform.DOMove(characterSelectCameraPosition.position, duration));
            seq.Join(mainCamera.transform.DORotateQuaternion(characterSelectCameraPosition.rotation, duration));

            // Adjust FOV (optional)
            seq.Join(DOTween.To(() => mainCamera.fieldOfView,
                                x => mainCamera.fieldOfView = x,
                                newFOV,
                                duration));

            await seq.AsyncWaitForCompletion();
            GameEvents.OnCameraTransitionComplete.Invoke();
        }
    }
}