// File: Assets/_CyberPickle/Code/DOTS/Components/PlayerStatsData.cs
// Namespace: CyberPickle.DOTS.Components
//
// ECS singleton mirroring the player's effective stats. Written by
// PlayerStatsBridge each frame from the MonoBehaviour PlayerStats
// source-of-truth (only when stats actually change). Read by Burst-
// compiled systems that need fast access to player stats:
//   - XPMagnetSystem (reads MagneticField for radius scaling)
//   - ProjectileCollisionSystem (reads Power, CritChance, Lifesteal for damage pipeline)
//   - Future damage / status systems (read Defense, etc.)
//
// All values stored here are EFFECTIVE (post-modifier) values — the
// final numbers gameplay should use. Base values + modifiers live on
// the MonoBehaviour side.
//
// Field order matches BaseStats / PlayerStatType for one-to-one copy.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct PlayerStatsData : IComponentData
    {
        // Defense / Survivability
        public float MaxHealth;
        public float HealthRegen;
        public float Defense;

        // Combat Output
        public float Power;
        public float CritChance;
        public float Lifesteal;

        // Movement / Pickup
        public float Speed;
        public float MagneticField;
        public float AreaOfEffect;
        public float Dexterity;

        // Replay Variance / Identity
        public float Luck;
        public float Hack;
        public float CooldownReduction;
        public float NeuralAdaptation;
    }
}
