// File: Assets/_CyberPickle/Code/DOTS/Visual/ZombieAnimDriver.cs
// Namespace: CyberPickle.DOTS.Visual
//
// Lives on the hybrid GameObject visual prefab (Zombie1_Visual.prefab).
// Reads its own per-frame transform delta and writes a "Speed" float
// to the Animator so the controller can blend Idle <-> Run.
//
// Why per-frame transform delta and not a velocity component on the
// entity: the visual stays decoupled from ECS. Drop the prefab into
// any scene without the bridge and it still drives the animator off
// its own motion — useful for editor previews and isolated debugging.
//
// Smoothing is exponential and frame-rate independent.

using UnityEngine;

namespace CyberPickle.DOTS.Visual
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class ZombieAnimDriver : MonoBehaviour
    {
        [Tooltip("Animator parameter name driven by the visual's observed world-space speed.")]
        public string speedParameter = "Speed";

        [Tooltip("Smoothing applied to the speed value (0 = no smoothing, larger = smoother / slower response).")]
        [Min(0f)] public float speedSmoothing = 8f;

        [Tooltip("Speeds below this threshold are clamped to zero — avoids tiny jitter from sub-pixel transform sync.")]
        [Min(0f)] public float speedDeadzone = 0.05f;

        [Tooltip("Randomize the animator's playback phase on first enable so a swarm doesn't move in lockstep. Disable for boss-quality unique characters where deterministic playback matters.")]
        public bool randomizePhaseOnSpawn = true;

        private Animator animator;
        private Vector3 lastPosition;
        private float smoothedSpeed;
        private int speedHash;
        private bool phaseRandomized;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            speedHash = Animator.StringToHash(speedParameter);
            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            lastPosition = transform.position;
            smoothedSpeed = 0f;

            if (randomizePhaseOnSpawn && !phaseRandomized)
            {
                // Re-play the current state in each layer at a random normalized
                // time. Zombies that share the same animation no longer sync
                // their footfalls, hand swings, head bobs, etc.
                for (int i = 0; i < animator.layerCount; i++)
                {
                    var info = animator.GetCurrentAnimatorStateInfo(i);
                    animator.Play(info.fullPathHash, i, Random.value);
                }
                phaseRandomized = true;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 current = transform.position;
            Vector3 delta = current - lastPosition;
            delta.y = 0f; // ignore vertical drift
            lastPosition = current;

            float instantSpeed = delta.magnitude / dt;
            if (instantSpeed < speedDeadzone) instantSpeed = 0f;

            // Frame-rate-independent exponential smoothing.
            float t = speedSmoothing > 0f ? 1f - Mathf.Exp(-speedSmoothing * dt) : 1f;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, instantSpeed, t);

            animator.SetFloat(speedHash, smoothedSpeed);
        }
    }
}
