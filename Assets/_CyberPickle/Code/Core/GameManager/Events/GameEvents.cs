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
    }
}
