using System;
using UnityEngine;

namespace MagicPigGames
{
    [Documentation("Use in conjunction with RaycastShooter.cs to scale a transform by the length of a Raycast.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/additional-scripts/raycast-shooter-+-handlers")]
    [Serializable]
    public class ScaleFromRaycastLength : MonoBehaviour, IHandleRaycasts
    {
        [Header("Plumbing")]
        [Tooltip("This is the object that we will be scaling. If null, the object's own transform will be used.")]
        public Transform thisTransform;

        [Header("Directions to Scale")] public bool scaleOnX;

        public bool scaleOnY;
        public bool scaleOnZ = true;

        [Header("Options")] [Tooltip("Alternative max distance for this object only. Set to -1 to ignore.")]
        public float maxDistanceOverride = -1f;

        [Tooltip("Default distance to use when the raycast distance is 0.")]
        public float defaultDistance = 50f;

        private float _lineDistance;

        protected void OnValidate()
        {
            // If thisTransform is not this, set it ot this
            if (thisTransform == null)
                thisTransform = transform;
        }

        public virtual void HandleRaycastHit(RaycastHit raycastHit, float maxDistance)
        {
            _lineDistance = GetLineDistance(raycastHit.distance, maxDistance);
            ScaleObjectByLine(_lineDistance);
        }

        public void ScaleObjectByLine(float distance)
        {
            _lineDistance = distance;
            thisTransform.localScale = new Vector3(
                scaleOnX ? _lineDistance : thisTransform.localScale.x,
                scaleOnY ? _lineDistance : thisTransform.localScale.y,
                scaleOnZ ? _lineDistance : thisTransform.localScale.z);
        }

        private float GetLineDistance(float raycastHitDistance, float maxDistance)
        {
            var distanceToUse = raycastHitDistance == 0 ? defaultDistance : raycastHitDistance;
            return Mathf.Min(maxDistanceOverride > 0 ? maxDistanceOverride : maxDistance, distanceToUse);
        }
    }
}