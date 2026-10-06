using UnityEngine;

namespace Bouncer.Core
{
    public static class Tutorial
    {
        const string OfferedKey = "bouncer.tutorial.offered";
        const string DoneKey = "bouncer.tutorial.done";

        static bool s_requested;

        public static bool Active { get; private set; }

        public static bool OpenKidsOnTitle { get; set; }

        public static bool Completed => PlayerPrefs.GetInt(DoneKey, 0) == 1;

        public static bool ShouldAsk => PlayerPrefs.GetInt(OfferedKey, 0) == 0 && !Completed;

        public static void Request() => s_requested = true;

        public static bool Begin()
        {
            Active = s_requested;
            s_requested = false;
            return Active;
        }

        public static void MarkOffered()
        {
            PlayerPrefs.SetInt(OfferedKey, 1);
            PlayerPrefs.Save();
        }

        public static void MarkCompleted()
        {
            PlayerPrefs.SetInt(OfferedKey, 1);
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_requested = false;
            Active = false;
            OpenKidsOnTitle = false;
        }
    }
}
