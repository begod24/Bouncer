using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class LunokhodEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable, INetEnemy
    {
        public enum State
        {
            Drive,
            Open,
            Hold,
            Close,
        }

        const float RepathInterval = 0.6f;

        [SerializeField] LunokhodDefinition definition;
        [SerializeField] Ball ballPrefab;
        [Tooltip("Откуда вылетают ёжики (Lunokhod_Muzzle)")]
        [SerializeField] Transform muzzle;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        [Header("Вид")]
        [SerializeField] Transform lid;
        [Tooltip("Насколько открывается крышка, градусов вокруг X (минус — передний край вверх)")]
        [SerializeField] float lidOpenAngle = -75f;
        [SerializeField] Transform[] wheels;
        [SerializeField] float wheelRadius = 0.2f;
        [Tooltip("Корпус: качается от попаданий и на ходу")]
        [SerializeField] Transform body;

        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        Targetable _target;
        readonly BossArmor _armor = new();
        State _state;
        float _stateTime;
        float _nextRepath;
        float _nextFire;
        float _lid;
        float _wheelAngle;
        float _jolt;
        Quaternion _lidRest;
        Quaternion _bodyRest;
        Vector3 _knockback;

        public bool LidOpen => _state is State.Hold || (_state == State.Open && _stateTime > definition.lidOpenTime * 0.5f);

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _agent.updateRotation = false;
            _health.Died += OnDied;
            _health.Damaged += _ =>
            {
                if (NetHooks.IsGuest)
                    _jolt = 1f;
            };
            if (lid)
                _lidRest = lid.localRotation;
            if (body)
                _bodyRest = body.localRotation;
            ApplyDefinition();
        }

        public void ApplyDefinition()
        {
            _agent.speed = definition.moveSpeed;
            _agent.acceleration = 4f;
            _agent.stoppingDistance = 1f;
            _health.Configure(EnemyScaling.Hits(definition.hitsToKill, gameObject), 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _armor.Reset();
            _knockback = Vector3.zero;
            _nextRepath = 0f;
            _nextFire = Time.time + definition.firstFireDelay;
            _lid = 0f;
            _target = null;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            Enter(State.Drive);
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
                if (_state == State.Drive)
                    Reposition();
            }
            bool hasTarget = _target != null && _target.IsAlive && GameSession.IsGameplayActive;
            if (_knockback.sqrMagnitude > 0.01f)
            {
                _agent.Move(_knockback * dt);
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-8f * dt));
            }

            switch (_state)
            {
                case State.Drive:
                    _agent.isStopped = !hasTarget;
                    _agent.speed = definition.moveSpeed * _self.SpeedMultiplier * GroundZone.MoveMultiplierAt(transform.position);
                    if (!hasTarget)
                        break;
                    Vector3 toTarget = Flat(_target.Position - transform.position);
                    Vector3 velocity = Flat(_agent.velocity);
                    Face(velocity.sqrMagnitude > 0.2f ? velocity : toTarget, dt);
                    if (Time.time >= _nextFire && toTarget.magnitude <= definition.maxFireRange)
                    {
                        Halt();
                        GameEvents.PlaySound(SoundCue.DoorOpen, transform.position);
                        Enter(State.Open);
                    }
                    break;

                case State.Open:
                    if (hasTarget)
                        Face(_target.Position - transform.position, dt);
                    if (_stateTime >= definition.lidOpenTime)
                    {
                        if (hasTarget)
                            Fire();
                        Enter(State.Hold);
                    }
                    break;

                case State.Hold:
                    if (_stateTime >= definition.openAfterVolley)
                        Enter(State.Close);
                    break;

                case State.Close:
                    if (_stateTime >= definition.lidCloseTime)
                    {
                        _nextFire = Time.time + definition.fireInterval;
                        Enter(State.Drive);
                    }
                    break;
            }
        }

        void Reposition()
        {
            if (_target == null || !_target.IsAlive || !GameSession.IsGameplayActive)
                return;
            Vector3 away = Flat(transform.position - _target.Position);
            float distance = away.magnitude;
            away = distance > 0.1f ? away / distance : Vector3.back;
            Vector3 desired = distance < definition.minDistance || distance > definition.preferredDistance + 2f
                ? _target.Position + away * definition.preferredDistance
                : transform.position;
            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        void Fire()
        {
            Vector3 origin = muzzle ? muzzle.position : transform.position + Vector3.up * 1.1f;
            Vector3 aim = _target.AimPoint + Flat(_target.Velocity) * definition.lead;
            Vector3 flat = Flat(aim - origin);
            float distance = Mathf.Max(1f, flat.magnitude);
            float time = distance / definition.ballSpeed;
            float up = (aim.y - origin.y + 0.5f * definition.ballGravity * time * time) / time;
            Vector3 forward = flat / distance;
            GameEvents.PlaySound(SoundCue.ThrowCharged, origin);
            if (ballPrefab == null)
                return;
            for (int i = 0; i < definition.volleyCount; i++)
            {
                float angle = (i - (definition.volleyCount - 1) * 0.5f) * definition.volleySpread;
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                var ball = PoolService.Spawn(ballPrefab, origin, Quaternion.identity);
                ball.Launch(new BallThrow
                {
                    Origin = origin,
                    Direction = direction,
                    Team = Team.Enemy,
                    Thrower = gameObject,
                    Stats = new ThrowStats
                    {
                        Speed = definition.ballSpeed,
                        UpVelocity = up,
                        Gravity = definition.ballGravity,
                        Damage = definition.ballDamage,
                        Knockback = definition.ballKnockback,
                        Flags = HitFlags.Spiky,
                    },
                });
            }
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
            bool open = LidOpen;
            BossArmor.Flash(hitFlash, open);
            GameEvents.PlaySound(open ? SoundCue.EnemyHitStrong : SoundCue.ShieldBlock, hit.Point);
            _jolt = 1f;
            var scaled = hit;
            scaled.Damage = _armor.Take(hit.Damage, open);
            if (scaled.Damage > 0)
                _health.TryDamage(scaled);
            if (!_health.IsDead)
                _knockback += Flat(hit.Direction) * (hit.Force * definition.knockbackScale);
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
            GameEvents.PlaySound(SoundCue.Explosion, transform.position);
            PoolService.Despawn(gameObject);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float targetLid = _state switch
            {
                State.Open => Mathf.Clamp01(_stateTime / Mathf.Max(0.05f, definition.lidOpenTime)),
                State.Hold => 1f,
                State.Close => 1f - Mathf.Clamp01(_stateTime / Mathf.Max(0.05f, definition.lidCloseTime)),
                _ => 0f,
            };
            _lid = Mathf.MoveTowards(_lid, targetLid, dt * 4f);
            if (lid)
                lid.localRotation = _lidRest * Quaternion.Euler(lidOpenAngle * _lid, 0f, 0f);

            Vector3 velocity = NetHooks.IsGuest ? _self.Velocity : _agent.isOnNavMesh ? _agent.velocity : Vector3.zero;
            float speed = Vector3.Dot(velocity, transform.forward);
            _wheelAngle += speed / Mathf.Max(0.05f, wheelRadius) * Mathf.Rad2Deg * dt;
            if (wheels != null)
                foreach (var w in wheels)
                    if (w)
                        w.localRotation = Quaternion.Euler(_wheelAngle, 0f, 0f);

            _jolt = Mathf.MoveTowards(_jolt, 0f, dt * 3f);
            if (body)
            {
                float rock = Mathf.Sin(Time.time * 9f) * 3f * _jolt + Mathf.Sin(Time.time * 4f) * 0.6f * Mathf.Clamp01(Mathf.Abs(speed));
                body.localRotation = _bodyRest * Quaternion.Euler(rock, 0f, 0f);
            }
        }

        public void WriteNet(NetWriter writer)
        {
            writer.Byte((byte)_state);
            writer.Seconds(_stateTime);
        }

        public void ReadNet(NetReader reader, float age)
        {
            _state = (State)reader.Byte();
            _stateTime = reader.Seconds() + age;
        }

        void Enter(State state)
        {
            _state = state;
            _stateTime = 0f;
            if (state == State.Drive && _agent.isOnNavMesh)
                _agent.isStopped = false;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
