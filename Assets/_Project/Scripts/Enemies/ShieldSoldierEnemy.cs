using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Солдатик с крышкой от кастрюли. Идёт на игрока, держа крышку перед собой, и медленно поворачивается:
    /// мяч спереди отскакивает от крышки, как от стены. Бить его надо сбоку, сзади, рикошетом от бортов или
    /// бумерангом. Сильный (заряженный) мяч выбивает крышку в сторону — пару секунд он открыт со всех сторон.
    /// Вблизи замахивается половником и бьёт крышкой.
    /// По сети думает, отбивает и бьёт только у хозяина комнаты; у гостя копия (с крышкой) по его вестям.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class ShieldSoldierEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable, INetEnemy
    {
        public enum State
        {
            Advance,
            Windup,
            Bash,
            Recover,
            Stagger,
        }

        const float RepathInterval = 0.3f;
        const float RecoverTime = 0.35f;

        static readonly Color BlockFlash = new(0.85f, 0.9f, 1f);

        [SerializeField] ShieldSoldierDefinition definition;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        Targetable _target;
        State _state;
        float _stateTime;
        float _nextRepath;
        float _nextBash;
        float _guardBrokenUntil;
        bool _bashHit;
        Vector3 _knockback;

        public ShieldSoldierDefinition Definition => definition;
        public State CurrentState => _state;
        public float StateTime => _stateTime;
        /// <summary>Крышка перед собой: мячи спереди отскакивают.</summary>
        public bool GuardUp => Time.time >= _guardBrokenUntil;
        /// <summary>1 сразу после отбитого мяча, затухает: крышка вздрагивает.</summary>
        public float BlockKick { get; private set; }
        public Vector3 PlanarVelocity => NetHooks.IsGuest ? Flat(_self.Velocity)
            : _agent.isOnNavMesh ? Flat(_agent.velocity) : Vector3.zero;
        public Vector3 LastHitDirection { get; private set; } = Vector3.back;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _agent.updateRotation = false;
            _health.Died += OnDied;
            ApplyDefinition();
        }

        public void ApplyDefinition()
        {
            _agent.speed = definition.moveSpeed;
            _agent.acceleration = 16f;
            _agent.stoppingDistance = 1.1f;
            _agent.autoBraking = true;
            _health.Configure(EnemyScaling.Hits(definition.hitsToKill, gameObject), 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _knockback = Vector3.zero;
            _nextRepath = 0f;
            _nextBash = Time.time + 1f;
            _guardBrokenUntil = 0f;
            BlockKick = 0f;
            _target = null;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            Enter(State.Advance);
        }

        public void OnDespawned() { }

        // ---------- Мозги ----------

        void Update()
        {
            float dt = Time.deltaTime;
            if (NetHooks.IsGuest)
            {
                BlockKick = Mathf.MoveTowards(BlockKick, 0f, dt * 4f);
                return;
            }
            if (!_agent.isOnNavMesh)
                return;
            if (_self.IsFrozen)
            {
                Halt();
                return;
            }
            _stateTime += dt;
            BlockKick = Mathf.MoveTowards(BlockKick, 0f, dt * 4f);

            if (Time.time >= _nextRepath)
            {
                _nextRepath = Time.time + RepathInterval;
                _target = Targetable.FindNearest(transform.position, Team.Player);
                if (_target != null && _state == State.Advance && GameSession.IsGameplayActive)
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

            switch (_state)
            {
                case State.Advance:
                    if (!hasTarget)
                    {
                        Halt();
                        break;
                    }
                    _agent.isStopped = false;
                    _agent.speed = definition.moveSpeed * _self.SpeedMultiplier * GumSpot.EnemyMoveMultiplierAt(transform.position)
                                   * GroundZone.MoveMultiplierAt(transform.position);
                    // Крышкой всегда к игроку — но поворачивается медленно.
                    Face(toTarget, dt);
                    if (distance <= definition.bashRange && Time.time >= _nextBash)
                    {
                        Halt();
                        Enter(State.Windup);
                    }
                    break;

                case State.Windup:
                    if (hasTarget)
                        Face(toTarget, dt);
                    if (_stateTime >= definition.bashWindup)
                    {
                        _bashHit = false;
                        GameEvents.PlaySound(SoundCue.SwingBat, transform.position);
                        Enter(State.Bash);
                    }
                    break;

                case State.Bash:
                    _agent.Move(transform.forward * (definition.bashLunge * dt));
                    TryBashHit();
                    if (_stateTime >= definition.bashTime)
                    {
                        _nextBash = Time.time + definition.bashCooldown;
                        Enter(State.Recover);
                    }
                    break;

                case State.Recover:
                    if (_stateTime >= RecoverTime)
                        Enter(State.Advance);
                    break;

                case State.Stagger:
                    if (_stateTime >= definition.staggerTime)
                        Enter(State.Advance);
                    break;
            }
        }

        void TryBashHit()
        {
            if (_bashHit || _target == null || !_target.IsAlive)
                return;
            Vector3 delta = Flat(_target.Position - transform.position);
            if (delta.sqrMagnitude > (definition.bashRange + 0.3f) * (definition.bashRange + 0.3f)
                || Vector3.Dot(delta.normalized, transform.forward) < 0.3f
                || !_target.TryGetComponent(out IDamageable damageable))
                return;
            _bashHit = true;
            damageable.ApplyHit(new HitInfo
            {
                Damage = definition.bashDamage,
                Point = _target.AimPoint,
                Direction = transform.forward,
                Force = definition.bashKnockback,
                SourceTeam = Team.Enemy,
                Source = gameObject,
                Flags = HitFlags.Melee,
            });
            GameEvents.PlaySound(SoundCue.ShieldBlock, _target.AimPoint);
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
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction),
                definition.turnSpeed * dt);
        }

        // ---------- Попадания ----------

        public BallContactResult OnBallContact(Ball ball, in RaycastHit hit)
        {
            if (_health.IsDead)
                return BallContactResult.PassThrough;

            Vector3 incoming = Flat(ball.Velocity);
            if (incoming.sqrMagnitude < 1e-4f)
                incoming = -Flat(hit.normal);
            incoming.Normalize();

            // Мяч летит в крышку: спереди, пока крышка поднята.
            if (GuardUp && Vector3.Angle(transform.forward, -incoming) <= definition.guardHalfAngle)
            {
                GameEvents.PlaySound(SoundCue.ShieldBlock, hit.point);
                if (!ball.Stats.Has(HitFlags.Charged))
                {
                    BlockKick = 1f;
                    if (hitFlash)
                        hitFlash.Flash(BlockFlash, 0.08f);
                    return BallContactResult.Bounce;
                }
                // Сильный мяч выбивает крышку — и попадание засчитывается.
                _guardBrokenUntil = Time.time + definition.guardBrokenTime;
            }

            ApplyHit(new HitInfo
            {
                Damage = ball.Stats.Damage,
                Point = hit.point,
                Direction = incoming,
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
            LastHitDirection = hit.Direction;

            _health.TryDamage(hit);
            if (!_health.IsDead)
            {
                _knockback += Flat(hit.Direction) * (hit.Force * definition.knockbackScale);
                if (_state != State.Bash)
                {
                    Halt();
                    Enter(State.Stagger);
                }
            }
            return true;
        }

        void OnDied(HitInfo hit)
        {
            if (debrisPrefab)
            {
                var debris = PoolService.Spawn(debrisPrefab, transform.position, transform.rotation).GetComponent<Debris>();
                if (debris)
                    debris.Burst(hit.Direction, definition.debrisForce + hit.Force * 0.25f, PlanarVelocity);
            }
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.SoldierPop, transform.position);
            GameFeel.Shake(0.25f);
            PoolService.Despawn(gameObject);
        }

        // ---------- Сеть ----------

        public void WriteNet(NetWriter writer)
        {
            writer.Byte((byte)_state);
            writer.Seconds(_stateTime);
            writer.Seconds(Mathf.Max(0f, _guardBrokenUntil - Time.time));
            writer.Byte((byte)Mathf.RoundToInt(Mathf.Clamp01(BlockKick) * 255f));
            writer.Direction(LastHitDirection);
        }

        public void ReadNet(NetReader reader, float age)
        {
            _state = (State)reader.Byte();
            _stateTime = reader.Seconds() + age;
            float broken = reader.Seconds() - age;
            _guardBrokenUntil = broken > 0f ? Time.time + broken : 0f;
            // Вздрагивание крышки: новое начинается с прихода, а не тянется с каждой вестью.
            float kick = reader.Byte() / 255f;
            if (kick > BlockKick + 0.3f)
                BlockKick = kick;
            LastHitDirection = reader.Direction();
        }

        // ---------- Служебное ----------

        void Enter(State state)
        {
            _state = state;
            _stateTime = 0f;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
