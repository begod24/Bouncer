using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    [Serializable]
    public sealed class RunRecord
    {
        public float time;
        public int danger = 1;
        public string kid;
        public string[] cards = Array.Empty<string>();
        public string date;
        public int players = 1;

        public bool IsCoop => players > 1;
    }

    public static class RunRecords
    {
        const string Key = "bouncer.records";
        const string UnseenKey = "bouncer.records.unseen";
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

        public static bool HasUnseen => PlayerPrefs.GetInt(UnseenKey, 0) == 1;

        public static void MarkSeen()
        {
            if (!HasUnseen)
                return;
            PlayerPrefs.DeleteKey(UnseenKey);
            PlayerPrefs.Save();
        }

        public static RunRecord Best(int danger, bool coop = false)
        {
            RunRecord best = null;
            foreach (var run in Data.runs)
                if (run.danger == danger && run.IsCoop == coop && (best == null || run.time < best.time))
                    best = run;
            return best;
        }

        public static bool Add(RunRecord record)
        {
            if (record == null || record.time <= 0f)
                return false;
            var best = Best(record.danger, record.IsCoop);
            bool isBest = best == null || record.time < best.time;
            record.date = DateTime.Now.ToString("yyyy-MM-dd");
            Data.runs.Add(record);
            Data.runs.Sort((a, b) => a.danger != b.danger ? b.danger.CompareTo(a.danger) : a.time.CompareTo(b.time));
            var kept = new Dictionary<int, int>();
            for (int i = 0; i < Data.runs.Count; i++)
            {
                int group = Data.runs[i].danger * 2 + (Data.runs[i].IsCoop ? 1 : 0);
                kept.TryGetValue(group, out int count);
                if (count >= KeepPerDanger)
                {
                    Data.runs.RemoveAt(i--);
                    continue;
                }
                kept[group] = count + 1;
            }
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            if (isBest)
                PlayerPrefs.SetInt(UnseenKey, 1);
            PlayerPrefs.Save();
            return isBest;
        }

        public static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_store = null;
    }
}
