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

        /// <summary>Current number of banked rerolls.</summary>
        public int BankedRerolls { get; private set; }

        /// <summary>Cap on banked rerolls (inspector-configured).</summary>
        public int BankedRerollsCap => bankedRerollsCap;

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
            string applyResult = card.source.ApplyToAxis(playerStats, loadout, axisIndex, card.rolledElement, card.rolledRarity);
            _ownedCardIds.Add(card.source.cardId);
            if (verbose)
                Debug.Log($"[LevelUpCoordinator] Picked '{card.source.cardId}' ({card.source.cardType}, {card.rolledRarity}/{card.rolledElement}, axis={axisIndex}): {applyResult}.");

            MusicEventBus.Fire(MusicEvent.CardPicked, card.source.cardId);
            OnCardApplied?.Invoke(card);

            ResumeRun();
            ProcessNextPendingLevelUp();
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

            ResumeRun();
            ProcessNextPendingLevelUp();
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

        private void ShowLevelUpScreen(int newLevel)
        {
            if (pool == null)
            {
                Debug.LogError("[LevelUpCoordinator] No UpgradePoolSO assigned. Cannot draw cards.");
                return;
            }

            if (RunStateManager.Instance != null)
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
            OnCardsDrawn?.Invoke(cards);
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
