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
using UnityEngine.UI;

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
            // Add debug logging
            Debug.Log($"[CharacterPointerHandler] Initializing for GameObject: {gameObject.name}");

            pointerCollider = GetComponent<BoxCollider>();
            if (pointerCollider == null)
            {
                pointerCollider = gameObject.AddComponent<BoxCollider>();
                // Make sure collider size is appropriate
                pointerCollider.size = new Vector3(2f, 4f, 2f);
                pointerCollider.center = new Vector3(0f, 2f, 0f);
                pointerCollider.isTrigger = true;
                Debug.Log($"[CharacterPointerHandler] Added BoxCollider to {gameObject.name}");
            }
        }

       
        private void Update()
        {
            // Debug ray from mouse position
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.yellow);
                
            }
        }
        private void OnDrawGizmos()
        {
            if (pointerCollider != null)
            {
                // Draw collider bounds
                Gizmos.color = Color.yellow;
                Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                Gizmos.matrix = rotationMatrix;
                Gizmos.DrawWireCube(pointerCollider.center, pointerCollider.size);
            }
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
        public void SetInteractable(bool interactable, bool allowHover = true)
        {
            isInteractable = interactable;

            // Allow hover even if interaction is disabled
            pointerCollider.isTrigger = !interactable || allowHover;

            Debug.Log($"[CharacterPointerHandler] Interactable set to {interactable}, AllowHover: {allowHover}");
        }

        /// <summary>
        /// Handles pointer enter events, triggering character hover state
        /// </summary>
        /// <param name="eventData">Data associated with the pointer event</param>
        public void OnPointerEnter(PointerEventData eventData)
        {
            Debug.Log($"[CharacterPointerHandler] POINTER ENTER - Position: {eventData.position}");
            Debug.Log($"Cursor Entering {name} GameObject");

            // Always invoke hover events, even for locked characters
            GameEvents.OnCharacterHoverEnter?.Invoke(characterId);

            // Add additional debug to verify behavior
            if (isInteractable)
            {
                Debug.Log($"[CharacterPointerHandler] Hovering over unlocked character: {characterId}");
            }
            else
            {
                Debug.Log($"[CharacterPointerHandler] Hovering over locked character: {characterId}");
            }
        }

        /// <summary>
        /// Handles pointer exit events, ending character hover state
        /// </summary>
        /// <param name="eventData">Data associated with the pointer event</param>
        public void OnPointerExit(PointerEventData eventData)
        {
            Debug.Log($"[CharacterPointerHandler] POINTER EXIT - Position: {eventData.position}");
            if (!isInteractable) return;
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
            Debug.Log($"[CharacterPointerHandler] POINTER CLICK - Position: {eventData.position}, Button: {eventData.button}");
            if (!isInteractable)
            {
                Debug.Log($"[CharacterPointerHandler] Click blocked for locked character: {characterId}");
                return; // Block click interaction
            }

            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    GameEvents.OnCharacterSelected?.Invoke(characterId);
                    break;
                case PointerEventData.InputButton.Right:
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
