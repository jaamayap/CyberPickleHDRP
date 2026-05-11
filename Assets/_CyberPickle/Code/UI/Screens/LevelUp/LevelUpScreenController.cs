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
using CyberPickle.Core;
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

            // Expand the cross — its center area becomes the visual stage
            // for these cards. The cross's Compact ↔ Expanded tween (DOTween,
            // unscaled time) animates while the game is paused.
            if (crossPanel != null) crossPanel.SetState(LoadoutCrossPanel.CrossState.Expanded);

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
            // Skip resumes the run with no card applied — hide cards +
            // collapse the cross + fade the panel.
            HideAllCardSlots();
            if (crossPanel != null) crossPanel.SetState(LoadoutCrossPanel.CrossState.Compact);
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
            // Trigger the modifier-pip "fly into the core" animation BEFORE
            // applying the card. The pip visual is element-tinted (matches
            // the rolled element on the card) and spawns from the picked
            // card slot's position.
            if (crossPanel != null)
            {
                Vector2 fromScreenPos = GetClickedCardScreenPos(card);
                Color elementColor = card.rolledElement != ElementId.None
                    ? card.rolledElement.DisplayColor()
                    : new Color(0.85f, 0.85f, 0.95f, 1f); // neutral chrome for non-element cards
                crossPanel.PlayCommitAnimation(fromScreenPos, elementColor, pipCount: 5);
            }

            coordinator.NotifyCardPicked(card, axisIndex);

            // Hide all card slots — the player committed; the in-game HUD
            // should now show ONLY the cross axes (weapons + power-ups).
            // Without this, the unselected cards would ride the cross
            // back to the compact corner still visible.
            HideAllCardSlots();

            // Collapse the cross + fade the panel as the run resumes.
            if (crossPanel != null) crossPanel.SetState(LoadoutCrossPanel.CrossState.Compact);
            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        /// <summary>
        /// Deactivate every card slot's GameObject. CardSlot.Bind(default)
        /// hides itself via SetActive(false). On the next level-up,
        /// HandleCardsDrawn re-Binds with fresh draft cards which re-enables
        /// the GameObjects.
        /// </summary>
        private void HideAllCardSlots()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) slots[i].Bind(default);
            }
        }

        /// <summary>
        /// Find the screen-space position of the slot that's currently
        /// showing the picked card, so the commit animation can spawn its
        /// pips from there. Falls back to screen center if the slot can't
        /// be matched (defensive — e.g., picked via debug command).
        /// </summary>
        private Vector2 GetClickedCardScreenPos(DraftedCard card)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                if (!slots[i].Card.IsValid) continue;
                if (slots[i].Card.source == card.source && slots[i].Card.rolledRarity == card.rolledRarity)
                {
                    var rt = (RectTransform)slots[i].transform;
                    return RectTransformUtility.WorldToScreenPoint(null, rt.position);
                }
            }
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
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
