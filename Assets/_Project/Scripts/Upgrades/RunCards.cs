using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Upgrades
{
    /// <summary>
    /// Карточки прогулки поверх сцен: какие взяты (по порядку — на новой арене они применяются заново)
    /// и какие жвачки отложены на витрине до следующего ларька. У каждого игрока свои, по его номеру (slot);
    /// золотые считаются на всю команду — в коопе золотая одна на всех. Сбрасывается сам, когда начинается новая
    /// прогулка (<see cref="RunState.RunId"/>).
    /// По сети каждый компьютер знает только карточки своего игрока, а сколько золотых взяла вся команда, приносит
    /// сеть (<see cref="SharedGolds"/>, <see cref="GoldRecorded"/>).
    /// </summary>
    public static class RunCards
    {
        sealed class PlayerRunCards
        {
            public readonly List<UpgradeCard> Taken = new();
            public readonly List<UpgradeCard> Locked = new();
            /// <summary>Выборы «1 из 3», которые не успели открыть на прошлой арене (кооп: был выбит).</summary>
            public readonly List<OfferKind> Carried = new();
            public int Combos;
            public int Backpack;

            public void Clear()
            {
                Taken.Clear();
                Locked.Clear();
                Carried.Clear();
                Combos = 0;
                Backpack = 0;
            }
        }

        static readonly PlayerRunCards[] s_players = { new(), new(), new(), new() };
        static int s_runId = -1;
        static int s_golds;
        static int s_sharedGolds;

        /// <summary>Сколько золотых (не комбо) взято за прогулку всей командой — даже если потом выкинуты.</summary>
        public static int GoldsTaken
        {
            get
            {
                Sync();
                return Mathf.Max(s_golds, s_sharedGolds);
            }
        }

        /// <summary>По сети: сколько золотых взяла вся команда (считает хозяин комнаты).</summary>
        public static int SharedGolds
        {
            get
            {
                Sync();
                return s_sharedGolds;
            }
            set
            {
                Sync();
                s_sharedGolds = Mathf.Max(0, value);
            }
        }

        /// <summary>Взята золотая карточка (не комбо): по сети об этом узнаёт вся команда.</summary>
        public static event System.Action GoldRecorded;

        /// <summary>Сколько комбо собрал игрок за прогулку — даже если потом выкинуты.</summary>
        public static int CombosTaken(int slot) => Of(slot).Combos;

        /// <summary>Взятые игроком карточки по порядку.</summary>
        public static IReadOnlyList<UpgradeCard> Taken(int slot) => Of(slot).Taken;

        /// <summary>Жвачки, отложенные игроком на витрине: ждут в следующем ларьке.</summary>
        public static List<UpgradeCard> Locked(int slot) => Of(slot).Locked;

        /// <summary>Выборы, перенесённые с прошлой арены: откроются на этой.</summary>
        public static List<OfferKind> Carried(int slot) => Of(slot).Carried;

        /// <summary>Кооп: сколько неоткрытых портфелей у игрока в рюкзаке (переходят с арены на арену).</summary>
        public static int BackpackOf(int slot) => Of(slot).Backpack;

        public static void SetBackpack(int slot, int count) => Of(slot).Backpack = Mathf.Max(0, count);

        public static void Record(int slot, UpgradeCard card)
        {
            var player = Of(slot);
            if (!card)
                return;
            player.Taken.Add(card);
            if (card.IsCombo)
            {
                player.Combos++;
            }
            else if (card.rarity == CardRarity.Gold)
            {
                s_golds++;
                GoldRecorded?.Invoke();
            }
        }

        /// <summary>Выкинуть карточку из взятых (все её повторы): на следующих аренах она больше не применяется.</summary>
        public static void Remove(int slot, UpgradeCard card) => Of(slot).Taken.RemoveAll(taken => taken == card);

        static PlayerRunCards Of(int slot)
        {
            Sync();
            return s_players[Mathf.Clamp(slot, 0, s_players.Length - 1)];
        }

        static void Sync()
        {
            if (s_runId == RunState.RunId)
                return;
            s_runId = RunState.RunId;
            foreach (var player in s_players)
                player.Clear();
            s_golds = 0;
            s_sharedGolds = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            foreach (var player in s_players)
                player.Clear();
            s_runId = -1;
            s_golds = 0;
            s_sharedGolds = 0;
            GoldRecorded = null;
        }
    }
}
