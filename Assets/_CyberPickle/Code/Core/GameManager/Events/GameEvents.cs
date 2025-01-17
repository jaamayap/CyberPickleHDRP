using CyberPickle.Core.GameFlow.States.ProfileCard;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Core.States;
using System.Collections.Generic;

namespace CyberPickle.Core.Events
{
    public static class GameEvents
    {
        // Game State Events
        public static readonly GameEvent OnGameInitialized = new GameEvent();
        public static readonly GameEvent OnGameStarted = new GameEvent();
        public static readonly GameEvent OnGamePaused = new GameEvent();
        public static readonly GameEvent OnGameResumed = new GameEvent();
        public static readonly GameEvent OnGameOver = new GameEvent();

        // Player Events
        public static readonly GameEvent<int> OnExperienceGained = new GameEvent<int>();
        public static readonly GameEvent<int> OnLevelUp = new GameEvent<int>();
        public static readonly GameEvent<float> OnHealthChanged = new GameEvent<float>();
        public static readonly GameEvent OnPlayerDied = new GameEvent();

        // Progress Events
        public static readonly GameEvent<string> OnAchievementUnlocked = new GameEvent<string>();
        public static readonly GameEvent<string> OnLevelCompleted = new GameEvent<string>();
        public static readonly GameEvent<string> OnCharacterUnlocked = new GameEvent<string>();

        // Economy Events
        public static readonly GameEvent<int> OnCurrencyChanged = new GameEvent<int>();
        public static readonly GameEvent<string> OnItemPurchased = new GameEvent<string>();
        public static readonly GameEvent<string> OnItemEquipped = new GameEvent<string>();

        
        // Input related events
        public static readonly GameEvent OnMainMenuInput = new GameEvent();
        public static readonly GameEvent<float> OnHorizontalInput = new GameEvent<float>();
        public static readonly GameEvent OnPauseRequested = new GameEvent();
        public static readonly GameEvent OnResumeRequested = new GameEvent();
        public static readonly GameEvent<GameState> OnGameStateChanged = new GameEvent<GameState>();
        public static GameEvent<float> OnProfileNavigationInput = new GameEvent<float>();

        // Authentication events
        public static readonly GameEvent OnAuthenticationRequested = new GameEvent();
        public static readonly GameEvent OnProfileLoadRequested = new GameEvent();

        // Profile-related events
        public static readonly GameEvent<List<ProfileData>> OnProfilesLoaded = new GameEvent<List<ProfileData>>();
        public static readonly GameEvent OnProfilesCleared = new GameEvent();
        public static readonly GameEvent<string> OnProfileSelected = new GameEvent<string>();
        public static readonly GameEvent<string> OnProfileCreated = new GameEvent<string>();
        public static readonly GameEvent<string> OnProfileDeleted = new GameEvent<string>();
        public static readonly GameEvent<string> OnProfileRestored = new GameEvent<string>();

        // UI Animation Events
        public static readonly GameEvent OnUIAnimationStarted = new GameEvent();
        public static readonly GameEvent OnUIAnimationCompleted = new GameEvent();
        public static readonly GameEvent OnProfileUIReady = new GameEvent();

        // Profile Card UI Events
        public static readonly GameEvent<ProfileCardState> OnProfileCardStateChanged = new GameEvent<ProfileCardState>();
        public static readonly GameEvent OnProfileCardClicked = new GameEvent();
        public static readonly GameEvent OnProfileCardTransitionComplete = new GameEvent();
        public static readonly GameEvent<bool> OnProfileCardInteractionEnabled = new GameEvent<bool>();

        //Camera Events
        public static readonly GameEvent OnCameraTransitionComplete = new GameEvent();

        // Character Selection Events
        public static readonly GameEvent<string> OnCharacterHoverEnter = new GameEvent<string>();
        public static readonly GameEvent<string> OnCharacterHoverExit = new GameEvent<string>();
        public static readonly GameEvent<string> OnCharacterSelected = new GameEvent<string>();
        public static readonly GameEvent<string> OnCharacterDetailsRequested = new GameEvent<string>();

        /// <summary>
        /// Event raised when the mouse pointer enters a character in the selection screen
        /// </summary>
        /// <param name="characterId">ID of the character being hovered</param>

        /// <summary>
        /// Event raised when the mouse pointer exits a character in the selection screen
        /// </summary>
        /// <param name="characterId">ID of the character no longer being hovered</param>

        /// <summary>
        /// Event raised when a character is selected via left-click
        /// </summary>
        /// <param name="characterId">ID of the selected character</param>

        /// <summary>
        /// Event raised when character details are requested via right-click
        /// </summary>
        /// <param name="characterId">ID of the character whose details are requested</param>

    }
}
