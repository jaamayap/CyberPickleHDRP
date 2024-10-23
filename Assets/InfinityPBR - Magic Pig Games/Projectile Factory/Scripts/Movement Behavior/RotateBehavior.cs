using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Rotate",
        menuName = "Projectile Factory/Moving Behavior/Rotate")]
    [Serializable]
    public class RotateBehavior : MovementBehavior
    {
        [Header("Rotation Options")]
        [Tooltip("The axis around which the projectile will rotate.")]
        [ShowInProjectileEditor("Rotation Axis")]
        public Vector3 rotationAxis = Vector3.up;

        [Tooltip("The speed at which the projectile will rotate, in angles per second")]
        [ShowInProjectileEditor("Rotation Speed")]
        public float rotationSpeedMin = 90f;

        [Tooltip("The speed at which the projectile will rotate, in angles per second.")]
        [ShowInProjectileEditor("Rotation Speed")]
        public float rotationSpeedMax = 90f;

        [Tooltip(
            "When true, each axis will rotate at a different speed. Otherwise, the speed will be the same for all axes.")]
        [ShowInProjectileEditor("Separate Axis Speeds")]
        public bool separateAxisSpeeds;

        [Tooltip("The interval at which the speed of the projectile will change.")]
        [ShowInProjectileEditor("Speed Change Interval")]
        [Min(0)]
        public float speedChangeIntervalMin;

        [Tooltip("The interval at which the speed of the projectile will change.")]
        [ShowInProjectileEditor("Speed Change Interval")]
        [Min(0)]
        public float speedChangeIntervalMax;

        private float _nextSpeedChange;
        private Coroutine _speedChangeCoroutine;

        private float _speedChangeTimer;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "Rotates the projectile around a specified axis.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Move";

        public float RandomSpeed => Random.Range(rotationSpeedMin, rotationSpeedMax);
        public float RandomSpeedChangeInterval => Random.Range(speedChangeIntervalMin, speedChangeIntervalMax);
        public Vector3 RotationSpeed { get; private set; }

        private void OnValidate()
        {
            if (speedChangeIntervalMin > speedChangeIntervalMax)
            {
                speedChangeIntervalMax = speedChangeIntervalMin;
                Debug.Log("Min speed must be <= MaxSpeed. Setting MaxSpeed to MinSpeed.");
            }
        }

        //private Projectile _projectile;

        public override void Tick()
        {
            Projectile.transform.Rotate(RotationSpeed * Time.deltaTime);
        }

        public override void LaunchProjectile(Projectile projectile)
        {
            _speedChangeTimer = 0f;
            RotationSpeed = GetRotationSpeed();
            if (speedChangeIntervalMax > 0)
            {
                SetNextSpeedChange();
                StartTheCoroutine();
            }
        }

        private void StartTheCoroutine()
        {
            if (_speedChangeCoroutine != null)
                return;

            _speedChangeCoroutine = Projectile.StartCoroutine(SpeedChange());
        }

        private void SetNextSpeedChange()
        {
            _speedChangeTimer = 0f;
            _nextSpeedChange = RandomSpeedChangeInterval;
        }

        private IEnumerator SpeedChange()
        {
            while (true)
            {
                if (_speedChangeTimer >= RandomSpeedChangeInterval)
                {
                    RotationSpeed = GetRotationSpeed();
                    SetNextSpeedChange();
                }
                else
                {
                    _speedChangeTimer += Time.deltaTime;
                }

                yield return null;
            }
        }

        public Vector3 GetRotationSpeed()
        {
            var newRotation = rotationAxis;
            if (separateAxisSpeeds)
            {
                newRotation.x *= RandomSpeed;
                newRotation.y *= RandomSpeed;
                newRotation.z *= RandomSpeed;
            }
            else
            {
                newRotation *= RandomSpeed;
            }

            return newRotation;
        }
    }
}