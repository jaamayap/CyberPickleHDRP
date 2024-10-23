using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Circles Above Spawner",
        menuName = "Projectile Factory/Moving Behavior/Basic Circiles Above Spawner")]
    [Serializable]
    public class CirclesAboveSpawner : HomingMovementBehavior
    {
        [ShowInProjectileEditor("Height Above Spawner")] [SerializeField]
        private float heightAboveSpawner = 10f;

        [ShowInProjectileEditor("Rotate Clockwise")] [SerializeField]
        private bool rotateClockwise = true;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "This Movement behavior makes the projectile circle above the target spawner.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Move";

        protected override Vector3 CalculateLocalDirection()
        {
            // If we don't have a target spawner, fall back to parent behavior
            if (Projectile.Target == null) return base.CalculateLocalDirection();

            // Get the direction towards the target and calculate the circumferential target point
            var directionToTarget = (Projectile.Target.transform.position - Projectile.transform.position).normalized;
            directionToTarget.y = 0; // Flattening the direction vector to make it horizontal

            var targetUp = Projectile.transform.position.y < Projectile.Target.transform.position.y + heightAboveSpawner
                ? Vector3.up
                : Vector3.zero;

            var turningDirection = Vector3.Cross(directionToTarget, Vector3.up);

            // Control the circling direction: clockwise or counter-clockwise
            if (!rotateClockwise) turningDirection *= -1;

            var compoundDirection = turningDirection + targetUp;

            return compoundDirection.normalized;
        }
    }
}