using System;
using System.Linq;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Physics Velocity Active Overlay",
        menuName = "Projectile Factory/Trajectory Behavior/Physics Velocity Active Overlay")]
    [Serializable]
    public class PhysicsVelocityActiveOverlay : TrajectoryActiveOverlay
    {
        [Header("Physics Velocity")]
        [Tooltip("This is the PhysicsVelocity object which you should also be using on your projectile, so that the " +
                 "values match.")]
        public PhysicsVelocityModified physicsVelocityModified;

        [Tooltip("When true, the scaling between min/max values will be computed based on the inaccuracy distances, " +
                 "and modified by the inaccuracyCurve.")]
        public bool scaleWithInaccuracy = true;

        [Tooltip("When true, the sprite will hide when the projectile has passed the target.")]
        public bool hideWhenPastTarget = true;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "For use specifically with the PhysicsVelocityModified Spawn Behavior " +
            "Modification. This will show an overlay on the TARGET.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Target";

        public override float FarDistance =>
            scaleWithInaccuracy ? physicsVelocityModified.maxInaccuracyDistance : farDistance;

        public override float CloseDistance =>
            scaleWithInaccuracy ? physicsVelocityModified.minInaccuracyDistance : closeDistance;

        public override float T
        {
            get
            {
                if (Math.Abs(_lastTimeSinceStart - Time.time) < 0.001f)
                    return _t;

                _lastTimeSinceStart = Time.time;

                if (scaleWithInaccuracy)
                {
                    _t = Mathf.InverseLerp(CloseDistance, FarDistance, DistanceToTarget());
                    _t = physicsVelocityModified.inaccuracyByDistanceCurve
                        .Evaluate(_t); // Flip it so we get the right min/max
                    _t = 1 - _t;
                }
                else
                {
                    _t = Mathf.InverseLerp(FarDistance, CloseDistance, DistanceToTarget());
                }

                return _t;
            }
        }

        protected virtual float DistanceToSpawner()
        {
            return ProjectileSpawner != null
                ? Vector3.Distance(Projectile.transform.position, ProjectileSpawner.transform.position)
                : -1f;
        }

        protected virtual float SpawnerDistanceToTarget()
        {
            return ProjectileSpawner != null
                ? Vector3.Distance(ProjectileSpawner.Target.transform.position, ProjectileSpawner.transform.position)
                : -1f;
        }

        public override void Tick()
        {
            if (physicsVelocityModified == null)
                SetPhysicsVelocityModified();
            if (physicsVelocityModified == null)
            {
                Debug.LogError($"Projectile {Projectile.name} has the PhysicsVelocityActiveOverlay Trajectory " +
                               "Behavior set, but it does not have the Spawn Behavior Modification required. Please " +
                               "ensure there is a PhysicsVelocityModified Spawn Behavior Modification on the " +
                               "projectile.");
                return;
            }

            base.Tick();

            // Check to see if we should NOT hide the sprite
            if (DistanceToSpawner() < SpawnerDistanceToTarget())
                return;
            if (!hideWhenPastTarget)
                return;

            // Hide the sprite by setting opacity to 0
            _image.color = new Color(SpriteColor.r, SpriteColor.g, SpriteColor.b, 0);
        }

        private void SetPhysicsVelocityModified()
        {
            physicsVelocityModified = Projectile.spawnBehaviorModifications
                    .FirstOrDefault(x => x is PhysicsVelocityModified)
                as PhysicsVelocityModified;
        }

        public override float DistanceToTarget()
        {
            return Projectile != null
                ? Vector3.Distance(physicsVelocityModified.thisTargetPosition, Projectile.transform.position)
                : base.DistanceToTarget();
        }
    }
}