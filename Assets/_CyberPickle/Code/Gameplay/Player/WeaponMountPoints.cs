// File: Assets/_CyberPickle/Code/Gameplay/Player/WeaponMountPoints.cs
// Namespace: CyberPickle.Gameplay.Player
//
// Purpose: Typed bag of Transform references on a player character that
// declare WHERE equipped items attach. Pure data — no logic. The
// PlayerLoadoutLoader reads from this and instantiates equipment prefabs
// as children of the appropriate slot.
//
// Per GDD §2.5 / §4.2: 2 hand weapons + 1 body weapon. Power-ups, armor,
// and amulet may be added as additional slots later when those have
// visible 3D representations (currently they're stat-only).

using UnityEngine;

namespace CyberPickle.Gameplay.Player
{
    [DisallowMultipleComponent]
    public class WeaponMountPoints : MonoBehaviour
    {
        [Header("Hand Weapons (max 2 per GDD)")]
        [Tooltip("Right-hand weapon mount. Hand-weapon slot 0 spawns here.")]
        [SerializeField] private Transform handR;

        [Tooltip("Left-hand weapon mount. Hand-weapon slot 1 spawns here.")]
        [SerializeField] private Transform handL;

        [Header("Body Weapon (max 1 per GDD)")]
        [Tooltip("Body / back-mount weapon attachment. Body-weapon slot spawns here.")]
        [SerializeField] private Transform body;

        public Transform HandR => handR;
        public Transform HandL => handL;
        public Transform Body  => body;

        /// <summary>
        /// Returns the appropriate mount for a hand-weapon slot index (0 = right, 1 = left).
        /// Returns null for indices outside [0, 1] or if the corresponding mount isn't assigned.
        /// </summary>
        public Transform GetHandMount(int slotIndex)
        {
            return slotIndex switch
            {
                0 => handR,
                1 => handL,
                _ => null
            };
        }
    }
}
