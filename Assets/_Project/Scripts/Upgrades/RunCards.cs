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
            if (card)
                s_taken.Add(card);
        }

        static void Sync()
        {
            if (s_runId == RunState.RunId)
                return;
            s_runId = RunState.RunId;
            s_taken.Clear();
            s_locked.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_taken.Clear();
            s_locked.Clear();
            s_runId = -1;
        }
    }
}
