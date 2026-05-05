using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Documentation(
        "This pool manager will work with the Projectile Power system. It will manage the pool of projectiles. " +
        "Instantiation of new particles is done on the ProjectileSpawner.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/object-pooling")]
    [Serializable]
    public class ProjectilePoolManager : MonoBehaviour
    {
        public static ProjectilePoolManager instance;

        public bool debugLogs;
        public Color logColor = Color.cyan;

        public ConcurrentDictionary<GameObject, Queue<GameObject>> poolDictionary = new();
        public ConcurrentDictionary<string, Queue<GameObject>> poolGameObjectDictionary = new();

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogError("Multiple Pool Manager instances found! Will destroy this one.", this);
                Destroy(this);
            }
            else
            {
                instance = this;
            }
        }

        private void DebugLogMessage(string message)
        {
            if (!debugLogs) return;
            Debug.Log($"{Time.frameCount} <color=#{ColorUtility.ToHtmlStringRGB(logColor)}>{message}</color>");
        }

        // Search the dictionary based on the key, which is the prefab of the particle that runtime versions are 
        // instantiated from.
        public GameObject GetProjectile(GameObject objectToGet)
        {
            // Returning null means one doesn't exist, and then the spawner will instantiate a new one.
            if (!poolDictionary.TryGetValue(objectToGet, out var projectileQueue) ||
                projectileQueue.Count <= 0)
                return GetGameObject(objectToGet);

            DebugLogMessage($"Getting **PROJECTILE** {objectToGet.name}");
            return RetrieveFromPool(projectileQueue);
        }

        public GameObject GetGameObject(GameObject objectToGet)
        {
            DebugLogMessage($"Getting **GAME OBJECT** {objectToGet.name}");
            // Returning null means one doesn't exist, and then the spawner will instantiate a new one.
            if (!poolGameObjectDictionary.TryGetValue(objectToGet.name, out var projectileQueue) ||
                projectileQueue.Count <= 0)
            {
                DebugLogMessage($"No {objectToGet.name} in the pool -- Will instantiate a new one!");
                return null;
            }

            return RetrieveFromPool(projectileQueue);
        }

        private GameObject RetrieveFromPool(Queue<GameObject> objectQueue)
        {
            if (objectQueue.Count <= 0) return null;

            var numberOfObjectsInQueue = objectQueue.Count;
            var foundObject = objectQueue.Dequeue(); // Find an object from the pool
            var rigidBody = foundObject.GetComponent<Rigidbody>();

            var numberAfterDequeue = objectQueue.Count;
            if (rigidBody != null && !rigidBody.isKinematic)
            {
                rigidBody.linearVelocity = Vector3.zero;
                rigidBody.angularVelocity = Vector3.zero;
            }

            // Projectile Aspects
            var projectile = foundObject.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.DoGetFromPool();
                DebugLogMessage(
                    $"Found {foundObject.name}. Pool had {numberOfObjectsInQueue} and now {numberAfterDequeue}. ID: {projectile.poolUniqueID}");
                projectile.SetInPool(false);
            }

            foundObject.SetActive(true);
            return foundObject;
        }

        public void ReturnProjectile(GameObject projectileGameObject, Projectile basicProjectile)
        {
            // Avoid multiple calls in the same frame.
            if (basicProjectile.IsInPool)
                return;

            basicProjectile.SetInPool(true);

            var dictionaryKey = basicProjectile.ParentPrefab;

            if (dictionaryKey == null)
            {
                Debug.LogWarning("The dictionary key is null for the object(s) passed. Projectile is " +
                                 $"null: {basicProjectile == null}. Poolable object is null: {projectileGameObject == null}. " +
                                 "This may be caused by a projectile that was left in the scene, rather than being " +
                                 "created by the tool, meaning it did not have all of it's properties set.");
                return;
            }

            DebugLogMessage(
                $"Returning **PROJECTILE** {projectileGameObject.name} (ID: {basicProjectile.poolUniqueID}) to the pool");
            basicProjectile.DoReturnToPool();
            basicProjectile.ResetProjectile();

            var queue = GetOrCreateQueue(poolDictionary, dictionaryKey);
            queue.Enqueue(projectileGameObject); // add the object 
            projectileGameObject.SetActive(false);
        }

        private Queue<GameObject> GetOrCreateQueue(ConcurrentDictionary<GameObject, Queue<GameObject>> dictionary,
            GameObject key)
        {
            if (!dictionary.ContainsKey(key))
                dictionary[key] = new Queue<GameObject>();

            return dictionary[key];
        }

        private Queue<GameObject> GetOrCreateQueue(ConcurrentDictionary<string, Queue<GameObject>> dictionary,
            string key)
        {
            if (!dictionary.ContainsKey(key))
                dictionary[key] = new Queue<GameObject>();

            return dictionary[key];
        }

        public void ReturnGameObject(GameObject poolableObject)
        {
            // poolableObject is null if it was destroyed -- this can happen if there is a delay, and it
            // was parented to an object. We will avoid putting it back, if that happens.
            if (poolableObject == null)
            {
                return;
            }
            poolableObject.name = poolableObject.name.RemoveClone();
            DebugLogMessage($"Returning **GAME OBJECT** {poolableObject.name} to the pool");
            var dictionaryKey = poolableObject.name;

            var queue = GetOrCreateQueue(poolGameObjectDictionary, dictionaryKey);
            queue.Enqueue(poolableObject); // add the object back to the pool
            poolableObject.SetActive(false);
        }

        public void PutBackProjectile(Projectile basicProjectile, float delay)
        {
            if (instance == null) return;

            DebugLogMessage(
                $"Putting back **PROJECTILE** {basicProjectile.name} in the pool after {delay} seconds; gameObject: {basicProjectile.gameObject.name}");
            if (delay <= 0)
                ReturnProjectile(basicProjectile.gameObject, basicProjectile);

            StartCoroutine(PutBackProjectileCoroutine(basicProjectile, delay));
        }

        public void PutBackGameObject(GameObject objectToPutBack, float delay)
        {
            if (instance == null) return;

            DebugLogMessage($"Putting back **GAME OBJECT** {objectToPutBack.name} in the pool after {delay} seconds");

            if (delay <= 0)
                ReturnGameObject(objectToPutBack);

            StartCoroutine(PutBackGameObjectCoroutine(objectToPutBack, delay));
        }

        // Coroutine to wait for a delay and then put the projectile back in the pool
        private IEnumerator PutBackProjectileCoroutine(Projectile basicProjectile, float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnProjectile(basicProjectile.gameObject, basicProjectile);
        }

        private IEnumerator PutBackGameObjectCoroutine(GameObject objectToPutBack, float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnGameObject(objectToPutBack);
        }
    }
}