using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Spawn Projectiles",
        menuName = "Projectile Factory/Generic Behavior/Spawn Projectiles")]
    [Serializable]
    public class SpawnProjectiles : ProjectileBehavior
    {
        [Header("Required")] public Projectile[] projectiles;

        [Header("Options")] public float startDelay = 1f;

        public float spawnInterval = 1f;

        [Min(-1)] [Tooltip("Set to -1 for infinite spawns")]
        public int totalSpawns = 10;

        public bool startSpawningOnLaunch = true;
        public bool destroyWhenDone = true;
        public float destroyDelay = 1f;

        [Header("Spawn Direction")] public bool spawnAtRandomDirection = true;

        public Vector3 spawnDirection = Vector3.back;
        public float offsetFromSpawner;

        private int _spawnedCount;

        private Coroutine _spawningCoroutine;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "Spawns a projectile at a set interval.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "CreateObject";

        private bool UsingObjectPool => Projectile.useObjectPool && ProjectilePoolManager.instance != null;

        public override void LaunchProjectile(Projectile projectile)
        {
            if (startSpawningOnLaunch)
                StartSpawningCoroutine();
        }

        public override void Disable(Projectile projectile)
        {
            if (_spawningCoroutine != null)
                Projectile.StopCoroutine(_spawningCoroutine);
        }

        public override void OnReturnToPool(Projectile projectile)
        {
            // Reset
            _spawnedCount = 0;
            _spawningCoroutine = null;
        }

        public override void OnGetFromPool(Projectile projectile)
        {
        }

        private void StartSpawningCoroutine()
        {
            if (_spawningCoroutine != null)
                return;

            if (!Projectile.gameObject.activeInHierarchy)
                Projectile.gameObject.SetActive(true);

            _spawningCoroutine = Projectile.StartCoroutine(SpawnProjectilesCoroutine());
        }

        private IEnumerator SpawnProjectilesCoroutine()
        {
            yield return new WaitForSeconds(startDelay);

            if (totalSpawns == -1)
                totalSpawns = int.MaxValue;
            for (var i = 0; i < totalSpawns; i++)
            {
                var projectileToSpawn = projectiles[Random.Range(0, projectiles.Length)];
                SpawnOneProjectile(projectileToSpawn);

                _spawnedCount++;
                yield return new WaitForSeconds(spawnInterval);
            }

            if (destroyWhenDone)
            {
                yield return new WaitForSeconds(destroyDelay);
                Projectile.TriggerDestroy();
            }
        }

        private void SpawnOneProjectile(Projectile basicProjectileToSpawn)
        {
            GameObject spawnedProjectile = null;
            if (UsingObjectPool)
                spawnedProjectile = ProjectilePoolManager.instance.GetProjectile(basicProjectileToSpawn.gameObject);

            if (spawnedProjectile == null)
                spawnedProjectile = Instantiate(basicProjectileToSpawn.gameObject, Projectile.transform.position,
                    Quaternion.identity);

            spawnedProjectile.transform.position =
                Projectile.transform.position + Projectile.transform.forward * offsetFromSpawner;

            var newProjectile = spawnedProjectile.GetComponent<Projectile>();
            newProjectile.SetProjectileSpawner(Projectile.ProjectileSpawner);
            newProjectile.ParentPrefab = basicProjectileToSpawn.gameObject;

            if (spawnAtRandomDirection)
                newProjectile.transform.forward = Random.onUnitSphere;
            else
                newProjectile.transform.forward = spawnDirection;

            newProjectile.AddObservers();

            newProjectile.Launch();
        }
    }
}