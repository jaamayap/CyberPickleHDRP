using System;
using System.Collections;
using UnityEngine;

/*
 * This will destroy an object now, or in time, specifically using the object pool only if the projectile is
 * using the object pool, otherwise a traditional destroy. It has the option to disable specific objects more quickly,
 * and reenable them when the projectile is reenabled (assumed from an object pool).
 */

namespace MagicPigGames.ProjectileFactory
{
    [Documentation("This will destroy an object now, or in time, specifically using the object pool only if " +
                   "the projectile is using the object pool, otherwise a traditional destroy. It has the option to " +
                   "disable specific objects more quickly, and reenable them when the projectile is re-enabled " +
                   "(assumed from an object pool).",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/additional-scripts/destroy-or-pool-object")]
    [Serializable]
    public class DestroyOrPoolObject : MonoBehaviour
    {
        [Tooltip("The amount of time before the projectile is destroyed")]
        public float timeToDestroy = 5f;

        [Tooltip("If true, the projectile will be destroyed when the projectile is set")]
        public bool triggerWhenProjectileSet = true;

        [Header("Options")] public GameObject[] objectsToDisableImmediately;

        public float delayToDisableObjects = 0.05f; // Should be > 0 if you want to actually SEE the objects

        [Tooltip("If true, the objects will be re-enabled when the projectile is re-enabled")]
        public bool reenableThemOnEnable = true;

        public bool UseObjectPool => Projectile.useObjectPool;
        public Projectile Projectile { get; private set; }

        protected virtual void OnEnable()
        {
            EnableObjects();
        }

        public void SetProjectile(Projectile basicProjectile)
        {
            Projectile = basicProjectile;
            if (triggerWhenProjectileSet)
                RemoveGameObject(timeToDestroy);
        }

        protected virtual void PutBackInPool(float delay = 0)
        {
            ProjectilePoolManager.instance.PutBackGameObject(gameObject, delay);
        }

        public virtual void RemoveGameObject(float delay = 0)
        {
            DisableObjects();
            if (Projectile != null && UseObjectPool)
                PutBackInPool(delay);
            else
                Destroy(gameObject, delay);
        }

        protected virtual void EnableObjects()
        {
            if (!reenableThemOnEnable)
                return;

            foreach (var obj in objectsToDisableImmediately)
                obj.SetActive(true);
        }

        protected virtual void DisableObjects()
        {
            if (gameObject.activeSelf == false)
                gameObject.SetActive(true);
            StartCoroutine(DisableObjectsInTime());
        }

        protected virtual IEnumerator DisableObjectsInTime()
        {
            yield return new WaitForSeconds(delayToDisableObjects);
            foreach (var obj in objectsToDisableImmediately)
                obj.SetActive(false);
        }
    }
}