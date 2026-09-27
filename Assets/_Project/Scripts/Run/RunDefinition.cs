using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Run
{
    /// <summary>
    /// Прогулка: арены по порядку, от утра до ночи. Последняя арена — финал, её победа заканчивает прогулку.
    /// На некоторых этапах есть развилка: вместо основной арены можно пойти на другую (Коробка или Барахолка) —
    /// на пройденной прошлой арене тогда две стрелки. Вариант 0 — основная арена этапа, 1 и дальше — развилки.
    /// </summary>
    [CreateAssetMenu(menuName = "Bouncer/Run/Run", fileName = "Run_")]
    public sealed class RunDefinition : ScriptableObject
    {
        /// <summary>Другая арена этапа.</summary>
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

        /// <summary>Арена этапа: variant 0 — основная, 1 и дальше — развилки этого этапа по порядку.</summary>
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

        /// <summary>Сколько арен на выбор у этапа (основная и развилки).</summary>
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

        /// <summary>Первая арена, которая играется в этой сцене. -1 — ни одной.</summary>
        public int IndexOfScene(string sceneName) => FindScene(sceneName, out int index, out _) ? index : -1;

        /// <summary>Этап и вариант первой арены (основные — раньше развилок), которая играется в этой сцене.</summary>
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
