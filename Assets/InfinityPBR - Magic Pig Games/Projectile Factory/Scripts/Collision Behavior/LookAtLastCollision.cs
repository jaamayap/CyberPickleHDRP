using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Look At Last Collision",
        menuName = "Projectile Factory/Collision Behavior/Look At Last Collision")]
    [Serializable]
    public class LookAtLastCollision : CollisionBehavior
    {
        private Vector3 _lastCollisionPoint;

        public override string InternalDescription =>
            "This will cause the projectile to look at the last collision point. This is useful for beams that you want " +
            "to attach to the Spawn Point, but ensure the end point doesn't move even if the Spawn Point does.";

        // On collision enter, set the last collision point to the point of collision
        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            var pointOfCollision = collision == null ? contactPoint : collision.contacts[0].point;
            _lastCollisionPoint = pointOfCollision;
        }

        // Look at the last collision point
        public override void Tick()
        {
            if (_lastCollisionPoint == Vector3.zero) return;
            Projectile.transform.LookAt(_lastCollisionPoint);
        }
    }
}