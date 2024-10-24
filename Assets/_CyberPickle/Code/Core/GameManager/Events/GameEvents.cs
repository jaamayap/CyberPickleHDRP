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
    }
}
