using UnityEngine;
using TMPro;
using System.Collections;
using CyberPickle.UI.Effects.Config;

namespace CyberPickle.UI.Effects
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class GlitchTextEffect : MonoBehaviour
    {
        [SerializeField] private GlitchTextConfig config;

        private TextMeshProUGUI textMesh;
        private Material glitchMaterial;
        private string originalText;

        void Start()
        {
            if (config == null)
            {
                Debug.LogError("GlitchTextConfig not assigned to GlitchTextEffect on " + gameObject.name);
                return;
            }

            textMesh = GetComponent<TextMeshProUGUI>();
            glitchMaterial = textMesh.fontMaterial;
            originalText = textMesh.text;

            if (glitchMaterial.shader.name != "TextMeshPro/Distance Field Glitch")
            {
                Debug.LogWarning("Please assign the 'TextMeshPro/Distance Field Glitch with Scanlines and Chromatic Aberration' shader to the font material.");
            }

            UpdateShaderProperties();
            StartCoroutine(FadeInRoutine());
            StartCoroutine(GlitchTextRoutine());
            StartCoroutine(RandomCharRoutine());
            StartCoroutine(PulseRoutine());
            StartCoroutine(DynamicGlitchIntensityRoutine());
            StartCoroutine(CharacterColorGlitchRoutine());
        }

        void OnValidate()
        {
            if (glitchMaterial == null)
            {
                textMesh = GetComponent<TextMeshProUGUI>();
                if (textMesh != null)
                {
                    glitchMaterial = textMesh.fontMaterial;
                }
            }

            if (glitchMaterial != null && config != null)
            {
                UpdateShaderProperties();
            }
        }

        void UpdateShaderProperties()
        {
            glitchMaterial.SetFloat("_GlitchIntensity", config.glitchIntensity);
            glitchMaterial.SetFloat("_GlitchAmplitude", config.glitchAmplitude);
            glitchMaterial.SetFloat("_GlitchSpeed", config.glitchSpeed);
            glitchMaterial.SetFloat("_ScanlineIntensity", config.scanlineIntensity);
            glitchMaterial.SetFloat("_ScanlineSpeed", config.scanlineSpeed);
            glitchMaterial.SetFloat("_ScanlineFrequency", config.scanlineFrequency);
            glitchMaterial.SetFloat("_ChromAberration", config.chromAberration);
        }

        IEnumerator FadeInRoutine()
        {
            float elapsedTime = 0f;
            Color color = textMesh.color;
            color.a = 0f;
            textMesh.color = color;

            while (elapsedTime < config.fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                color.a = Mathf.Clamp01(elapsedTime / config.fadeDuration);
                textMesh.color = color;
                yield return null;
            }

            color.a = 1f;
            textMesh.color = color;
        }

        IEnumerator GlitchTextRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(config.glitchInterval);
                if (Random.value < config.glitchIntensity)
                {
                    UpdateShaderProperties();
                    yield return new WaitForSeconds(0.1f);
                    glitchMaterial.SetFloat("_RGBSplitAmount", 0f);
                    UpdateShaderProperties();
                }
            }
        }

        IEnumerator RandomCharRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(config.randomCharIntervalMin, config.randomCharIntervalMax));
                if (Random.value < config.randomCharIntensity)
                {
                    int charIndex = Random.Range(0, originalText.Length);
                    char randomChar = (char)Random.Range(33, 126);
                    string glitchedText = originalText.Substring(0, charIndex) + randomChar + originalText.Substring(charIndex + 1);
                    textMesh.text = glitchedText;
                    yield return new WaitForSeconds(Random.Range(config.randomCharDisplayDurationMin, config.randomCharDisplayDurationMax));
                    textMesh.text = originalText;
                }
            }
        }

        IEnumerator PulseRoutine()
        {
            Vector3 originalScale = textMesh.transform.localScale;
            while (true)
            {
                float scale = Mathf.Lerp(1f, config.pulseScale, Mathf.PingPong(Time.time * config.pulseSpeed, 1));
                textMesh.transform.localScale = originalScale * scale;
                yield return null;
            }
        }

        IEnumerator DynamicGlitchIntensityRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(0f, config.glitchIntensityInterval));
                config.glitchIntensity = Random.Range(config.glitchIntensityMin, config.glitchIntensityMax);
                UpdateShaderProperties();
                yield return new WaitForSeconds(config.glitchIntensityDuration);
                config.glitchIntensity = 0f;
                UpdateShaderProperties();
            }
        }

        IEnumerator CharacterColorGlitchRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(5f, 15f));
                if (Random.value < config.colorGlitchIntensity)
                {
                    int charIndex = Random.Range(0, originalText.Length);
                    Color originalColor = textMesh.color;
                    Color glitchColor = Random.value > 0.5f ? new Color(0.0f, 1.0f, 1.0f) : new Color(1.0f, 0.0f, 1.0f);

                    TMP_TextInfo textInfo = textMesh.textInfo;
                    textMesh.ForceMeshUpdate();
                    var charInfo = textInfo.characterInfo[charIndex];
                    if (charInfo.isVisible)
                    {
                        int meshIndex = charInfo.materialReferenceIndex;
                        int vertexIndex = charInfo.vertexIndex;
                        Color32[] newVertexColors = textInfo.meshInfo[meshIndex].colors32;
                        newVertexColors[vertexIndex + 0] = glitchColor;
                        newVertexColors[vertexIndex + 1] = glitchColor;
                        newVertexColors[vertexIndex + 2] = glitchColor;
                        newVertexColors[vertexIndex + 3] = glitchColor;
                        textMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
                    }
                    yield return new WaitForSeconds(config.colorGlitchDuration);

                    textMesh.ForceMeshUpdate();
                    var charInfoRestore = textInfo.characterInfo[charIndex];
                    if (charInfoRestore.isVisible)
                    {
                        int meshIndex = charInfoRestore.materialReferenceIndex;
                        int vertexIndex = charInfoRestore.vertexIndex;
                        Color32[] newVertexColors = textInfo.meshInfo[meshIndex].colors32;
                        newVertexColors[vertexIndex + 0] = originalColor;
                        newVertexColors[vertexIndex + 1] = originalColor;
                        newVertexColors[vertexIndex + 2] = originalColor;
                        newVertexColors[vertexIndex + 3] = originalColor;
                        textMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
                    }
                }
            }
        }
    }
}
