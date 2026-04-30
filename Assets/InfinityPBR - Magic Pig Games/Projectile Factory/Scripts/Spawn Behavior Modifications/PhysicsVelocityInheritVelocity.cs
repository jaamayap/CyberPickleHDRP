using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Physics Velocity Inherit Velocity",
        menuName = "Projectile Factory/Spawn Behavior Mod/Physics Velocity Inherit Velocity")]
    [Serializable]
    public class PhysicsVelocityInheritVelocity : PhysicsVelocity
    {
        public override string InternalDescription =>
            "Applies a velocity to the projectile using a Rigidbody, inheriting the velocity of the spawner.";

        public override void OnSpawn(GameObject projectileObject, Projectile projectile)
        {
            base.OnSpawn(projectileObject, projectile);

            // Set the velocity of the Rigidbody to launch the projectile
            var spawnerVelocity = Projectile.ProjectileSpawner.Rigidbody == null
                ? Vector3.zero
                : Projectile.ProjectileSpawner.Rigidbody.linearVelocity;
            ProjectileRigidbody.linearVelocity =
                projectileObject.transform.forward * Projectile.ProjectileData.Speed + spawnerVelocity;
        }
    }
}