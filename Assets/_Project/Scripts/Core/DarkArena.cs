using UnityEngine;

namespace Bouncer.Core
{
    public sealed class DarkArena : MonoBehaviour
    {
        static int s_count;

        public static bool Active => s_count > 0 || LightsOut.Active;

        void OnEnable() => s_count++;

        void OnDisable() => s_count = Mathf.Max(0, s_count - 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_count = 0;
    }
}
