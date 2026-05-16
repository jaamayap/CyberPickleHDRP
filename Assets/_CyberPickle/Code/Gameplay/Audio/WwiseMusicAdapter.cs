// File: Assets/_CyberPickle/Code/Gameplay/Audio/WwiseMusicAdapter.cs
// Namespace: CyberPickle.Gameplay.Audio
//
// Bridges the MusicEventBus → Wwise sound engine. Subscribes to gameplay
// MusicEvents (WeaponFire, WeaponDetonate, …) and posts the corresponding
// Wwise event for the source weapon. The event NAMES live on the weapon's
// own WeaponData asset (wwiseFireEventName / wwiseDetonateEventName) —
// no separate mapping table, no inspector-list to maintain. Lookup goes
// through WeaponLoadoutRuntime.FindByWeaponId() so adding a new weapon
// is "make the WeaponData asset, set the two event names, done."
//
// Place ONE instance of this in the scene (typically on the same
// GameObject as the MusicConductor, or on a dedicated AudioRoot
// object). No other authoring required.
//
// Why a single managed adapter (not per-weapon scripts): keeps Wwise
// posting concentrated. If we ever swap Wwise for another middleware,
// this file is the single point of change.
//
// ─── Positioning model ─────────────────────────────────────────────────
//
//  Fire events:  posted with this adapter's GameObject as the source.
//                The Wwise Random Container for Kick is configured as
//                Listener Relative Routing + 3D Position (Game-defined)
//                with NO attenuation, so kicks center perfectly when the
//                adapter sits on the listener (or anywhere fixed, since
//                the Music bus is 2D-locked anyway). Could spawn a
//                transient GO at the muzzle for shotgun-style per-muzzle
//                panning later — out of scope for v1.
//
//  Detonate events: posted with a TRANSIENT GameObject spawned at the
//                explosion epicenter (carried in WeaponDetonatePayload.
//                WorldPosition). Wwise reads its transform.position to
//                drive 3D-spatialized panning of the snare. No attenuation
//                curve on the Random Container → full volume regardless of
//                distance; only the *pan* changes with screen position.
//                The transient GO is destroyed shortly after the event so
//                we don't leak GameObjects. The 1-second delay is generous
//                — our kick/snare samples are all <500ms.
//
// ─── Threading ─────────────────────────────────────────────────────────
//
// MusicEventBus.OnEvent fires on the main thread (MusicEventBus's
// docstring states main-thread-only). AkUnitySoundEngine.PostEvent and
// GameObject construction are also main-thread. So no marshalling needed.
//
// Burst-side producers like ProjectileExplosionSystem accumulate events
// into a NativeQueue (DamageReportQueueSingleton) and the managed-side
// DamageReportDrainSystem fires MusicEventBus from OnUpdate, which Unity
// runs on the main thread.

