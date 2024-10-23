using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Homing Forward Movement Behavior",
        menuName = "Projectile Factory/Moving Behavior/Homing Forward Movement Behavior")]
    [Serializable]
    public class HomingMovementBehavior : MovementBehaviorForward
    {
        [ShowInProjectileEditor("Rotation Speed")]
        public float rotationSpeed = 200f;

        [ShowInProjectileEditor("Max Rotation Angle")]
        public float maxRotationAngle = 30f;

        [Header("Options")]
        [Tooltip(
            "On Launch, the projectile will follow the target after this delay. 0 means it will follow the target immediately.")]
        [ShowInProjectileEditor("Target Follow Delay")]
        public float targetFollowDelay;

        [Tooltip("Offset to the target position. Useful to aim at a specific point in the target.")]
        [ShowInProjectileEditor("Target Offset")]
        public Vector3 targetOffset = Vector3.zero;

        [Header("Debug Options")]
        [Tooltip("Draw a debug line to show the direction the projectile is aiming.")]
        [ShowInProjectileEditor("Draw Debug Line")]
        public bool drawDebugLine;

        [Tooltip("The color of the debug line.")] [ShowInProjectileEditor("Line Color")]
        public Color lineColor = Color.cyan;

        private float _targetFollowTimer;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "This Movement behavior makes the projectile follow the target after an optional delay.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Move";
        public bool IsFollowingTarget => _targetFollowTimer <= 0;

        protected virtual void OnEnable()
        {
            _targetFollowTimer = targetFollowDelay;
        }

        protected virtual Vector3 CalculateLocalDirection(Projectile projectile)
        {
            if (projectile.Target == null) return LocalDirection;

            var directionToTarget =
                (projectile.Target.transform.position + targetOffset - projectile.transform.position).normalized;
            return Vector3.RotateTowards(LocalDirection, directionToTarget,
                maxRotationAngle * rotationSpeed * Time.deltaTime, 0f);
        }

        public override void Tick()
        {
            if (_targetFollowTimer > 0)
                _targetFollowTimer -= Time.deltaTime;

            // Rotate towards the target, otherwise we just move forward
            if (Projectile.Target != null && IsFollowingTarget)
            {
                var directionToTarget = CalculateLocalDirection(Projectile);

                // Use sqrMagnitude to check for a very small vector which is effectively zero
                if (directionToTarget.sqrMagnitude <
                    Mathf.Epsilon) // Mathf.Epsilon is a very small number close to zero
                {
                    base.Tick();
                    return;
                }

                var targetRotation = Quaternion.LookRotation(directionToTarget);
                Projectile.transform.rotation = Quaternion.RotateTowards(
                    Projectile.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }


            if (drawDebugLine)
                Debug.DrawLine(Projectile.transform.position,
                    Projectile.transform.position + Projectile.transform.forward * 10f, lineColor);

            base.Tick();
        }
    }
}