using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// «Гасит свет» финального босса: на время гаснут фонари, окна и луна, двор тонет в темноте, а у игрока
    /// загорается круг света, как на стройке (<see cref="DarkArena.Active"/>). Фонари гаснут здесь же
    /// (<see cref="LightZone.PutOut"/>), луну и окна приглушают те, кто их ведёт, — по <see cref="Dark01"/>.
    /// </summary>
    public static class LightsOut
    {
        /// <summary>За сколько секунд двор гаснет и за сколько свет возвращается.</summary>
        const float FadeIn = 0.5f;
        const float FadeOut = 1.2f;

        static float s_start;
        static float s_until;

        /// <summary>Свет погашен (или ещё гаснет).</summary>
        public static bool Active => Time.time < s_until;

        /// <summary>Сила темноты для экрана: 0 — свет горит, 1 — темно. Плавно входит и выходит.</summary>
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

        /// <summary>Погасить весь свет на столько секунд: фонари мигают и тухнут, потом загораются снова.</summary>
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
        }

        /// <summary>Вернуть свет сейчас же (мама позвала — во дворе зажигается всё).</summary>
        public static void End()
        {
            float now = Time.time;
            if (now >= s_until)
                return;
            s_until = now;
            foreach (var zone in LightZone.All)
                zone.Relight();
        }

        /// <summary>Новая сцена: темнота прошлого боя в неё не переходит.</summary>
        public static void Clear()
        {
            s_start = 0f;
            s_until = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Clear();
    }
}
