using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Player
{
    public sealed class AbilityRunner : MonoBehaviour
    {
        readonly List<(float at, Action action)> _queue = new();

        public static AbilityRunner On(GameObject go) =>
            go.TryGetComponent(out AbilityRunner runner) ? runner : go.AddComponent<AbilityRunner>();

        public void After(float seconds, Action action)
        {
            if (action == null)
                return;
            if (seconds <= 0f)
            {
                action();
                return;
            }
            _queue.Add((Time.time + seconds, action));
        }

        void Update()
        {
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (Time.time < _queue[i].at)
                    continue;
                var action = _queue[i].action;
                _queue.RemoveAt(i);
                action();
            }
        }

        void OnDisable() => _queue.Clear();
    }
}
