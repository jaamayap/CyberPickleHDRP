// File: Assets/_CyberPickle/Code/DOTS/Components/XPGemValue.cs
// Namespace: CyberPickle.DOTS.Components
//
// XP awarded when this gem is collected. Set when the gem is spawned —
// EnemyDeathSystem reads the tier registry, picks the rolled tier, and
// stamps the corresponding XP value onto the new gem entity.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct XPGemValue : IComponentData
    {
        public int Value;
    }
}
