using System;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Забег-«прогулка» поверх сцен: номер арены, монетки, сердца и итоги переходят с арены на арену.
    /// Карточки забега хранит сборка Upgrades (RunCards) — Core о них не знает и только меняет <see cref="RunId"/>,
    /// когда начинается новая прогулка. Всё лежит в статике: смена сцены его не трогает, «Заново» и «В меню»
    /// начинают с чистого листа.
    /// </summary>
    public static class RunState
    {
        public static bool Active { get; private set; }
        /// <summary>Номер прогулки: меняется с каждой новой, по нему остальные сбрасывают своё.</summary>
        public static int RunId { get; private set; }
        /// <summary>Номер арены в прогулке: 0 — первая.</summary>
        public static int ArenaIndex { get; private set; }
        public static int Coins { get; private set; }
        /// <summary>Сколько сердец было у игрока в конце прошлой арены. 0 — не задано, начать с полными.</summary>
        public static int Lives { get; set; }
        /// <summary>Новая сцена продолжает начатую прогулку: без заставки, карточки и сердца переносятся.</summary>
        public static bool ContinuesRun { get; private set; }
        /// <summary>В начале прогулки ещё не выбрана стартовая карточка.</summary>
        public static bool NeedsStartCard { get; set; }
        /// <summary>Сцена первой арены: туда ведут «Заново» и «В меню». Пусто — перезагрузить текущую.</summary>
        public static string FirstScene { get; set; }

        /// <summary>Итоги прошлых арен; текущая арена считается в GameSession.</summary>
        public static float PastTime { get; private set; }
        public static int PastKills { get; private set; }
        public static int CoinsEarned { get; private set; }

        /// <summary>Монеток стало больше или меньше: изменение (может быть отрицательным).</summary>
        public static event Action<int> CoinsChanged;

        /// <summary>Новая прогулка с первой арены.</summary>
        public static void BeginNew()
        {
            Active = true;
            RunId++;
            ArenaIndex = 0;
            Coins = 0;
            Lives = 0;
            ContinuesRun = false;
            NeedsStartCard = true;
            PastTime = 0f;
            PastKills = 0;
            CoinsEarned = 0;
            CoinsChanged?.Invoke(0);
        }

        /// <summary>Прогулки нет (заставка) — следующая начнётся заново.</summary>
        public static void Clear()
        {
            Active = false;
            ArenaIndex = 0;
            Coins = 0;
            Lives = 0;
            ContinuesRun = false;
            NeedsStartCard = false;
            PastTime = 0f;
            PastKills = 0;
            CoinsEarned = 0;
        }

        /// <summary>Сцена отдельной арены запущена сама (редактор): прогулка начинается с неё, тоже со стартовой карточкой.</summary>
        public static void BeginAt(int arenaIndex)
        {
            BeginNew();
            ArenaIndex = Mathf.Max(0, arenaIndex);
        }

        public static void AddCoins(int amount)
        {
            if (amount <= 0)
                return;
            Coins += amount;
            CoinsEarned += amount;
            CoinsChanged?.Invoke(amount);
        }

        public static bool TrySpend(int amount)
        {
            if (amount < 0 || amount > Coins)
                return false;
            Coins -= amount;
            CoinsChanged?.Invoke(-amount);
            return true;
        }

        /// <summary>Арена пройдена, дальше следующая: запомнить её итоги и сердца игрока.</summary>
        public static void AdvanceArena(float arenaTime, int arenaKills, int lives)
        {
            PastTime += arenaTime;
            PastKills += arenaKills;
            Lives = lives;
            ArenaIndex++;
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
