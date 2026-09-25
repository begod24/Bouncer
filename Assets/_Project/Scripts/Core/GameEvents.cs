using System;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>Глобальные игровые события, на которые подписываются сессия, HUD, звук и т.п.</summary>
    public static class GameEvents
    {
        public static event Action<GameObject, HitInfo> EnemyKilled;
        public static event Action<GameObject> PlayerDied;
        /// <summary>Прозвучало событие игры; position — где (для интерфейса — любая точка).</summary>
        public static event Action<SoundCue, Vector3> SoundRequested;
        /// <summary>Босс арены выбит целиком, вместе со всеми половинками.</summary>
        public static event Action BossDefeated;
        /// <summary>
        /// Надпись мелом на экране: правило раунда Физрука («Замри!») и т.п. Ключи — строки таблицы «UI»
        /// (подсказка может быть пустой), seconds — сколько висит надпись.
        /// </summary>
        public static event Action<Announcement> Announced;
        /// <summary>Кто-то зовёт подмогу (Физрук — «Замена!»): спавнер ставит группу в очередь у этой точки, с метками.</summary>
        public static event Action<SpawnRequest> SpawnRequested;
        /// <summary>Финал: мама позвала домой — открыт подъезд, сумеречные разбегаются.</summary>
        public static event Action MomCalled;

        public static void RaiseEnemyKilled(GameObject enemy, in HitInfo hit) => EnemyKilled?.Invoke(enemy, hit);
        public static void RaisePlayerDied(GameObject player) => PlayerDied?.Invoke(player);
        public static void PlaySound(SoundCue cue, Vector3 position) => SoundRequested?.Invoke(cue, position);
        public static void RaiseBossDefeated() => BossDefeated?.Invoke();
        public static void Announce(in Announcement announcement) => Announced?.Invoke(announcement);
        public static void RequestSpawn(in SpawnRequest request) => SpawnRequested?.Invoke(request);
        public static void RaiseMomCalled() => MomCalled?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            EnemyKilled = null;
            PlayerDied = null;
            SoundRequested = null;
            BossDefeated = null;
            Announced = null;
            SpawnRequested = null;
            MomCalled = null;
        }
    }

    /// <summary>Надпись мелом на экране (правило раунда, погода).</summary>
    public struct Announcement
    {
        /// <summary>Строка таблицы «UI».</summary>
        public string Title;
        /// <summary>Строка таблицы «UI» или пусто.</summary>
        public string Hint;
        /// <summary>Сколько секунд висит надпись; полоска под ней показывает, сколько осталось.</summary>
        public float Seconds;
    }

    /// <summary>Просьба выпустить группу врагов у определённой точки (скамейка запасных).</summary>
    public struct SpawnRequest
    {
        public GameObject Prefab;
        public int Count;
        public Vector3 Position;
        /// <summary>Шеренгой (солдатики) или кучкой.</summary>
        public bool Line;
    }
}
