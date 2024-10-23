using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Spawn Object and Parent It on Trigger or Collision",
        menuName = "Projectile Factory/Collision Behavior/Spawn Object and Parent It on Trigger or Collision")]
    [Serializable]
    public class SpawnObjectAndParentItOnTriggerOrCollision : SpawnObjectOnTriggerOrCollision
    {
        private GameObject _parent;

        public override string InternalDescription => "This behavior spawns an object at the collision point, and if " +
                                                      "its a game object, will parent the spawned object to that.";

        protected virtual void ResetParent()
        {
            _parent = null;
        }

        // In this basic form, we will spawn the hitEffect at the collision point.
        public override void TriggerEnter(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            ResetParent();
            if (objectHit != null)
                _parent = objectHit;
            else if (collider != null)
                _parent = collider.gameObject;

            var pointOfCollision = collider == null
                ? contactPoint
                : collider.ClosestPointOnBounds(Projectile.transform.position);
            SpawnObjectAtPosition(pointOfCollision);
        }

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            ResetParent();
            if (objectHit != null)
                _parent = objectHit;
            else if (collision != null)
                _parent = collision.gameObject;
            var pointOfCollision = collision == null ? contactPoint : collision.contacts[0].point;
            SpawnObjectAtPosition(pointOfCollision);
        }

        protected virtual void SpawnObjectAtPosition(Vector3 position)
        {
            if (objectToSpawn == null) return;
            _spawnedObjects += 1;

            // Create the object from the pool or Instantiate
            var obj = CreateObject(objectToSpawn, position, Quaternion.identity);

            // Face the projectile
            if (inheritProjectileRotation)
                obj.transform.rotation = Projectile.transform.rotation;

            // Set the projectile on the object if it has a ProjectilePowerDestroy component
            obj.ProjectilePowerDestroySetProjectile(Projectile);

            if (_parent != null)
                obj.transform.SetParent(_parent.transform);
        }
    }
}