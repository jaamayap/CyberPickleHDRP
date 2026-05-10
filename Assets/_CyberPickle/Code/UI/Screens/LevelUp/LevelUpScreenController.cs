// File: Assets/_CyberPickle/Code/UI/Screens/LevelUp/LevelUpScreenController.cs
// Namespace: CyberPickle.UI.Screens.LevelUp
//
// The level-up choice screen UI. Subscribes to LevelUpCoordinator's
// OnCardsDrawn event, populates the slots, fades in. Click on a slot
// reports back to the coordinator and fades out.
//
// Critical detail: this UI runs while RunStateManager.CurrentPhase ==
// LevelUpPaused, which sets Time.timeScale = 0. ALL animations and
// timers MUST use unscaled time, or the UI will freeze the moment it
// appears. Same defensive pattern we used for EquipmentHubManager fades
// and the ResultsScreenController.
//
// What this is (Day 3, minimum viable):
//   - 3 pre-authored CardSlot GameObjects in a CanvasGroup panel
//   - Fade in / fade out via unscaled time
//   - Click → coordinator
//   - Banish click → coordinator
//
// What this is NOT (yet):
//   - 3D model preview rigs per slot — Day 4
//   - Hold-to-confirm picking — Day 5
//   - Beat-aligned slow-mo (Time.timeScale = 0.05 instead of 0) — Day 5
//   - Hover-stinger Wwise preview — handled at MusicEventBus listener
//     level when Wwise integrates (M9, Stage 2 of audio rollout)
//
// Hierarchy convention (drop these in Game.unity under your Canvas):
//
//   [LevelUpScreenPanel]            ← CanvasGroup, alpha=0, !raycasts initially
//     ├─ Background (full-screen dim)
//     ├─ Title (TMP "LEVEL UP")
//     ├─ Slot1 (CardSlot component)
//     │   ├─ Background (Image)
//     │   ├─ Icon (Image)
//     │   ├─ Name (TMP)
//     │   ├─ Description (TMP)
//     │   ├─ Rarity (TMP)
//     │   ├─ PickButton (Button on the whole card area)
//     │   └─ BanishButton (Button, small X icon)
//     ├─ Slot2 (CardSlot component, same layout)
//     └─ Slot3 (CardSlot component, same layout)
//
// Drop LevelUpScreenController on [LevelUpScreenPanel]. Drag the 3
// CardSlot components into the slots[] array. Wire panelGroup to the
// CanvasGroup on the same GameObject.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CyberPickle.Gameplay.Progression;

namespace CyberPickle.UI.Screens.LevelUp
{
    [DisallowMultipleComponent]
    public class LevelUpScreenController : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("CanvasGroup driving the panel's visibility. Should start with alpha=0, interactable=false, blocksRaycasts=false.")]
        [SerializeField] private CanvasGroup panelGroup;

        [Tooltip("Seconds to fade the panel in / out. Uses unscaled time so it animates while the game is paused (Time.timeScale=0 during LevelUpPaused).")]
        [Min(0f)] [SerializeField] private float fadeDuration = 0.4f;

        [Header("Slots")]
        [Tooltip("The card slots — typically 3, matching LevelUpCoordinator.cardsPerOffer. Each slot is a CardSlot MonoBehaviour authored in the scene.")]
        [SerializeField] private CardSlot[] slots = new CardSlot[3];

        [Header("Coordinator")]
        [Tooltip("The level-up coordinator this screen subscribes to. Auto-discovered if left empty at OnEnable.")]
        [SerializeField] private LevelUpCoordinator coordinator;

        [Header("Diagnostics")]
        [SerializeField] private bool verbose = true;

        // ─── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            // Hidden until OnCardsDrawn fires.
            SetPanelVisibility(0f, interactable: false);
        }

        private void OnEnable()
        {
            if (coordinator == null)
                coordinator = FindFirstObjectByType<LevelUpCoordinator>();

            if (coordinator != null)
            {
                coordinator.OnCardsDrawn += HandleCardsDrawn;
            }
            else
            {
                Debug.LogWarning("[LevelUpScreenController] No LevelUpCoordinator found. Screen will never display cards.");
            }

            // Wire each slot's pick + banish events to our local handlers.
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                slots[i].OnPicked   += HandleSlotPicked;
                slots[i].OnBanished += HandleSlotBanished;
            }
        }

        private void OnDisable()
        {
            if (coordinator != null)
                coordinator.OnCardsDrawn -= HandleCardsDrawn;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                slots[i].OnPicked   -= HandleSlotPicked;
                slots[i].OnBanished -= HandleSlotBanished;
            }
        }

        // ─── Coordinator → UI ─────────────────────────────────────────────

        private void HandleCardsDrawn(IReadOnlyList<DraftedCard> cards)
        {
            if (verbose) Debug.Log($"[LevelUpScreen] Showing {cards.Count} cards.");

            // Bind each slot. If the coordinator drew fewer cards than we
            // have slots (small pool, lots banished), unused slots hide
            // themselves via Bind(default DraftedCard).
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                DraftedCard card = i < cards.Count ? cards[i] : default;
                slots[i].Bind(card);
                slots[i].SetInteractable(true);
            }

            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }

        // ─── Slot interactions → Coordinator ──────────────────────────────

        private void HandleSlotPicked(CardSlot slot)
        {
            if (slot == null || !slot.Card.IsValid || coordinator == null) return;

            if (verbose) Debug.Log($"[LevelUpScreen] Picked '{slot.Card.source.cardId}'.");

            // Disable all slots immediately so a fast double-click can't
            // pick two cards before the fade-out completes.
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null) slots[i].SetInteractable(false);

            // Apply via the coordinator. For cards requiring slot-pick
            // (NewWeapon / NewPowerUp), this currently auto-picks the
            // first empty axis (axisIndex=-1). The cross-UI slot-picker
            // flow lands in M8 step 4 — it'll route through a new
            // coordinator.RequestCommit() → coordinator.NotifySlotPicked()
            // sequence so the player chooses the axis explicitly.
            coordinator.NotifyCardPicked(slot.Card);

            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        private void HandleSlotBanished(CardSlot slot)
        {
            if (slot == null || !slot.Card.IsValid || coordinator == null) return;

            if (verbose) Debug.Log($"[LevelUpScreen] Banished '{slot.Card.source.cardId}'.");

            coordinator.NotifyCardBanished(slot.Card);
            slot.Bind(default);
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
