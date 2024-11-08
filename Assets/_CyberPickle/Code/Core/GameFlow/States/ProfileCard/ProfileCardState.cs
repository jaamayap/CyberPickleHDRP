// File: Assets/Code/Core/GameFlow/States/ProfileCard/ProfileCardState.cs
//
// Purpose: Defines the possible states of the profile card UI component
// Used to manage the card's visibility and interaction states across different game screens
//
// Created: 2024-02-11

namespace CyberPickle.Core.GameFlow.States.ProfileCard
{
    /// <summary>
    /// Represents the different states of the profile card UI component
    /// </summary>
    public enum ProfileCardState
    {
        /// <summary>
        /// Card is not visible
        /// </summary>
        Hidden,

        /// <summary>
        /// Card is visible in minimized form (corner display)
        /// </summary>
        Minimized,

        /// <summary>
        /// Card is visible in expanded form showing full details
        /// </summary>
        Expanded,

        /// <summary>
        /// Card is currently transitioning between states
        /// </summary>
        Transitioning
    }
}
