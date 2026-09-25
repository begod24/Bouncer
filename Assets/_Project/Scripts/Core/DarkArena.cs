using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Тёмная арена (стройка в сумерках): видно только в свете фонарей и в небольшом круге вокруг игрока.
    /// Сам компонент лежит в сцене; по его флагу игрок включает свой круг света.
    /// </summary>
    public sealed class DarkArena : MonoBehaviour
    {
        static int s_count;

        /// <summary>В сцене есть включённая тёмная арена.</summary>
        public static bool Active => s_count > 0;

        void OnEnable() => s_count++;

        void OnDisable() => s_count = Mathf.Max(0, s_count - 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_count = 0;
    }
}
