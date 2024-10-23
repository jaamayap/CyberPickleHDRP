using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Collision at Line Renderer End",
        menuName = "Projectile Factory/Collision Behavior/Collision at Line Renderer End")]
    [Serializable]
    public class CollisionAtLineRendererEnd : CollisionBehavior
    {
        [Header("Options")] [ShowInProjectileEditor("Radius")]
        public float radius = 0.25f;

        private Collider[] _colliders = new Collider[50];

        protected LineRenderer _lineRenderer;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "Causes the projectile to check for collisions at the end of a Line Renderer.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Line End";

        public override void LaunchProjectile(Projectile projectile)
        {
            _lineRenderer = Projectile.GetComponent<LineRenderer>();

            if (_lineRenderer == null)
                Debug.LogError($"No Line Renderer was found ong {Projectile.gameObject.name}, it is " +
                               $"required for the {nameof(CollisionAtLineRendererEnd)} behavior.");
        }

        public override void Tick()
        {
            ComputeCollisionAtLineRendererEnd();
        }

        protected virtual void ComputeCollisionAtLineRendererEnd()
        {
            if (_lineRenderer == null)
                return;

            var positionOfLastPointOnLine = WorldPositionOfLastPointOnLine(Projectile);
            var colliderCount = PerformOverlapSphereNonAlloc(positionOfLastPointOnLine);

            while (colliderCount > _colliders.Length)
            {
                _colliders = new Collider[_colliders.Length * 2];
                colliderCount = PerformOverlapSphereNonAlloc(positionOfLastPointOnLine);
            }

            for (var i = 0; i < colliderCount; i++)
            {
                var hit = _colliders[i];
                if (hit.gameObject != Projectile.gameObject)
                    Projectile.TriggerCollisionWithObject(hit.gameObject);
            }
        }

        protected virtual int PerformOverlapSphereNonAlloc(Vector3 position)
        {
            return Physics.OverlapSphereNonAlloc(position, radius, _colliders, Projectile.CollisionMask);
        }

        protected virtual Vector3 WorldPositionOfLastPointOnLine(Projectile basicProjectile)
        {
            return basicProjectile.transform.TransformPoint(_lineRenderer.GetPosition(_lineRenderer.positionCount - 1));
        }
    }
}