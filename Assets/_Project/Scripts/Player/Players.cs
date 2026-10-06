using System;
using System.Collections.Generic;

namespace Bouncer.Player
{
    public static class Players
    {
        static readonly List<PlayerController> s_all = new();

        public static IReadOnlyList<PlayerController> All => s_all;
        public static PlayerController Local { get; private set; }

        public static event Action<PlayerController> LocalChanged;

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
