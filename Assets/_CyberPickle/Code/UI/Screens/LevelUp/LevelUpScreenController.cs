// File: Assets/_CyberPickle/Code/UI/Screens/LevelUp/LevelUpScreenController.cs
// Namespace: CyberPickle.UI.Screens.LevelUp
//
// The level-up choice screen UI. Subscribes to LevelUpCoordinator's
// OnCardsDrawn event, populates the slots, fades in. Click on a slot
// either commits the card immediately (StatModifier / LevelUp / RarityUp
// cards) or hands off to the cross-panel slot-picker for cards that need
// the player to pick an axis (NewWeapon / NewPowerUp).
//
// Skip and Reroll buttons:
//   - Skip → coordinator.NotifyDraftSkipped() (adds +1 to bankedRerolls,
//     resumes run with no card applied)
//   - Reroll → coordinator.NotifyRerollRequested() (spends 1 banked
//     reroll, redraws the same draft) — disabled when bankedRerolls == 0
//
// Critical detail: this UI runs while RunStateManager.CurrentPhase ==
// LevelUpPaused, which sets Time.timeScale = 0. ALL animations and
// timers MUST use unscaled time, or the UI will freeze the moment it
// appears.
//
// What this is (M8 step 4 cut):
//   - 3+ pre-authored CardSlot GameObjects in a CanvasGroup panel
//   - Fade in / fade out via unscaled time
//   - Click → coordinator (or → slot-picker if RequiresSlotSelection)
//   - Skip + Reroll buttons
//   - Optional Cancel button to back out of slot-picker mode
//
// What this is NOT (yet, M8 step 5 polish):
//   - 3D model preview rigs per slot
//   - Hold-to-confirm picking
//   - Beat-aligned slow-mo (timeScale = 0.05)
//   - Slot-picker fade animation
//   - Cards-in-the-center-of-the-cross expanded layout

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CyberPickle.Gameplay.Progression;
using CyberPickle.UI.HUD;

