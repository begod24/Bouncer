using System;
using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    /// <summary>
    /// Связывает модули игрока: берёт намерение (локальный ввод, позже — сеть),
    /// раздаёт его движению, прицелу и мячам. Принимает попадания мячей и удары.
    /// Здесь же эффекты карточек, которые не про мяч: подкат рывком, «Замри!» после ловли,
    /// «Крышка от кастрюли» (блок удара) и «Домино» (выбитый враг сбивает соседей).
    /// </summary>
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
        float _lidReadyAt;
        IPlayerIntentSource _intentSource;

        public PlayerStats Stats => stats;
        /// <summary>Прибавки от карточек за этот забег.</summary>
        public PlayerModifiers Modifiers { get; } = new();
        public PlayerMotor Motor { get; private set; }
        public PlayerAim Aim { get; private set; }
        public PlayerBallHandler Balls { get; private set; }
        public Health Health { get; private set; }
        public bool IsDead => Health.IsDead;
        public PlayerIntent LastIntent { get; private set; }

        /// <summary>Получил урон (для визуала).</summary>
        public event Action<HitInfo> Hurt;
        /// <summary>«Замри!»: удачная ловля замедлила всё вокруг (для визуала).</summary>
        public event Action Froze;
        /// <summary>«Крышка от кастрюли» отбила удар (для визуала).</summary>
        public event Action LidBlocked;

        public bool HasLid => Modifiers.LidCooldown > 0f;
        public bool LidReady => HasLid && Time.time >= _lidReadyAt;
        /// <summary>1 — крышка готова, 0 — только что отбила удар.</summary>
        public float LidReady01 => !HasLid ? 0f
            : Mathf.Clamp01(1f - (_lidReadyAt - Time.time) / Mathf.Max(0.01f, Modifiers.LidCooldown));

        void Awake()
        {
            Motor = GetComponent<PlayerMotor>();
            Aim = GetComponent<PlayerAim>();
            Balls = GetComponent<PlayerBallHandler>();
            Health = GetComponent<Health>();
            _intentSource = GetComponent<IPlayerIntentSource>();

            Motor.Init(stats, Modifiers);
            Aim.Init(stats);
            Balls.Init(stats, Modifiers);
            Health.Configure(stats.maxLives, 0f);
            GetComponent<Targetable>().Team = Team.Player;

            Health.Died += OnDied;
            Balls.Caught += OnCaught;
        }

        void OnEnable() => GameEvents.EnemyKilled += OnEnemyKilled;

        void OnDisable() => GameEvents.EnemyKilled -= OnEnemyKilled;

        void Update()
        {
            if (_intentSource == null)
                return;

            var intent = _intentSource.ReadIntent();
            LastIntent = intent;
            if (intent.PausePressed && GameSession.Instance != null)
                GameSession.Instance.TogglePause();
            if (GameFeel.Paused)
                return;

            float dt = Time.deltaTime;
            UpdateDominoes();
            bool canAct = !IsDead && GameSession.IsPlayerActive;
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

            Balls.Tick(intent, Aim, canAct: true, canCatch: !Motor.IsDashing);

            float speedMultiplier = Balls.IsCharging ? stats.chargingMoveMultiplier
                : Balls.IsCatching ? stats.catchingMoveMultiplier
                : 1f;
            Motor.Tick(intent.Move, speedMultiplier, dt);
            Motor.Face(Aim.Direction, dt);
            if (Modifiers.TackleDamage > 0 && Motor.IsDashing)
                Tackle();
        }

        /// <summary>Подкат: рывок сбивает с ног врагов на пути — каждого по разу за рывок.</summary>
        void Tackle()
        {
            // Новый рывок — снова можно сбить и тех, кого сбил прошлый.
            if (_tackleDashStart != Motor.DashStartTime)
            {
                _tackleDashStart = Motor.DashStartTime;
                _tackled.Clear();
            }

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
                _tackled.Add(target);
                // Сбитого отбрасывает вперёд по рывку и немного в сторону — с пути.
                Vector3 away = other.transform.position - transform.position;
                away.y = 0f;
                Vector3 push = direction + (away.sqrMagnitude > 1e-4f ? away.normalized * 0.5f : Vector3.zero);
                target.ApplyHit(new HitInfo
                {
                    Damage = Modifiers.TackleDamage,
                    Point = other.ClosestPoint(center),
                    Direction = push.normalized,
                    Force = stats.tackleKnockback,
                    SourceTeam = Team.Player,
                    Source = gameObject,
                    Flags = HitFlags.Tackle | HitFlags.Charged,
                });
            }
        }

        public BallContactResult OnBallContact(Ball ball, in RaycastHit hit)
        {
            if (IsDead || !ball.Team.IsHostileTo(Team.Player))
                return BallContactResult.PassThrough;

            // Окно ловли открыто и мяч прилетел спереди — пойман, даже если врезался в тело.
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
            if (IsDead || Motor.IsDashInvulnerable || Health.IsInvulnerable)
                return false;
            if (TryLidBlock(hit.Point))
                return false;

            if (!Health.TryDamage(hit))
                return false;

            Health.SetInvulnerable(stats.hurtInvulnerability);
            Motor.AddKnockback(hit.Direction * hit.Force);
            Balls.CancelCharge();
            GameFeel.HitStop(0.1f);
            GameFeel.Shake(0.9f);
            if (!IsDead)
                GameEvents.PlaySound(SoundCue.PlayerHurt, transform.position);
            Hurt?.Invoke(hit);
            return true;
        }

        public bool TryReceive(Ball ball) => !IsDead && Balls.TryReceive(ball);

        /// <summary>«Крышка от кастрюли»: готова — удар отбит, игрок цел, крышка перезаряжается.</summary>
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

        /// <summary>«Домино»: выбитый игроком враг чуть позже сбивает соседей — и так по цепочке.</summary>
        void OnEnemyKilled(GameObject enemy, HitInfo hit)
        {
            if (Modifiers.DominoDamage <= 0 || enemy == null || hit.SourceTeam != Team.Player || hit.Has(HitFlags.Despawn))
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
                target.ApplyHit(new HitInfo
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
            // Лечит только мяч врага, пойманный в последний момент.
            if (info.EnemyBall)
                Health.Heal(info.Perfect ? stats.catchHeal : stats.earlyCatchHeal);
            GameFeel.HitStop(info.Candle || info.Perfect ? 0.06f : 0.04f);
            GameFeel.Shake(0.25f);
            if (Modifiers.CatchFreeze > 0f)
            {
                GameFeel.BulletTime(stats.freezeTimeScale, Modifiers.CatchFreeze);
                Froze?.Invoke();
            }
        }

        void OnDied(HitInfo hit)
        {
            Balls.CancelCharge();
            Balls.CancelCatch();
            Motor.Stop();
            GameEvents.PlaySound(SoundCue.PlayerKnockedOut, transform.position);
            GameEvents.RaisePlayerDied(gameObject);
        }
    }
}
