// Create new file: EventSystemEnsurer.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CyberPickle.Core.UI
{
    public class EventSystemEnsurer : MonoBehaviour
    {
        private void Awake()
        {
            EnsureEventSystem();
        }

        public static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<StandaloneInputModule>();

                Debug.Log("[EventSystemEnsurer] Created EventSystem");
            }
        }
    }
}