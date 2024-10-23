using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Spawn Object on Trigger or Collision",
        menuName = "Projectile Factory/Collision Behavior/Spawn Object on Trigger or Collision")]
    [Serializable]
    public class SpawnObjectOnTriggerOrCollision : CollisionBehavior
    {
        [Header("Spawn Options")]
        // Most often the objectToSpawn will be an explosion or something like that. However, it may be a different 
        // projectile entirely, so we use the base class ProjectileBase to allow for that. All explosions should
        // inherit from ProjectileBase.
        [ShowInProjectileEditor("Object to Spawn")]
        public GameObject objectToSpawn;

        [ShowInProjectileEditor("Inherit Projectile Rotation")]
        public bool inheritProjectileRotation = true;


        protected int _spawnedObjects;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "This behavior spawns an object at the collision point.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "CreateObject";

        // In this basic form, we will spawn the hitEffect at the collision point.
        public override void TriggerEnter(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            var pointOfCollision = collider == null
                ? contactPoint
                : collider.ClosestPointOnBounds(Projectile.transform.position);
            SpawnObjectAtPosition(pointOfCollision);
        }

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
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
        }

        public override void ResetValues()
        {
            _spawnedObjects = 0;
        }
    }
}