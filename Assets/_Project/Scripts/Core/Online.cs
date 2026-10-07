using UnityEngine;

namespace Bouncer.Core
{
    public interface IOnlineSession
    {
        bool IsHost { get; }

        double HostTime { get; }

        void Leave();
    }

    public static class Online
    {
        public static bool Active { get; set; }

        public static IOnlineSession Session { get; set; }

        public static bool IsHost => !Active || Session == null || Session.IsHost;

        public static bool WavesHeld { get; set; }

        public static bool Joining { get; set; }

        public static bool TryHostTime(out double time)
        {
            time = 0d;
            if (!Active || Session == null)
                return false;
            time = Session.HostTime;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = false;
            Session = null;
            WavesHeld = false;
            Joining = false;
        }
    }
}
