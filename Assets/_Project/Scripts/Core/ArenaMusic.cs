using UnityEngine;

namespace Bouncer.Core
{
    public static class ArenaMusic
    {
        public static bool FinalTheme { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => FinalTheme = false;
    }
}
