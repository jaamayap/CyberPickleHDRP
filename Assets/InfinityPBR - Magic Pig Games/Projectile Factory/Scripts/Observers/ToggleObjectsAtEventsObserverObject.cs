using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Documentation("IMPORTANT: Do not put this component on any object you plan to toggle, " +
                   "since once the object is off, this script will not work.\n\nToggles objects on and off at " +
                   "projectile lifecycle events that you select.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/observers-global-observers-and-observer-objects")]
    [Serializable]
    public class ToggleObjectsAtEventsObserverObject : LifecycleActionsObserverObject
    {
        [Header("Toggle Options")] public List<GameObject> objects = new();

        protected override void OnValidate()
        {
            base.OnValidate(); // ALWAYS CALL THIS :)

            objects = objects.Where(obj => obj.transform.IsChildOf(transform.root)).ToList();
        }

        protected override void DoOnAction(Projectile projectile = null, ProjectileSpawner projectileSpawner = null,
            Collision collision = default, GameObject objectHit = null, Vector3 contactPoint = default)
        {
            if (delayBeforeOnAction > 0)
            {
                StopTheCoroutine();
                _onActionCoroutine = StartCoroutine(OnActionsCoroutine(true));
                return;
            }

            foreach (var obj in objects)
                SetObject(obj, true);
        }

        protected override void DoOffAction(Projectile projectile = null, ProjectileSpawner projectileSpawner = null,
            Collision collision = default, GameObject objectHit = null, Vector3 contactPoint = default)
        {
            if (delayBeforeOnAction > 0)
            {
                StopTheCoroutine();
                StartCoroutine(OnActionsCoroutine(false));
                return;
            }

            foreach (var obj in objects)
            {
                SetObject(obj, false);
            }
        }

        protected virtual void SetObject(GameObject obj, bool value)
        {
            obj.SetActive(value);
        }


        protected override IEnumerator OnActionsCoroutine(bool value, Projectile projectile = null,
            ProjectileSpawner projectileSpawner = null, Collision collision = default, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            yield return new WaitForSeconds(value ? delayBeforeOnAction : turnOffDelay);

            foreach (var obj in objects)
                SetObject(obj, value);
        }
    }
}