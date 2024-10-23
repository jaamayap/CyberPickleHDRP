using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Documentation("Turn on and off objects based on the Projectile Spawners lifecycle events.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/observers-global-observers-and-observer-objects")]
    [Serializable]
    public class SpawnOnSpawnPointsSpawnerObserverObject : SpawnerLifecycleActionsObserverObject
    {
        [Header("Spawn Options")] public List<GameObject> objects = new();

        public bool parentToSpawnPosition = true;
        public bool spawnOnAllSpawnPoints;
        public Vector3 offset;

        private List<GameObject> _objectSpawned = new();

        protected override void DoOnAction(GameObject projectileObject = default,
            SpawnBehavior spawnBehaviorInstance = default,
            Transform spawnTransform = default)
        {
            if (delayBeforeOnAction > 0)
            {
                StopTheCoroutine();
                _onActionCoroutine = StartCoroutine(OnActionsCoroutine(true));
                return;
            }

            SpawnAllObjects();
        }

        private void SpawnAllObjects()
        {
            if (!spawnOnAllSpawnPoints)
            {
                foreach (var obj in objects)
                    SpawnObject(obj, targetSpawner.LastSpawnPoint);
                return;
            }

            foreach (var spawnPoint in targetSpawner.spawnPoints)
            foreach (var obj in objects)
                SpawnObject(obj, spawnPoint);
        }

        protected override IEnumerator OnActionsCoroutine(bool value, Projectile projectile = null,
            ProjectileSpawner projectileSpawner = null, Collision collision = default, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            yield return new WaitForSeconds(value ? delayBeforeOnAction : turnOffDelay);

            SpawnAllObjects();
        }

        protected override void DoOffAction(Projectile projectile = null, ProjectileSpawner projectileSpawner = null,
            Collision collision = default, GameObject objectHit = null, Vector3 contactPoint = default)
        {
            foreach (var obj in _objectSpawned)
                Destroy(obj);
        }

        protected virtual void SpawnObject(GameObject obj, SpawnPoint spawnPoint)
        {
            // Convert targetProjectile.ProjectileSpawner.LastSpawnRotation to a quaternion
            var spawnQuaternion = Quaternion.Euler(spawnPoint.Rotation);

            var newObj = Instantiate(obj, spawnPoint.Position + offset, spawnQuaternion);
            if (parentToSpawnPosition)
                newObj.transform.SetParent(spawnPoint.transform);

            _objectSpawned.Add(newObj);
        }
    }
}