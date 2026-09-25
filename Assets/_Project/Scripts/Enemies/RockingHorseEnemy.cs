using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Лошадка-качалка. Подкачивается к игроку, потом раскачивается на месте всё сильнее (видно, что сейчас рванёт)
    /// и таранит по прямой. После тарана качается на месте — окно для бросков. Попадание во время раскачки сбивает
    /// разбег. Конь-огонь (элитный) оставляет на таране огненный след.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class RockingHorseEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        enum State
        {
            Approach,
            Windup,
            Charge,
            Recover,
            Stagger,
        }

        const float RepathInterval = 0.3f;

        [SerializeField] RockingHorseDefinition definition;
        [Tooltip("Модель: качается на полозьях вокруг оси X")]
        [SerializeField] Transform rocker;
        [Tooltip("Конь-огонь: огненное пятно на таране. Пусто — без следа")]
        [SerializeField] FireSpot firePrefab;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        Targetable _target;
        State _state;
        float _stateTime;
        float _nextRepath;
        float _nextCharge;
        float _rockPhase;
        float _fireDistance;
        bool _chargeHit;
        Vector3 _chargeDirection;
        Vector3 _knockback;
        Quaternion _rockerRest;

        public RockingHorseDefinition Definition => definition;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _agent.updateRotation = false;
            _health.Died += OnDied;
            if (rocker)
                _rockerRest = rocker.localRotation;
            ApplyDefinition();
        }

        public void ApplyDefinition()
        {
            _agent.speed = definition.moveSpeed;
            _agent.acceleration = 14f;
            _agent.stoppingDistance = 0.5f;
            _health.Configure(definition.hitsToKill, 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _knockback = Vector3.zero;
            _nextRepath = 0f;
            _nextCharge = Time.time + 1.5f;
            _target = null;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            Enter(State.Approach);
        }

        public void OnDespawned() { }

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
                if (_target != null && _state == State.Approach)
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
                case State.Approach:
                    _agent.isStopped = !hasTarget;
                    _agent.speed = definition.moveSpeed * GumSpot.EnemyMoveMultiplierAt(transform.position)
                                                        * GroundZone.MoveMultiplierAt(transform.position);
                    if (!hasTarget)
                        break;
                    Vector3 velocity = Flat(_agent.velocity);
                    Face(velocity.sqrMagnitude > 0.2f ? velocity : toTarget, dt);
                    if (distance <= definition.chargeRange && Time.time >= _nextCharge && CanSee(_target))
                    {
                        Halt();
                        _chargeDirection = toTarget / Mathf.Max(0.01f, distance);
                        Enter(State.Windup);
                    }
                    break;

                case State.Windup:
                    // Доворачивает на игрока почти до самого рывка, потом направление зафиксировано.
                    if (hasTarget && _stateTime < definition.windupTime - definition.aimLockTime)
                        _chargeDirection = toTarget / Mathf.Max(0.01f, distance);
                    Face(_chargeDirection, dt * 2f);
                    if (_stateTime >= definition.windupTime)
                    {
                        _chargeHit = false;
                        _fireDistance = 0f;
                        transform.rotation = Quaternion.LookRotation(_chargeDirection);
                        GameEvents.PlaySound(SoundCue.ThrowCharged, transform.position);
                        Enter(State.Charge);
                    }
                    break;

                case State.Charge:
                    Charge(dt);
                    break;

                case State.Recover:
                    if (_stateTime >= definition.recoverTime)
                    {
                        _nextCharge = Time.time + definition.chargeCooldown;
                        Enter(State.Approach);
                    }
                    break;

                case State.Stagger:
                    if (_stateTime >= definition.staggerTime)
                        Enter(State.Approach);
                    break;
            }
        }

        /// <summary>Таран по прямой: по NavMesh, поэтому стена его останавливает.</summary>
        void Charge(float dt)
        {
            Vector3 before = transform.position;
            float step = definition.chargeSpeed * dt;
            _agent.Move(_chargeDirection * step);
            float moved = Flat(transform.position - before).magnitude;
            LeaveFire(moved);

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
            // Упёрлась в стену или время вышло — качается на месте.
            bool blocked = _stateTime > 0.1f && moved < step * 0.3f;
            if (blocked || _stateTime >= definition.chargeTime)
            {
                if (blocked)
                {
                    GameEvents.PlaySound(SoundCue.AreaThud, transform.position);
                    GameFeel.Shake(0.2f);
                }
                Enter(State.Recover);
            }
        }

        void LeaveFire(float moved)
        {
            if (firePrefab == null)
                return;
            _fireDistance += moved;
            while (_fireDistance >= definition.fireSpacing)
            {
                _fireDistance -= definition.fireSpacing;
                PoolService.Spawn(firePrefab, transform.position, Quaternion.identity);
            }
        }

        bool CanSee(Targetable target) =>
            !Physics.Linecast(transform.position + Vector3.up * 0.6f, target.Position + Vector3.up * 0.6f,
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
            if (!_health.IsDead)
            {
                if (_state != State.Charge)
                    _knockback += Flat(hit.Direction) * (hit.Force * definition.knockbackScale);
                // Попадание во время раскачки сбивает разбег.
                if (_state == State.Windup)
                {
                    _nextCharge = Time.time + definition.chargeCooldown * 0.5f;
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
            if (!rocker || _self.IsFrozen)
                return;
            float dt = Time.deltaTime;
            float angle;
            switch (_state)
            {
                case State.Windup:
                    float k = Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, definition.windupTime));
                    _rockPhase += dt * Mathf.Lerp(definition.rockFrequency, definition.windupRockFrequency, k) * Mathf.PI * 2f;
                    angle = Mathf.Sin(_rockPhase) * Mathf.Lerp(definition.rockAngle, definition.windupRockAngle, k);
                    break;
                case State.Charge:
                    // Скачет вперёд, наклонившись.
                    _rockPhase += dt * definition.windupRockFrequency * Mathf.PI * 2f;
                    angle = 12f + Mathf.Sin(_rockPhase) * 8f;
                    break;
                case State.Recover:
                    _rockPhase += dt * definition.rockFrequency * Mathf.PI * 2f;
                    angle = Mathf.Sin(_rockPhase) * definition.rockAngle * (1f - Mathf.Clamp01(_stateTime / definition.recoverTime)) * 2f;
                    break;
                default:
                    float speed01 = _agent.isOnNavMesh ? Mathf.Clamp01(_agent.velocity.magnitude / Mathf.Max(0.1f, definition.moveSpeed)) : 0f;
                    _rockPhase += dt * definition.rockFrequency * Mathf.PI * 2f * Mathf.Max(0.3f, speed01);
                    angle = Mathf.Sin(_rockPhase) * definition.rockAngle * Mathf.Max(0.3f, speed01);
                    break;
            }
            rocker.localRotation = _rockerRest * Quaternion.Euler(angle, 0f, 0f);
        }

        void Enter(State state)
        {
            _state = state;
            _stateTime = 0f;
            if (state == State.Approach && _agent.isOnNavMesh)
                _agent.isStopped = false;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
