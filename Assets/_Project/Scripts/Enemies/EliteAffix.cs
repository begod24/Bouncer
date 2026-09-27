using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>Свойство элитки — дёшево, без новой модели: над ней надпись, поведение то же, плюс своё.</summary>
    public enum AffixKind
    {
        None,
        /// <summary>«Шустрый»: бегает на 40% быстрее.</summary>
        Swift,
        /// <summary>«Командир»: подгоняет соседей — они бегают на четверть быстрее.</summary>
        Commander,
        /// <summary>«Ловкач»: ловит каждый третий мяч и бросает его обратно.</summary>
        Catcher,
    }

    /// <summary>
    /// Свойство элитки. Выдаёт спавнер (<see cref="Assign"/>): на 1-й опасности — через раз, со 2-й — всегда.
    /// Надпись над элиткой рисует интерфейс по реестру <see cref="Active"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EliteAffix : MonoBehaviour, IBallInterceptor
    {
        const float SwiftBoost = 1.4f;
        const float CommanderBoost = 1.25f;
        const float CommanderRadius = 7f;
        const float CommanderPulse = 0.5f;
        const int CatchEvery = 3;
        const float HoldTime = 0.6f;
        const float ThrowSpeed = 15f;
        const float ThrowGravity = 3f;

        static readonly List<EliteAffix> s_active = new();

        Targetable _self;
        Health _health;
        float _nextPulse;
        int _contacts;
        Ball _held;
        float _throwAt;

        public static IReadOnlyList<EliteAffix> Active => s_active;
        public AffixKind Kind { get; private set; }
        public Targetable Self => _self;

        /// <summary>Выдать свойство только что появившейся элитке (None — снять прошлое).</summary>
        public static void Assign(GameObject elite, AffixKind kind)
        {
            if (elite == null)
                return;
            if (!elite.TryGetComponent(out EliteAffix affix))
            {
                if (kind == AffixKind.None)
                    return;
                affix = elite.AddComponent<EliteAffix>();
            }
            affix.SetKind(kind);
        }

        /// <summary>Случайное свойство.</summary>
        public static AffixKind RandomKind() => (AffixKind)Random.Range(1, 4);

        void Awake()
        {
            _self = GetComponent<Targetable>();
            _health = GetComponent<Health>();
        }

        void SetKind(AffixKind kind)
        {
            Kind = kind;
            _contacts = 0;
            _held = null;
            if (_self)
                _self.SpeedBoost = kind == AffixKind.Swift ? SwiftBoost : 1f;
            if (kind == AffixKind.None)
                s_active.Remove(this);
            else if (!s_active.Contains(this))
                s_active.Add(this);
        }

        void OnDisable()
        {
            s_active.Remove(this);
            ReleaseHeld();
            Kind = AffixKind.None;
        }

        void Update()
        {
            if (Kind == AffixKind.None)
                return;
            if (_health && _health.IsDead)
            {
                ReleaseHeld();
                return;
            }
            if (Kind == AffixKind.Commander && Time.time >= _nextPulse)
            {
                _nextPulse = Time.time + CommanderPulse;
                Rally();
            }
            if (_held != null)
                HoldAndThrow();
        }

        /// <summary>«Командир»: враги вокруг бегают быстрее, пока он рядом.</summary>
        void Rally()
        {
            Vector3 center = transform.position;
            float radiusSqr = CommanderRadius * CommanderRadius;
            var all = Targetable.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var target = all[i];
                if (target == _self || target.Team != Team.Enemy || !target.IsAlive)
                    continue;
                Vector3 delta = target.Position - center;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                    target.Hurry(CommanderBoost, CommanderPulse + 0.2f);
            }
        }

        public bool TryIntercept(Ball ball)
        {
            if (Kind != AffixKind.Catcher || _held != null || ball == null || (_health && _health.IsDead))
                return false;
            if (++_contacts % CatchEvery != 0)
                return false;
            _held = ball;
            _throwAt = Time.time + HoldTime;
            ball.Stick(HandPosition);
            GameEvents.PlaySound(SoundCue.ScarecrowCatch, HandPosition);
            return true;
        }

        Vector3 HandPosition => (_self ? _self.AimPoint : transform.position + Vector3.up) + Vector3.up * 0.4f;

        void HoldAndThrow()
        {
            if (!_held.isActiveAndEnabled || _held.State != BallState.Stuck)
            {
                _held = null;
                return;
            }
            _held.HoldAt(HandPosition);
            if (Time.time < _throwAt)
                return;
            var target = Targetable.FindNearest(transform.position, Team.Player);
            if (target == null)
            {
                ReleaseHeld();
                return;
            }
            var ball = _held;
            _held = null;
            Vector3 origin = ball.Position;
            Vector3 flat = target.AimPoint - origin;
            flat.y = 0f;
            float distance = Mathf.Max(0.5f, flat.magnitude);
            float time = distance / ThrowSpeed;
            float up = (target.AimPoint.y - origin.y + 0.5f * ThrowGravity * time * time) / time;
            // Мяч игрока остаётся мячом игрока: хозяин у броска не задан.
            ball.Launch(new BallThrow
            {
                Origin = origin,
                Direction = flat / distance,
                Team = Team.Enemy,
                Thrower = gameObject,
                Stats = new ThrowStats
                {
                    Speed = ThrowSpeed,
                    UpVelocity = up,
                    Gravity = ThrowGravity,
                    Damage = 1,
                    Knockback = 6f,
                },
            });
            GameEvents.PlaySound(SoundCue.SoldierThrow, origin);
        }

        void ReleaseHeld()
        {
            if (_held != null && _held.isActiveAndEnabled && _held.State == BallState.Stuck)
                _held.Drop(_held.Position, Vector3.up * 2f);
            _held = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_active.Clear();
    }
}
