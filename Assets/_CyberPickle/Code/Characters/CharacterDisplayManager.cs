// File: Assets/_CyberPickle/Code/Characters/CharacterDisplayManager.cs
//
// Purpose: Handles the visual presentation of characters in the selection screen,
// including model instantiation, animation control, material management, and 
// interaction setup. Responsible for all display-related functionality and pointer
// interaction infrastructure.
//
// Created: 2024-02-11
// Updated: 2024-02-11

using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using CyberPickle.Core.Management;
using CyberPickle.Core.Events;
using CyberPickle.Characters.Data;
using DG.Tweening;
using UnityEngine.EventSystems;

namespace CyberPickle.Characters
{
    /// <summary>
    /// Manages character visualization and interaction infrastructure in the selection screen.
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
        [SerializeField] private float spotlightIntensity = 2f;
        [SerializeField] private float spotlightRange = 10f;
        [SerializeField] private float spotlightAngle = 30f;

        #endregion

        #region Private Fields

        // Component caches for performance
        private Dictionary<GameObject, Animator> characterAnimators = new Dictionary<GameObject, Animator>();
        private Dictionary<GameObject, Material[]> originalMaterials = new Dictionary<GameObject, Material[]>();
        private Dictionary<GameObject, SkinnedMeshRenderer> characterRenderers = new Dictionary<GameObject, SkinnedMeshRenderer>();
        private bool isInitialized;

        #endregion

        #region Initialization

        protected override void OnManagerAwake()
        {
            base.OnManagerAwake();
            ValidateReferences();
        }

        /// <summary>
        /// Initializes the display manager and sets up interaction infrastructure
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
        /// Sets up the required components for pointer interaction with characters
        /// </summary>
        private void SetupPointerInteraction()
        {
            // Setup Physics Raycaster
            var camera = Camera.main;
            if (camera != null && camera.GetComponent<PhysicsRaycaster>() == null)
            {
                var raycaster = camera.gameObject.AddComponent<PhysicsRaycaster>();
                raycaster.eventMask = LayerMask.GetMask("Character");
                Debug.Log("[CharacterDisplayManager] Added PhysicsRaycaster to main camera");
            }

            // Setup Event System if needed
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
        /// Spawns and initializes a character model with visual components
        /// </summary>
        /// <param name="characterData">Data containing character configuration</param>
        /// <param name="position">World position for spawning</param>
        /// <param name="rotation">Initial rotation (will be adjusted to face camera)</param>
        /// <returns>The instantiated character GameObject, or null if spawning fails</returns>
        public async Task<GameObject> SpawnCharacter(CharacterData characterData, Vector3 position, Quaternion rotation)
        {
            if (characterData == null || characterData.characterPrefab == null)
            {
                Debug.LogError($"[CharacterDisplayManager] Invalid character data for spawning!");
                return null;
            }

            try
            {
                // Handle the actual instantiation and setup
                Quaternion targetRotation = Quaternion.Euler(0, 180, 0);
                GameObject characterInstance = Instantiate(characterData.characterPrefab, position, targetRotation);
                characterInstance.name = $"Character_{characterData.characterId}";

                // Cache components for performance
                CacheCharacterComponents(characterInstance);

                // Initialize visual state
                await InitializeCharacterVisuals(characterInstance, characterData);

                Debug.Log($"[CharacterDisplayManager] Successfully spawned character: {characterData.characterId}");
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

            await Task.Yield();
        }

        #endregion

        #region Visual State Management

        /// <summary>
        /// Updates the visual state of a character based on its display state
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
            if (!characterAnimators.TryGetValue(character, out var animator))
                return;

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
                    animator.SetTrigger("Locked");
                    animator.speed = 0.5f;
                    break;
            }
        }

        private void UpdateMaterials(GameObject character, CharacterDisplayState state)
        {
            if (!characterRenderers.TryGetValue(character, out var renderer) ||
                !originalMaterials.ContainsKey(character))
                return;

            Material[] currentMaterials;

            switch (state)
            {
                case CharacterDisplayState.Locked:
                    currentMaterials = new Material[renderer.materials.Length];
                    for (int i = 0; i < currentMaterials.Length; i++)
                    {
                        currentMaterials[i] = lockedMaterial;
                    }
                    break;

                case CharacterDisplayState.Hover:
                case CharacterDisplayState.Selected:
                    currentMaterials = originalMaterials[character];
                    foreach (var material in currentMaterials)
                    {
                        if (material.HasProperty("_EmissionIntensity"))
                        {
                            material.SetFloat("_EmissionIntensity", highlightIntensity);
                        }
                    }
                    break;

                default:
                    currentMaterials = originalMaterials[character];
                    foreach (var material in currentMaterials)
                    {
                        if (material.HasProperty("_EmissionIntensity"))
                        {
                            material.SetFloat("_EmissionIntensity", 1f);
                        }
                    }
                    break;
            }

            renderer.materials = currentMaterials;
        }

        private void UpdateLighting(GameObject character, CharacterDisplayState state)
        {
            float targetIntensity = state == CharacterDisplayState.Hover ||
                                  state == CharacterDisplayState.Selected
                                  ? highlightIntensity
                                  : dimmedIntensity;

            var characterLights = character.GetComponentsInChildren<Light>();
            foreach (var light in characterLights)
            {
                light.intensity = targetIntensity;
            }
        }

        #endregion

        #region Spotlight Control

        /// <summary>
        /// Rotates the spotlight to focus on a character
        /// </summary>
        public async Task RotateSpotlight(Light spotlight, float targetRotation, float duration)
        {
            if (spotlight == null) return;

            try
            {
                Vector3 currentRotation = spotlight.transform.localEulerAngles;
                Vector3 targetRotationVector = new Vector3(
                    currentRotation.x,
                    targetRotation,
                    currentRotation.z
                );

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
        /// Cleans up cached components and materials
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
}