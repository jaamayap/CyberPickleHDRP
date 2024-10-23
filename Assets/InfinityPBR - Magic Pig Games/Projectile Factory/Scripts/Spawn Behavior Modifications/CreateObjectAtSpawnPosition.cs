using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Create Object At Spawn Point",
        menuName = "Projectile Factory/Spawn Behavior Mod/Create Object At Spawn Point")]
    [Serializable]
    public class CreateObjectAtSpawnPosition : SpawnBehaviorModification
    {
        [ShowInProjectileEditor("Object To Create")]
        public GameObject objectToCreate;

        [ShowInProjectileEditor("Parent To Position")]
        public bool parentToPosition = true;

        [ShowInProjectileEditor("Adjust Launch Rotation")]
        public bool adjustLaunchRotation = true;

        [ShowInProjectileEditor("Rotation Relative To Spawn Point Forward")]
        public Vector3 rotationRelativeToSpawnPointForward = Vector3.zero;

        public override string InternalDescription => "Creates an object at the spawn point of the projectile.";
        public override string InternalIcon => "CreateObject";

        public override void OnSpawn(GameObject projectileObject, Projectile projectile)
        {
            base.OnSpawn(projectileObject, projectile);

            if (projectile.useObjectPool && ProjectilePoolManager.instance != null)
            {
                var obj = ProjectilePoolManager.instance.GetProjectile(objectToCreate);
                if (obj != null)
                {
                    obj.SetActive(false);
                    obj.ProjectilePowerDestroySetProjectile(projectile);
                    obj.transform.position = projectile.ProjectileSpawner.SpawnPosition;
                    obj.SetActive(true);
                    return;
                }
            }

            var newObj = Instantiate(objectToCreate, projectile.ProjectileSpawner.SpawnPosition, Quaternion.identity);
            newObj.ProjectilePowerDestroySetProjectile(projectile);
            if (parentToPosition)
                newObj.transform.SetParent(projectile.ProjectileSpawner.SpawnTransform.transform);
            if (adjustLaunchRotation)
            {
                // Convert rotationRelativeToSpawnPointForward to a quaternion
                var additionalRotation = Quaternion.Euler(rotationRelativeToSpawnPointForward);

                // Calculate the new forward direction by applying the additional rotation to the spawn point's forward direction
                var newForward = additionalRotation * projectile.ProjectileSpawner.SpawnTransform.forward;

                // Set the forward direction of newObj
                newObj.transform.forward = newForward;
            }
        }
    }
}