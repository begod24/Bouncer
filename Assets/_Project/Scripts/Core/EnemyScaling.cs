using UnityEngine;

namespace Bouncer.Core
{
    public static class EnemyScaling
    {
        const float EliteShare = 0.5f;

        static readonly float[] s_extraPerPlayer = { 0.5f, 0.65f, 0.8f };

        public static float ArenaHits { get; set; } = 1f;

        public static int Players { get; set; } = 1;
        public static int ArenaIndex { get; set; }

        public static float PlayerHits => 1f + Mathf.Max(0, Players - 1)
            * s_extraPerPlayer[Mathf.Clamp(ArenaIndex, 0, s_extraPerPlayer.Length - 1)];

        const float BossTempoPerPlayer = 0.15f;

        public static float BossCooldown => 1f / (1f + BossTempoPerPlayer * Mathf.Max(0, Players - 1));

        public static float EliteHits => 1f + (ArenaHits - 1f) * EliteShare;

        public static int Hits(int baseHits, GameObject enemy) =>
            Scale(baseHits, (IsElite(enemy) ? EliteHits : ArenaHits) * PlayerHits);

        public static int BossHits(int baseHits) =>
            Mathf.Max(1, Mathf.CeilToInt(baseHits * Danger.BossHitsMultiplier * PlayerHits - 0.001f));

        static bool IsElite(GameObject enemy) =>
            enemy && enemy.TryGetComponent(out EnemyReward reward) && reward.Portfolio;

        static int Scale(int baseHits, float multiplier) =>
            baseHits >= 2 ? Mathf.Max(baseHits, Mathf.CeilToInt(baseHits * multiplier - 0.001f)) : Mathf.Max(1, baseHits);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ArenaHits = 1f;
            Players = 1;
            ArenaIndex = 0;
        }
    }
}
