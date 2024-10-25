using UnityEngine;

namespace CyberPickle.UI.Effects.Config
{
    [CreateAssetMenu(fileName = "GlitchTextConfig", menuName = "CyberPickle/UI/Effects/GlitchTextConfig")]
    public class GlitchTextConfig : ScriptableObject
    {
        [Header("Glitch Effect Settings")]
        [Range(0.1f, 2f)]
        public float glitchInterval = 0.95f;

        [Range(0f, 1f)]
        public float glitchIntensity;

        [Range(0f, 1f)]
        public float glitchIntensityMax;

        [Range(0f, 1f)]
        public float glitchIntensityMin;

        [Range(0f, 0.5f)]
        public float glitchAmplitude = 0.1f;

        [Range(0f, 10f)]
        public float glitchSpeed = 5f;

        [Header("Random Character Effect Settings")]
        [Range(0.1f, 2f)]
        public float randomCharIntervalMin = 0.5f;

        [Range(0.1f, 2f)]
        public float randomCharIntervalMax = 2f;

        [Range(0.01f, 0.5f)]
        public float randomCharDisplayDurationMin = 0.05f;

        [Range(0.1f, 1f)]
        public float randomCharDisplayDurationMax = 0.3f;

        [Range(0f, 1f)]
        public float randomCharIntensity = 0.5f;

        [Header("Fade-In Effect Settings")]
        [Range(0.1f, 5f)]
        public float fadeDuration = 2f;

        [Header("Pulse Animation Settings")]
        [Range(1f, 1.5f)]
        public float pulseScale = 1.1f;

        [Range(0.1f, 5f)]
        public float pulseSpeed = 1f;

        [Header("Scanline Effect Settings")]
        [Range(0f, 1f)]
        public float scanlineIntensity = 1f;

        [Range(0f, 10f)]
        public float scanlineSpeed = 4.1f;

        [Range(100f, 2000f)]
        public float scanlineFrequency = 800f;

        [Header("Chromatic Aberration Settings")]
        [Range(0f, 0.01f)]
        public float chromAberration = 0.002f;

        [Header("Color Glitch Effect Settings")]
        [Range(0f, 1f)]
        public float colorGlitchIntensity = 0.5f;

        [Range(0.1f, 2f)]
        public float colorGlitchDuration = 0.5f;

        [Header("Dynamic Glitch Intensity Settings")]
        [Range(0.1f, 5f)]
        public float glitchIntensityDuration = 2f;

        [Range(5f, 15f)]
        public float glitchIntensityInterval = 10f;

        [Range(0f, 0.05f)]
        public float rgbSplitAmount = 0.01f;
    }
}