using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Пупс — быстрый рой. Бежит к игроку по общему полю направлений (<see cref="EnemyFlowField"/>),
    /// расталкивает соседей, вблизи приседает и прыгает, кусая в прыжке. Игрока телом не толкает,
    /// чтобы рой не зажимал его в угол. Выбивается с одного попадания, мяч пробивает его насквозь.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Health), typeof(Targetable))]
    public sealed class PupsikEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        public enum State
        {
            Chase,
            Windup,
            Hop,
            Recover,
        }

        const float RetargetInterval = 0.3f;

        static readonly Collider[] s_neighbours = new Collider[16];

        [SerializeField] PupsikDefinition definition;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        Rigidbody _rb;
        Collider _collider;
        Health _health;
        Targetable _self;
        State _state;
        float _stateTime;
        float _nextAttack;
        float _nextRetarget;
        bool _bitten;
        Targetable _target;
        Vector3 _hopDirection;

        public PupsikDefinition Definition => definition;
        public State CurrentState => _state;
        public float StateTime => _stateTime;
        public Vector3 PlanarVelocity => Flat(_rb.linearVelocity);

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();

            _self.Team = Team.Enemy;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _health.Died += OnDied;
            ApplyDefinition();
        }

        public void ApplyDefinition()
        {
            _rb.mass = definition.mass;
            _health.Configure(definition.hitsToKill, 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _target = null;
            _nextRetarget = 0f;
            _nextAttack = Time.time + Random.Range(0.4f, 0.9f);
            Enter(State.Chase);

            // Рой не толкает игрока телом — только кусает в прыжке.
            foreach (var target in Targetable.All)
                if (target.Team == Team.Player && target.TryGetComponent(out CharacterController controller))
                    Physics.IgnoreCollision(_collider, controller, true);
        }

        public void OnDespawned() { }

        // ---------- Мозги ----------

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (_rb.position.y < -5f)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            if (_self.IsFrozen)
            {
                Drive(Vector3.zero);
                return;
            }
            _stateTime += dt;

            if (Time.time >= _nextRetarget)
            {
                _nextRetarget = Time.time + RetargetInterval;
                _target = Targetable.FindNearest(_rb.position, Team.Player);
            }
            bool hasTarget = _target != null && _target.IsAlive && GameSession.IsGameplayActive;
            Vector3 toTarget = hasTarget ? Flat(_target.Position - _rb.position) : Vector3.zero;
            float distance = toTarget.magnitude;
            Vector3 targetDirection = distance > 0.01f ? toTarget / distance : Forward;

            switch (_state)
            {
                case State.Chase:
                    if (!hasTarget)
                    {
                        Drive(Separation() * definition.separationStrength);
                        break;
                    }
                    if (distance <= definition.attackRange && Time.time >= _nextAttack && IsGrounded())
                    {
                        _hopDirection = targetDirection;
                        Enter(State.Windup);
                        break;
                    }
                    // Рядом с вожаком (пупс в чепчике) рой бегает быстрее.
                    float speed = definition.moveSpeed * SwarmLeader.SpeedMultiplierAt(_rb.position);
                    Vector3 desired = ChaseDirection(targetDirection, distance)
                                      * (speed * GroundZone.MoveMultiplierAt(_rb.position) * GumSpot.EnemyMoveMultiplierAt(_rb.position))
                                      + Separation() * definition.separationStrength;
                    Drive(desired, speed);
                    Face(desired.sqrMagnitude > 0.04f ? desired : targetDirection, dt);
                    break;

                case State.Windup:
                    Drive(Vector3.zero);
                    if (hasTarget)
                        _hopDirection = targetDirection;
                    Face(_hopDirection, dt);
                    if (_stateTime >= definition.windupTime)
                        Hop();
                    break;

                case State.Hop:
                    if (!_bitten && hasTarget)
                        TryBite();
                    if ((_stateTime > 0.15f && _rb.linearVelocity.y <= 0.05f && IsGrounded()) || _stateTime > 1.5f)
                        Enter(State.Recover);
                    break;

                case State.Recover:
                    Drive(Vector3.zero);
                    if (_stateTime >= definition.recoverTime)
                        Enter(State.Chase);
                    break;
            }
        }

        /// <summary>Вблизи и на виду — прямо на игрока, иначе по полю направлений в обход препятствий.</summary>
        Vector3 ChaseDirection(Vector3 targetDirection, float distance)
        {
            Vector3 eye = _rb.position + Vector3.up * 0.4f;
            if (distance <= definition.directChaseDistance
                && !Physics.Linecast(eye, _target.Position + Vector3.up * 0.4f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                return targetDirection;
            return EnemyFlowField.Instance.TryGetDirection(_rb.position, out Vector3 flow) ? flow : targetDirection;
        }

        Vector3 Separation()
        {
            float radius = definition.separationRadius;
            int count = Physics.OverlapSphereNonAlloc(_rb.position + Vector3.up * 0.4f, radius, s_neighbours, Layers.EnemyMask,
                QueryTriggerInteraction.Ignore);
            Vector3 push = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                var other = s_neighbours[i];
                if (other == _collider)
                    continue;
                Vector3 away = Flat(_rb.position - other.transform.position);
                float d = away.magnitude;
                if (d < 1e-3f)
                    continue;
                push += away / d * Mathf.Clamp01(1f - d / radius);
            }
            return push;
        }

        void Hop()
        {
            _bitten = false;
            _nextAttack = Time.time + definition.attackCooldown;
            _rb.linearVelocity = _hopDirection * definition.hopSpeed + Vector3.up * definition.hopUpSpeed;
            GameEvents.PlaySound(SoundCue.PupsikSqueak, _rb.position);
            Enter(State.Hop);
        }

        void TryBite()
        {
            Vector3 delta = _target.Position - _rb.position;
            if (Flat(delta).sqrMagnitude > definition.biteRadius * definition.biteRadius || Mathf.Abs(delta.y) > 1.2f)
                return;
            _bitten = true;
            if (_target.TryGetComponent(out IDamageable damageable))
            {
                damageable.ApplyHit(new HitInfo
                {
                    Damage = definition.contactDamage,
                    Point = _target.AimPoint,
                    Direction = _hopDirection,
                    Force = definition.attackKnockback,
                    SourceTeam = Team.Enemy,
                    Source = gameObject,
                    Flags = HitFlags.Melee,
                });
            }
            // Укусил — отскакивает назад, а не повисает на игроке.
            Vector3 velocity = _rb.linearVelocity;
            _rb.linearVelocity = new Vector3(-_hopDirection.x * 2f, velocity.y, -_hopDirection.z * 2f);
        }

        // ---------- Тело ----------

        void Drive(Vector3 desired) => Drive(desired, definition.moveSpeed);

        void Drive(Vector3 desired, float maxSpeed)
        {
            desired.y = 0f;
            desired = Vector3.ClampMagnitude(desired, maxSpeed);
            Vector3 horizontal = Flat(_rb.linearVelocity);
            _rb.AddForce(Vector3.ClampMagnitude((desired - horizontal) * 12f, definition.acceleration), ForceMode.Acceleration);
        }

        void Face(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f)
                return;
            _rb.angularVelocity = Vector3.zero;
            _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, Quaternion.LookRotation(direction), definition.turnSpeed * dt));
        }

        bool IsGrounded() =>
            Physics.Raycast(_rb.position + Vector3.up * 0.1f, Vector3.down, 0.2f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);

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
            // Лёгкий пупс мяч не останавливает: выбитого пробивает насквозь.
            return _health.IsDead ? BallContactResult.Pierce : BallContactResult.Hit;
        }

        public bool ApplyHit(in HitInfo hit)
        {
            if (_health.IsDead)
                return false;

            // Без стоп-кадра: пупсов выбивают пачками, стоп-кадры подряд выглядели бы как подвисания.
            if (hitFlash)
                hitFlash.Flash(Color.white, 0.1f);
            GameEvents.PlaySound(SoundCue.EnemyHit, hit.Point);
            _health.TryDamage(hit);
            if (!_health.IsDead)
            {
                float impulse = hit.Force * definition.knockbackScale;
                _rb.AddForce(hit.Direction * impulse + Vector3.up * (impulse * 0.3f), ForceMode.VelocityChange);
                Enter(State.Recover);
            }
            return true;
        }

        void OnDied(HitInfo hit)
        {
            if (debrisPrefab)
            {
                var debris = PoolService.Spawn(debrisPrefab, transform.position, transform.rotation).GetComponent<Debris>();
                if (debris)
                    debris.Burst(hit.Direction, definition.debrisForce + hit.Force * 0.2f, _rb.linearVelocity);
            }
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.PupsikPop, transform.position);
            GameFeel.Shake(0.1f);
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
