using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Forward Movement with Speed Curve",
        menuName = "Projectile Factory/Moving Behavior/Forward Movement with Speed Curve")]
    [Serializable]
    public class ForwardWithSpeedCurve : MovementBehaviorForward
    {
        [Header("Speed Curve")] [ShowInProjectileEditor("Speed Curve")]
        public AnimationCurve speedCurve = AnimationCurve.Linear(0, 1, 1, 1);

        [ShowInProjectileEditor("Time Length")]
        public float timeLength = 10f;

        [ShowInProjectileEditor("Repeat")] public int repeat = -1;

        private int _repetitions;
        private float _speed;

        private float _time;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "Moves the projectile forward with a speed curve.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Move";

        protected bool RepeatForever => repeat < 0;
        protected bool DoneRepeating => repeat >= 0 && _repetitions > repeat;

        protected override void Reset()
        {
            base.Reset();
            _time = 0f;
            _repetitions = 0;
        }

        public override void LaunchProjectile(Projectile projectile)
        {
            base.LaunchProjectile(Projectile);
            Reset();
            SetSpeed(0f);
        }

        protected virtual void SetSpeed(float curveValue)
        {
            _speed = speedCurve.Evaluate(curveValue) * ProjectileSpeed;
            Debug.Log($"_Speed is {_speed} from {curveValue} and {ProjectileSpeed}");
        }

        public override void Tick()
        {
            if (DoneRepeating && !RepeatForever)
            {
                base.Tick();
                return;
            }

            IncreaseTime();
            Debug.Log($"Done Repeating: {DoneRepeating} from {_repetitions} repetitions, Repeat Forever: {RepeatForever}, Time: {_time}, Time Length: {timeLength}");
            var curveValue = DoneRepeating && !RepeatForever 
                ? 1f 
                : _time / timeLength;
            SetSpeed(curveValue);
            base.Tick();
        }

        protected virtual void IncreaseTime()
        {
            _time += Time.deltaTime;

            if (!(_time >= timeLength)) return;

            _time -= timeLength;
            _repetitions += 1;
        }

        protected override void Move()
        {
            Projectile.transform.Translate(LocalDirection * (_speed * Time.deltaTime)
                , Space.Self);
        }
    }
}