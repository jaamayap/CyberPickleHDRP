// File: Assets/_CyberPickle/Code/Gameplay/Player/PlayerAnimationDriver.cs
// Namespace: CyberPickle.Gameplay.Player
//
// Purpose: Pushes the player's current movement speed into the Animator
// so the controller can blend between Idle and locomotion states. Reads
// from Rigidbody.velocity (not PlayerMotor) so the animation reflects
// actual motion — including future cases like knockback / pushed-by-AOE
// where the player isn't generating their own movement.
//
// Why a separate component (not folded into PlayerMotor):
//   - Single responsibility: motor handles physical motion, this drives
//     the visual feedback layer
//   - When we add hit / death / special-ability animation states later,
//     they all hook into THIS component, not into the motor
//   - Easy to swap if we change animation systems (e.g., Animator -> Animancer)

using UnityEngine;

namespace CyberPickle.Gameplay.Player
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class PlayerAnimationDriver : MonoBehaviour
    {
        [Header("Animator Parameter")]
        [Tooltip("Name of the float parameter that drives Idle <-> Run (and future locomotion) transitions in the Animator Controller.")]
        [SerializeField] private string speedParameter = "Speed";

        [Tooltip("Damping time (seconds) applied to the Speed value sent to the Animator. Smooths the Idle <-> Run transition. Set to 0 for instant.")]
        [SerializeField] private float speedDampTime = 0.1f;

        private Animator animator;
        private Rigidbody rb;
        private int speedHash;
        private bool speedParameterExists;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            rb = GetComponent<Rigidbody>();
            // String-to-hash lookup once at Awake; cheaper than passing the string
            // every frame to SetFloat.
            speedHash = Animator.StringToHash(speedParameter);

            // Verify the Animator Controller actually has a float parameter
            // matching speedParameter. If not, Animator.SetFloat would spam
            // "Parameter 'Hash X' does not exist" once per Update — 60+/sec.
            // We log a single warning at Awake and short-circuit Update so the
            // character still animates via its idle state, just without
            // locomotion blending.
            speedParameterExists = HasFloatParameter(animator, speedHash);
            if (!speedParameterExists)
            {
                Debug.LogWarning(
                    $"[PlayerAnimationDriver] Animator on '{name}' has no float parameter '{speedParameter}'. " +
                    $"Locomotion blending disabled. Add a float parameter named '{speedParameter}' to the " +
                    $"Animator Controller, or change the [Speed Parameter] field on this component to match " +
                    $"whatever name the controller uses (e.g., 'Velocity', 'MoveSpeed').",
                    this);
            }
        }

        private void Update()
        {
            // Skip every frame if the controller has no matching parameter —
            // calling SetFloat on a missing parameter produces a warning per call.
            if (!speedParameterExists) return;

            // Use horizontal velocity only — vertical motion (gravity, jumps later)
            // shouldn't trigger the Run animation.
            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            float currentSpeed = vel.magnitude;

            if (speedDampTime > 0f)
            {
                // Animator handles the smoothing internally. dampTime is in seconds.
                animator.SetFloat(speedHash, currentSpeed, speedDampTime, Time.deltaTime);
            }
            else
            {
                animator.SetFloat(speedHash, currentSpeed);
            }
        }

        /// <summary>
        /// Returns true iff the Animator's runtime controller exposes a Float
        /// parameter whose nameHash matches the supplied hash. Safe against a
        /// null runtimeAnimatorController (e.g., prefab authored without one).
        /// </summary>
        private static bool HasFloatParameter(Animator anim, int hash)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return false;
            foreach (var p in anim.parameters)
            {
                if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Float)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
