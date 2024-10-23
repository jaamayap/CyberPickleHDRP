using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Add Force On Collision",
        menuName = "Projectile Factory/Collision Behavior/Add Force On Collision")]
    [Serializable]
    public class AddForceOnCollision : CollisionBehavior
    {
        [Header("Force")] [ShowInProjectileEditor("Force")]
        public float force = 5f;

        [Header("Explosive Force")] [ShowInProjectileEditor("Explosive Force")]
        public bool explosiveForce;

        [Min(0.2f)] [ShowInProjectileEditor("Explosive Force Radius")]
        public float explosiveForceRadius = 5f;

        [Header("Options")] public bool handleTriggerEnter = true;

        public bool handleCollisionEnter = true;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "This behavior adds force to the object hit by the projectile.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Force";

        public override void TriggerEnter(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (!handleTriggerEnter)
                return;
            var pointOfCollision = collider == null ? contactPoint : collider.ClosestPointOnBounds(ProjectilePosition);
            ApplyForce(pointOfCollision, objectHit == null ? collider.gameObject : objectHit);
        }

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (!handleCollisionEnter)
                return;
            var pointOfCollision = collision == null ? contactPoint : collision.contacts[0].point;
            ApplyForce(pointOfCollision, objectHit == null ? collision?.gameObject : objectHit);
        }

        // Apply force to the object hit by the projectile.
        private void ApplyForce(Vector3 pointOfCollision, GameObject objectHit)
        {
            if (force == 0)
                return;

            if (objectHit == null || explosiveForce)
            {
                ApplyExplosiveForceInRadius(pointOfCollision, objectHit);
                return;
            }

            if (objectHit == null)
                return;

            var layer = objectHit.layer;
            if (((1 << layer) & CollisionMask.value) == 0) // Check if the layer is in CollisionMask
                return;

            var rb = objectHit.GetComponent<Rigidbody>();
            if (rb == null)
                return;

            rb.AddForceAtPosition(Projectile.transform.forward * force, pointOfCollision, ForceMode.Impulse);
        }

        // Apply explosive force to all objects in the radius of the point of collision.
        private void ApplyExplosiveForceInRadius(Vector3 pointOfCollision, GameObject objectHit)
        {
            var colliders = Physics.OverlapSphere(pointOfCollision, explosiveForceRadius, CollisionMask);

            foreach (var col in colliders)
            {
                if (col.attachedRigidbody == null) continue;
                col.attachedRigidbody.AddExplosionForce(force, pointOfCollision, explosiveForceRadius);
            }
        }
    }
}