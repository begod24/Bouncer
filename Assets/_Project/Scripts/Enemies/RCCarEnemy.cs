using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class RCCarEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable, INetEnemy
    {
        public enum State
        {
            Cruise,
            Rev,
            Charge,
            Skid,
            Flipped,
        }

        const float RepathInterval = 0.35f;

        [SerializeField] RCCarDefinition definition;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;
        [SerializeField] ParticleBurst skidDust;

        [Header("Вид")]
        [Tooltip("Корпус с колёсами: переворачивается и кренится")]
        [SerializeField] Transform visual;
        [SerializeField] Transform wheelFL;
        [SerializeField] Transform wheelFR;
        [SerializeField] Transform wheelRL;
        [SerializeField] Transform wheelRR;
        [SerializeField] Transform antenna;
        [SerializeField] float wheelRadius = 0.17f;

        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        Targetable _target;
        State _state;
        float _stateTime;
        float _nextRepath;
        float _nextCharge;
        float _orbitSide = 1f;
        bool _chargeHit;
        Vector3 _chargeDirection;
        float _skidSpeed;
        float _skidSign;
        Vector3 _knockback;
        float _wheelAngle;
        float _steer;
        float _flip;
        Vector3 _antennaSwing;
        Vector3 _antennaVelocity;
        Vector3 _lastPosition;

        public State CurrentState => _state;

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
            _agent.speed = definition.cruiseSpeed;
            _agent.acceleration = 20f;
            _agent.stoppingDistance = 0.3f;
            _agent.autoBraking = false;
            _health.Configure(EnemyScaling.Hits(definition.hitsToKill, gameObject), 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _knockback = Vector3.zero;
            _nextRepath = 0f;
            _nextCharge = Time.time + 2f;
            _orbitSide = Random.value < 0.5f ? -1f : 1f;
            _flip = 0f;
            _target = null;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            _lastPosition = transform.position;
            Enter(State.Cruise);
        }

        public void OnDespawned() { }

        void Update()
        {
            if (NetHooks.IsGuest)
                return;
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
                if (_state == State.Cruise)
                    Orbit();
            }
            bool hasTarget = _target != null && _target.IsAlive && GameSession.IsGameplayActive;
            if (_knockback.sqrMagnitude > 0.01f)
            {
                _agent.Move(_knockback * dt);
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-6f * dt));
            }

            switch (_state)
            {
                case State.Cruise:
                    _agent.isStopped = !hasTarget;
                    _agent.speed = definition.cruiseSpeed * _self.SpeedMultiplier * GumSpot.EnemyMoveMultiplierAt(transform.position)
                                   * GroundZone.MoveMultiplierAt(transform.position);
                    if (!hasTarget)
                        break;
                    Vector3 velocity = Flat(_agent.velocity);
                    if (velocity.sqrMagnitude > 0.2f)
                        Face(velocity, dt);
                    Vector3 toTarget = Flat(_target.Position - transform.position);
                    if (Time.time >= _nextCharge && toTarget.magnitude <= definition.chargeRange && CanSee(_target))
                    {
                        Halt();
                        GameEvents.PlaySound(SoundCue.EngineRev, transform.position);
                        Enter(State.Rev);
                    }
                    break;

                case State.Rev:
                    if (hasTarget)
                    {
                        _chargeDirection = Flat(_target.Position - transform.position).normalized;
                        Face(_chargeDirection, dt * 1.5f);
                    }
                    if (_stateTime >= definition.revTime)
                    {
                        _chargeHit = false;
                        if (_chargeDirection.sqrMagnitude < 0.5f)
                            _chargeDirection = transform.forward;
                        transform.rotation = Quaternion.LookRotation(_chargeDirection);
                        GameEvents.PlaySound(SoundCue.Dash, transform.position);
                        Enter(State.Charge);
                    }
                    break;

                case State.Charge:
                    Charge(dt);
                    break;

                case State.Skid:
                    float k = Mathf.Clamp01(_stateTime / Mathf.Max(0.05f, definition.skidTime));
                    float speed = Mathf.Lerp(_skidSpeed, 0f, k);
                    _agent.Move(_chargeDirection * (speed * dt));
                    transform.rotation *= Quaternion.Euler(0f, _skidSign * definition.skidTurn / definition.skidTime * dt, 0f);
                    if (_stateTime >= definition.skidTime)
                    {
                        _nextCharge = Time.time + definition.chargeCooldown;
                        _orbitSide = -_orbitSide;
                        Enter(State.Cruise);
                    }
                    break;

                case State.Flipped:
                    if (_stateTime >= definition.flipTime)
                        Enter(State.Cruise);
                    break;
            }
        }

        void Orbit()
        {
            if (_target == null || !_target.IsAlive || !GameSession.IsGameplayActive)
                return;
            Vector3 fromPlayer = Flat(transform.position - _target.Position);
            if (fromPlayer.sqrMagnitude < 0.01f)
                fromPlayer = Vector3.back;
            Vector3 next = Quaternion.AngleAxis(_orbitSide * 50f, Vector3.up) * fromPlayer.normalized;
            Vector3 desired = _target.Position + next * definition.orbitDistance;
            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
            else
                _orbitSide = -_orbitSide;
        }

        void Charge(float dt)
        {
            Vector3 before = transform.position;
            float step = definition.chargeSpeed * _self.SpeedMultiplier * dt;
            _agent.Move(_chargeDirection * step);
            float moved = Flat(transform.position - before).magnitude;
            if (!_chargeHit && _target != null && _target.IsAlive)
            {
                Vector3 delta = Flat(_target.Position - transform.position);
                if (delta.sqrMagnitude <= definition.hitDistance * definition.hitDistance && _target.TryGetComponent(out IDamageable damageable))
                {
                    _chargeHit = true;
                    damageable.ApplyHit(new HitInfo
                    {
                        Damage = definition.damage,
                        Point = _target.AimPoint,
                        Direction = _chargeDirection,
                        Force = definition.knockback,
                        SourceTeam = Team.Enemy,
                        Source = gameObject,
                        Flags = HitFlags.Melee | HitFlags.Charged,
                    });
                }
            }
            bool blocked = _stateTime > 0.1f && moved < step * 0.3f;
            if (blocked || _stateTime >= definition.chargeTime || _chargeHit)
            {
                if (blocked)
                {
                    GameEvents.PlaySound(SoundCue.AreaThud, transform.position);
                    GameFeel.Shake(0.15f);
                }
                _skidSpeed = blocked ? 0f : definition.chargeSpeed * 0.8f;
                _skidSign = Random.value < 0.5f ? -1f : 1f;
                if (skidDust)
                    PoolService.Spawn(skidDust, transform.position + Vector3.up * 0.1f, Quaternion.identity).Play(0.5f);
                Enter(State.Skid);
            }
        }

        bool CanSee(Targetable target) =>
            !Physics.Linecast(transform.position + Vector3.up * 0.4f, target.Position + Vector3.up * 0.4f,
                Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);

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
            if (hitFlash)
                hitFlash.Flash(Color.white, 0.12f);
            GameEvents.PlaySound(strong ? SoundCue.EnemyHitStrong : SoundCue.EnemyHit, hit.Point);
            _health.TryDamage(hit);
            if (!_health.IsDead)
            {
                _knockback += Flat(hit.Direction) * (hit.Force * definition.knockbackScale);
                Halt();
                Enter(State.Flipped);
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
            PoolService.Despawn(gameObject);
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 velocity = (transform.position - _lastPosition) / dt;
            _lastPosition = transform.position;
            float forward = Vector3.Dot(velocity, transform.forward);
            float spin = _state == State.Rev ? 30f : _state == State.Flipped ? 12f : forward / Mathf.Max(0.05f, wheelRadius);
            _wheelAngle += spin * Mathf.Rad2Deg * dt;
            float targetSteer = _state == State.Skid ? _skidSign * 28f : _state == State.Cruise ? _orbitSide * 14f : 0f;
            _steer = Mathf.MoveTowards(_steer, targetSteer, 160f * dt);
            Quaternion roll = Quaternion.Euler(_wheelAngle, 0f, 0f);
            if (wheelFL)
                wheelFL.localRotation = Quaternion.Euler(0f, _steer, 0f) * roll;
            if (wheelFR)
                wheelFR.localRotation = Quaternion.Euler(0f, _steer, 0f) * roll;
            if (wheelRL)
                wheelRL.localRotation = roll;
            if (wheelRR)
                wheelRR.localRotation = roll;

            _flip = Mathf.MoveTowards(_flip, _state == State.Flipped && _stateTime < definition.flipTime - 0.25f ? 1f : 0f, dt * 5f);
            float lean = _state == State.Skid ? -_skidSign * 8f : _state == State.Rev ? Mathf.Sin(Time.time * 50f) * 1.5f : 0f;
            if (visual)
            {
                visual.localRotation = Quaternion.Euler(0f, 0f, lean + 180f * _flip);
                visual.localPosition = Vector3.up * (0.35f * Mathf.Sin(_flip * Mathf.PI) + 0.5f * _flip);
            }

            if (antenna)
            {
                Vector3 accel = -transform.InverseTransformDirection(velocity) * 0.02f;
                _antennaVelocity += (accel - _antennaSwing) * (40f * dt) - _antennaVelocity * (4f * dt);
                _antennaSwing += _antennaVelocity * dt * 10f;
                _antennaSwing = Vector3.ClampMagnitude(_antennaSwing, 0.6f);
                antenna.localRotation = Quaternion.Euler(_antennaSwing.z * 40f, 0f, -_antennaSwing.x * 40f);
            }
        }

        public void WriteNet(NetWriter writer)
        {
            writer.Byte((byte)_state);
            writer.Seconds(_stateTime);
            writer.Bool(_orbitSide > 0f);
            writer.Bool(_skidSign > 0f);
        }

        public void ReadNet(NetReader reader, float age)
        {
            _state = (State)reader.Byte();
            _stateTime = reader.Seconds() + age;
            _orbitSide = reader.Bool() ? 1f : -1f;
            _skidSign = reader.Bool() ? 1f : -1f;
        }

        void Enter(State state)
        {
            _state = state;
            _stateTime = 0f;
            if (state == State.Cruise && _agent.isOnNavMesh)
                _agent.isStopped = false;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
