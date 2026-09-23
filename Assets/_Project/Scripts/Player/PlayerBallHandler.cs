using System;
using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    public struct CatchInfo
    {
        /// <summary>Пойман мяч после высокого отскока — следующий бросок усиленный.</summary>
        public bool Candle;
        /// <summary>Пойман летящий мяч врага — +1 жизнь.</summary>
        public bool EnemyBall;
        public Vector3 Position;
    }

    /// <summary>
    /// Мячи игрока: запас, заряд и бросок, окно ловли, подбор с пола.
    /// Мячи в руках — просто счётчик, объект мяча появляется только в момент броска.
    /// </summary>
    public sealed class PlayerBallHandler : MonoBehaviour
    {
        [SerializeField] Ball ballPrefab;

        PlayerStats _stats;
        float _chargeStart;
        float _nextThrowAt;
        float _catchUntil;
        float _catchReadyAt;
        float _catchCooldownStart;
        bool _catchWindowOpen;

        public Ball BallPrefab => ballPrefab;
        public BallDefinition BallDefinition => ballPrefab ? ballPrefab.Definition : null;
        public int Balls { get; private set; }
        public int MaxBalls => _stats.maxBalls;
        public bool CandleReady { get; private set; }
        public bool IsCharging { get; private set; }
        public float Charge01 { get; private set; }
        public bool IsCatching => _catchWindowOpen && Time.time < _catchUntil;
        public bool CatchOnCooldown => !IsCatching && Time.time < _catchReadyAt;
        /// <summary>1 — перезарядка только началась, 0 — ловля готова.</summary>
        public float CatchCooldown01
        {
            get
            {
                float total = _catchReadyAt - _catchCooldownStart;
                return total <= 0f ? 0f : Mathf.Clamp01((_catchReadyAt - Time.time) / total);
            }
        }

        public event Action<ThrowStats> Thrown;
        public event Action<CatchInfo> Caught;
        public event Action CatchStarted;
        public event Action CatchMissed;
        public event Action PickedUp;

        public void Init(PlayerStats stats)
        {
            _stats = stats;
            Balls = Mathf.Min(stats.startBalls, stats.maxBalls);
        }

        public void Tick(in PlayerIntent intent, PlayerAim aim, bool canAct, bool canCatch)
        {
            if (!canAct)
            {
                CancelCharge();
                CancelCatch();
                return;
            }

            // --- Ловля ---
            if (intent.CatchPressed && canCatch && !IsCatching && !IsCharging && Time.time >= _catchReadyAt)
                StartCatch();
            if (IsCatching)
                TryCatchNearby();
            else if (_catchWindowOpen)
                MissCatch();

            // --- Бросок: нажал — начался заряд, отпустил — бросок ---
            if (!IsCharging && intent.ThrowPressed && Balls > 0 && !IsCatching && Time.time >= _nextThrowAt)
            {
                IsCharging = true;
                _chargeStart = Time.time;
            }
            if (IsCharging)
            {
                float held = Time.time - _chargeStart;
                Charge01 = held <= _stats.tapThreshold
                    ? 0f
                    : Mathf.Clamp01((held - _stats.tapThreshold) / Mathf.Max(0.01f, _stats.chargeTime));
                if (!intent.ThrowHeld || intent.ThrowReleased)
                    Throw(aim.Direction);
            }

            // --- Подбор с пола ---
            if (Balls < MaxBalls)
                TryPickup();
        }

        public void CancelCharge()
        {
            IsCharging = false;
            Charge01 = 0f;
        }

        public void CancelCatch()
        {
            _catchWindowOpen = false;
            _catchUntil = 0f;
        }

        public void GiveBall(int amount = 1) => Balls = Mathf.Max(0, Balls + amount);

        /// <summary>Поймать конкретный мяч (вызывается и когда мяч врезается в игрока с открытым окном).</summary>
        public void Catch(Ball ball)
        {
            var info = new CatchInfo
            {
                Candle = ball.State == BallState.Popped,
                EnemyBall = ball.State == BallState.Live && ball.Team == Team.Enemy,
                Position = ball.Position,
            };

            if (Balls < MaxBalls)
            {
                Balls++;
                ball.Consume();
            }
            else
            {
                // Руки заняты — мяч падает под ноги.
                ball.Drop(transform.position + transform.forward * 0.6f + Vector3.up * 0.5f, Vector3.zero);
            }

            if (info.Candle)
                CandleReady = true;

            _catchWindowOpen = false;
            _catchUntil = 0f;
            StartCatchCooldown(_stats.catchSuccessCooldown);
            Caught?.Invoke(info);
        }

        void StartCatch()
        {
            _catchWindowOpen = true;
            _catchUntil = Time.time + _stats.catchWindow;
            CatchStarted?.Invoke();
        }

        void MissCatch()
        {
            _catchWindowOpen = false;
            StartCatchCooldown(_stats.catchMissCooldown);
            CatchMissed?.Invoke();
        }

        void StartCatchCooldown(float seconds)
        {
            _catchCooldownStart = Time.time;
            _catchReadyAt = Time.time + seconds;
        }

        void TryCatchNearby()
        {
            Vector3 feet = transform.position;
            Vector3 chest = feet + Vector3.up * _stats.throwHeight;
            float radiusSqr = _stats.catchRadius * _stats.catchRadius;
            var balls = Ball.Active;
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                var ball = balls[i];
                if (!ball.IsCatchableBy(Team.Player))
                    continue;

                Vector3 position = ball.Position;
                if (ball.State == BallState.Popped)
                {
                    // «Свечка»: мяч над игроком, достаточно низко, чтобы дотянуться.
                    if (position.y - feet.y > _stats.candleCatchHeight)
                        continue;
                    Vector3 flat = position - feet;
                    flat.y = 0f;
                    if (flat.sqrMagnitude > radiusSqr)
                        continue;
                }
                else if ((position - chest).sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                Catch(ball);
                return;
            }
        }

        void TryPickup()
        {
            Vector3 feet = transform.position;
            float radiusSqr = _stats.pickupRadius * _stats.pickupRadius;
            var balls = Ball.Active;
            for (int i = balls.Count - 1; i >= 0 && Balls < MaxBalls; i--)
            {
                var ball = balls[i];
                if (ball.State != BallState.Loose)
                    continue;
                Vector3 delta = ball.Position - feet;
                if (delta.y > 1.2f)
                    continue;
                delta.y = 0f;
                if (delta.sqrMagnitude > radiusSqr)
                    continue;
                ball.Consume();
                Balls++;
                PickedUp?.Invoke();
            }
        }

        void Throw(Vector3 direction)
        {
            var definition = BallDefinition;
            bool candle = CandleReady;
            var stats = definition.GetThrowStats(Charge01, candle);
            Vector3 origin = SafeOrigin(direction, definition.radius);

            var ball = PoolService.Spawn(ballPrefab, origin, Quaternion.identity);
            ball.Launch(new BallThrow
            {
                Origin = origin,
                Direction = direction,
                Stats = stats,
                Team = Team.Player,
                Thrower = gameObject,
            });

            Balls--;
            CandleReady = false;
            CancelCharge();
            _nextThrowAt = Time.time + _stats.throwCooldown;
            Thrown?.Invoke(stats);
        }

        /// <summary>Точка вылета перед грудью, но не внутри стены.</summary>
        Vector3 SafeOrigin(Vector3 direction, float radius)
        {
            Vector3 chest = transform.position + Vector3.up * _stats.throwHeight;
            float distance = _stats.throwForwardOffset;
            if (Physics.SphereCast(chest, radius, direction, out RaycastHit hit, distance, Layers.EnvironmentMask,
                    QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(0f, hit.distance - 0.02f);
            return chest + direction * distance;
        }
    }
}
