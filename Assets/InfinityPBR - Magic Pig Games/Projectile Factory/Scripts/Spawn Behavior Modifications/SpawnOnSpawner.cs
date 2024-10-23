using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Spawn On Spawner",
        menuName = "Projectile Factory/Spawn Behavior Mod/Spawn On Spawner")]
    [Serializable]
    public class SpawnOnSpawner : SpawnBehaviorModification
    {
        [Header("Options")] public Vector3 targetOffset = new(0, 0, 0);

        public bool parentToSpawner;

        public override string InternalDescription =>
            "Spawns the projectile at the spawner's position with an optional offset.";

        public override void OnSpawn(GameObject projectileObject, Projectile projectile)
        {
            base.OnSpawn(projectileObject, projectile);
            projectileObject.SetActive(false);
            projectileObject.transform.position =
                projectile.ProjectileSpawner.gameObject.transform.position + targetOffset;
            projectileObject.SetActive(true);
            if (parentToSpawner)
                projectileObject.transform.SetParent(projectile.ProjectileSpawner.transform);
        }
    }
}