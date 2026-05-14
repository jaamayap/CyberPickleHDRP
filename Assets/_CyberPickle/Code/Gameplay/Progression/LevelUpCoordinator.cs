// File: Assets/_CyberPickle/Code/Gameplay/Progression/LevelUpCoordinator.cs
// Namespace: CyberPickle.Gameplay.Progression
//
// Orchestrates the level-up flow:
//
//   1. Subscribes to MusicEventBus.OnEvent and filters for MusicEvent.LevelUp.
//      (Originally tried PlayerXPBridge.OnLevelUp directly, but that race-
//      conditioned with the player-spawn order: LevelUpCoordinator's OnEnable
//      fires at scene-load, before GameSceneBootstrap.Start spawns the player,
//      so FindFirstObjectByType<PlayerXPBridge>() returned null and the
//      coordinator silently never subscribed. The MusicEventBus is a static
//      class — process-global, alive from assembly load — so bus
//      subscriptions don't care about scene-load timing.)
//   2. On level-up: transitions RunStateManager → LevelUpPaused.
//   3. Draws N cards from the active UpgradePoolSO. N is Luck-driven
//      via RarityRollService.CardsVisibleForLuck (3 base, +1 per 50 Luck,
//      cap 6 — see CLAUDE.md design pillar). Cards are filtered against
//      the current loadout state (no NewWeapon when slots full, no
//      LevelUpWeapon for un-equipped weapons, etc.).
//   4. Raises OnCardsDrawn for the UI (LevelUpScreenController).
//   5. Awaits one of three player actions:
//        - Pick a card → Apply it, end the draft, advance.
//        - Skip       → No card applied, +1 to bankedRerolls, end the draft, advance.
//        - Reroll     → Costs 1 banked reroll, redraws the SAME draft (different cards).
//   6. Applies the picked card's effect via UpgradeCardSO.ApplyToAxis,
//      records the pick in OwnedCardIds, fires MusicEvent.CardPicked.
//   7. Transitions RunStateManager back to Running.
//
// Banked rerolls are run-scoped (cleared on RunStart). The bank cap is
// configurable; default 3.
//
// If multiple level-ups stack (XP gem cluster pushed past two thresholds
// in one frame), the queue handles them in FIFO order — one card screen
// per level, no batching.

using System;
using System.Collections.Generic;
using UnityEngine;
using CyberPickle.Core.Services;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.RunState;
using CyberPickle.Gameplay.Stats;
using CyberPickle.Gameplay.Weapons;

namespace CyberPickle.Gameplay.Progression
{
    [DisallowMultipleComponent]
    public class LevelUpCoordinator : MonoBehaviour
    {
        [Header("Pool")]
        [Tooltip("The card pool this run draws from. Future: per-character pools live on CharacterData; for M7.3 day-2 this is set per scene.")]
        [SerializeField] private UpgradePoolSO pool;

        [Header("Player Refs")]
        [Tooltip("Player's PlayerStats component. Lazy-discovered at first level-up. Inspector slot left available for explicit testing.")]
        [SerializeField] private PlayerStats playerStats;

        [Header("Draw Settings")]
        [Tooltip(
            "OBSOLETE — kept for back-compat with existing scenes. Card count is now driven by " +
            "RarityRollService.CardsVisibleForLuck (3 base, +1 per 50 Luck, cap 6) per CLAUDE.md " +
            "design pillar. This inspector value is only used as a floor when Luck = 0.")]
        [Min(1)] [SerializeField] private int cardsPerOfferFloor = 3;

        [Header("Banked Rerolls")]
        [Tooltip("Maximum number of banked rerolls. Skip-without-pick adds 1 to the bank; rerolls drain it. Run-scoped (resets on RunStart).")]
        [Min(0)] [SerializeField] private int bankedRerollsCap = 3;

        [Header("Diagnostics")]
        [Tooltip("Log each level-up + card pick to the console.")]
        [SerializeField] private bool verbose = true;

        // ─── Public events for the UI layer ───────────────────────────────

        /// <summary>Fires when the coordinator has drawn cards and the UI should display them.</summary>
        public event Action<IReadOnlyList<DraftedCard>> OnCardsDrawn;

        /// <summary>Fires after the picked card's effect has been applied. Useful for animation hooks.</summary>
        public event Action<DraftedCard> OnCardApplied;

        /// <summary>Fires when banked rerolls change (UI updates the Reroll button label).</summary>
        public event Action<int> OnBankedRerollsChanged;

        /// <summary>
        /// Fires when the multi-level-up stack state changes (start, advance, end).
        /// Arguments: (currentIndex, totalInStack). currentIndex is 1-based.
        /// When totalInStack <= 1 the UI should hide the stack indicator.
        /// </summary>
        public event Action<int, int> OnStackProgressChanged;

