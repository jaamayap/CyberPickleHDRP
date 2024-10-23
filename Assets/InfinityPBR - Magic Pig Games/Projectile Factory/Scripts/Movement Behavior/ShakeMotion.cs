using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Shake Motion",
        menuName = "Projectile Factory/Moving Behavior/Shake Motion")]
    [Serializable]
    public class ShakeMotion : MovementBehavior
    {
        [Header("Shake Settings")]
        [Tooltip("Determines the direction of the shake.")]
        [ShowInProjectileEditor("Shake Direction")]
        public Vector3 shakeDirection = new(1, 1, 1);

        [Tooltip("Determines the strength of the shake, i.e. the distance from center.")]
        [ShowInProjectileEditor("Shake Magnitude")]
        public float shakeMagnitude = 0.5f;

        [Tooltip("Determines how fast the shake will be.")] [ShowInProjectileEditor("Shake Frequency")]
        public float shakeFrequency = 10f;

        [ShowInProjectileEditor("Shake Curve")]
        public AnimationCurve shakeCurve = AnimationCurve.Linear(0, 1, 1, 1);

        [ShowInProjectileEditor("Animation Time Length")]
        public float animationTimeLength = 10f;

        [Header("Timing")] [ShowInProjectileEditor("Delay")]
        public float delay;

        [ShowInProjectileEditor("Repeat")] public int repeat = -1;

        [ShowInProjectileEditor("Delay Between Repetitions")]
        public float delayBetweenRepetitions;

        [Header("Options")] [ShowInProjectileEditor("Destroy After Shaking")]
        public bool destroyAfterShaking;

        [ShowInProjectileEditor("End Delay On Collision Enter")]
        public bool endDelayOnCollisionEnter;

        private float _delayTimer;
        private Vector3 _initialPosition;
        private bool _positionSet;
        private int _repetitions;

        private float _time;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "Shakes the projectile in a direction.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Shake";

        protected bool RepeatForever => repeat < 0;

        private void Reset()
        {
            _time = 0f;
            _delayTimer = delay;
            _repetitions = 0;
            _positionSet = false;
        }

        public override void LaunchProjectile(Projectile projectile)
        {
            base.LaunchProjectile(Projectile);
            Reset();
        }

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (!endDelayOnCollisionEnter) return;

            _delayTimer = 0;
        }

        public override void Tick()
        {
            if (DelayIsActive())
                return;

            SetPosition();

            if (DoneRepeating() && !RepeatForever)
                return;

            IncreaseTime();
            ShakeProjectile();

            TryDestroy();
        }

        protected virtual void TryDestroy()
        {
            if (!destroyAfterShaking) return;
            if (RepeatForever) return;
            if (!DoneRepeating()) return;

            Projectile.TriggerDestroy();
        }

        protected virtual void ShakeProjectile()
        {
            var curvePosition = _time / animationTimeLength;
            var curveValue = shakeCurve.Evaluate(curvePosition);
            var shakeAmount = shakeDirection * (shakeMagnitude * curveValue);

            var perlinX = Mathf.PerlinNoise(_time * shakeFrequency, 0) * 2f - 1f;
            var perlinY = Mathf.PerlinNoise(0, _time * shakeFrequency) * 2f - 1f;
            var perlinZ = Mathf.PerlinNoise(_time * shakeFrequency, _time * shakeFrequency) * 2f - 1f;

            var perlinNoise = new Vector3(perlinX, perlinY, perlinZ);
            var shake = Vector3.Scale(perlinNoise, shakeAmount);

            // Get a value for the local scale compared to 1
            var localScale = Projectile.transform.localScale;
            var scaleValue = (localScale.x + localScale.y + localScale.z) / 3f;
            shake *= scaleValue;

            if (Projectile.transform.parent)
                Projectile.transform.localPosition = _initialPosition + shake;
            else
                Projectile.transform.position = _initialPosition + shake;
        }


        protected virtual bool DoneRepeating()
        {
            return _repetitions > repeat;
        }

        protected virtual bool DelayIsActive()
        {
            if (!(_delayTimer > 0)) return false;

            _delayTimer -= Time.deltaTime;
            return true;
        }

        protected virtual void SetPosition()
        {
            if (_positionSet) return;

            _initialPosition = Projectile.transform.parent != null
                ? Projectile.transform.localPosition
                : Projectile.transform.position;

            _positionSet = true;
        }

        protected virtual void IncreaseTime()
        {
            _time += Time.deltaTime;

            if (!(_time >= animationTimeLength)) return;

            _time -= animationTimeLength;
            _repetitions += 1;
            delayBetweenRepetitions = Mathf.Max(0, delayBetweenRepetitions);
        }
    }
}