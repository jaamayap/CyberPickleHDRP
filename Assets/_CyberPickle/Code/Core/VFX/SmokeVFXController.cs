using UnityEngine;
using UnityEngine.VFX;

namespace CyberPickle.Core.VFX
{
    [RequireComponent(typeof(VisualEffect))]
    public class SmokeVFXController : MonoBehaviour
    {
        private VisualEffect vfx;

        // These names must match exactly with the parameters in your Visual Effect Graph
        private const string SPAWN_RATE_PARAM = "SpawnRate";
        private const string SMOKE_DENSITY_PARAM = "SmokeDensity";
        private const string SMOKE_SIZE_PARAM = "SmokeSize";

        [Header("Debug")]
        [SerializeField] private bool showDebugLog = true;

        private void Awake()
        {
            vfx = GetComponent<VisualEffect>();
            ValidateParameters();
        }

        private void ValidateParameters()
        {
            if (vfx == null)
            {
                Debug.LogError("[SmokeVFXController] No VisualEffect component found!");
                return;
            }

            // Check if parameters exist
            bool hasSpawnRate = HasParameter(SPAWN_RATE_PARAM);
            bool hasSmokeDensity = HasParameter(SMOKE_DENSITY_PARAM);
            bool hasSmokeSize = HasParameter(SMOKE_SIZE_PARAM);

            if (showDebugLog)
            {
                Debug.Log($"[SmokeVFXController] Parameter check:\n" +
                    $"SpawnRate: {(hasSpawnRate ? "Found" : "Missing")}\n" +
                    $"SmokeDensity: {(hasSmokeDensity ? "Found" : "Missing")}\n" +
                    $"SmokeSize: {(hasSmokeSize ? "Found" : "Missing")}");
            }

            // Log warning for any missing parameters
            if (!hasSpawnRate) Debug.LogWarning($"[SmokeVFXController] Missing parameter: {SPAWN_RATE_PARAM}");
            if (!hasSmokeDensity) Debug.LogWarning($"[SmokeVFXController] Missing parameter: {SMOKE_DENSITY_PARAM}");
            if (!hasSmokeSize) Debug.LogWarning($"[SmokeVFXController] Missing parameter: {SMOKE_SIZE_PARAM}");
        }

        private bool HasParameter(string name)
        {
            return vfx.HasFloat(name);
        }

        public void SetSmokeDensity(float density)
        {
            if (vfx != null && HasParameter(SMOKE_DENSITY_PARAM))
            {
                vfx.SetFloat(SMOKE_DENSITY_PARAM, density);
                if (showDebugLog) Debug.Log($"[SmokeVFXController] Set SmokeDensity to {density}");
            }
        }

        public void SetSpawnRate(float rate)
        {
            if (vfx != null && HasParameter(SPAWN_RATE_PARAM))
            {
                vfx.SetFloat(SPAWN_RATE_PARAM, rate);
                if (showDebugLog) Debug.Log($"[SmokeVFXController] Set SpawnRate to {rate}");
            }
        }

        public void SetSmokeSize(float size)
        {
            if (vfx != null && HasParameter(SMOKE_SIZE_PARAM))
            {
                vfx.SetFloat(SMOKE_SIZE_PARAM, size);
                if (showDebugLog) Debug.Log($"[SmokeVFXController] Set SmokeSize to {size}");
            }
        }
    }
}
