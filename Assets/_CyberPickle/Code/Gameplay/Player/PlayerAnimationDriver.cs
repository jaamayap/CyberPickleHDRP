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

        private void Awake()
        {
            animator = GetComponent<Animator>();
            rb = GetComponent<Rigidbody>();
            // String-to-hash lookup once at Awake; cheaper than passing the string
            // every frame to SetFloat.
            speedHash = Animator.StringToHash(speedParameter);
        }

        private void Update()
        {
            // Use horizontal velocity only — vertical motion (gravity, jumps later)
            // shouldn't trigger the Run animation.
            Vector3 vel = rb.velocity;
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
    }
}
