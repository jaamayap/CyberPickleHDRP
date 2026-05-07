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
//   2. On level-up: transitions RunStateManager → LevelUpPaused (per
//      GDD §3.11, this freezes Time.timeScale. Future M7.3-day-5 polish
//      replaces full pause with timeScale=0.05 slow-mo).
//   3. Draws N cards from the active UpgradePoolSO (filtered by banished
//      + prerequisite, weighted by Luck).
//   4. Raises OnCardsDrawn for the UI (LevelUpScreenController).
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
// Inspector wiring: assign the active UpgradePoolSO. PlayerStats is
// lazy-discovered the first time it's needed (which is at card-pick time,
// by which point the player has been spawned for sure).

using System;
using System.Collections.Generic;
using UnityEngine;
using CyberPickle.Gameplay.Audio;
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
        [Tooltip("Player's PlayerStats component. Lazy-discovered at first level-up (the player isn't spawned yet at scene-load time, so we can't bind here in OnEnable). Inspector slot left available in case you want to wire explicitly for testing.")]
        [SerializeField] private PlayerStats playerStats;

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

            if (!EnsurePlayerStats())
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
            // Subscribe to the process-global music bus. This is the ONLY
            // listener we wire here because the bus is the only source we
            // can rely on to be alive at OnEnable time — components like
            // PlayerXPBridge don't exist yet (the player is spawned by
            // GameSceneBootstrap.Start, AFTER all OnEnables). PlayerStats
            // is lazy-discovered when needed (see EnsurePlayerStats).
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
                    // The player is spawned and PlayerStats is initialized BEFORE
                    // RunStateManager.TransitionTo(Running) fires this event (see
                    // GameSceneBootstrap.SpawnSelectedCharacter — InitializePlayerStats
                    // is called before TransitionTo). So by the time we land here,
                    // PlayerStats exists and is ready to read.
                    //
                    // Doing the find here means level-up events later are
                    // free of any FindFirstObjectByType cost. The find is O(n)
                    // in scene size — fine to pay once per run, expensive to
                    // repeat per level-up.
                    CachePlayerRefs();
                    ResetRunScopedState();
                    break;

                case MusicEvent.LevelUp:
                    int newLevel = payload is int l ? l : 0;
                    EnqueueLevelUp(newLevel);
                    break;
            }
        }

        /// <summary>
        /// One-time per-run cache of the player components. Honors any
        /// inspector-wired reference (don't overwrite if the user pre-bound).
        /// Logs an error if PlayerStats is somehow still missing — would
        /// indicate the player failed to spawn or the bootstrap mis-ordered
        /// stat init vs. RunStateManager transition.
        /// </summary>
        private void CachePlayerRefs()
        {
            if (playerStats == null)
                playerStats = FindFirstObjectByType<PlayerStats>();

            if (playerStats == null)
                Debug.LogError("[LevelUpCoordinator] CachePlayerRefs: no PlayerStats found at RunStart. Player did not spawn correctly.");
            else if (verbose)
                Debug.Log($"[LevelUpCoordinator] Cached PlayerStats on '{playerStats.name}' at RunStart.");
        }

        /// <summary>
        /// Resets per-run state. Called on RunStart so a Try Again from the
        /// results screen starts with a fresh banish list, fresh owned-cards
        /// list, and an empty pending-level queue. Without this, a previous
        /// run's banishments could carry over (currently masked by the fact
        /// that scene-bound managers are recreated on scene reload, but
        /// resetting here removes that dependency).
        /// </summary>
        private void ResetRunScopedState()
        {
            _ownedCardIds.Clear();
            _banishedCardIds.Clear();
            _pendingLevels.Clear();
            _screenActive = false;
        }

        /// <summary>
        /// Defensive fallback in case a level-up somehow fires before RunStart
        /// (direct-Play testing, time-travel via debug command, etc.). Returns
        /// true if PlayerStats is now valid; false if we genuinely can't find it.
        /// In the normal flow this is a no-op since CachePlayerRefs already ran.
        /// </summary>
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

            // Draw cards. Pull Luck for rarity weighting; tolerate missing
            // PlayerStats (would only happen on a direct-Play test that
            // skipped the bootstrap) and just use luck=0.
            float luck = EnsurePlayerStats() ? playerStats.Get(PlayerStatType.Luck) : 0f;
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
