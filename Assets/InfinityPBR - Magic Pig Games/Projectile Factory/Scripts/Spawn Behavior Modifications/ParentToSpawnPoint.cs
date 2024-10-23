using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Parent To Spawn Point",
        menuName = "Projectile Factory/Spawn Behavior Mod/Parent To Spawn Point")]
    [Serializable]
    public class ParentToSpawnPoint : SpawnBehaviorModification
    {
        public override string InternalDescription => "Parents the projectile to the spawn point.";

        public override void OnSpawn(GameObject projectileObject, Projectile projectile)
        {
            base.OnSpawn(projectileObject, projectile);
            projectileObject.transform.SetParent(projectile.ProjectileSpawner.SpawnTransform);
        }
    }
}