        /// <summary>Current number of banked rerolls.</summary>
        public int BankedRerolls { get; private set; }

        /// <summary>Cap on banked rerolls (inspector-configured).</summary>
        public int BankedRerollsCap => bankedRerollsCap;

        /// <summary>Total picks expected in the currently-running multi-level-up stack. 0 or 1 = not in a stack.</summary>
        public int StackTotalPicks => _stackPlannedSize;

        /// <summary>1-based index of the currently-shown draft within the stack. 0 when no stack is active.</summary>
        public int StackCurrentPick => _stackPicksMade + 1;

        /// <summary>
        /// True when a level-up draft is currently being shown to the player
        /// (panel up, run paused). UI controllers read this AFTER calling
        /// NotifyCardPicked / NotifyDraftSkipped to decide whether to tear
        /// down their visuals: if another draft already opened synchronously
        /// (multi-level stack continuation), the UI should stay put and let
        /// HandleCardsDrawn rebind it — tearing down would undo that work.
        /// </summary>
        public bool IsDrafting => _screenActive;

        // ─── Public API for the UI to call back into ──────────────────────

        /// <summary>
        /// Player picked a card from the current draft. Applies it, records
        /// ownership, resumes the run.
        ///
        /// For cards with <c>RequiresSlotSelection == true</c>, callers can
        /// supply <paramref name="axisIndex"/>; pass -1 to fall back to
        /// "first empty axis" auto-pick.
        /// </summary>
        public void NotifyCardPicked(DraftedCard card, int axisIndex = -1)
        {
            if (!card.IsValid)
            {
                Debug.LogError("[LevelUpCoordinator] NotifyCardPicked called with invalid card.");
                ResumeRun();
                return;
            }

            if (!EnsurePlayerStats())
            {
                Debug.LogError("[LevelUpCoordinator] No playerStats reference — cannot apply card. Resuming anyway.");
                ResumeRun();
                return;
            }

            var loadout = WeaponLoadoutRuntime.Instance;
            // Pass resolved targets from the DraftedCard wrapper. For
            // TEMPLATE cards (source.targetWeaponData == null) the pool's
            // draft logic stuffed the resolved weapon onto the wrapper; the
            // card's Apply prefers it over the SO's authored target. For
            // legacy specific cards these are null and Apply uses the SO's
            // authored target as before.
            string applyResult = card.source.ApplyToAxis(
                playerStats, loadout, axisIndex,
                card.rolledElement, card.rolledRarity,
                card.resolvedTargetWeapon, card.resolvedTargetPowerUp);
            _ownedCardIds.Add(card.source.cardId);
            if (verbose)
                Debug.Log($"[LevelUpCoordinator] Picked '{card.source.cardId}' ({card.source.cardType}, {card.rolledRarity}/{card.rolledElement}, axis={axisIndex}): {applyResult}.");

            MusicEventBus.Fire(MusicEvent.CardPicked, card.source.cardId);
            OnCardApplied?.Invoke(card);

            AdvanceStackProgress();
            AdvanceToNextDraftOrResume();
        }

        /// <summary>
        /// Player chose to skip this draft entirely (didn't pick any card).
        /// Adds +1 to the banked-reroll counter (clamped at cap), ends the
        /// current draft, advances. No card applied, no card recorded as owned.
        /// </summary>
        public void NotifyDraftSkipped()
        {
            int previousBank = BankedRerolls;
            BankedRerolls = Mathf.Min(bankedRerollsCap, BankedRerolls + 1);

            if (verbose)
            {
                if (BankedRerolls > previousBank)
                    Debug.Log($"[LevelUpCoordinator] Draft skipped. Banked rerolls: {previousBank} → {BankedRerolls}.");
                else
                    Debug.Log($"[LevelUpCoordinator] Draft skipped. Bank already at cap ({bankedRerollsCap}); +1 reroll discarded.");
            }

            OnBankedRerollsChanged?.Invoke(BankedRerolls);
            MusicEventBus.Fire(MusicEvent.CardSkipped, null);

            AdvanceStackProgress();
            AdvanceToNextDraftOrResume();
        }

