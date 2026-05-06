// File: Assets/_CyberPickle/Code/Gameplay/Progression/LevelUpCoordinator.cs
// Namespace: CyberPickle.Gameplay.Progression
//
// Orchestrates the level-up flow:
//
//   1. Subscribes to PlayerXPBridge.OnLevelUp (managed C# event).
//   2. On level-up: transitions RunStateManager → LevelUpPaused (per
//      GDD §3.11, this freezes Time.timeScale. Future M7.3-day-5 polish
//      replaces full pause with timeScale=0.05 slow-mo).
//   3. Draws N cards from the active UpgradePoolSO (filtered by banished
//      + prerequisite, weighted by Luck).
//   4. Raises OnCardsDrawn for the UI (LevelUpScreenController, M7.3 day 3-4).
//   5. Awaits a card pick via NotifyCardPicked() (called by the UI).
//   6. Applies the picked card's modifiers to PlayerStats, records the
//      pick in OwnedCardIds for prerequisite tracking, fires
//      MusicEvent.CardPicked.
//   7. Transitions RunStateManager back to Running.
//
// If multiple level-ups stack (XP gem cluster pushed past two thresholds
// in one frame), the queue handles them in FIFO order — one card screen
// per level, no batching. This matches Vampire Survivors' UX.
//
// Lifecycle: drop on a [LevelUpCoordinator] empty GameObject in Game.unity.
// Inspector wiring: assign the active UpgradePoolSO + the PlayerStats and
// PlayerXPBridge components on the player root. Coordinator finds the
// SpawnedPlayer at run-start by listening to GameSceneBootstrap (deferred —
// for M7.3 day 2 we just inspector-bind directly; auto-discovery is
// day-5 polish).

using System;
using System.Collections.Generic;
using UnityEngine;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.Player;
using CyberPickle.Gameplay.RunState;
using CyberPickle.Gameplay.Stats;

namespace CyberPickle.Gameplay.Progression
{
    [DisallowMultipleComponent]
    public class LevelUpCoordinator : MonoBehaviour
    {
        [Header("Pool")]
        [Tooltip("The card pool this run draws from. Future: per-character pools live on CharacterData; for M7.3 day-2 this is set per scene.")]
        [SerializeField] private UpgradePoolSO pool;

        [Header("Player Refs")]
        [Tooltip("Player's PlayerStats component. Filled by GameSceneBootstrap when the player spawns (auto-discovered if left empty at Awake).")]
        [SerializeField] private PlayerStats playerStats;

        [Tooltip("Player's PlayerXPBridge component. Source of OnLevelUp events.")]
        [SerializeField] private PlayerXPBridge xpBridge;

        [Header("Draw Settings")]
        [Tooltip("How many cards to show on each level-up. Default 3 (Vampire Survivors / Survivor.io standard).")]
        [Min(1)] [SerializeField] private int cardsPerOffer = 3;

        [Header("Diagnostics")]
        [Tooltip("Log each level-up + card pick to the console.")]
        [SerializeField] private bool verbose = true;

        // ─── Public events for the UI layer ───────────────────────────────

        /// <summary>Fires when the coordinator has drawn cards and the UI should display them. Argument is the cards to show.</summary>
        public event Action<IReadOnlyList<UpgradeCardSO>> OnCardsDrawn;

        /// <summary>Fires after the picked card's modifiers have been applied. Argument is the picked card. Useful for animation hooks.</summary>
        public event Action<UpgradeCardSO> OnCardApplied;

        // ─── Public API for the UI to call back into ──────────────────────

        /// <summary>
        /// Called by the UI when the player picks a card. Applies modifiers,
        /// records ownership, resumes the run.
        /// </summary>
        public void NotifyCardPicked(UpgradeCardSO card)
        {
            if (card == null)
            {
                Debug.LogError("[LevelUpCoordinator] NotifyCardPicked called with null card.");
                ResumeRun();
                return;
            }

            if (playerStats == null)
            {
                Debug.LogError("[LevelUpCoordinator] No playerStats reference — cannot apply card. Resuming anyway.");
                ResumeRun();
                return;
            }

            int applied = card.ApplyTo(playerStats);
            _ownedCardIds.Add(card.cardId);
            if (verbose)
                Debug.Log($"[LevelUpCoordinator] Applied '{card.cardId}' ({applied} modifiers). Stats now refreshed.");

            MusicEventBus.Fire(MusicEvent.CardPicked, card.cardId);
            OnCardApplied?.Invoke(card);

            ResumeRun();
            ProcessNextPendingLevelUp();
        }

