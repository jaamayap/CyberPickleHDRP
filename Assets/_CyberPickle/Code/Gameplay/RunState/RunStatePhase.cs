// File: Assets/_CyberPickle/Code/Gameplay/RunState/RunStatePhase.cs
// Namespace: CyberPickle.Gameplay.RunState
//
// Phases of a single run. RunStateManager owns the current phase and
// transitions between them; every gameplay system queries the manager
// (or relies on Time.timeScale = 0 during paused phases) before acting.
//
// Numeric values are stable for serialization in case we ever save mid-run
// state. Add new phases at the end of the enum.

namespace CyberPickle.Gameplay.RunState
{
    public enum RunStatePhase : byte
    {
        /// <summary>Scene + player are being set up. Gameplay hasn't started yet.</summary>
        Loading        = 0,

        /// <summary>Active gameplay. Time.timeScale = 1.</summary>
        Running        = 1,

        /// <summary>Player just leveled up; choice screen is showing. Time.timeScale = 0. (M7.3)</summary>
        LevelUpPaused  = 2,

        /// <summary>User pause menu is open. Time.timeScale = 0. (Future)</summary>
        Paused         = 3,

        /// <summary>Player died (or boss killed). Results screen is showing. Time.timeScale = 0.</summary>
        GameOver       = 4,
    }
}