        /// <summary>
        /// Player spent 1 banked reroll to redraw the current draft.
        /// Returns the new card list (also fires OnCardsDrawn so the UI can
        /// just listen to the event). Returns an empty list if no banked
        /// rerolls available — UI should disable the Reroll button when
        /// BankedRerolls == 0.
        /// </summary>
        public IReadOnlyList<DraftedCard> NotifyRerollRequested()
        {
            if (BankedRerolls <= 0)
            {
                if (verbose) Debug.Log("[LevelUpCoordinator] Reroll requested but no banked rerolls available.");
                return Array.Empty<DraftedCard>();
            }

            BankedRerolls--;
            OnBankedRerollsChanged?.Invoke(BankedRerolls);

            if (verbose) Debug.Log($"[LevelUpCoordinator] Reroll spent. Banked rerolls: {BankedRerolls + 1} → {BankedRerolls}. Redrawing draft.");

            // Re-draw using current loadout + Luck.
            float luck = EnsurePlayerStats() ? playerStats.Get(PlayerStatType.Luck) : 0f;
            int count = Mathf.Max(cardsPerOfferFloor, RarityRollService.CardsVisibleForLuck(luck));
            var loadout = WeaponLoadoutRuntime.Instance;
            var cards = pool.DrawCards(count, luck, loadout, _banishedCardIds, _ownedCardIds);

            OnCardsDrawn?.Invoke(cards);
            return cards;
        }

        /// <summary>Banish a card from the rest-of-run pool. Doesn't end the draft.</summary>
        public void NotifyCardBanished(DraftedCard card)
        {
            if (!card.IsValid) return;
            _banishedCardIds.Add(card.source.cardId);
            MusicEventBus.Fire(MusicEvent.CardBanished, card.source.cardId);
            if (verbose) Debug.Log($"[LevelUpCoordinator] Banished '{card.source.cardId}'.");
        }

        public IReadOnlyCollection<string> OwnedCardIds => _ownedCardIds;
        public IReadOnlyCollection<string> BanishedCardIds => _banishedCardIds;

        // ─── Internal state ───────────────────────────────────────────────

        private readonly HashSet<string> _ownedCardIds = new HashSet<string>();
        private readonly HashSet<string> _banishedCardIds = new HashSet<string>();
        private readonly Queue<int> _pendingLevels = new Queue<int>();
        private bool _screenActive;

        // Multi-level-up stack tracking. Set by MultiLevelUp event when a
        // burst is about to land; advanced on each pick / skip. Reset to 0
        // when the stack completes. Used by the UI to render "k of N"
        // progress on the level-up screens.
        private int _stackPlannedSize;
        private int _stackPicksMade;

        // ─── Lifecycle ────────────────────────────────────────────────────

        private void OnEnable()
        {
            MusicEventBus.OnEvent += HandleMusicEvent;
        }

        private void OnDisable()
        {
            MusicEventBus.OnEvent -= HandleMusicEvent;
        }

        private void HandleMusicEvent(MusicEvent type, object payload)
        {
            switch (type)
            {
                case MusicEvent.RunStart:
                    CachePlayerRefs();
                    ResetRunScopedState();
                    break;

                case MusicEvent.MultiLevelUp:
                    // Fired by PlayerXPBridge BEFORE the per-level events
                    // when a single XP delta will cross multiple thresholds.
                    // Pre-sizes the stack so the very first draft screen
                    // can render "1 of N" instead of "1 of ?".
                    int totalLevels = payload is int n ? n : 0;
                    if (totalLevels >= 2)
                    {
                        _stackPlannedSize = totalLevels;
                        _stackPicksMade = 0;
                        if (verbose) Debug.Log($"[LevelUpCoordinator] Multi-level-up stack starting: {totalLevels} picks.");
                    }
                    break;

                case MusicEvent.LevelUp:
                    int newLevel = payload is int l ? l : 0;
                    EnqueueLevelUp(newLevel);
                    break;
            }
        }

        private void CachePlayerRefs()
        {
            if (playerStats == null)
                playerStats = FindFirstObjectByType<PlayerStats>();

            if (playerStats == null)
                Debug.LogError("[LevelUpCoordinator] CachePlayerRefs: no PlayerStats found at RunStart.");
            else if (verbose)
                Debug.Log($"[LevelUpCoordinator] Cached PlayerStats on '{playerStats.name}' at RunStart.");
        }

        private void ResetRunScopedState()
        {
            _ownedCardIds.Clear();
            _banishedCardIds.Clear();
            _pendingLevels.Clear();
            _screenActive = false;
            _stackPlannedSize = 0;
            _stackPicksMade = 0;
            // Notify any UI listeners that the stack indicator should hide.
            OnStackProgressChanged?.Invoke(0, 0);

            if (BankedRerolls != 0)
            {
                BankedRerolls = 0;
                OnBankedRerollsChanged?.Invoke(0);
            }
        }

        private bool EnsurePlayerStats()
        {
            if (playerStats != null) return true;
            CachePlayerRefs();
            return playerStats != null;
        }

        // ─── Level-up flow ────────────────────────────────────────────────

        private void EnqueueLevelUp(int newLevel)
        {
            _pendingLevels.Enqueue(newLevel);
            if (verbose)
                Debug.Log($"[LevelUpCoordinator] Level-up queued: {newLevel}. Queue depth = {_pendingLevels.Count}.");

            if (!_screenActive)
                ProcessNextPendingLevelUp();
        }

