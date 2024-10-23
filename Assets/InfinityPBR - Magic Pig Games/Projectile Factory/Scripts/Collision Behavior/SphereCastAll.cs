using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Sphere Cast All",
        menuName = "Projectile Factory/Collision Behavior/Sphere Cast All")]
    [Serializable]
    public class SphereCastAll : CollisionBehavior
    {
        [Header("Sphere Options")] [ShowInProjectileEditor("Radius")]
        public float radius = 1f;

        [ShowInProjectileEditor("Distance")] public float distance = 15f;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "This Collision behavior casts a sphere in front of the projectile " +
            "and triggers a collision with any objects it hits.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Raycast";

        public override void Tick()
        {
            CastOverlapSphere();
        }

        private void CastOverlapSphere()
        {
            var hits = Physics.SphereCastAll(Projectile.transform.position, radius, Projectile.transform.forward,
                distance, CollisionMask);
            foreach (var hit in hits)
                Projectile.TriggerCollisionWithObject(hit.collider.gameObject, hit.point);
        }
    }
}