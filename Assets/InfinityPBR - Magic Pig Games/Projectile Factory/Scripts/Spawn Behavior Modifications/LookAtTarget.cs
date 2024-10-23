using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Look At Target",
        menuName = "Projectile Factory/Spawn Behavior Mod/Look At Target")]
    [Serializable]
    public class LookAtTarget : SpawnBehaviorModification
    {
        [Header("Options")] public Vector3 offset = Vector3.zero;

        public override string InternalDescription => "Rotates the projectile to look at the target.";

        public override void OnSpawn(GameObject projectileObject, Projectile projectile)
        {
            base.OnSpawn(projectileObject, projectile);

            projectileObject.transform.LookAt(projectile.ProjectileSpawner.TargetPosition + offset);
        }
    }
}