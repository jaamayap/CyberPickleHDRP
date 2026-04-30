using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Physics Velocity",
        menuName = "Projectile Factory/Spawn Behavior Mod/Physics Velocity")]
    [Serializable]
    public class PhysicsVelocity : SpawnBehaviorModification
    {
        public bool useGravity = true;

        // The rigid body will be cached the first time it's accessed. If the projectile doesn't have one, it will be
        // added and cached.
        private Rigidbody _rigidbody;
        public override string InternalDescription => "Applies a velocity to the projectile using a Rigidbody.";
        public Projectile Projectile { get; private set; }

        public Rigidbody ProjectileRigidbody => GetRigidBody();

        public Rigidbody GetRigidBody()
        {
            if (_rigidbody != null)
                return _rigidbody;

            _rigidbody = Projectile.GetComponent<Rigidbody>();
            if (_rigidbody == null)
                _rigidbody = Projectile.gameObject.AddComponent<Rigidbody>();

            _rigidbody.useGravity = useGravity;

            return _rigidbody;
        }

        protected virtual void SetProjectile(Projectile projectile)
        {
            Projectile = projectile;
        }

        public override void OnSpawn(GameObject projectileObject, Projectile projectile)
        {
            base.OnSpawn(projectileObject, projectile);

            SetProjectile(projectile);

            // Set the velocity of the Rigidbody to launch the projectile
            ProjectileRigidbody.linearVelocity = projectileObject.transform.forward * Projectile.ProjectileData.Speed;
        }
    }
}