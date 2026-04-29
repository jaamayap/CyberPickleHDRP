using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;

namespace CyberPickle.Core.Events
{
    public class EventManager : Manager<EventManager>, IInitializable
    {
        private bool isInitialized;

        public void Initialize()
        {
            if (isInitialized) return;

            // Initialize any event-related systems
            RegisterGlobalEventListeners();

            isInitialized = true;
        }

        private void RegisterGlobalEventListeners()
        {
            // Register for analytics
            GameEvents.OnLevelCompleted.AddListener((levelId) =>
                TrackLevelCompletion(levelId));

            GameEvents.OnAchievementUnlocked.AddListener((achievementId) =>
                TrackAchievement(achievementId));

            // Add more global event listeners as needed
        }

        private void TrackLevelCompletion(string levelId)
        {
            // Will be implemented when Analytics system is ready
            UnityEngine.Debug.Log($"Level completed: {levelId}");
        }

        private void TrackAchievement(string achievementId)
        {
            // Will be implemented when Achievement system is ready
            UnityEngine.Debug.Log($"Achievement unlocked: {achievementId}");
        }

        protected override void OnManagerAwake()
        {
            Initialize();
        }
    }
}
