using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Run
{
    [CreateAssetMenu(menuName = "Bouncer/Run/Run", fileName = "Run_")]
    public sealed class RunDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Fork
        {
            [Tooltip("Номер этапа прогулки (0 — первая арена), на котором можно свернуть сюда")]
            [Min(0)] public int stage;
            public ArenaDefinition arena;
        }

        [Tooltip("Основные арены этапов по порядку")]
        public List<ArenaDefinition> arenas = new();
        [Tooltip("Развилки: другие арены этапов. К ним ведёт вторая стрелка на прошлой арене")]
        public List<Fork> forks = new();

        public int Count => arenas.Count;

        public ArenaDefinition Get(int index) =>
            index >= 0 && index < arenas.Count ? arenas[index] : null;

        public ArenaDefinition Get(int index, int variant)
        {
            if (variant <= 0)
                return Get(index);
            if (index < 0 || index >= arenas.Count)
                return null;
            int found = 0;
            foreach (var fork in forks)
                if (fork != null && fork.stage == index && fork.arena && ++found == variant)
                    return fork.arena;
            return null;
        }

        public int VariantCount(int index)
        {
            if (index < 0 || index >= arenas.Count)
                return 0;
            int count = 1;
            foreach (var fork in forks)
                if (fork != null && fork.stage == index && fork.arena)
                    count++;
            return count;
        }

        public bool IsLast(int index) => index >= arenas.Count - 1;

        public int IndexOfScene(string sceneName) => FindScene(sceneName, out int index, out _) ? index : -1;

        public bool FindScene(string sceneName, out int index, out int variant)
        {
            variant = 0;
            for (index = 0; index < arenas.Count; index++)
                if (arenas[index] && arenas[index].sceneName == sceneName)
                    return true;
            for (index = 0; index < arenas.Count; index++)
                for (variant = 1; variant < VariantCount(index); variant++)
                    if (Get(index, variant).sceneName == sceneName)
                        return true;
            index = -1;
            variant = 0;
            return false;
        }
    }
}
