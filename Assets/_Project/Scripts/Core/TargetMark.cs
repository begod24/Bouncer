using UnityEngine;

namespace Bouncer.Core
{
    public static class TargetMark
    {
        static float s_until;
        static int s_bonus;

        public static Targetable Target { get; private set; }
        public static bool Active => Target != null && Target.IsAlive && Time.time < s_until;
        public static int Bonus => Active ? s_bonus : 0;
        public static float Left => Active ? s_until - Time.time : 0f;

        public static event System.Action<Targetable, float> Marked;

        public static void Mark(Targetable target, float seconds, int bonus)
        {
            if (target == null || seconds <= 0f)
                return;
            Target = target;
            s_until = Time.time + seconds;
            s_bonus = Mathf.Max(0, bonus);
            Marked?.Invoke(target, seconds);
        }

        public static void Clear()
        {
            Target = null;
            s_until = 0f;
            s_bonus = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Clear();
            Marked = null;
        }
    }
}
