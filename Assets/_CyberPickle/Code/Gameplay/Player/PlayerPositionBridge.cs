// File: Assets/_CyberPickle/Code/Gameplay/Player/PlayerPositionBridge.cs
// Namespace: CyberPickle.Gameplay.Player
//
// MonoBehaviour <-> ECS bridge. Each Update, writes the player's world
// position into the PlayerPositionData singleton entity so ECS systems
// (enemy AI, projectile homing, magnet pickups, etc.) can read it
// without needing to look up the player MonoBehaviour from a Burst job.
//
// Lifecycle: this component is DISABLED on character prefabs by default
// (matches the pattern for PlayerInput / PlayerMotor / PlayerLoadoutLoader).
// GameSceneBootstrap activates it after spawning the player. Outside the
// Game scene there are no ECS systems consuming this data, so the bridge
// stays inert.

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CyberPickle.DOTS.Components;

namespace CyberPickle.Gameplay.Player
{
    [DisallowMultipleComponent]
    public class PlayerPositionBridge : MonoBehaviour
    {
        private EntityManager entityManager;
        private Entity singletonEntity = Entity.Null;
        private bool initialized;

        private void OnEnable()
        {
            EnsureSingleton();
        }

        private void OnDisable()
        {
            // We deliberately don't destroy the singleton entity here — other
            // systems may still reference it briefly during scene teardown.
            // The next world-init will clean it up.
            initialized = false;
        }

        private void EnsureSingleton()
        {
            if (initialized) return;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogWarning("[PlayerPositionBridge] No DefaultGameObjectInjectionWorld available — DOTS not initialized.");
                return;
            }

            entityManager = world.EntityManager;

            // Reuse an existing singleton if one is already in the world (e.g., from
            // a previous scene's bridge instance). Otherwise create one.
            EntityQuery query = entityManager.CreateEntityQuery(typeof(PlayerPositionData));
            if (query.CalculateEntityCount() > 0)
            {
                singletonEntity = query.GetSingletonEntity();
            }
            else
            {
                singletonEntity = entityManager.CreateEntity(typeof(PlayerPositionData));
                entityManager.SetName(singletonEntity, "PlayerPositionSingleton");
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
                // Singleton was destroyed (world reload?). Re-create.
                initialized = false;
                EnsureSingleton();
                if (!initialized) return;
            }

            Vector3 pos = transform.position;
            entityManager.SetComponentData(singletonEntity, new PlayerPositionData
            {
                Position = new float3(pos.x, pos.y, pos.z)
            });
        }
    }
}
