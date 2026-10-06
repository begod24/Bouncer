using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// То, во что можно целиться (автоприцел) или кого преследовать (враги ищут игроков).
    /// Держит общий реестр, чтобы не искать объекты через Find.
    /// Здесь же общее для всех персонажей состояние, которое читают другие сборки: заморозка («Замри!» Физрука,
    /// «Свисток», «Гиря»), взгляд игрока (под ним замирают манекены) и свет вокруг игрока («Фонарик»).
    /// Когда игроков несколько, враги расходятся по ним: за кем уже гонятся многие, тот для новых чуть «дальше»
    /// (<see cref="FindNearest"/>).
    /// </summary>
    public sealed class Targetable : MonoBehaviour
    {
        /// <summary>Заморозка подсвечивает врага холодным цветом не дольше этого, с.</summary>
        const float FreezeFlashTime = 1f;
        /// <summary>Внимание врагов к игроку забывается примерно за столько секунд.</summary>
        const float AttentionMemory = 1f;
        /// <summary>Насколько внимание врагов «отодвигает» игрока: расстояние² × (1 + это × внимание).</summary>
        const float AttentionWeight = 0.12f;

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
        float _hurryUntil;
        float _hurryBoost = 1f;
        /// <summary>Сколько раз враги недавно выбирали этого игрока целью (тает со временем).</summary>
        float _attention;
        float _attentionTime;

        public Team Team
        {
            get => team;
            set => team = value;
        }

        public Vector3 Position => transform.position;
        public Vector3 AimPoint => aimPoint ? aimPoint.position : transform.position + Vector3.up;
        /// <summary>Точка прицеливания как объект (за ней же следит камера игрока). Нет — сам персонаж.</summary>
        public Transform AimTransform => aimPoint ? aimPoint : transform;
        /// <summary>Сглаженная скорость — для упреждения при броске.</summary>
        public Vector3 Velocity { get; private set; }
        public Health Health => _health;
        public bool IsAlive => (_health == null || !_health.IsDead) && !OutOfPlay;
        /// <summary>
        /// Вне игры, хоть и цел: по сети игрок уже дома, у подъезда (финал), — враги его не ищут, монетки не летят.
        /// </summary>
        public bool OutOfPlay { get; set; }

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

        /// <summary>Автоприцел не берёт эту цель: ворона кружит высоко, босс растворился в темноте.</summary>
        public bool HiddenFromAim { get; set; }

        /// <summary>Своя прибавка к скорости: свойство элитки «Шустрый». 1 — нет.</summary>
        public float SpeedBoost { get; set; } = 1f;

        /// <summary>
        /// Во сколько раз быстрее ходит этот враг: уровень опасности, «Шустрый» и «Командир» рядом.
        /// Враги умножают на неё свою скорость бега (у игрока всегда 1).
        /// </summary>
        public float SpeedMultiplier => team != Team.Enemy ? 1f
            : Danger.EnemySpeed * SpeedBoost * (Time.time < _hurryUntil ? _hurryBoost : 1f);

        /// <summary>Подогнать на время: «Командир» ускоряет соседей. Берётся самое сильное из действующих.</summary>
        public void Hurry(float boost, float seconds)
        {
            if (Time.time >= _hurryUntil)
                _hurryBoost = 1f;
            _hurryBoost = Mathf.Max(_hurryBoost, boost);
            _hurryUntil = Mathf.Max(_hurryUntil, Time.time + seconds);
        }

        /// <summary>Заморожен: стоит на месте и не атакует, но попадания по нему проходят.</summary>
        public bool IsFrozen => Time.time < _frozenUntil || (team == Team.Enemy && Time.time < s_enemiesFrozenUntil);

        /// <summary>Все враги заморожены («Замри!» Физрука).</summary>
        public static bool EnemiesFrozen => Time.time < s_enemiesFrozenUntil;

        /// <summary>Сколько ещё длится общая заморозка врагов, с.</summary>
        public static float EnemiesFrozenLeft => Mathf.Max(0f, s_enemiesFrozenUntil - Time.time);

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
            HiddenFromAim = false;
            OutOfPlay = false;
            SpeedBoost = 1f;
            _hurryUntil = 0f;
            _hurryBoost = 1f;
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

        /// <summary>
        /// Заморозить на столько секунд (дольше уже идущей заморозки — продлевает). У гостя сетевой игры заморозка
        /// копии врага уходит хозяину — он заморозит настоящего, а копия замрёт по его вестям.
        /// </summary>
        public void Freeze(float seconds)
        {
            if (seconds <= 0f || !IsAlive)
                return;
            if (team == Team.Enemy && NetHooks.IsGuest && NetHooks.ForwardFreeze != null)
            {
                NetHooks.ForwardFreeze(this, seconds);
                return;
            }
            FreezeLocal(seconds);
        }

        /// <summary>Заморозить здесь и сейчас (по сети — копию, по вестям хозяина).</summary>
        public void FreezeLocal(float seconds)
        {
            if (seconds <= 0f)
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

        /// <summary>Сколько ещё заморожен сам (без общей заморозки врагов), с.</summary>
        public float FrozenLeft => Mathf.Max(0f, _frozenUntil - Time.time);

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

        /// <summary>
        /// «Замри!» Физрука: все враги стоят столько секунд, новые появившиеся — тоже. У гостя сетевой игры заморозку
        /// делает хозяин (враги — у него), а копии замирают по его вестям.
        /// </summary>
        public static void FreezeEnemies(float seconds)
        {
            if (NetHooks.IsGuest && NetHooks.ForwardFreezeEnemies != null && NetHooks.ForwardFreezeEnemies(seconds))
                return;
            FreezeEnemiesLocal(seconds);
        }

        /// <summary>Все враги заморожены на столько секунд (по сети хозяин сразу сообщает гостям).</summary>
        public static event System.Action<float> EnemiesFroze;

        /// <summary>Заморозить всех врагов здесь (по сети у гостя — по вестям хозяина).</summary>
        public static void FreezeEnemiesLocal(float seconds)
        {
            if (!NetHooks.IsGuest)
                EnemiesFroze?.Invoke(seconds);
            float now = Time.time;
            if (now >= s_enemiesFrozenUntil)
                s_enemiesFrozenStart = now;
            s_enemiesFrozenUntil = Mathf.Max(s_enemiesFrozenUntil, now + seconds);
        }

        /// <summary>Новая сцена: общая заморозка врагов прошлого боя в неё не переходит.</summary>
        public static void ClearEnemyFreeze()
        {
            s_enemiesFrozenStart = 0f;
            s_enemiesFrozenUntil = 0f;
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

        /// <summary>
        /// Ближайший живой из команды. Игроков, когда их несколько, враги делят: за кем недавно погнались многие,
        /// тот для следующих чуть «дальше», и часть врагов уходит к тому, за кем никто не гонится.
        /// </summary>
        /// <summary>По сети: копия чужого игрока — двигает её его компьютер, здесь её не толкают и не катают.</summary>
        public bool IsRemote { get; set; }

        /// <summary>Свой игрок (за этим компьютером): вокруг него туман, капли и т.п. Ставит реестр игроков.</summary>
        public static Targetable LocalPlayer { get; set; }

        public static Targetable FindNearest(Vector3 from, Team team, float maxDistance = float.PositiveInfinity)
        {
            bool share = team == Team.Player && CountAlive(Team.Player) > 1;
            Targetable best = null;
            float bestScore = float.PositiveInfinity;
            float maxSqr = maxDistance * maxDistance;
            float now = Time.time;
            foreach (var t in s_all)
            {
                if (t.team != team || !t.IsAlive)
                    continue;
                Vector3 delta = t.Position - from;
                delta.y = 0f;
                float sqr = delta.sqrMagnitude;
                if (sqr > maxSqr)
                    continue;
                float score = share ? sqr * (1f + AttentionWeight * t.Attention(now)) : sqr;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
            if (share && best != null)
                best.Notice(now);
            return best;
        }

        float Attention(float now) => _attention * Mathf.Exp(-(now - _attentionTime) / AttentionMemory);

        void Notice(float now)
        {
            _attention = Attention(now) + 1f;
            _attentionTime = now;
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
            LocalPlayer = null;
            EnemiesFroze = null;
        }
    }
}
