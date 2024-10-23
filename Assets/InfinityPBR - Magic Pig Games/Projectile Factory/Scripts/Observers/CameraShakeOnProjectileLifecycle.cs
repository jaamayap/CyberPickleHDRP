using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Documentation(
        "This class will shake the camera when a projectile collides with something. As a global observer, " +
        "it will follow all Projectiles. The shake strength can be modified by distance, using a curve.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/observers-global-observers-and-observer-objects")]
    [Serializable]
    public class CameraShakeOnProjectileLifecycle : GlobalObserver
    {
        [Header("Required")] public Camera thisCamera;

        public LifecycleEvent[] lifecycleEvents = { LifecycleEvent.CollisionEnter, LifecycleEvent.TriggerEnter };

        [Header("Shake Settings")] [Tooltip("Determines how long the shake will last.")]
        public float shakeDuration = 0.5f;

        [Tooltip("Determines the strength of the shake, i.e. the distance from center.")]
        public float shakeMagnitude = 0.5f;

        [Tooltip("Determines how fast the shake will be.")]
        public float shakeFrequency = 10f;

        public Vector3 shakeDirections = Vector3.one;

        [Header("Options")]
        [Tooltip("When true, the strength of the shake will be modified by distance, using the curve.")]
        public bool distanceCheck;

        [Tooltip("The maximum distance from the camera that the shake will be applied.")]
        public float maxDistance = 40f;

        [Tooltip("The curve that will be used to modify the shake strength based on distance.")]
        public AnimationCurve distanceCurve = AnimationCurve.Linear(0, 1, 1, 0);

        private Vector3 _originalPos;
        private float _remainingDuration;
        private Coroutine _shakeCoroutine;

        protected virtual void Awake()
        {
            if (thisCamera == null)
                thisCamera = Camera.main;

            if (thisCamera == null)
            {
                Debug.LogError($"No camera was attached to {gameObject.name}. Disabling {GetType().Name}.");
                enabled = false;
                return;
            }

            _originalPos = thisCamera.transform.localPosition;
        }

        protected virtual void OnValidate()
        {
            if (lifecycleEvents.Length == 0)
                lifecycleEvents = new[] { LifecycleEvent.CollisionEnter, LifecycleEvent.TriggerEnter };

            if (thisCamera == null)
                thisCamera = GetComponent<Camera>();

            if (lifecycleEvents.Contains(LifecycleEvent.Start))
            {
                Debug.LogWarning("The Start event is not supported by this observer. Disabling it.");
                lifecycleEvents = lifecycleEvents.Where(e => e != LifecycleEvent.Start).ToArray();
            }
        }

        protected virtual void HandleCollisionData(Projectile projectile, Collision collision,
            GameObject objectHit = null, Vector3 contactPoint = default)
        {
            Vector3 point;
            if (contactPoint != default)
                point = contactPoint;
            else if (collision != null)
                point = collision.contacts[0].point;
            else if (objectHit != null)
                point = objectHit.transform.position;
            else
                point = projectile.transform.position;
            DoTheShake(point);
        }

        protected virtual void HandleTriggerData(Projectile projectile, Collider collider,
            GameObject objectHit = null, Vector3 contactPoint = default)
        {
            Vector3 point;
            if (contactPoint != default)
                point = contactPoint;
            else if (collider != null)
                point = collider.ClosestPointOnBounds(projectile.transform.position);
            else if (objectHit != null)
                point = objectHit.transform.position;
            else
                point = projectile.transform.position;
            DoTheShake(point);
        }

        protected override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (!lifecycleEvents.Contains(LifecycleEvent.CollisionEnter)) return;
            HandleCollisionData(projectile, collision, objectHit, contactPoint);
        }

        protected override void TriggerEnter(Projectile projectile, Collider colliderValue, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (!lifecycleEvents.Contains(LifecycleEvent.TriggerEnter)) return;
            HandleTriggerData(projectile, colliderValue, objectHit, contactPoint);
        }

        protected override void CollisionStay(Projectile projectile, Collision collision,
            GameObject objectHit = null, Vector3 contactPoint = default)
        {
            if (!lifecycleEvents.Contains(LifecycleEvent.CollisionStay)) return;
            HandleCollisionData(projectile, collision, objectHit, contactPoint);
        }

        protected override void TriggerStay(Projectile projectile, Collider colliderValue, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (!lifecycleEvents.Contains(LifecycleEvent.TriggerStay)) return;
            HandleTriggerData(projectile, colliderValue, objectHit, contactPoint);
        }

        protected override void CollisionExit(Projectile projectile, Collision collision,
            GameObject objectHit = null, Vector3 contactPoint = default)
        {
            if (!lifecycleEvents.Contains(LifecycleEvent.CollisionExit)) return;
            HandleCollisionData(projectile, collision, objectHit, contactPoint);
        }

        protected override void TriggerExit(Projectile projectile, Collider colliderValue, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (!lifecycleEvents.Contains(LifecycleEvent.TriggerExit)) return;
            HandleTriggerData(projectile, colliderValue, objectHit, contactPoint);
        }

        protected override void OnLaunch(Projectile projectile)
        {
            if (!lifecycleEvents.Contains(LifecycleEvent.LaunchProjectile)) return;
            DoTheShake(projectile.transform.position);
        }

        protected override void DoDestroy(Projectile projectile)
        {
            if (!lifecycleEvents.Contains(LifecycleEvent.DoDestroy)) return;
            DoTheShake(projectile.transform.position);
        }

        protected override void Disable(Projectile projectile)
        {
            if (!lifecycleEvents.Contains(LifecycleEvent.Disable)) return;
            DoTheShake(projectile.transform.position);
        }

        protected override void Enable(Projectile projectile)
        {
            if (!lifecycleEvents.Contains(LifecycleEvent.Enable)) return;
            DoTheShake(projectile.transform.position);
        }

        protected override void ReturnToPool(Projectile projectile)
        {
            if (!lifecycleEvents.Contains(LifecycleEvent.OnReturnToPool)) return;
            DoTheShake(projectile.transform.position);
        }

        protected override void GetFromPool(Projectile projectile)
        {
            if (!lifecycleEvents.Contains(LifecycleEvent.OnGetFromPool)) return;
            DoTheShake(projectile.transform.position);
        }

        private void DoTheShake(Vector3 point)
        {
            var valueOnCurve = DistanceValueOnCurve(point);
            ShakeCamera(shakeDuration * valueOnCurve, shakeMagnitude * valueOnCurve, shakeFrequency * valueOnCurve);
        }

        private float DistanceValueOnCurve(Vector3 contactPoint)
        {
            if (!distanceCheck) return 1f;

            var distance = Vector3.Distance(thisCamera.transform.position, contactPoint);
            var distancePercent = distance / maxDistance;
            return distanceCurve.Evaluate(distancePercent);
        }

        public void ShakeCamera(float duration, float magnitude, float roughness)
        {
            if (_shakeCoroutine == null)
                _originalPos = thisCamera.transform.localPosition; // Only cache when we aren't shaking

            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
                duration += _remainingDuration; // Optionally extend the shake duration if called again before finishing
            }

            _shakeCoroutine = StartCoroutine(Shake(duration, magnitude, roughness));
        }

        private IEnumerator Shake(float duration, float magnitude, float frequency)
        {
            var elapsed = 0.0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var percentComplete = elapsed / duration;
                var damper = 1.0f - Mathf.Clamp(4.0f * percentComplete - 3.0f, 0.0f, 1.0f);

                // Calculate random shake amount
                var x = Mathf.PerlinNoise(Time.time * frequency, 0) * 2f - 1f;
                var y = Mathf.PerlinNoise(0, Time.time * frequency) * 2f - 1f;
                var z = Mathf.PerlinNoise(Time.time * frequency, Time.time * frequency) * 2f - 1f;
                var shakeAmount = new Vector3(x, y, z) * (magnitude * damper);

                // Apply direction multipliers
                shakeAmount.Scale(shakeDirections);

                thisCamera.transform.localPosition = _originalPos + shakeAmount;

                yield return null;
            }

            // Smoothly transition back to the original position to ensure it ends where it started
            const float returnTime = 0.1f; // Time to return to original position
            var returnElapsed = 0.0f;
            while (returnElapsed < returnTime)
            {
                returnElapsed += Time.deltaTime;
                thisCamera.transform.localPosition = Vector3.Lerp(thisCamera.transform.localPosition, _originalPos,
                    returnElapsed / returnTime);
                yield return null;
            }

            thisCamera.transform.localPosition = _originalPos;
        }
    }
}