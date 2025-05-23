using UnityEngine;
using CyberPickle.Core.Interfaces;
using System.Threading;

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
        protected CancellationTokenSource cancellationTokenSource;

        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    // Check if we're quitting or in play mode
                    if (!Application.isPlaying)
                    {
                        Debug.LogWarning($"[{typeof(T).Name}] Instance will not be created because the application is not in play mode.");
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
                    }
                }
                return instance;
            }
        }

        protected virtual void Awake()
        {
            if (instance == null)
            {
                instance = (T)this;
                DontDestroyOnLoad(gameObject);
                cancellationTokenSource = new CancellationTokenSource();
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

        protected virtual void OnEnable()
        {
            if (cancellationTokenSource == null || cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource = new CancellationTokenSource();
            }
            OnManagerEnabled();
        }

        protected virtual void OnDisable()
        {
            OnManagerDisabled();
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                OnManagerDestroyed();
                instance = null;

                // Cancel any pending async operations
                if (cancellationTokenSource != null)
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = null;
                }
            }
        }

        protected virtual void OnApplicationQuit()
        {
            isQuitting = true;

            // Cancel any pending async operations
            if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
            }

            OnManagerApplicationQuit();
        }

        // Virtual methods for derived classes to override
        protected virtual void OnManagerEnabled() { }
        protected virtual void OnManagerDisabled() { }
        protected virtual void OnManagerDestroyed() { }
        protected virtual void OnManagerApplicationQuit() { }

        // Helper method to check if this is the active instance
        protected bool IsActiveInstance => instance == this;

        // Helper method to check if the application is quitting
        protected bool IsQuitting => Application.isPlaying && !Application.isEditor && (Time.frameCount == 0 || !enabled);
    }
}