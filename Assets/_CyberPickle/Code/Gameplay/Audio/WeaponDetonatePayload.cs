// File: Assets/_CyberPickle/Code/Gameplay/Audio/WeaponDetonatePayload.cs
// Namespace: CyberPickle.Gameplay.Audio
//
// Typed payload for MusicEvent.WeaponDetonate. Fired when an AoE projectile
// (currently: grenade launcher) reaches its rhythm-locked detonation time
// and explodes. Carries:
//
//   • WeaponId      — source weapon's equipmentId. Used by the Wwise
//     adapter to map to a per-weapon detonation event (e.g.,
//     "Play_GrenadeLauncher_Snare" for "handweapon_grenadeL").
//
//   • WorldPosition — where the explosion happened. Wwise uses this to
//     drive 3D-spatialized stereo panning of the snare so the player
//     can hear *where* on screen the impact occurred without losing
//     the beat (positioning configured as "Position only" with no
//     attenuation curve — panning only, full volume preserved).
//
// SlotIndex is intentionally absent in this first iteration — slot-filtered
// UI animation on detonate (vs. on fire, which WeaponSlotBeatPulse already
// handles via WeaponFirePayload.SlotIndex) isn't a v1 requirement, and
// adding it would mean plumbing SlotIndex through ProjectileSource +
// DamageHitReport. Add when a detonate-side UI consumer needs it.
//
// Why UnityEngine.Vector3 rather than Unity.Mathematics.float3: this
// struct is consumed exclusively by managed (Mono) code (the Wwise
// adapter sets GameObject.transform.position from it). Keeping the type
// Unity-native avoids a conversion at the consumer.

using UnityEngine;

namespace CyberPickle.Gameplay.Audio
{
    public struct WeaponDetonatePayload
    {
        public string  WeaponId;
        public Vector3 WorldPosition;
    }
}
