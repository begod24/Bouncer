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
    /// Каким мячом бросать (резиновый, волейбольный…), решают карточки — <see cref="SetBallPrefab"/>.
    /// </summary>
    public sealed class PlayerBallHandler : MonoBehaviour
    {
        [SerializeField] Ball ballPrefab;

        PlayerStats _stats;
        PlayerModifiers _mods;
        float _chargeStart;
        float _nextThrowAt;
        float _catchUntil;
        float _catchReadyAt;
        float _catchCooldownStart;
        bool _catchWindowOpen;

        public Ball BallPrefab => ballPrefab;
        public BallDefinition BallDefinition => ballPrefab ? ballPrefab.Definition : null;
        public int Balls { get; private set; }
        public int MaxBalls => _stats.maxBalls + _mods.ExtraBalls;
        public float CatchRadius => _stats.catchRadius * _mods.CatchRadius;
        float CatchWindow => _stats.catchWindow * _mods.CatchWindow;
        float PickupRadius => _stats.pickupRadius * _mods.PickupRadius;
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
        /// <summary>Мяч сам вернулся в руки (бумеранг, резинка).</summary>
        public event Action Returned;
        public event Action BallTypeChanged;

        public void Init(PlayerStats stats, PlayerModifiers mods)
        {
            _stats = stats;
            _mods = mods;
            Balls = Mathf.Min(stats.startBalls, MaxBalls);
        }

        /// <summary>Сменить тип мяча: следующие броски будут этим мячом. Запас в руках не меняется.</summary>
        public void SetBallPrefab(Ball prefab)
        {
            if (prefab == null || prefab == ballPrefab)
                return;
            ballPrefab = prefab;
            BallTypeChanged?.Invoke();
        }

        /// <summary>Мяч вернулся сам. false — руки заняты.</summary>
        public bool TryReceive(Ball ball)
        {
            if (Balls >= MaxBalls)
                return false;
            Balls++;
            GameEvents.PlaySound(SoundCue.Pickup, ball.Position);
            ball.Consume();
            Returned?.Invoke();
            return true;
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
            GameEvents.PlaySound(info.Candle ? SoundCue.CatchCandle : SoundCue.Catch, info.Position);
            Caught?.Invoke(info);
        }

        void StartCatch()
        {
            _catchWindowOpen = true;
            _catchUntil = Time.time + CatchWindow;
            CatchStarted?.Invoke();
        }

        void MissCatch()
        {
            _catchWindowOpen = false;
            StartCatchCooldown(_stats.catchMissCooldown);
            GameEvents.PlaySound(SoundCue.CatchMiss, transform.position);
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
            float radiusSqr = CatchRadius * CatchRadius;
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
            float radiusSqr = PickupRadius * PickupRadius;
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
                GameEvents.PlaySound(SoundCue.Pickup, ball.Position);
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
            var perks = BallPerks.Combine(definition.perks, _mods.Perks);

            Launch(direction, definition.radius, stats, perks, phantom: false);
            // Веер (теннисный): двойники по очереди справа и слева от основного мяча.
            for (int i = 1; i <= perks.extraShots; i++)
            {
                float angle = perks.spreadAngle * ((i + 1) / 2) * (i % 2 == 1 ? 1f : -1f);
                Launch(Quaternion.Euler(0f, angle, 0f) * direction, definition.radius, stats, perks.ForTwin(), phantom: true);
            }

            Balls--;
            var cue = candle ? SoundCue.ThrowCandle : stats.Has(HitFlags.Charged) ? SoundCue.ThrowCharged : SoundCue.Throw;
            GameEvents.PlaySound(cue, transform.position);
            CandleReady = false;
            CancelCharge();
            _nextThrowAt = Time.time + _stats.throwCooldown;
            Thrown?.Invoke(stats);
        }

        void Launch(Vector3 direction, float radius, in ThrowStats stats, in BallPerks perks, bool phantom)
        {
            Vector3 origin = SafeOrigin(direction, radius);
            var ball = PoolService.Spawn(ballPrefab, origin, Quaternion.identity);
            ball.Launch(new BallThrow
            {
                Origin = origin,
                Direction = direction,
                Stats = stats,
                Team = Team.Player,
                Thrower = gameObject,
                Perks = perks,
                Phantom = phantom,
            });
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
