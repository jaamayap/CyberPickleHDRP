using System;
using UnityEngine;

/*
 * NOTE: This is a DEMO class, which works VERY SPECIFICALLY with the demo damage system. Your damage system will
 * be different. While you can make your own (modified from this), you may need to heavily modify it depending on
 * how your damage system operates.
 */

namespace MagicPigGames.ProjectileFactory.Demo
{
    [CreateAssetMenu(fileName = "New Area Damage on Trigger or Collision",
        menuName = "Projectile Factory/Collision Behavior/Area Damage on Trigger or Collision")]
    [Serializable]
    public class AreaDamageOnTriggerOrCollision : CollisionBehavior
    {
        [ShowInProjectileEditor("Radius")] public float radius = 6;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "[DEMO: This works specifically with the ProjectileDemoActor class provided for the demo scenes!]\n\n" +
            "This behavior causes damage to all actors within a radius of the collision point.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Area Damage";

        // In this basic form, we will spawn the hitEffect at the collision point.
        public override void TriggerEnter(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            var pointOfCollision = collider == null
                ? contactPoint
                : collider.ClosestPointOnBounds(Projectile.transform.position);
            CauseDamageAtPosition(Projectile, pointOfCollision);
        }

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            var pointOfCollision = collision == null ? contactPoint : collision.contacts[0].point;
            CauseDamageAtPosition(Projectile, pointOfCollision);
        }

        protected virtual void CauseDamageAtPosition(Projectile projectile, Vector3 position)
        {
            var layerMask = Projectile.CollisionMask;
            var owner = Projectile.ProjectileSpawner.GetComponent<ProjectileDemoActor>();
            var colliders = Physics.OverlapSphere(position, radius, layerMask);
            foreach (var collider in colliders)
            {
                var actor = collider.GetComponent<ProjectileDemoActor>();
                if (actor == null)
                    continue;

                owner.RegisterHit(Projectile, actor);
            }
        }
    }
}