        /// <summary>
        /// Called by the UI when the player banishes a card from the pool.
        /// The card stays in this offer (the player still has to pick from
        /// the remaining cards) but won't appear in future draws this run.
        /// </summary>
        public void NotifyCardBanished(UpgradeCardSO card)
        {
            if (card == null) return;
            _banishedCardIds.Add(card.cardId);
            MusicEventBus.Fire(MusicEvent.CardBanished, card.cardId);
            if (verbose) Debug.Log($"[LevelUpCoordinator] Banished '{card.cardId}'.");
        }

        /// <summary>Diagnostic accessor. UI uses to grey-out already-owned cards if relevant.</summary>
        public IReadOnlyCollection<string> OwnedCardIds => _ownedCardIds;

        /// <summary>Diagnostic accessor. UI uses to grey-out banished cards.</summary>
        public IReadOnlyCollection<string> BanishedCardIds => _banishedCardIds;

        // ─── Internal state ───────────────────────────────────────────────

        private readonly HashSet<string> _ownedCardIds = new HashSet<string>();
        private readonly HashSet<string> _banishedCardIds = new HashSet<string>();
        private readonly Queue<int> _pendingLevels = new Queue<int>();
        private bool _screenActive;

        // ─── Lifecycle ────────────────────────────────────────────────────

        private void OnEnable()
        {
            // Inspector binding is the primary path; auto-discovery is a
            // safety net for direct-Play in the editor without a full Boot
            // scene flow.
            TryAutoDiscoverPlayerRefs();

            if (xpBridge != null)
            {
                xpBridge.OnLevelUp += HandleLevelUp;
            }
            else
            {
                Debug.LogWarning("[LevelUpCoordinator] No PlayerXPBridge — coordinator will not receive level-up events.");
            }
        }

        private void OnDisable()
        {
            if (xpBridge != null)
                xpBridge.OnLevelUp -= HandleLevelUp;
        }

        private void TryAutoDiscoverPlayerRefs()
        {
            if (playerStats == null)
                playerStats = FindFirstObjectByType<PlayerStats>();
            if (xpBridge == null)
                xpBridge = FindFirstObjectByType<PlayerXPBridge>();
        }

        // ─── Level-up flow ────────────────────────────────────────────────

        private void HandleLevelUp(int newLevel)
        {
            _pendingLevels.Enqueue(newLevel);
            if (verbose)
                Debug.Log($"[LevelUpCoordinator] Level-up queued: {newLevel}. Queue depth = {_pendingLevels.Count}.");

            // If the screen isn't currently up, start processing. If it IS
            // up, this level-up is queued and processed when the current
            // pick completes.
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

            // Pause the run.
            if (RunStateManager.Instance != null)
                RunStateManager.Instance.TransitionTo(RunStatePhase.LevelUpPaused);

            // Draw cards.
            float luck = playerStats != null ? playerStats.Get(PlayerStatType.Luck) : 0f;
            var cards = pool.DrawCards(cardsPerOffer, luck, _banishedCardIds, _ownedCardIds);

            if (cards.Count == 0)
            {
                Debug.LogWarning("[LevelUpCoordinator] Pool returned 0 eligible cards. Resuming run with no offer.");
                ResumeRun();
                ProcessNextPendingLevelUp();
                return;
            }

            if (verbose)
            {
                string cardList = string.Join(", ", cards.ConvertAll(c => $"{c.cardId}({c.rarity})"));
                Debug.Log($"[LevelUpCoordinator] Drew {cards.Count} cards for level {newLevel}: {cardList}");
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
