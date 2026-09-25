using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Плюшевый мишка — медленный танк. Идёт к игроку по NavMesh и бьёт лапой вблизи (замах виден заранее).
    /// Мяч застревает у него в животе: урон проходит, но мяч не отскакивает, и у игрока становится на мяч меньше.
    /// Попадание другим мячом выбивает застрявший на пол; выбитый мишка роняет всё, что застряло.
    /// Мишка-моряк (элитный) держит два мяча и выплёвывает их обратно в игрока — их можно поймать.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class BearEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        enum State
        {
            Chase,
            Windup,
            Swipe,
            Recover,
            Stagger,
        }

        const float RepathInterval = 0.3f;

        [SerializeField] BearDefinition definition;
        [Tooltip("Точка на животе, где висит застрявший мяч")]
        [SerializeField] Transform ballSocket;
        [Tooltip("Тело: переваливается при ходьбе")]
        [SerializeField] Transform body;
        [Tooltip("Лапа, которой мишка бьёт")]
        [SerializeField] Transform pawStrike;
        [Tooltip("Вторая лапа: просто качается при ходьбе")]
        [SerializeField] Transform pawOther;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        readonly List<Ball> _stuck = new();
        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        Targetable _target;
        State _state;
        float _stateTime;
        float _nextRepath;
        float _nextAttack;
        float _spitAt;
        bool _swiped;
        Vector3 _knockback;
        Vector3 _attackDirection;
        Quaternion _bodyRest;
        Quaternion _pawStrikeRest;
        Quaternion _pawOtherRest;
        float _walkPhase;

        public BearDefinition Definition => definition;
        public int StuckBalls => _stuck.Count;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _agent.updateRotation = false;
            _health.Died += OnDied;
            if (body)
                _bodyRest = body.localRotation;
            if (pawStrike)
                _pawStrikeRest = pawStrike.localRotation;
            if (pawOther)
                _pawOtherRest = pawOther.localRotation;
            ApplyDefinition();
        }

        public void ApplyDefinition()
        {
            _agent.speed = definition.moveSpeed;
            _agent.acceleration = 12f;
            _agent.stoppingDistance = definition.attackRange * 0.8f;
            _health.Configure(definition.hitsToKill, 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _stuck.Clear();
            _knockback = Vector3.zero;
            _nextRepath = 0f;
            _nextAttack = Time.time + 1f;
            _target = null;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            Enter(State.Chase);
        }

        public void OnDespawned() => ReleaseAll(Vector3.back);

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

            _agent.speed = definition.moveSpeed * GumSpot.EnemyMoveMultiplierAt(transform.position)
                                                * GroundZone.MoveMultiplierAt(transform.position);
            if (_knockback.sqrMagnitude > 0.01f)
            {
                _agent.Move(_knockback * dt);
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-8f * dt));
            }

            switch (_state)
            {
                case State.Chase:
                    _agent.isStopped = !hasTarget;
                    if (!hasTarget)
                        break;
                    Vector3 velocity = Flat(_agent.velocity);
                    Face(velocity.sqrMagnitude > 0.2f ? velocity : toTarget, dt);
                    if (distance <= definition.attackRange && Time.time >= _nextAttack)
                    {
                        _attackDirection = toTarget / Mathf.Max(0.01f, distance);
                        Halt();
                        Enter(State.Windup);
                    }
                    break;

                case State.Windup:
                    if (hasTarget)
                    {
                        _attackDirection = toTarget / Mathf.Max(0.01f, distance);
                        Face(_attackDirection, dt);
                    }
                    if (_stateTime >= definition.windupTime)
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

            if (definition.spitDelay > 0f && _stuck.Count > 0 && hasTarget && Time.time >= _spitAt)
                Spit(_target);
        }

        void LateUpdate()
        {
            HoldStuckBalls();
            Animate(Time.deltaTime);
        }

        void Swipe()
        {
            GameEvents.PlaySound(SoundCue.SwingBat, transform.position);
            if (_target == null || !_target.IsAlive)
                return;
            Vector3 toTarget = Flat(_target.Position - transform.position);
            if (toTarget.magnitude > definition.swipeRange || Vector3.Angle(transform.forward, toTarget) > definition.swipeHalfAngle)
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

        // ---------- Застрявшие мячи ----------

        Vector3 SocketPosition(int index)
        {
            Vector3 socket = ballSocket ? ballSocket.position : transform.position + Vector3.up * 0.75f + transform.forward * 0.45f;
            // Второй мяч висит чуть в стороне от первого.
            return socket + transform.right * (index * 0.32f - (_stuck.Count - 1) * 0.16f);
        }

        void HoldStuckBalls()
        {
            for (int i = _stuck.Count - 1; i >= 0; i--)
                if (_stuck[i] == null || !_stuck[i].isActiveAndEnabled || _stuck[i].State != BallState.Stuck)
                    _stuck.RemoveAt(i);
            for (int i = 0; i < _stuck.Count; i++)
                _stuck[i].HoldAt(SocketPosition(i));
        }

        void Stick(Ball ball)
        {
            ball.Stick(SocketPosition(_stuck.Count));
            _stuck.Add(ball);
            if (_stuck.Count == 1)
                _spitAt = Time.time + definition.spitDelay;
            GameEvents.PlaySound(SoundCue.BallStuck, ball.Position);
        }

        /// <summary>Застрявший мяч выбит: отлетает на пол.</summary>
        void KnockOut(Vector3 direction)
        {
            if (_stuck.Count == 0)
                return;
            var ball = _stuck[0];
            _stuck.RemoveAt(0);
            Vector3 side = Flat(direction);
            side = side.sqrMagnitude > 1e-4f ? side.normalized : transform.forward;
            ball.Drop(ball.Position, side * definition.knockOutVelocity.x + Vector3.up * definition.knockOutVelocity.y);
            GameEvents.PlaySound(SoundCue.BallWall, ball.Position);
        }

        void ReleaseAll(Vector3 direction)
        {
            while (_stuck.Count > 0)
            {
                if (_stuck[0] != null && _stuck[0].State == BallState.Stuck)
                    KnockOut(direction + Random.insideUnitSphere * 0.5f);
                else
                    _stuck.RemoveAt(0);
            }
        }

        /// <summary>Мишка-моряк плюётся застрявшим мячом: летит в игрока, его можно поймать.</summary>
        void Spit(Targetable target)
        {
            var ball = _stuck[0];
            _stuck.RemoveAt(0);
            _spitAt = Time.time + definition.spitDelay;
            Vector3 origin = ball.Position;
            Vector3 flat = Flat(target.AimPoint - origin);
            float distance = Mathf.Max(0.5f, flat.magnitude);
            float time = distance / definition.spitSpeed;
            float up = (target.AimPoint.y - origin.y + 0.5f * definition.spitGravity * time * time) / time;
            ball.Launch(new BallThrow
            {
                Origin = origin,
                Direction = flat / distance,
                Team = Team.Enemy,
                Thrower = gameObject,
                Stats = new ThrowStats
                {
                    Speed = definition.spitSpeed,
                    UpVelocity = up,
                    Gravity = definition.spitGravity,
                    Damage = definition.spitDamage,
                    Knockback = 6f,
                },
            });
            GameEvents.PlaySound(SoundCue.SoldierThrow, origin);
        }

        // ---------- Попадания ----------

        public BallContactResult OnBallContact(Ball ball, in RaycastHit hit)
        {
            if (_health.IsDead)
                return BallContactResult.PassThrough;

            Vector3 direction = Flat(ball.Velocity);
            if (direction.sqrMagnitude < 1e-4f)
                direction = -Flat(hit.normal);
            direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : transform.forward;
            ApplyHit(new HitInfo
            {
                Damage = ball.Stats.Damage,
                Point = hit.point,
                Direction = direction,
                Force = ball.Stats.Knockback,
                SourceTeam = ball.Team,
                Source = ball.Thrower,
                Flags = ball.Stats.Flags,
            });
            if (_health.IsDead || ball.IsPhantom)
                return BallContactResult.Hit;
            // Живот полный — новый мяч выбивает застрявший и отскакивает сам. Иначе застревает.
            if (_stuck.Count >= definition.maxStuckBalls)
            {
                KnockOut(-direction);
                return BallContactResult.Hit;
            }
            Stick(ball);
            return BallContactResult.Caught;
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
            if (!_health.IsDead)
            {
                _knockback += Flat(hit.Direction) * (hit.Force * definition.knockbackScale);
                // Сильный удар сбивает замах, обычный мишка терпит.
                if (strong && _state is (State.Windup or State.Chase))
                {
                    Halt();
                    Enter(State.Stagger);
                }
            }
            return true;
        }

        void OnDied(HitInfo hit)
        {
            ReleaseAll(hit.Direction);
            if (debrisPrefab)
            {
                var debris = PoolService.Spawn(debrisPrefab, transform.position, transform.rotation).GetComponent<Debris>();
                if (debris)
                    debris.Burst(hit.Direction, definition.debrisForce + hit.Force * 0.2f, Vector3.zero);
            }
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.SoldierPop, transform.position);
            GameFeel.Shake(0.35f);
            PoolService.Despawn(gameObject);
        }

        // ---------- Вид ----------

        void Animate(float dt)
        {
            Vector3 velocity = _agent.isOnNavMesh ? Flat(_agent.velocity) : Vector3.zero;
            float speed01 = Mathf.Clamp01(velocity.magnitude / Mathf.Max(0.1f, definition.moveSpeed));
            _walkPhase += dt * 7f * speed01;
            if (body)
            {
                float roll = Mathf.Sin(_walkPhase) * 6f * speed01;
                float lean = _state == State.Windup ? -8f * Mathf.Clamp01(_stateTime / definition.windupTime)
                    : _state == State.Swipe ? 12f
                    : _state == State.Stagger ? -10f
                    : 0f;
                body.localRotation = _bodyRest * Quaternion.Euler(lean, 0f, roll);
            }
            if (pawStrike)
            {
                float angle = _state switch
                {
                    State.Windup => Mathf.Lerp(0f, -130f, Mathf.Clamp01(_stateTime / definition.windupTime)),
                    State.Swipe => Mathf.Lerp(-130f, 50f, Mathf.Clamp01(_stateTime / definition.swipeTime)),
                    State.Recover => Mathf.Lerp(50f, 0f, Mathf.Clamp01(_stateTime / definition.recoverTime)),
                    _ => Mathf.Sin(_walkPhase) * 18f * speed01,
                };
                pawStrike.localRotation = _pawStrikeRest * Quaternion.Euler(angle, 0f, 0f);
            }
            if (pawOther)
                pawOther.localRotation = _pawOtherRest * Quaternion.Euler(-Mathf.Sin(_walkPhase) * 18f * speed01, 0f, 0f);
        }

        // ---------- Служебное ----------

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
