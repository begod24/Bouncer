using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Тень. Выходит из темноты (спавнер ставит её только в неосвещённые точки) и плывёт к игроку.
    /// В темноте мячи и взрывы проходят сквозь неё, на свету фонаря она твёрдая — чтобы напасть на игрока
    /// под фонарём, ей приходится выйти на свет. На замахе тень плотнеет где угодно: это окно, чтобы попасть.
    /// Маленький круг света вокруг игрока светом не считается, «Фонарик» — считается (<see cref="LightZone.IsLit"/>).
    /// Элитная тень гасит фонари, мимо которых пролетает. Вокруг тела всё время клубится дым.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class ShadowEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        enum State
        {
            Emerge,
            Chase,
            Windup,
            Strike,
            Recover,
            Stagger,
        }

        const float RepathInterval = 0.3f;

        [SerializeField] ShadowDefinition definition;

        [Header("Вид")]
        [Tooltip("Корень модели: парит и покачивается")]
        [SerializeField] Transform visual;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;
        [Tooltip("Дым вокруг тела: гуще, пока тень бесплотная")]
        [SerializeField] ParticleSystem smoke;
        [Tooltip("Клуб дыма, когда тень выбита, из пула")]
        [SerializeField] ParticleBurst poof;
        [SerializeField] HitFlash hitFlash;
        [Tooltip("Цвет, в который темнеет бесплотная тень")]
        [SerializeField] Color ghostTint = new(0.02f, 0.02f, 0.05f);
        [SerializeField, Range(0f, 1f)] float ghostTintAmount = 0.75f;
        [SerializeField] float smokeRateGhost = 26f;
        [SerializeField] float smokeRateSolid = 8f;

        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        Targetable _target;
        State _state;
        float _stateTime;
        float _nextRepath;
        float _nextAttack;
        float _nextPutOut;
        bool _struck;
        float _solid01;
        Vector3 _knockback;
        Vector3 _visualRest;
        Vector3 _visualScale = Vector3.one;
        Quaternion _armLRest, _armRRest;
        float _bobPhase;

        public ShadowDefinition Definition => definition;

        /// <summary>
        /// Твёрдая: мячи и удары по ней проходят. На свету — всегда, в темноте — только пока замахивается и бьёт.
        /// Пока проявляется из темноты — бесплотна в любом случае.
        /// </summary>
        public bool IsSolid => _state != State.Emerge
                               && (_state is State.Windup or State.Strike || LightZone.IsLit(transform.position));

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _agent.updateRotation = false;
            _health.Died += OnDied;
            if (visual)
            {
                _visualRest = visual.localPosition;
                _visualScale = visual.localScale;
            }
            if (armL)
                _armLRest = armL.localRotation;
            if (armR)
                _armRRest = armR.localRotation;
            ApplyDefinition();
        }

        public void ApplyDefinition()
        {
            _agent.speed = definition.moveSpeed;
            _agent.acceleration = 16f;
            _agent.stoppingDistance = definition.attackRange * 0.7f;
            _health.Configure(definition.hitsToKill, 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _knockback = Vector3.zero;
            _nextRepath = 0f;
            _nextAttack = Time.time + 0.6f;
            _nextPutOut = Time.time + 2f;
            _target = null;
            _solid01 = 0f;
            _bobPhase = Random.value * 10f;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            if (smoke)
            {
                smoke.Clear(true);
                smoke.Play(true);
            }
            GameEvents.PlaySound(SoundCue.ShadowHiss, transform.position);
            Enter(State.Emerge);
        }

        public void OnDespawned()
        {
            if (smoke)
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // ---------- Мозги ----------

        void Update()
        {
            float dt = Time.deltaTime;
            if (!_agent.isOnNavMesh)
                return;
            if (_self.IsFrozen)
            {
                Halt();
                return;
            }
            _stateTime += dt;
            if (Time.time >= _nextRepath)
            {
                _nextRepath = Time.time + RepathInterval;
                _target = Targetable.FindNearest(transform.position, Team.Player);
                if (_target != null && _state == State.Chase)
                    _agent.SetDestination(_target.Position);
            }
            bool hasTarget = _target != null && _target.IsAlive && GameSession.IsGameplayActive;
            Vector3 toTarget = hasTarget ? Flat(_target.Position - transform.position) : Vector3.zero;
            float distance = toTarget.magnitude;

            if (_knockback.sqrMagnitude > 0.01f)
            {
                _agent.Move(_knockback * dt);
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-8f * dt));
            }
            if (definition.putOutReach > 0f)
                PutOutLamps();

            switch (_state)
            {
                case State.Emerge:
                    Halt();
                    if (hasTarget)
                        Face(toTarget, dt);
                    if (_stateTime >= definition.emergeTime)
                        Enter(State.Chase);
                    break;

                case State.Chase:
                    _agent.isStopped = !hasTarget;
                    if (!hasTarget)
                        break;
                    _agent.speed = definition.moveSpeed * GumSpot.EnemyMoveMultiplierAt(transform.position);
                    Vector3 velocity = Flat(_agent.velocity);
                    Face(velocity.sqrMagnitude > 0.3f ? velocity : toTarget, dt);
                    if (distance <= definition.attackRange && Time.time >= _nextAttack)
                    {
                        Halt();
                        Enter(State.Windup);
                        GameEvents.PlaySound(SoundCue.ShadowHiss, transform.position);
                    }
                    break;

                case State.Windup:
                    if (hasTarget)
                        Face(toTarget, dt);
                    if (_stateTime >= definition.windupTime)
                    {
                        _struck = false;
                        Enter(State.Strike);
                    }
                    break;

                case State.Strike:
                    if (!_struck)
                    {
                        _struck = true;
                        Strike();
                    }
                    if (_stateTime >= definition.strikeTime)
                        Enter(State.Recover);
                    break;

                case State.Recover:
                    if (_stateTime >= definition.recoverTime)
                    {
                        _nextAttack = Time.time + definition.attackCooldown;
                        Enter(State.Chase);
                    }
                    break;

                case State.Stagger:
                    if (_stateTime >= definition.staggerTime)
                        Enter(State.Chase);
                    break;
            }
        }

        void Strike()
        {
            GameEvents.PlaySound(SoundCue.SwingBat, transform.position);
            if (_target == null || !_target.IsAlive)
                return;
            Vector3 toTarget = Flat(_target.Position - transform.position);
            if (toTarget.magnitude > definition.strikeRange || Vector3.Angle(transform.forward, toTarget) > definition.strikeHalfAngle)
                return;
            if (_target.TryGetComponent(out IDamageable damageable))
            {
                damageable.ApplyHit(new HitInfo
                {
                    Damage = definition.damage,
                    Point = _target.AimPoint,
                    Direction = toTarget.normalized,
                    Force = definition.knockback,
                    SourceTeam = Team.Enemy,
                    Source = gameObject,
                    Flags = HitFlags.Melee,
                });
            }
        }

        /// <summary>Элитная тень: подлетела к фонарю — он мигает и гаснет на несколько секунд.</summary>
        void PutOutLamps()
        {
            if (Time.time < _nextPutOut)
                return;
            var lamp = LightZone.FindNearestOn(transform.position, definition.putOutReach);
            if (lamp == null)
                return;
            _nextPutOut = Time.time + definition.putOutCooldown;
            lamp.PutOut(definition.putOutTime);
            GameEvents.PlaySound(SoundCue.LampOut, lamp.Center);
        }

        void Halt()
        {
            if (_agent.isOnNavMesh && !_agent.isStopped)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
        }

        void Face(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f)
                return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), definition.turnSpeed * dt);
        }

        // ---------- Попадания ----------

        public BallContactResult OnBallContact(Ball ball, in RaycastHit hit)
        {
            // В темноте мяч проходит насквозь, как сквозь дым.
            if (_health.IsDead || !IsSolid)
                return BallContactResult.PassThrough;
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
            // Бесплотную тень не берут ни взрывы, ни подкат — только арена пройдена (исчезают все).
            if (_health.IsDead || (!IsSolid && !hit.Has(HitFlags.Despawn)))
                return false;
            bool strong = hit.Has(HitFlags.Charged);
            GameFeel.Shake(strong ? 0.25f : 0.1f);
            if (hitFlash)
                hitFlash.Flash(new Color(0.75f, 0.8f, 1f), 0.15f);
            GameEvents.PlaySound(strong ? SoundCue.EnemyHitStrong : SoundCue.EnemyHit, hit.Point);
            _health.TryDamage(hit);
            if (!_health.IsDead)
            {
                _knockback += Flat(hit.Direction) * (hit.Force * definition.knockbackScale);
                // Попадание сбивает замах.
                if (_state == State.Windup)
                {
                    _nextAttack = Time.time + definition.attackCooldown * 0.5f;
                    Halt();
                    Enter(State.Stagger);
                }
            }
            return true;
        }

        void OnDied(HitInfo hit)
        {
            // Тень не разваливается на части — рассеивается дымом.
            if (poof)
                PoolService.Spawn(poof, transform.position + Vector3.up * 1f, Quaternion.identity);
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.ShadowHiss, transform.position);
            GameFeel.Shake(0.2f);
            PoolService.Despawn(gameObject);
        }

        // ---------- Вид ----------

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            _solid01 = Mathf.MoveTowards(_solid01, IsSolid ? 1f : 0f, dt * 5f);
            if (hitFlash)
                hitFlash.SetTint(ghostTint, ghostTintAmount * (1f - _solid01));
            if (smoke)
            {
                var emission = smoke.emission;
                emission.rateOverTimeMultiplier = Mathf.Lerp(smokeRateGhost, smokeRateSolid, _solid01);
            }
            if (visual)
            {
                _bobPhase += dt * 2.2f;
                float emerge = _state == State.Emerge ? Mathf.Clamp01(_stateTime / Mathf.Max(0.05f, definition.emergeTime)) : 1f;
                // Проявляется из темноты: вырастает из клуба дыма у земли.
                visual.localScale = _visualScale * Mathf.Lerp(0.3f, 1f, emerge * emerge);
                visual.localPosition = _visualRest + Vector3.up * (0.15f + Mathf.Sin(_bobPhase) * 0.08f - (1f - emerge) * 0.6f);
            }
            float reach = _state switch
            {
                State.Windup => Mathf.Lerp(0f, -150f, Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, definition.windupTime))),
                State.Strike => -40f,
                State.Recover => Mathf.Lerp(-40f, 0f, Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, definition.recoverTime))),
                _ => Mathf.Sin(_bobPhase * 1.3f) * 10f,
            };
            if (armL)
                armL.localRotation = _armLRest * Quaternion.Euler(reach, 0f, 0f);
            if (armR)
                armR.localRotation = _armRRest * Quaternion.Euler(reach, 0f, 0f);
        }

        void Enter(State state)
        {
            _state = state;
            _stateTime = 0f;
            if (state == State.Chase && _agent.isOnNavMesh)
                _agent.isStopped = false;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
