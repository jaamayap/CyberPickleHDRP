using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Wave Movement Behavior",
        menuName = "Projectile Factory/Moving Behavior/Wave Movement Behavior")]
    [Serializable]
    public class WaveMovementBehavior : MovementBehaviorForward
    {
        [Header("Wave Motion Options")]
        [Tooltip("Direction of the wave: 1,0,0 for horizontal, 0,1,0 for vertical, 1,1,0 for diagonal")]
        [ShowInProjectileEditor("Wave Direction")]
        public Vector3 waveDirection = Vector3.up;

        [Range(0.0f, 0.1f)]
        [ShowInProjectileEditor("Wave Amplitude")]
        [Tooltip("Amplitude of the wave -- should be a very small value")]
        public float waveAmplitude = 0.01f; // Very small amplitude for a slight motion

        [Range(0.0f, 10.0f)]
        [ShowInProjectileEditor("Wave Frequency")]
        [Tooltip("Frequency of the wave. 1.0f is one full wave per second.")]
        public float waveFrequency = 2f; // Frequency of the wave

        [Tooltip("Stop the wave motion after a collision.")] [ShowInProjectileEditor("Stop Motion After Collision")]
        public bool stopMotionAfterCollision = true;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "Moves the projectile in a wave pattern.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Move";

        protected override void Reset()
        {
            _collided = false;
            base.Reset();
        }

        protected override void Move()
        {
            if (_collided)
                return;

            // Base forward movement
            var forwardMovement = LocalDirection * (ProjectileSpeed * Time.deltaTime);

            // Calculate wave offset with a baseline adjustment
            var waveOffset =
                waveAmplitude * (Mathf.Sin(Time.time * waveFrequency) - 0.0f); // Adjust the '-0.5f' as needed

            // Combine forward movement with wave motion
            var waveMotion = waveDirection * waveOffset;
            var finalMovement = forwardMovement + waveMotion;

            // Apply the translation
            Projectile.transform.Translate(finalMovement, Space.Self);
        }

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (stopMotionAfterCollision) _collided = true;
            base.CollisionEnter(Projectile, collision, objectHit, contactPoint);
        }
    }
}