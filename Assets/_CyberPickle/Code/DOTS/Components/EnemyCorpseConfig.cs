// File: Assets/_CyberPickle/Code/DOTS/Components/EnemyCorpseConfig.cs
// Namespace: CyberPickle.DOTS.Components
//
// Per-enemy corpse timing, baked from EnemyData.corpseDelayBeforeDissolve
// + EnemyData.corpseDissolveDuration. Read by EnemyDeathSystem when the
// enemy dies — its values are copied into the CorpseLifecycle component
// added at death time.
//
// Why a separate config component instead of putting these on
// CorpseLifecycle directly: we don't want every alive enemy to carry
// the runtime corpse state (DeathTime, DissolveSignaled). CorpseLifecycle
// is added only at the moment of death; this config is the static
// designer-tuned values that live on every enemy entity from spawn.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct EnemyCorpseConfig : IComponentData
    {
        public float DelayBeforeDissolve;
        public float DissolveDuration;
    }
}
