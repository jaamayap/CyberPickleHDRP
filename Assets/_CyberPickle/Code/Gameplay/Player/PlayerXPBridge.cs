// File: Assets/_CyberPickle/Code/Gameplay/Player/PlayerXPBridge.cs
// Namespace: CyberPickle.Gameplay.Player
//
// MonoBehaviour <-> ECS bridge for the player's XP / level state.
//
//   - Creates / maintains the PlayerXP singleton entity.
//   - Each Update: reads the singleton; if CurrentXP >= XPToNextLevel,
//     consumes the threshold (CurrentXP -= XPToNextLevel), increments
//     CurrentLevel, raises the threshold for the next level, and fires
//     OnLevelUp / OnXPChanged events for HUD + level-up screen.
//   - Exposes CurrentXP / CurrentLevel / XPToNextLevel public getters
//     for the HUD to read.
//
// 2026-05-11 fix — RUN-RESET ON RETRY:
//   The PlayerXP entity lives in the DOTS World, which is independent of
//   the Unity scene lifecycle. On retry, the scene reloads but the entity
//   persists, so a fresh PlayerXPBridge would adopt last run's level / XP
//   instead of starting clean. Now subscribes to MusicEvent.RunStart and
//   resets the singleton's contents to (level = startingLevel, XP = 0,
//   threshold = ComputeXPThreshold(startingLevel)) on every run start.
//   Same pattern as LevelUpCoordinator / PerWeaponStatsTracker.
//
// XP curve: simple "level² × xpPerLevelBase" — first level needs 10 XP,
// level 2 needs ~40, level 3 needs ~90. Tunable below; replace with a
// curve asset when balance starts mattering.

using System;
using Unity.Entities;
using UnityEngine;
using CyberPickle.DOTS.Components;
using CyberPickle.Gameplay.Audio;

namespace CyberPickle.Gameplay.Player
{
    [DisallowMultipleComponent]
    public class PlayerXPBridge : MonoBehaviour
    {
        [Header("Initial State")]
        [Tooltip("Starting level. 1 by default.")]
        [Min(1)] public int startingLevel = 1;

        [Header("XP Curve")]
        [Tooltip("XP needed for level 1 -> 2. The next-level threshold scales with level².")]
        [Min(1)] public int xpPerLevelBase = 10;

        [Tooltip("Quadratic scaling factor on the level² term. Higher = steeper curve.")]
        [Min(0.1f)] public float xpCurveExponent = 1f;

        // ─── Events ─────────────────────────────────────────────────────────

        /// <summary>Fires when level-up threshold is crossed. Argument = new level.</summary>
        public event Action<int> OnLevelUp;

        /// <summary>Fires every time CurrentXP changes (gem collected, level-up). Useful for HUD progress bar.</summary>
        public event Action<int, int, int> OnXPChanged; // (currentXP, xpToNextLevel, currentLevel)

        // ─── Public read-only state ─────────────────────────────────────────

        public int CurrentXP { get; private set; }
        public int CurrentLevel { get; private set; }
        public int XPToNextLevel { get; private set; }

        // ─── Internal ───────────────────────────────────────────────────────

        private EntityManager entityManager;
        private Entity singletonEntity = Entity.Null;
        private bool initialized;
        private int lastReportedXP = -1;

        private void OnEnable()
        {
            EnsureSingleton();
            MusicEventBus.OnEvent += HandleMusicEvent;
        }

        private void OnDisable()
        {
            MusicEventBus.OnEvent -= HandleMusicEvent;
            initialized = false;
        }

        private void HandleMusicEvent(MusicEvent type, object _)
        {
            if (type == MusicEvent.RunStart) ResetForNewRun();
        }

