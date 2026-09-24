using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Оловянный солдатик. Ходит строем (<see cref="SoldierSquad"/>): шеренга встаёт на дистанции от игрока,
    /// разом замахивается и бросает по очереди. Его мячи можно ловить — это главный запас мячей и жизней игрока.
    /// Негнущийся: от попадания качается как игрушка и пропускает залп. Выбитый роняет мяч из руки.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class TinSoldierEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable, IGroupMember
    {
        public enum State
        {
            March,
            Aim,
            Throw,
            Stagger,
        }

        const float RepathInterval = 0.4f;
        const float ThrowPoseTime = 0.35f;

        [SerializeField] TinSoldierDefinition definition;
        [SerializeField] Ball ballPrefab;
        [Tooltip("Мяч в руке: из этой точки вылетает брошенный мяч")]
        [SerializeField] Transform hand;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        SoldierSquad _squad;
        State _state;
        float _stateTime;
        float _throwAt;
        int _lastVolley;
        float _ballBackAt;
        float _nextRepath;
        Vector3 _knockback;

        public TinSoldierDefinition Definition => definition;
        public State CurrentState => _state;
        public float StateTime => _stateTime;
        public bool HasBall { get; private set; }
        /// <summary>0..1 — насколько поднята рука для броска.</summary>
        public float AimProgress => _state == State.Aim ? Mathf.Clamp01(_stateTime / Mathf.Max(0.05f, definition.aimTime)) : 0f;
        public Vector3 PlanarVelocity => _agent.isOnNavMesh ? Flat(_agent.velocity) : Vector3.zero;
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
            _agent.acceleration = 20f;
            _agent.stoppingDistance = 0.15f;
            _agent.autoBraking = true;
            _health.Configure(definition.hitsToKill, definition.comboResetTime);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            HasBall = true;
            _knockback = Vector3.zero;
            _nextRepath = 0f;
            _lastVolley = -1;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            JoinSquad(new SoldierSquad());
            Enter(State.March);
        }

        public void OnDespawned()
        {
            _squad?.Remove(this);
            _squad = null;
        }

        public void OnGroupSpawned(IReadOnlyList<GameObject> group, int index)
        {
            if (index != 0)
                return;
            var squad = new SoldierSquad();
            foreach (var member in group)
                if (member && member.TryGetComponent(out TinSoldierEnemy soldier))
                    soldier.JoinSquad(squad);
        }

        void JoinSquad(SoldierSquad squad)
        {
            _squad?.Remove(this);
            _squad = squad;
            squad.Add(this, definition);
        }

        // ---------- Мозги ----------

        void Update()
        {
            float dt = Time.deltaTime;
            _stateTime += dt;
            if (!_agent.isOnNavMesh)
                return;

            if (!HasBall && Time.time >= _ballBackAt)
                HasBall = true;
            // В жвачке солдатик вязнет и марширует медленнее.
            _agent.speed = definition.moveSpeed * GumSpot.EnemyMoveMultiplierAt(transform.position);
            if (_knockback.sqrMagnitude > 0.01f)
            {
                _agent.Move(_knockback * dt);
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-8f * dt));
            }

            _squad.Tick(definition);
            var target = GameSession.IsGameplayActive ? _squad.Target : null;
            if (target != null && !target.IsAlive)
                target = null;

            switch (_state)
            {
                case State.March:
                    if (target == null)
                    {
                        Halt();
                        break;
                    }
                    if (HasBall && _squad.VolleyId != _lastVolley && _squad.IsVolleyActive(definition))
                    {
                        _lastVolley = _squad.VolleyId;
                        _throwAt = _squad.ThrowTimeFor(this, definition);
                        Halt();
                        Enter(State.Aim);
                        break;
                    }
                    MoveToSlot();
                    Vector3 velocity = PlanarVelocity;
                    Face(velocity.sqrMagnitude > 0.3f && _agent.remainingDistance > 1f ? velocity : target.Position - transform.position, dt);
                    break;

                case State.Aim:
                    if (target == null)
                    {
                        Enter(State.March);
                        break;
                    }
                    Face(target.Position - transform.position, dt);
                    if (Time.time >= _throwAt)
                    {
                        Throw(target);
                        Enter(State.Throw);
                    }
                    break;

                case State.Throw:
                    if (_stateTime >= ThrowPoseTime)
                        Enter(State.March);
                    break;

                case State.Stagger:
                    if (_stateTime >= definition.staggerTime)
                        Enter(State.March);
                    break;
            }
        }

        void MoveToSlot()
        {
            _agent.isStopped = false;
            if (Time.time < _nextRepath)
                return;
            _nextRepath = Time.time + RepathInterval;
            _agent.SetDestination(_squad.SlotFor(this, definition));
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

        void Throw(Targetable target)
        {
            HasBall = false;
            _ballBackAt = Time.time + definition.reloadTime * 0.8f;

            Vector3 origin = hand ? hand.position : transform.position + Vector3.up * 1.3f;
            Vector3 aim = target.AimPoint;
            Vector3 flat = Flat(aim - origin);
            aim += Flat(target.Velocity) * (flat.magnitude / definition.ballSpeed * definition.lead);
            flat = Flat(aim - origin);
            float distance = flat.magnitude;
            if (distance < 0.5f)
                return;

            // Вертикальная скорость — чтобы мяч прилетел на уровень груди.
            float time = distance / definition.ballSpeed;
            float upVelocity = (aim.y - origin.y + 0.5f * definition.ballGravity * time * time) / time;
            GameEvents.PlaySound(SoundCue.SoldierThrow, origin);
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
                    UpVelocity = upVelocity,
                    Gravity = definition.ballGravity,
                    Damage = definition.damage,
                    Knockback = definition.knockback,
                },
            });
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

            // Без стоп-кадра: вздрагивает сам солдатик (HitPunch), камеру трясёт слегка.
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
                    debris.Burst(hit.Direction, definition.debrisForce + hit.Force * 0.25f, PlanarVelocity);
            }
            if (HasBall && definition.dropBallOnDeath && ballPrefab)
            {
                Vector3 position = hand ? hand.position : transform.position + Vector3.up;
                var ball = PoolService.Spawn(ballPrefab, position, Quaternion.identity);
                ball.Drop(position, Flat(hit.Direction) * 2f + Vector3.up * 2f);
            }
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.SoldierPop, transform.position);
            GameFeel.Shake(0.25f);
            PoolService.Despawn(gameObject);
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