        private void ProcessNextPendingLevelUp()
        {
            if (_pendingLevels.Count == 0) return;
            int level = _pendingLevels.Dequeue();
            ShowLevelUpScreen(level);
        }

        /// <summary>
        /// Drives the transition between drafts. If more levels are queued,
        /// show the next draft directly (stays in LevelUpPaused, fires
        /// OnCardsDrawn so the UI rebinds). If the queue is empty, resume
        /// the run.
        ///
        /// Critical: ProcessNext must run BEFORE ResumeRun so we don't briefly
        /// flip the run state Running→LevelUpPaused mid-stack — that would
        /// fire phase-change events to the music conductor twice per stack
        /// transition and re-trigger anything that listens for run resume.
        /// </summary>
        private void AdvanceToNextDraftOrResume()
        {
            if (_pendingLevels.Count > 0)
            {
                // Stack continuation — keep _screenActive true throughout.
                // ShowLevelUpScreen sets it true again (idempotent) and the
                // same-state phase transition is guarded in ShowLevelUpScreen.
                ProcessNextPendingLevelUp();
            }
            else
            {
                ResumeRun();
            }
        }

        private void ShowLevelUpScreen(int newLevel)
        {
            if (pool == null)
            {
                Debug.LogError("[LevelUpCoordinator] No UpgradePoolSO assigned. Cannot draw cards.");
                return;
            }

            // Guard same-state transition during multi-level stacks — we'd
            // otherwise fire OnPhaseChanged for LevelUpPaused → LevelUpPaused
            // on every continuation, which double-pings every consumer of
            // the phase event (music conductor, HUD, etc.).
            if (RunStateManager.Instance != null && RunStateManager.Instance.CurrentPhase != RunStatePhase.LevelUpPaused)
                RunStateManager.Instance.TransitionTo(RunStatePhase.LevelUpPaused);

            // Card count: Luck-driven via RarityRollService.CardsVisibleForLuck.
            // Floor at the inspector-configured count for legacy scenes.
            float luck = EnsurePlayerStats() ? playerStats.Get(PlayerStatType.Luck) : 0f;
            int count = Mathf.Max(cardsPerOfferFloor, RarityRollService.CardsVisibleForLuck(luck));
            var loadout = WeaponLoadoutRuntime.Instance;
            var cards = pool.DrawCards(count, luck, loadout, _banishedCardIds, _ownedCardIds);

            if (cards.Count == 0)
            {
                Debug.LogWarning("[LevelUpCoordinator] Pool returned 0 eligible cards. Resuming run with no offer.");
                ResumeRun();
                ProcessNextPendingLevelUp();
                return;
            }

            if (verbose)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < cards.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    var d = cards[i];
                    sb.Append($"{d.source.cardId}({d.rolledRarity}");
                    if (d.rolledElement != Core.ElementId.None) sb.Append($"/{d.rolledElement}");
                    sb.Append(')');
                }
                Debug.Log($"[LevelUpCoordinator] Drew {cards.Count} cards for level {newLevel}: {sb}");
            }

            _screenActive = true;

            // Tell the UI which slot of the stack this draft represents.
            // For non-stacked level-ups (single level gained) we still emit
            // (1, 1) so the UI can decide whether to show or hide based on
            // total > 1.
            int totalForUI = _stackPlannedSize >= 2 ? _stackPlannedSize : 1;
            int currentForUI = _stackPicksMade + 1;
            OnStackProgressChanged?.Invoke(currentForUI, totalForUI);

            OnCardsDrawn?.Invoke(cards);
        }

        /// <summary>
        /// Advance the stack counter and broadcast the new progress to UI.
        /// Called from NotifyCardPicked and NotifyDraftSkipped — NOT from
        /// NotifyRerollRequested (rerolls don't move the stack forward).
        /// When all picks land, resets the stack state to (0,0).
        /// </summary>
        private void AdvanceStackProgress()
        {
            if (_stackPlannedSize < 2) return; // not in a multi-level stack

            _stackPicksMade++;
            if (_stackPicksMade >= _stackPlannedSize)
            {
                if (verbose) Debug.Log($"[LevelUpCoordinator] Multi-level-up stack complete ({_stackPlannedSize} picks).");
                _stackPlannedSize = 0;
                _stackPicksMade = 0;
                OnStackProgressChanged?.Invoke(0, 0);
            }
        }

        private void ResumeRun()
        {
            _screenActive = false;
            if (RunStateManager.Instance != null && RunStateManager.Instance.CurrentPhase == RunStatePhase.LevelUpPaused)
            {
                RunStateManager.Instance.TransitionTo(RunStatePhase.Running);
            }
        }
    }
}
