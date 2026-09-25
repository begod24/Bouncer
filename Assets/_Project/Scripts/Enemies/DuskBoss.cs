using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Bouncer.Enemies
{
    /// <summary>
    /// «Тот, кто в сумерках» — финальный босс, Бабай с мешком и клюкой, ночью в нашем дворе.
    /// Всегда: скользит к игроку и медленно поворачивается; вблизи бьёт клюкой, издалека бросает сильный мяч;
    /// «Ловец» — хватает все мячи спереди, даже заряженные, и отвечает веером сильных (бить сбоку и сзади).
    /// Умения по очереди из мешка, без повторов подряд, новые — с каждой фазой (по жизням или по времени боя):
    /// 1 — «Прыжок на шесте», «Мешок», «Вороньё»; 2 — «Карусель», «Считалочка», «Гасит свет»; 3 — «Прятки».
    /// Мама позвала (<see cref="GameEvents.MomCalled"/>) — мелочь разбегается, а он прыгает на игрока,
    /// пока тот бежит к подъезду; в свет из двери не заходит. Жизнь — на полосе босса (<see cref="BossSplit"/>).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class DuskBoss : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        public enum Ability
        {
            Vault,
            Sack,
            Crows,
            Carousel,
            Count,
            Lights,
            Hide,
        }

        enum State
        {
            Appear,
            Stalk,
            SwipeWindup,
            Swipe,
            SwipeRecover,
            Aim,
            SweepMove,
            SweepCrouch,
            VaultWindup,
            Vault,
            VaultStuck,
            CarouselWindup,
            Carousel,
            Dizzy,
            Count,
            Search,
            Volley,
            Confused,
            LightsCast,
            CrowsCast,
            HideVanish,
            Hidden,
            Stagger,
        }

        const float RepathInterval = 0.3f;
        const float ChestHeight = 2.2f;
        static readonly Ability[][] PhaseAbilities =
        {
            new[] { Ability.Vault, Ability.Sack, Ability.Crows },
            new[] { Ability.Carousel, Ability.Count, Ability.Lights },
            new[] { Ability.Hide },
        };

        [SerializeField] DuskBossDefinition definition;

        [Header("Части модели")]
        [Tooltip("Корень модели внутри Visual: его поднимает прыжок, крутит карусель, прячет земля")]
        [SerializeField] Transform model;
        [SerializeField] Transform body;
        [SerializeField] Transform head;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;
        [SerializeField] Transform sack;
        [Tooltip("Горящие глаза: у ложных чучел их нет, у настоящего в «Прятках» они вспыхивают")]
        [SerializeField] GameObject eyes;
        [Tooltip("Вороны на плечах: улетают, когда он зовёт стаю")]
        [SerializeField] GameObject[] shoulderCrows;
        [Tooltip("Где висят пойманные мячи (левая рука)")]
        [SerializeField] Transform hand;
        [Tooltip("Куда складываются мячи в мешке")]
        [SerializeField] Transform sackInside;

        [Header("Коллайдеры")]
        [SerializeField] Collider bodyCollider;
        [SerializeField] Collider sackCollider;

        [Header("Мячи и подручные")]
        [SerializeField] Ball ballPrefab;
        [SerializeField] CrowEnemy crowPrefab;
        [SerializeField] ScarecrowDecoy decoyPrefab;
        [Tooltip("Тень: выходит из темноты, когда он гасит свет")]
        [SerializeField] GameObject shadowPrefab;

        [Header("Эффекты")]
        [SerializeField] GroundMarker landingMarkerPrefab;
        [SerializeField] ExpandingRing ringPrefab;
        [Tooltip("Пыль при приземлении")]
        [SerializeField] ParticleBurst dustPrefab;
        [Tooltip("Дым: растворяется в темноте и вылезает из земли")]
        [SerializeField] ParticleBurst smokePrefab;
        [SerializeField] ParticleBurst strawPrefab;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;
        [Tooltip("Свет от глаз: вспыхивает на «Я иду искать!»")]
        [SerializeField] Light eyeLight;

        readonly List<Ability> _bag = new();
        readonly List<Ability> _fresh = new();
        readonly List<Ball> _held = new();
        readonly List<float> _answerAt = new();
        readonly List<Ball> _sack = new();
        readonly List<CrowEnemy> _crows = new();
        readonly List<Health> _shadows = new();
        readonly List<ScarecrowDecoy> _decoys = new();
        readonly List<float> _shadowAt = new();
        readonly List<Vector3> _spots = new();

        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        Targetable _target;
        State _state;
        float _stateTime;
        float _stateLength;
        float _fightTime;
        int _phase;
        float _nextRepath;
        bool _repathNow;
        float _nextAbility;
        float _nextSwipe;
        float _nextThrow;
        bool _hasLastAbility;
        Ability _lastAbility;
        bool _swiped;
        bool _called;
        float _nextRageVault;
        bool _lightsWereOut;

        // Прыжок
        int _vaultsLeft;
        Vector3 _vaultFrom;
        Vector3 _vaultTo;
        GroundMarker _landingMarker;
        bool _rageVault;

        // Мешок, карусель, считалочка
        int _sweepStopsLeft;
        Vector3 _sweepPoint;
        float _nextCarouselShot;
        float _carouselAngle;
        int _ticks;
        int _volleyLeft;
        float _nextVolley;

        // Вид
        Quaternion _bodyRest, _headRest, _armLRest, _armRRest;
        Vector3 _bodyEuler, _headEuler, _armLEuler, _armREuler;
        Vector3 _modelRestPosition;
        Quaternion _modelRestRotation = Quaternion.identity;
        Vector3 _modelRestScale = Vector3.one;
        Vector3 _sackRestScale = Vector3.one;
        float _sackScale = 1f;
        float _spin;
        float _walkPhase;
        float _catchPose;
        float _throwPose;
        bool _crowsAway;

        public static DuskBoss Instance { get; private set; }
        public DuskBossDefinition Definition => definition;
        /// <summary>Фаза: 0, 1 или 2.</summary>
        public int Phase => _phase;
        public int SackCount => _sack.Count;
        public Vector3 SackPosition => sackInside ? sackInside.position : transform.position + Vector3.up * 1.4f - transform.forward * 0.8f;
        Vector3 Chest => transform.position + Vector3.up * ChestHeight;
        Vector3 Eye => transform.position + Vector3.up * definition.eyeHeight;
        Vector3 HandPosition => hand ? hand.position : Chest + transform.forward * 0.6f - transform.right * 0.6f;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _agent.updateRotation = false;
            _health.Died += OnDied;
            _bodyRest = Rest(body);
            _headRest = Rest(head);
            _armLRest = Rest(armL);
            _armRRest = Rest(armR);
            if (model)
            {
                _modelRestPosition = model.localPosition;
                _modelRestRotation = model.localRotation;
                _modelRestScale = model.localScale;
            }
            if (sack)
                _sackRestScale = sack.localScale;
            ApplyDefinition();
        }

        static Quaternion Rest(Transform part) => part ? part.localRotation : Quaternion.identity;

        void OnEnable()
        {
            Instance = this;
            GameEvents.MomCalled += OnMomCalled;
        }

        void OnDisable()
        {
            GameEvents.MomCalled -= OnMomCalled;
            if (Instance == this)
                Instance = null;
        }

        public void ApplyDefinition()
        {
            _agent.speed = definition.moveSpeed;
            _agent.acceleration = 8f;
            _agent.stoppingDistance = definition.keepDistance;
            _health.Configure(definition.hitsToKill, 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _fightTime = 0f;
            _phase = 0;
            _bag.Clear();
            _fresh.Clear();
            _hasLastAbility = false;
            _called = false;
            _held.Clear();
            _answerAt.Clear();
            _sack.Clear();
            _crows.Clear();
            _shadows.Clear();
            _shadowAt.Clear();
            _decoys.Clear();
            _target = null;
            _nextRepath = 0f;
            _nextSwipe = Time.time + 2f;
            _nextThrow = Time.time + 3f;
            _nextAbility = Time.time + definition.appearTime + definition.firstAbility;
            _lightsWereOut = false;
            _sackScale = 1f;
            _spin = 0f;
            SetCrowsAway(false);
            SetTangible(true);
            if (eyes)
                eyes.SetActive(true);
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            _agent.updatePosition = true;
            Enter(State.Appear, definition.appearTime);
            Burst(smokePrefab, transform.position + Vector3.up * 0.5f, 1.6f);
            GameEvents.PlaySound(SoundCue.BabaiLaugh, transform.position);
        }

        public void OnDespawned()
        {
            ReleaseHeld(Vector3.back);
            ClearMarker();
            DispelMinions();
        }

        // ---------- Мозги ----------

        void Update()
        {
            float dt = Time.deltaTime;
            _repathNow = Time.time >= _nextRepath;
            if (_repathNow)
            {
                _nextRepath = Time.time + RepathInterval;
                _target = Targetable.FindNearest(transform.position, Team.Player);
            }
            bool hasTarget = _target != null && _target.IsAlive && GameSession.IsGameplayActive;
            if (GameSession.IsGameplayActive)
                _fightTime += dt;
            UpdatePhase();
            TrackLights();
            SpawnPendingShadows();
            CleanMinions();

            if (_state != State.Vault && !_agent.isOnNavMesh)
                return;
            // «Свисток», «Гиря» и прочие заморозки держат и его — но не в полёте.
            if (_self.IsFrozen && _state != State.Vault)
            {
                Halt();
                return;
            }
            _stateTime += dt;
            Vector3 toTarget = hasTarget ? Flat(_target.Position - transform.position) : Vector3.zero;
            float distance = toTarget.magnitude;

            switch (_state)
            {
                case State.Appear:
                    Halt();
                    if (hasTarget)
                        Face(toTarget, dt);
                    if (_stateTime >= _stateLength)
                        Enter(State.Stalk);
                    break;
                case State.Stalk:
                    Stalk(hasTarget, toTarget, distance, dt);
                    break;
                case State.SwipeWindup:
                    if (hasTarget)
                        Face(toTarget, dt, 1.6f);
                    if (_stateTime >= definition.swipeWindup)
                    {
                        _swiped = false;
                        Enter(State.Swipe);
                    }
                    break;
                case State.Swipe:
                    if (!_swiped)
                    {
                        _swiped = true;
                        Swipe();
                    }
                    if (_stateTime >= definition.swipeTime)
                        Enter(State.SwipeRecover);
                    break;
                case State.SwipeRecover:
                    if (_stateTime >= definition.swipeRecover)
                    {
                        _nextSwipe = Time.time + definition.swipeCooldown;
                        Enter(State.Stalk);
                    }
                    break;
                case State.Aim:
                    if (hasTarget)
                        Face(toTarget, dt, 1.5f);
                    if (_stateTime >= definition.throwWindup)
                    {
                        if (hasTarget && !LightZone.Repels(_target.Position))
                            ThrowAt(_target, 0f, definition.ballSpeed, HitFlags.Charged, SoundCue.ThrowCharged, null);
                        _throwPose = 1f;
                        _nextThrow = Time.time + definition.throwCooldown;
                        Enter(State.Stalk);
                    }
                    break;
                case State.SweepMove:
                    SweepMove(dt);
                    break;
                case State.SweepCrouch:
                    if (_stateTime >= definition.sweepCrouch)
                        SweepUp();
                    break;
                case State.VaultWindup:
                    Face(Flat(_vaultTo - transform.position), dt, 2f);
                    if (_stateTime >= _stateLength)
                        TakeOff();
                    break;
                case State.Vault:
                    Fly();
                    break;
                case State.VaultStuck:
                    if (_stateTime >= definition.vaultStuck)
                        AfterLanding(hasTarget);
                    break;
                case State.CarouselWindup:
                    if (hasTarget)
                        Face(toTarget, dt);
                    if (_stateTime >= definition.carouselWindup)
                    {
                        _carouselAngle = Mathf.Atan2(transform.forward.x, transform.forward.z) * Mathf.Rad2Deg;
                        _nextCarouselShot = Time.time;
                        GameEvents.PlaySound(SoundCue.CarouselSpin, transform.position);
                        Enter(State.Carousel);
                    }
                    break;
                case State.Carousel:
                    Carousel();
                    if (_stateTime >= definition.carouselTime)
                        Enter(State.Dizzy, definition.carouselDizzy);
                    break;
                case State.Dizzy:
                case State.Confused:
                case State.Stagger:
                    Halt();
                    if (_stateTime >= _stateLength)
                        EndAbility();
                    break;
                case State.Count:
                    Count(hasTarget, toTarget, dt);
                    break;
                case State.Search:
                    if (hasTarget)
                        Face(toTarget, dt, 5f);
                    if (_stateTime >= definition.searchTime)
                        LookAround(hasTarget);
                    break;
                case State.Volley:
                    if (hasTarget)
                        Face(toTarget, dt, 3f);
                    if (_volleyLeft > 0 && Time.time >= _nextVolley && hasTarget)
                    {
                        _volleyLeft--;
                        _nextVolley = Time.time + definition.foundInterval;
                        ThrowAt(_target, Random.Range(-4f, 4f), definition.ballSpeed, HitFlags.Charged, SoundCue.ThrowCharged, null);
                        _throwPose = 1f;
                    }
                    if (_volleyLeft <= 0 || !hasTarget)
                        EndAbility();
                    break;
                case State.LightsCast:
                    Halt();
                    if (hasTarget)
                        Face(toTarget, dt);
                    if (_stateTime >= definition.lightsCast)
                        PutOutLights();
                    break;
                case State.CrowsCast:
                    Halt();
                    if (hasTarget)
                        Face(toTarget, dt);
                    if (_stateTime >= definition.crowsCast)
                        ReleaseCrows();
                    break;
                case State.HideVanish:
                    Halt();
                    if (_stateTime >= definition.hideVanish)
                        SpreadDecoys();
                    break;
                case State.Hidden:
                    Halt();
                    if (hasTarget)
                        Face(toTarget, dt, 0.5f);
                    if (_stateTime >= _stateLength)
                        StopHiding(found: false);
                    break;
            }

            if (CanCatchNow)
                TryCatchNearby();
            if (CanAnswerNow && hasTarget)
                Answer();
        }

        void Stalk(bool hasTarget, Vector3 toTarget, float distance, float dt)
        {
            if (!hasTarget)
            {
                Halt();
                return;
            }
            // Свет из подъезда: туда он не заходит и оттуда не достаёт.
            bool targetSafe = LightZone.Repels(_target.Position);
            if (_called && Time.time >= _nextRageVault && !targetSafe)
            {
                StartVault(1, rage: true);
                return;
            }
            if (!_called && Time.time >= _nextAbility && TryStartAbility())
                return;
            if (!targetSafe && distance <= definition.swipeRange && Time.time >= _nextSwipe)
            {
                Halt();
                Enter(State.SwipeWindup);
                return;
            }
            if (!targetSafe && distance >= definition.throwMinRange && distance <= definition.throwMaxRange
                && Time.time >= _nextThrow && _held.Count == 0 && CanSee(_target))
            {
                Halt();
                Enter(State.Aim);
                return;
            }

            Vector3 goal = _target.Position;
            if (LightZone.PushOutOfRepelling(ref goal, 1.5f))
                _agent.stoppingDistance = 0.5f;
            else
                _agent.stoppingDistance = definition.keepDistance;
            _agent.isStopped = false;
            _agent.speed = definition.moveSpeed * (_called ? 1.5f : 1f) * GumSpot.EnemyMoveMultiplierAt(transform.position);
            if (_repathNow || !_agent.hasPath)
                _agent.SetDestination(goal);
            Vector3 velocity = Flat(_agent.velocity);
            Face(distance < definition.keepDistance + 2f || velocity.sqrMagnitude < 0.3f ? toTarget : velocity, dt);
        }

        // ---------- Фазы и очередь умений ----------

        void UpdatePhase()
        {
            if (_state == State.Appear || _health.IsDead)
                return;
            float health01 = _health.Current / (float)Mathf.Max(1, _health.Max);
            int want = health01 <= definition.phase3At || _fightTime >= definition.phase3Time ? 2
                : health01 <= definition.phase2At || _fightTime >= definition.phase2Time ? 1
                : 0;
            if (want <= _phase)
                return;
            // Новые умения — сразу первыми, чтобы игрок их точно увидел.
            for (int p = _phase + 1; p <= want; p++)
                _fresh.AddRange(PhaseAbilities[p]);
            _phase = want;
            _bag.Clear();
            GameEvents.PlaySound(SoundCue.BabaiLaugh, transform.position);
            GameFeel.Shake(0.3f);
        }

        bool TryStartAbility()
        {
            // Сколько умений ни пропусти (мячей на земле нет, свет уже погашен), до следующего — пауза.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var ability = NextAbility();
                if (StartAbility(ability))
                {
                    _lastAbility = ability;
                    _hasLastAbility = true;
                    return true;
                }
            }
            _nextAbility = Time.time + 2f;
            return false;
        }

        Ability NextAbility()
        {
            if (_fresh.Count > 0)
            {
                var fresh = _fresh[0];
                _fresh.RemoveAt(0);
                return fresh;
            }
            if (_bag.Count == 0)
            {
                for (int p = 0; p <= _phase; p++)
                    _bag.AddRange(PhaseAbilities[p]);
                for (int i = _bag.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
                }
                if (_hasLastAbility && _bag.Count > 1 && _bag[0] == _lastAbility)
                    (_bag[0], _bag[1]) = (_bag[1], _bag[0]);
            }
            var next = _bag[0];
            _bag.RemoveAt(0);
            return next;
        }

        bool StartAbility(Ability ability)
        {
            Halt();
            switch (ability)
            {
                case Ability.Vault:
                    StartVault(DuskBossDefinition.ByPhase(definition.vaultCount, _phase), rage: false);
                    return true;
                case Ability.Sack:
                    if (Ball.LooseCount < definition.sweepMinBalls || _sack.Count >= definition.sackCapacity
                        || !FindLooseCluster(out _sweepPoint))
                        return false;
                    _sweepStopsLeft = definition.sweepStops;
                    Announce("rule.sack", 3f);
                    Enter(State.SweepMove);
                    return true;
                case Ability.Crows:
                    if (_crows.Count > 0)
                        return false;
                    Announce("rule.crows", 3f);
                    GameEvents.PlaySound(SoundCue.CrowCaw, transform.position);
                    Enter(State.CrowsCast);
                    return true;
                case Ability.Carousel:
                    Announce("rule.carousel", definition.carouselWindup + definition.carouselTime);
                    Enter(State.CarouselWindup);
                    return true;
                case Ability.Count:
                    _ticks = 0;
                    Announce("rule.count", definition.countTime);
                    Enter(State.Count);
                    return true;
                case Ability.Lights:
                    if (LightsOut.Active)
                        return false;
                    Announce("rule.lights", definition.lightsCast + definition.lightsOutTime);
                    GameEvents.PlaySound(SoundCue.BabaiLaugh, transform.position);
                    Enter(State.LightsCast);
                    return true;
                case Ability.Hide:
                    if (_decoys.Count > 0)
                        return false;
                    Announce("rule.hide", definition.hideVanish + definition.hideRise + 2f);
                    ReleaseHeld(transform.forward);
                    Burst(smokePrefab, transform.position + Vector3.up * 1.5f, 2f);
                    GameEvents.PlaySound(SoundCue.ShadowHiss, transform.position);
                    SetTangible(false);
                    Enter(State.HideVanish);
                    return true;
            }
            return false;
        }

        /// <summary>Умение кончилось: снова ходит, следующее — через паузу своей фазы.</summary>
        void EndAbility()
        {
            _nextAbility = Time.time + DuskBossDefinition.ByPhase(definition.abilityInterval, _phase);
            Enter(State.Stalk);
        }

        static void Announce(string key, float seconds) =>
            GameEvents.Announce(new Announcement { Title = key, Hint = key + ".hint", Seconds = seconds });

        // ---------- «Ловец» ----------

        bool CanCatchNow => (_state is State.Stalk or State.Aim or State.SwipeWindup or State.SwipeRecover
            or State.SweepMove or State.CrowsCast or State.LightsCast) && !_self.IsFrozen;

        bool CanAnswerNow => _held.Count > 0 && Time.time >= _answerAt[0]
                                             && (_state is State.Stalk or State.SweepMove);

        void TryCatchNearby()
        {
            if (_held.Count >= definition.maxHeld)
                return;
            Vector3 chest = Chest;
            float radiusSqr = definition.catchRadius * definition.catchRadius;
            var balls = Ball.Active;
            for (int i = balls.Count - 1; i >= 0 && _held.Count < definition.maxHeld; i--)
            {
                var ball = balls[i];
                if (!CanCatch(ball))
                    continue;
                Vector3 position = ball.Position;
                if (position.y > chest.y + 2f)
                    continue;
                Vector3 toBall = Flat(position - chest);
                if (toBall.sqrMagnitude > radiusSqr)
                    continue;
                // Только мяч, который летит к нему, а не уже пролетевший мимо.
                if (Vector3.Dot(Flat(ball.Velocity), -toBall) <= 0f)
                    continue;
                Catch(ball);
            }
        }

        bool CanCatch(Ball ball)
        {
            if (ball.State != BallState.Live || ball.IsPhantom || !ball.Team.IsHostileTo(Team.Enemy))
                return false;
            Vector3 to = Flat(ball.Position - transform.position);
            return to.sqrMagnitude < 1e-4f || Vector3.Angle(transform.forward, to) <= definition.catchHalfAngle;
        }

        void Catch(Ball ball)
        {
            ball.Stick(HeldPosition(_held.Count));
            _held.Add(ball);
            _answerAt.Add(Time.time + definition.answerDelay);
            _catchPose = 1f;
            GameEvents.PlaySound(SoundCue.ScarecrowCatch, ball.Position);
        }

        Vector3 HeldPosition(int index) => HandPosition + transform.right * (index * 0.35f);

        /// <summary>Ответ на пойманный мяч: веер сильных мячей, пойманный — в середине.</summary>
        void Answer()
        {
            var caught = _held[0];
            _held.RemoveAt(0);
            _answerAt.RemoveAt(0);
            int count = Mathf.Max(1, definition.answerBalls);
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (count - 1f);
                float angle = Mathf.Lerp(-definition.answerSpread * 0.5f, definition.answerSpread * 0.5f, t);
                bool middle = i == count / 2;
                var reuse = middle && caught != null && caught.State == BallState.Stuck ? caught : null;
                ThrowAt(_target, angle, definition.answerSpeed, HitFlags.Charged, i == 0 ? SoundCue.ThrowCharged : (SoundCue?)null, reuse);
            }
            if (caught != null && caught.State == BallState.Stuck && count % 2 == 0)
                caught.Drop(caught.Position, transform.forward * 2f);
            _throwPose = 1f;
        }

        void HoldBalls()
        {
            for (int i = _held.Count - 1; i >= 0; i--)
            {
                if (_held[i] == null || !_held[i].isActiveAndEnabled || _held[i].State != BallState.Stuck)
                {
                    _held.RemoveAt(i);
                    _answerAt.RemoveAt(i);
                }
            }
            for (int i = 0; i < _held.Count; i++)
                _held[i].HoldAt(HeldPosition(i));
        }

        void ReleaseHeld(Vector3 direction)
        {
            foreach (var ball in _held)
                if (ball != null && ball.State == BallState.Stuck)
                    ball.Drop(ball.Position, (Flat(direction) + Random.insideUnitSphere * 0.4f).normalized * 3f + Vector3.up * 3f);
            _held.Clear();
            _answerAt.Clear();
        }

        // ---------- Клюка и броски ----------

        void Swipe()
        {
            GameEvents.PlaySound(SoundCue.SwingBat, transform.position);
            GameFeel.Shake(0.25f);
            if (_target == null || !_target.IsAlive)
                return;
            Vector3 toTarget = Flat(_target.Position - transform.position);
            if (toTarget.magnitude > definition.swipeReach || Vector3.Angle(transform.forward, toTarget) > definition.swipeHalfAngle)
                return;
            HitPlayer(_target, definition.swipeDamage, toTarget.normalized, definition.swipeKnockback, HitFlags.Melee | HitFlags.Charged);
        }

        static void HitPlayer(Targetable target, int damage, Vector3 direction, float force, HitFlags flags)
        {
            if (damage <= 0 || !target.TryGetComponent(out IDamageable damageable))
                return;
            damageable.ApplyHit(new HitInfo
            {
                Damage = damage,
                Point = target.AimPoint,
                Direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward,
                Force = force,
                SourceTeam = Team.Enemy,
                Flags = flags,
            });
        }

        /// <summary>Мяч в игрока с упреждением; reuse — бросить пойманный мяч, а не новый из пула.</summary>
        void ThrowAt(Targetable target, float angle, float speed, HitFlags flags, SoundCue? cue, Ball reuse)
        {
            if (target == null || (reuse == null && ballPrefab == null))
                return;
            Vector3 origin = reuse ? reuse.Position : HandPosition;
            Vector3 aim = target.AimPoint;
            Vector3 flat = Flat(aim - origin);
            aim += Flat(target.Velocity) * (flat.magnitude / speed * 0.5f);
            flat = Flat(aim - origin);
            float distance = flat.magnitude;
            if (distance < 0.5f)
                return;
            float gravity = definition.ballGravity;
            float time = distance / speed;
            float up = (aim.y - origin.y + 0.5f * gravity * time * time) / time;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * (flat / distance);
            var ball = reuse ? reuse : PoolService.Spawn(ballPrefab, origin, Quaternion.identity);
            ball.Launch(new BallThrow
            {
                Origin = origin,
                Direction = direction,
                Team = Team.Enemy,
                Thrower = gameObject,
                Stats = new ThrowStats
                {
                    Speed = speed,
                    UpVelocity = up,
                    Gravity = gravity,
                    Damage = definition.ballDamage,
                    Knockback = definition.ballKnockback,
                    Flags = flags,
                },
            });
            if (cue.HasValue)
                GameEvents.PlaySound(cue.Value, origin);
        }

        bool CanSee(Targetable target) =>
            !Physics.Linecast(Eye, target.AimPoint, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore)
            && !CoverVolume.Blocks(Eye, target.AimPoint);

        // ---------- «Мешок» ----------

        /// <summary>Куча лежащих мячей: точка, вокруг которой их больше всего (из равных — ближе к нему).</summary>
        bool FindLooseCluster(out Vector3 point)
        {
            point = default;
            var balls = Ball.Active;
            float radiusSqr = definition.sweepRadius * definition.sweepRadius;
            int bestCount = 0;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < balls.Count; i++)
            {
                if (balls[i].State != BallState.Loose)
                    continue;
                Vector3 p = balls[i].Position;
                int count = 0;
                for (int j = 0; j < balls.Count; j++)
                    if (balls[j].State == BallState.Loose && Flat(balls[j].Position - p).sqrMagnitude <= radiusSqr)
                        count++;
                float distance = Flat(p - transform.position).sqrMagnitude;
                if (count > bestCount || (count == bestCount && distance < bestDistance))
                {
                    bestCount = count;
                    bestDistance = distance;
                    point = p;
                }
            }
            return bestCount > 0;
        }

        void SweepMove(float dt)
        {
            Vector3 to = Flat(_sweepPoint - transform.position);
            if (to.magnitude <= 1.2f || _stateTime >= definition.sweepTimeout)
            {
                Halt();
                Enter(State.SweepCrouch);
                return;
            }
            _agent.isStopped = false;
            _agent.stoppingDistance = 0.8f;
            _agent.speed = definition.moveSpeed * definition.sweepSpeedMultiplier;
            if (_repathNow || !_agent.hasPath)
                _agent.SetDestination(_sweepPoint);
            Vector3 velocity = Flat(_agent.velocity);
            Face(velocity.sqrMagnitude > 0.3f ? velocity : to, dt, 1.5f);
        }

        /// <summary>Нагнулся — и все мячи вокруг улетели в мешок.</summary>
        void SweepUp()
        {
            var balls = Ball.Active;
            float radiusSqr = definition.sweepRadius * definition.sweepRadius;
            int stuffed = 0;
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                var ball = balls[i];
                if (ball.State != BallState.Loose || Flat(ball.Position - transform.position).sqrMagnitude > radiusSqr)
                    continue;
                if (StuffBall(ball, sound: false))
                    stuffed++;
            }
            if (stuffed > 0)
                GameEvents.PlaySound(SoundCue.SackStuff, SackPosition);
            _sweepStopsLeft--;
            if (_sweepStopsLeft > 0 && _sack.Count < definition.sackCapacity && FindLooseCluster(out _sweepPoint))
                Enter(State.SweepMove);
            else
                EndAbility();
        }

        /// <summary>Мяч в мешок (сам сгрёб или принесла ворона). false — мешок полон.</summary>
        public bool StuffBall(Ball ball, bool sound = true)
        {
            if (ball == null || _health.IsDead || _sack.Count >= definition.sackCapacity)
                return false;
            ball.Stick(SackPosition);
            _sack.Add(ball);
            if (sound)
                GameEvents.PlaySound(SoundCue.SackStuff, SackPosition);
            return true;
        }

        void HoldSack()
        {
            for (int i = _sack.Count - 1; i >= 0; i--)
                if (_sack[i] == null || !_sack[i].isActiveAndEnabled || _sack[i].State != BallState.Stuck)
                    _sack.RemoveAt(i);
            Vector3 inside = SackPosition;
            foreach (var ball in _sack)
                ball.HoldAt(inside);
        }

        /// <summary>Попали в мешок: мяч бьёт и самого босса, а собранные мячи высыпаются.</summary>
        public BallContactResult OnSackContact(Ball ball, in RaycastHit hit)
        {
            if (_health.IsDead || _state == State.HideVanish)
                return BallContactResult.PassThrough;
            var result = OnBallContact(ball, hit, allowCatch: false);
            if (_sack.Count > 0)
            {
                SpillSack(Flat(ball.Velocity));
                if (!_health.IsDead && _state is State.Stalk or State.Aim or State.SwipeWindup or State.SwipeRecover
                        or State.SweepMove or State.SweepCrouch)
                {
                    Halt();
                    Enter(State.Stagger, definition.spillStagger);
                }
            }
            return result;
        }

        void SpillSack(Vector3 direction)
        {
            if (_sack.Count == 0)
                return;
            Vector3 origin = SackPosition;
            foreach (var ball in _sack)
            {
                if (ball == null || ball.State != BallState.Stuck)
                    continue;
                Vector3 away = Flat(direction) * 0.5f + Flat(Random.insideUnitSphere);
                away = away.sqrMagnitude > 1e-4f ? away.normalized : -transform.forward;
                ball.Drop(origin + Vector3.up * 0.3f, away * Random.Range(3f, 6.5f) + Vector3.up * Random.Range(3.5f, 6f));
            }
            _sack.Clear();
            GameEvents.PlaySound(SoundCue.SackSpill, origin);
            GameFeel.Shake(0.3f);
        }

        // ---------- «Прыжок на шесте» ----------

        void StartVault(int count, bool rage)
        {
            _vaultsLeft = Mathf.Max(1, count);
            _rageVault = rage;
            BeginJump();
        }

        void BeginJump()
        {
            Vector3 target = _target != null ? _target.Position + Flat(_target.Velocity) * definition.vaultLead : transform.position;
            LightZone.PushOutOfRepelling(ref target, definition.vaultRadius * 0.5f);
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                EndAbility();
                return;
            }
            _vaultFrom = transform.position;
            _vaultTo = hit.position;
            float windup = _rageVault ? definition.rageVaultWindup : definition.vaultWindup;
            ClearMarker();
            if (landingMarkerPrefab)
            {
                _landingMarker = PoolService.Spawn(landingMarkerPrefab, _vaultTo, Quaternion.identity);
                _landingMarker.ShowCircle(_vaultTo, definition.vaultRadius, windup + definition.vaultTime);
            }
            Halt();
            Enter(State.VaultWindup, windup);
        }

        void TakeOff()
        {
            _agent.updatePosition = false;
            GameEvents.PlaySound(SoundCue.Dash, transform.position);
            Enter(State.Vault, definition.vaultTime);
        }

        void Fly()
        {
            float t = Mathf.Clamp01(_stateTime / Mathf.Max(0.05f, definition.vaultTime));
            transform.position = Vector3.Lerp(_vaultFrom, _vaultTo, t);
            if (t >= 1f)
                Land();
        }

        void Land()
        {
            transform.position = _vaultTo;
            _agent.Warp(_vaultTo);
            _agent.updatePosition = true;
            ClearMarker();
            if (ringPrefab)
                PoolService.Spawn(ringPrefab, _vaultTo + Vector3.up * 0.05f, Quaternion.identity).Play(definition.vaultRadius);
            Burst(dustPrefab, _vaultTo, 1.4f);
            GameEvents.PlaySound(SoundCue.PoleLand, _vaultTo);
            GameFeel.Shake(0.5f);
            foreach (var target in Targetable.All)
            {
                if (target.Team != Team.Player || !target.IsAlive)
                    continue;
                Vector3 away = Flat(target.Position - _vaultTo);
                if (away.magnitude > definition.vaultRadius)
                    continue;
                HitPlayer(target, definition.vaultDamage, away, definition.vaultKnockback, HitFlags.Area | HitFlags.Charged);
            }
            Enter(State.VaultStuck);
        }

        void AfterLanding(bool hasTarget)
        {
            _vaultsLeft--;
            if (_vaultsLeft > 0 && hasTarget)
            {
                BeginJump();
                return;
            }
            if (_rageVault)
            {
                _nextRageVault = Time.time + definition.rageVaultInterval;
                Enter(State.Stalk);
                return;
            }
            EndAbility();
        }

        void ClearMarker()
        {
            if (_landingMarker && _landingMarker.isActiveAndEnabled)
                _landingMarker.Hide();
            _landingMarker = null;
        }

        // ---------- «Карусель» ----------

        void Carousel()
        {
            if (Time.time < _nextCarouselShot || ballPrefab == null)
                return;
            _nextCarouselShot = Time.time + definition.carouselInterval;
            int arms = Mathf.Max(1, DuskBossDefinition.ByPhase(definition.carouselArms, _phase));
            Vector3 center = transform.position + Vector3.up * 1.4f;
            for (int i = 0; i < arms; i++)
            {
                float angle = (_carouselAngle + i * 360f / arms) * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 origin = center + direction * 1.3f;
                var ball = PoolService.Spawn(ballPrefab, origin, Quaternion.identity);
                ball.Launch(new BallThrow
                {
                    Origin = origin,
                    Direction = direction,
                    Team = Team.Enemy,
                    Thrower = gameObject,
                    Stats = new ThrowStats
                    {
                        Speed = definition.carouselBallSpeed,
                        UpVelocity = 0.6f,
                        Gravity = definition.carouselBallGravity,
                        Damage = definition.ballDamage,
                        Knockback = definition.ballKnockback * 0.6f,
                        Flags = HitFlags.None,
                    },
                });
            }
            GameEvents.PlaySound(SoundCue.SoldierThrow, center);
            _carouselAngle += definition.carouselTurn;
        }

        // ---------- «Считалочка» ----------

        void Count(bool hasTarget, Vector3 toTarget, float dt)
        {
            Halt();
            // Отвернулся к стенке и закрыл глаза руками.
            if (hasTarget)
                Face(-toTarget, dt, 2.5f);
            int ticks = Mathf.Min(5, Mathf.FloorToInt(_stateTime / Mathf.Max(0.1f, definition.countTime / 5f)) + 1);
            while (_ticks < ticks)
            {
                _ticks++;
                GameEvents.PlaySound(SoundCue.CountTick, transform.position);
            }
            if (_stateTime >= definition.countTime)
            {
                GameEvents.Announce(new Announcement { Title = "rule.count.found", Seconds = 1.5f });
                GameEvents.PlaySound(SoundCue.FoundYou, transform.position);
                Enter(State.Search);
            }
        }

        /// <summary>Обернулся: кого видно — в того залп сильных мячей, кто спрятался — того не нашёл.</summary>
        void LookAround(bool hasTarget)
        {
            if (hasTarget && CanSee(_target) && !LightZone.Repels(_target.Position))
            {
                _volleyLeft = definition.foundVolley;
                _nextVolley = Time.time;
                Enter(State.Volley);
                return;
            }
            Enter(State.Confused, definition.confusedTime);
        }

        // ---------- «Гасит свет» ----------

        void PutOutLights()
        {
            LightsOut.Trigger(definition.lightsOutTime);
            GameEvents.PlaySound(SoundCue.DarkWave, transform.position);
            GameFeel.Shake(0.35f);
            _lightsWereOut = true;
            int count = DuskBossDefinition.ByPhase(definition.shadowCount, _phase);
            for (int i = 0; i < count; i++)
                _shadowAt.Add(Time.time + 0.4f + i * 0.5f);
            EndAbility();
        }

        void SpawnPendingShadows()
        {
            for (int i = _shadowAt.Count - 1; i >= 0; i--)
            {
                if (Time.time < _shadowAt[i])
                    continue;
                _shadowAt.RemoveAt(i);
                if (shadowPrefab == null || !LightsOut.Active || _target == null)
                    continue;
                if (!TryFindSpot(_target.Position, definition.shadowDistance.x, definition.shadowDistance.y, out Vector3 point))
                    continue;
                Vector3 facing = Flat(_target.Position - point);
                var shadow = PoolService.Spawn(shadowPrefab, point,
                    facing.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(facing) : Quaternion.identity);
                if (shadow.TryGetComponent(out Health health))
                    _shadows.Add(health);
            }
        }

        /// <summary>Свет вернулся — тени, которых он вызвал, рассеиваются.</summary>
        void TrackLights()
        {
            bool dark = LightsOut.Active;
            if (_lightsWereOut && !dark)
            {
                _shadowAt.Clear();
                KillAll(_shadows);
            }
            _lightsWereOut = dark;
        }

        // ---------- «Вороньё» ----------

        void ReleaseCrows()
        {
            if (crowPrefab != null)
            {
                int count = DuskBossDefinition.ByPhase(definition.crowCount, _phase);
                for (int i = 0; i < count; i++)
                {
                    // Две — с его плеч, остальные слетаются из-за краёв двора.
                    Vector3 from = i < 2
                        ? transform.position + Vector3.up * 4.3f + transform.right * (i == 0 ? -0.6f : 0.6f)
                        : EdgePoint() + Vector3.up * 7f;
                    SpawnCrow(from);
                }
                SetCrowsAway(true);
                GameEvents.PlaySound(SoundCue.CrowFlap, transform.position);
            }
            EndAbility();
        }

        void SpawnCrow(Vector3 from)
        {
            var crow = PoolService.Spawn(crowPrefab, from, Quaternion.identity);
            crow.Launch(this);
            _crows.Add(crow);
        }

        static Vector3 EdgePoint()
        {
            float x = Random.Range(-20f, 20f);
            float z = Random.value < 0.5f ? -16f : 16f;
            if (Random.value < 0.4f)
                (x, z) = (Random.value < 0.5f ? -23f : 23f, Random.Range(-13f, 13f));
            return new Vector3(x, 0f, z);
        }

        // ---------- «Прятки» ----------

        void SpreadDecoys()
        {
            Vector3 around = _target != null ? _target.Position : Vector3.zero;
            int decoys = decoyPrefab ? DuskBossDefinition.ByPhase(definition.decoyCount, _phase) : 0;
            _spots.Clear();
            for (int attempt = 0; attempt < 60 && _spots.Count < decoys + 1; attempt++)
            {
                if (!TryFindSpot(around, definition.decoyMinDistance, 16f, out Vector3 point))
                    continue;
                bool crowded = false;
                foreach (var spot in _spots)
                    if (Flat(spot - point).sqrMagnitude < definition.decoySpacing * definition.decoySpacing)
                        crowded = true;
                if (!crowded)
                    _spots.Add(point);
            }
            if (_spots.Count == 0)
                _spots.Add(transform.position);
            int real = Random.Range(0, _spots.Count);
            for (int i = 0; i < _spots.Count; i++)
            {
                Vector3 facing = Flat(around - _spots[i]);
                var rotation = facing.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(facing) : transform.rotation;
                if (i == real)
                {
                    _agent.Warp(_spots[i]);
                    transform.rotation = rotation;
                    continue;
                }
                var decoy = PoolService.Spawn(decoyPrefab, _spots[i], rotation);
                decoy.Init(this, definition.hideRise);
                _decoys.Add(decoy);
            }
            Burst(smokePrefab, transform.position + Vector3.up * 0.5f, 1.4f);
            SetTangible(true);
            Enter(State.Hidden, definition.hideRise + definition.hideTime);
        }

        /// <summary>Нашли настоящего (или сам вышел): ложные чучела осыпаются без ворон.</summary>
        void StopHiding(bool found)
        {
            foreach (var decoy in _decoys)
                if (decoy && decoy.isActiveAndEnabled)
                    decoy.Collapse();
            _decoys.Clear();
            if (eyes)
                eyes.SetActive(true);
            if (found)
            {
                Enter(State.Stagger, definition.revealStagger);
                return;
            }
            // Никто не нашёл — сам нашёл игрока: прыжок из укрытия.
            GameEvents.PlaySound(SoundCue.BabaiLaugh, transform.position);
            StartVault(1, rage: false);
        }

        /// <summary>Ложное чучело разбили: из соломы вылетают вороны.</summary>
        public void OnDecoyBroken(ScarecrowDecoy decoy, bool byPlayer)
        {
            _decoys.Remove(decoy);
            if (!byPlayer || crowPrefab == null || _called)
                return;
            for (int i = 0; i < definition.decoyCrows; i++)
                SpawnCrow(decoy.transform.position + Vector3.up * (2.5f + i * 0.4f) + Random.insideUnitSphere);
        }

        public void OnCrowGone(CrowEnemy crow) => _crows.Remove(crow);

        // ---------- Подручные ----------

        /// <summary>Место на навмеше в кольце вокруг точки, в пределах двора.</summary>
        static bool TryFindSpot(Vector3 around, float minDistance, float maxDistance, out Vector3 point)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float distance = Random.Range(minDistance, maxDistance);
                Vector3 candidate = around + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                if (Mathf.Abs(candidate.x) > 20.5f || Mathf.Abs(candidate.z) > 13.5f)
                    continue;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    continue;
                if (Flat(hit.position - around).magnitude < minDistance * 0.8f)
                    continue;
                point = hit.position;
                return true;
            }
            point = default;
            return false;
        }

        void CleanMinions()
        {
            for (int i = _crows.Count - 1; i >= 0; i--)
                if (_crows[i] == null || !_crows[i].isActiveAndEnabled)
                    _crows.RemoveAt(i);
            for (int i = _shadows.Count - 1; i >= 0; i--)
                if (_shadows[i] == null || !_shadows[i].isActiveAndEnabled || _shadows[i].IsDead)
                    _shadows.RemoveAt(i);
            for (int i = _decoys.Count - 1; i >= 0; i--)
                if (_decoys[i] == null || !_decoys[i].isActiveAndEnabled)
                    _decoys.RemoveAt(i);
            if (_crowsAway && _crows.Count == 0 && _state != State.CrowsCast)
                SetCrowsAway(false);
        }

        /// <summary>Вся мелочь исчезает: вороны улетают, тени рассеиваются, ложные чучела осыпаются.</summary>
        void DispelMinions()
        {
            foreach (var crow in _crows)
                if (crow && crow.isActiveAndEnabled)
                    crow.FlyAway();
            _crows.Clear();
            _shadowAt.Clear();
            KillAll(_shadows);
            foreach (var decoy in _decoys)
                if (decoy && decoy.isActiveAndEnabled)
                    decoy.Collapse();
            _decoys.Clear();
        }

        static void KillAll(List<Health> healths)
        {
            var hit = new HitInfo { Damage = 999, Direction = Vector3.up, SourceTeam = Team.Neutral, Flags = HitFlags.Despawn };
            foreach (var health in healths)
                if (health && health.isActiveAndEnabled && !health.IsDead)
                    health.Kill(hit);
            healths.Clear();
        }

        void SetTangible(bool tangible)
        {
            if (bodyCollider)
                bodyCollider.enabled = tangible;
            if (sackCollider)
                sackCollider.enabled = tangible;
            _self.HiddenFromAim = !tangible;
        }

        void SetCrowsAway(bool away)
        {
            _crowsAway = away;
            if (shoulderCrows == null)
                return;
            foreach (var crow in shoulderCrows)
                if (crow)
                    crow.SetActive(!away);
        }

        // ---------- Мама позвала ----------

        void OnMomCalled()
        {
            if (_health.IsDead || _called)
                return;
            _called = true;
            DispelMinions();
            ClearMarker();
            if (_state == State.Vault)
                Land();
            SetTangible(true);
            if (eyes)
                eyes.SetActive(true);
            Halt();
            _nextRageVault = Time.time + definition.rageStagger + 0.5f;
            GameEvents.PlaySound(SoundCue.ShadowHiss, transform.position);
            Enter(State.Stagger, definition.rageStagger);
        }

        // ---------- Попадания ----------

        public BallContactResult OnBallContact(Ball ball, in RaycastHit hit) => OnBallContact(ball, hit, allowCatch: true);

        BallContactResult OnBallContact(Ball ball, in RaycastHit hit, bool allowCatch)
        {
            if (_health.IsDead || _state == State.HideVanish)
                return BallContactResult.PassThrough;
            // Спереди руки ловят — если есть свободная.
            if (allowCatch && CanCatchNow && _held.Count < definition.maxHeld && CanCatch(ball))
            {
                Catch(ball);
                return BallContactResult.Caught;
            }
            Vector3 direction = Flat(ball.Velocity);
            if (direction.sqrMagnitude < 1e-4f)
                direction = -Flat(hit.normal);
            ApplyHit(new HitInfo
            {
                Damage = ball.Stats.Damage,
                Point = hit.point,
                Direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : transform.forward,
                Force = ball.Stats.Knockback,
                SourceTeam = ball.Team,
                Source = ball.Thrower,
                Flags = ball.Stats.Flags,
            });
            return BallContactResult.Hit;
        }

        public bool ApplyHit(in HitInfo hit)
        {
            if (_health.IsDead || _state == State.HideVanish)
                return false;
            var damage = hit;
            bool found = _state == State.Hidden && !hit.Has(HitFlags.Despawn);
            // Нашли настоящего среди ложных — попадание больнее.
            if (found)
                damage.Damage *= definition.revealDamageMultiplier;
            bool strong = hit.Has(HitFlags.Charged) || found;
            GameFeel.Shake(strong ? 0.3f : 0.12f);
            if (hitFlash)
                hitFlash.Flash(found ? new Color(1f, 0.85f, 0.4f) : Color.white, 0.12f);
            GameEvents.PlaySound(strong ? SoundCue.EnemyHitStrong : SoundCue.EnemyHit, hit.Point);
            _health.TryDamage(damage);
            if (found && !_health.IsDead)
                StopHiding(found: true);
            return true;
        }

        void OnDied(HitInfo hit)
        {
            ClearMarker();
            if (_state == State.Vault)
            {
                transform.position = _vaultTo;
                _agent.Warp(_vaultTo);
                _agent.updatePosition = true;
            }
            ReleaseHeld(hit.Direction);
            SpillSack(hit.Direction);
            DispelMinions();
            if (debrisPrefab)
            {
                var debris = PoolService.Spawn(debrisPrefab, transform.position, transform.rotation).GetComponent<Debris>();
                if (debris)
                    debris.Burst(hit.Direction, definition.debrisForce + hit.Force * 0.2f, Vector3.zero);
            }
            Burst(strawPrefab, transform.position + Vector3.up * 2f, 2.5f);
            Burst(smokePrefab, transform.position + Vector3.up * 1.5f, 2.5f);
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.BossSplit, transform.position);
            GameEvents.PlaySound(SoundCue.DecoyBurst, transform.position);
            GameFeel.HitStop(0.1f);
            GameFeel.Shake(1f);
            PoolService.Despawn(gameObject);
        }

        static void Burst(ParticleBurst prefab, Vector3 position, float scale)
        {
            if (prefab)
                PoolService.Spawn(prefab, position, Quaternion.identity).Play(scale);
        }

        // ---------- Движение ----------

        void Halt()
        {
            if (_agent.isOnNavMesh && _agent.updatePosition && !_agent.isStopped)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
        }

        void Face(Vector3 direction, float dt, float speedMultiplier = 1f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f)
                return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction),
                definition.turnSpeed * speedMultiplier * dt);
        }

        void Enter(State state, float length = 0f)
        {
            _state = state;
            _stateTime = 0f;
            _stateLength = length;
            if (state == State.Stalk && _agent.isOnNavMesh && _agent.updatePosition)
                _agent.isStopped = false;
        }

        // ---------- Вид ----------

        void LateUpdate()
        {
            HoldBalls();
            HoldSack();
            float dt = Time.deltaTime;
            float k = _stateLength > 0f ? Mathf.Clamp01(_stateTime / _stateLength) : 0f;
            Vector3 bodyE = Vector3.zero, headE = Vector3.zero, armLE = Vector3.zero, armRE = Vector3.zero;
            float height = 0f, sink = 0f, scale = 1f, follow = 12f;
            float speed01 = _agent.isOnNavMesh && _agent.updatePosition
                ? Mathf.Clamp01(_agent.velocity.magnitude / Mathf.Max(0.1f, definition.moveSpeed)) : 0f;
            _walkPhase += dt * (1.5f + 3f * speed01);
            float sway = Mathf.Sin(_walkPhase);

            switch (_state)
            {
                case State.Appear:
                    sink = 1f - k * k;
                    headE = new Vector3(-10f, 0f, 20f * (1f - k));
                    break;
                case State.SwipeWindup:
                {
                    float w = Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, definition.swipeWindup));
                    bodyE = new Vector3(0f, 28f * w, 0f);
                    armRE = new Vector3(-35f * w, 0f, 95f * w);
                    follow = 20f;
                    break;
                }
                case State.Swipe:
                    bodyE = new Vector3(6f, -32f, 0f);
                    armRE = new Vector3(-80f, 0f, 20f);
                    follow = 40f;
                    break;
                case State.SwipeRecover:
                    bodyE = new Vector3(4f, -12f, 0f);
                    armRE = new Vector3(-30f, 0f, 10f);
                    break;
                case State.Aim:
                {
                    float w = Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, definition.throwWindup));
                    bodyE = new Vector3(-6f * w, -15f * w, 0f);
                    armLE = new Vector3(-165f * w, 0f, -15f * w);
                    follow = 20f;
                    break;
                }
                case State.SweepCrouch:
                    bodyE = new Vector3(40f, 0f, 0f);
                    headE = new Vector3(20f, 0f, 0f);
                    armLE = new Vector3(-55f, 0f, -10f);
                    armRE = new Vector3(-25f, 0f, 10f);
                    sink = 0.12f;
                    break;
                case State.VaultWindup:
                    bodyE = new Vector3(18f * k, 0f, 0f);
                    armRE = new Vector3(-95f * k, 0f, 10f);
                    armLE = new Vector3(-40f * k, 0f, -10f);
                    sink = 0.12f * k;
                    follow = 18f;
                    break;
                case State.Vault:
                {
                    height = Mathf.Sin(k * Mathf.PI) * definition.vaultHeight;
                    bodyE = new Vector3(-20f + 45f * k, 0f, 0f);
                    armRE = new Vector3(-170f, 0f, 5f);
                    armLE = new Vector3(-150f, 0f, -20f);
                    follow = 25f;
                    break;
                }
                case State.VaultStuck:
                {
                    float w = Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, definition.vaultStuck));
                    bodyE = new Vector3(28f * (1f - w), 0f, 0f);
                    headE = new Vector3(15f * (1f - w), 0f, 0f);
                    armRE = new Vector3(-60f * (1f - w), 0f, 10f);
                    sink = 0.15f * (1f - w);
                    break;
                }
                case State.CarouselWindup:
                case State.Carousel:
                    armLE = new Vector3(0f, 0f, -85f);
                    armRE = new Vector3(0f, 0f, 85f);
                    headE = new Vector3(-10f, 0f, 0f);
                    break;
                case State.Dizzy:
                    bodyE = new Vector3(Mathf.Sin(Time.time * 7f) * 8f, 0f, Mathf.Sin(Time.time * 5f) * 12f);
                    headE = new Vector3(0f, Mathf.Sin(Time.time * 6f) * 25f, Mathf.Sin(Time.time * 8f) * 15f);
                    armLE = new Vector3(-20f, 0f, -30f);
                    armRE = new Vector3(-20f, 0f, 30f);
                    break;
                case State.Count:
                {
                    // Закрыл лицо руками, на каждый счёт вздрагивает.
                    float tick = Mathf.Repeat(_stateTime, definition.countTime / 5f) / (definition.countTime / 5f);
                    float nod = Mathf.Exp(-tick * 6f) * 10f;
                    bodyE = new Vector3(10f + nod * 0.5f, 0f, 0f);
                    headE = new Vector3(25f + nod, 0f, 0f);
                    armLE = new Vector3(-150f, 0f, -35f);
                    armRE = new Vector3(-150f, 0f, 35f);
                    break;
                }
                case State.Search:
                    headE = new Vector3(-15f, 0f, 0f);
                    armLE = new Vector3(-30f, 0f, -45f);
                    armRE = new Vector3(-30f, 0f, 45f);
                    follow = 25f;
                    break;
                case State.Volley:
                    armLE = new Vector3(-150f, 0f, -15f);
                    break;
                case State.Confused:
                    headE = new Vector3(5f, Mathf.Sin(_stateTime * 4f) * 45f, 10f);
                    armLE = new Vector3(-20f, 0f, -25f);
                    armRE = new Vector3(-20f, 0f, 25f);
                    break;
                case State.LightsCast:
                    bodyE = new Vector3(-8f * k, 0f, 0f);
                    headE = new Vector3(-25f * k, 0f, 0f);
                    armLE = new Vector3(-175f * k, 0f, -15f);
                    break;
                case State.CrowsCast:
                    headE = new Vector3(-20f * k, 0f, 0f);
                    armLE = new Vector3(-150f * k, 0f, -40f * k);
                    armRE = new Vector3(-150f * k, 0f, 40f * k);
                    break;
                case State.HideVanish:
                    sink = Mathf.Clamp01(_stateTime / Mathf.Max(0.05f, definition.hideVanish));
                    scale = Mathf.Lerp(1f, 0.6f, sink);
                    break;
                case State.Hidden:
                {
                    // Вылезает из земли и стоит пугалом, руки чуть в стороны.
                    float rise = Mathf.Clamp01(_stateTime / Mathf.Max(0.05f, definition.hideRise));
                    sink = 1f - rise;
                    armLE = new Vector3(0f, 0f, -20f);
                    armRE = new Vector3(0f, 0f, 20f);
                    headE = new Vector3(0f, 0f, 12f);
                    break;
                }
                case State.Stagger:
                    bodyE = new Vector3(-14f, 0f, 0f);
                    headE = new Vector3(-12f, 0f, 0f);
                    armLE = new Vector3(-35f, 0f, -40f);
                    armRE = new Vector3(-35f, 0f, 40f);
                    break;
                default:
                    // Скользит к игроку: чуть наклонён вперёд, покачивается, клюка постукивает по асфальту.
                    bodyE = new Vector3(6f + 4f * speed01, 0f, sway * 3f);
                    headE = new Vector3(-6f, 0f, 10f + Mathf.Sin(Time.time * 0.7f) * 6f);
                    armLE = new Vector3(-_catchPose * 70f - _throwPose * 90f + sway * 8f * speed01, 0f, -6f);
                    armRE = new Vector3(-12f - Mathf.Max(0f, sway) * 14f * speed01, 0f, 8f);
                    break;
            }

            _catchPose = Mathf.MoveTowards(_catchPose, _held.Count > 0 ? 0.8f : 0f, dt * 3f);
            _throwPose = Mathf.MoveTowards(_throwPose, 0f, dt * 3f);
            float f = 1f - Mathf.Exp(-follow * dt);
            _bodyEuler = Vector3.Lerp(_bodyEuler, bodyE, f);
            _headEuler = Vector3.Lerp(_headEuler, headE, f);
            _armLEuler = Vector3.Lerp(_armLEuler, armLE, f);
            _armREuler = Vector3.Lerp(_armREuler, armRE, f);
            Apply(body, _bodyRest, _bodyEuler);
            Apply(head, _headRest, _headEuler);
            Apply(armL, _armLRest, _armLEuler);
            Apply(armR, _armRRest, _armREuler);

            if (model)
            {
                // Карусель: вся фигура крутится вокруг клюки.
                _spin = _state == State.Carousel ? _spin + dt * 600f
                    : Mathf.MoveTowards(_spin, Mathf.Round(_spin / 360f) * 360f, dt * 400f);
                model.localPosition = _modelRestPosition + Vector3.up * (height - sink * 5.5f);
                model.localRotation = Quaternion.Euler(0f, _spin, 0f) * _modelRestRotation;
                model.localScale = _modelRestScale * scale;
            }
            if (sack)
            {
                _sackScale = Mathf.MoveTowards(_sackScale, 1f + definition.sackGrowth * _sack.Count, dt * 1.5f);
                sack.localScale = _sackRestScale * _sackScale;
            }
            UpdateEyes();
        }

        /// <summary>
        /// Глаза: в «Прятках» вспыхивают и гаснут — так выдаёт себя настоящий; на «Я иду искать!» горят ярче.
        /// </summary>
        void UpdateEyes()
        {
            bool on = true;
            float glow = 1f;
            if (_state == State.Hidden)
            {
                float cycle = definition.eyePulse.x + definition.eyePulse.y;
                on = cycle <= 0f || Mathf.Repeat(_stateTime, cycle) < definition.eyePulse.x;
            }
            else if (_state is State.Search or State.Volley or State.LightsCast)
                glow = 2.2f;
            else if (_state == State.HideVanish)
                on = false;
            if (eyes && eyes.activeSelf != on)
                eyes.SetActive(on);
            if (eyeLight)
            {
                eyeLight.enabled = on;
                eyeLight.intensity = Mathf.MoveTowards(eyeLight.intensity, 2.5f * glow, Time.deltaTime * 10f);
            }
        }

        static void Apply(Transform part, Quaternion rest, Vector3 euler)
        {
            if (part)
                part.localRotation = rest * Quaternion.Euler(euler);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
