// File: Assets/_CyberPickle/Code/Gameplay/Player/WeaponMountPoints.cs
// Namespace: CyberPickle.Gameplay.Player
//
// Typed bag of Transform references on a player character that declare
// WHERE equipped items attach. Pure data — no logic.
//
// 2026-05-11 (M9 PR B): migrated from the old 3-mount model (HandR / HandL /
// Body) to a 4-mount cross-axis model matching the loadout's N/E/S/W axes:
//
//     Axis 0 = North = FRONT  (in front of the player)
//     Axis 1 = East  = RIGHT  (player's right side)
//     Axis 2 = South = BACK   (behind the player)
//     Axis 3 = West  = LEFT   (player's left side)
//
// The old HandR / HandL / Body fields are retained for back-compat with
// any scene still wired to the 3-mount model — GetMountForAxis falls
// back through them if the new 4-mount fields aren't all assigned.

using UnityEngine;

namespace CyberPickle.Gameplay.Player
{
    [DisallowMultipleComponent]
    public class WeaponMountPoints : MonoBehaviour
    {
        [Header("Cross-Axis Mounts (M9 — match the loadout cross)")]
        [Tooltip("Front mount (axis 0 = N). The weapon in the North loadout axis spawns here.")]
        [SerializeField] private Transform front;

        [Tooltip("Right mount (axis 1 = E). The weapon in the East loadout axis spawns here.")]
        [SerializeField] private Transform right;

        [Tooltip("Back mount (axis 2 = S). The weapon in the South loadout axis spawns here.")]
        [SerializeField] private Transform back;

        [Tooltip("Left mount (axis 3 = W). The weapon in the West loadout axis spawns here.")]
        [SerializeField] private Transform left;

        [Header("LEGACY — pre-M9 3-mount model (back-compat only)")]
        [Tooltip("LEGACY — pre-M9 right-hand weapon mount. Used as a fallback for axis 1 (Right) if the new 'right' mount isn't assigned. New scenes should leave this empty.")]
        [SerializeField] private Transform handR;

        [Tooltip("LEGACY — pre-M9 left-hand weapon mount. Fallback for axis 3 (Left). New scenes should leave this empty.")]
        [SerializeField] private Transform handL;

        [Tooltip("LEGACY — pre-M9 body-mount weapon. Fallback for axis 2 (Back). New scenes should leave this empty.")]
        [SerializeField] private Transform body;

        // ─── New cross-axis API ───────────────────────────────────────────

        public Transform Front => front;
        public Transform Right => right;
        public Transform Back  => back;
        public Transform Left  => left;

        /// <summary>
        /// Returns the mount Transform for a loadout axis index.
        /// Axis 0 → Front, 1 → Right, 2 → Back, 3 → Left.
        /// Falls back to the legacy HandR / HandL / Body mounts if the
        /// matching new field isn't assigned (returns null if neither
        /// resolves).
        /// </summary>
        public Transform GetMountForAxis(int axisIndex)
        {
            switch (axisIndex)
            {
                case 0: return front ?? handR; // N — first preference Front, fallback to old right hand
                case 1: return right ?? handL; // E — first preference Right, fallback to old left hand
                case 2: return back  ?? body;  // S — first preference Back, fallback to body
                case 3: return left;           // W — no legacy fallback (it's the 4th slot, new)
                default: return null;
            }
        }

        // ─── Legacy API (kept for back-compat) ────────────────────────────

        public Transform HandR => handR;
        public Transform HandL => handL;
        public Transform Body  => body;

        /// <summary>
        /// LEGACY — pre-M9 lookup by hand slot index (0 = right, 1 = left).
        /// New code should use <see cref="GetMountForAxis"/> instead.
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
