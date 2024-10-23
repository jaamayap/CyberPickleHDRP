using System;
using System.Collections;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Documentation("This handles a variety of aspects of the particle system, lights, and more.")]
    [Serializable]
    public class ProjectileSystemParticle : MonoBehaviour
    {
        [Tooltip("How long after Stop() is called that the entire object will be destroyed.")]
        public float destroyDelay = 5f;

        [Tooltip("These are the ParticleSystems that will be stopped when the projectile is stopped.")]
        public ParticleSystem[] particleSystems;

        [Space] [Tooltip("These are the LineRenderers that will be faded out when the projectile is stopped.")]
        public LineRenderer[] lineRenderers;

        [Tooltip("The time it takes for the line renderers to fade out.")]
        public float fadeTimeLineRenders;

        [Tooltip("If true, the line renderer end point will be adjusted to the hit point.")]
        public bool adjustLineRendererEndPointToHitPoint = true;

        [Space] [Tooltip("These are the Lights that will be faded out when the projectile is stopped.")]
        public Light[] lights;

        [Tooltip("The time it takes for the lights to fade out.")]
        public float fadeTimeLights;

        [Space] [Tooltip("These are the AudioSources that will be faded out when the projectile is stopped.")]
        public AudioSource[] audioSources;

        [Tooltip("The time it takes for the audio sources to fade out.")]
        public float fadeAudioTime;

        private float _destroyTimer;
        public Projectile BasicProjectile { get; private set; }

        public virtual void Update()
        {
            AdjustLineRendererEndPointToHitPoint();
        }

        public virtual void SetProjectile(Projectile value)
        {
            BasicProjectile = value;
        }

        private void AdjustLineRendererEndPointToHitPoint()
        {
            if (!adjustLineRendererEndPointToHitPoint) return;
        }

        public void Stop()
        {
            StopParticleSystemsEmission();
            // If the object is inactive, return
            if (!gameObject.activeInHierarchy) return;
            StartCoroutine(FadeLights());
            StartCoroutine(FadeLineRenderers());
            StartCoroutine(FadeAudioSources());
            StartCoroutine(DestroyThis());
        }

        protected virtual IEnumerator DestroyThis()
        {
            _destroyTimer = destroyDelay;
            while (_destroyTimer > 0)
            {
                _destroyTimer -= Time.deltaTime;
                yield return null;
            }

            Destroy(gameObject);
        }

        protected virtual IEnumerator FadeAudioSources()
        {
            // If there is no fade time, then just stop the audio sources.
            if (fadeAudioTime <= 0)
            {
                foreach (var audioSource in audioSources)
                    audioSource.Stop();
                yield break;
            }

            var startVolumes = new float[audioSources.Length];
            for (var i = 0; i < audioSources.Length; i++)
                startVolumes[i] = audioSources[i].volume;

            var timer = fadeAudioTime;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                for (var i = 0; i < audioSources.Length; i++)
                    audioSources[i].volume = Mathf.Lerp(0, startVolumes[i], timer / fadeAudioTime);
                yield return null;
            }
        }

        protected virtual IEnumerator FadeLineRenderers()
        {
            // If there is no fade time, then turn off line renderers instantly
            if (fadeTimeLineRenders <= 0)
            {
                foreach (var lr in lineRenderers)
                    lr.enabled = false;
                yield break;
            }

            var timer = fadeTimeLineRenders;
            // Fade the line renderers opacity
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                foreach (var lr in lineRenderers)
                {
                    var startColor = lr.startColor;
                    startColor.a = Mathf.Lerp(0, 1, timer / fadeTimeLineRenders);
                    lr.startColor = startColor;

                    var endColor = lr.endColor;
                    endColor.a = Mathf.Lerp(0, 1, timer / fadeTimeLineRenders);
                    lr.endColor = endColor;
                }

                yield return null;
            }
        }

        protected virtual IEnumerator FadeLights()
        {
            // If there is no fade time, then just turn off the lights instantly.
            if (fadeTimeLights <= 0)
            {
                foreach (var l in lights)
                    l.intensity = 0;
                yield break;
            }

            var startIntensity = new float[lights.Length];
            for (var i = 0; i < lights.Length; i++)
                startIntensity[i] = lights[i].intensity;

            var timer = fadeTimeLights;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                for (var i = 0; i < lights.Length; i++)
                    lights[i].intensity = Mathf.Lerp(0, startIntensity[i], timer / fadeTimeLights);
                yield return null;
            }
        }

        protected virtual void StopParticleSystemsEmission()
        {
            foreach (var ps in particleSystems)
            {
                // Stop emission
                var emission = ps.emission;
                emission.enabled = false;
            }
        }

        public void SetLineDistance(float hitInfoDistance)
        {
            foreach (var lr in lineRenderers)
                lr.SetPosition(1, new Vector3(0, 0, hitInfoDistance));
        }
    }
}