using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Waves
{
    /// <summary>Как стоят враги группы в момент появления.</summary>
    public enum GroupLayout
    {
        /// <summary>Кучкой (рой пупсов, пара неваляшек).</summary>
        Cluster,
        /// <summary>Шеренгой лицом к игроку (строй солдатиков).</summary>
        Line,
    }

    /// <summary>
    /// Волны арены: дорожки (кто появляется, когда, как часто и какими группами) и разовые выходы
    /// (например, босс). Время — секунды забега на арене.
    /// </summary>
    [CreateAssetMenu(menuName = "Bouncer/Wave Definition", fileName = "Wave_")]
    public sealed class WaveDefinition : ScriptableObject
    {
        [Tooltip("Длина забега на арене, с. К этому времени лимит врагов доходит до конечного")]
        [Min(1f)] public float duration = 420f;

        [Header("Сколько врагов может быть на арене одновременно")]
        [Min(1)] public int maxAliveStart = 14;
        [Min(1)] public int maxAliveEnd = 36;

        public List<SpawnTrack> tracks = new();
        public List<SpawnBurst> bursts = new();

        public int MaxAliveAt(float time) =>
            Mathf.RoundToInt(Mathf.Lerp(maxAliveStart, maxAliveEnd, Mathf.Clamp01(time / duration)));
    }

    /// <summary>Дорожка: один вид врагов, появляющийся группами с растущим темпом.</summary>
    [Serializable]
    public sealed class SpawnTrack
    {
        public string name = "Враги";
        public GameObject prefab;

        [Tooltip("С какой и до какой секунды забега работает дорожка")]
        [Min(0f)] public float from;
        [Min(0f)] public float to = 420f;
        [Tooltip("Первая группа — через столько секунд после начала дорожки")]
        [Min(0f)] public float firstDelay = 1f;

        [Tooltip("Пауза между группами в начале и в конце дорожки, с")]
        [Min(0.2f)] public float intervalStart = 7f;
        [Min(0.2f)] public float intervalEnd = 3.5f;

        [Tooltip("Размер группы в начале и в конце дорожки")]
        [Min(1)] public int groupStart = 1;
        [Min(1)] public int groupEnd = 2;
        public GroupLayout layout = GroupLayout.Cluster;

        [Tooltip("Сколько врагов этой дорожки может быть живо одновременно")]
        [Min(1)] public int maxAlive = 12;

        public float Progress(float time) => to > from ? Mathf.Clamp01((time - from) / (to - from)) : 1f;
        public float IntervalAt(float time) => Mathf.Lerp(intervalStart, intervalEnd, Progress(time));
        public int GroupSizeAt(float time) => Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(groupStart, groupEnd, Progress(time))));
    }

    /// <summary>Разовый выход группы в заданную секунду забега (не смотрит на лимиты).</summary>
    [Serializable]
    public sealed class SpawnBurst
    {
        public string name = "Выход";
        public GameObject prefab;
        [Min(0f)] public float time;
        [Min(1)] public int count = 1;
        public GroupLayout layout = GroupLayout.Line;
        [Tooltip("Босс арены: когда он и все его половинки выбиты, забег пройден")]
        public bool boss;
    }
}
