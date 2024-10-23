using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Spawn Object On Destroy",
        menuName = "Projectile Factory/Destroy Behavior/Spawn Object On Destroy")]
    [Serializable]
    public class SpawnObjectOnDestroy : DestroyBehavior
    {
        [Header("Options")] [ShowInProjectileEditor("Object To Spawn")]
        public GameObject objectToSpawn;

        [ShowInProjectileEditor("Offset")] public Vector3 offset = Vector3.zero;

        [ShowInProjectileEditor("Look At Spawner")]
        public bool lookAtSpawner;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "Spawns an object at the projectile's position when projectile is " +
            "destroyed.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "CreateObject";

        public override void DoDestroy(Projectile projectile)
        {
            SpawnTheObject();
            base.DoDestroy(Projectile);
        }

        protected virtual void SpawnTheObject()
        {
            if (objectToSpawn == null) return;

            if (Projectile.useObjectPool && ProjectilePoolManager.instance != null)
                RetrieveObjectFromPool();
            else
                CreateNewObject();
        }

        private void RetrieveObjectFromPool()
        {
            var pooledObject = ProjectilePoolManager.instance.GetProjectile(objectToSpawn);
            if (pooledObject != null)
                PrepareObject(pooledObject);
            else
                CreateNewObject();
        }

        private void CreateNewObject()
        {
            PrepareObject(Instantiate(objectToSpawn, Projectile.transform.position + offset, Quaternion.identity));
        }

        private void PrepareObject(GameObject projectileObject)
        {
            projectileObject.SetActive(false);
            if (lookAtSpawner)
                projectileObject.transform.LookAt(Projectile.transform.position);

            projectileObject.transform.position = Projectile.transform.position + offset;
            projectileObject.ProjectilePowerDestroySetProjectile(Projectile);
            projectileObject.SetActive(true);
        }
    }
}