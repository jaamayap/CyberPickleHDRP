using System.Collections.Generic;
using UnityEngine;

// DOCUMENTATION: https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/factory-manager

namespace MagicPigGames.ProjectileFactory
{
    public static class FactoryManager
    {
        public delegate void ProjectileCollisionHandler(Projectile projectile, Collision collision,
            GameObject objectHit = null, Vector3 contactPoint = default);

        public delegate void ProjectileHandler(Projectile projectile);

        // EVENTS

        public delegate void ProjectileHandlerNoVariables();

        public delegate void ProjectileSpawnerHandler(ProjectileSpawner spawner);

        public delegate void ProjectileSpawnerProjectileHandler(ProjectileSpawner spawner, Projectile newProjectile,
            Projectile oldProjectile);

        public delegate void ProjectileSpawnerSpawnPointHandler(ProjectileSpawner spawner, SpawnPoint newSpawnPoint,
            SpawnPoint oldSpawnPoint);

        public delegate void ProjectileTriggerHandler(Projectile projectile, Collider collider,
            GameObject objectHit = null, Vector3 contactPoint = default);

        public static List<ProjectileSpawner> ProjectileSpawners { get; } = new();
        public static List<Projectile> Projectiles { get; } = new();

        public static void RegisterProjectile(Projectile projectile)
        {
            if (Projectiles.Contains(projectile)) return;

            Projectiles.Add(projectile);
            SubscribeToEvents(projectile);
        }

        public static void UnregisterProjectile(Projectile projectile)
        {
            if (!Projectiles.Contains(projectile)) return;

            Projectiles.Remove(projectile);
            UnsubscribeFromEvents(projectile);
        }

        public static void RegisterProjectileSpawner(ProjectileSpawner spawner)
        {
            if (!ProjectileSpawners.Contains(spawner))
                ProjectileSpawners.Add(spawner);
        }

        public static void UnregisterProjectileSpawner(ProjectileSpawner spawner)
        {
            if (ProjectileSpawners.Contains(spawner))
                ProjectileSpawners.Remove(spawner);
        }

        // Extension method to attach a single observer to this spawner.
        public static void AttachGlobalObserver(this ProjectileSpawner spawner, ProjectileObserver observer)
        {
            if (!spawner.observers.Contains(observer))
                spawner.observers.Add(observer);
        }

        // Extension method to remove a single observer from this spawner.
        public static void DetachGlobalObserver(this ProjectileSpawner spawner, ProjectileObserver observer)
        {
            if (spawner.observers.Contains(observer))
                spawner.observers.Remove(observer);
        }

        // Adds a single observer to all spawners. Used after a new Global Observer is added.
        private static void AddGlobalObserverToSpawners(ProjectileObserver observer)
        {
            foreach (var spawner in ProjectileSpawners)
                spawner.AttachGlobalObserver(observer);
        }

        // Removes a single observer from all spawners. Used after a Global Observer is removed.
        private static void RemoveGlobalObserverFromSpawners(ProjectileObserver observer)
        {
            foreach (var spawner in ProjectileSpawners)
                spawner.DetachGlobalObserver(observer);
        }

        public static event ProjectileHandler OnLaunchGlobal;
        public static event ProjectileCollisionHandler CollisionEnterGlobal;
        public static event ProjectileCollisionHandler CollisionExitGlobal;
        public static event ProjectileCollisionHandler CollisionStayGlobal;
        public static event ProjectileTriggerHandler TriggerEnterGlobal;
        public static event ProjectileTriggerHandler TriggerExitGlobal;
        public static event ProjectileTriggerHandler TriggerStayGlobal;
        public static event ProjectileHandler DoDestroyGlobal;
        public static event ProjectileHandler DisableGlobal;
        public static event ProjectileHandler EnableGlobal;
        public static event ProjectileHandler ReturnToPoolGlobal;
        public static event ProjectileHandler GetFromPoolGlobal;

        private static void SubscribeToEvents(Projectile projectile)
        {
            UnsubscribeFromEvents(projectile);
            projectile.LaunchProjectile += LaunchProjectile;
            projectile.CollisionEnter += CollisionEnter;
            projectile.CollisionExit += CollisionExit;
            projectile.CollisionStay += CollisionStay;
            projectile.TriggerEnter += TriggerEnter;
            projectile.TriggerExit += TriggerExit;
            projectile.TriggerStay += TriggerStay;
            projectile.DoDestroy += DoDestroy;
            projectile.Disable += Disable;
            projectile.Enable += Enable;
            projectile.ReturnToPool += OnReturnToPool;
            projectile.GetFromPool += OnGetFromPool;
        }

        private static void UnsubscribeFromEvents(Projectile projectile)
        {
            projectile.LaunchProjectile -= LaunchProjectile;
            projectile.CollisionEnter -= CollisionEnter;
            projectile.CollisionExit -= CollisionExit;
            projectile.CollisionStay -= CollisionStay;
            projectile.TriggerEnter -= TriggerEnter;
            projectile.TriggerExit -= TriggerExit;
            projectile.TriggerStay -= TriggerStay;
            projectile.DoDestroy -= DoDestroy;
            projectile.Disable -= Disable;
            projectile.Enable -= Enable;
            projectile.ReturnToPool -= OnReturnToPool;
            projectile.GetFromPool -= OnGetFromPool;
        }

        private static void LaunchProjectile(Projectile projectile)
        {
            OnLaunchGlobal?.Invoke(projectile);
        }

        private static void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            CollisionEnterGlobal?.Invoke(projectile, collision, objectHit, contactPoint);
        }

        private static void CollisionExit(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            CollisionExitGlobal?.Invoke(projectile, collision, objectHit, contactPoint);
        }

        private static void CollisionStay(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            CollisionStayGlobal?.Invoke(projectile, collision, objectHit, contactPoint);
        }

        private static void TriggerEnter(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            TriggerEnterGlobal?.Invoke(projectile, collider, objectHit, contactPoint);
        }

        private static void TriggerExit(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            TriggerExitGlobal?.Invoke(projectile, collider, objectHit, contactPoint);
        }

        private static void TriggerStay(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            TriggerStayGlobal?.Invoke(projectile, collider, objectHit, contactPoint);
        }

        private static void DoDestroy(Projectile projectile)
        {
            DoDestroyGlobal?.Invoke(projectile);
        }

        private static void Disable(Projectile projectile)
        {
            DisableGlobal?.Invoke(projectile);
        }

        private static void Enable(Projectile projectile)
        {
            EnableGlobal?.Invoke(projectile);
        }

        private static void OnReturnToPool(Projectile projectile)
        {
            ReturnToPoolGlobal?.Invoke(projectile);
        }

        private static void OnGetFromPool(Projectile projectile)
        {
            GetFromPoolGlobal?.Invoke(projectile);
        }
    }
}