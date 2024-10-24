using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(TextMeshProUGUI))]
public class GlitchTextEffect : MonoBehaviour
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

    private TextMeshProUGUI textMesh;
    private Material glitchMaterial;
    [Range(0f, 0.05f)]
    public float rgbSplitAmount = 0.01f;
    private string originalText;

    void Start()
    {
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

    // This method is called when the script is loaded or a value is changed in the inspector
    void OnValidate()
    {
        // Ensure that the glitchMaterial is not null before updating properties
        if (glitchMaterial == null)
        {
            textMesh = GetComponent<TextMeshProUGUI>();
            if (textMesh != null)
            {
                glitchMaterial = textMesh.fontMaterial;
            }
        }

        if (glitchMaterial != null)
        {
            UpdateShaderProperties();
        }
    }

    void UpdateShaderProperties()
    {
        // Glitch effect properties
        glitchMaterial.SetFloat("_GlitchIntensity", glitchIntensity);
        glitchMaterial.SetFloat("_GlitchAmplitude", glitchAmplitude);
        glitchMaterial.SetFloat("_GlitchSpeed", glitchSpeed);

        // Scanline effect properties
        glitchMaterial.SetFloat("_ScanlineIntensity", scanlineIntensity);
        glitchMaterial.SetFloat("_ScanlineSpeed", scanlineSpeed);
        glitchMaterial.SetFloat("_ScanlineFrequency", scanlineFrequency);

        // Chromatic aberration property
        glitchMaterial.SetFloat("_ChromAberration", chromAberration);
    }

    IEnumerator FadeInRoutine()
    {
        float elapsedTime = 0f;
        Color color = textMesh.color;
        color.a = 0f;
        textMesh.color = color;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Clamp01(elapsedTime / fadeDuration);
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
            yield return new WaitForSeconds(glitchInterval);
            if (Random.value < glitchIntensity)
            {
                // Removed RGB Split effect as it was not defined properly.
                UpdateShaderProperties();
                yield return new WaitForSeconds(0.1f); // Duration for the glitch peak
                glitchMaterial.SetFloat("_RGBSplitAmount", 0f); // Reset after glitch
                UpdateShaderProperties();
            }
        }
    }

    IEnumerator RandomCharRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(randomCharIntervalMin, randomCharIntervalMax));
            if (Random.value < randomCharIntensity)
            {
                int charIndex = Random.Range(0, originalText.Length);
                char randomChar = (char)Random.Range(33, 126); // Random ASCII character between '!' and '~'
                string glitchedText = originalText.Substring(0, charIndex) + randomChar + originalText.Substring(charIndex + 1);
                textMesh.text = glitchedText;
                yield return new WaitForSeconds(Random.Range(randomCharDisplayDurationMin, randomCharDisplayDurationMax));
                textMesh.text = originalText;
            }
        }
    }

    IEnumerator PulseRoutine()
    {
        Vector3 originalScale = textMesh.transform.localScale;
        while (true)
        {
            float scale = Mathf.Lerp(1f, pulseScale, Mathf.PingPong(Time.time * pulseSpeed, 1));
            textMesh.transform.localScale = originalScale * scale;
            yield return null;
        }
    }

    IEnumerator DynamicGlitchIntensityRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(0f, glitchIntensityInterval));
            glitchIntensity = Random.Range(glitchIntensityMin, glitchIntensityMax);
            UpdateShaderProperties();
            yield return new WaitForSeconds(glitchIntensityDuration);
            glitchIntensity = 0f;
            UpdateShaderProperties();
        }
    }

    IEnumerator CharacterColorGlitchRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(5f, 15f));
            if (Random.value < colorGlitchIntensity)
            {
                int charIndex = Random.Range(0, originalText.Length);
                Color originalColor = textMesh.color;
                Color glitchColor = Random.value > 0.5f ? new Color(0.0f, 1.0f, 1.0f) : new Color(1.0f, 0.0f, 1.0f); // Neon cyan or neon purple

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
                yield return new WaitForSeconds(colorGlitchDuration);

                // Restore the original color
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
