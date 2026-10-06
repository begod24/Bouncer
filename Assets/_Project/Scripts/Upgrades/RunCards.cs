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
    /// </summary>
    public static class RunCards
    {
        sealed class PlayerRunCards
        {
            public readonly List<UpgradeCard> Taken = new();
            public readonly List<UpgradeCard> Locked = new();
            public int Combos;

            public void Clear()
            {
                Taken.Clear();
                Locked.Clear();
                Combos = 0;
            }
        }

        static readonly PlayerRunCards[] s_players = { new(), new(), new(), new() };
        static int s_runId = -1;
        static int s_golds;

        /// <summary>Сколько золотых (не комбо) взято за прогулку всей командой — даже если потом выкинуты.</summary>
        public static int GoldsTaken
        {
            get
            {
                Sync();
                return s_golds;
            }
        }

        /// <summary>Сколько комбо собрал игрок за прогулку — даже если потом выкинуты.</summary>
        public static int CombosTaken(int slot) => Of(slot).Combos;

        /// <summary>Взятые игроком карточки по порядку.</summary>
        public static IReadOnlyList<UpgradeCard> Taken(int slot) => Of(slot).Taken;

        /// <summary>Жвачки, отложенные игроком на витрине: ждут в следующем ларьке.</summary>
        public static List<UpgradeCard> Locked(int slot) => Of(slot).Locked;

        public static void Record(int slot, UpgradeCard card)
        {
            var player = Of(slot);
            if (!card)
                return;
            player.Taken.Add(card);
            if (card.IsCombo)
                player.Combos++;
            else if (card.rarity == CardRarity.Gold)
                s_golds++;
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
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            foreach (var player in s_players)
                player.Clear();
            s_runId = -1;
            s_golds = 0;
        }
    }
}