namespace CyberPickle.UI.Screens.LevelUp
{
    [DisallowMultipleComponent]
    public class LevelUpScreenController : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("CanvasGroup driving the panel's visibility. Should start with alpha=0, interactable=false, blocksRaycasts=false.")]
        [SerializeField] private CanvasGroup panelGroup;

        [Tooltip("Seconds to fade the panel in / out. Uses unscaled time.")]
        [Min(0f)] [SerializeField] private float fadeDuration = 0.4f;

        [Header("Slots")]
        [Tooltip("The card slots — typically 3, but can be up to 6 (Luck-driven). Slots beyond the drawn count are hidden via Bind(default).")]
        [SerializeField] private CardSlot[] slots = new CardSlot[3];

        [Header("Coordinator + Cross Panel")]
        [Tooltip("Level-up coordinator this screen subscribes to. Auto-discovered at OnEnable if null.")]
        [SerializeField] private LevelUpCoordinator coordinator;

        [Tooltip("LoadoutCrossPanel used as the slot-picker when a NewWeapon/NewPowerUp card is clicked. Auto-discovered at OnEnable if null.")]
        [SerializeField] private LoadoutCrossPanel crossPanel;

        [Header("Skip / Reroll")]
        [Tooltip("Optional Skip button — invokes coordinator.NotifyDraftSkipped() on click (adds +1 to banked rerolls).")]
        [SerializeField] private Button skipButton;

        [Tooltip("Optional Reroll button — invokes coordinator.NotifyRerollRequested() on click (spends 1 banked reroll).")]
        [SerializeField] private Button rerollButton;

        [Tooltip("Optional TMP showing the current banked-reroll count, e.g. 'Reroll (×2)'.")]
        [SerializeField] private TextMeshProUGUI rerollLabel;

        [Tooltip("Optional Cancel button shown only during slot-picker mode — backs out so the player can pick a different card.")]
        [SerializeField] private Button cancelSlotPickerButton;

        [Tooltip("Optional TMP shown during slot-picker mode (e.g. 'Pick a slot →').")]
        [SerializeField] private TextMeshProUGUI slotPickerHintLabel;

        [Header("Diagnostics")]
        [SerializeField] private bool verbose = true;

        // ─── Internal state ───────────────────────────────────────────────

        // The card the player has clicked but not yet committed (because
        // it requires a slot-pick). null when no card is pending.
        private DraftedCard? _pendingSlottableCard;

        // ─── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            SetPanelVisibility(0f, interactable: false);
            SetSlotPickerHint(false);
        }

        private void OnEnable()
        {
            if (coordinator == null) coordinator = FindFirstObjectByType<LevelUpCoordinator>();
            if (crossPanel == null)  crossPanel  = FindFirstObjectByType<LoadoutCrossPanel>();

            if (coordinator != null)
            {
                coordinator.OnCardsDrawn          += HandleCardsDrawn;
                coordinator.OnBankedRerollsChanged += HandleBankedRerollsChanged;
                HandleBankedRerollsChanged(coordinator.BankedRerolls); // initial paint
            }
            else
            {
                Debug.LogWarning("[LevelUpScreenController] No LevelUpCoordinator found.");
            }

            if (crossPanel != null)
            {
                crossPanel.OnSlotPicked            += HandleSlotPicked;
                crossPanel.OnSlotPickerCancelled   += HandleSlotPickerCancelled;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                slots[i].OnPicked   += HandleSlotPicked;
                slots[i].OnBanished += HandleSlotBanished;
            }

            if (skipButton != null)               skipButton.onClick.AddListener(HandleSkipClicked);
            if (rerollButton != null)             rerollButton.onClick.AddListener(HandleRerollClicked);
            if (cancelSlotPickerButton != null)   cancelSlotPickerButton.onClick.AddListener(HandleCancelSlotPickerClicked);
        }

        private void OnDisable()
        {
            if (coordinator != null)
            {
                coordinator.OnCardsDrawn          -= HandleCardsDrawn;
                coordinator.OnBankedRerollsChanged -= HandleBankedRerollsChanged;
            }
            if (crossPanel != null)
            {
                crossPanel.OnSlotPicked          -= HandleSlotPicked;
                crossPanel.OnSlotPickerCancelled -= HandleSlotPickerCancelled;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                slots[i].OnPicked   -= HandleSlotPicked;
                slots[i].OnBanished -= HandleSlotBanished;
            }

            if (skipButton != null)             skipButton.onClick.RemoveListener(HandleSkipClicked);
            if (rerollButton != null)           rerollButton.onClick.RemoveListener(HandleRerollClicked);
            if (cancelSlotPickerButton != null) cancelSlotPickerButton.onClick.RemoveListener(HandleCancelSlotPickerClicked);
        }

        // ─── Coordinator → UI ─────────────────────────────────────────────

        private void HandleCardsDrawn(IReadOnlyList<DraftedCard> cards)
        {
            if (verbose) Debug.Log($"[LevelUpScreen] Showing {cards.Count} cards.");

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                DraftedCard card = i < cards.Count ? cards[i] : default;
                slots[i].Bind(card);
                slots[i].SetInteractable(true);
            }

            _pendingSlottableCard = null;
            SetSlotPickerHint(false);

            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }

        private void HandleBankedRerollsChanged(int newCount)
        {
            if (rerollButton != null) rerollButton.interactable = newCount > 0;
            if (rerollLabel != null)  rerollLabel.text = newCount > 0 ? $"Reroll (×{newCount})" : "Reroll";
        }

        // ─── Slot interactions → Coordinator ──────────────────────────────

        // CardSlot.OnPicked — player clicked a card on the level-up panel.
        private void HandleSlotPicked(CardSlot slot)
        {
            if (slot == null || !slot.Card.IsValid || coordinator == null) return;

            var card = slot.Card;
            if (verbose) Debug.Log($"[LevelUpScreen] Picked '{card.source.cardId}'.");

            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null) slots[i].SetInteractable(false);

            // Cards that need an axis pick → enter slot-picker mode.
            // Other cards → commit immediately.
            if (card.RequiresSlotSelection && crossPanel != null)
            {
                _pendingSlottableCard = card;
                var kind = card.source.cardType == CardType.NewWeapon
                    ? LoadoutCrossPanel.SlotKind.Weapon
                    : LoadoutCrossPanel.SlotKind.PowerUp;
                crossPanel.BeginSlotPicker(kind);
                SetSlotPickerHint(true, kind);
                if (verbose) Debug.Log($"[LevelUpScreen] Awaiting slot pick ({kind}).");
                // Keep the level-up panel visible (dimmed could be added later)
                // so the player still sees what they're committing to. Cancel
                // button lets them back out without committing.
                return;
            }

            CommitPick(card, axisIndex: -1);
        }

        // LoadoutCrossPanel.OnSlotPicked — player clicked an eligible slot
        // during slot-picker mode.
        private void HandleSlotPicked(int axisIndex)
        {
            if (_pendingSlottableCard == null) return;
            var card = _pendingSlottableCard.Value;
            _pendingSlottableCard = null;
            SetSlotPickerHint(false);
            if (verbose) Debug.Log($"[LevelUpScreen] Slot picked: axis {axisIndex}. Committing '{card.source.cardId}'.");
            CommitPick(card, axisIndex);
        }

        private void HandleSlotPickerCancelled()
        {
            if (_pendingSlottableCard == null) return;
            // Slot picker was cancelled — re-enable the cards so the player
            // can pick a different one.
            _pendingSlottableCard = null;
            SetSlotPickerHint(false);
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null) slots[i].SetInteractable(true);
            if (verbose) Debug.Log("[LevelUpScreen] Slot picker cancelled — re-enabling cards.");
        }

        private void HandleSlotBanished(CardSlot slot)
        {
            if (slot == null || !slot.Card.IsValid || coordinator == null) return;
            if (verbose) Debug.Log($"[LevelUpScreen] Banished '{slot.Card.source.cardId}'.");
            coordinator.NotifyCardBanished(slot.Card);
            slot.Bind(default);
        }

        private void HandleSkipClicked()
        {
            if (coordinator == null) return;
            if (verbose) Debug.Log("[LevelUpScreen] Skip clicked.");
            // If we're mid-slot-pick, cancel that first.
            if (_pendingSlottableCard != null) HandleCancelSlotPickerClicked();
            coordinator.NotifyDraftSkipped();
            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        private void HandleRerollClicked()
        {
            if (coordinator == null) return;
            if (coordinator.BankedRerolls <= 0) return;
            if (verbose) Debug.Log("[LevelUpScreen] Reroll clicked.");
            // Same: cancel any pending slot-pick before re-rolling.
            if (_pendingSlottableCard != null) HandleCancelSlotPickerClicked();
            coordinator.NotifyRerollRequested(); // OnCardsDrawn fires → HandleCardsDrawn rebinds
        }

        private void HandleCancelSlotPickerClicked()
        {
            if (crossPanel != null && crossPanel.IsPicking) crossPanel.CancelSlotPicker();
            // CancelSlotPicker fires OnSlotPickerCancelled which re-enables cards.
        }

        private void CommitPick(DraftedCard card, int axisIndex)
        {
            coordinator.NotifyCardPicked(card, axisIndex);
            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        // ─── Helpers ─────────────────────────────────────────────────────

        private void SetSlotPickerHint(bool show, LoadoutCrossPanel.SlotKind kind = default)
        {
            if (slotPickerHintLabel != null)
            {
                slotPickerHintLabel.gameObject.SetActive(show);
                if (show)
                {
                    string what = kind == LoadoutCrossPanel.SlotKind.Weapon ? "weapon" : "power-up";
                    slotPickerHintLabel.text = $"Pick an empty {what} slot →";
                }
            }
            if (cancelSlotPickerButton != null)
                cancelSlotPickerButton.gameObject.SetActive(show);
        }

        // ─── Fades (unscaled time) ────────────────────────────────────────

        private IEnumerator FadeIn()
        {
            if (panelGroup == null) yield break;
            panelGroup.interactable   = true;
            panelGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                panelGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            panelGroup.alpha = 1f;
        }

        private IEnumerator FadeOut()
        {
            if (panelGroup == null) yield break;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                panelGroup.alpha = Mathf.Clamp01(1f - elapsed / fadeDuration);
                yield return null;
            }
            panelGroup.alpha = 0f;
            panelGroup.interactable   = false;
            panelGroup.blocksRaycasts = false;
        }

        private void SetPanelVisibility(float alpha, bool interactable)
        {
            if (panelGroup == null) return;
            panelGroup.alpha = alpha;
            panelGroup.interactable   = interactable;
            panelGroup.blocksRaycasts = interactable;
        }
    }
}
