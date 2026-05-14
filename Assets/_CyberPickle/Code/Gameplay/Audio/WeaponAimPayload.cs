// File: Assets/_CyberPickle/Code/Gameplay/Audio/WeaponAimPayload.cs
// Namespace: CyberPickle.Gameplay.Audio
//
// Typed payload for MusicEvent.WeaponAimChanged. Fired by WeaponFiring
// whenever its WeaponTargeting flips between "has target" and "no target."
//
// Consumers (currently: WeaponSlotBeatPulse) use this to hide their
// anticipation visuals when the weapon has nothing to fire at, and to
// re-start the anticipation cycle from 0% the moment a target is acquired.
//
// Why this matters: WeaponFiring.HandleSubdivision SKIPS firing when
// !targeting.HasTarget, so no WeaponFire event reaches the UI for the
// grid cells that pass during "no target." Without this signal, UI fuses
// would animate toward fires that never happen.

namespace CyberPickle.Gameplay.Audio
{
    public struct WeaponAimPayload
    {
        public int  SlotIndex;
        public bool HasTarget;
    }
}
