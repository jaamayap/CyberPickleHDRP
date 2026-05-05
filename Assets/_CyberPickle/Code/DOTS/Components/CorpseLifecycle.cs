// File: Assets/_CyberPickle/Code/DOTS/Components/CorpseLifecycle.cs
// Namespace: CyberPickle.DOTS.Components
//
// Tracks the post-death timeline for a corpse entity. Added by
// EnemyDeathSystem when an enemy dies; ticked by CorpseLifecycleSystem.
//
// Timeline:
//   t=0                       death (component added)
//   t=DelayBeforeDissolve     dissolve starts — visual signaled, animator
//                             disabled, physics body neutralized
//   t=Delay + DissolveDuration  entity destroyed (and bridge auto-cleans visual)
//
// Storing absolute SystemAPI.Time.ElapsedTime stamps avoids per-frame
// drift accumulation. The system computes elapsed by subtracting
// current ElapsedTime from DeathTime each frame.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct CorpseLifecycle : IComponentData
    {
        /// <summary>SystemAPI.Time.ElapsedTime when the entity died (and this component was added).</summary>
        public double DeathTime;

        /// <summary>Seconds from DeathTime before the dissolve effect starts. Tunable per enemy via EnemyData.</summary>
        public float DelayBeforeDissolve;

        /// <summary>Seconds the dissolve effect runs before the entity is destroyed.</summary>
        public float DissolveDuration;

        /// <summary>Set to true the frame dissolve is signaled to the visual, so we don't re-signal every frame.</summary>
        public bool DissolveSignaled;
    }
}
