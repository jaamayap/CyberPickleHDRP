using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Serializable]
    public class SpawnerLifecycleActionsObserverObject : SpawnerObserverObject
    {
        [Header("Required")] public SpawnerLifecycleEvent[] onEvents = { SpawnerLifecycleEvent.StartProjectile };

        public SpawnerLifecycleEvent[] offEvents = { SpawnerLifecycleEvent.StopProjectile };

        [Space] public List<GameObject> onlyTheseProjectileObjects = new();

        [Header("Options")] public float delayBeforeOnAction;

        public float turnOffDelay;
        private SpawnerLifecycleEvent[] _cachedTurnOffEvents;
        private SpawnerLifecycleEvent[] _cachedTurnOnEvents;

        protected Coroutine _onActionCoroutine;

        // Note start ONLY happens once, and LaunchProjectile will NOT be called at the start,
        // which is why the default toggle on event includes both Start and LaunchProjectile
        protected override void Start()
        {
            base.Start(); // ALWAYS INCLUDE BASE.START!!!!

            if (onEvents.Contains(SpawnerLifecycleEvent.Start)) DoOnAction();
            if (offEvents.Contains(SpawnerLifecycleEvent.Start)) DoOffAction();
        }

        protected override void OnValidate()
        {
            base.OnValidate(); // ALWAYS CALL THIS :)

            if (onEvents.Length == 0 && offEvents.Length == 0)
            {
                onEvents = new[] { SpawnerLifecycleEvent.StartProjectile };
                offEvents = new[] { SpawnerLifecycleEvent.StopProjectile };
            }

            foreach (var onEvent in onEvents)
            {
                if (!offEvents.Contains(onEvent)) continue;

                Debug.LogError("Turn On and Turn Off events must not have the same events! Will reset.");
                // Handles resetting of turnOnEvent and turnOffEvent here
                if (onEvents != _cachedTurnOnEvents) onEvents = _cachedTurnOnEvents;
                if (offEvents != _cachedTurnOffEvents) offEvents = _cachedTurnOffEvents;

                break; // Exiting the loop after reset as there's no need to continue
            }

            _cachedTurnOnEvents = onEvents;
            _cachedTurnOffEvents = offEvents;
        }

        /*
         * This is the main method you'll likely want to override. This is the method that will be called when the
         * event you've selected is triggered. You can use this method to do whatever you want when the event is triggered.
         *
         * Note the methods have access to various parameters that can be used to get information about the event that
         * was triggered. They are optional otherwise.
         */
        protected virtual void DoOnAction(GameObject projectileObject = default,
            SpawnBehavior spawnBehaviorInstance = default,
            Transform spawnTransform = default)
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

        protected override void ProjectileLaunched(GameObject projectileObject, SpawnBehavior spawnBehaviorInstance,
            Transform spawnTransform)
        {
            if (onlyTheseProjectileObjects.Count != 0 &&
                !onlyTheseProjectileObjects.Contains(targetSpawner.ProjectileObject))
                return;

            if (onEvents.Contains(SpawnerLifecycleEvent.StartProjectile))
                DoOnAction(projectileObject, spawnBehaviorInstance, spawnTransform);
            if (offEvents.Contains(SpawnerLifecycleEvent.StartProjectile)) DoOffAction();
        }

        protected override void ProjectileStopped()
        {
            if (onlyTheseProjectileObjects.Count != 0 &&
                !onlyTheseProjectileObjects.Contains(targetSpawner.LastProjectileObject))
                return;

            if (onEvents.Contains(SpawnerLifecycleEvent.StopProjectile)) DoOnAction();
            if (offEvents.Contains(SpawnerLifecycleEvent.StopProjectile)) DoOffAction();
        }
    }
}