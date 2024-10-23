using System;
using UnityEngine;

// Use in conjunction with RaycastShooter.cs to position an object at the hit point of a raycast.

namespace MagicPigGames
{
    [Documentation("Use in conjunction with RaycastShooter.cs to position an object at the hit point of a raycast.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/additional-scripts/raycast-shooter-+-handlers")]
    [Serializable]
    public class SetPositionFromRaycast : MonoBehaviour, IHandleRaycasts
    {
        [Header("Plumbing")]
        [Tooltip("This is the object that we will be positioning. If null, the object's own transform will be used.")]
        public Transform thisTransform;

        [Tooltip(
            "The transform to use as the base position for the line. If null, the object's own transform will be used.")]
        public Transform baseTransform;

        [Header("(Only when this component is not on thisTransform!)")]
        public bool turnOffOnEnable;

        public bool turnOnWhenPositioned;

        [Header("Options")] [Tooltip("Alternative max distance for this object only. Set to -1 to ignore.")]
        public float maxDistanceOverride = -1f;

        [Tooltip("Default distance to use when the raycast distance is 0.")]
        public float defaultDistance = 50f;

        public bool resetOnDisable = true;
        public bool faceDirectionOfBase = true;

        private float _lineDistance;
        private Vector3 _localStartEulerAngles;
        private Vector3 _localStartPosition;

        private void Awake()
        {
            if (thisTransform == null)
                thisTransform = transform;

            _localStartPosition = thisTransform.localPosition;
            _localStartEulerAngles = thisTransform.localEulerAngles;

            if (baseTransform == null)
            {
                baseTransform = transform.parent;
                if (baseTransform == null)
                {
                    Debug.LogError("We need a base transform for this object to work properly.");
                    enabled = false;
                }
            }
        }

        private void OnEnable()
        {
            if (thisTransform != transform && turnOffOnEnable)
                thisTransform.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (resetOnDisable)
            {
                thisTransform.localPosition = _localStartPosition;
                thisTransform.localEulerAngles = _localStartEulerAngles;
            }
        }

        protected void OnValidate()
        {
            // If thisTransform is not this, set it ot this
            if (thisTransform == null || thisTransform != transform)
                thisTransform = transform;

            // If baseTransform is not set, find a RaycastShooter component on the parent or any child of it, and set it to that transform
            if (baseTransform == null || !baseTransform.IsChildOf(transform.root))
            {
                var raycastShooter = GetComponentInParent<RaycastShooter>();
                if (raycastShooter != null)
                    baseTransform = raycastShooter.raycastOrigin;
            }
        }

        public virtual void HandleRaycastHit(RaycastHit raycastHit, float maxDistance)
        {
            _lineDistance = GetLineDistance(raycastHit.distance, maxDistance);
            PositionObjectOnLine(_lineDistance);
        }

        public void PositionObjectOnLine(float distance)
        {
            _lineDistance = distance;
            thisTransform.position = baseTransform.position + baseTransform.forward * _lineDistance;
            if (thisTransform != transform && turnOnWhenPositioned)
                thisTransform.gameObject.SetActive(true);
            if (faceDirectionOfBase)
                thisTransform.forward = baseTransform.forward;
        }

        private float GetLineDistance(float raycastHitDistance, float maxDistance)
        {
            var distanceToUse = raycastHitDistance == 0 ? defaultDistance : raycastHitDistance;
            return Mathf.Min(maxDistanceOverride > 0 ? maxDistanceOverride : maxDistance, distanceToUse);
        }
    }
}