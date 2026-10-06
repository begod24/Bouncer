using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Живучесть врагов по аренам: чем дальше прогулка, тем больше попаданий нужно. Множитель арены задаёт
    /// ArenaDirector; однохитовые враги (неваляшки, пупсы, цыплята) остаются однохитовыми — их берут числом.
    /// Элиткам достаётся половина прибавки арены: они и так втрое крепче обычных.
    /// Боссы растут только от уровня опасности.
    /// В коопе каждый лишний игрок добавляет попаданий (кроме однохитовых): на первой арене +50%, на второй +65%,
    /// дальше +80% (<see cref="Players"/>, <see cref="PlayerHits"/>). И боссы с каждым лишним игроком чаще пускают
    /// в ход свои приёмы (<see cref="BossCooldown"/>).
    /// </summary>
    public static class EnemyScaling
    {
        /// <summary>Какая доля прибавки арены достаётся элиткам.</summary>
        const float EliteShare = 0.5f;

        static readonly float[] s_extraPerPlayer = { 0.5f, 0.65f, 0.8f };

        /// <summary>Во сколько раз больше попаданий нужно врагам этой арены.</summary>
        public static float ArenaHits { get; set; } = 1f;

        /// <summary>Сколько игроков в прогулке и какая по счёту арена (0 — первая): живучесть растёт с числом игроков.</summary>
        public static int Players { get; set; } = 1;
        public static int ArenaIndex { get; set; }

        /// <summary>Во сколько раз крепче враги от лишних игроков: 2 игрока на первой арене — ×1,5.</summary>
        public static float PlayerHits => 1f + Mathf.Max(0, Players - 1)
            * s_extraPerPlayer[Mathf.Clamp(ArenaIndex, 0, s_extraPerPlayer.Length - 1)];

        /// <summary>Насколько чаще приёмы босса с каждым лишним игроком: +15%.</summary>
        const float BossTempoPerPlayer = 0.15f;

        /// <summary>
        /// Множитель перезарядки приёмов босса: в соло 1, вдвоём ≈0,87, вчетвером ≈0,69 (свисток, прыжки, залпы чаще).
        /// </summary>
        public static float BossCooldown => 1f / (1f + BossTempoPerPlayer * Mathf.Max(0, Players - 1));

        /// <summary>Множитель элиток: половина прибавки арены (×1,5 → ×1,25, ×2 → ×1,5).</summary>
        public static float EliteHits => 1f + (ArenaHits - 1f) * EliteShare;

        /// <summary>Сколько попаданий нужно врагу на этой арене; элитке — с половиной прибавки.</summary>
        public static int Hits(int baseHits, GameObject enemy) =>
            Scale(baseHits, (IsElite(enemy) ? EliteHits : ArenaHits) * PlayerHits);

        public static int BossHits(int baseHits) =>
            Mathf.Max(1, Mathf.CeilToInt(baseHits * Danger.BossHitsMultiplier * PlayerHits - 0.001f));

        /// <summary>Элитка: портфель роняют только они.</summary>
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
