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
        // Scene-bound: every camera reference, post-process volume, and pose
        // Transform below lives in the MainMenu scene. When that scene unloads
        // (e.g., the user enters EquipmentHub), the references die. Persisting
        // this manager across scenes leaves a zombie that throws
        // MissingReferenceException the next time OnGameStateChanged fires.
        protected override bool PersistAcrossScenes => false;

        [Header("Camera References")]
        [Tooltip("The Unity Camera moved by this manager. If null at Awake, falls back to Camera.main.")]
        [SerializeField] private UnityEngine.Camera mainCamera;
        [Tooltip("Pose the camera sits at while the user is in the main-menu / profile-select / press-any-button screens.")]
        [SerializeField] private Transform menuCameraPosition;
        [Tooltip("Pose the camera sits at while the user is on the character-select screen (wide overhead shot of all characters).")]
        [SerializeField] private Transform characterSelectCameraPosition;

        [Header("Default Transition (menu-return + focus moves)")]
        [Tooltip("Duration (seconds) of the camera move BACK from character-select to the main menu (the back button). NOT used for the entry-into-character-select transition — that has its own duration below. Default: 1.5s.")]
        [SerializeField] private float transitionDuration = 1.5f;
        [Tooltip("Ease curve shared by every 'normal' camera move:\n  • TransitionToMainMenu (back button)\n  • FocusCameraOnCharacter (zoom in on a clicked character)\n  • ResetToDefaultPosition / ResetToCharacterSelectionView (zoom back out)\n\nNOT used for the entry-into-character-select transition — that has its own slow-start curve below. Default: symmetric EaseInOut.")]
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Character Select Entry Transition (Start button)")]
        [Tooltip("Total duration (seconds) of the camera move FROM the menu pose INTO the character-select pose, fired when the user clicks Start. Intentionally longer than the default transition above so the slow-start phase has room to breathe. NOT used for any other camera move. Default: 2.0s.")]
        [SerializeField] private float characterSelectEntryDuration = 2.0f;
        [Tooltip("Slow-start ease curve for the camera move INTO character-select (Start click). Default: barely moves for the first ~40% of duration, then accelerates to deliver the user smoothly to the destination. The slow start visually masks per-frame Instantiate / shader-compile hitches that may happen concurrently — the eye reads slow camera motion as smooth even when the underlying frame time is uneven. NOT used for any other camera move.")]
        [SerializeField] private AnimationCurve characterSelectEntryCurve;

        [Header("Idle Animation Settings")]
        [Tooltip("Vertical bob amplitude of the camera while idling at the menu pose.")]
        [SerializeField] private float menuIdleAmplitude = 0.1f;
        [Tooltip("Vertical bob frequency of the camera while idling at the menu pose (Hz, roughly).")]
        [SerializeField] private float menuIdleFrequency = 1f;
        [Tooltip("Vertical bob amplitude while idling at the character-select pose.")]
        [SerializeField] private float characterSelectIdleAmplitude = 0.2f;
        [Tooltip("Vertical bob frequency while idling at the character-select pose (Hz, roughly).")]
        [SerializeField] private float characterSelectIdleFrequency = 0.5f;

        [Header("Post Processing")]
        [Tooltip("Optional post-processing Volume that's blended in for the menu pose. Cross-fades against characterSelectVolume in SetPostProcessingBlend.")]
        [SerializeField] private Volume menuVolume;
        [Tooltip("Optional post-processing Volume that's blended in for the character-select pose. Cross-fades against menuVolume in SetPostProcessingBlend.")]
        [SerializeField] private Volume characterSelectVolume;

        [Header("Idle Animation Toggles")]
        [Tooltip("If false, the camera holds dead-still at the menu pose. Default: false (avoids constant subtle motion behind UI).")]
        [SerializeField] private bool enableMenuIdleAnimation = false;
        [Tooltip("If true, the camera bobs gently while sitting at the character-select pose. Default: true (gives the scene some life).")]
        [SerializeField] private bool enableCharacterSelectIdleAnimation = true;

        [Header("Camera Settings")]
        [Tooltip("Field of view restored at the start of every transition (focus zooms can override it temporarily). Default: 60.")]
        [SerializeField] private float defaultFieldOfView = 60f;
        public Transform CharacterSelectCameraPosition => characterSelectCameraPosition;

        private Coroutine currentTransition;
        private Coroutine idleAnimationCoroutine;
        private Vector3 cameraVelocity;
        private Vector3 rotationVelocity;

        // Tracks whether HandleGameStateChanged has run at least once on this
        // (scene-bound) instance. On a fresh boot the first state change is
        // MainMenu (from ProfileSelection completion); on a return from
        // EquipmentHub the post-load broadcast fires CharacterSelect FIRST.
        // The first-event flag is used as a fallback signal — but the primary
        // mechanism for the "return" case is GameManager.PendingTargetState,
        // which lets us pre-position the camera in InitializeCamera so the
        // first rendered frame is already correct.
        private bool hasHandledFirstStateChange;

        protected override void OnManagerAwake()
        {
            base.OnManagerAwake();

            if (mainCamera == null)
            {
                mainCamera = UnityEngine.Camera.main;
                Debug.Log("[CameraManager] Main camera assigned automatically");
            }

            EnsureCharacterSelectEntryCurveDefault();
            ValidateReferences();
            InitializeCamera();
        }

        /// <summary>
        /// Builds the slow-start curve at runtime if the inspector value is
        /// empty (default for fresh component). The shape: nearly flat for
        /// the first 40% of duration, gentle ramp to ~10% progress at 50%,
        /// then accelerating ease-out to 100%. SmoothTangents at every key
        /// gives a continuous, natural-feeling motion profile.
        /// </summary>
        private void EnsureCharacterSelectEntryCurveDefault()
        {
            if (characterSelectEntryCurve != null && characterSelectEntryCurve.length >= 2) return;

            characterSelectEntryCurve = new AnimationCurve(
                new Keyframe(0f,    0f),
                new Keyframe(0.45f, 0.06f),
                new Keyframe(0.75f, 0.55f),
                new Keyframe(1f,    1f)
            );
            for (int i = 0; i < characterSelectEntryCurve.keys.Length; i++)
            {
                characterSelectEntryCurve.SmoothTangents(i, 0f);
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
            if (mainCamera == null) return;

            // Pre-position the camera at the pose that matches the incoming
            // state, BEFORE the first frame is rendered. Without this, on
            // return-from-EquipmentHub the camera Awakes at menuCameraPosition,
            // renders one frame at the wrong pose, then "snaps" to
            // characterSelectCameraPosition — the user sees a jarring cut.
            // GameManager.PendingTargetState exposes what state the next
            // OnGameStateChanged broadcast will deliver; we use it here.
            Transform initialPose = ResolveInitialCameraPose();
            if (initialPose != null)
            {
                mainCamera.transform.position = initialPose.position;
                mainCamera.transform.rotation = initialPose.rotation;
            }
            mainCamera.fieldOfView = defaultFieldOfView;

            // Idle animations only make sense at the menu pose for now —
            // CharacterSelect idle is started by SnapToCharacterSelect /
            // TransitionToCharacterSelect at the end of those moves.
            bool startedAtMenuPose = initialPose == menuCameraPosition;
            if (startedAtMenuPose && enableMenuIdleAnimation)
            {
                StartMenuIdleAnimation();
            }
            else if (initialPose == characterSelectCameraPosition && enableCharacterSelectIdleAnimation)
            {
                // Returning from a downstream scene — start the character-select
                // idle immediately since the camera is already at its destination.
                StartCharacterSelectIdleAnimation();
            }
        }

        /// <summary>
        /// Resolves which authored pose the camera should snap to on Awake.
        /// Returns characterSelectCameraPosition when GameManager has a
        /// pending CharacterSelect target (returning from EquipmentHub etc.);
        /// otherwise menuCameraPosition (fresh launch / direct load).
        /// </summary>
        private Transform ResolveInitialCameraPose()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.PendingTargetState == GameState.CharacterSelect && characterSelectCameraPosition != null)
            {
                return characterSelectCameraPosition;
            }
            return menuCameraPosition;
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
            // First event on a fresh (scene-bound) CameraManager + state ==
            // CharacterSelect means we're returning from a downstream scene.
            // The camera was already pre-positioned at characterSelectCameraPosition
            // by InitializeCamera (via GameManager.PendingTargetState), so the
            // "snap" is a visual no-op — its purpose now is just to start the
            // idle animation and broadcast OnCameraTransitionComplete.
            bool isFirstChange = !hasHandledFirstStateChange;
            hasHandledFirstStateChange = true;

            if (newState == GameState.CharacterSelect)
            {
                if (isFirstChange)
                {
                    SnapToCharacterSelect();
                }
                else
                {
                    TransitionToCharacterSelect();
                }
                return;
            }

            switch (newState)
            {
                case GameState.MainMenu:
                    TransitionToMainMenu();
                    break;
            }
        }

        /// <summary>
        /// Instant version of TransitionToCharacterSelect. Used on the first
        /// state change after a scene reload that targeted CharacterSelect
        /// (returning from EquipmentHub). InitializeCamera has already placed
        /// the camera at characterSelectCameraPosition; this method just
        /// starts the idle animation and broadcasts the transition-complete
        /// event for any downstream listeners.
        /// </summary>
        private void SnapToCharacterSelect()
        {
            Debug.Log("[CameraManager] Snapping to character-select pose (returning from a downstream scene).");
            StopIdleAnimation();
            if (currentTransition != null)
            {
                StopCoroutine(currentTransition);
                currentTransition = null;
            }

            if (mainCamera != null && characterSelectCameraPosition != null)
            {
                mainCamera.transform.position = characterSelectCameraPosition.position;
                mainCamera.transform.rotation = characterSelectCameraPosition.rotation;
            }

            StartCharacterSelectIdleAnimation();
            GameEvents.OnCameraTransitionComplete.Invoke();
        }

        private void TransitionToCharacterSelect()
        {
            Debug.Log("[CameraManager] Starting slow-start transition to character select");
            StopIdleAnimation();

            if (currentTransition != null)
                StopCoroutine(currentTransition);

            // Slow-start curve + extended duration. The camera barely moves
            // for the first ~40% of the duration, which gives
            // CharacterSelectionManager time to spawn its 4 characters with
            // their per-frame Instantiate hitches concealed by the gentle
            // motion. After the slow phase, the curve accelerates to deliver
            // the user smoothly to the character-select pose.
            currentTransition = StartCoroutine(TransitionCameraRoutine(
                characterSelectCameraPosition.position,
                characterSelectCameraPosition.rotation,
                characterSelectEntryCurve,
                characterSelectEntryDuration,
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
                transitionCurve,
                transitionDuration,
                () => {
                    Debug.Log("[CameraManager] Main menu transition complete");
                    StartMenuIdleAnimation();
                    GameEvents.OnCameraTransitionComplete.Invoke();
                }
            ));
        }

        private IEnumerator TransitionCameraRoutine(Vector3 targetPosition, Quaternion targetRotation, AnimationCurve curve, float duration, System.Action onComplete = null)
        {
            float elapsedTime = 0f;
            Vector3 startPosition = mainCamera.transform.position;
            Quaternion startRotation = mainCamera.transform.rotation;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / duration;
                float curveValue = curve.Evaluate(normalizedTime);

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
            if (!enableCharacterSelectIdleAnimation)
            {
                Debug.Log("[CameraManager] Character select idle animation disabled");
                return;
            }

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