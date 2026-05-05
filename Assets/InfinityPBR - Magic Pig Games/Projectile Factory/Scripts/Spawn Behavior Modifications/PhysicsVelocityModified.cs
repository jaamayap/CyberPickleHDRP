using System;
using UnityEngine;
using static MagicPigGames.ProjectileFactory.ProjectileUtilities;

/*
 * This is the Spawn Behavior one should use for firing arrows or other things that use physics. It has a few options
 * to handle multiple scenarios, such as flattening the launch angle, adding inaccuracy, and more.
 */

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Physics Velocity Modified",
        menuName = "Projectile Factory/Spawn Behavior Mod/Physics Velocity Modified")]
    [Serializable]
    public class PhysicsVelocityModified : PhysicsVelocity
    {
        [Header("Velocity Options")]
        [Tooltip("When true, the velocity will be applied to the projectile.")]
        [ShowInProjectileEditor("Apply Velocity")]
        public bool applyVelocity = true;

        [Header("Angled Spawn Options")]
        [Tooltip("When true, the launch angle will be flattened to be horizontal.")]
        [ShowInProjectileEditor("Flatten Launch Angle")]
        public bool flattenLaunchAngle;

        [Tooltip("When no modifications are, or can be made, the default angle will be used.")]
        [ShowInProjectileEditor("Default Angle")]
        public float defaultAngle;

        [Tooltip("Generally two angles are available, with the higher angle being a very high arc.")]
        [ShowInProjectileEditor("Use Lower Angle")]
        public bool useLowerAngle = true;

        [Header("Target Options")]
        [Tooltip("The offset from the target position that the projectile will aim for.")]
        [ShowInProjectileEditor("Target Offset")]
        public Vector3 targetOffset = new(0, 0, 0);

        [Header("Initial Angle Requirements")]
        [Tooltip("The vertical angle calculation will only occur if the target direction angle is within this value.")]
        [ShowInProjectileEditor("Max Initial Angle Delta")]
        public float maxInitialAngleDelta = 10f;

        [Tooltip("This is the mode that the initial angle delta will be calculated in. LeftRight will only consider " +
                 "the horizontal angle, UpDown will only consider the vertical angle, and AllDirections will " +
                 "consider both.")]
        [ShowInProjectileEditor("Initial Angle Delta Mode")]
        public AngleModMode initialAngleDeltaMode = AngleModMode.LeftRight;

        [Header("Horizontal Angle Modification Options")]
        [Tooltip("When true, the horizontal angle of the projectile will be shifted to face the target.")]
        [ShowInProjectileEditor("Shift Horizontal Angle")]
        public bool shiftHorizontalAngle = true;

        [Tooltip("This is the maximum amount the angle will be shifted on the horizontal.")]
        [ShowInProjectileEditor("Max Horizontal Shift")]
        public float maxHorizontalShift = 3f;

        [Header("Inaccuracy Options")]
        [Tooltip("The minimum directional range of inaccuracy")]
        [ShowInProjectileEditor("Inaccuracy Direction Min")]
        public Vector3 inaccuracyDirectionMin = new(-1f, -1f, -1f);

        [Tooltip("The maximum directional range of inaccuracy")] [ShowInProjectileEditor("Inaccuracy Direction Max")]
        public Vector3 inaccuracyDirectionMax = new(1f, 1f, 1f);

        [Tooltip("The inaccuracy of the projectile based on the distance to the target")]
        [ShowInProjectileEditor("Inaccuracy By Distance Curve")]
        public AnimationCurve inaccuracyByDistanceCurve = AnimationCurve.Linear(0, 1, 1, 1);

        [Tooltip("The minimum distance that inaccuracy will be applied, based on the left size of the curve.")]
        [ShowInProjectileEditor("Min Inaccuracy Distance")]
        public float minInaccuracyDistance;

        [Tooltip("Any distance at or beyond this will get the full inaccuracy value.")]
        [ShowInProjectileEditor("Max Inaccuracy Distance")]
        public float maxInaccuracyDistance = 10f;


        [Header("Debug Options")]
        [Tooltip("When true, a debug line will be drawn from the spawn position to the target position.")]
        [ShowInProjectileEditor("Draw Debug Line To Target")]
        public bool drawDebugLineToTarget;

        [HideInInspector] public Vector3 thisTargetPosition;

        public override string InternalDescription =>
            "Applies a velocity to the projectile using a Rigidbody. This version has additional options for modifying the launch angle, adding inaccuracy, and more.";

        public float DistanceToTarget => Vector3.Distance(Projectile.ProjectileSpawner.SpawnPosition,
            Projectile.ProjectileSpawner.Target.transform.position);

        public Vector3 RandomPositionMod => RandomVector3(MinimumInaccuracyDirection(), MaximumInaccuracyDirection());

        public Vector3 MinimumInaccuracyDirection()
        {
            var distance = DistanceToTarget;
            if (distance < minInaccuracyDistance)
                return inaccuracyByDistanceCurve.Evaluate(0) * inaccuracyDirectionMin;
            if (distance > maxInaccuracyDistance)
                return inaccuracyByDistanceCurve.Evaluate(1) * inaccuracyDirectionMin;
            return inaccuracyByDistanceCurve.Evaluate(distance / maxInaccuracyDistance) * inaccuracyDirectionMin;
        }

        public Vector3 MaximumInaccuracyDirection()
        {
            var distance = DistanceToTarget;
            if (distance < minInaccuracyDistance)
                return inaccuracyByDistanceCurve.Evaluate(0) * inaccuracyDirectionMax;
            if (distance > maxInaccuracyDistance)
                return inaccuracyByDistanceCurve.Evaluate(1) * inaccuracyDirectionMax;
            return inaccuracyByDistanceCurve.Evaluate(distance / maxInaccuracyDistance) * inaccuracyDirectionMax;
        }

        /*
        private Projectile _projectile;
        public Projectile Projectile => _projectile;

        // The rigid body will be cached the first time it's accessed. If the projectile doesn't have one, it will be
        // added and cached.
        private Rigidbody _rigidbody;
        public Rigidbody ProjectileRigidbody => GetRigidBody();
        public Rigidbody GetRigidBody()
        {
            if (_rigidbody != null)
                return _rigidbody;

            _rigidbody = Projectile.GetComponent<Rigidbody>();
            if (_rigidbody == null)
                _rigidbody = Projectile.gameObject.AddComponent<Rigidbody>();

            return _rigidbody;
        }*/

        protected virtual Vector3 GetInitialDirection(Quaternion projectileRotation)
        {
            var forwardDirection = projectileRotation * Vector3.forward; // Get the forward direction of the projectile
            var flatForwardDirection =
                new Vector3(forwardDirection.x, 0, forwardDirection.z).normalized; // Flatten the direction
            return flattenLaunchAngle ? flatForwardDirection : forwardDirection;
        }

        protected virtual bool AngleExceedsMax(Vector3 targetPosition, Vector3 spawnPosition, float initialAngleDelta,
            Vector3 initialDirection)
        {
            switch (initialAngleDeltaMode)
            {
                case AngleModMode.UpDown:
                    return Mathf.Abs(targetPosition.y - spawnPosition.y) > maxInitialAngleDelta;
                case AngleModMode.LeftRight:
                    return initialAngleDelta > maxInitialAngleDelta;
                case AngleModMode.AllDirections:
                    return Vector3.Angle(initialDirection, targetPosition - spawnPosition) > maxInitialAngleDelta;
            }

            return false;
        }

        protected virtual void SetAltTargetPosition(Projectile basicProjectile, Vector3 altPosition)
        {
            basicProjectile.altTargetPosition = altPosition;
            basicProjectile.useAltTargetPosition = true;
        }

        protected virtual void SetHideTarget(Projectile basicProjectile, bool value)
        {
            basicProjectile.hideTarget = value;
        }


        public override void OnSpawn(GameObject projectileObject, Projectile projectile)
        {
            base.OnSpawn(projectileObject, projectile);

            SetProjectile(projectile);

            var spawnPosition = Projectile.ProjectileSpawner.SpawnPosition;
            var targetPosition = Projectile.ProjectileSpawner.Target.transform.position + targetOffset;
            var projectileRotation = ProjectileRigidbody.rotation =
                Quaternion.Euler(Projectile.ProjectileSpawner.SpawnRotation);

            var initialDirection = GetInitialDirection(projectileRotation);

            var initialAngleDelta = Mathf.Abs(Vector3.SignedAngle(
                Vector3.ProjectOnPlane(initialDirection, Vector3.up),
                Vector3.ProjectOnPlane(targetPosition - spawnPosition, Vector3.up), Vector3.up));

            if (AngleExceedsMax(targetPosition, spawnPosition, initialAngleDelta, initialDirection))
            {
                // If the initial angle exceeds the maximum allowed angle delta, fire the projectile forward with the default angle
                var launchDirectionEarly =
                    Quaternion.AngleAxis(defaultAngle, Vector3.Cross(Vector3.up, initialDirection)) * initialDirection;
                ProjectileRigidbody.linearVelocity = launchDirectionEarly * Projectile.ProjectileData.Speed;

                // Set the alt target position to a position that is in front of the trajectory
                var altTargetPosition = spawnPosition + launchDirectionEarly * 4f;
                SetHideTarget(projectile, true);
                return;
            }

            SetHideTarget(projectile, false);

            // We are within the angle requirements, so we can calculate the launch angle
            targetPosition = GetModifiedPosition(spawnPosition, targetPosition, RandomPositionMod);
            SetAltTargetPosition(projectile, targetPosition);
            if (drawDebugLineToTarget)
                Debug.DrawLine(spawnPosition, targetPosition, Color.red, 5f);

            var launchAngle = CalculateLaunchAngle(spawnPosition, targetPosition, Projectile.ProjectileData.Speed,
                defaultAngle, useLowerAngle);

            // Check if we need to shift the horizontal angle
            var launchDirection = LaunchDirection(initialDirection, targetPosition, spawnPosition, launchAngle);

            if (!applyVelocity)
                return;

            // Set the velocity of the Rigidbody to launch the projectile
            ProjectileRigidbody.linearVelocity = launchDirection * Projectile.ProjectileData.Speed;
        }

        protected virtual Vector3 LaunchDirection(Vector3 initialDirection, Vector3 targetPosition,
            Vector3 spawnPosition, float launchAngle)
        {
            if (!shiftHorizontalAngle)
                // Adjust the launch direction based on the launch angle without shifting
                return Quaternion.AngleAxis(launchAngle, Vector3.Cross(Vector3.up, initialDirection)) *
                       initialDirection;

            var horizontalAngleDelta = Mathf.Abs(Vector3.SignedAngle(
                Vector3.ProjectOnPlane(initialDirection, Vector3.up),
                Vector3.ProjectOnPlane(targetPosition - spawnPosition, Vector3.up), Vector3.up));

            if (!(horizontalAngleDelta <= maxHorizontalShift))
                return Quaternion.AngleAxis(launchAngle, Vector3.Cross(Vector3.up, initialDirection)) *
                       initialDirection;

            // Calculate the horizontal shift angle
            var shiftAngle = horizontalAngleDelta *
                             Mathf.Sign(Vector3.Cross(initialDirection, targetPosition - spawnPosition).y);
            // Adjust the launch direction based on the shift angle
            return Quaternion.AngleAxis(shiftAngle, Vector3.up) *
                   Quaternion.AngleAxis(launchAngle, Vector3.Cross(Vector3.up, initialDirection)) *
                   initialDirection;
        }

        //private void SetProjectile(Projectile projectile) => _projectile = projectile;
    }
}