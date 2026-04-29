using System;
using System.Collections.Generic;

namespace CyberPickle.Core.Events
{
    public class GameEvent
    {
        private readonly List<Action> listeners = new List<Action>();

        public void AddListener(Action listener)
        {
            listeners.Add(listener);
        }

        public void RemoveListener(Action listener)
        {
            listeners.Remove(listener);
        }

        public void Invoke()
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i]?.Invoke();
        }
    }

    public class GameEvent<T>
    {
        private readonly List<Action<T>> listeners = new List<Action<T>>();

        public void AddListener(Action<T> listener)
        {
            listeners.Add(listener);
        }

        public void RemoveListener(Action<T> listener)
        {
            listeners.Remove(listener);
        }

        public void Invoke(T value)
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i]?.Invoke(value);
        }
    }
}
