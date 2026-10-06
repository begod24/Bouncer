using System;
using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Waves;
using UnityEngine;
using UnityEngine.Localization;

namespace Bouncer.Run
{
    public enum BestiarySection
    {
        Enemy,
        Elite,
        Boss,
    }

    [CreateAssetMenu(menuName = "Bouncer/Bestiary", fileName = "Bestiary")]
    public sealed class Bestiary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public BestiarySection section;
            [Tooltip("Префаб врага: модель — его ребёнок Visual, имя префаба — ключ открытия босса")]
            public GameObject prefab;
            [Tooltip("Имя — строка таблицы «Content»")]
            public LocalizedString title;
            [Tooltip("Что он делает")]
            public LocalizedString description;
            [Tooltip("Как его выбить")]
            public LocalizedString tip;
            [Tooltip("Сколько попаданий держит на первой арене (дальше по прогулке — больше)")]
            [Min(0)] public int hits;
            [Tooltip("Где ещё встречается, кроме волн арен: ворон зовёт босс финала")]
            public ArenaDefinition[] alsoIn = Array.Empty<ArenaDefinition>();

            public string Id => prefab ? prefab.name : string.Empty;
            public bool IsOpen => section != BestiarySection.Boss || BestiaryProgress.IsOpen(Id);
            public bool IsNew => section == BestiarySection.Boss && BestiaryProgress.IsNew(Id);
        }

        [Tooltip("Прогулка: по её аренам ищется, где встречается враг")]
        public RunDefinition run;
        public List<Entry> entries = new();

        public void Collect(BestiarySection section, List<Entry> result)
        {
            result.Clear();
            foreach (var entry in entries)
                if (entry != null && entry.section == section)
                    result.Add(entry);
        }

        public bool AnyNew()
        {
            foreach (var entry in entries)
                if (entry != null && entry.IsNew)
                    return true;
            return false;
        }

        public int CountOpen(BestiarySection section, out int total)
        {
            int open = 0;
            total = 0;
            foreach (var entry in entries)
            {
                if (entry == null || entry.section != section)
                    continue;
                total++;
                if (entry.IsOpen)
                    open++;
            }
            return open;
        }

        public void ArenasOf(Entry entry, List<ArenaDefinition> result)
        {
            result.Clear();
            if (entry == null || entry.prefab == null)
                return;
            if (run != null)
                for (int stage = 0; stage < run.Count; stage++)
                    for (int variant = 0; variant < run.VariantCount(stage); variant++)
                    {
                        var arena = run.Get(stage, variant);
                        if (arena != null && !result.Contains(arena) && Spawns(arena.wave, entry.prefab))
                            result.Add(arena);
                    }
            if (entry.alsoIn != null)
                foreach (var arena in entry.alsoIn)
                    if (arena != null && !result.Contains(arena))
                        result.Add(arena);
        }

        static bool Spawns(WaveDefinition wave, GameObject prefab)
        {
            if (wave == null)
                return false;
            foreach (var track in wave.tracks)
                if (track.prefab == prefab)
                    return true;
            foreach (var burst in wave.bursts)
            {
                if (burst.prefab == prefab)
                    return true;
                if (burst.variants != null && Array.IndexOf(burst.variants, prefab) >= 0)
                    return true;
            }
            return false;
        }
    }
}
