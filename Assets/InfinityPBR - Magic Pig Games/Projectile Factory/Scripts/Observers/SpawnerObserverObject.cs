using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Documentation("Spawner Observer Objects are Monobehaviors that can observe Spawners and react to the Spawner " +
                   "Lifecycle Events [This one doesn't seem " +
                   "to have an updated Internal Description. Please add one by overriding this string.]",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/observers-global-observers-and-observer-objects")]
    [Serializable]
    public class SpawnerObserverObject : MonoBehaviour
    {
        [Header("Spawner to Observe")] public ProjectileSpawner targetSpawner;

        protected virtual void Start()
        {
            if (targetSpawner == null)
                targetSpawner = GetComponent<ProjectileSpawner>();

            if (targetSpawner == null)
            {
                Debug.LogError(
                    $"No ProjectileSpawner assigned to ObserverObject {this}. Please assign a ProjectileSpawner.");
                Destroy(this);
                return;
            }

            SubscribeToEvents(targetSpawner);
        }

        protected virtual void OnDestroy()
        {
            if (targetSpawner == null) return;
            UnsubscribeFromEvents(targetSpawner);
        }

        protected virtual void OnValidate()
        {
            if (targetSpawner == null)
                targetSpawner = GetComponent<ProjectileSpawner>();
        }

        public virtual void SubscribeToEvents(ProjectileSpawner projectileSpawner)
        {
            UnsubscribeFromEvents(projectileSpawner);
            projectileSpawner.OnProjectileLaunched += ProjectileLaunched;
            projectileSpawner.OnProjectileStopped += ProjectileStopped;
        }

        public virtual void UnsubscribeFromEvents(ProjectileSpawner projectileSpawner)
        {
            projectileSpawner.OnProjectileLaunched -= ProjectileLaunched;
            projectileSpawner.OnProjectileStopped -= ProjectileStopped;
        }

        protected virtual void ProjectileStopped()
        {
            // Do nothing
        }

        protected virtual void ProjectileLaunched(GameObject projectileObject, SpawnBehavior spawnBehaviorInstance,
            Transform spawnTransform)
        {
            // Do nothing
        }

        // Use this in situations where the projectile may not be live in the scene (i.e. Trajectory)
        public virtual void Tick()
        {
            // Do nothing
        }

        protected virtual void FixedTick()
        {
            // Do nothing
        }

        public virtual void LateTick()
        {
            // Do nothing
        }

        public virtual void Validate()
        {
            // Do nothing
        }

        public virtual void Disable(Projectile projectile)
        {
            // Do nothing
        }

        public virtual void Enable(Projectile projectile)
        {
            // Do nothing
        }
    }
}