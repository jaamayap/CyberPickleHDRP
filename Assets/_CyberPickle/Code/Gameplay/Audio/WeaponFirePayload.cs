// File: Assets/_CyberPickle/Code/Gameplay/Audio/WeaponFirePayload.cs
// Namespace: CyberPickle.Gameplay.Audio
//
// Typed payload for MusicEvent.WeaponFire. Carries enough information for
// downstream consumers to route the event to a specific weapon's UI/audio:
//
//   • SlotIndex — which loadout axis fired (0..3). UI consumers
//     (WeaponSlotBeatPulse) filter on this so each slot only reacts to
//     its OWN weapon's shots, not every weapon's shots.
//
//   • WeaponId — the source weapon's equipmentId. Future Wwise stage 2
//     maps weapon-id → musical note / sample.
//
// Replaced the legacy `string weaponName` payload (2026-05-13) — name was
// not stable enough for Wwise mapping and didn't tell UI which slot to
// react to. Only consumer at the time was a Debug.Log stub, so no
// migration was needed.

namespace CyberPickle.Gameplay.Audio
{
    public struct WeaponFirePayload
    {
        public int    SlotIndex;
        public string WeaponId;
    }
}
