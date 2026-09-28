using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Какая музыка у текущей арены: случайный игровой трек или своя тема финала (с Бабаем).
    /// Задаёт ArenaDirector по данным арены, выбирает трек MusicPlayer.
    /// </summary>
    public static class ArenaMusic
    {
        /// <summary>На арене играет тема финала, а не случайный игровой трек.</summary>
        public static bool FinalTheme { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => FinalTheme = false;
    }
}
