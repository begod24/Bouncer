using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// То, во что можно целиться (автоприцел) или кого преследовать (враги ищут игроков).
    /// Держит общий реестр, чтобы не искать объекты через Find.
    /// </summary>
    public sealed class Targetable : MonoBehaviour
    {
        static readonly List<Targetable> s_all = new();
        public static IReadOnlyList<Targetable> All => s_all;

        [SerializeField] Team team;
        [Tooltip("Точка прицеливания (обычно на уровне груди). Пусто — позиция + 1 м вверх.")]
        [SerializeField] Transform aimPoint;

        Health _health;
        Vector3 _lastPosition;

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

        void Awake() => _health = GetComponent<Health>();

        void OnEnable()
        {
            s_all.Add(this);
            _lastPosition = transform.position;
            Velocity = Vector3.zero;
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
        static void ResetStatics() => s_all.Clear();
    }
}
