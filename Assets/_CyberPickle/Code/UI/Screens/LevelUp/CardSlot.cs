// File: Assets/_CyberPickle/Code/UI/Screens/LevelUp/CardSlot.cs
// Namespace: CyberPickle.UI.Screens.LevelUp
//
// One card slot in the level-up choice screen. Binds a DraftedCard
// (the card SO + the values rolled at draft time — rarity, element)
// to the slot's visual elements (background tint, icon, name,
// description, rarity badge, element badge), and surfaces hover +
// click as C# callbacks the parent controller wires up.
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
using CyberPickle.Core;
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

        [Tooltip("TMP for the element badge ('FIRE', 'LIGHTNING', etc.) — only shown for power-up cards. Anchor this near the TOP of the card per the design (element 'faces the weapon ring' on the cross). Optional.")]
        [SerializeField] private TextMeshProUGUI elementText;

        [Header("Stat Pips (bottom-of-card, M8 step 5)")]
        [Tooltip("Parent RectTransform for the stat-magnitude pips. The card's visual concept (chat 2026-05-11) puts the element on top facing the weapon and the stat pips on the bottom facing the core — so anchor this container at the bottom of the card. Cleared and repopulated on each Bind. Optional — leave null to skip pip rendering.")]
        [SerializeField] private RectTransform statPipsContainer;

        [Tooltip("Prefab spawned once per rarity tier when populating the pip row. A small Image is sufficient — the script tints it by the rolled element / rarity. Hidden if statPipsContainer is null.")]
        [SerializeField] private RectTransform statPipPrefab;

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

        /// <summary>The DraftedCard currently shown by this slot. Default if not bound.</summary>
        public DraftedCard Card { get; private set; }

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
        /// Bind a drafted card to this slot. Updates all visuals. Pass an
        /// invalid (default) DraftedCard to clear the slot (used when the
        /// slot is recycled across level-ups or when the player has fewer
        /// cards than slots).
        /// </summary>
        public void Bind(DraftedCard card)
        {
            Card = card;

            if (!card.IsValid)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            var so = card.source;
            // For power-up cards, tint by element instead of card.tintColor.
            // The element badge mirrors this — keeps the visual identity
            // matched (per chat 2026-05-11 design: element on top of card).
            if (backgroundImage != null)
            {
                backgroundImage.color = card.rolledElement != ElementId.None
                    ? card.rolledElement.DisplayColor()
                    : so.tintColor;
            }

            if (iconImage != null)
            {
                iconImage.sprite = so.icon;
                iconImage.enabled = so.icon != null;
            }

            // Template cards use {WEAPON} / {POWERUP} placeholders in their
            // authored displayName/description. Substitute with the
            // resolved target's actual name from WeaponData/PowerUpData.
            // Non-templated cards' authored text passes through unchanged
            // (no placeholders → no-op replace).
            string resolvedName = ResolveCardText(so.displayName, card);
            string resolvedDesc = ResolveCardText(so.description ?? string.Empty, card);

            if (nameText != null)
                nameText.text = resolvedName;

            if (descriptionText != null)
                descriptionText.text = resolvedDesc;

            if (rarityText != null)
                rarityText.text = card.rolledRarity.ToString().ToUpperInvariant();

            if (elementText != null)
            {
                if (card.rolledElement != ElementId.None)
                {
                    elementText.text = card.rolledElement.DisplayName().ToUpperInvariant();
                    elementText.color = card.rolledElement.DisplayColor();
                    elementText.gameObject.SetActive(true);
                }
                else
                {
                    elementText.gameObject.SetActive(false);
                }
            }

            if (banishButton != null)
                banishButton.gameObject.SetActive(true);

            // Stat pips — Common=1 ... Legendary=5. Tinted by the rolled
            // element when present, else by rarity. Designer anchors the
            // container at the card's bottom edge per the "modifiers face
            // the core" visual concept.
            PopulateStatPips(card);
        }

        /// <summary>
        /// Substitute placeholders in card text with resolved target names.
        /// Placeholders supported:
        ///   <c>{WEAPON}</c>  → DraftedCard's effective weapon target's
        ///                       <c>displayName</c> (resolved template or
        ///                       authored fallback). Empty string when no
        ///                       weapon target applies.
        ///   <c>{POWERUP}</c> → same for power-up template targets.
        ///
        /// Template cards (e.g., displayName = "{WEAPON}: Level Up") render
        /// dynamically per draft. Non-template cards have no placeholders
        /// in their authored text → this is a no-op for them.
        /// </summary>
        private static string ResolveCardText(string raw, DraftedCard card)
        {
            if (string.IsNullOrEmpty(raw)) return raw ?? string.Empty;

            // Fast path: skip the work entirely when no placeholder appears.
            bool hasWeaponPh  = raw.Contains("{WEAPON}");
            bool hasPowerUpPh = raw.Contains("{POWERUP}");
            if (!hasWeaponPh && !hasPowerUpPh) return raw;

            string output = raw;
            if (hasWeaponPh)
            {
                var w = card.EffectiveWeaponTarget;
                output = output.Replace("{WEAPON}", w != null ? w.displayName : string.Empty);
            }
            if (hasPowerUpPh)
            {
                var p = card.EffectivePowerUpTarget;
                output = output.Replace("{POWERUP}", p != null ? p.displayName : string.Empty);
            }
            return output;
        }

        private void PopulateStatPips(DraftedCard card)
        {
            if (statPipsContainer == null) return;

            // Clear previous pips.
            for (int i = statPipsContainer.childCount - 1; i >= 0; i--)
                Destroy(statPipsContainer.GetChild(i).gameObject);

            if (statPipPrefab == null) return;

            int pipCount = (int)card.rolledRarity + 1; // Common=1 ... Legendary=5
            Color tint = card.rolledElement != ElementId.None
                ? card.rolledElement.DisplayColor()
                : card.rolledRarity.DisplayColor();

            for (int i = 0; i < pipCount; i++)
            {
                var pip = Instantiate(statPipPrefab, statPipsContainer);
                pip.gameObject.SetActive(true);
                var graphic = pip.GetComponent<Graphic>();
                if (graphic != null) graphic.color = tint;
            }
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
            if (!Card.IsValid) return;
            // Bus event drives the hover-stinger preview (Stage 0 = log,
            // Stage 2 = Wwise event post). See GDD §3.11.4.
            MusicEventBus.Fire(MusicEvent.CardHover, Card.source.cardId);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // No bus event on exit — the hover stinger fades naturally.
            // If audio team wants explicit exit handling later, add a
            // CardHoverExit event to the enum.
        }

        private void HandlePickClicked()
        {
            if (!Card.IsValid) return;
            OnPicked?.Invoke(this);
        }

        private void HandleBanishClicked()
        {
            if (!Card.IsValid) return;
            OnBanished?.Invoke(this);
        }
    }
}
