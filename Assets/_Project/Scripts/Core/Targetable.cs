using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    public sealed class Targetable : MonoBehaviour
    {
        const float FreezeFlashTime = 1f;
        const float AttentionMemory = 1f;
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
        float _attention;
        float _attentionTime;

        public Team Team
        {
            get => team;
            set => team = value;
        }

        public Vector3 Position => transform.position;
        public Vector3 AimPoint => aimPoint ? aimPoint.position : transform.position + Vector3.up;
        public Transform AimTransform => aimPoint ? aimPoint : transform;
        public Vector3 Velocity { get; private set; }
        public Health Health => _health;
        public bool IsAlive => (_health == null || !_health.IsDead) && !OutOfPlay;
        public bool OutOfPlay { get; set; }

        public Vector3 Facing
        {
            get
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            }
        }

        public float GazeHalfAngle { get; set; }

        public float BackGazeHalfAngle { get; set; }

        public float LightRadius { get; set; }

        public bool HiddenFromAim { get; set; }

        public float SpeedBoost { get; set; } = 1f;

        public float SpeedMultiplier => team != Team.Enemy ? 1f
            : Danger.EnemySpeed * SpeedBoost * (Time.time < _hurryUntil ? _hurryBoost : 1f);

        public void Hurry(float boost, float seconds)
        {
            if (Time.time >= _hurryUntil)
                _hurryBoost = 1f;
            _hurryBoost = Mathf.Max(_hurryBoost, boost);
            _hurryUntil = Mathf.Max(_hurryUntil, Time.time + seconds);
        }

        public bool IsFrozen => Time.time < _frozenUntil || (team == Team.Enemy && Time.time < s_enemiesFrozenUntil);

        public static bool EnemiesFrozen => Time.time < s_enemiesFrozenUntil;

        public static float EnemiesFrozenLeft => Mathf.Max(0f, s_enemiesFrozenUntil - Time.time);

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

        public float FrozenLeft => Mathf.Max(0f, _frozenUntil - Time.time);

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

        public static bool AnyPlayerSees(Vector3 point)
        {
            foreach (var t in s_all)
                if (t.team == Team.Player && t.IsAlive && t.Sees(point))
                    return true;
            return false;
        }

        public static void FreezeEnemies(float seconds)
        {
            if (NetHooks.IsGuest && NetHooks.ForwardFreezeEnemies != null && NetHooks.ForwardFreezeEnemies(seconds))
                return;
            FreezeEnemiesLocal(seconds);
        }

        public static event System.Action<float> EnemiesFroze;

        public static void FreezeEnemiesLocal(float seconds)
        {
            if (!NetHooks.IsGuest)
                EnemiesFroze?.Invoke(seconds);
            float now = Time.time;
            if (now >= s_enemiesFrozenUntil)
                s_enemiesFrozenStart = now;
            s_enemiesFrozenUntil = Mathf.Max(s_enemiesFrozenUntil, now + seconds);
        }

        public static void ClearEnemyFreeze()
        {
            s_enemiesFrozenStart = 0f;
            s_enemiesFrozenUntil = 0f;
        }

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

        public bool IsRemote { get; set; }

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
