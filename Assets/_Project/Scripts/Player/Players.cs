using System;
using System.Collections.Generic;

namespace Bouncer.Player
{
    /// <summary>
    /// Все игроки на арене — чтобы не искать их через Find. В соло игрок один; в коопе и PvP их несколько,
    /// и только часть из них — за этим компьютером. <see cref="Local"/> — тот, за кого играют здесь:
    /// за ним следит камера, его показывают HUD, карманы и ларёк.
    /// </summary>
    public static class Players
    {
        static readonly List<PlayerController> s_all = new();

        public static IReadOnlyList<PlayerController> All => s_all;
        /// <summary>Игрок за этим компьютером (первый, если их здесь несколько). null — пока не появился.</summary>
        public static PlayerController Local { get; private set; }

        /// <summary>Появился или сменился свой игрок (null — ушёл).</summary>
        public static event Action<PlayerController> LocalChanged;

        /// <summary>Игрок с этим номером. null — такого нет.</summary>
        public static PlayerController InSlot(int slot)
        {
            foreach (var player in s_all)
                if (player.Slot == slot)
                    return player;
            return null;
        }

        internal static void Add(PlayerController player)
        {
            if (!s_all.Contains(player))
                s_all.Add(player);
            Refresh();
        }

        internal static void Remove(PlayerController player)
        {
            s_all.Remove(player);
            Refresh();
        }

        /// <summary>Пересчитать своего игрока: кто-то пришёл, ушёл или стал своим.</summary>
        internal static void Refresh()
        {
            PlayerController local = null;
            foreach (var player in s_all)
            {
                if (!player.IsLocal)
                    continue;
                if (local == null || player.Slot < local.Slot)
                    local = player;
            }
            if (local == Local)
                return;
            Local = local;
            Bouncer.Core.Targetable.LocalPlayer = local != null ? local.Targetable : null;
            LocalChanged?.Invoke(local);
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_all.Clear();
            Local = null;
            LocalChanged = null;
        }
    }
}
