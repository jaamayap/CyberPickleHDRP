using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Documentation("This will destroy an object now, or in time, specifically using the object pool only if " +
                   "the projectile is using the object pool, otherwise a traditional destroy. It has the option to " +
                   "disable specific objects more quickly, and reenable them when the projectile is re-enabled " +
                   "(assumed from an object pool).\n\nIt will also fade out various components that are on this object and, if " +
                   "selected, its children.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/additional-scripts/destroy-or-pool-object")]
    public class DestroyOrPoolObjectWithFade : DestroyOrPoolObject
    {
        [Header("Fade Options")] public float fadeTime = 1f;

        public bool fadeAudio = true;
        public bool fadeParticles = true;
        public bool fadeLights = true;
        public bool fadeTrailRenderers = true;
        public bool fadeLineRenderers = true;
        public bool includeChildrenOfThisObject = true;
        public bool fadeMeshParticles; // REQUIRED A SHADER THAT SUPPORTS TRANSPARENCY
        public bool shrinkEntireParticleObject;
        protected List<AudioSource> _audioSources = new();

        protected List<float> _audioSourceVolumes = new();

        protected Coroutine _fadeCoroutine;
        protected bool _isSetup;
        protected List<float> _lightIntensities = new();
        protected List<Light> _lights = new();
        protected List<LineRenderer> _lineRenderers = new();
        protected List<float> _lineRendererWidths = new();
        protected List<float> _particleSystemAlphas = new();
        protected List<ParticleSystemRenderer> _particleSystemRenderers = new();
        protected List<bool> _isGradient = new List<bool>();
        protected List<Gradient> _particleSystemGradients = new List<Gradient>();

        protected List<ParticleSystem> _particleSystems = new();
        protected List<TrailRenderer> _trailRenderers = new();
        protected List<float> _trailRendererWidths = new();

        protected override void OnEnable()
        {
            base.OnEnable();
            Setup();
            _fadeCoroutine = StartCoroutine(Fade());
        }

        protected virtual void OnDisable()
        {
            StopTheCoroutine();
        }

        protected virtual void OnValidate()
        {
            if (timeToDestroy < fadeTime)
                timeToDestroy = fadeTime;
        }

        private void StopTheCoroutine()
        {
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);
        }

        protected virtual IEnumerator Fade()
        {
            // return until timeToDestroy - fadeTime is elapsed
            var timeToWait = timeToDestroy - fadeTime;
            yield return new WaitForSeconds(timeToWait);

            var time = 0f;
            while (time < fadeTime)
            {
                time += Time.deltaTime;
                var t = time / fadeTime;
                FadeObjects(t);
                yield return null;
            }
        }

        private ParticleSystem.Particle[] particles = Array.Empty<ParticleSystem.Particle>();
        
        private void FadeObjects(float time)
        {
            for (var i = 0; i < _audioSources.Count; i++)
                _audioSources[i].volume = Mathf.Lerp(_audioSourceVolumes[i], 0, time);

            //var particles = Array.Empty<ParticleSystem.Particle>();

            for (var i = 0; i < _particleSystems.Count; i++)
            {
                var main = _particleSystems[i].main;

                if (!_isGradient[i])
                {
                    // Handle single color fading
                    var startColor = new ParticleSystem.MinMaxGradient(main.startColor.color);
                    var tempColor = startColor.color;
                    tempColor.a = Mathf.Lerp(_particleSystemAlphas[i], 0, time);
                    tempColor.a = Mathf.Clamp01(tempColor.a); // Clamp alpha between 0 and 1
                    startColor.color = tempColor;
                    main.startColor = startColor;
                    
                    // Fade individual particles
                    FadeParticleAlpha(_particleSystems[i], tempColor.a);
                }
                else
                {
                    // Handle gradient fading
                    var gradient = _particleSystemGradients[i];
                    var fadedGradient = new Gradient();
                    var colorKeys = gradient.colorKeys;
                    var alphaKeys = gradient.alphaKeys;

                    for (var j = 0; j < alphaKeys.Length; j++)
                    {
                        alphaKeys[j].alpha = Mathf.Lerp(alphaKeys[j].alpha, 0, time);
                    }

                    fadedGradient.SetKeys(colorKeys, alphaKeys);
                    main.startColor = new ParticleSystem.MinMaxGradient(fadedGradient);
                    
                    // Fade individual particles
                    FadeParticleAlpha(_particleSystems[i], Mathf.Lerp(1.0f, 0, time));
                }

                // Handle mesh particle fading and shrinking
                if (_particleSystemRenderers[i] != null &&
                    _particleSystemRenderers[i].renderMode == ParticleSystemRenderMode.Mesh)
                {
                    if (fadeMeshParticles)
                    {
                        var material = _particleSystemRenderers[i].material;
                        var color = material.color;
                        color.a = Mathf.Lerp(_particleSystemAlphas[i], 0, time);
                        material.color = color;
                    }

                    if (shrinkEntireParticleObject)
                    {
                        var scale = _particleSystemRenderers[i].transform.localScale;
                        scale = Vector3.Lerp(scale, Vector3.zero, time);

                        if (scale.magnitude > 0.01f)
                            _particleSystemRenderers[i].transform.localScale = scale;
                        else
                            _particleSystemRenderers[i].transform.localScale = Vector3.one * 0.01f;
                    }
                }
            }

            for (var i = 0; i < _lights.Count; i++)
                _lights[i].intensity = Mathf.Lerp(_lightIntensities[i], 0, time);

            for (var i = 0; i < _trailRenderers.Count; i++)
                _trailRenderers[i].widthMultiplier = Mathf.Lerp(_trailRendererWidths[i], 0, time);

            for (var i = 0; i < _lineRenderers.Count; i++)
                _lineRenderers[i].startWidth = Mathf.Lerp(_lineRendererWidths[i], 0, time);
        }
        
        private void FadeParticleAlpha(ParticleSystem particleSystem, float targetAlpha)
        {
            if (particles.Length < particleSystem.particleCount)
                particles = new ParticleSystem.Particle[particleSystem.particleCount];

            var fetchedParticles = particleSystem.GetParticles(particles);
            var byteAlpha = (byte)(targetAlpha * 255); // Convert float to byte

            for (var j = 0; j < fetchedParticles; j++)
            {
                var color = particles[j].startColor;
                color.a = byteAlpha; // set byte alpha
                particles[j].startColor = color;
            }

            particleSystem.SetParticles(particles, fetchedParticles);
        }
        
        protected virtual void Setup()
        {
            if (_isSetup)
            {
                TurnBackToCachedValues();
                return;
            }

            _isSetup = true;
            FindObjects();
            CacheObjectValues();
        }

        private void TurnBackToCachedValues()
        {
            for (var i = 0; i < _audioSources.Count; i++)
                _audioSources[i].volume = _audioSourceVolumes[i];

            for (var i = 0; i < _particleSystems.Count; i++)
            {
                var main = _particleSystems[i].main;

                if (!_isGradient[i])
                {
                    // Restore single color
                    var startColor = main.startColor.color;
                    startColor.a = _particleSystemAlphas[i];
                    main.startColor = startColor;
                }
                else
                {
                    // Restore gradient
                    var gradient = _particleSystemGradients[i];
                    main.startColor = new ParticleSystem.MinMaxGradient(gradient);
                }

                if (_particleSystemRenderers[i] != null &&
                    _particleSystemRenderers[i].renderMode == ParticleSystemRenderMode.Mesh)
                {
                    _particleSystemRenderers[i].transform.localScale = Vector3.one;
                }
            }

            for (var i = 0; i < _lights.Count; i++)
                _lights[i].intensity = _lightIntensities[i];

            for (var i = 0; i < _trailRenderers.Count; i++)
                _trailRenderers[i].widthMultiplier = _trailRendererWidths[i];

            for (var i = 0; i < _lineRenderers.Count; i++)
                _lineRenderers[i].startWidth = _lineRendererWidths[i];
        }

        protected virtual void CacheObjectValues()
        {
            foreach (var audioSource in _audioSources)
                _audioSourceVolumes.Add(audioSource.volume);

            foreach (var ps in _particleSystems)
            {
                var main = ps.main;
        
                if (main.startColor.mode == ParticleSystemGradientMode.Color)
                {
                    var color = main.startColor.color;
                    _particleSystemAlphas.Add(color.a);
                    _isGradient.Add(false);
                    _particleSystemGradients.Add(null); // No gradient for single color
                }
                else if (main.startColor.mode == ParticleSystemGradientMode.Gradient)
                {
                    var gradient = main.startColor.gradient;
                    _isGradient.Add(true);
                    _particleSystemGradients.Add(gradient); // Store the entire gradient
                }

                _particleSystemRenderers.Add(ps.GetComponent<ParticleSystemRenderer>());
            }

            foreach (var l in _lights)
                _lightIntensities.Add(l.intensity);

            foreach (var trailRenderer in _trailRenderers)
                _trailRendererWidths.Add(trailRenderer.widthMultiplier);

            foreach (var lineRenderer in _lineRenderers)
                _lineRendererWidths.Add(lineRenderer.startWidth);
        }

        protected virtual void FindObjects()
        {
            if (includeChildrenOfThisObject)
            {
                if (fadeAudio)
                    _audioSources.AddRange(GetComponentsInChildren<AudioSource>());
                if (fadeParticles)
                    _particleSystems.AddRange(GetComponentsInChildren<ParticleSystem>());
                if (fadeTrailRenderers)
                    _trailRenderers.AddRange(GetComponentsInChildren<TrailRenderer>());
                if (fadeLights)
                    _lights.AddRange(GetComponentsInChildren<Light>());
                if (fadeLineRenderers)
                    _lineRenderers.AddRange(GetComponentsInChildren<LineRenderer>());
            }
            else
            {
                if (fadeAudio)
                    _audioSources.AddRange(GetComponents<AudioSource>());
                if (fadeParticles)
                    _particleSystems.AddRange(GetComponents<ParticleSystem>());
                if (fadeTrailRenderers)
                    _trailRenderers.AddRange(GetComponents<TrailRenderer>());
                if (fadeLights)
                    _lights.AddRange(GetComponents<Light>());
                if (fadeLineRenderers)
                    _lineRenderers.AddRange(GetComponents<LineRenderer>());
            }
        }
    }
}