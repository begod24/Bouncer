using Bouncer.Balls;
using Bouncer.Core;
using Bouncer.Player;
using UnityEngine;

namespace Bouncer.Trailer
{
    // Источник намерений вместо клавиатуры: сцена задаёт движение/прицел/кнопки, либо простой бот играет сам.
    public sealed class TrailerPuppet : MonoBehaviour, IPlayerIntentSource
    {
        public Vector3 Move;
        public Vector3 AimDirection = Vector3.forward;
        public bool HoldThrow;

        [Header("Бот")]
        public bool Bot;
        public bool AutoCatch = true;
        public Vector3 Home;
        public float HomeRadius = 9f;
        public float PreferredRange = 8f;
        public Vector2 ChargeTime = new(0.2f, 0.7f);
        public Vector2 ThrowPause = new(0.35f, 0.9f);
        public Targetable ForcedTarget;
        public float DashWhenClose = 2.2f;
        float _nextDashAt;

        PlayerController _player;
        bool _throwWasHeld;
        bool _catchQueued;
        bool _dashQueued;
        bool _abilityQueued;
        float _releaseAt;
        float _nextThrowAt;
        float _strafeSign = 1f;
        float _strafeFlipAt;
        float _catchCooldownUntil;

        public PlayerController Player => _player != null ? _player : _player = GetComponent<PlayerController>();

        public void Catch() => _catchQueued = true;
        public void Dash() => _dashQueued = true;
        public void Ability() => _abilityQueued = true;

        public void AimAt(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f)
                AimDirection = d.normalized;
        }

        public PlayerIntent ReadIntent()
        {
            if (Bot)
                Think();
            if (AutoCatch)
                WatchIncoming();

            var intent = new PlayerIntent
            {
                Move = Vector3.ClampMagnitude(Move, 1f),
                Aim = AimDirection,
                UsingGamepad = true,
                ThrowHeld = HoldThrow,
                ThrowPressed = HoldThrow && !_throwWasHeld,
                ThrowReleased = !HoldThrow && _throwWasHeld,
                CatchPressed = _catchQueued,
                DashPressed = _dashQueued,
                AbilityPressed = _abilityQueued,
            };
            _throwWasHeld = HoldThrow;
            _catchQueued = _dashQueued = _abilityQueued = false;
            return intent;
        }

        void WatchIncoming()
        {
            if (Time.time < _catchCooldownUntil || Player == null || Player.Balls.IsCharging)
                return;
            Vector3 chest = transform.position + Vector3.up * 1f;
            var balls = Ball.Active;
            for (int i = 0; i < balls.Count; i++)
            {
                var ball = balls[i];
                if (ball.State != BallState.Live || !ball.Team.IsHostileTo(Team.Player))
                    continue;
                Vector3 rel = chest - ball.Position;
                Vector3 v = ball.Velocity;
                float speedSqr = v.sqrMagnitude;
                if (speedSqr < 1f)
                    continue;
                float t = Vector3.Dot(rel, v) / speedSqr;
                if (t < 0f || t > 0.11f)
                    continue;
                Vector3 miss = ball.Position + v * t - chest;
                if (miss.magnitude > 1.3f)
                    continue;
                AimAt(ball.Position);
                HoldThrow = false;
                _catchQueued = true;
                _catchCooldownUntil = Time.time + 0.6f;
                return;
            }
        }

        void Think()
        {
            var player = Player;
            if (player == null)
                return;
            Vector3 pos = transform.position;
            var target = ForcedTarget != null && ForcedTarget.IsAlive ? ForcedTarget : Targetable.FindNearest(pos, Team.Enemy);

            if (player.Balls.Balls <= 0 && !HoldThrow)
            {
                Move = ToNearestLooseBall(pos);
                if (target != null)
                    AimAt(target.Position);
                return;
            }

            if (target == null)
            {
                Move = SteerHome(pos, Vector3.zero);
                HoldThrow = false;
                return;
            }

            AimAt(target.Position);
            Vector3 to = target.Position - pos;
            to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 1e-3f ? to / dist : Vector3.forward;
            if (Time.time >= _strafeFlipAt)
            {
                _strafeSign = Random.value < 0.5f ? -1f : 1f;
                _strafeFlipAt = Time.time + Random.Range(0.8f, 1.8f);
            }
            Vector3 strafe = Vector3.Cross(Vector3.up, dir) * _strafeSign;
            Vector3 move = strafe * 0.7f;
            if (dist < DashWhenClose && Time.time >= _nextDashAt)
            {
                _dashQueued = true;
                _nextDashAt = Time.time + 1.5f;
                move = (strafe - dir).normalized;
            }
            if (dist < PreferredRange - 2f)
                move -= dir;
            else if (dist > PreferredRange + 3f)
                move += dir * 0.8f;
            Move = SteerHome(pos, move);

            if (HoldThrow)
            {
                if (Time.time >= _releaseAt)
                {
                    HoldThrow = false;
                    _nextThrowAt = Time.time + Random.Range(ThrowPause.x, ThrowPause.y);
                }
            }
            else if (Time.time >= _nextThrowAt && dist < PreferredRange + 8f)
            {
                HoldThrow = true;
                _releaseAt = Time.time + Random.Range(ChargeTime.x, ChargeTime.y);
            }
        }

        Vector3 SteerHome(Vector3 pos, Vector3 move)
        {
            Vector3 back = Home - pos;
            back.y = 0f;
            float over = back.magnitude - HomeRadius;
            if (over > 0f)
                move += back.normalized * Mathf.Clamp01(over / 2f) * 1.5f;
            return Vector3.ClampMagnitude(move, 1f);
        }

        Vector3 ToNearestLooseBall(Vector3 pos)
        {
            Ball best = null;
            float bestSqr = float.MaxValue;
            var balls = Ball.Active;
            for (int i = 0; i < balls.Count; i++)
            {
                var ball = balls[i];
                if (ball.State != BallState.Loose)
                    continue;
                float d = (ball.Position - pos).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = ball;
                }
            }
            if (best == null)
                return SteerHome(pos, Vector3.zero);
            Vector3 to = best.Position - pos;
            to.y = 0f;
            return to.normalized;
        }
    }
}
