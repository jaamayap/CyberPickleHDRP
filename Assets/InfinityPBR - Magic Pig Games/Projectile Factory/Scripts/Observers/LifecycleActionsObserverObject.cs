using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Serializable]
    public class LifecycleActionsObserverObject : ObserverObject
    {
        [Header("Required")]
        public LifecycleEvent[] onEvent = { LifecycleEvent.Start, LifecycleEvent.LaunchProjectile };

        public LifecycleEvent[] offEvent = { LifecycleEvent.CollisionEnter };

        [Header("Options")] public float delayBeforeOnAction;

        public float turnOffDelay;
        private LifecycleEvent[] _cachedTurnOffEvent;
        private LifecycleEvent[] _cachedTurnOnEvent;

        protected Coroutine _onActionCoroutine;

        // Note start ONLY happens once, and LaunchProjectile will NOT be called at the start,
        // which is why the default toggle on event includes both Start and LaunchProjectile
        protected override void Start()
        {
            base.Start(); // ALWAYS INCLUDE BASE.START!!!!

            if (onEvent.Contains(LifecycleEvent.Start)) DoOnAction();
            if (offEvent.Contains(LifecycleEvent.Start)) DoOffAction();
        }

        protected override void OnValidate()
        {
            base.OnValidate(); // ALWAYS CALL THIS :)

            if (onEvent.Length == 0 && offEvent.Length == 0)
            {
                onEvent = new[] { LifecycleEvent.Start, LifecycleEvent.LaunchProjectile };
                offEvent = new[] { LifecycleEvent.CollisionEnter };
            }

            foreach (var onEvent in onEvent)
            {
                if (!offEvent.Contains(onEvent)) continue;

                Debug.LogError("Turn On and Turn Off events must not have the same events! Will reset.");
                // Handles resetting of turnOnEvent and turnOffEvent here
                if (this.onEvent != _cachedTurnOnEvent) this.onEvent = _cachedTurnOnEvent;
                if (offEvent != _cachedTurnOffEvent) offEvent = _cachedTurnOffEvent;

                break; // Exiting the loop after reset as there's no need to continue
            }

            _cachedTurnOnEvent = onEvent;
            _cachedTurnOffEvent = offEvent;
        }

        /*
         * This is the main method you'll likely want to override. This is the method that will be called when the
         * event you've selected is triggered. You can use this method to do whatever you want when the event is triggered.
         *
         * Note the methods have access to various parameters that can be used to get information about the event that
         * was triggered. They are optional otherwise.
         */
        protected virtual void DoOnAction(Projectile projectile = null, ProjectileSpawner projectileSpawner = null,
            Collision collision = default, GameObject objectHit = null, Vector3 contactPoint = default)
        {
            // OVERRIDE THIS AND DO YOUR ACTIONS HERE
        }

        protected virtual void DoOffAction(Projectile projectile = null, ProjectileSpawner projectileSpawner = null,
            Collision collision = default, GameObject objectHit = null, Vector3 contactPoint = default)
        {
            // OVERRIDE THIS AND DO YOUR ACTIONS HERE
        }

        protected virtual void StopTheCoroutine()
        {
            if (_onActionCoroutine == null) return;

            StopCoroutine(_onActionCoroutine);
            _onActionCoroutine = null;
        }

        protected virtual IEnumerator OnActionsCoroutine(bool value, Projectile projectile = null,
            ProjectileSpawner projectileSpawner = null, Collision collision = default, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            yield return new WaitForSeconds(value ? delayBeforeOnAction : turnOffDelay);

            // OVERRIDE THIS AND DO YOUR ACTIONS HERE
        }

        public override void LaunchProjectile(Projectile projectile)
        {
            if (onEvent.Contains(LifecycleEvent.LaunchProjectile)) DoOnAction(projectile);
            if (offEvent.Contains(LifecycleEvent.LaunchProjectile)) DoOffAction(projectile);
        }

        public override void DoDestroy(Projectile projectile)
        {
            if (onEvent.Contains(LifecycleEvent.DoDestroy)) DoOnAction(projectile);
            if (offEvent.Contains(LifecycleEvent.DoDestroy)) DoOffAction(projectile);
        }

        public override void Disable(Projectile projectile)
        {
            if (onEvent.Contains(LifecycleEvent.Disable)) DoOnAction(projectile);
            if (offEvent.Contains(LifecycleEvent.Disable)) DoOffAction(projectile);
        }

        public override void Enable(Projectile projectile)
        {
            if (onEvent.Contains(LifecycleEvent.Enable)) DoOnAction(projectile);
            if (offEvent.Contains(LifecycleEvent.Enable)) DoOffAction(projectile);
        }

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (onEvent.Contains(LifecycleEvent.CollisionEnter))
                DoOnAction(projectile, collision: collision, objectHit: objectHit, contactPoint: contactPoint);
            if (offEvent.Contains(LifecycleEvent.CollisionEnter))
                DoOffAction(projectile, collision: collision, objectHit: objectHit, contactPoint: contactPoint);
        }

        public override void CollisionExit(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (onEvent.Contains(LifecycleEvent.CollisionExit))
                DoOnAction(projectile, collision: collision, objectHit: objectHit, contactPoint: contactPoint);
            if (offEvent.Contains(LifecycleEvent.CollisionExit))
                DoOffAction(projectile, collision: collision, objectHit: objectHit, contactPoint: contactPoint);
        }

        public override void CollisionStay(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (onEvent.Contains(LifecycleEvent.CollisionStay))
                DoOnAction(projectile, collision: collision, objectHit: objectHit, contactPoint: contactPoint);
            if (offEvent.Contains(LifecycleEvent.CollisionStay))
                DoOffAction(projectile, collision: collision, objectHit: objectHit, contactPoint: contactPoint);
        }

        public override void TriggerEnter(Projectile projectile, Collider colliderValue, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (onEvent.Contains(LifecycleEvent.TriggerEnter))
                DoOnAction(projectile, objectHit: objectHit, contactPoint: contactPoint);
            if (offEvent.Contains(LifecycleEvent.TriggerEnter))
                DoOffAction(projectile, objectHit: objectHit, contactPoint: contactPoint);
        }

        public override void TriggerExit(Projectile projectile, Collider colliderValue, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (onEvent.Contains(LifecycleEvent.TriggerExit))
                DoOnAction(projectile, objectHit: objectHit, contactPoint: contactPoint);
            if (offEvent.Contains(LifecycleEvent.TriggerExit))
                DoOffAction(projectile, objectHit: objectHit, contactPoint: contactPoint);
        }

        public override void TriggerStay(Projectile projectile, Collider colliderValue, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (onEvent.Contains(LifecycleEvent.TriggerStay))
                DoOnAction(projectile, objectHit: objectHit, contactPoint: contactPoint);
            if (offEvent.Contains(LifecycleEvent.TriggerStay))
                DoOffAction(projectile, objectHit: objectHit, contactPoint: contactPoint);
        }

        public override void OnReturnToPool(Projectile projectile)
        {
            if (onEvent.Contains(LifecycleEvent.OnReturnToPool)) DoOnAction(projectile);
            if (offEvent.Contains(LifecycleEvent.OnReturnToPool)) DoOffAction(projectile);
        }

        public override void OnGetFromPool(Projectile projectile)
        {
            if (onEvent.Contains(LifecycleEvent.OnGetFromPool)) DoOnAction(projectile);
            if (offEvent.Contains(LifecycleEvent.OnGetFromPool)) DoOffAction(projectile);
        }

        protected override void ProjectileStopped()
        {
            if (onEvent.Contains(LifecycleEvent.ProjectileStopped)) DoOnAction();
            if (offEvent.Contains(LifecycleEvent.ProjectileStopped)) DoOffAction();
        }
    }
}