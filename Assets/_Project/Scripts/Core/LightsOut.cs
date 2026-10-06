using UnityEngine;

namespace Bouncer.Core
{
    public static class LightsOut
    {
        const float FadeIn = 0.5f;
        const float FadeOut = 1.2f;

        static float s_start;
        static float s_until;

        public static event System.Action<float> Triggered;

        public static bool Active => Time.time < s_until;

        public static float Dark01
        {
            get
            {
                float now = Time.time;
                if (now >= s_until + FadeOut)
                    return 0f;
                float enter = Mathf.Clamp01((now - s_start) / FadeIn);
                float exit = now < s_until ? 1f : 1f - (now - s_until) / FadeOut;
                return Mathf.Clamp01(Mathf.Min(enter, exit));
            }
        }

        public static void Trigger(float seconds)
        {
            if (seconds <= 0f)
                return;
            float now = Time.time;
            if (now >= s_until)
                s_start = now;
            s_until = Mathf.Max(s_until, now + seconds);
            foreach (var zone in LightZone.All)
                zone.PutOut(seconds);
            if (!NetHooks.IsGuest)
                Triggered?.Invoke(seconds);
        }

        public static void End()
        {
            float now = Time.time;
            if (now >= s_until)
                return;
            s_until = now;
            foreach (var zone in LightZone.All)
                zone.Relight();
        }

        public static void Clear()
        {
            s_start = 0f;
            s_until = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Clear();
            Triggered = null;
        }
    }
}
