// File: Assets/_CyberPickle/Code/Gameplay/Audio/MusicEventBus.cs
// Namespace: CyberPickle.Gameplay.Audio
//
// Static fan-out for gameplay → audio events. Producers call Fire(); any
// number of consumers subscribe to OnEvent. Stage 0 (today): Debug.Log
// when verbose logging is on. Stage 2 (M9 Wwise integration): a single
// listener maps each MusicEvent to an Ak event post.
//
// Why static, not Manager<T>: the bus has no per-scene state and no
// inspector-bindings. It's a process-global pure dispatcher. Keeping it
// static side-steps Manager<T>'s Awake-ordering concerns and means callers
// don't have to null-check an Instance during editor recompiles.
//
// Threading: Unity main thread only. Burst-compiled ECS systems CANNOT
// call this directly — they must accumulate events into an
// IComponentData / NativeQueue and have a managed bridge drain them on
// the main thread. See EnemyDeathBridge (TBD) for the pattern.
//
// Payload alloc note: the `object` parameter boxes value-typed payloads.
// For the Day-1 stub this is fine; the firing rates are bounded and we
// just Debug.Log. When Stage 2 lands, replace with typed Fire<T>(MusicEvent, T)
// overloads constrained to struct, plus a SetPayloadTo / GetPayloadFrom
// pattern for the Wwise listener.

using System;
using UnityEngine;

namespace CyberPickle.Gameplay.Audio
{
    public static class MusicEventBus
    {
        /// <summary>
        /// Subscribe to receive every event fired through the bus. Listeners
        /// MUST be written defensively (handle null payloads, unexpected
        /// types). Subscribe in OnEnable, unsubscribe in OnDisable.
        /// </summary>
        public static event Action<MusicEvent, object> OnEvent;

        /// <summary>
        /// When true, every Fire() emits a Debug.Log. Off by default to
        /// avoid console flood in combat (WeaponFire alone can fire 30+/sec).
        /// Toggle on for diagnostic sessions or when wiring a new producer.
        /// </summary>
        public static bool VerboseLogging;

        /// <summary>
        /// Dispatch a music event. Payload semantics are per-event and
        /// documented on the MusicEvent enum. Pass null when not applicable.
        /// </summary>
        public static void Fire(MusicEvent type, object payload = null)
        {
            if (VerboseLogging)
            {
                if (payload != null)
                    Debug.Log($"[MusicEventBus] {type}  payload={payload}");
                else
                    Debug.Log($"[MusicEventBus] {type}");
            }

            // Defensive: a buggy listener mustn't take down other listeners.
            // We swallow per-listener exceptions and log them so combat keeps
            // running even if (e.g.) a UI controller is mid-destruction.
            var handlers = OnEvent;
            if (handlers == null) return;

            foreach (var h in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<MusicEvent, object>)h)(type, payload);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MusicEventBus] Listener threw on {type}: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// Clear all subscribers. Editor-only utility for recovering from
        /// stuck domain-reload state. Don't call from gameplay code.
        /// </summary>
        public static void ClearAllListeners()
        {
            OnEvent = null;
        }
    }
}
