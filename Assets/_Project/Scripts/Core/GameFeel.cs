using Unity.Cinemachine;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// «Сок»: стоп-кадр при попадании, тряска камеры, замедление. Единственное место,
    /// которое пишет Time.timeScale — пауза, отладочная скорость и эффекты не спорят друг с другом.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameFeel : MonoBehaviour
    {
        [SerializeField] CinemachineImpulseSource impulseSource;
        [Tooltip("Скорость времени во время стоп-кадра")]
        [SerializeField, Range(0f, 1f)] float hitStopTimeScale = 0.03f;
        [Tooltip("Общий множитель тряски камеры")]
        [SerializeField, Min(0f)] float shakeMultiplier = 1f;

        static GameFeel s_instance;
        static float s_hitStopUntil;
        static float s_slowMoUntil;
        static float s_slowMoScale = 1f;

        public static float DebugTimeScale { get; set; } = 1f;
        public static bool Paused { get; set; }
        public static float ShakeMultiplier
        {
            get => s_instance ? s_instance.shakeMultiplier : 1f;
            set
            {
                if (s_instance)
                    s_instance.shakeMultiplier = value;
            }
        }

        void Awake() => s_instance = this;

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
            float scale = DebugTimeScale;
            if (now < s_slowMoUntil)
                scale *= s_slowMoScale;
            if (now < s_hitStopUntil)
                scale *= hitStopTimeScale;
            Time.timeScale = Paused ? 0f : scale;
        }

        public static void HitStop(float seconds)
        {
            if (seconds > 0f)
                s_hitStopUntil = Mathf.Max(s_hitStopUntil, Time.unscaledTime + seconds);
        }

        public static void SlowMotion(float scale, float seconds)
        {
            s_slowMoScale = scale;
            s_slowMoUntil = Time.unscaledTime + seconds;
        }

        public static void Shake(float force)
        {
            if (s_instance == null || s_instance.impulseSource == null || force <= 0f)
                return;
            Vector3 direction = Random.insideUnitSphere;
            direction.y = Mathf.Abs(direction.y) + 0.5f;
            s_instance.impulseSource.GenerateImpulseWithVelocity(direction.normalized * (force * s_instance.shakeMultiplier));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            DebugTimeScale = 1f;
            Paused = false;
            s_hitStopUntil = 0f;
            s_slowMoUntil = 0f;
            s_slowMoScale = 1f;
        }
    }
}
