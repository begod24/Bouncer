using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// То, во что можно целиться (автоприцел) или кого преследовать (враги ищут игроков).
    /// Держит общий реестр, чтобы не искать объекты через Find.
    /// Здесь же общее для всех персонажей состояние, которое читают другие сборки: заморозка («Замри!» Физрука,
    /// «Свисток», «Гиря»), взгляд игрока (под ним замирают манекены) и свет вокруг игрока («Фонарик»).
    /// </summary>
    public sealed class Targetable : MonoBehaviour
    {
        /// <summary>Заморозка подсвечивает врага холодным цветом не дольше этого, с.</summary>
        const float FreezeFlashTime = 1f;

        static readonly List<Targetable> s_all = new();
        static float s_enemiesFrozenStart;
        static float s_enemiesFrozenUntil;

        public static IReadOnlyList<Targetable> All => s_all;

        [SerializeField] Team team;
        [Tooltip("Точка прицеливания (обычно на уровне груди). Пусто — позиция + 1 м вверх.")]
        [SerializeField] Transform aimPoint;

        Health _health;
        HitFlash _flash;
        bool _flashSearched;
        Vector3 _lastPosition;
        float _frozenUntil;

        public Team Team
        {
            get => team;
            set => team = value;
        }

        public Vector3 Position => transform.position;
        public Vector3 AimPoint => aimPoint ? aimPoint.position : transform.position + Vector3.up;
        /// <summary>Сглаженная скорость — для упреждения при броске.</summary>
        public Vector3 Velocity { get; private set; }
        public Health Health => _health;
        public bool IsAlive => _health == null || !_health.IsDead;

        /// <summary>Куда смотрит, в плоскости XZ.</summary>
        public Vector3 Facing
        {
            get
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            }
        }

        /// <summary>Половина угла сектора взгляда спереди, градусы: манекены в нём замирают. 0 — не смотрит (враги).</summary>
        public float GazeHalfAngle { get; set; }

        /// <summary>«Зеркальце»: сектор взгляда за спиной, половина угла в градусах. 0 — нет.</summary>
        public float BackGazeHalfAngle { get; set; }

        /// <summary>«Фонарик»: в этом радиусе вокруг светло — тень твёрдая. 0 — нет.</summary>
        public float LightRadius { get; set; }

        /// <summary>Заморожен: стоит на месте и не атакует, но попадания по нему проходят.</summary>
        public bool IsFrozen => Time.time < _frozenUntil || (team == Team.Enemy && Time.time < s_enemiesFrozenUntil);

        /// <summary>Все враги заморожены («Замри!» Физрука).</summary>
        public static bool EnemiesFrozen => Time.time < s_enemiesFrozenUntil;

        /// <summary>Сила общей заморозки врагов для экрана: 0 — нет, 1 — в разгаре. Плавно входит и выходит.</summary>
        public static float EnemiesFrozen01
        {
            get
            {
                float now = Time.time;
                if (now >= s_enemiesFrozenUntil)
                    return 0f;
                float enter = Mathf.Clamp01((now - s_enemiesFrozenStart) / 0.15f);
                float exit = Mathf.Clamp01((s_enemiesFrozenUntil - now) / 0.35f);
                return Mathf.Min(enter, exit);
            }
        }

        void Awake() => _health = GetComponent<Health>();

        void OnEnable()
        {
            s_all.Add(this);
            _lastPosition = transform.position;
            Velocity = Vector3.zero;
            _frozenUntil = 0f;
        }

        void OnDisable() => s_all.Remove(this);

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 position = transform.position;
            if (dt > 0f)
                Velocity = Vector3.Lerp(Velocity, (position - _lastPosition) / dt, 0.5f);
            _lastPosition = position;
        }

        /// <summary>Заморозить на столько секунд (дольше уже идущей заморозки — продлевает).</summary>
        public void Freeze(float seconds)
        {
            if (seconds <= 0f || !IsAlive)
                return;
            _frozenUntil = Mathf.Max(_frozenUntil, Time.time + seconds);
            if (!_flashSearched)
            {
                _flashSearched = true;
                _flash = GetComponentInChildren<HitFlash>();
            }
            if (_flash)
                _flash.Flash(new Color(0.6f, 0.85f, 1f), Mathf.Min(seconds, FreezeFlashTime));
        }

        /// <summary>Видит ли этот игрок точку: она в секторе взгляда спереди или («Зеркальце») за спиной.</summary>
        public bool Sees(Vector3 point)
        {
            if (GazeHalfAngle <= 0f && BackGazeHalfAngle <= 0f)
                return false;
            Vector3 to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-4f)
                return true;
            float angle = Vector3.Angle(Facing, to);
            return angle <= GazeHalfAngle || 180f - angle <= BackGazeHalfAngle;
        }

        /// <summary>Точку видит хоть один живой игрок.</summary>
        public static bool AnyPlayerSees(Vector3 point)
        {
            foreach (var t in s_all)
                if (t.team == Team.Player && t.IsAlive && t.Sees(point))
                    return true;
            return false;
        }

        /// <summary>«Замри!» Физрука: все враги стоят столько секунд, новые появившиеся — тоже.</summary>
        public static void FreezeEnemies(float seconds)
        {
            float now = Time.time;
            if (now >= s_enemiesFrozenUntil)
                s_enemiesFrozenStart = now;
            s_enemiesFrozenUntil = Mathf.Max(s_enemiesFrozenUntil, now + seconds);
        }

        /// <summary>Заморозить всех живых этой команды в радиусе. Возвращает, скольких задело.</summary>
        public static int FreezeAround(Vector3 center, float radius, Team team, float seconds)
        {
            int count = 0;
            float radiusSqr = radius * radius;
            for (int i = s_all.Count - 1; i >= 0; i--)
            {
                var t = s_all[i];
                if (t.team != team || !t.IsAlive)
                    continue;
                Vector3 delta = t.Position - center;
                delta.y = 0f;
                if (delta.sqrMagnitude > radiusSqr)
                    continue;
                t.Freeze(seconds);
                count++;
            }
            return count;
        }

        public static Targetable FindNearest(Vector3 from, Team team, float maxDistance = float.PositiveInfinity)
        {
            Targetable best = null;
            float bestSqr = maxDistance * maxDistance;
            foreach (var t in s_all)
            {
                if (t.team != team || !t.IsAlive)
                    continue;
                Vector3 delta = t.Position - from;
                delta.y = 0f;
                float sqr = delta.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = t;
                }
            }
            return best;
        }

        public static int CountAlive(Team team)
        {
            int count = 0;
            foreach (var t in s_all)
                if (t.team == team && t.IsAlive)
                    count++;
            return count;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_all.Clear();
            s_enemiesFrozenStart = 0f;
            s_enemiesFrozenUntil = 0f;
        }
    }
}
