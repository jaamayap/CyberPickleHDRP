using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Set Target Null",
        menuName = "Projectile Factory/Spawn Behavior Mod/Set Target Null")]
    [Serializable]
    public class SetTargetNull : SpawnBehaviorModification
    {
        public override string InternalDescription => "Sets the target of the projectile to null.";

        public override void OnSpawn(GameObject projectileObject, Projectile projectile)
        {
            base.OnSpawn(projectileObject, projectile);

            projectile.SetTarget(null);
        }
    }
}