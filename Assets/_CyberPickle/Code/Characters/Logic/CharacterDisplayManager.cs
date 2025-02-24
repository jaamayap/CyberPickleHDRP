using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using CyberPickle.Core.Management;
using CyberPickle.Core.Events;
using CyberPickle.Characters.Data;
using DG.Tweening;
using UnityEngine.EventSystems;

namespace CyberPickle.Characters.Logic
{
    /// <summary>
    /// Manages character visualization and interaction infrastructure in the selection screen.
    /// Responsible for spawning character models, controlling animations,
    /// applying materials, and handling spotlight logic (purely visual aspects).
    /// </summary>
    public class CharacterDisplayManager : Manager<CharacterDisplayManager>
    {
        #region Serialized Fields

        [Header("Animation Settings")]
        [SerializeField] private float transitionDuration = 0.5f;
        [SerializeField] private float idleAnimationSpeed = 1f;
        [SerializeField] private float hoverAnimationSpeed = 1.2f;

        [Header("Visual Effects")]
        [SerializeField] private float highlightIntensity = 1.5f;
        [SerializeField] private float dimmedIntensity = 0.5f;
        [SerializeField] private Material lockedMaterial;

        [Header("Spotlight Settings")]
        [SerializeField] private float spotlightIntensity = 5000f;
        [SerializeField] private float spotlightRange = 10f;
        [SerializeField] private float spotlightAngle = 30f;

        #endregion

        #region Private Fields

        private Dictionary<GameObject, Animator> characterAnimators = new Dictionary<GameObject, Animator>();
        private Dictionary<GameObject, Material[]> originalMaterials = new Dictionary<GameObject, Material[]>();
        private Dictionary<GameObject, SkinnedMeshRenderer> characterRenderers = new Dictionary<GameObject, SkinnedMeshRenderer>();
        private bool isInitialized;

        #endregion

        #region Manager Lifecycle

        protected override void OnManagerAwake()
        {
            base.OnManagerAwake();
            ValidateReferences();
        }

        /// <summary>
        /// Called once before use. Prepares pointer interaction and related
        /// display infrastructure.
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            SetupPointerInteraction();
            isInitialized = true;
            Debug.Log("[CharacterDisplayManager] Initialized successfully");
        }

        private void ValidateReferences()
        {
            if (lockedMaterial == null)
            {
                Debug.LogError("[CharacterDisplayManager] Locked material is not assigned!");
            }
        }

