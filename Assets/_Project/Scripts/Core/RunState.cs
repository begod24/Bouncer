using System;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Забег-«прогулка» поверх сцен: номер арены, монетки, сердца и итоги переходят с арены на арену.
    /// Карточки забега хранит сборка Upgrades (RunCards) — Core о них не знает и только меняет <see cref="RunId"/>,
    /// когда начинается новая прогулка. Всё лежит в статике: смена сцены его не трогает, «Заново» и «В меню»
    /// начинают с чистого листа.
    /// Монетки, сердца, «Второе дыхание» и стартовая карточка — у каждого игрока свои, по номеру игрока (slot):
    /// в соло он один (номер 0), в коопе до <see cref="MaxPlayers"/>. Добыча с арены общая: кто бы ни подобрал
    /// монетку, каждый получает свою долю (<see cref="AddLoot"/>), а тратит каждый сам.
    /// </summary>
    public static class RunState
    {
        /// <summary>Больше игроков в одной прогулке не бывает.</summary>
        public const int MaxPlayers = 4;

        /// <summary>Сколько от добычи получает каждый игрок: в соло всё, в коопе — доля (вдвоём 70%, втроём 55%, вчетвером 45%).</summary>
        static readonly float[] s_lootShare = { 1f, 0.7f, 0.55f, 0.45f };

        /// <summary>Своё у каждого игрока прогулки.</summary>
        sealed class PlayerRun
        {
            public int Coins;
            /// <summary>Дробный остаток доли добычи: копится, пока не наберётся целая монетка.</summary>
            public float CoinCarry;
            /// <summary>Сколько сердец было в конце прошлой арены. 0 — не задано, начать с полными.</summary>
            public int Lives;
            public bool SecondWindUsed;
            public bool NeedsStartCard;
        }

        static readonly PlayerRun[] s_players = { new(), new(), new(), new() };

        public static bool Active { get; private set; }
        /// <summary>Номер прогулки: меняется с каждой новой, по нему остальные сбрасывают своё.</summary>
        public static int RunId { get; private set; }
        /// <summary>Сколько игроков в прогулке (1 — соло).</summary>
        public static int PlayerCount { get; private set; } = 1;
        /// <summary>Номер арены в прогулке: 0 — первая.</summary>
        public static int ArenaIndex { get; private set; }
        /// <summary>Какая арена этапа выбрана на развилке: 0 — основная, 1 и дальше — другие (см. RunDefinition).</summary>
        public static int ArenaVariant { get; private set; }
        /// <summary>По какой стрелке уходит игрок: станет <see cref="ArenaVariant"/> следующей арены.</summary>
        public static int NextVariant { get; set; }
        /// <summary>Новая сцена продолжает начатую прогулку: без заставки, карточки и сердца переносятся.</summary>
        public static bool ContinuesRun { get; private set; }
        /// <summary>Сцена первой арены: туда ведут «Заново» и «В меню». Пусто — перезагрузить текущую.</summary>
        public static string FirstScene { get; set; }

        /// <summary>Итоги прошлых арен; текущая арена считается в GameSession.</summary>
        public static float PastTime { get; private set; }
        public static int PastKills { get; private set; }
        /// <summary>Сколько монеток добыто за прогулку (добыча целиком, без деления на доли, и выручка ларька).</summary>
        public static int CoinsEarned { get; private set; }
        /// <summary>Уровень опасности прогулки (1–5), см. <see cref="Bouncer.Core.Danger"/>.</summary>
        public static int Danger { get; private set; } = 1;
        /// <summary>
        /// Часы прогулки для таймера и рекордов: всё время с первой арены, кроме паузы и затемнений между аренами.
        /// </summary>
        public static float RunClock { get; set; }

        /// <summary>Монеток у игрока стало больше или меньше: номер игрока и изменение (может быть отрицательным).</summary>
        public static event Action<int, int> CoinsChanged;

        /// <summary>Доля добычи каждого игрока при нынешнем числе игроков.</summary>
        public static float LootShare => s_lootShare[Mathf.Clamp(PlayerCount, 1, MaxPlayers) - 1];

        static PlayerRun Of(int slot) => s_players[Mathf.Clamp(slot, 0, MaxPlayers - 1)];

        public static int CoinsOf(int slot) => Of(slot).Coins;

        /// <summary>Сколько сердец было у игрока в конце прошлой арены. 0 — не задано, начать с полными.</summary>
        public static int LivesOf(int slot) => Of(slot).Lives;

        public static void SetLives(int slot, int lives) => Of(slot).Lives = Mathf.Max(0, lives);

        /// <summary>«Второе дыхание» уже спасло игрока в этой прогулке (действует раз за прогулку).</summary>
        public static bool SecondWindUsed(int slot) => Of(slot).SecondWindUsed;

        public static void UseSecondWind(int slot) => Of(slot).SecondWindUsed = true;

        /// <summary>В начале прогулки игрок ещё не выбрал стартовую карточку.</summary>
        public static bool NeedsStartCard(int slot) => Active && Of(slot).NeedsStartCard;

        public static void StartCardChosen(int slot) => Of(slot).NeedsStartCard = false;

        /// <summary>Новая прогулка с первой арены.</summary>
        public static void BeginNew(int players = 1)
        {
            Active = true;
            RunId++;
            PlayerCount = Mathf.Clamp(players, 1, MaxPlayers);
            ArenaIndex = 0;
            ArenaVariant = 0;
            NextVariant = 0;
            ResetPlayers(needStartCard: true);
            ContinuesRun = false;
            PastTime = 0f;
            PastKills = 0;
            CoinsEarned = 0;
            Danger = Bouncer.Core.Danger.Selected;
            RunClock = 0f;
            for (int slot = 0; slot < MaxPlayers; slot++)
                CoinsChanged?.Invoke(slot, 0);
        }

        /// <summary>
        /// Сетевая прогулка: все в комнате начинают её вместе, на опасности, которую выбрал хозяин. Первая арена
        /// грузится по сети уже как продолжение прогулки — сразу бой, без заставки.
        /// Стартовой карточки пока нет: карточки по сети появятся вместе с коопом.
        /// </summary>
        public static void BeginOnline(int players, int danger)
        {
            BeginNew(players);
            ResetPlayers(needStartCard: false);
            Danger = Mathf.Clamp(danger, 1, Bouncer.Core.Danger.Max);
            ContinuesRun = true;
        }

        /// <summary>Обучение: прогулка с первой арены на первой опасности, без стартовой карточки — её даст шаг обучения.</summary>
        public static void BeginTutorial()
        {
            BeginNew();
            ResetPlayers(needStartCard: false);
            Danger = 1;
        }

        /// <summary>Прогулки нет (заставка) — следующая начнётся заново.</summary>
        public static void Clear()
        {
            Active = false;
            PlayerCount = 1;
            ArenaIndex = 0;
            ArenaVariant = 0;
            NextVariant = 0;
            ResetPlayers(needStartCard: false);
            ContinuesRun = false;
            PastTime = 0f;
            PastKills = 0;
            CoinsEarned = 0;
            RunClock = 0f;
        }

        static void ResetPlayers(bool needStartCard)
        {
            foreach (var player in s_players)
            {
                player.Coins = 0;
                player.CoinCarry = 0f;
                player.Lives = 0;
                player.SecondWindUsed = false;
                player.NeedsStartCard = needStartCard;
            }
        }

        /// <summary>Сцена отдельной арены запущена сама (редактор): прогулка начинается с неё, тоже со стартовой карточкой.</summary>
        public static void BeginAt(int arenaIndex, int variant = 0)
        {
            BeginNew();
            ArenaIndex = Mathf.Max(0, arenaIndex);
            ArenaVariant = Mathf.Max(0, variant);
        }

        /// <summary>Монетки лично игроку: выручка за карточку, «Копилка».</summary>
        public static void AddCoins(int slot, int amount)
        {
            if (amount <= 0)
                return;
            Of(slot).Coins += amount;
            CoinsEarned += amount;
            CoinsChanged?.Invoke(slot, amount);
        }

        /// <summary>
        /// Добыча с арены (монетки с врагов, находки): общая — каждый игрок прогулки получает свою долю
        /// (<see cref="LootShare"/>), дробные остатки копятся до целой монетки. В соло всё достаётся игроку.
        /// </summary>
        public static void AddLoot(int amount)
        {
            if (amount <= 0)
                return;
            CoinsEarned += amount;
            float share = amount * LootShare;
            for (int slot = 0; slot < PlayerCount; slot++)
            {
                var player = s_players[slot];
                float total = player.CoinCarry + share;
                int whole = Mathf.FloorToInt(total + 1e-4f);
                player.CoinCarry = Mathf.Max(0f, total - whole);
                if (whole <= 0)
                    continue;
                player.Coins += whole;
                CoinsChanged?.Invoke(slot, whole);
            }
        }

        public static bool TrySpend(int slot, int amount)
        {
            var player = Of(slot);
            if (amount < 0 || amount > player.Coins)
                return false;
            player.Coins -= amount;
            CoinsChanged?.Invoke(slot, -amount);
            return true;
        }

        /// <summary>Арена пройдена, дальше следующая: запомнить её итоги. Сердца игроков записывает <see cref="SetLives"/>.</summary>
        public static void AdvanceArena(float arenaTime, int arenaKills)
        {
            PastTime += arenaTime;
            PastKills += arenaKills;
            ArenaIndex++;
            ArenaVariant = Mathf.Max(0, NextVariant);
            NextVariant = 0;
            ContinuesRun = true;
        }

        /// <summary>Новая сцена приняла продолжение прогулки.</summary>
        public static void ConsumeContinuation() => ContinuesRun = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Clear();
            RunId = 0;
            FirstScene = null;
            CoinsChanged = null;
        }
    }
}
