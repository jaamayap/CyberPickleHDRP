using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Spawn Offset",
        menuName = "Projectile Factory/Spawn Behavior Mod/Spawn Offset")]
    [Serializable]
    public class SpawnOffset : SpawnBehaviorModification
    {
        [Header("Options")] [ShowInProjectileEditor("Target Offset")]
        public Vector3 targetOffset = new(0, 0, 0);

        public override string InternalDescription => "Spawns the projectile with an optional offset.";

        public override void OnSpawn(GameObject projectileObject, Projectile projectile)
        {
            Debug.Log("OnSpawn");
            base.OnSpawn(projectileObject, projectile);
            projectileObject.SetActive(false);
            var directionInWorldSpace = projectileObject.transform.TransformDirection(targetOffset);
            projectileObject.transform.position += directionInWorldSpace;
            projectileObject.SetActive(true);
            Debug.Log($"Spawned projectile at {projectileObject.transform.position} with offset {targetOffset}.");
        }
    }
}