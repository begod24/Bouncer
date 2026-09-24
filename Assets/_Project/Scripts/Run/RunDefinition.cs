using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Run
{
    /// <summary>Прогулка: арены по порядку, от утра до ночи. Последняя арена — финал, её победа заканчивает прогулку.</summary>
    [CreateAssetMenu(menuName = "Bouncer/Run/Run", fileName = "Run_")]
    public sealed class RunDefinition : ScriptableObject
    {
        public List<ArenaDefinition> arenas = new();

        public int Count => arenas.Count;

        public ArenaDefinition Get(int index) =>
            index >= 0 && index < arenas.Count ? arenas[index] : null;

        public bool IsLast(int index) => index >= arenas.Count - 1;

        /// <summary>Первая арена, которая играется в этой сцене. -1 — ни одной.</summary>
        public int IndexOfScene(string sceneName)
        {
            for (int i = 0; i < arenas.Count; i++)
                if (arenas[i] && arenas[i].sceneName == sceneName)
                    return i;
            return -1;
        }
    }
}
