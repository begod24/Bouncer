using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>Одна пройденная прогулка: время, опасность, кем играл и с какими карточками.</summary>
    [Serializable]
    public sealed class RunRecord
    {
        public float time;
        public int danger = 1;
        /// <summary>Имя ассета ребёнка (KidDefinition).</summary>
        public string kid;
        /// <summary>Имена ассетов карточек, которые были в карманах в конце.</summary>
        public string[] cards = Array.Empty<string>();
        public string date;
    }

    /// <summary>
    /// Локальные рекорды: лучшие победы (по времени) на каждом уровне опасности. Лежат в PlayerPrefs одним JSON.
    /// Новый рекорд помечается «новое!» в тетрадке, пока игрок не откроет страницу рекордов (<see cref="MarkSeen"/>).
    /// </summary>
    public static class RunRecords
    {
        const string Key = "bouncer.records";
        const string UnseenKey = "bouncer.records.unseen";
        /// <summary>Сколько побед хранить на одном уровне опасности.</summary>
        const int KeepPerDanger = 5;

        [Serializable]
        sealed class Store
        {
            public List<RunRecord> runs = new();
        }

        static Store s_store;

        static Store Data
        {
            get
            {
                if (s_store != null)
                    return s_store;
                s_store = new Store();
                string json = PlayerPrefs.GetString(Key, string.Empty);
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        JsonUtility.FromJsonOverwrite(json, s_store);
                    }
                    catch (Exception)
                    {
                        s_store = new Store();
                    }
                }
                s_store.runs ??= new List<RunRecord>();
                return s_store;
            }
        }

        public static IReadOnlyList<RunRecord> All => Data.runs;

        /// <summary>Есть рекорд, которого игрок ещё не видел в тетрадке.</summary>
        public static bool HasUnseen => PlayerPrefs.GetInt(UnseenKey, 0) == 1;

        /// <summary>Страницу рекордов открыли — «новое!» больше не показывать.</summary>
        public static void MarkSeen()
        {
            if (!HasUnseen)
                return;
            PlayerPrefs.DeleteKey(UnseenKey);
            PlayerPrefs.Save();
        }

        /// <summary>Лучшая победа на этом уровне опасности. null — побед ещё нет.</summary>
        public static RunRecord Best(int danger)
        {
            RunRecord best = null;
            foreach (var run in Data.runs)
                if (run.danger == danger && (best == null || run.time < best.time))
                    best = run;
            return best;
        }

        /// <summary>Запомнить победу. true — это новый рекорд своего уровня опасности.</summary>
        public static bool Add(RunRecord record)
        {
            if (record == null || record.time <= 0f)
                return false;
            var best = Best(record.danger);
            bool isBest = best == null || record.time < best.time;
            record.date = DateTime.Now.ToString("yyyy-MM-dd");
            Data.runs.Add(record);
            Data.runs.Sort((a, b) => a.danger != b.danger ? b.danger.CompareTo(a.danger) : a.time.CompareTo(b.time));
            // На каждом уровне держим только несколько лучших.
            var kept = new Dictionary<int, int>();
            for (int i = 0; i < Data.runs.Count; i++)
            {
                int danger = Data.runs[i].danger;
                kept.TryGetValue(danger, out int count);
                if (count >= KeepPerDanger)
                {
                    Data.runs.RemoveAt(i--);
                    continue;
                }
                kept[danger] = count + 1;
            }
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            if (isBest)
                PlayerPrefs.SetInt(UnseenKey, 1);
            PlayerPrefs.Save();
            return isBest;
        }

        /// <summary>Время прогулки как «мм:сс».</summary>
        public static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_store = null;
    }
}
