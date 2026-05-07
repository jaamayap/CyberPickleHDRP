// File: Assets/_CyberPickle/Code/UI/Screens/LevelUp/CardSlot.cs
// Namespace: CyberPickle.UI.Screens.LevelUp
//
// One card slot in the level-up choice screen. Binds an UpgradeCardSO to
// the slot's visual elements (background tint, icon, name, description,
// rarity badge), and surfaces hover + click as C# callbacks the parent
// controller wires up.
//
// Why a separate component (not just inline in LevelUpScreenController):
// - Clean separation: controller owns flow, slot owns view
// - Easy to instantiate from a prefab if we move from "3 pre-authored
//   slots" to "instantiate slot prefabs at runtime" later
// - Hover/click handlers via UI EventSystem stay scoped to the slot,
//   not coupled to the parent
//
// Hover behavior fires MusicEvent.CardHover with the cardId payload.
// Stage 0 audio bus stub just logs it; Stage 2 (M9 Wwise) maps to a
// hover-stinger preview per the GDD §3.11.4 differentiator.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.Progression;

namespace CyberPickle.UI.Screens.LevelUp
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class CardSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Visual References")]
        [Tooltip("Background image — its color is tinted to the card's element color (or neutral chrome for stat cards). Required.")]
        [SerializeField] private Image backgroundImage;

        [Tooltip("Icon image. Optional — if the card has no icon, the slot just shows the background tint.")]
        [SerializeField] private Image iconImage;

        [Tooltip("TMP for the card's display name. Required.")]
        [SerializeField] private TextMeshProUGUI nameText;

        [Tooltip("TMP for the card's description. Optional — visual-first cards may leave this blank.")]
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Tooltip("TMP for the rarity badge ('COMMON', 'RARE', etc.). Optional.")]
        [SerializeField] private TextMeshProUGUI rarityText;

        [Header("Interaction")]
        [Tooltip("The pick button — usually the entire card area. Required.")]
        [SerializeField] private Button pickButton;

        [Tooltip("Optional banish button (small X). Click removes the card from this run's pool. Hidden if not assigned.")]
        [SerializeField] private Button banishButton;

        // ─── Public events ────────────────────────────────────────────────

        /// <summary>Fired when the player clicks the slot to pick this card.</summary>
        public event Action<CardSlot> OnPicked;

        /// <summary>Fired when the player clicks the banish button on this slot.</summary>
        public event Action<CardSlot> OnBanished;

        // ─── Public state ─────────────────────────────────────────────────

        public UpgradeCardSO Card { get; private set; }

        // ─── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (pickButton != null)   pickButton.onClick.AddListener(HandlePickClicked);
            if (banishButton != null) banishButton.onClick.AddListener(HandleBanishClicked);
        }

        private void OnDestroy()
        {
            if (pickButton != null)   pickButton.onClick.RemoveListener(HandlePickClicked);
            if (banishButton != null) banishButton.onClick.RemoveListener(HandleBanishClicked);
        }

        // ─── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Bind a card to this slot. Updates all visuals. Pass null to clear
        /// (used when the slot is recycled across level-ups).
        /// </summary>
        public void Bind(UpgradeCardSO card)
        {
            Card = card;

            if (card == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (backgroundImage != null)
                backgroundImage.color = card.tintColor;

            if (iconImage != null)
            {
                iconImage.sprite = card.icon;
                iconImage.enabled = card.icon != null;
            }

            if (nameText != null)
                nameText.text = card.displayName;

            if (descriptionText != null)
                descriptionText.text = card.description ?? string.Empty;

            if (rarityText != null)
                rarityText.text = card.rarity.ToString().ToUpperInvariant();

            // Hide banish button if not configured. Future: hide based on
            // whether banishment is allowed for this run (token currency check).
            if (banishButton != null)
                banishButton.gameObject.SetActive(true);
        }

        /// <summary>Disable interaction (e.g., during the pick-completion animation).</summary>
        public void SetInteractable(bool interactable)
        {
            if (pickButton != null)   pickButton.interactable   = interactable;
            if (banishButton != null) banishButton.interactable = interactable;
        }

        // ─── Event handlers ───────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Card == null) return;
            // Bus event drives the hover-stinger preview (Stage 0 = log,
            // Stage 2 = Wwise event post). See GDD §3.11.4.
            MusicEventBus.Fire(MusicEvent.CardHover, Card.cardId);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // No bus event on exit — the hover stinger fades naturally.
            // If audio team wants explicit exit handling later, add a
            // CardHoverExit event to the enum.
        }

        private void HandlePickClicked()
        {
            if (Card == null) return;
            OnPicked?.Invoke(this);
        }

        private void HandleBanishClicked()
        {
            if (Card == null) return;
            OnBanished?.Invoke(this);
        }
    }
}
