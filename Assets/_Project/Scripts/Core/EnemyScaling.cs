using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Живучесть врагов по аренам: чем дальше прогулка, тем больше попаданий нужно. Множитель арены задаёт
    /// ArenaDirector; однохитовые враги (неваляшки, пупсы, цыплята) остаются однохитовыми — их берут числом.
    /// Боссы растут только от уровня опасности.
    /// </summary>
    public static class EnemyScaling
    {
        /// <summary>Во сколько раз больше попаданий нужно врагам этой арены.</summary>
        public static float ArenaHits { get; set; } = 1f;

        public static int Hits(int baseHits) =>
            baseHits >= 2 ? Mathf.Max(baseHits, Mathf.CeilToInt(baseHits * ArenaHits - 0.001f)) : Mathf.Max(1, baseHits);

        public static int BossHits(int baseHits) =>
            Mathf.Max(1, Mathf.CeilToInt(baseHits * Danger.BossHitsMultiplier - 0.001f));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ArenaHits = 1f;
    }
}
