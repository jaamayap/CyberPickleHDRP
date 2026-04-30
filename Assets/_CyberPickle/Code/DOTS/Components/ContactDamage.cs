// File: Assets/_CyberPickle/Code/DOTS/Components/ContactDamage.cs
// Namespace: CyberPickle.DOTS.Components
//
// Damage applied to the player when this entity makes physical contact.
// Used by enemies (M6 contact-damage-to-player system) and potentially by
// hazardous environment entities. Per-enemy value baked from
// EnemyData.contactDamage.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct ContactDamage : IComponentData
    {
        public float Value;
    }
}
