using UnityEngine;
using UnityEngine.EventSystems;
using CyberPickle.Core.Events;

namespace CyberPickle.Characters.Logic
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

        private void Awake()
        {
            Debug.Log($"[CharacterPointerHandler] Initializing for GameObject: {gameObject.name}");

            pointerCollider = GetComponent<BoxCollider>();
            if (pointerCollider == null)
            {
                pointerCollider = gameObject.AddComponent<BoxCollider>();
                pointerCollider.size = new Vector3(2f, 4f, 2f);
                pointerCollider.center = new Vector3(0f, 2f, 0f);
                pointerCollider.isTrigger = true;
                Debug.Log($"[CharacterPointerHandler] Added BoxCollider to {gameObject.name}");
            }
        }

        /// <summary>
        /// Sets up character-specific data.
        /// </summary>
        public void Initialize(string charId)
        {
            characterId = charId;
            Debug.Log($"[CharacterPointerHandler] Initialized for character: {characterId}");
        }

        /// <summary>
        /// Enables or disables pointer interactions with the character.
        /// </summary>
        /// <param name="interactable">Whether the character should respond to pointer events.</param>
        /// <param name="allowHover">If false, hover events are also disabled.</param>
        public void SetInteractable(bool interactable, bool allowHover = true)
        {
            isInteractable = interactable;
            // If we only want hover, we keep the collider as a trigger but block clicks
            pointerCollider.isTrigger = !interactable || allowHover;
            Debug.Log($"[CharacterPointerHandler] Interactable set to {interactable}, AllowHover: {allowHover}");
        }

        public bool IsInteractable => isInteractable;

        public void OnPointerEnter(PointerEventData eventData)
        {
            Debug.Log($"[CharacterPointerHandler] POINTER ENTER - {characterId}");
            GameEvents.OnCharacterHoverEnter?.Invoke(characterId);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Debug.Log($"[CharacterPointerHandler] POINTER EXIT - {characterId}");
            if (!isInteractable) return;
            GameEvents.OnCharacterHoverExit?.Invoke(characterId);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log($"[CharacterPointerHandler] POINTER CLICK - {characterId}, Button: {eventData.button}");
            if (!isInteractable) return;

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
        /// Adjusts the collider size at runtime to match character model scale.
        /// </summary>
        public void AdjustColliderSize(Vector3 size, Vector3 center)
        {
            if (pointerCollider == null) return;
            pointerCollider.size = size;
            pointerCollider.center = center;
            Debug.Log($"[CharacterPointerHandler] Adjusted collider size for {characterId}");
        }
    }
}
