using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Upgrades
{
    /// <summary>
    /// Карточки прогулки поверх сцен: какие взяты (по порядку — на новой арене они применяются заново)
    /// и какие жвачки отложены на витрине до следующего ларька. Сбрасывается сам, когда начинается новая
    /// прогулка (<see cref="RunState.RunId"/>).
    /// </summary>
    public static class RunCards
    {
        static readonly List<UpgradeCard> s_taken = new();
        static readonly List<UpgradeCard> s_locked = new();
        static int s_runId = -1;
        static int s_golds;
        static int s_combos;

        /// <summary>Сколько золотых (не комбо) взято за прогулку — даже если потом выкинуты.</summary>
        public static int GoldsTaken
        {
            get
            {
                Sync();
                return s_golds;
            }
        }

        /// <summary>Сколько комбо собрано за прогулку — даже если потом выкинуты.</summary>
        public static int CombosTaken
        {
            get
            {
                Sync();
                return s_combos;
            }
        }

        /// <summary>Взятые карточки по порядку.</summary>
        public static IReadOnlyList<UpgradeCard> Taken
        {
            get
            {
                Sync();
                return s_taken;
            }
        }

        /// <summary>Жвачки, отложенные на витрине: ждут в следующем ларьке.</summary>
        public static List<UpgradeCard> Locked
        {
            get
            {
                Sync();
                return s_locked;
            }
        }

        public static void Record(UpgradeCard card)
        {
            Sync();
            if (!card)
                return;
            s_taken.Add(card);
            if (card.IsCombo)
                s_combos++;
            else if (card.rarity == CardRarity.Gold)
                s_golds++;
        }

        /// <summary>Выкинуть карточку из взятых (все её повторы): на следующих аренах она больше не применяется.</summary>
        public static void Remove(UpgradeCard card)
        {
            Sync();
            s_taken.RemoveAll(taken => taken == card);
        }

        static void Sync()
        {
            if (s_runId == RunState.RunId)
                return;
            s_runId = RunState.RunId;
            s_taken.Clear();
            s_locked.Clear();
            s_golds = 0;
            s_combos = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_taken.Clear();
            s_locked.Clear();
            s_runId = -1;
            s_golds = 0;
            s_combos = 0;
        }
    }
}
