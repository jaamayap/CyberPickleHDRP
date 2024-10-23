using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Documentation("Use in conjunction with RaycastShooter.cs to set the LineRenderer length and position.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/additional-scripts/raycast-shooter-+-handlers")]
    [RequireComponent(typeof(LineRenderer))]
    public class LineRendererLength : MonoBehaviour, IHandleRaycasts
    {
        [Header("Plumbing")] public Projectile projectile;

        public LineRenderer lineRenderer;

        [Header("Beam Options")] [Min(2)] public int minBeamPoints = 2;

        public int maxBeamPoints = 5;
        public float beamPointPerDistance = 10f;

        [Tooltip("Alternative max distance for this object only. Set to -1 to ignore.")]
        public float maxDistanceOverride = -1f;

        [Tooltip("Default distance to use when the raycast distance is 0.")]
        public float defaultDistance = 50f;

        public float beamScaleFactor = 1;
        public float beamScaleFactorMultiplier = 0.1f;

        protected float _lineDistance;

        protected virtual void Awake()
        {
            if (projectile == null)
                projectile = GetComponent<Projectile>();

            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();

            if (lineRenderer.useWorldSpace)
            {
                lineRenderer.useWorldSpace = false;
                Debug.LogWarning(
                    "LineRendererLength: LineRenderer should not use world space. Setting useWorldSpace to false.");
            }
        }

        protected virtual void Update()
        {
            lineRenderer.positionCount = ComputePoints();
        }

        protected void OnValidate()
        {
            // If Projectile is null or not on THIS ojbect or it's children, find it on this object or it's children
            if (projectile == null || !projectile.transform.IsChildOf(transform))
                projectile = GetComponentInChildren<Projectile>();

            // If LineRenderer is null or not on THIS object or it's children, find it on this object or it's children
            if (lineRenderer == null || !lineRenderer.transform.IsChildOf(transform))
                lineRenderer = GetComponentInChildren<LineRenderer>();

            if (lineRenderer.useWorldSpace)
            {
                lineRenderer.useWorldSpace = false;
                Debug.LogWarning(
                    "LineRendererLength: LineRenderer should not use world space. Setting useWorldSpace to false.");
            }
        }

        public virtual void HandleRaycastHit(RaycastHit raycastHit, float maxDistance)
        {
            _lineDistance = GetLineDistance(raycastHit.distance, maxDistance);
            SetLineDistance(_lineDistance);
        }

        protected virtual void SetLineRendererPosition()
        {
            var positionCount = lineRenderer.positionCount;

            for (var i = 0; i < positionCount; i++)
            {
                if (i == 0)
                {
                    lineRenderer.SetPosition(0, Vector3.zero);
                    continue;
                }

                if (i == positionCount - 1)
                {
                    lineRenderer.SetPosition(positionCount - 1, new Vector3(0f, 0f, _lineDistance));
                    continue;
                }

                var positionOnLine = _lineDistance / (positionCount - 1) * i;
                var randomPosition = new Vector3(0, 0, positionOnLine);

                lineRenderer.SetPosition(i, randomPosition);
            }
        }

        public virtual float GetLineDistance(float raycastHitDistance, float maxDistance)
        {
            var distanceToUse = raycastHitDistance == 0 ? defaultDistance : raycastHitDistance;
            return Mathf.Min(maxDistanceOverride > 0 ? maxDistanceOverride : maxDistance, distanceToUse);
        }

        public virtual void SetLineDistance(float distance)
        {
            _lineDistance = distance;
            SetLineRendererPosition();
            SetTextureScale();
        }

        protected virtual void SetTextureScale()
        {
            lineRenderer.material.SetTextureScale("_MainTex", new Vector2(TextureScale(_lineDistance), 1f));
        }

        protected virtual float TextureScale(float distance)
        {
            return distance * (beamScaleFactor * beamScaleFactorMultiplier);
        }

        protected virtual int ComputePoints()
        {
            var pointsByDistance = (int)(_lineDistance / beamPointPerDistance);
            var points = Math.Max(minBeamPoints, pointsByDistance);
            return Math.Min(maxBeamPoints, points);
        }
    }
}