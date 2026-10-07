using System;
using UnityEngine;

namespace Bouncer.Player
{
    public enum PlayerFxKind : byte
    {
        GumBubblePop,
        TamagotchiSave,
        PassCatch,
        CountThree,
        CircleGuard,
        SeaFigure,
        PerfectCatch,
    }

    public static class PlayerFx
    {
        public static event Action<PlayerController, PlayerFxKind, Vector3, Vector3> Played;
        public static event Action<PlayerController, PlayerFxKind, Vector3, Vector3> Sent;

        public static void Play(PlayerController player, PlayerFxKind kind, Vector3 a, Vector3 b = default, bool relay = true)
        {
            if (player == null)
                return;
            Played?.Invoke(player, kind, a, b);
            if (relay && player.IsLocal)
                Sent?.Invoke(player, kind, a, b);
        }

        public static void Replay(PlayerController player, PlayerFxKind kind, Vector3 a, Vector3 b)
        {
            if (player != null)
                Played?.Invoke(player, kind, a, b);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Played = null;
            Sent = null;
        }
    }
}
