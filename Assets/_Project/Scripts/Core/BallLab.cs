using UnityEngine;

namespace Bouncer.Core
{
    // «Площадка мячей»: двор без волн, где бросаешь любые мячи по мишеням и смотришь урон. Инструмент для баланса.
    public static class BallLab
    {
        const string EditorKey = "bouncer.balllab.request";

        static bool s_requested;

        public static bool Active { get; private set; }

        public static bool PointerOverPanel { get; set; }

        public static bool Available => Application.isEditor || Debug.isDebugBuild;

        public static void Request() => s_requested = true;

        public static bool Begin()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool(EditorKey, false))
            {
                UnityEditor.SessionState.EraseBool(EditorKey);
                s_requested = true;
            }
#endif
            Active = s_requested;
            s_requested = false;
            PointerOverPanel = false;
            return Active;
        }

#if UNITY_EDITOR
        public static void RequestFromEditor() => UnityEditor.SessionState.SetBool(EditorKey, true);
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_requested = false;
            Active = false;
            PointerOverPanel = false;
        }
    }
}
