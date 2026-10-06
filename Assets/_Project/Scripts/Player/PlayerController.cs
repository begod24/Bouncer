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
    /// Здесь же эффекты карточек, которые не про мяч: подкат рывком (сбивает с ног без урона), «Замри!» и «Свисток»
    /// после ловли, «Крышка от кастрюли» (блок удара), «Домино» (выбитый враг сбивает соседей), «Второе дыхание»
    /// и «Кувырок» (уворот в последний момент: мяч или удар прошёл рядом во время рывка — замедление и бросок
    /// с силой «свечки»). Идеальная ловля лечит не чаще раза в catchHealCooldown. Взгляд игрока (под ним замирают
    /// манекены), «Зеркальце» и «Фонарик» передаются в <see cref="Targetable"/> — враги читают их оттуда.
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
        bool _tackleThisDash;
        float _nextTackleAt;
        float _dodgedDashStart = float.NegativeInfinity;
        float _nextCatchHealAt;
        float _lidReadyAt;
        IPlayerIntentSource _intentSource;

        public PlayerStats Stats => stats;
        /// <summary>Прибавки от карточек за этот забег.</summary>
        public PlayerModifiers Modifiers { get; } = new();
        public PlayerMotor Motor { get; private set; }
        public PlayerAim Aim { get; private set; }
        public PlayerBallHandler Balls { get; private set; }
        public Health Health { get; private set; }
        public Targetable Targetable { get; private set; }
        public bool IsDead => Health.IsDead;
        public PlayerIntent LastIntent { get; private set; }
        /// <summary>Игроком ведёт сценка (финал: бежит в подъезд): ввод не читается, удары не проходят.</summary>
        public bool IsScripted { get; private set; }
        /// <summary>Номер игрока в прогулке (0–3): по нему в <see cref="RunState"/> лежат его монетки и сердца.</summary>
        public int Slot { get; private set; }
        /// <summary>
        /// Игрок за этим компьютером (а не пришедший по сети). Чужим игроком управляет его компьютер: здесь он
        /// только виден — ввод не читается, удары и мячи его не задевают, позу присылает сеть (<see cref="RemoteAction"/>).
        /// По сети каждого игрока сначала создаёт сеть, а своим он становится в <see cref="Setup"/>.
        /// </summary>
        public bool IsLocal { get; private set; } = !Online.Active;
        /// <summary>Что делает чужой игрок — присылает его компьютер.</summary>
        public PlayerActionState RemoteAction { get; set; }

        /// <summary>Что игрок делает прямо сейчас — для анимации.</summary>
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

        /// <summary>Получил урон (для визуала).</summary>
        public event Action<HitInfo> Hurt;
        /// <summary>«Замри!»: удачная ловля замедлила всё вокруг (для визуала).</summary>
        public event Action Froze;
        /// <summary>«Крышка от кастрюли» отбила удар (для визуала).</summary>
        public event Action LidBlocked;
        /// <summary>«Второе дыхание» спасло от выбывания (для визуала).</summary>
        public event Action SecondWindUsed;
        /// <summary>«Кувырок»: уворот в последний момент (для визуала).</summary>
        public event Action Dodged;
        /// <summary>Сбит с ног медболом (для визуала).</summary>
        public event Action KnockedDown;

        /// <summary>1 — идеальная ловля снова лечит, 0 — только что вылечила.</summary>
        public float CatchHeal01 => stats.catchHealCooldown <= 0f ? 1f
            : Mathf.Clamp01(1f - (_nextCatchHealAt - Time.time) / stats.catchHealCooldown);

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

            Targetable = GetComponent<Targetable>();

            Motor.Init(stats, Modifiers);
            Aim.Init(stats);
            Balls.Init(stats, Modifiers);
            Health.Configure(stats.maxLives, 0f);
            Targetable.Team = Team.Player;
            Targetable.GazeHalfAngle = stats.gazeHalfAngle;

            Health.Died += OnDied;
            Balls.Caught += OnCaught;
            Modifiers.Changed += OnModifiersChanged;
        }

        /// <summary>Карточки поменяли взгляд или свет — враги читают их из Targetable.</summary>
        void OnModifiersChanged()
        {
            Targetable.BackGazeHalfAngle = Modifiers.MirrorAngle;
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

        /// <summary>Чей это игрок: номер в прогулке и за этим ли компьютером. Зовёт тот, кто его создал.</summary>
        public void Setup(int slot, bool isLocal)
        {
            Slot = Mathf.Clamp(slot, 0, RunState.MaxPlayers - 1);
            IsLocal = isLocal;
            Players.Refresh();
        }

        void Update()
        {
            if (_intentSource == null || !IsLocal)
                return;

            var intent = _intentSource.ReadIntent();
            LastIntent = intent;
            if (intent.PausePressed && GameSession.Instance != null)
                GameSession.Instance.TogglePause();
            if (GameFeel.Paused || IsScripted)
                return;

            float dt = Time.deltaTime;
            UpdateDominoes();
            // Сбитый с ног (медбол) не бросает, не ловит и не бегает.
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

            Balls.Tick(intent, Aim, canAct: true, canCatch: !Motor.IsDashing);

            float speedMultiplier = Balls.IsCharging ? stats.chargingMoveMultiplier
                : Balls.IsCatching ? stats.catchingMoveMultiplier
                : 1f;
            Motor.Tick(intent.Move, speedMultiplier, dt);
            Motor.Face(Aim.Direction, dt);
            if (Modifiers.TackleDamage > 0 && Motor.IsDashing)
                Tackle();
            if (Modifiers.DashCatch && Motor.IsDashing)
                WatchDodge();
        }

        /// <summary>«Кувырок»: мяч врага пролетел рядом, пока идёт рывок, — уворот в последний момент.</summary>
        void WatchDodge()
        {
            if (_dodgedDashStart == Motor.DashStartTime)
                return;
            Vector3 chest = transform.position + Vector3.up * stats.throwHeight;
            float radiusSqr = stats.dashCatchRadius * stats.dashCatchRadius;
            var balls = Ball.Active;
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                var ball = balls[i];
                if (ball.State != BallState.Live || ball.IsPhantom || !ball.Team.IsHostileTo(Team.Player))
                    continue;
                if ((ball.Position - chest).sqrMagnitude <= radiusSqr)
                {
                    PerfectDodge();
                    return;
                }
            }
        }

        /// <summary>Уворот в последний момент: на полсекунды замедление, следующий бросок — с силой «свечки».</summary>
        void PerfectDodge()
        {
            if (!Modifiers.DashCatch || _dodgedDashStart == Motor.DashStartTime)
                return;
            _dodgedDashStart = Motor.DashStartTime;
            GameFeel.BulletTime(stats.dodgeTimeScale, stats.dodgeSlowTime);
            Balls.ArmCandle();
            GameEvents.PlaySound(SoundCue.CatchCandle, transform.position);
            Dodged?.Invoke();
        }

        /// <summary>
        /// Подкат: рывок сбивает с ног врагов на пути — каждого по разу за рывок. Урона нет, сбитый оглушён;
        /// срабатывает не чаще раза в tackleCooldown (рывок между ними — обычный).
        /// </summary>
        void Tackle()
        {
            // Новый рывок — снова можно сбить и тех, кого сбил прошлый.
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
                _tackled.Add(target);
                // Сбитого отбрасывает вперёд по рывку и немного в сторону — с пути.
                Vector3 away = other.transform.position - transform.position;
                away.y = 0f;
                Vector3 push = direction + (away.sqrMagnitude > 1e-4f ? away.normalized * 0.5f : Vector3.zero);
                target.ApplyHit(new HitInfo
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

        /// <summary>
        /// Дальше игроком ведёт сценка: мячи и ввод отключены, персонаж неуязвим, двигает его тот, кто позвал
        /// (финал: дорога от двора до подъезда).
        /// </summary>
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

            // «Кувырок»: мяч, в который влетел рывок, пролетает сквозь — это уворот в последний момент.
            if (Modifiers.DashCatch && Motor.IsDashInvulnerable)
                PerfectDodge();
            // Окно ловли открыто и мяч прилетел спереди — пойман, даже если врезался в тело. Ёжик колется — попадание.
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
            if (!IsLocal || IsDead || IsScripted)
                return false;
            if (Motor.IsDashInvulnerable)
            {
                // Удар прошёл сквозь рывок — «Кувырок» считает это увортом.
                if (Modifiers.DashCatch)
                    PerfectDodge();
                return false;
            }
            if (Health.IsInvulnerable)
                return false;
            if (TryLidBlock(hit.Point))
                return false;
            if (TrySecondWind(hit))
                return true;

            if (!Health.TryDamage(hit))
                return false;

            Health.SetInvulnerable(stats.hurtInvulnerability);
            Motor.AddKnockback(hit.Direction * hit.Force);
            // Тёмный мяч Бабая: ноги вязнут.
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

        /// <summary>
        /// Мяч вернулся в руки сам. Игроку другого компьютера мяч отдаёт сеть: руки его — там
        /// (<see cref="IBallNetwork.GiveToRemote"/>).
        /// </summary>
        public bool TryReceive(Ball ball)
        {
            if (IsDead)
                return false;
            if (IsLocal)
                return Balls.TryReceive(ball);
            return Ball.Network != null && Ball.Network.GiveToRemote(ball, gameObject);
        }

        /// <summary>
        /// «Второе дыхание»: удар, который выбил бы, оставляет с одним сердцем и даёт пару секунд неуязвимости.
        /// Раз за прогулку — отметка в <see cref="RunState.SecondWindUsed"/> (своя у каждого игрока) переживает смену арены.
        /// </summary>
        bool TrySecondWind(in HitInfo hit)
        {
            if (!Modifiers.SecondWind || RunState.SecondWindUsed(Slot) || hit.Damage < Health.Current)
                return false;
            RunState.UseSecondWind(Slot);
            var saved = hit;
            saved.Damage = Health.Current - 1;
            if (saved.Damage > 0)
                Health.TryDamage(saved);
            Health.SetInvulnerable(stats.secondWindInvulnerability);
            Motor.AddKnockback(hit.Direction * hit.Force);
            Balls.CancelCharge();
            GameFeel.HitStop(0.12f);
            GameFeel.Shake(1f);
            GameEvents.PlaySound(SoundCue.SecondWind, transform.position);
            Hurt?.Invoke(saved);
            SecondWindUsed?.Invoke();
            return true;
        }

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
            // Лечит только мяч врага, пойманный в последний момент, и не чаще раза в catchHealCooldown.
            if (info.EnemyBall && Time.time >= _nextCatchHealAt && Health.Current < Health.Max)
            {
                int heal = info.Perfect ? stats.catchHeal : stats.earlyCatchHeal;
                if (heal > 0)
                {
                    Health.Heal(heal);
                    _nextCatchHealAt = Time.time + stats.catchHealCooldown;
                }
            }
            // Сильный мяч пойман, но толкает назад; медбол сбивает с ног.
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
            GameFeel.Shake(0.25f);
            if (Modifiers.CatchFreeze > 0f)
            {
                GameFeel.BulletTime(stats.freezeTimeScale, Modifiers.CatchFreeze);
                Froze?.Invoke();
            }
            // «Свисток»: идеальная ловля — и враги вокруг замирают.
            if (info.Perfect && Modifiers.WhistleFreeze > 0f)
            {
                Targetable.FreezeAround(transform.position, stats.whistleRadius, Team.Enemy, Modifiers.WhistleFreeze);
                GameEvents.PlaySound(SoundCue.Whistle, transform.position);
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
