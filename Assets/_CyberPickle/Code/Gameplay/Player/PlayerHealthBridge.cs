// File: Assets/_CyberPickle/Code/Gameplay/Player/PlayerHealthBridge.cs
// Namespace: CyberPickle.Gameplay.Player
//
// Bidirectional bridge between PlayerHealth (MonoBehaviour) and
// PlayerHealthData (ECS singleton).
//
// Outbound (MB → ECS): mirrors CurrentHealth / MaxHealth / IsAlive into
// the singleton each frame, only when changed (event-driven dirty bit).
// Used by HUD systems and any ECS code that needs to react to player
// health.
//
// Inbound (ECS → MB): drains PendingDamage from the singleton each
// frame and forwards to PlayerHealth.TakeDamage. ECS damage sources
// (EnemyContactDamageSystem, future enemy-projectile system) just += into
// PendingDamage; the bridge handles the application + i-frame logic
// via PlayerHealth.
//
// Lifecycle: disabled by default on character prefabs (matches
// PlayerPositionBridge / PlayerStatsBridge / PlayerXPBridge pattern).
// GameSceneBootstrap activates it on the spawned player.

using Unity.Entities;
using UnityEngine;
using CyberPickle.DOTS.Components;

namespace CyberPickle.Gameplay.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerHealthBridge : MonoBehaviour
    {
        private PlayerHealth   _health;
        private EntityManager  _entityManager;
        private Entity         _singletonEntity = Entity.Null;
        private bool           _initialized;
        private bool           _outboundDirty = true; // first frame writes a baseline

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
        }

        private void OnEnable()
        {
            if (_health != null)
                _health.OnHealthChanged += HandleHealthChanged;
            _outboundDirty = true;
            EnsureSingleton();
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.OnHealthChanged -= HandleHealthChanged;
            _initialized = false;
        }

        private void HandleHealthChanged(float current, float max) => _outboundDirty = true;

        private void EnsureSingleton()
        {
            if (_initialized) return;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogWarning("[PlayerHealthBridge] No DefaultGameObjectInjectionWorld — DOTS not initialized.");
                return;
            }

            _entityManager = world.EntityManager;

            EntityQuery query = _entityManager.CreateEntityQuery(typeof(PlayerHealthData));
            if (query.CalculateEntityCount() > 0)
            {
                _singletonEntity = query.GetSingletonEntity();
            }
            else
            {
                _singletonEntity = _entityManager.CreateEntity(typeof(PlayerHealthData));
                _entityManager.SetName(_singletonEntity, "PlayerHealthSingleton");
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
                _initialized = false;
                EnsureSingleton();
                if (!_initialized) return;
            }

            // ─── Inbound: drain ECS-accumulated damage ──────────────────────
            // Read PendingDamage, zero it, apply via PlayerHealth (which
            // handles i-frames + Defense reduction).
            var data = _entityManager.GetComponentData<PlayerHealthData>(_singletonEntity);
            if (data.PendingDamage > 0f && _health.IsAlive)
            {
                float damage = data.PendingDamage;
                data.PendingDamage = 0f;
                // Write zero back BEFORE TakeDamage so any ECS systems running
                // later this frame (after the bridge) start fresh.
                _entityManager.SetComponentData(_singletonEntity, data);
                _health.TakeDamage(damage);
                _outboundDirty = true; // health changed, force outbound write
            }

            // ─── Outbound: mirror MB state to the singleton ────────────────
            if (!_outboundDirty) return;

            data = _entityManager.GetComponentData<PlayerHealthData>(_singletonEntity);
            data.CurrentHealth = _health.CurrentHealth;
            data.MaxHealth     = _health.MaxHealth;
            data.IsAlive       = _health.IsAlive;
            // Don't overwrite PendingDamage — could have been written by ECS
            // systems that ran since the drain above.
            _entityManager.SetComponentData(_singletonEntity, data);

            _outboundDirty = false;
        }
    }
}
