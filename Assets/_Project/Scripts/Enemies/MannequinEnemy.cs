using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Манекен («Море волнуется, раз…»). Пока он в секторе взгляда любого игрока (<see cref="Targetable.Sees"/>),
    /// стоит как вкопанный — каждый раз в новой позе. Стоит отвернуться — быстро подкрадывается по NavMesh
    /// и бьёт вблизи; взгляд во время замаха останавливает и удар. Попадать можно всегда: замерший не шатается.
    /// Элитный манекен ещё и бросает сильный мяч в спину, если на него не смотрят.
    /// Модель — жёсткие части (корпус, голова, руки, ноги), позы и ходьба задаются в коде.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class MannequinEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        enum State
        {
            Sneak,
            Frozen,
            Windup,
            Strike,
            Recover,
            Aim,
            Stagger,
        }

        /// <summary>Повороты частей модели от их положения покоя, градусы.</summary>
        struct Pose
        {
            public Vector3 Body, Head, ArmL, ArmR, LegL, LegR;

            public static Pose Lerp(in Pose a, in Pose b, float t) => new()
            {
                Body = Vector3.Lerp(a.Body, b.Body, t),
                Head = Vector3.Lerp(a.Head, b.Head, t),
                ArmL = Vector3.Lerp(a.ArmL, b.ArmL, t),
                ArmR = Vector3.Lerp(a.ArmR, b.ArmR, t),
                LegL = Vector3.Lerp(a.LegL, b.LegL, t),
                LegR = Vector3.Lerp(a.LegR, b.LegR, t),
            };
        }

        const float RepathInterval = 0.25f;
        /// <summary>Рука по оси X: минус — вперёд, −180 — вверх. По оси Z левая уходит в сторону минусом, правая плюсом.</summary>
        static readonly Pose[] FrozenPoses =
        {
            // Тянется вперёд, как зомби.
            new() { Body = new(8f, 0f, 0f), Head = new(12f, 0f, 0f), ArmL = new(-88f, 0f, 6f), ArmR = new(-80f, 0f, -6f), LegL = new(-22f, 0f, 0f), LegR = new(16f, 0f, 0f) },
            // Замер на полушаге.
            new() { Body = new(4f, 0f, 3f), Head = new(0f, 18f, 0f), ArmL = new(28f, 0f, -4f), ArmR = new(-34f, 0f, 4f), LegL = new(-32f, 0f, 0f), LegR = new(24f, 0f, 0f) },
            // Машет рукой.
            new() { Body = new(0f, 0f, -4f), Head = new(0f, 0f, 16f), ArmL = new(6f, 0f, -12f), ArmR = new(0f, 0f, 155f), LegL = new(0f, 0f, -6f), LegR = new(0f, 0f, 4f) },
            // Руки в стороны.
            new() { Body = new(0f, 0f, 0f), Head = new(-10f, 0f, 0f), ArmL = new(0f, 0f, -88f), ArmR = new(0f, 0f, 88f), LegL = new(0f, 0f, -10f), LegR = new(0f, 0f, 10f) },
            // Закрывает лицо.
            new() { Body = new(14f, 0f, 0f), Head = new(20f, 0f, 0f), ArmL = new(-150f, 0f, 30f), ArmR = new(-150f, 0f, -30f), LegL = new(-8f, 0f, 0f), LegR = new(10f, 0f, 0f) },
            // Указывает на тебя.
            new() { Body = new(-4f, 12f, 0f), Head = new(0f, -14f, 0f), ArmL = new(12f, 0f, -8f), ArmR = new(-92f, -10f, 0f), LegL = new(-14f, 0f, 0f), LegR = new(8f, 0f, 0f) },
            // Голова набок, руки за спиной.
            new() { Body = new(-6f, 0f, 0f), Head = new(0f, 0f, 38f), ArmL = new(36f, 0f, 14f), ArmR = new(36f, 0f, -14f), LegL = new(0f, 0f, 0f), LegR = new(0f, 0f, 0f) },
            // Крадётся на цыпочках.
            new() { Body = new(22f, 0f, 0f), Head = new(-18f, 0f, 0f), ArmL = new(-40f, 0f, -20f), ArmR = new(-40f, 0f, 20f), LegL = new(-40f, 0f, 0f), LegR = new(20f, 0f, 0f) },
        };

        [SerializeField] MannequinDefinition definition;

        [Header("Части модели")]
        [SerializeField] Transform body;
        [SerializeField] Transform head;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;
        [SerializeField] Transform legL;
        [SerializeField] Transform legR;

        [Header("Элитный: бросок в спину")]
        [Tooltip("Мяч для броска. Пусто — не бросает")]
        [SerializeField] Ball ballPrefab;
        [Tooltip("Откуда вылетает мяч (рука)")]
        [SerializeField] Transform hand;
        [Tooltip("Мяч в руке: виден, пока бросок готов")]
        [SerializeField] GameObject handBall;

        [Header("Прочее")]
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        Targetable _target;
        State _state;
        float _stateTime;
        float _nextRepath;
        float _nextAttack;
        float _nextThrow;
        float _unwatchedTime;
        bool _struck;
        Vector3 _knockback;
        Vector3 _attackDirection;
        Quaternion _bodyRest, _headRest, _armLRest, _armRRest, _legLRest, _legRRest;
        Pose _current;
        Pose _frozenPose;
        int _lastPose = -1;
        float _walkPhase;

        public MannequinDefinition Definition => definition;
        /// <summary>Сейчас замер (на него смотрят или он заморожен).</summary>
        public bool IsStill => _state == State.Frozen;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _agent.updateRotation = false;
            _health.Died += OnDied;
            Cache(body, ref _bodyRest);
            Cache(head, ref _headRest);
            Cache(armL, ref _armLRest);
            Cache(armR, ref _armRRest);
            Cache(legL, ref _legLRest);
            Cache(legR, ref _legRRest);
            ApplyDefinition();
        }

        static void Cache(Transform part, ref Quaternion rest)
        {
            if (part)
                rest = part.localRotation;
        }

        public void ApplyDefinition()
        {
            _agent.speed = definition.sneakSpeed;
            _agent.acceleration = 40f;
            _agent.stoppingDistance = definition.attackRange * 0.7f;
            _health.Configure(definition.hitsToKill, 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _knockback = Vector3.zero;
            _nextRepath = 0f;
            _nextAttack = Time.time + 0.8f;
            _nextThrow = Time.time + definition.backThrowCooldown * 0.5f;
            _target = null;
            _current = default;
            _walkPhase = Random.value * 10f;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            // Появляется замершим — в первой позе.
            FreezeInPose(sound: false);
        }

        public void OnDespawned() { }

        // ---------- Мозги ----------

        void Update()
        {
            float dt = Time.deltaTime;
            if (!_agent.isOnNavMesh)
                return;
            if (Time.time >= _nextRepath)
            {
                _nextRepath = Time.time + RepathInterval;
                _target = Targetable.FindNearest(transform.position, Team.Player);
                if (_target != null && _state == State.Sneak)
                    _agent.SetDestination(_target.Position);
            }
            bool active = GameSession.IsGameplayActive;
            bool hasTarget = _target != null && _target.IsAlive && active;
            bool watched = !active || _self.IsFrozen || IsWatched();
            Vector3 toTarget = hasTarget ? Flat(_target.Position - transform.position) : Vector3.zero;
            float distance = toTarget.magnitude;
            _unwatchedTime = watched ? 0f : _unwatchedTime + dt;
            _stateTime += dt;

            if (_knockback.sqrMagnitude > 0.01f)
            {
                _agent.Move(_knockback * dt);
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-8f * dt));
            }

            switch (_state)
            {
                case State.Sneak:
                    if (watched || !hasTarget)
                    {
                        FreezeInPose(sound: hasTarget);
                        break;
                    }
                    _agent.isStopped = false;
                    _agent.speed = definition.sneakSpeed * GumSpot.EnemyMoveMultiplierAt(transform.position)
                                                         * GroundZone.MoveMultiplierAt(transform.position);
                    Vector3 velocity = Flat(_agent.velocity);
                    Face(velocity.sqrMagnitude > 0.3f ? velocity : toTarget, dt);
                    if (distance <= definition.attackRange && Time.time >= _nextAttack)
                    {
                        _attackDirection = toTarget / Mathf.Max(0.01f, distance);
                        Halt();
                        Enter(State.Windup);
                    }
                    else if (CanBackThrow(distance))
                    {
                        Halt();
                        Enter(State.Aim);
                    }
                    break;

                case State.Frozen:
                    Halt();
                    if (hasTarget && _unwatchedTime >= definition.unfreezeDelay)
                        Enter(State.Sneak);
                    break;

                case State.Windup:
                    // Взгляд во время замаха останавливает удар.
                    if (watched)
                    {
                        _nextAttack = Time.time + 0.4f;
                        FreezeInPose(sound: true);
                        break;
                    }
                    if (hasTarget)
                    {
                        _attackDirection = toTarget / Mathf.Max(0.01f, distance);
                        Face(_attackDirection, dt);
                    }
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
                        Enter(State.Sneak);
                    }
                    break;

                case State.Aim:
                    if (watched || !hasTarget)
                    {
                        _nextThrow = Time.time + definition.backThrowCooldown * 0.5f;
                        FreezeInPose(sound: true);
                        break;
                    }
                    Face(toTarget, dt);
                    if (_stateTime >= definition.backThrowWindup)
                    {
                        Throw(_target);
                        Enter(State.Sneak);
                    }
                    break;

                case State.Stagger:
                    if (_stateTime >= definition.staggerTime)
                        Enter(State.Sneak);
                    break;
            }
        }

        /// <summary>На манекен смотрит хоть один игрок, и между ними нет стены.</summary>
        bool IsWatched()
        {
            Vector3 chest = transform.position + Vector3.up * 1.2f;
            foreach (var viewer in Targetable.All)
            {
                if (viewer.Team != Team.Player || !viewer.IsAlive || !viewer.Sees(chest))
                    continue;
                Vector3 eye = viewer.Position + Vector3.up * 1.4f;
                if (!Physics.Linecast(eye, chest, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                    return true;
            }
            return false;
        }

        bool CanBackThrow(float distance) =>
            ballPrefab && definition.backThrowMinRange > 0f && Time.time >= _nextThrow
            && distance >= definition.backThrowMinRange && distance <= definition.backThrowMaxRange
            && !Physics.Linecast(transform.position + Vector3.up * 1.3f, _target.AimPoint, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);

        void FreezeInPose(bool sound)
        {
            Halt();
            if (_state != State.Frozen)
            {
                // Каждая остановка — новая поза, не та же, что в прошлый раз.
                int pose = Random.Range(0, FrozenPoses.Length - 1);
                if (pose >= _lastPose && _lastPose >= 0)
                    pose++;
                _lastPose = pose;
                _frozenPose = FrozenPoses[pose];
                if (sound)
                    GameEvents.PlaySound(SoundCue.MannequinPose, transform.position);
            }
            Enter(State.Frozen);
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

        /// <summary>Элитный манекен: сильный мяч в спину — удержит только идеальная ловля, а спиной не поймать вовсе.</summary>
        void Throw(Targetable target)
        {
            _nextThrow = Time.time + definition.backThrowCooldown;
            if (target == null)
                return;
            Vector3 origin = hand ? hand.position : transform.position + Vector3.up * 1.5f;
            Vector3 aim = target.AimPoint;
            Vector3 flat = Flat(aim - origin);
            aim += Flat(target.Velocity) * (flat.magnitude / definition.ballSpeed * 0.6f);
            flat = Flat(aim - origin);
            float distance = flat.magnitude;
            if (distance < 0.5f)
                return;
            float time = distance / definition.ballSpeed;
            float up = (aim.y - origin.y + 0.5f * definition.ballGravity * time * time) / time;
            var ball = PoolService.Spawn(ballPrefab, origin, Quaternion.identity);
            ball.Launch(new BallThrow
            {
                Origin = origin,
                Direction = flat / distance,
                Team = Team.Enemy,
                Thrower = gameObject,
                Stats = new ThrowStats
                {
                    Speed = definition.ballSpeed,
                    UpVelocity = up,
                    Gravity = definition.ballGravity,
                    Damage = definition.ballDamage,
                    Knockback = definition.ballKnockback,
                    Flags = HitFlags.Charged,
                },
            });
            GameEvents.PlaySound(SoundCue.ThrowCharged, origin);
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
            if (_health.IsDead)
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
            if (_health.IsDead)
                return false;
            bool strong = hit.Has(HitFlags.Charged);
            GameFeel.Shake(strong ? 0.25f : 0.1f);
            if (hitFlash)
                hitFlash.Flash(Color.white, 0.12f);
            GameEvents.PlaySound(strong ? SoundCue.EnemyHitStrong : SoundCue.EnemyHit, hit.Point);
            _health.TryDamage(hit);
            if (!_health.IsDead && _state != State.Frozen)
            {
                // Замерший стоит как вкопанный; идущего сбивает с шага.
                _knockback += Flat(hit.Direction) * (hit.Force * definition.knockbackScale);
                Halt();
                Enter(State.Stagger);
            }
            return true;
        }

        void OnDied(HitInfo hit)
        {
            if (debrisPrefab)
            {
                var debris = PoolService.Spawn(debrisPrefab, transform.position, transform.rotation).GetComponent<Debris>();
                if (debris)
                    debris.Burst(hit.Direction, definition.debrisForce + hit.Force * 0.2f, Vector3.zero);
            }
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.SoldierPop, transform.position);
            GameFeel.Shake(0.3f);
            PoolService.Despawn(gameObject);
        }

        // ---------- Вид ----------

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (handBall)
            {
                bool ready = ballPrefab && definition.backThrowMinRange > 0f && (Time.time >= _nextThrow || _state == State.Aim);
                if (handBall.activeSelf != ready)
                    handBall.SetActive(ready);
            }

            Pose want;
            float follow;
            switch (_state)
            {
                case State.Frozen:
                    want = _frozenPose;
                    follow = 1f / Mathf.Max(0.01f, definition.poseSnapTime);
                    break;
                case State.Windup:
                {
                    float k = Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, definition.windupTime));
                    want = new Pose
                    {
                        Body = new Vector3(-12f * k, 0f, 0f),
                        Head = new Vector3(-10f * k, 0f, 0f),
                        ArmL = new Vector3(-150f * k, 0f, 10f),
                        ArmR = new Vector3(-150f * k, 0f, -10f),
                        LegL = new Vector3(-10f, 0f, 0f),
                        LegR = new Vector3(15f, 0f, 0f),
                    };
                    follow = 30f;
                    break;
                }
                case State.Strike:
                    want = new Pose
                    {
                        Body = new Vector3(24f, 0f, 0f),
                        Head = new Vector3(10f, 0f, 0f),
                        ArmL = new Vector3(-60f, 0f, 6f),
                        ArmR = new Vector3(-60f, 0f, -6f),
                        LegL = new Vector3(-30f, 0f, 0f),
                        LegR = new Vector3(20f, 0f, 0f),
                    };
                    follow = 40f;
                    break;
                case State.Aim:
                {
                    float k = Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, definition.backThrowWindup));
                    want = new Pose
                    {
                        Body = new Vector3(-8f * k, -20f * k, 0f),
                        Head = new Vector3(0f, 10f, 0f),
                        ArmL = new Vector3(-50f, 0f, -10f),
                        ArmR = new Vector3(-170f * k, 0f, 20f * k),
                        LegL = new Vector3(-20f, 0f, 0f),
                        LegR = new Vector3(18f, 0f, 0f),
                    };
                    follow = 25f;
                    break;
                }
                case State.Stagger:
                    want = new Pose
                    {
                        Body = new Vector3(-18f, 0f, 8f),
                        Head = new Vector3(-20f, 0f, 0f),
                        ArmL = new Vector3(20f, 0f, -30f),
                        ArmR = new Vector3(20f, 0f, 30f),
                        LegL = new Vector3(12f, 0f, 0f),
                        LegR = new Vector3(-8f, 0f, 0f),
                    };
                    follow = 25f;
                    break;
                default:
                {
                    // Ходьба рывками, как на покадровой съёмке: поза меняется ступеньками.
                    float speed01 = _agent.isOnNavMesh ? Mathf.Clamp01(_agent.velocity.magnitude / Mathf.Max(0.1f, definition.sneakSpeed)) : 0f;
                    _walkPhase += dt * 9f * Mathf.Max(0.2f, speed01);
                    float stepped = Mathf.Floor(_walkPhase * 1.6f) / 1.6f;
                    float swing = Mathf.Sin(stepped) * 34f * Mathf.Max(0.3f, speed01);
                    want = new Pose
                    {
                        Body = new Vector3(10f, 0f, Mathf.Sin(stepped) * 4f),
                        Head = new Vector3(-8f, 0f, 0f),
                        ArmL = new Vector3(swing * 0.8f, 0f, -6f),
                        ArmR = new Vector3(-swing * 0.8f, 0f, 6f),
                        LegL = new Vector3(-swing, 0f, 0f),
                        LegR = new Vector3(swing, 0f, 0f),
                    };
                    follow = 60f;
                    break;
                }
            }
            _current = Pose.Lerp(_current, want, 1f - Mathf.Exp(-follow * dt));
            Apply(body, _bodyRest, _current.Body);
            Apply(head, _headRest, _current.Head);
            Apply(armL, _armLRest, _current.ArmL);
            Apply(armR, _armRRest, _current.ArmR);
            Apply(legL, _legLRest, _current.LegL);
            Apply(legR, _legRRest, _current.LegR);
        }

        static void Apply(Transform part, Quaternion rest, Vector3 euler)
        {
            if (part)
                part.localRotation = rest * Quaternion.Euler(euler);
        }

        void Enter(State state)
        {
            _state = state;
            _stateTime = 0f;
            if (state == State.Sneak && _agent.isOnNavMesh)
                _agent.isStopped = false;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