        /// <summary>
        /// Optionally set up physics raycasting on the main camera, etc.
        /// </summary>
        private void SetupPointerInteraction()
        {
            var camera = Camera.main;
            if (camera != null && camera.GetComponent<PhysicsRaycaster>() == null)
            {
                var raycaster = camera.gameObject.AddComponent<PhysicsRaycaster>();
                raycaster.eventMask = LayerMask.GetMask("Character");
                Debug.Log("[CharacterDisplayManager] Added PhysicsRaycaster to main camera");
            }

            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("Event System");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
                Debug.Log("[CharacterDisplayManager] Created EventSystem");
            }
        }

        #endregion

        #region Character Spawning and Setup

        /// <summary>
        /// Spawns and initializes a character model with visual components.
        /// </summary>
        public async Task<GameObject> SpawnCharacter(CharacterData characterData, Vector3 position, Quaternion rotation)
        {
            if (characterData == null || characterData.characterPrefab == null)
            {
                Debug.LogError("[CharacterDisplayManager] Invalid character data for spawning!");
                return null;
            }

            try
            {
                // Instantiate
                Quaternion targetRotation = Quaternion.Euler(0, 180, 0);
                GameObject characterInstance = Instantiate(characterData.characterPrefab, position, targetRotation);
                characterInstance.name = $"Character_{characterData.characterId}";

                // Cache animator, renderer, etc.
                CacheCharacterComponents(characterInstance);
                await InitializeCharacterVisuals(characterInstance, characterData);

                Debug.Log($"[CharacterDisplayManager] Spawned character: {characterData.characterId}");
                return characterInstance;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CharacterDisplayManager] Failed to spawn character: {e.Message}");
                return null;
            }
        }

        private void CacheCharacterComponents(GameObject character)
        {
            var animator = character.GetComponent<Animator>();
            if (animator != null)
            {
                characterAnimators[character] = animator;
            }

            var renderer = character.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer != null)
            {
                characterRenderers[character] = renderer;
                originalMaterials[character] = renderer.materials;
            }
        }

        private async Task InitializeCharacterVisuals(GameObject character, CharacterData characterData)
        {
            if (characterAnimators.TryGetValue(character, out var animator))
            {
                animator.SetTrigger(characterData.idleAnimationTrigger);
                animator.speed = idleAnimationSpeed;
            }
            await Task.Yield(); // Simple async yield
        }

        #endregion

        #region Visual State Management

        /// <summary>
        /// Updates the visual state (materials, animation, lighting) for a character.
        /// </summary>
        public void UpdateCharacterState(GameObject character, CharacterDisplayState state)
        {
            if (character == null) return;
            UpdateAnimation(character, state);
            UpdateMaterials(character, state);
            UpdateLighting(character, state);
        }

        private void UpdateAnimation(GameObject character, CharacterDisplayState state)
        {
            if (!characterAnimators.TryGetValue(character, out var animator)) return;

            // Reset triggers
            animator.ResetTrigger("Idle");
            animator.ResetTrigger("Dance");
            animator.ResetTrigger("Selected");
            animator.ResetTrigger("Locked");

            string charId = character.name.Replace("Character_", "");

            // Check lock status by consulting the selection manager
            bool isLocked = !CharacterSelectionManager.Instance.IsCharacterUnlocked(charId);

            if (isLocked)
            {
                // If the selection manager always passes "Hover" even for locked,
                // we can do locked hover logic here
                if (state == CharacterDisplayState.Hover)
                {
                    // E.g. "LockedHover" if your animator needs a separate state, or just "Locked"
                    animator.SetTrigger("Locked");
                    animator.speed = 0.5f;
                }
                else
                {
                    animator.SetTrigger("Idle");
                    animator.speed = idleAnimationSpeed;
                }
            }
            else
            {
                // Normal unlocked logic
                switch (state)
                {
                    case CharacterDisplayState.Idle:
                        animator.SetTrigger("Idle");
                        animator.speed = idleAnimationSpeed;
                        break;
                    case CharacterDisplayState.Hover:
                        animator.SetTrigger("Dance");
                        animator.speed = hoverAnimationSpeed;
                        break;
                    case CharacterDisplayState.Selected:
                        animator.SetTrigger("Selected");
                        animator.speed = 1f;
                        break;
                    case CharacterDisplayState.Locked:
                        // Just in case
                        animator.SetTrigger("Locked");
                        animator.speed = 0.5f;
                        break;
                }
            }
        }


        private void UpdateMaterials(GameObject character, CharacterDisplayState state)
        {
            if (!characterRenderers.TryGetValue(character, out var renderer) ||
                !originalMaterials.ContainsKey(character))
            {
                return;
            }

            // 1) Figure out if it’s truly locked
            string charId = character.name.Replace("Character_", "");
            bool isLocked = !CharacterSelectionManager.Instance.IsCharacterUnlocked(charId);

            // 2) If locked, apply lockedMaterial
            if (isLocked && lockedMaterial != null)
            {
                var lockedMats = new Material[renderer.materials.Length];
                for (int i = 0; i < lockedMats.Length; i++)
                {
                    lockedMats[i] = lockedMaterial;
                }
                renderer.materials = lockedMats;
            }
            else
            {
                // 3) Otherwise, normal logic
                float intensity = (state == CharacterDisplayState.Hover || state == CharacterDisplayState.Selected)
                    ? highlightIntensity
                    : 1f;

                var currentMaterials = originalMaterials[character];
                foreach (var mat in currentMaterials)
                {
                    if (mat.HasProperty("_EmissionIntensity"))
                    {
                        mat.SetFloat("_EmissionIntensity", intensity);
                    }
                }
                renderer.materials = currentMaterials;
            }
        }

        private void UpdateLighting(GameObject character, CharacterDisplayState state)
        {
            float targetIntensity = (state == CharacterDisplayState.Hover || state == CharacterDisplayState.Selected)
                ? highlightIntensity : dimmedIntensity;

            var characterLights = character.GetComponentsInChildren<Light>();
            foreach (var light in characterLights)
            {
                light.intensity = targetIntensity;
            }
        }

        #endregion

        #region Spotlight Control

        /// <summary>
        /// Rotates the given spotlight to a target rotation over time.
        /// Adjust intensity, range, angle, etc. as needed.
        /// </summary>
        public async Task RotateSpotlight(Light spotlight, float targetRotation, float duration)
        {
            if (spotlight == null) return;

            try
            {
                Vector3 currentRotation = spotlight.transform.localEulerAngles;
                Vector3 targetRotationVector = new Vector3(currentRotation.x, targetRotation, currentRotation.z);

                await spotlight.transform
                    .DOLocalRotate(targetRotationVector, duration)
                    .SetEase(Ease.InOutQuad)
                    .AsyncWaitForCompletion();

                spotlight.intensity = spotlightIntensity;
                spotlight.range = spotlightRange;
                spotlight.spotAngle = spotlightAngle;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CharacterDisplayManager] Failed to rotate spotlight: {e.Message}");
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Clears all cached character data and resets internal state.
        /// </summary>
        public void Cleanup()
        {
            characterAnimators.Clear();
            characterRenderers.Clear();
            originalMaterials.Clear();
            isInitialized = false;
        }

        protected override void OnManagerDestroyed()
        {
            base.OnManagerDestroyed();
            Cleanup();
        }

        #endregion
    }

    /// <summary>
    /// Defines display states used by CharacterDisplayManager.
    /// Typically, "Locked" is used if the character is not unlocked in the profile.
    /// </summary>
    public enum CharacterDisplayState
    {
        Hidden,
        Locked,
        Idle,
        Hover,
        Selected
    }
}
