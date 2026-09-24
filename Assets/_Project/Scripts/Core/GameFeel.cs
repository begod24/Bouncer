using Unity.Cinemachine;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// «Сок»: стоп-кадр, тряска камеры, замедление. Единственное место,
    /// которое пишет Time.timeScale — пауза и эффекты не спорят друг с другом.
    /// Стоп-кадр останавливает всю игру, поэтому он только для редких важных моментов (игрока ударили,
    /// босс раскололся, ловля). На обычные попадания вздрагивает сам враг (<see cref="HitPunch"/>).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameFeel : MonoBehaviour
    {
        /// <summary>За сколько секунд «Замри!» входит в полную силу и за сколько отпускает.</summary>
        const float BulletTimeEnter = 0.06f;
        const float BulletTimeExit = 0.3f;

        [SerializeField] CinemachineImpulseSource impulseSource;
        [Tooltip("Скорость времени во время стоп-кадра")]
        [SerializeField, Range(0f, 1f)] float hitStopTimeScale = 0.03f;
        [Tooltip("Новый стоп-кадр не короче прошлого — не раньше, чем через столько секунд после него: " +
                 "стоп-кадры подряд выглядят как подвисания")]
        [SerializeField, Min(0f)] float hitStopCooldown = 0.35f;
        [Tooltip("Общий множитель тряски камеры")]
        [SerializeField, Min(0f)] float shakeMultiplier = 1f;
        [Tooltip("Тряски, пришедшие почти разом (пробил толпу, взрыв задел многих), не складываются: " +
                 "за это окно, с, остаётся только самая сильная")]
        [SerializeField, Min(0f)] float shakeWindow = 0.1f;

        static GameFeel s_instance;
        static float s_hitStopStart;
        static float s_hitStopUntil;
        static float s_hitStopLength;
        static float s_shakeWindowUntil;
        static float s_shakeWindowPeak;
        static float s_slowMoUntil;
        static float s_slowMoScale = 1f;
        static float s_bulletTimeStart;
        static float s_bulletTimeUntil;
        static float s_bulletTimeScale = 1f;

        public static bool Paused { get; set; }
        /// <summary>Время стоит, но это не пауза: открыт выбор карточки и т.п.</summary>
        public static bool Frozen { get; set; }

        /// <summary>Сила замедления «Замри!»: 0 — нет, 1 — в разгаре. Плавно входит и выходит.</summary>
        public static float BulletTime01
        {
            get
            {
                float now = Time.unscaledTime;
                if (now >= s_bulletTimeUntil)
                    return 0f;
                float enter = Mathf.Clamp01((now - s_bulletTimeStart) / BulletTimeEnter);
                float exit = Mathf.Clamp01((s_bulletTimeUntil - now) / BulletTimeExit);
                return Mathf.Min(enter, exit);
            }
        }

        void Awake()
        {
            s_instance = this;
            // Замедления прошлого забега (конец игры, «Замри!») в новый не переходят.
            s_hitStopUntil = 0f;
            s_slowMoUntil = 0f;
            s_bulletTimeUntil = 0f;
        }

        void OnDestroy()
        {
            if (s_instance != this)
                return;
            s_instance = null;
            Time.timeScale = 1f;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            float scale = 1f;
            if (now < s_slowMoUntil)
                scale *= s_slowMoScale;
            scale *= Mathf.Lerp(1f, s_bulletTimeScale, BulletTime01);
            if (now < s_hitStopUntil)
                scale *= hitStopTimeScale;
            Time.timeScale = Paused || Frozen ? 0f : scale;
        }

        public static void HitStop(float seconds)
        {
            if (seconds <= 0f)
                return;
            float now = Time.unscaledTime;
            if (now < s_hitStopUntil)
            {
                // Стоп-кадры не складываются: идущий длится столько, сколько самый долгий из запрошенных.
                s_hitStopLength = Mathf.Max(s_hitStopLength, seconds);
                s_hitStopUntil = s_hitStopStart + s_hitStopLength;
                return;
            }
            float cooldown = s_instance != null ? s_instance.hitStopCooldown : 0f;
            if (now < s_hitStopUntil + cooldown && seconds <= s_hitStopLength)
                return;
            s_hitStopStart = now;
            s_hitStopLength = seconds;
            s_hitStopUntil = now + seconds;
        }

        public static void SlowMotion(float scale, float seconds)
        {
            s_slowMoScale = scale;
            s_slowMoUntil = Time.unscaledTime + seconds;
        }

        /// <summary>
        /// «Замри!»: всё вокруг замедляется до scale на seconds секунд реального времени.
        /// Новый вызов, пока замедление идёт, продлевает его, а не начинает заново.
        /// </summary>
        public static void BulletTime(float scale, float seconds)
        {
            if (seconds <= 0f)
                return;
            float now = Time.unscaledTime;
            if (now >= s_bulletTimeUntil)
                s_bulletTimeStart = now;
            s_bulletTimeScale = Mathf.Clamp(scale, 0.05f, 1f);
            s_bulletTimeUntil = Mathf.Max(s_bulletTimeUntil, now + seconds);
        }

        public static void Shake(float force)
        {
            if (s_instance == null || s_instance.impulseSource == null)
                return;
            // Множитель из настроек игрока — поверх общего из инспектора.
            force *= s_instance.shakeMultiplier * GameSettings.ScreenShake;
            if (force <= 0f)
                return;
            // Внутри окна добавляется только то, на что новая тряска сильнее уже идущей.
            float now = Time.unscaledTime;
            if (now < s_shakeWindowUntil)
            {
                if (force <= s_shakeWindowPeak)
                    return;
                (force, s_shakeWindowPeak) = (force - s_shakeWindowPeak, force);
            }
            else
            {
                s_shakeWindowPeak = force;
                s_shakeWindowUntil = now + s_instance.shakeWindow;
            }
            Vector3 direction = Random.insideUnitSphere;
            direction.y = Mathf.Abs(direction.y) + 0.5f;
            s_instance.impulseSource.GenerateImpulseWithVelocity(direction.normalized * force);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Paused = false;
            Frozen = false;
            s_hitStopStart = 0f;
            s_hitStopUntil = 0f;
            s_hitStopLength = 0f;
            s_shakeWindowUntil = 0f;
            s_shakeWindowPeak = 0f;
            s_slowMoUntil = 0f;
            s_slowMoScale = 1f;
            s_bulletTimeStart = 0f;
            s_bulletTimeUntil = 0f;
            s_bulletTimeScale = 1f;
        }
    }
}
