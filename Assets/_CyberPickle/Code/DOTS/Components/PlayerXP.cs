// File: Assets/_CyberPickle/Code/DOTS/Components/PlayerXP.cs
// Namespace: CyberPickle.DOTS.Components
//
// Singleton holding the player's running XP / level state. Lives on a
// dedicated entity created by PlayerXPBridge. The XP collection system
// (Burst) increments CurrentXP when gems are picked up; the bridge
// MonoBehaviour reads the singleton each frame to update the HUD and
// detect level-up thresholds.
//
// LevelUpPending flag is set by the bridge when CurrentXP crosses
// XPToNextLevel — read by the future level-up screen (chunk 6c).

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct PlayerXP : IComponentData
    {
        public int CurrentXP;
        public int CurrentLevel;
        public int XPToNextLevel;

        /// <summary>True when CurrentXP >= XPToNextLevel and a level-up needs to be processed.</summary>
        public bool LevelUpPending;
    }
}
