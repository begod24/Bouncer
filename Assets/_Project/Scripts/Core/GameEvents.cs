using System;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>Глобальные игровые события, на которые подписываются сессия, HUD и т.п.</summary>
    public static class GameEvents
    {
        public static event Action<GameObject, HitInfo> EnemyKilled;
        public static event Action<GameObject> PlayerDied;

        public static void RaiseEnemyKilled(GameObject enemy, in HitInfo hit) => EnemyKilled?.Invoke(enemy, hit);
        public static void RaisePlayerDied(GameObject player) => PlayerDied?.Invoke(player);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            EnemyKilled = null;
            PlayerDied = null;
        }
    }
}
