using System;
using UnityEngine;

/*
 * This Spawn Behavior Modification will spawn the projectile on the target position. It turns off the projectile
 * before movement to ensure that the projectile is not visible until it reaches the target.
 */

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Spawn On Target",
        menuName = "Projectile Factory/Spawn Behavior Mod/Spawn On Target")]
    [Serializable]
    public class SpawnOnTarget : SpawnBehaviorModification
    {
        [Header("Options")] [Tooltip("Offset from the target position.")] [ShowInProjectileEditor("Target Offset")]
        public Vector3 targetOffset = new(0, 0, 0);

        [Tooltip("Parent the projectile to the target.")] [ShowInProjectileEditor("Parent To Target")]
        public bool parentToTarget;

        public override string InternalDescription =>
            "Spawns the projectile at the target's position with an optional offset.";

        public override void OnSpawn(GameObject projectileObject, Projectile projectile)
        {
            base.OnSpawn(projectileObject, projectile);
            
            // Do not run this if the target is null
            if (projectile.ProjectileSpawner.Target == null)
            {
                projectileObject.SetActive(false);
                return;
            }
            
            projectileObject.SetActive(false);
            projectileObject.transform.position = projectile.ProjectileSpawner.TargetPosition + targetOffset;
            projectileObject.SetActive(true);

            if (parentToTarget)
                projectileObject.transform.SetParent(projectile.ProjectileSpawner.Target.transform);
        }
    }
}