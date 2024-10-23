using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

// This script will animate a line renderer using a set of textures. This was created specifically for 
// the integration with Sci-Fi Effects by FORGE3D, but can be used with any other texture set.

namespace MagicPigGames.ProjectileFactory
{
    [Documentation(
        "This script will animate a line renderer using a set of textures. This was created specifically for " +
        "the integration with Sci-Fi Effects by FORGE3D, but can be used with any other texture set.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/additional-scripts/raycast-shooter-+-handlers")]
    [RequireComponent(typeof(LineRenderer))]
    public class LineRendererFrameAnimation : MonoBehaviour, IHandleRaycasts
    {
        [FormerlySerializedAs("projectile")] [Header("Plumbing")]
        public Projectile basicProjectile;

        public LineRenderer lineRenderer;

        [Header("Options")] public bool startAnimationOnSpawn = true;

        [Tooltip("Alternative max distance for this object only. Set to -1 to ignore.")]
        public float maxDistanceOverride = -1f;

        [Tooltip("Default distance to use when the raycast distance is 0.")]
        public float defaultDistance = 50f;

        [Header("Animation Setup")] public Texture[] animationFrames;

        [Min(1)] public float maxNormalizedDistance = 10f;

        public float frameStep = 0.035f;
        public bool randomizeFrames;
        public bool animateUVs = true;
        public float uvTime = 5;

        //private Coroutine _animateBeamCoroutine;
        private Coroutine _animationCoroutine;
        private int _currentFrame;

        private RaycastHit _hitPoint;
        private float _initialBeamOffset;
        private float _lineDistance;

        private void Awake()
        {
            // Assign first frame texture
            if (!animateUVs && animationFrames.Length > 0)
                lineRenderer.material.mainTexture = animationFrames[0];

            // Randomize uv offset
            _initialBeamOffset = Random.Range(0f, 5f);

            if (basicProjectile == null)
                basicProjectile = GetComponent<Projectile>();

            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();

            if (startAnimationOnSpawn)
                StartAnimation();
        }

        private void Update()
        {
            AnimateUVs();
            StartAnimation();
        }

        private void OnDisable()
        {
            _currentFrame = 0;

            if (_animationCoroutine == null) return;

            StopCoroutine(_animationCoroutine);
            _animationCoroutine = null;
        }

        public void HandleRaycastHit(RaycastHit raycastHit, float maxDistance)
        {
            _lineDistance = GetLineDistance(raycastHit.distance, maxDistance);
            SetLineRendererPosition(_lineDistance);
        }

        protected virtual void AnimateUVs()
        {
            if (!animateUVs) return;

            var scaleLength = new Vector2(_lineDistance / maxNormalizedDistance, 1f);
            lineRenderer.material.SetTextureScale("_MainTex", scaleLength);

            var offset = new Vector2(_initialBeamOffset + Time.time * uvTime, 0);
            lineRenderer.material.SetTextureOffset("_MainTex", offset);
        }

        public void StartAnimation()
        {
            if (_animationCoroutine != null)
                return;

            _animationCoroutine = StartCoroutine(Animation());
        }

        private void OnFrameStep()
        {
            _currentFrame = randomizeFrames
                ? Random.Range(0, animationFrames.Length)
                : (_currentFrame + 1) % animationFrames.Length;

            lineRenderer.material.mainTexture = animationFrames[_currentFrame];
        }

        private IEnumerator Animation()
        {
            _currentFrame = 0;
            UpdateFrame();

            while (true)
            {
                if (animationFrames.Length > 1)
                {
                    yield return new WaitForSeconds(frameStep);
                    OnFrameStep();
                }

                yield return null;
            }
        }

        private void UpdateFrame()
        {
            if (animationFrames.Length <= 0) return;
            lineRenderer.material.mainTexture = animationFrames[_currentFrame];
        }

        private float GetLineDistance(float raycastHitDistance, float maxDistance)
        {
            var distanceToUse = raycastHitDistance == 0 ? defaultDistance : raycastHitDistance;
            return Mathf.Min(maxDistanceOverride > 0 ? maxDistanceOverride : maxDistance, distanceToUse);
        }

        private void SetLineRendererPosition(float distance = -1f)
        {
            lineRenderer.SetPosition(1, new Vector3(0f, 0f, distance > 0 ? distance : _lineDistance));
        }
    }
}