using System;
using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    public struct CatchInfo
    {
        public bool Candle;
        public bool EnemyBall;
        public bool Perfect;
        public bool Strong;
        public bool Heavy;
        public Vector3 Position;
        public Vector3 Direction;
    }

    public sealed class PlayerBallHandler : MonoBehaviour
    {
        [SerializeField] Ball ballPrefab;

        PlayerStats _stats;
        PlayerModifiers _mods;
        Ball _defaultBallPrefab;
        float _chargeStart;
        float _nextThrowAt;
        float _catchStart;
        float _catchUntil;
        float _catchReadyAt;
        float _catchCooldownStart;
        bool _catchWindowOpen;
        int _missStreak;
        readonly List<GameObject> _borrowed = new();
        PlayerController _player;
        bool _yoyoOut;
        int _lostBalls;
        bool _lostYoyo;
        float _lostReturnAt;

        public Ball BallPrefab => ballPrefab;
        public BallDefinition BallDefinition => ballPrefab ? ballPrefab.Definition : null;
        public int Balls { get; private set; }
        public int BorrowedBalls => _borrowed.Count;
        public int MaxBalls => _stats.maxBalls + _mods.ExtraBalls;
        public float CatchRadius => _stats.catchRadius * _mods.CatchRadius;
        float CatchWindow => _stats.catchWindow * _mods.CatchWindow;
        float PerfectCatchWindow => _stats.perfectCatchWindow;
        public float CatchHalfAngle => _stats.catchHalfAngle;
        float PickupRadius => _stats.pickupRadius * _mods.PickupRadius;
        public bool CandleReady { get; private set; }
        public bool CatchPerksReady { get; private set; }
        public bool IsCharging { get; private set; }
        public float Charge01 { get; private set; }
        public bool IsCatching => _catchWindowOpen && Time.time < _catchUntil;
        public bool IsCatchPerfect => IsCatching && Time.time - _catchStart <= PerfectCatchWindow;
        public bool CatchOnCooldown => !IsCatching && Time.time < _catchReadyAt;
        public float CatchCooldown01
        {
            get
            {
                float total = _catchReadyAt - _catchCooldownStart;
                return total <= 0f ? 0f : Mathf.Clamp01((_catchReadyAt - Time.time) / total);
            }
        }
        public bool YoyoInHand => _mods.Perks.yoyo && !_yoyoOut;

        public event Action<ThrowStats> Thrown;
        public event Action<CatchInfo> Caught;
        public event Action CatchStarted;
        public event Action CatchMissed;
        public event Action Fumbled;
        public event Action Slipped;
        public event Action Pricked;
        public event Action PickedUp;
        public event Action Returned;
        public event Action Grabbed;
        public event Action BallTypeChanged;

        public void Init(PlayerStats stats, PlayerModifiers mods)
        {
            _stats = stats;
            _mods = mods;
            _player = GetComponent<PlayerController>();
            _defaultBallPrefab = ballPrefab;
            Balls = MaxBalls;
        }

        void OnEnable() => Ball.OwnBallLost += OnOwnBallLost;

        void OnDisable() => Ball.OwnBallLost -= OnOwnBallLost;

        public void SetBallPrefab(Ball prefab)
        {
            if (prefab == null || prefab == ballPrefab)
                return;
            ballPrefab = prefab;
            BallTypeChanged?.Invoke();
        }

        public void ResetBallPrefab()
        {
            if (_defaultBallPrefab)
                SetBallPrefab(_defaultBallPrefab);
        }

        public bool TryReceive(Ball ball)
        {
            if (Balls >= MaxBalls)
                return false;
            GameEvents.PlaySound(SoundCue.Pickup, ball.Position);
            AddToHands(ball);
            Returned?.Invoke();
            return true;
        }

        void AddToHands(Ball ball)
        {
            AddToHands(ball.Owner, ball.IsYoyoString);
            ball.TakeInHands();
        }

        void AddToHands(GameObject owner, bool yoyoString)
        {
            Balls++;
            if (owner != gameObject)
                _borrowed.Add(owner);
            else if (yoyoString)
                _yoyoOut = false;
        }

        public void ReceiveFromNetwork(GameObject owner, bool yoyoString)
        {
            if (Balls >= MaxBalls)
            {
                if (owner == gameObject)
                    LoseOwnBall(yoyoString);
                return;
            }
            AddToHands(owner, yoyoString);
            GameEvents.PlaySound(SoundCue.Pickup, transform.position);
            Returned?.Invoke();
        }

        public void SetRemoteBalls(int count)
        {
            if (_player == null || _player.IsLocal)
                return;
            Balls = Mathf.Max(0, count);
            TrimBorrowed();
        }

        public void RevokeFromNetwork(GameObject owner, bool yoyoString)
        {
            if (Balls <= 0)
                return;
            Balls--;
            if (owner != gameObject)
            {
                int index = _borrowed.LastIndexOf(owner);
                if (index < 0)
                    index = _borrowed.Count - 1;
                if (index >= 0)
                    _borrowed.RemoveAt(index);
            }
            else if (yoyoString)
            {
                _yoyoOut = true;
            }
        }

        public void Tick(in PlayerIntent intent, PlayerAim aim, bool canAct, bool canCatch)
        {
            ReturnLostBalls();
            if (!canAct)
            {
                CancelCharge();
                CancelCatch();
                return;
            }

            if (intent.CatchPressed && canCatch && !IsCatching && !IsCharging && Time.time >= _catchReadyAt)
            {
                if (!TryGrab())
                    StartCatch();
            }
            if (IsCatching)
                TryCatchNearby();
            else if (_catchWindowOpen)
                MissCatch();

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

        public void ArmCandle() => CandleReady = true;

        public void ClampToMax()
        {
            Balls = Mathf.Clamp(Balls, 0, MaxBalls);
            TrimBorrowed();
        }

        public void DropExcess()
        {
            while (Balls > MaxBalls)
            {
                Balls--;
                if (_borrowed.Count > 0)
                    _borrowed.RemoveAt(_borrowed.Count - 1);
            }
        }

        void TrimBorrowed()
        {
            while (_borrowed.Count > Balls)
                _borrowed.RemoveAt(_borrowed.Count - 1);
        }

        public bool TryCatch(Ball ball)
        {
            if (!IsCatching || !InFront(ball))
                return false;
            if (ball.Stats.Has(HitFlags.Spiky) && ball.State == BallState.Live)
            {
                _catchWindowOpen = false;
                _catchUntil = 0f;
                StartCatchCooldown(_stats.catchMissCooldown);
                Pricked?.Invoke();
                return false;
            }
            CatchNow(ball);
            return true;
        }

        void CatchNow(Ball ball)
        {
            bool live = ball.State == BallState.Live;
            Vector3 direction = ball.Velocity;
            direction.y = 0f;
            var info = new CatchInfo
            {
                Candle = ball.State == BallState.Popped,
                EnemyBall = live && ball.Team == Team.Enemy,
                Perfect = IsCatchPerfect,
                Strong = live && ball.Stats.Has(HitFlags.Charged),
                Heavy = live && ball.Stats.Has(HitFlags.Heavy),
                Position = ball.Position,
                Direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : transform.forward,
            };

            _catchWindowOpen = false;
            _catchUntil = 0f;
            _missStreak = 0;

            if (live && ball.Stats.Has(HitFlags.Wet))
            {
                Fumble(ball);
                Slipped?.Invoke();
                return;
            }
            if (info.Strong && !info.Perfect && !info.Heavy)
            {
                Fumble(ball);
                Fumbled?.Invoke();
                return;
            }

            if (Balls < MaxBalls)
            {
                AddToHands(ball);
            }
            else
            {
                ball.Drop(transform.position + transform.forward * 0.6f + Vector3.up * 0.5f, Vector3.zero);
            }

            if (info.Candle)
                CandleReady = true;
            if (_mods.HasCatchPerks)
                CatchPerksReady = true;

            StartCatchCooldown(_stats.catchSuccessCooldown);
            GameEvents.PlaySound(info.Candle || info.Perfect ? SoundCue.CatchCandle : SoundCue.Catch, info.Position);
            Caught?.Invoke(info);
        }

        void Fumble(Ball ball)
        {
            Vector3 position = ball.Position;
            Vector3 away = position - transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : transform.forward;
            ball.Drop(position, away * _stats.fumbleBounce.x + Vector3.up * _stats.fumbleBounce.y);
            StartCatchCooldown(_stats.catchMissCooldown);
            GameEvents.PlaySound(SoundCue.BallWall, position);
        }

        bool InFront(Ball ball)
        {
            if (ball.State == BallState.Popped || _stats.catchHalfAngle >= 180f)
                return true;
            Vector3 toBall = ball.Position - transform.position;
            toBall.y = 0f;
            return toBall.sqrMagnitude < 1e-4f || Vector3.Angle(transform.forward, toBall) <= _stats.catchHalfAngle;
        }

        void StartCatch()
        {
            if (Time.time - _catchReadyAt > _stats.catchMissStreakReset)
                _missStreak = 0;
            _catchWindowOpen = true;
            _catchStart = Time.time;
            _catchUntil = Time.time + CatchWindow;
            CatchStarted?.Invoke();
        }

        void MissCatch()
        {
            _catchWindowOpen = false;
            float penalty = _stats.catchMissStreakPenalty * Mathf.Min(_missStreak, _stats.catchMissStreakMax);
            _missStreak++;
            StartCatchCooldown(_stats.catchMissCooldown + penalty);
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
                if (!ball.IsCatchableBy(Team.Player, gameObject) || !InFront(ball))
                    continue;
                if (ball.State == BallState.Live && ball.Stats.Has(HitFlags.Spiky))
                    continue;

                Vector3 position = ball.Position;
                if (ball.State == BallState.Popped)
                {
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

                CatchNow(ball);
                return;
            }
        }

        bool TryGrab()
        {
            if (Balls >= MaxBalls)
                return false;
            Vector3 feet = transform.position;
            float blockSqr = _stats.grabBlockRadius * _stats.grabBlockRadius;
            float grabSqr = _stats.grabRadius * _stats.grabRadius;
            Ball best = null;
            float bestSqr = grabSqr;
            var balls = Ball.Active;
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                var ball = balls[i];
                Vector3 delta = ball.Position - feet;
                if (ball.IsCatchableBy(Team.Player, gameObject) && !(ball.State == BallState.Live && ball.Stats.Has(HitFlags.Spiky)))
                {
                    if (delta.sqrMagnitude <= blockSqr)
                        return false;
                    continue;
                }
                if (ball.State != BallState.Loose || delta.y > 1.6f)
                    continue;
                delta.y = 0f;
                float sqr = delta.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = ball;
                }
            }
            if (best == null || !best.Summon(gameObject))
                return false;
            GameEvents.PlaySound(SoundCue.Catch, best.Position);
            StartCatchCooldown(_stats.catchSuccessCooldown);
            Grabbed?.Invoke();
            return true;
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
                AddToHands(ball);
                PickedUp?.Invoke();
            }
        }

        void OnOwnBallLost(Ball ball, GameObject owner)
        {
            if (owner != gameObject || (_player != null && !_player.IsLocal))
                return;
            LoseOwnBall(ball.IsYoyoString);
        }

        public void LoseOwnBall(bool yoyoString)
        {
            _lostBalls++;
            if (yoyoString)
                _lostYoyo = true;
            _lostReturnAt = Time.time + _stats.lostBallReturnDelay;
        }

        void ReturnLostBalls()
        {
            if (_lostBalls <= 0 || Time.time < _lostReturnAt)
                return;
            int returned = Mathf.Min(_lostBalls, Mathf.Max(0, MaxBalls - Balls));
            Balls += returned;
            _lostBalls = 0;
            if (_lostYoyo)
            {
                _lostYoyo = false;
                _yoyoOut = false;
            }
            if (returned > 0)
            {
                GameEvents.PlaySound(SoundCue.Pickup, transform.position);
                Returned?.Invoke();
            }
        }

        void Throw(Vector3 direction)
        {
            var definition = BallDefinition;
            bool candle = CandleReady;
            var stats = definition.GetThrowStats(Charge01, candle);
            stats.Damage += _mods.BonusDamage;
            stats.BonusDamage = _mods.BonusDamage;
            var perks = BallPerks.Combine(definition.perks, _mods.Perks);
            if (CatchPerksReady)
            {
                perks = BallPerks.Combine(perks, _mods.CatchPerks);
                CatchPerksReady = false;
            }

            bool yoyo = YoyoInHand;
            GameObject owner = gameObject;
            if (yoyo)
            {
                _yoyoOut = true;
            }
            else
            {
                perks.yoyo = false;
                if (_borrowed.Count > 0)
                {
                    owner = _borrowed[^1];
                    _borrowed.RemoveAt(_borrowed.Count - 1);
                }
            }

            Launch(direction, definition.radius, stats, perks, phantom: false, owner, yoyo);
            var twinStats = stats.WithoutBonus();
            for (int i = 1; i <= perks.extraShots; i++)
            {
                float angle = perks.spreadAngle * ((i + 1) / 2) * (i % 2 == 1 ? 1f : -1f);
                Launch(Quaternion.Euler(0f, angle, 0f) * direction, definition.radius, twinStats, perks.ForTwin(), phantom: true,
                    null, false);
            }

            Balls--;
            var cue = candle ? SoundCue.ThrowCandle : stats.Has(HitFlags.Charged) ? SoundCue.ThrowCharged : SoundCue.Throw;
            GameEvents.PlaySound(cue, transform.position);
            CandleReady = false;
            CancelCharge();
            _nextThrowAt = Time.time + _stats.throwCooldown;
            Thrown?.Invoke(stats);
        }

        void Launch(Vector3 direction, float radius, in ThrowStats stats, in BallPerks perks, bool phantom, GameObject owner,
            bool yoyoString)
        {
            Vector3 origin = SafeOrigin(direction, radius);
            Ball.Throw(ballPrefab, new BallThrow
            {
                Origin = origin,
                Direction = direction,
                Stats = stats,
                Team = Team.Player,
                Thrower = gameObject,
                Perks = perks,
                Phantom = phantom,
                Owner = owner,
                YoyoString = yoyoString,
            });
        }

        Vector3 SafeOrigin(Vector3 direction, float radius)
        {
            Vector3 chest = transform.position + Vector3.up * _stats.throwHeight;
            float distance = _stats.throwForwardOffset;
            if (Physics.SphereCast(chest, radius, direction, out RaycastHit hit, distance, Layers.BallSolidMask,
                    QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(0f, hit.distance - 0.02f);
            return chest + direction * distance;
        }
    }
}
