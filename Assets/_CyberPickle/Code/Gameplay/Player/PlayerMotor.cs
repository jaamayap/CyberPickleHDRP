// File: Assets/_CyberPickle/Code/Gameplay/Player/PlayerMotor.cs
// Namespace: CyberPickle.Gameplay.Player
//
// Purpose: Top-down smooth-analog character movement on a Rigidbody.
// Reads input from a sibling PlayerInput (in Update — zero-frame lag),
// applies velocity in FixedUpdate (physics-correct), and uses Rigidbody
// interpolation so the rendered position is smooth between physics steps.
// Movement is camera-relative so the angled HDRP camera's "up" matches
// the player's intuition.

using UnityEngine;

namespace CyberPickle.Gameplay.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInput))]
    [DisallowMultipleComponent]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Speeds")]
        [Tooltip("Maximum movement speed in units/second when input is fully held.")]
        [SerializeField] private float maxSpeed = 6f;

        [Tooltip("Acceleration in units/second² while input is held. Higher = snappier.")]
        [SerializeField] private float acceleration = 40f;

        [Tooltip("Deceleration in units/second² when input is released. Higher = stops sooner.")]
        [SerializeField] private float deceleration = 50f;

        [Header("Camera")]
        [Tooltip("If true, input is interpreted relative to the camera's yaw — recommended for the angled top-down view.")]
        [SerializeField] private bool useCameraRelativeMovement = true;

        [Tooltip("Optional explicit camera reference. Falls back to Camera.main if null.")]
        [SerializeField] private Camera referenceCamera;

        [Header("Rotation")]
        [Tooltip("If true, the character rotates to face the direction it's moving.")]
        [SerializeField] private bool rotateTowardMovement = true;

        [Tooltip("How fast (degrees/second) the character can turn to face movement direction. 720° = 2 full turns/sec — very snappy. Lower for slower turns.")]
        [SerializeField] private float rotationSpeed = 720f;

        [Tooltip("Minimum velocity magnitude required before applying rotation. Prevents the character snapping rotation when nearly stopped.")]
        [SerializeField] private float rotationVelocityThreshold = 0.2f;

        private Rigidbody rb;
        private PlayerInput input;
        private Vector3 cachedTargetVelocity;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            input = GetComponent<PlayerInput>();

            // Sensible Rigidbody defaults for a top-down character controller.
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            // CRITICAL for low-lag feel: smooths the rendered position between
            // physics steps so visual movement looks frame-rate-locked even
            // though physics ticks at the FixedUpdate rate.
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        private void Update()
        {
            // Compute the world-space target velocity here in Update so it
            // reflects the latest input (PlayerInput polls input in its Update too).
            // We cache the result; FixedUpdate consumes it.
            Vector2 stickInput = input.MoveInput;
            Vector3 worldDir;

            if (useCameraRelativeMovement)
            {
                Camera cam = referenceCamera != null ? referenceCamera : Camera.main;
                if (cam != null)
                {
                    // Project camera forward/right onto the XZ plane so camera tilt
                    // doesn't affect movement direction (e.g., a 60° angled camera
                    // shouldn't make "forward" a partial-down).
                    Vector3 camForward = cam.transform.forward;
                    Vector3 camRight = cam.transform.right;
                    camForward.y = 0f;
                    camRight.y = 0f;
                    camForward.Normalize();
                    camRight.Normalize();
                    worldDir = camRight * stickInput.x + camForward * stickInput.y;
                }
                else
                {
                    // Fallback: world-aligned input.
                    worldDir = new Vector3(stickInput.x, 0f, stickInput.y);
                }
            }
            else
            {
                worldDir = new Vector3(stickInput.x, 0f, stickInput.y);
            }

            // Clamp so diagonal isn't 1.41× faster than axis-aligned.
            if (worldDir.sqrMagnitude > 1f) worldDir.Normalize();

            cachedTargetVelocity = worldDir * maxSpeed;
        }

        private void FixedUpdate()
        {
            // Smooth analog: accelerate toward target velocity when input is held,
            // decelerate to zero when input is released.
            float lerpRate = (cachedTargetVelocity.sqrMagnitude > 0.0001f) ? acceleration : deceleration;

            // Preserve vertical velocity (gravity, jumps, etc.) — only blend horizontal.
            Vector3 currentVel = rb.velocity;
            float verticalVel = currentVel.y;
            currentVel.y = 0f;

            Vector3 newHorizontal = Vector3.MoveTowards(currentVel, cachedTargetVelocity, lerpRate * Time.fixedDeltaTime);

            rb.velocity = new Vector3(newHorizontal.x, verticalVel, newHorizontal.z);

            // Rotate the character to face the direction of motion. Done via
            // Rigidbody.MoveRotation so it plays nicely with physics (and with
            // the FreezeRotationX/Z constraints set in Awake — only Y rotates).
            if (rotateTowardMovement && newHorizontal.sqrMagnitude > rotationVelocityThreshold * rotationVelocityThreshold)
            {
                Quaternion targetRot = Quaternion.LookRotation(newHorizontal, Vector3.up);
                Quaternion newRot = Quaternion.RotateTowards(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
                rb.MoveRotation(newRot);
            }
        }
    }
}
