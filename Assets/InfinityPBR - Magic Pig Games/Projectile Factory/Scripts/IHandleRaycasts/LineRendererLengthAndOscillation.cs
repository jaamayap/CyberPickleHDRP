using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MagicPigGames.ProjectileFactory
{
    [Documentation(
        "This script will animate a line renderer using a set of textures. This was created specifically for " +
        "the integration with Sci-Fi Effects by FORGE3D, but can be used with any other texture set.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/additional-scripts/raycast-shooter-+-handlers")]
    [Serializable]
    public class LineRendererLengthAndOscillation : LineRendererLength
    {
        public bool oscillate = true;
        public float amplitude = 0.3f;
        public float oscillateTime = 0.05f;

        private Coroutine _oscillateBeamCoroutine;

        protected override void Update()
        {
            base.Update();
            StartOscillation();
        }

        protected virtual void OnDisable()
        {
            if (_oscillateBeamCoroutine != null)
                StopCoroutine(_oscillateBeamCoroutine);
            _oscillateBeamCoroutine = null;
        }

        protected virtual void StartOscillation()
        {
            _oscillateBeamCoroutine ??= StartCoroutine(Oscillate());
        }

        protected virtual IEnumerator Oscillate()
        {
            yield return new WaitForSeconds(oscillateTime);
            OscillateBeam();

            StartCoroutine(Oscillate());
        }

        protected virtual float RandomAmplitude()
        {
            return Random.Range(-amplitude, amplitude);
        }

        protected virtual void OscillateBeam()
        {
            if (!oscillate) return;

            lineRenderer.positionCount = ComputePoints();
            SetLineRendererPosition();
        }

        protected override void SetLineRendererPosition()
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
                var randomPosition = new Vector3(RandomAmplitude(), RandomAmplitude(), positionOnLine);

                lineRenderer.SetPosition(i, randomPosition);
            }
        }
    }
}