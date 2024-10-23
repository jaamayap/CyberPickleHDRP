using System;
using System.Collections;
using UnityEngine;

// Turn the lights on/off when the projectile is launched.

namespace MagicPigGames.ProjectileFactory
{
    [Documentation("This script turns the lights on/off when the projectile is launched.")]
    [Serializable]
    public class ToggleLightsWithProjectileLaunch : MonoBehaviour
    {
        public Projectile projectile;

        [Header("Options")] public float fadeTime = 0.2f;

        private bool _isOn;

        private Coroutine _lightingCoroutine;
        private float[] _lightInitialIntensity;

        private Light[] _lights;

        private void Update()
        {
            if (!_isOn && projectile.Launched)
                TurnOnLight();
            else if (_isOn && !projectile.Launched)
                TurnOffLight();
        }

        private void OnEnable()
        {
            _lights = GetComponentsInChildren<Light>();
            _lightInitialIntensity = new float[_lights.Length];
            for (var i = 0; i < _lights.Length; i++)
                _lightInitialIntensity[i] = _lights[i].intensity;

            if (projectile.Launched == false)
                TurnOffLight(true);
            else
                TurnOnLight();
        }

        private void TurnOnLight()
        {
            _isOn = true;

            // If the light is already in the process of turning on/off, stop that process
            if (_lightingCoroutine != null) StopCoroutine(_lightingCoroutine);

            // Start the process of turning on the light
            _lightingCoroutine = StartCoroutine(FadeLightOverTime(true));
        }

        private void TurnOffLight(bool instant = false)
        {
            _isOn = false;

            if (instant)
            {
                TurnOffLightsInstantly();
                return;
            }

            if (_lightingCoroutine != null)
                StopCoroutine(_lightingCoroutine);

            _lightingCoroutine = StartCoroutine(FadeLightOverTime(false));
        }

        private void TurnOffLightsInstantly()
        {
            foreach (var lightObject in _lights)
                lightObject.intensity = 0;
            if (_lightingCoroutine != null)
                StopCoroutine(_lightingCoroutine);
        }

        private IEnumerator FadeLightOverTime(bool turnOn)
        {
            for (float t = 0; t < fadeTime; t += Time.deltaTime)
            {
                for (var i = 0; i < _lights.Length; i++)
                    _lights[i].intensity =
                        Mathf.Lerp(_lightInitialIntensity[i], TargetIntensity(turnOn, i), t / fadeTime);

                yield return null;
            }

            for (var i = 0; i < _lights.Length; i++)
                _lights[i].intensity = TargetIntensity(turnOn, i);
        }

        protected virtual float TargetIntensity(bool turnOn, int i)
        {
            return turnOn ? _lightInitialIntensity[i] : 0;
        }
    }
}