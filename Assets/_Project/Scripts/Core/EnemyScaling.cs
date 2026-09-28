using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Живучесть врагов по аренам: чем дальше прогулка, тем больше попаданий нужно. Множитель арены задаёт
    /// ArenaDirector; однохитовые враги (неваляшки, пупсы, цыплята) остаются однохитовыми — их берут числом.
    /// Элиткам достаётся половина прибавки арены: они и так втрое крепче обычных.
    /// Боссы растут только от уровня опасности.
    /// </summary>
    public static class EnemyScaling
    {
        /// <summary>Какая доля прибавки арены достаётся элиткам.</summary>
        const float EliteShare = 0.5f;

        /// <summary>Во сколько раз больше попаданий нужно врагам этой арены.</summary>
        public static float ArenaHits { get; set; } = 1f;

        /// <summary>Множитель элиток: половина прибавки арены (×1,5 → ×1,25, ×2 → ×1,5).</summary>
        public static float EliteHits => 1f + (ArenaHits - 1f) * EliteShare;

        /// <summary>Сколько попаданий нужно врагу на этой арене; элитке — с половиной прибавки.</summary>
        public static int Hits(int baseHits, GameObject enemy) =>
            Scale(baseHits, IsElite(enemy) ? EliteHits : ArenaHits);

        public static int BossHits(int baseHits) =>
            Mathf.Max(1, Mathf.CeilToInt(baseHits * Danger.BossHitsMultiplier - 0.001f));

        /// <summary>Элитка: портфель роняют только они.</summary>
        static bool IsElite(GameObject enemy) =>
            enemy && enemy.TryGetComponent(out EnemyReward reward) && reward.Portfolio;

        static int Scale(int baseHits, float multiplier) =>
            baseHits >= 2 ? Mathf.Max(baseHits, Mathf.CeilToInt(baseHits * multiplier - 0.001f)) : Mathf.Max(1, baseHits);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ArenaHits = 1f;
    }
}
