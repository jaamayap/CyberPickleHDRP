// File: Assets/_CyberPickle/Code/Characters/CharacterPointerHandler.cs
//
// Purpose: Handles pointer interaction events for character models in the selection screen.
// Manages mouse hover, click events, and communicates with the selection system through
// GameEvents. Requires a BoxCollider for physics raycasting.
//
// Created: 2024-02-11
// Updated: 2024-02-11

using UnityEngine;
using UnityEngine.EventSystems;
using CyberPickle.Core.Events;
using CyberPickle.Characters.Data;

namespace CyberPickle.Characters
{
    /// <summary>
    /// Handles pointer interactions with character models in the selection screen.
    /// Requires a BoxCollider component for physics raycasting.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CharacterPointerHandler : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
    {
        private BoxCollider pointerCollider;
        private string characterId;
        private bool isInteractable = true;

        /// <summary>
        /// Initializes the required components and configures the BoxCollider
        /// for pointer interactions.
        /// </summary>
        private void Awake()
        {
            // Setup collider for mouse interaction
            pointerCollider = GetComponent<BoxCollider>();
            if (pointerCollider == null)
            {
                pointerCollider = gameObject.AddComponent<BoxCollider>();
                Debug.Log($"[CharacterPointerHandler] Added BoxCollider to {gameObject.name}");
            }

            // Configure collider for pointer events
            pointerCollider.isTrigger = true;

            // Set default collider size and position
            // These values can be adjusted based on character model scale
            pointerCollider.size = new Vector3(2f, 4f, 2f);
            pointerCollider.center = new Vector3(0f, 2f, 0f);

            Debug.Log($"[CharacterPointerHandler] Initialized on {gameObject.name}");
        }

        /// <summary>
        /// Initializes the handler with character-specific data
        /// </summary>
        /// <param name="characterId">Unique identifier for the character</param>
        public void Initialize(string characterId)
        {
            this.characterId = characterId;
            Debug.Log($"[CharacterPointerHandler] Initialized for character: {characterId}");
        }

        /// <summary>
        /// Enables or disables pointer interactions with the character
        /// </summary>
        /// <param name="interactable">Whether the character should respond to pointer events</param>
        public void SetInteractable(bool interactable)
        {
            isInteractable = interactable;
            Debug.Log($"[CharacterPointerHandler] Interactable set to {interactable} for character: {characterId}");
        }

        /// <summary>
        /// Handles pointer enter events, triggering character hover state
        /// </summary>
        /// <param name="eventData">Data associated with the pointer event</param>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isInteractable) return;

            Debug.Log($"[CharacterPointerHandler] Pointer entered character: {characterId}");
            GameEvents.OnCharacterHoverEnter?.Invoke(characterId);
        }

        /// <summary>
        /// Handles pointer exit events, ending character hover state
        /// </summary>
        /// <param name="eventData">Data associated with the pointer event</param>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isInteractable) return;

            Debug.Log($"[CharacterPointerHandler] Pointer exited character: {characterId}");
            GameEvents.OnCharacterHoverExit?.Invoke(characterId);
        }

        /// <summary>
        /// Handles pointer click events, differentiating between left and right clicks
        /// Left click: Select character
        /// Right click: Show character details
        /// </summary>
        /// <param name="eventData">Data associated with the pointer event</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isInteractable) return;

            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    Debug.Log($"[CharacterPointerHandler] Left click on character: {characterId}");
                    GameEvents.OnCharacterSelected?.Invoke(characterId);
                    break;

                case PointerEventData.InputButton.Right:
                    Debug.Log($"[CharacterPointerHandler] Right click on character: {characterId}");
                    GameEvents.OnCharacterDetailsRequested?.Invoke(characterId);
                    break;
            }
        }

        /// <summary>
        /// Allows runtime adjustment of the collider size to match character model scale
        /// </summary>
        /// <param name="size">New size for the collision box</param>
        /// <param name="center">New center position for the collision box</param>
        public void AdjustColliderSize(Vector3 size, Vector3 center)
        {
            if (pointerCollider != null)
            {
                pointerCollider.size = size;
                pointerCollider.center = center;
                Debug.Log($"[CharacterPointerHandler] Adjusted collider size for {characterId}");
            }
        }
    }
}
