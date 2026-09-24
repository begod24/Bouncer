using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Неваляшка. Настоящее физическое тело с центром масс ниже центра круглого дна:
    /// от попадания заваливается, качается и сама встаёт. Пока не оглушена — держит
    /// равновесие, идёт к игроку по NavMesh (агент только считает путь) и бьёт телом.
    /// Умирает от серии попаданий: Health сбрасывается, если долго не попадать.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(NavMeshAgent), typeof(Health))]
    [RequireComponent(typeof(Targetable))]
    public sealed class RolyPolyEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        enum State
        {
            Chase,
            Windup,
            Lunge,
            Recover,
            Stunned,
        }

        [SerializeField] EnemyDefinition definition;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;
        [Tooltip("Цвет, в который неваляшка краснеет от серии попаданий")]
        [SerializeField] Color comboTint = new(1f, 0.1f, 0.1f);

        Rigidbody _rb;
        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        State _state;
        float _stateTime;
        float _stunDuration;
        float _nextRepath;
        float _nextAttack;
        bool _lungeHit;
        Targetable _target;
        Vector3 _attackDirection;
        float _waddlePhase;

        public EnemyDefinition Definition => definition;
        public bool IsStunned => _state == State.Stunned;
        float Tilt => Vector3.Angle(_rb.rotation * Vector3.up, Vector3.up);

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();

            _agent.updatePosition = false;
            _agent.updateRotation = false;
            _self.Team = Team.Enemy;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            _health.Damaged += OnDamaged;
            _health.Restored += OnRestored;
            _health.Died += OnDied;
            ApplyDefinition();
        }

        public void ApplyDefinition()
        {
            _rb.mass = definition.mass;
            _rb.centerOfMass = new Vector3(0f, definition.centerOfMassHeight, 0f);
            _agent.speed = definition.moveSpeed;
            _agent.acceleration = 40f;
            _agent.stoppingDistance = definition.stopDistance;
            _health.Configure(definition.hitsToKill, definition.comboResetTime);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _target = null;
            _nextRepath = 0f;
            _nextAttack = Time.time + 0.6f;
            _waddlePhase = Random.value * 10f;
            Enter(State.Chase);
            SyncAgent(force: true);
        }

        public void OnDespawned() { }

        // ---------- Мозги ----------

        void Update()
        {
            if (Time.time >= _nextRepath)
            {
                _nextRepath = Time.time + definition.repathInterval;
                _target = Targetable.FindNearest(_rb.position, Team.Player);
                if (_target != null && _agent.isOnNavMesh)
                    _agent.SetDestination(_target.Position);
            }
            if (_agent.isOnNavMesh)
                _agent.nextPosition = _rb.position;
        }

        void FixedUpdate()
        {
            _stateTime += Time.fixedDeltaTime;
            if (_rb.position.y < -5f)
            {
                PoolService.Despawn(gameObject);
                return;
            }

            bool hasTarget = _target != null && _target.IsAlive && GameSession.IsGameplayActive;
            Vector3 toTarget = hasTarget ? Flat(_target.Position - _rb.position) : Vector3.zero;
            float distance = toTarget.magnitude;
            Vector3 targetDirection = distance > 0.01f ? toTarget / distance : Forward;

            switch (_state)
            {
                case State.Stunned:
                    if (_stateTime > _stunDuration)
                    {
                        // Мягкая помощь, если застряла лёжа (например, у стены).
                        Balance(0.35f);
                        if (Tilt < definition.recoverTilt && _rb.angularVelocity.magnitude < definition.recoverAngularSpeed)
                        {
                            SyncAgent(force: false);
                            Enter(State.Chase);
                        }
                    }
                    break;

                case State.Chase:
                    Balance(1f);
                    if (!hasTarget)
                    {
                        Drive(Vector3.zero);
                        break;
                    }
                    Turn(targetDirection);
                    if (distance <= definition.attackRange && Time.time >= _nextAttack)
                    {
                        _attackDirection = targetDirection;
                        Enter(State.Windup);
                        break;
                    }
                    bool usePath = _agent.isOnNavMesh && !_agent.pathPending && _agent.hasPath;
                    Vector3 desired = usePath ? _agent.desiredVelocity : targetDirection * definition.moveSpeed;
                    Drive(distance < definition.stopDistance ? Vector3.zero : desired);
                    break;

                case State.Windup:
                    Balance(0.6f);
                    Drive(Vector3.zero);
                    if (hasTarget)
                    {
                        _attackDirection = targetDirection;
                        Turn(targetDirection);
                    }
                    // Откидывается назад — видно, что сейчас ударит.
                    _rb.AddTorque(-Vector3.Cross(Vector3.up, _attackDirection) * definition.windupLean, ForceMode.Acceleration);
                    if (_stateTime >= definition.windupTime)
                        Lunge();
                    break;

                case State.Lunge:
                    Balance(0.5f);
                    if (!_lungeHit && hasTarget && distance <= definition.lungeHitRange)
                        HitTarget();
                    if (_stateTime >= definition.lungeTime)
                        Enter(State.Recover);
                    break;

                case State.Recover:
                    Balance(1f);
                    Drive(Vector3.zero);
                    if (_stateTime >= definition.recoverTime)
                        Enter(State.Chase);
                    break;
            }
        }

        void Lunge()
        {
            _lungeHit = false;
            _nextAttack = Time.time + definition.attackCooldown;
            _rb.AddForce(_attackDirection * definition.lungeSpeed + Vector3.up * (definition.lungeSpeed * 0.2f), ForceMode.VelocityChange);
            _rb.AddTorque(Vector3.Cross(Vector3.up, _attackDirection) * definition.lungeTip, ForceMode.VelocityChange);
            Enter(State.Lunge);
        }

        void HitTarget()
        {
            _lungeHit = true;
            if (!_target.TryGetComponent(out IDamageable damageable))
                return;
            damageable.ApplyHit(new HitInfo
            {
                Damage = definition.contactDamage,
                Point = _target.AimPoint,
                Direction = _attackDirection,
                Force = definition.attackKnockback,
                SourceTeam = Team.Enemy,
                Source = gameObject,
                Flags = HitFlags.Melee,
            });
        }

        // ---------- Тело ----------

        /// <summary>Удержание вертикали: момент к «вверх» + гашение раскачки.</summary>
        void Balance(float strength)
        {
            Vector3 axis = Vector3.Cross(_rb.rotation * Vector3.up, Vector3.up);
            Vector3 angular = _rb.angularVelocity;
            Vector3 tiltRate = new(angular.x, 0f, angular.z);
            _rb.AddTorque((axis * definition.uprightStrength - tiltRate * definition.uprightDamping) * strength, ForceMode.Acceleration);
        }

        void Turn(Vector3 direction)
        {
            direction.y = 0f;
            Vector3 forward = Forward;
            if (direction.sqrMagnitude < 1e-4f || forward.sqrMagnitude < 1e-4f)
                return;
            float error = Vector3.SignedAngle(forward, direction, Vector3.up) * Mathf.Deg2Rad;
            _rb.AddTorque(Vector3.up * (error * definition.yawStrength - _rb.angularVelocity.y * definition.yawDamping), ForceMode.Acceleration);
        }

        void Drive(Vector3 desired)
        {
            desired.y = 0f;
            desired = Vector3.ClampMagnitude(desired, definition.moveSpeed * GroundZone.MoveMultiplierAt(_rb.position)
                                                      * GumSpot.EnemyMoveMultiplierAt(_rb.position));
            Vector3 velocity = _rb.linearVelocity;
            Vector3 horizontal = new(velocity.x, 0f, velocity.z);
            Vector3 acceleration = Vector3.ClampMagnitude((desired - horizontal) * 10f, definition.acceleration);
            _rb.AddForce(acceleration, ForceMode.Acceleration);

            // Переваливается с боку на бок, пока идёт.
            float speed01 = Mathf.Clamp01(horizontal.magnitude / Mathf.Max(0.1f, definition.moveSpeed));
            if (speed01 > 0.05f)
            {
                _waddlePhase += Time.fixedDeltaTime * definition.waddleFrequency;
                _rb.AddTorque(horizontal.normalized * (Mathf.Sin(_waddlePhase) * definition.waddleStrength * speed01), ForceMode.Acceleration);
            }
        }

        Vector3 Forward
        {
            get
            {
                Vector3 forward = Flat(_rb.rotation * Vector3.forward);
                return forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            }
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
                Direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Forward,
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

            // Без стоп-кадра: вздрагивает сама неваляшка (HitPunch), камеру трясёт слегка.
            bool strong = hit.Has(HitFlags.Charged);
            GameFeel.Shake(strong ? 0.25f : 0.1f);
            if (hitFlash)
                hitFlash.Flash(Color.white, 0.12f);
            GameEvents.PlaySound(strong ? SoundCue.EnemyHitStrong : SoundCue.EnemyHit, hit.Point);

            _health.TryDamage(hit);
            if (!_health.IsDead)
            {
                Knock(hit.Direction, hit.Force);
                // Неваляшка качается — и звенит, как настоящая игрушка.
                GameEvents.PlaySound(SoundCue.RolyPolyChime, _rb.position);
            }
            return true;
        }

        public void Stun(float duration)
        {
            Enter(State.Stunned);
            _stunDuration = duration;
        }

        void Knock(Vector3 direction, float impulse)
        {
            _rb.AddForce(direction * impulse + Vector3.up * (impulse * definition.hitLift), ForceMode.Impulse);
            Vector3 tipAxis = Vector3.Cross(Vector3.up, direction);
            _rb.AddTorque(tipAxis * (impulse * definition.tipPerImpulse) + Vector3.up * Random.Range(-2f, 2f), ForceMode.VelocityChange);
            Stun(definition.stunTime);
        }

        void OnCollisionEnter(Collision collision)
        {
            // Оглушённая неваляшка, влетев в соседку, сбивает и её.
            if (_state != State.Stunned)
                return;
            if (collision.relativeVelocity.sqrMagnitude < definition.chainStunSpeed * definition.chainStunSpeed)
                return;
            var body = collision.rigidbody;
            if (body != null && body.TryGetComponent(out RolyPolyEnemy other) && other != this && !other.IsStunned)
                other.Stun(definition.chainStunTime);
        }

        void OnDamaged(HitInfo hit)
        {
            if (hitFlash)
                hitFlash.SetTint(comboTint, 1f - (float)_health.Current / _health.Max);
        }

        void OnRestored()
        {
            if (hitFlash)
                hitFlash.SetTint(comboTint, 0f);
        }

        void OnDied(HitInfo hit)
        {
            if (debrisPrefab)
            {
                var debris = PoolService.Spawn(debrisPrefab, transform.position, transform.rotation).GetComponent<Debris>();
                if (debris)
                    debris.Burst(hit.Direction, definition.debrisForce + hit.Force * 0.25f, _rb.linearVelocity);
            }
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.RolyPolyPop, transform.position);
            GameFeel.Shake(0.3f);
            PoolService.Despawn(gameObject);
        }

        // ---------- Служебное ----------

        void Enter(State state)
        {
            _state = state;
            _stateTime = 0f;
            if (state == State.Stunned)
                _stunDuration = definition.stunTime;
            if (_agent.isOnNavMesh)
                _agent.isStopped = state == State.Stunned;
        }

        /// <summary>Вернуть агента к телу, если тело отбросило далеко от его позиции на NavMesh.</summary>
        void SyncAgent(bool force)
        {
            if (!_agent.enabled)
                return;
            Vector3 position = _rb.position;
            if (!force && _agent.isOnNavMesh && Flat(_agent.nextPosition - position).sqrMagnitude < 1f)
                return;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
