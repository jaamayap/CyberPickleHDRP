using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Collision Projectile Forward",
        menuName = "Projectile Factory/Collision Behavior/Collision Projectile Forward")]
    [Serializable]
    public class CollisionProjectileForward : CollisionBehavior
    {
        [Header("Debug Options")] public bool drawDebugLines; // New debug option

        [Header("Options")] [ShowInProjectileEditor("Distance")]
        public float distance = 10f;

        [ShowInProjectileEditor("Angle")] public float angle = 10f;

        [ShowInProjectileEditor("Active When Launched Only")]
        public bool activeWhenLaunchedOnly = true;

        private Collider[] _colliders = new Collider[10];

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "This Collision behavior checks for collisions in a forward cone.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Raycast";


        public override void Tick()
        {
            ComputeCollisionInForwardCone();
        }

        protected virtual void ComputeCollisionInForwardCone()
        {
            if (activeWhenLaunchedOnly && !Projectile.Launched)
                return;

            var colliderCount = PerformOverlapSphereNonAlloc();

            while (colliderCount >= _colliders.Length)
            {
                _colliders = new Collider[_colliders.Length * 2];
                colliderCount = PerformOverlapSphereNonAlloc();
            }

            for (var i = 0; i < colliderCount; i++)
            {
                var hit = _colliders[i];

                // Only trigger collision if object is within the forward cone.
                if (hit.gameObject != Projectile.gameObject && IsWithinForwardCone(hit.transform))
                    Projectile.TriggerCollisionWithObject(hit.gameObject);
            }

            DrawDebugCone();
        }

        private void DrawDebugCone()
        {
            if (!drawDebugLines) return;

            var forward = Projectile.transform.forward;
            var right = Projectile.transform.right;
            var up = Projectile.transform.up;

            var halfAngle = angle / 2f;

            var forwardConeLeft = Quaternion.AngleAxis(-halfAngle, up) * forward;
            var forwardConeRight = Quaternion.AngleAxis(halfAngle, up) * forward;

            Debug.DrawRay(Projectile.transform.position, forwardConeLeft * distance, Color.green);
            Debug.DrawRay(Projectile.transform.position, forwardConeRight * distance, Color.green);
        }

        protected virtual int PerformOverlapSphereNonAlloc()
        {
            return Physics.OverlapSphereNonAlloc(Projectile.transform.position, distance, _colliders,
                Projectile.CollisionMask);
        }

        protected virtual bool IsWithinForwardCone(Transform other)
        {
            // Compare angle between projectile forward direction and line to other object.
            var directionToOther = other.position - Projectile.transform.position;
            var angleToOther = Vector3.Angle(Projectile.transform.forward, directionToOther);

            // Check if angle falls within cone.
            return angleToOther <= angle / 2f;
        }
    }
}