        /// <summary>
        /// Reset the PlayerXP singleton's contents to the initial state
        /// (level = startingLevel, XP = 0, threshold = curve at level 1).
        /// Called on MusicEvent.RunStart so retries / new runs start clean
        /// regardless of the DOTS entity surviving the scene reload.
        ///
        /// Does NOT destroy the singleton entity — just rewrites its data.
        /// Destruction-and-recreate would also work but risks racing with
        /// any other system that cached the entity reference.
        /// </summary>
        private void ResetForNewRun()
        {
            EnsureSingleton();
            if (!initialized) return;
            if (!entityManager.Exists(singletonEntity)) return;

            CurrentLevel  = startingLevel;
            CurrentXP     = 0;
            XPToNextLevel = ComputeXPThreshold(CurrentLevel);

            entityManager.SetComponentData(singletonEntity, new PlayerXP
            {
                CurrentLevel   = CurrentLevel,
                CurrentXP      = CurrentXP,
                XPToNextLevel  = XPToNextLevel,
                LevelUpPending = false,
            });

            // Force OnXPChanged to fire on the next Update so the HUD bar
            // snaps to 0/threshold immediately (without this it would only
            // fire when an XP gem was picked up, leaving the bar showing
            // last run's progress until then).
            lastReportedXP = -1;
        }

        private void EnsureSingleton()
        {
            if (initialized) return;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogWarning("[PlayerXPBridge] No DefaultGameObjectInjectionWorld — DOTS not initialized.");
                return;
            }

            entityManager = world.EntityManager;

            EntityQuery query = entityManager.CreateEntityQuery(typeof(PlayerXP));
            if (query.CalculateEntityCount() > 0)
            {
                singletonEntity = query.GetSingletonEntity();
            }
            else
            {
                singletonEntity = entityManager.CreateEntity(typeof(PlayerXP));
                entityManager.SetName(singletonEntity, "PlayerXPSingleton");

                CurrentLevel = startingLevel;
                CurrentXP = 0;
                XPToNextLevel = ComputeXPThreshold(CurrentLevel);

                entityManager.SetComponentData(singletonEntity, new PlayerXP
                {
                    CurrentLevel    = CurrentLevel,
                    CurrentXP       = CurrentXP,
                    XPToNextLevel   = XPToNextLevel,
                    LevelUpPending  = false,
                });
            }
            query.Dispose();

            initialized = true;
        }

        private void Update()
        {
            if (!initialized)
            {
                EnsureSingleton();
                if (!initialized) return;
            }

            if (!entityManager.Exists(singletonEntity))
            {
                initialized = false;
                EnsureSingleton();
                if (!initialized) return;
            }

            var xp = entityManager.GetComponentData<PlayerXP>(singletonEntity);

            // Consume any level-up thresholds that have been crossed.
            // Loop in case a single big drop pushed us past multiple levels.
            bool changed = false;
            while (xp.CurrentXP >= xp.XPToNextLevel)
            {
                xp.CurrentXP -= xp.XPToNextLevel;
                xp.CurrentLevel += 1;
                xp.XPToNextLevel = ComputeXPThreshold(xp.CurrentLevel);
                xp.LevelUpPending = true;
                changed = true;

                OnLevelUp?.Invoke(xp.CurrentLevel);
                // Broadcast onto the audio bus too — LevelUpCoordinator
                // listens here to drive the choice screen, and future
                // music systems use this for level-up stingers.
                MusicEventBus.Fire(MusicEvent.LevelUp, xp.CurrentLevel);
            }

            if (changed)
            {
                entityManager.SetComponentData(singletonEntity, xp);
            }

            // Mirror to the public getters and notify on any XP change.
            CurrentXP = xp.CurrentXP;
            CurrentLevel = xp.CurrentLevel;
            XPToNextLevel = xp.XPToNextLevel;

            if (xp.CurrentXP != lastReportedXP)
            {
                lastReportedXP = xp.CurrentXP;
                OnXPChanged?.Invoke(CurrentXP, XPToNextLevel, CurrentLevel);
            }
        }

        /// <summary>
        /// XP curve: threshold(level) = ceil(xpPerLevelBase × level^(1 + curveExponent)).
        /// Level 1->2 needs xpPerLevelBase XP exactly. Each subsequent level scales.
        /// </summary>
        private int ComputeXPThreshold(int level)
        {
            float exponent = 1f + xpCurveExponent;
            return Mathf.Max(1, Mathf.CeilToInt(xpPerLevelBase * Mathf.Pow(level, exponent)));
        }
    }
}
