using UnityEngine;
using CyberPickle.Core.Interfaces;

namespace CyberPickle.Core.Management
{
    /// <summary>
    /// Base manager class implementing the singleton pattern for all game managers
    /// </summary>
    public abstract class Manager<T> : MonoBehaviour where T : Manager<T>
    {
        private static T instance;
        private static readonly object lockObject = new object();
        private static bool isQuitting = false;

        public static T Instance
        {
            get
            {
                if (isQuitting)
                {
                    Debug.LogWarning($"[{typeof(T).Name}] Instance will not be returned because the application is quitting.");
                    return null;
                }

                lock (lockObject)
                {
                    if (instance == null)
                    {
                        instance = FindObjectOfType<T>();
                        if (instance == null)
                        {
                            GameObject go = new GameObject($"[{typeof(T).Name}]");
                            instance = go.AddComponent<T>();
                            DontDestroyOnLoad(go);
                        }
                    }
                    return instance;
                }
            }
        }

        protected virtual void Awake()
        {
            if (instance == null)
            {
                instance = (T)this;
                DontDestroyOnLoad(gameObject);
                OnManagerAwake();
            }
            else if (instance != this)
            {
                Debug.LogWarning($"[{typeof(T).Name}] Instance already exists, destroying duplicate!");
                Destroy(gameObject);
            }
        }

        protected virtual void OnManagerAwake()
        {
            // If this manager is initializable, initialize it automatically
            if (this is IInitializable initializable)
            {
                Debug.Log($"<color=yellow>[{typeof(T).Name}] Auto-initializing...</color>");
                initializable.Initialize();
            }
        }

        protected virtual void OnApplicationQuit()
        {
            isQuitting = true;
        }
    }
}
