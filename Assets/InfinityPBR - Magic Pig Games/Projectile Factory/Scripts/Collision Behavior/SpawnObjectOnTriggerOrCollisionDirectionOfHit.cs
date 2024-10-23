using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Spawn Object on Trigger or Collision Direction of Hit",
        menuName = "Projectile Factory/Collision Behavior/Spawn Object on Trigger or Collision Direction of Hit")]
    [Serializable]
    public class SpawnObjectOnTriggerOrCollisionDirectionOfHit : SpawnObjectOnTriggerOrCollision
    {
        protected Vector3 _contactNormal;

        protected Vector3 _contactPoint;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "This behavior spawns an object at the collision point, and turns it to " +
            "face the direction of hit. NOTE: inheritProjectileRotation is ignored!";

        public override void TriggerEnter(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            base.TriggerEnter(projectile, collider, objectHit, contactPoint);
            if (contactPoint != default)
            {
                _contactPoint = contactPoint;
                _contactNormal = Vector3.zero;
                return;
            }

            _contactPoint = collider.ClosestPointOnBounds(Projectile.transform.position);
            _contactNormal = (Projectile.transform.position - _contactPoint).normalized;
        }

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            base.CollisionEnter(projectile, collision, objectHit, contactPoint);
            if (contactPoint != default)
            {
                _contactPoint = contactPoint;
                _contactNormal = Vector3.zero;
                return;
            }

            _contactPoint = collision.contacts[0].point;
            _contactNormal = collision.contacts[0].normal;
        }

        protected override void SpawnObjectAtPosition(Vector3 position)
        {
            if (objectToSpawn == null) return;
            _spawnedObjects += 1;

            // Create the object from the pool or Instantiate
            var obj = CreateObject(objectToSpawn, position, Quaternion.identity);

            var positionOfProjectile = Projectile.transform.position;

            obj.transform.rotation = Quaternion.LookRotation(Projectile.LastPosition - positionOfProjectile);

            // Set the projectile on the object if it has a ProjectilePowerDestroy component
            obj.ProjectilePowerDestroySetProjectile(Projectile);
        }
    }
}