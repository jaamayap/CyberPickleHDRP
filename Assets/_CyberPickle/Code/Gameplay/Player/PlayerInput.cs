// File: Assets/_CyberPickle/Code/Gameplay/Player/PlayerInput.cs
// Namespace: CyberPickle.Gameplay.Player
//
// Purpose: Per-entity input source. Owns a PlayerControls instance,
// polls continuous movement input each Update for zero-frame-lag, and
// exposes a C# event for the special ability button press. Other player
// components (PlayerMotor, etc.) read from this — never directly from
// the Input System — so the input source can be swapped later (AI,
// replay, network) without touching consumers.

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CyberPickle.Gameplay.Player
{
    [DisallowMultipleComponent]
    public class PlayerInput : MonoBehaviour
    {
        /// <summary>
        /// Continuous movement input, normalized roughly to [-1..1] per axis
        /// (raw stick values; not yet normalized to unit length). Read this
        /// every frame from a consumer's Update or FixedUpdate.
        /// </summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>
        /// Fires once when the special ability button transitions to pressed.
        /// </summary>
        public event Action SpecialAbility;

        private PlayerControls controls;

        private void Awake()
        {
            controls = new PlayerControls();
        }

        private void OnEnable()
        {
            controls.Player.Enable();
            controls.Player.SpecialAbility.performed += HandleSpecialAbilityPerformed;
        }

        private void OnDisable()
        {
            controls.Player.SpecialAbility.performed -= HandleSpecialAbilityPerformed;
            controls.Player.Disable();
        }

        private void OnDestroy()
        {
            controls?.Dispose();
        }

        private void Update()
        {
            // Poll the continuous Move action every frame for zero-frame lag.
            // Held-key input gets read on the same frame the user pressed the key.
            MoveInput = controls.Player.Move.ReadValue<Vector2>();
        }

        private void HandleSpecialAbilityPerformed(InputAction.CallbackContext ctx)
        {
            SpecialAbility?.Invoke();
        }
    }
}