using UnityEngine;
using CyberPickle.Gameplay.Weapons;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.Gameplay.Audio
{
    [DisallowMultipleComponent]
    public class WwiseMusicAdapter : MonoBehaviour
    {
        [Header("Diagnostics")]
        [Tooltip("Log every PostEvent + every missing-mapping case. Use sparingly — combat fires 30+ events/sec at high BPM with multiple weapons.")]
        [SerializeField] private bool verboseLogging;

        [Tooltip("Seconds before the transient detonate GameObject is destroyed. Should comfortably exceed your longest detonate sample's tail. 1s is plenty for ~500ms samples.")]
        [SerializeField] private float detonateGameObjectLifetime = 1f;

        private void OnEnable()
        {
            MusicEventBus.OnEvent += HandleMusicEvent;
        }

        private void OnDisable()
        {
            MusicEventBus.OnEvent -= HandleMusicEvent;
        }

        private void HandleMusicEvent(MusicEvent type, object payload)
        {
            // Cheap switch — only the events we currently route to Wwise.
            // Add cases as new weapons / Wwise event categories come online
            // (EnemyHit → impact layer, LevelUp → stinger, etc.).
            switch (type)
            {
                case MusicEvent.WeaponFire:
                    if (payload is WeaponFirePayload fire) PostFire(fire);
                    break;

                case MusicEvent.WeaponDetonate:
                    if (payload is WeaponDetonatePayload det) PostDetonate(det);
                    break;
            }
        }

        // ─── WeaponData lookup ──────────────────────────────────────────────
        //
        // Find the WeaponData for the given weaponId by querying the active
        // loadout. Works for both Fire (we have the WeaponId in the payload)
        // and Detonate (ditto). Returns null if the weapon isn't currently
        // equipped — which shouldn't happen for normal gameplay (you can
        // only fire weapons you have equipped) but we null-check defensively.

        private static WeaponData ResolveWeaponData(string weaponId)
        {
            var loadout = WeaponLoadoutRuntime.Instance;
            if (loadout == null) return null;
            var instance = loadout.FindByWeaponId(weaponId);
            return (instance != null && instance.IsValid) ? instance.weaponData : null;
        }

        private void PostFire(WeaponFirePayload payload)
        {
            var data = ResolveWeaponData(payload.WeaponId);
            if (data == null)
            {
                if (verboseLogging)
                    Debug.Log($"[WwiseMusicAdapter] FIRE — couldn't resolve WeaponData for '{payload.WeaponId}' (slot {payload.SlotIndex}).");
                return;
            }
            if (string.IsNullOrWhiteSpace(data.wwiseFireEventName))
            {
                if (verboseLogging)
                    Debug.Log($"[WwiseMusicAdapter] FIRE — '{payload.WeaponId}' has no wwiseFireEventName authored. Silent.");
                return;
            }

            // Post from this adapter's GameObject. For the kick layer,
            // positioning is configured 3D in Wwise but the GO sits at the
            // listener so panning naturally lands center. Could be enhanced
            // later by spawning per-muzzle GameObjects for multi-muzzle
            // weapons, but v1 deliberately keeps the kick centered to lock
            // the beat against the music bus.
            AkUnitySoundEngine.PostEvent(data.wwiseFireEventName, gameObject);

            if (verboseLogging)
                Debug.Log($"[WwiseMusicAdapter] FIRE '{data.wwiseFireEventName}' for weapon '{payload.WeaponId}' (slot {payload.SlotIndex}).");
        }

        private void PostDetonate(WeaponDetonatePayload payload)
        {
            var data = ResolveWeaponData(payload.WeaponId);
            if (data == null)
            {
                if (verboseLogging)
                    Debug.Log($"[WwiseMusicAdapter] DETONATE — couldn't resolve WeaponData for '{payload.WeaponId}'.");
                return;
            }
            if (string.IsNullOrWhiteSpace(data.wwiseDetonateEventName))
            {
                if (verboseLogging)
                    Debug.Log($"[WwiseMusicAdapter] DETONATE — '{payload.WeaponId}' has no wwiseDetonateEventName authored. Silent.");
                return;
            }

            // Spawn a transient GameObject at the explosion epicenter so
            // Wwise reads its transform.position for the snare's 3D pan.
            // Adding an AkGameObj component is what registers it with the
            // sound engine — without that, Wwise treats the GO as
            // un-positioned. The component auto-registers in Awake.
            //
            // The destroy delay must outlive the longest sample tail; 1s
            // is plenty for the percussion samples we currently use. If
            // someone authors a 4-second crash cymbal here later, bump
            // the inspector field.
            var go = new GameObject($"WwiseDetonate_{data.wwiseDetonateEventName}");
            go.transform.position = payload.WorldPosition;
            go.AddComponent<AkGameObj>();
            AkUnitySoundEngine.PostEvent(data.wwiseDetonateEventName, go);
            Destroy(go, detonateGameObjectLifetime);

            if (verboseLogging)
                Debug.Log($"[WwiseMusicAdapter] DETONATE '{data.wwiseDetonateEventName}' for weapon '{payload.WeaponId}' at world pos {payload.WorldPosition}.");
        }
    }
}
