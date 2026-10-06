using UnityEngine;

namespace Bouncer.Core
{
    public interface IOnlineSession
    {
        bool IsHost { get; }

        void Leave();
    }

    public static class Online
    {
        public static bool Active { get; set; }

        public static IOnlineSession Session { get; set; }

        public static bool IsHost => !Active || Session == null || Session.IsHost;

        public static bool WavesHeld { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = false;
            Session = null;
            WavesHeld = false;
        }
    }
}
