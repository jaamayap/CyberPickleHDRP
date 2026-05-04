// File: Assets/_CyberPickle/Code/Gameplay/Player/PlayerStatsBridge.cs
// Namespace: CyberPickle.Gameplay.Player
//
// MonoBehaviour <-> ECS bridge for player stats. Mirrors the player's
// effective stat values from PlayerStats (the source of truth) into a
// PlayerStatsData ECS singleton, so Burst-compiled systems (XPMagnetSystem,
// damage pipeline, etc.) can read them via SystemAPI.GetSingleton with
// no managed boundary cost.
//
// Performance:
//   - Subscribes to PlayerStats.OnStatsChanged. Marks _dirty on event.
//   - Each Update, writes the singleton ONLY if dirty. Avoids per-frame
//     SetComponentData when the player is idle.
//
// Lifecycle:
//   - Disabled by default on character prefabs (matches the pattern of
//     PlayerPositionBridge / PlayerXPBridge).
//   - GameSceneBootstrap activates it on the spawned player.
//   - Outside the Game scene there are no ECS systems consuming this
//     singleton, so the bridge stays inert.

using Unity.Entities;
using UnityEngine;
using CyberPickle.DOTS.Components;
using CyberPickle.Gameplay.Stats;

namespace CyberPickle.Gameplay.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerStatsBridge : MonoBehaviour
    {
        private PlayerStats   _stats;
        private EntityManager _entityManager;
        private Entity        _singletonEntity = Entity.Null;
        private bool          _initialized;
        private bool          _statsDirty = true; // start dirty so we write at least once on enable

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
        }

        private void OnEnable()
        {
            if (_stats != null)
                _stats.OnStatsChanged += HandleStatsChanged;

            _statsDirty = true;
            EnsureSingleton();
        }

        private void OnDisable()
        {
            if (_stats != null)
                _stats.OnStatsChanged -= HandleStatsChanged;

            // Don't destroy the singleton — other systems may still read it
            // briefly during scene teardown. Next world-init cleans up.
            _initialized = false;
        }

        private void HandleStatsChanged(PlayerStatType _) => _statsDirty = true;

        private void EnsureSingleton()
        {
            if (_initialized) return;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogWarning("[PlayerStatsBridge] No DefaultGameObjectInjectionWorld — DOTS not initialized.");
                return;
            }

            _entityManager = world.EntityManager;

            // Reuse an existing singleton if one is already in the world
            // (e.g., from a previous scene's bridge). Otherwise create one.
            EntityQuery query = _entityManager.CreateEntityQuery(typeof(PlayerStatsData));
            if (query.CalculateEntityCount() > 0)
            {
                _singletonEntity = query.GetSingletonEntity();
            }
            else
            {
                _singletonEntity = _entityManager.CreateEntity(typeof(PlayerStatsData));
                _entityManager.SetName(_singletonEntity, "PlayerStatsSingleton");
            }
            query.Dispose();

            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized)
            {
                EnsureSingleton();
                if (!_initialized) return;
            }

            if (!_entityManager.Exists(_singletonEntity))
            {
                // Singleton was destroyed (world reload?). Re-create.
                _initialized = false;
                EnsureSingleton();
                if (!_initialized) return;
            }

            if (!_statsDirty) return; // no change — skip the write

            _entityManager.SetComponentData(_singletonEntity, new PlayerStatsData
            {
                MaxHealth         = _stats.Get(PlayerStatType.MaxHealth),
                HealthRegen       = _stats.Get(PlayerStatType.HealthRegen),
                Defense           = _stats.Get(PlayerStatType.Defense),
                Power             = _stats.Get(PlayerStatType.Power),
                CritChance        = _stats.Get(PlayerStatType.CritChance),
                Lifesteal         = _stats.Get(PlayerStatType.Lifesteal),
                Speed             = _stats.Get(PlayerStatType.Speed),
                MagneticField     = _stats.Get(PlayerStatType.MagneticField),
                AreaOfEffect      = _stats.Get(PlayerStatType.AreaOfEffect),
                Dexterity         = _stats.Get(PlayerStatType.Dexterity),
                Luck              = _stats.Get(PlayerStatType.Luck),
                Hack              = _stats.Get(PlayerStatType.Hack),
                CooldownReduction = _stats.Get(PlayerStatType.CooldownReduction),
                NeuralAdaptation  = _stats.Get(PlayerStatType.NeuralAdaptation),
            });

            _statsDirty = false;
        }
    }
}
