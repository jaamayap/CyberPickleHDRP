using System;
using MagicPigGames.ProjectileFactory;
using UnityEngine;

// This script will cause a collision at the raycast hit point. Use it in conjunction with the 
// RacyastShooter.cs component.

namespace MagicPigGames
{
    [Documentation(
        "This script will cause a collision at the raycast hit point. Use it in conjunction with the RaycastShooter.cs component.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/additional-scripts/raycast-shooter-+-handlers")]
    [Serializable]
    public class CollisionAtRaycastHitPoint : MonoBehaviour, IHandleRaycasts
    {
        [Header("Plumbing")] [Tooltip("The projectile that will be used to trigger the collision.")]
        public Projectile projectile;

        [Header("Options")] [Tooltip("This is the radius of the sphere that will be used to detect collisions.")]
        public float radius = 0.25f;

        private Collider[] _colliders = new Collider[50];

        public void HandleRaycastHit(RaycastHit raycastHit, float maxDistance)
        {
            ComputeCollisionAtLineRendererEnd(raycastHit.point);
        }

        // This method will perform an overlap sphere at the hit point of the raycast, and trigger a collision with any
        // object that is hit.
        protected virtual void ComputeCollisionAtLineRendererEnd(Vector3 hitPoint)
        {
            var colliderCount = PerformOverlapSphereNonAlloc(hitPoint);

            while (colliderCount > _colliders.Length)
            {
                _colliders = new Collider[_colliders.Length * 2];
                colliderCount = PerformOverlapSphereNonAlloc(hitPoint);
            }

            for (var i = 0; i < colliderCount; i++)
            {
                var hit = _colliders[i];
                if (hit.gameObject != projectile.gameObject)
                    projectile.TriggerCollisionWithObject(hit.gameObject);
            }
        }

        protected virtual int PerformOverlapSphereNonAlloc(Vector3 position)
        {
            return Physics.OverlapSphereNonAlloc(position, radius, _colliders, projectile.CollisionMask);
        }
    }
}