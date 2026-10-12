using System;
using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerAim), typeof(PlayerBallHandler))]
    [RequireComponent(typeof(Health), typeof(Targetable))]
    public sealed class PlayerController : MonoBehaviour, IBallTarget, IDamageable, IBallReceiver
    {
        static readonly Collider[] s_tackleHits = new Collider[16];
        static readonly Collider[] s_dominoHits = new Collider[16];
        static readonly List<IDamageable> s_dominoDamaged = new();

        [SerializeField] PlayerStats stats;

        readonly List<IDamageable> _tackled = new();
        readonly List<(Vector3 position, float at)> _dominoes = new();
        float _tackleDashStart = float.NegativeInfinity;
        bool _tackleThisDash;
        float _nextTackleAt;
        float _nextCatchHealAt;
        float _lidReadyAt;
        float _getUpAt;
        int _food;
        PlayerRewind _rewind;
        PlayerSpin _spin;
        IPlayerIntentSource _intentSource;

        public PlayerStats Stats => stats;
        public PlayerModifiers Modifiers { get; } = new();
        public PlayerMotor Motor { get; private set; }
        public PlayerAim Aim { get; private set; }
        public PlayerBallHandler Balls { get; private set; }
        public Health Health { get; private set; }
        public Targetable Targetable { get; private set; }
        public PlayerAbilities Abilities { get; private set; }
        public bool IsDead => Health.IsDead;
        public PlayerIntent LastIntent { get; private set; }
        public bool IsScripted { get; private set; }
        public int Slot { get; private set; }
        /// <summary>Имя игрока из комнаты (кооп) — для надписи «Наблюдаешь за…».</summary>
        public string DisplayName { get; set; }
        public bool IsLocal { get; private set; } = !Online.Active;
        public PlayerActionState RemoteAction { get; set; }
        public Vector3 RemoteVelocity { get; set; }
        public bool IsHome
        {
            get => Targetable.OutOfPlay;
            set => Targetable.OutOfPlay = value;
        }

        public PlayerActionState Action => !IsLocal ? RemoteAction : new PlayerActionState
        {
            Charging = Balls.IsCharging,
            Charge01 = Balls.Charge01,
            Catching = Balls.IsCatching,
            Dashing = Motor.IsDashing,
            Sliding = Motor.IsDashing && Modifiers.TackleDamage > 0,
            DashDirection = Motor.DashDirection,
            Down = IsDead,
        };

        public event Action<HitInfo> Hurt;
        public event Action Froze;
        public event Action LidBlocked;
        public event Action SecondWindUsed;
        public event Action KnockedDown;
        public event Action Revived;

        const float NetworkDodgeGrace = 0.15f;

        public float CatchHeal01 => stats.catchHealCooldown <= 0f ? 1f
            : Mathf.Clamp01(1f - (_nextCatchHealAt - Time.time) / stats.catchHealCooldown);

        public bool HasTamagotchi => Modifiers.Tamagotchi;
        public float TamagotchiFood01 => Mathf.Clamp01(_food / (float)Mathf.Max(1, stats.tamagotchiFood));
        public bool IsSpinning => _spin != null && _spin.Active;
        public bool IsRewinding => _rewind != null && _rewind.Active;

        /// <summary>«Второе дыхание»: выбит, но сам встанет — у гостя приходит из статуса владельца.</summary>
        public bool GettingUp => Health.GettingUp;

        public bool HasLid => Modifiers.LidCooldown > 0f;
        public bool LidReady => HasLid && Time.time >= _lidReadyAt;
        public float LidReady01 => !HasLid ? 0f
            : Mathf.Clamp01(1f - (_lidReadyAt - Time.time) / Mathf.Max(0.01f, Modifiers.LidCooldown));

        void Awake()
        {
            Motor = GetComponent<PlayerMotor>();
            Aim = GetComponent<PlayerAim>();
            Balls = GetComponent<PlayerBallHandler>();
            Health = GetComponent<Health>();
            _intentSource = GetComponent<IPlayerIntentSource>();

            Targetable = GetComponent<Targetable>();
            Abilities = GetComponent<PlayerAbilities>();
            _rewind = GetComponent<PlayerRewind>();
            _spin = GetComponent<PlayerSpin>();

            Motor.Init(stats, Modifiers);
            Aim.Init(stats);
            Balls.Init(stats, Modifiers);
            Health.Configure(stats.maxLives, 0f);
            Targetable.Team = Team.Player;
            Targetable.GazeHalfAngle = stats.gazeHalfAngle;

            Health.Died += OnDied;
            Balls.Caught += OnCaught;
            Modifiers.Changed += OnModifiersChanged;
            Motor.Landed += OnLeapLanded;
        }

        /// <summary>Максимум сердец: обычный плюс «Бабушкины пирожки», но не больше потолка.</summary>
        public void RefreshMaxLives() =>
            Health.SetMax(Mathf.Min(stats.maxLives + Modifiers.ExtraLives, Mathf.Max(stats.maxLives, stats.maxLivesCap)));

        void OnModifiersChanged()
        {
            Targetable.LightRadius = Modifiers.LanternRadius;
        }

        void OnEnable()
        {
            GameEvents.EnemyKilled += OnEnemyKilled;
            Players.Add(this);
        }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
            Players.Remove(this);
        }

        public void Setup(int slot, bool isLocal)
        {
            Slot = Mathf.Clamp(slot, 0, RunState.MaxPlayers - 1);
            IsLocal = isLocal;
            Targetable.IsRemote = !isLocal;
            Players.Refresh();
        }

        void Update()
        {
            if (_intentSource == null || !IsLocal)
                return;

            var intent = _intentSource.ReadIntent();
            if (BallLab.PointerOverPanel)
            {
                intent.ThrowPressed = false;
                intent.CatchPressed = false;
            }
            LastIntent = intent;
            if (intent.PausePressed && GameSession.Instance != null)
                GameSession.Instance.TogglePause();
            if (GameFeel.Paused || IsScripted || IsRewinding)
                return;

            float dt = Time.deltaTime;
            UpdateGettingUp();
            UpdateDominoes();
            bool canAct = !IsDead && GameSession.IsPlayerActive && !Motor.IsDown;
            if (!canAct)
            {
                Balls.Tick(intent, Aim, false, false);
                Motor.Tick(Vector3.zero, 0f, dt);
                return;
            }

            var definition = Balls.BallDefinition;
            float ballSpeed = definition ? definition.speed : 20f;
            Aim.Tick(intent, transform.position + Vector3.up * stats.throwHeight, ballSpeed);

            if (intent.DashPressed && !Balls.IsCatching)
                Motor.TryDash(intent.Move);
            if (Motor.IsDashing)
                Balls.KickWhileDashing(Motor.DashDirection);

            bool spinning = IsSpinning;
            Balls.Tick(intent, Aim, canAct: !spinning, canCatch: !Motor.IsDashing && !spinning);
            if (Abilities != null)
                Abilities.Tick(intent, !spinning && !Balls.IsCharging);

            float speedMultiplier = Balls.IsCharging ? stats.chargingMoveMultiplier
                : Balls.IsCatching ? stats.catchingMoveMultiplier
                : 1f;
            Motor.Tick(intent.Move, speedMultiplier, dt);
            Motor.Face(Aim.Direction, dt);
            if (Modifiers.TackleDamage > 0 && Motor.IsDashing)
                Tackle();
        }

        void Tackle()
        {
            if (_tackleDashStart != Motor.DashStartTime)
            {
                _tackleDashStart = Motor.DashStartTime;
                _tackled.Clear();
                _tackleThisDash = Time.time >= _nextTackleAt;
                if (_tackleThisDash)
                    _nextTackleAt = Time.time + stats.tackleCooldown;
            }
            if (!_tackleThisDash)
                return;

            Vector3 direction = Motor.DashDirection;
            Vector3 center = transform.position + Vector3.up * 0.6f + direction * 0.4f;
            int count = Physics.OverlapSphereNonAlloc(center, stats.tackleRadius, s_tackleHits, Layers.EnemyMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = s_tackleHits[i];
                var target = other.GetComponentInParent<IDamageable>();
                if (target == null || _tackled.Contains(target))
                    continue;
                // «Чехарда»: вместо подката — прыжок через первого врага на пути
                if (Modifiers.Leapfrog && !Motor.IsLeaping && other.GetComponentInParent<Targetable>() is { IsAlive: true } over)
                {
                    StartLeap(over);
                    return;
                }
                _tackled.Add(target);
                Vector3 away = other.transform.position - transform.position;
                away.y = 0f;
                Vector3 push = direction + (away.sqrMagnitude > 1e-4f ? away.normalized * 0.5f : Vector3.zero);
                NetHooks.ApplyHit(target, new HitInfo
                {
                    Damage = 0,
                    Point = other.ClosestPoint(center),
                    Direction = push.normalized,
                    Force = stats.tackleKnockback,
                    SourceTeam = Team.Player,
                    Source = gameObject,
                    Flags = HitFlags.Tackle | HitFlags.Charged,
                });
                if (other.GetComponentInParent<Targetable>() is { } stunned)
                    stunned.Freeze(stats.tackleStun);
            }
        }

        void StartLeap(Targetable over)
        {
            Vector3 direction = Motor.DashDirection;
            Vector3 to = over.Position + direction * stats.leapBeyond;
            to.y = transform.position.y;
            if (UnityEngine.AI.NavMesh.SamplePosition(to, out var hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
                to = new Vector3(hit.position.x, transform.position.y, hit.position.z);
            over.Freeze(stats.leapStun);
            Health.SetInvulnerable(stats.leapTime + 0.15f);
            Motor.Leap(to, stats.leapTime, stats.leapHeight);
            GameEvents.PlaySound(SoundCue.SpinWhoosh, transform.position);
            PlayerFx.Play(this, PlayerFxKind.LeapStart, transform.position);
        }

        void OnLeapLanded()
        {
            if (!IsLocal)
                return;
            Vector3 center = transform.position + Vector3.up * 0.6f;
            int count = Physics.OverlapSphereNonAlloc(center, stats.leapLandRadius, s_tackleHits, Layers.EnemyMask,
                QueryTriggerInteraction.Ignore);
            _tackled.Clear();
            for (int i = 0; i < count; i++)
            {
                var other = s_tackleHits[i];
                var target = other.GetComponentInParent<IDamageable>();
                if (target == null || _tackled.Contains(target))
                    continue;
                _tackled.Add(target);
                Vector3 away = other.transform.position - transform.position;
                away.y = 0f;
                NetHooks.ApplyHit(target, new HitInfo
                {
                    Damage = 0,
                    Point = other.ClosestPoint(center),
                    Direction = away.sqrMagnitude > 1e-4f ? away.normalized : transform.forward,
                    Force = stats.tackleKnockback * 0.7f,
                    SourceTeam = Team.Player,
                    Source = gameObject,
                    Flags = HitFlags.Tackle | HitFlags.Charged,
                });
                if (other.GetComponentInParent<Targetable>() is { } stunned)
                    stunned.Freeze(stats.leapLandStun);
            }
            GameEvents.PlaySound(SoundCue.AreaThud, transform.position);
            PlayerFx.Play(this, PlayerFxKind.LeapLand, transform.position, new Vector3(stats.leapLandRadius, 0f, 0f));
        }

        public void BeginScripted()
        {
            if (IsScripted)
                return;
            IsScripted = true;
            Balls.CancelCharge();
            Balls.CancelCatch();
            Motor.SetScripted(true);
        }

        public BallContactResult OnBallContact(Ball ball, in RaycastHit hit)
        {
            if (!IsLocal || IsDead || IsScripted || !ball.Team.IsHostileTo(Team.Player))
                return BallContactResult.PassThrough;

            if (Balls.TryCatch(ball))
                return BallContactResult.Caught;
            if (!Motor.IsDashInvulnerable && !Health.IsInvulnerable && TryLidBlock(hit.point))
                return BallContactResult.Bounce;

            Vector3 direction = ball.Velocity;
            direction.y = 0f;
            var info = new HitInfo
            {
                Damage = ball.Stats.Damage,
                Point = hit.point,
                Direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : -transform.forward,
                Force = ball.Stats.Knockback,
                SourceTeam = ball.Team,
                Source = ball.Thrower,
                Flags = ball.Stats.Flags,
            };
            return ApplyHit(info) ? BallContactResult.Hit : BallContactResult.PassThrough;
        }

        public bool ApplyHit(in HitInfo hit)
        {
            if (!IsLocal)
                return !IsDead && !IsScripted && NetHooks.HitRemotePlayer != null && NetHooks.HitRemotePlayer(gameObject, hit);
            if (IsDead || IsScripted)
                return false;
            if (Motor.IsDashInvulnerable)
                return false;
            if (Health.IsInvulnerable)
                return false;
            if (TryLidBlock(hit.Point))
                return false;
            if (TryTamagotchi(hit))
                return true;

            if (!Health.TryDamage(hit))
                return false;

            Health.SetInvulnerable(stats.hurtInvulnerability);
            Motor.AddKnockback(hit.Direction * hit.Force);
            if (hit.Has(HitFlags.Dark))
                Motor.Slow(stats.darkSlowMultiplier, stats.darkSlowTime);
            Balls.CancelCharge();
            GameFeel.HitStop(0.1f);
            GameFeel.Shake(0.9f);
            if (!IsDead)
                GameEvents.PlaySound(SoundCue.PlayerHurt, transform.position);
            Hurt?.Invoke(hit);
            return true;
        }

        public bool ApplyNetworkHit(in HitInfo hit)
        {
            if (!IsLocal || IsDead || IsScripted)
                return false;
            if (Motor.WasDashInvulnerable(NetworkDodgeGrace))
                return false;
            return ApplyHit(hit);
        }

        public void Revive(int lives)
        {
            if (!IsDead)
                return;
            // товарищ поднял раньше «Второго дыхания» — оно остаётся на потом
            if (Health.GettingUp)
                RunState.RefundSecondWind(Slot);
            StandUp(lives);
        }

        void StandUp(int lives)
        {
            Health.Restore();
            Health.SetCurrent(Mathf.Max(1, lives));
            Health.SetInvulnerable(stats.hurtInvulnerability * 2f);
            GameEvents.PlaySound(SoundCue.SecondWind, transform.position);
            Revived?.Invoke();
        }

        public bool TryReceive(Ball ball)
        {
            if (IsDead)
                return false;
            if (IsLocal)
                return Balls.TryReceive(ball);
            return Ball.Network != null && Ball.Network.GiveToRemote(ball, gameObject);
        }

        /// <summary>«Второе дыхание»: раз за прогулку выбитый игрок сам встаёт с одним сердцем через пару секунд.</summary>
        bool TryStartSecondWind()
        {
            if (!IsLocal || !Modifiers.SecondWind || RunState.SecondWindUsed(Slot))
                return false;
            RunState.UseSecondWind(Slot);
            Health.GettingUp = true;
            _getUpAt = Time.time + stats.secondWindDelay;
            PlayerFx.Play(this, PlayerFxKind.SecondWind, transform.position, new Vector3(stats.secondWindDelay, 0f, 0f));
            GameEvents.AnnounceLocal(new Announcement
                { Title = "secondwind.title", Hint = "secondwind.hint", Seconds = stats.secondWindDelay });
            SecondWindUsed?.Invoke();
            return true;
        }

        void UpdateGettingUp()
        {
            if (!Health.GettingUp)
                return;
            if (!IsDead)
            {
                Health.GettingUp = false;
                return;
            }
            if (Time.time < _getUpAt)
                return;
            StandUp(1);
            Health.SetInvulnerable(stats.secondWindInvulnerability);
            PlayerFx.Play(this, PlayerFxKind.SecondWindUp, transform.position);
        }

        bool TryTamagotchi(in HitInfo hit)
        {
            if (!Modifiers.Tamagotchi || _food < stats.tamagotchiFood || hit.Damage < Health.Current)
                return false;
            _food = 0;
            var saved = hit;
            saved.Damage = Health.Current - 1;
            if (saved.Damage > 0)
                Health.TryDamage(saved);
            Health.SetInvulnerable(stats.tamagotchiInvulnerability);
            Motor.AddKnockback(hit.Direction * hit.Force);
            Balls.CancelCharge();
            GameFeel.HitStop(0.1f);
            GameFeel.Shake(0.8f);
            GameEvents.PlaySound(SoundCue.TamagotchiBeep, transform.position);
            Hurt?.Invoke(saved);
            PlayerFx.Play(this, PlayerFxKind.TamagotchiSave, transform.position);
            return true;
        }

        public void OnRevivedTeammate(Vector3 at)
        {
            if (!Modifiers.CircleGuard)
                return;
            Targetable.FreezeAround(at, stats.circleGuardRadius, Team.Enemy, stats.circleGuardFreeze);
            Health.SetInvulnerable(stats.circleGuardInvulnerability);
            PlayerFx.Play(this, PlayerFxKind.CircleGuard, at);
        }

        bool TryLidBlock(Vector3 point)
        {
            if (!LidReady)
                return false;
            _lidReadyAt = Time.time + Modifiers.LidCooldown;
            Health.SetInvulnerable(0.4f);
            GameFeel.Shake(0.3f);
            GameEvents.PlaySound(SoundCue.ShieldBlock, point);
            LidBlocked?.Invoke();
            return true;
        }

        void OnEnemyKilled(GameObject enemy, HitInfo hit)
        {
            if (Modifiers.DominoDamage <= 0 || enemy == null || hit.SourceTeam != Team.Player || hit.Has(HitFlags.Despawn))
                return;
            if (Online.Active && hit.Source != gameObject)
                return;
            _dominoes.Add((enemy.transform.position, Time.time + stats.dominoDelay));
        }

        void UpdateDominoes()
        {
            for (int i = _dominoes.Count - 1; i >= 0; i--)
            {
                var (position, at) = _dominoes[i];
                if (Time.time < at)
                    continue;
                _dominoes.RemoveAt(i);
                KnockNeighbours(position);
            }
        }

        void KnockNeighbours(Vector3 center)
        {
            int count = Physics.OverlapSphereNonAlloc(center + Vector3.up * 0.5f, stats.dominoRadius, s_dominoHits,
                Layers.EnemyMask, QueryTriggerInteraction.Ignore);
            s_dominoDamaged.Clear();
            for (int i = 0; i < count; i++)
            {
                var other = s_dominoHits[i];
                var target = other.GetComponentInParent<IDamageable>();
                if (target == null || s_dominoDamaged.Contains(target))
                    continue;
                s_dominoDamaged.Add(target);
                Vector3 away = other.transform.position - center;
                away.y = 0f;
                NetHooks.ApplyHit(target, new HitInfo
                {
                    Damage = Modifiers.DominoDamage,
                    Point = other.ClosestPoint(center),
                    Direction = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward,
                    Force = stats.dominoKnockback,
                    SourceTeam = Team.Player,
                    Source = gameObject,
                    Flags = HitFlags.Domino,
                });
            }
        }

        void OnCaught(CatchInfo info)
        {
            if (info.EnemyBall && Time.time >= _nextCatchHealAt && Health.Current < Health.Max)
            {
                int heal = info.Perfect ? stats.catchHeal : stats.earlyCatchHeal;
                if (heal > 0)
                {
                    Health.Heal(heal);
                    _nextCatchHealAt = Time.time + stats.catchHealCooldown;
                }
            }
            if (info.Heavy)
            {
                Motor.KnockDown(stats.heavyKnockdown);
                Motor.AddKnockback(info.Direction * stats.heavyPush);
                Balls.CancelCharge();
                KnockedDown?.Invoke();
            }
            else if (info.Strong && info.Perfect)
            {
                Motor.AddKnockback(info.Direction * stats.strongCatchPush);
            }
            GameFeel.HitStop(info.Candle || info.Perfect ? 0.06f : 0.04f);
            if (info.Perfect)
                PlayerFx.Play(this, PlayerFxKind.PerfectCatch, info.Position);
            if (Modifiers.Tamagotchi && _food < stats.tamagotchiFood)
            {
                _food++;
                if (_food >= stats.tamagotchiFood)
                    GameEvents.PlaySound(SoundCue.TamagotchiBeep, transform.position);
            }
            // «Морская фигура»: замершие от «Замри!» — фигуры (их разбивает мяч), свисток показан голубым кольцом
            bool sea = Modifiers.Perks.shatter;
            if (info.Perfect && Modifiers.WhistleFreeze > 0f)
            {
                // «Замри!»: вторая карточка — дальше и дольше
                int extra = Mathf.Max(0, Modifiers.WhistleStacks - 1);
                float radius = stats.whistleRadius + extra * stats.whistleStackRadius;
                float seconds = Modifiers.WhistleFreeze + extra * stats.whistleStackTime;
                Targetable.FreezeAround(transform.position, radius, Team.Enemy, seconds);
                GameEvents.PlaySound(SoundCue.Whistle, transform.position);
                Froze?.Invoke();
                PlayerFx.Play(this, sea ? PlayerFxKind.SeaFigure : PlayerFxKind.Whistle, transform.position,
                    new Vector3(seconds, radius, 0f));
            }
        }

        void OnDied(HitInfo hit)
        {
            // до объявления о выбывании: сессия и комната видят, что игрок ещё встанет, и не заканчивают игру
            TryStartSecondWind();
            Balls.CancelCharge();
            Balls.CancelCatch();
            Motor.Stop();
            GameEvents.PlaySound(SoundCue.PlayerKnockedOut, transform.position);
            GameEvents.RaisePlayerDied(gameObject);
        }
    }
}
