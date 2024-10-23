using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MagicPigGames
{
    [Documentation(
        "Shoots Raycasts from a point in the scene, and notifies handlers of the results, which allow them to perform additional actions.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/additional-scripts/raycast-shooter-+-handlers")]
    [Serializable]
    public class RaycastShooter : MonoBehaviour
    {
        [Header("Origin")] [Tooltip("If not set, the transform of this GameObject will be used.")]
        public Transform raycastOrigin;

        [Tooltip("Offset from the origin position.")]
        public Vector3 originOffset = Vector3.zero;

        [Header("Options")] [Tooltip("The maximum distance the ray should travel.")]
        public float maxDistance = 50f;

        [Tooltip("The interval between each raycast. -1 means check every frame.")]
        public float raycastInterval = -1f;

        [Tooltip("The maximum number of shots that can be fired. -1 means no limit.")]
        public int maxShots = -1;

        [Tooltip("The delay before the first raycast is fired.")]
        public float startDelay;

        [Tooltip("If true, inactive handlers will be included when all handlers are gathered at the start.")]
        public bool includeInactiveHandlers = true;

        private bool _done;
        private Ray _ray;

        private Coroutine _raycastCoroutine;

        private List<IHandleRaycasts> _raycastHandlers = new();
        private RaycastHit _raycastHit;
        private int _shotsFired;

        private float _startDelayTimer;

        private Vector3 OriginPosition => (raycastOrigin
            ? raycastOrigin.position
            : transform.position) + originOffset;

        // We use Awake to ensure that the handlers are added before the first Update call.
        protected void Awake()
        {
            AddRaycastHandlersFromChildren();
        }

        // Reset the values to their defaults
        private void Reset()
        {
            _shotsFired = 0;
            _done = false;
            _startDelayTimer = 0f;
        }

        protected virtual void Update()
        {
            if (_done) return;

            // Simple delay at the start
            if (_startDelayTimer < startDelay)
            {
                _startDelayTimer += Time.deltaTime;
                return;
            }

            StartRaycast();
        }

        protected void OnEnable()
        {
            Reset();
        }

        protected void OnDisable()
        {
            StopTheCoroutine();
            Reset();
        }

        protected void OnValidate()
        {
            // if raycastOrigin is not this object or a child of it or set, set it to this object
            if (raycastOrigin == null || !raycastOrigin.IsChildOf(transform))
                raycastOrigin = transform;
        }

        private void StartRaycast()
        {
            if (MaxShotsReached())
                return;

            // If the interval is set to <= 0, we raycast every frame
            if (raycastInterval <= 0)
            {
                Raycast();
                return;
            }

            // Otherwise, we start the coroutine
            StopTheCoroutine();
            _raycastCoroutine ??= StartCoroutine(RaycastCoroutine());
        }

        private bool MaxShotsReached()
        {
            // If we have a maxShots value of -1, we don't have a limit
            if (maxShots <= 0 || _shotsFired < maxShots)
                return false;

            StopTheCoroutine();
            _done = true;
            return true;
        }

        private IEnumerator RaycastCoroutine()
        {
            while (true)
            {
                if (MaxShotsReached())
                    yield break;

                Raycast();
                yield return new WaitForSeconds(raycastInterval);
            }
        }

        private void StopTheCoroutine()
        {
            if (_raycastCoroutine == null)
                return;

            StopCoroutine(_raycastCoroutine);
            _raycastCoroutine = null;
        }

        // Add all IHandleRaycasts components from children to the list of handlers
        protected virtual void AddRaycastHandlersFromChildren()
        {
            var components = GetComponentsInChildren<IHandleRaycasts>(includeInactiveHandlers);
            foreach (var component in components)
                AddRaycastHandler(component);
        }

        public virtual void AddRaycastHandler(IHandleRaycasts handler)
        {
            if (!_raycastHandlers.Contains(handler))
                _raycastHandlers.Add(handler);
        }

        public virtual void RemoveRaycastHandler(IHandleRaycasts handler)
        {
            if (_raycastHandlers.Contains(handler))
                _raycastHandlers.Remove(handler);
        }

        // Perform a raycast and notify the handlers of the result. We also keep track of the number of 
        // shots fired, and draw a debug line in the scene view.
        public virtual void Raycast(float altMaxDistance = -1)
        {
            var distance = altMaxDistance > 0 ? altMaxDistance : maxDistance;
            _ray = new Ray(OriginPosition, transform.forward);

            _shotsFired += 1;

            // Draw a debug line from the start to distnace
            Debug.DrawRay(OriginPosition, transform.forward * distance, Color.magenta, 1f);

            if (Physics.Raycast(_ray, out _raycastHit, distance))
                NotifyHandlers(distance);
            else
                NotifyHandlers(distance);
        }

        // Notify all handlers of the raycast result by passing the RaycastHit and the distance used.
        protected virtual void NotifyHandlers(float maxDistanceUsed)
        {
            foreach (var handler in _raycastHandlers.Where(handler => handler != null))
                handler.HandleRaycastHit(_raycastHit, maxDistanceUsed);
        }
    }
}