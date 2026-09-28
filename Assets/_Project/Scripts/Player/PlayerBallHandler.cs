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
        /// <summary>Пойман летящий мяч врага.</summary>
        public bool EnemyBall;
        /// <summary>Мяч пойман сразу после нажатия — в последний момент. Только такая ловля лечит.</summary>
        public bool Perfect;
        /// <summary>Пойман сильный мяч (заряженный, отбитый качелями) — отталкивает назад.</summary>
        public bool Strong;
        /// <summary>Пойман медбол Физрука — сбивает с ног.</summary>
        public bool Heavy;
        public Vector3 Position;
        /// <summary>Куда летел мяч в момент ловли (в плоскости XZ).</summary>
        public Vector3 Direction;
    }

    /// <summary>
    /// Мячи игрока: запас, заряд и бросок, окно ловли, подбор с пола.
    /// Мячи в руках — просто счётчик, объект мяча появляется только в момент броска.
    /// Свои мячи (их <see cref="MaxBalls"/>) остаются на арене, пока их не подберут; подобранный или пойманный чужой
    /// мяч — «взаймы»: бросок им не возвращается (бумеранг, резинка), а упав, он снова лежит на арене как чужой.
    /// Бросается сперва мяч на нитке («Йо-йо»), потом чужие, потом свои.
    /// Каким мячом бросать (резиновый, волейбольный…), решают карточки — <see cref="SetBallPrefab"/>.
    /// Ловля — на тайминг: идеальная в начале окна, мячи только спереди, промахи подряд удлиняют перезарядку,
    /// сильный мяч без идеальной ловли выбивает из рук. Ёжика поймать нельзя (колется), мокрый мяч выскальзывает.
    /// ПКМ, когда ловить нечего, — хват: ближайший лежащий мяч летит в руки.
    /// </summary>
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
        /// <summary>Сколько попыток ловли подряд ушло в пустоту — за каждую перезарядка длиннее.</summary>
        int _missStreak;
        /// <summary>Сколько мячей в руках — чужие, пойманные (одноразовые).</summary>
        int _borrowed;
        /// <summary>Мяч на нитке («Йо-йо») сейчас не в руках.</summary>
        bool _yoyoOut;
        /// <summary>Сколько своих мячей пропало не в руках и ждёт возвращения.</summary>
        int _lostBalls;
        bool _lostYoyo;
        float _lostReturnAt;

        public Ball BallPrefab => ballPrefab;
        public BallDefinition BallDefinition => ballPrefab ? ballPrefab.Definition : null;
        /// <summary>Сколько мячей в руках (свои и чужие вместе).</summary>
        public int Balls { get; private set; }
        /// <summary>Сколько из них чужих — одноразовых.</summary>
        public int BorrowedBalls => _borrowed;
        public int MaxBalls => _stats.maxBalls + _mods.ExtraBalls;
        public float CatchRadius => _stats.catchRadius * _mods.CatchRadius;
        float CatchWindow => _stats.catchWindow * _mods.CatchWindow;
        // «Цепкие руки» растягивают окно ловли, но не идеальную его часть: тайминг остаётся навыком.
        float PerfectCatchWindow => _stats.perfectCatchWindow;
        /// <summary>Половина угла сектора спереди, из которого ловятся летящие мячи.</summary>
        public float CatchHalfAngle => _stats.catchHalfAngle;
        float PickupRadius => _stats.pickupRadius * _mods.PickupRadius;
        public bool CandleReady { get; private set; }
        /// <summary>Горячая картошка: следующий бросок получит <see cref="PlayerModifiers.CatchPerks"/>.</summary>
        public bool CatchPerksReady { get; private set; }
        public bool IsCharging { get; private set; }
        public float Charge01 { get; private set; }
        public bool IsCatching => _catchWindowOpen && Time.time < _catchUntil;
        /// <summary>Окно открыто, и мяч, пойманный прямо сейчас, будет пойман идеально.</summary>
        public bool IsCatchPerfect => IsCatching && Time.time - _catchStart <= PerfectCatchWindow;
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
        /// <summary>Есть «Йо-йо», и мяч на нитке в руках — следующий бросок будет им.</summary>
        public bool YoyoInHand => _mods.Perks.yoyo && !_yoyoOut;

        public event Action<ThrowStats> Thrown;
        public event Action<CatchInfo> Caught;
        public event Action CatchStarted;
        public event Action CatchMissed;
        /// <summary>Сильный мяч пойман не идеально и выбит из рук.</summary>
        public event Action Fumbled;
        /// <summary>Мокрый мяч выскользнул из рук.</summary>
        public event Action Slipped;
        /// <summary>Пытался поймать ёжика — укололся.</summary>
        public event Action Pricked;
        public event Action PickedUp;
        /// <summary>Мяч сам вернулся в руки (бумеранг, резинка, хват ПКМ, пропавший свой).</summary>
        public event Action Returned;
        /// <summary>Хват ПКМ: лежащий мяч полетел в руки.</summary>
        public event Action Grabbed;
        public event Action BallTypeChanged;

        public void Init(PlayerStats stats, PlayerModifiers mods)
        {
            _stats = stats;
            _mods = mods;
            _defaultBallPrefab = ballPrefab;
            // На арене игрок сразу со всеми своими мячами.
            Balls = MaxBalls;
        }

        void OnEnable() => Ball.OwnBallLost += OnOwnBallLost;

        void OnDisable() => Ball.OwnBallLost -= OnOwnBallLost;

        /// <summary>Сменить тип мяча: следующие броски будут этим мячом. Запас в руках не меняется.</summary>
        public void SetBallPrefab(Ball prefab)
        {
            if (prefab == null || prefab == ballPrefab)
                return;
            ballPrefab = prefab;
            BallTypeChanged?.Invoke();
        }

        /// <summary>Мяч по умолчанию (без карточек мячей) — перед тем как заново применить карточки.</summary>
        public void ResetBallPrefab()
        {
            if (_defaultBallPrefab)
                SetBallPrefab(_defaultBallPrefab);
        }

        /// <summary>Мяч вернулся сам (бумеранг, резинка, хват). false — руки заняты.</summary>
        public bool TryReceive(Ball ball)
        {
            if (Balls >= MaxBalls)
                return false;
            GameEvents.PlaySound(SoundCue.Pickup, ball.Position);
            AddToHands(ball);
            Returned?.Invoke();
            return true;
        }

        /// <summary>Мяч в руки: свой или чужой (одноразовый), мяч на нитке снова готов.</summary>
        void AddToHands(Ball ball)
        {
            Balls++;
            if (ball.Owner != gameObject)
                _borrowed++;
            else if (ball.IsYoyoString)
                _yoyoOut = false;
            ball.TakeInHands();
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

            // --- Ловля (или хват, если ловить нечего) ---
            if (intent.CatchPressed && canCatch && !IsCatching && !IsCharging && Time.time >= _catchReadyAt)
            {
                if (!TryGrab())
                    StartCatch();
            }
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

        /// <summary>Выдать свои мячи в руки (карточка «Ещё мяч», новая арена).</summary>
        public void GiveBall(int amount = 1) => Balls = Mathf.Max(0, Balls + amount);

        /// <summary>«Кувырок»: уворот в последний момент — следующий бросок с силой «свечки».</summary>
        public void ArmCandle() => CandleReady = true;

        /// <summary>Лишние мячи сверх вместимости пропадают (карточки заново применяются на новой арене).</summary>
        public void ClampToMax()
        {
            Balls = Mathf.Clamp(Balls, 0, MaxBalls);
            _borrowed = Mathf.Min(_borrowed, Balls);
        }

        /// <summary>В руках больше мячей, чем теперь помещается (хулиганство): сперва уходят чужие, потом свои.</summary>
        public void DropExcess()
        {
            while (Balls > MaxBalls)
            {
                Balls--;
                if (_borrowed > 0)
                    _borrowed--;
            }
        }

        /// <summary>
        /// Поймать мяч, который врезался в игрока. false — поймать нельзя: окно закрыто, мяч прилетел не спереди
        /// или это ёжик (колется — считается попаданием).
        /// </summary>
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

            // Мокрый мяч выскальзывает: не лечит и не остаётся в руках.
            if (live && ball.Stats.Has(HitFlags.Wet))
            {
                Fumble(ball);
                Slipped?.Invoke();
                return;
            }
            // Сильный мяч (заряженный, отбитый качелями) удерживает только идеальная ловля.
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
                // Руки заняты — мяч падает под ноги.
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

        /// <summary>Мяч выбило из рук: отскакивает вперёд и падает на пол, урона нет.</summary>
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

        /// <summary>Летящий мяч ловится только спереди. «Свечка» падает сверху — её видно всегда.</summary>
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
            // Выждал после прошлой попытки — промахи подряд забыты: штраф только за нажатия наугад.
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
                if (!ball.IsCatchableBy(Team.Player) || !InFront(ball))
                    continue;
                // Ёжика навстречу не ловим: пусть долетит и уколет (TryCatch) — ловить его нельзя.
                if (ball.State == BallState.Live && ball.Stats.Has(HitFlags.Spiky))
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

                CatchNow(ball);
                return;
            }
        }

        /// <summary>
        /// Хват ПКМ: если рядом не летит мяч, который можно поймать, ближайший лежащий мяч (в том числе из-под
        /// ног врага) летит в руки. true — схватил, окно ловли не открывается и промахом это не считается.
        /// </summary>
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
                if (ball.IsCatchableBy(Team.Player) && !(ball.State == BallState.Live && ball.Stats.Has(HitFlags.Spiky)))
                {
                    // Есть что ловить — ПКМ остаётся ловлей.
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

        /// <summary>Свой мяч пропал не в руках: через пару секунд он «вернулся» (запас мячей не тает).</summary>
        void OnOwnBallLost(Ball ball, GameObject owner)
        {
            if (owner != gameObject)
                return;
            _lostBalls++;
            if (ball.IsYoyoString)
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

            // Какой мяч из рук: сперва мяч на нитке, потом чужие (одноразовые), потом свои.
            bool yoyo = YoyoInHand;
            GameObject owner = gameObject;
            if (yoyo)
            {
                _yoyoOut = true;
            }
            else
            {
                perks.yoyo = false;
                if (_borrowed > 0)
                {
                    _borrowed--;
                    owner = null;
                }
            }

            Launch(direction, definition.radius, stats, perks, phantom: false, owner, yoyo);
            // Веер (теннисный): двойники по очереди справа и слева от основного мяча. Прибавки карточек им не достаётся.
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
                Owner = owner,
                YoyoString = yoyoString,
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
