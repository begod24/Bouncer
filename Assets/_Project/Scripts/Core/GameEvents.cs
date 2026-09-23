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

        public static void RaiseEnemyKilled(GameObject enemy, in HitInfo hit) => EnemyKilled?.Invoke(enemy, hit);
        public static void RaisePlayerDied(GameObject player) => PlayerDied?.Invoke(player);
        public static void PlaySound(SoundCue cue, Vector3 position) => SoundRequested?.Invoke(cue, position);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            EnemyKilled = null;
            PlayerDied = null;
            SoundRequested = null;
        }
    }
}
