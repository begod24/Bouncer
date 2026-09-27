using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Балерина из музыкальной шкатулки. Кружится на подставке и медленно скользит к игроку: мяч, прилетевший
    /// в пируэт, облетает её и летит обратно в игрока — его можно поймать (это всё ещё мяч игрока). Сильный мяч
    /// пробивает пируэт и сбивает его. После пируэта — реверанс: стоит открытая, тут её и бить. Пачка вблизи
    /// задевает.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class BallerinaEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable, IBallInterceptor
    {
        public enum State
        {
            Spin,
            Rest,
            Stagger,
        }

        const float RepathInterval = 0.4f;
        const float OrbitRadius = 0.7f;

        [SerializeField] BallerinaDefinition definition;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        [Header("Вид")]
        [Tooltip("Балерина на пружинке: крутится вокруг оси Y")]
        [SerializeField] Transform body;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;
        [Tooltip("Руки опущены в реверансе, градусов вокруг Z")]
        [SerializeField] float armsDownAngle = 115f;

        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        Targetable _target;
        State _state;
        float _stateTime;
        float _nextRepath;
        float _nextContact;
        Vector3 _knockback;
        Ball _held;
        float _heldUntil;
        float _spinAngle;
        float _arms;
        Quaternion _bodyRest;
        Vector3 _bodyRestPosition;

        public State CurrentState => _state;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _agent.updateRotation = false;
            _health.Died += OnDied;
            if (body)
            {
                _bodyRest = body.localRotation;
                _bodyRestPosition = body.localPosition;
            }
            ApplyDefinition();
        }

        public void ApplyDefinition()
        {
            _agent.speed = definition.moveSpeed;
            _agent.acceleration = 6f;
            _agent.stoppingDistance = 0.6f;
            _health.Configure(EnemyScaling.Hits(definition.hitsToKill), 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _knockback = Vector3.zero;
            _nextRepath = 0f;
            _nextContact = 0f;
            _held = null;
            _target = null;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            EnterSpin();
        }

        public void OnDespawned() => ReleaseHeld();

        // ---------- Мозги ----------

        void Update()
        {
            float dt = Time.deltaTime;
            if (!_agent.isOnNavMesh)
                return;
            if (_held != null)
                HoldAndReflect();
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
                if (_target != null && _state == State.Spin && GameSession.IsGameplayActive)
                    _agent.SetDestination(_target.Position);
            }
            bool hasTarget = _target != null && _target.IsAlive && GameSession.IsGameplayActive;
            if (_knockback.sqrMagnitude > 0.01f)
            {
                _agent.Move(_knockback * dt);
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-8f * dt));
            }

            switch (_state)
            {
                case State.Spin:
                    _agent.isStopped = !hasTarget;
                    _agent.speed = definition.moveSpeed * _self.SpeedMultiplier * GroundZone.MoveMultiplierAt(transform.position);
                    if (hasTarget)
                    {
                        Face(_target.Position - transform.position, dt);
                        TryContact();
                    }
                    if (_stateTime >= definition.spinTime)
                    {
                        Halt();
                        Enter(State.Rest);
                    }
                    break;

                case State.Rest:
                    if (hasTarget)
                        Face(_target.Position - transform.position, dt);
                    if (_stateTime >= definition.restTime)
                        EnterSpin();
                    break;

                case State.Stagger:
                    if (_stateTime >= definition.staggerTime)
                        Enter(State.Rest);
                    break;
            }
        }

        void EnterSpin()
        {
            Enter(State.Spin);
            if (_agent.isOnNavMesh)
                _agent.isStopped = false;
            GameEvents.PlaySound(SoundCue.RolyPolyChime, transform.position);
        }

        void TryContact()
        {
            if (Time.time < _nextContact)
                return;
            Vector3 delta = Flat(_target.Position - transform.position);
            if (delta.sqrMagnitude > definition.contactRange * definition.contactRange || !_target.TryGetComponent(out IDamageable damageable))
                return;
            _nextContact = Time.time + definition.contactCooldown;
            damageable.ApplyHit(new HitInfo
            {
                Damage = definition.contactDamage,
                Point = _target.AimPoint,
                Direction = delta.sqrMagnitude > 1e-4f ? delta.normalized : transform.forward,
                Force = definition.contactKnockback,
                SourceTeam = Team.Enemy,
                Source = gameObject,
                Flags = HitFlags.Melee,
            });
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

        // ---------- Отражение: мяч облетает её и летит обратно ----------

        public bool TryIntercept(Ball ball)
        {
            if (_state != State.Spin || _held != null || ball == null || _health.IsDead || ball.Stats.Has(HitFlags.Charged))
                return false;
            _held = ball;
            _heldUntil = Time.time + definition.reflectHold;
            ball.Stick(OrbitPoint(0f));
            GameEvents.PlaySound(SoundCue.RolyPolyChime, transform.position);
            return true;
        }

        Vector3 OrbitPoint(float progress)
        {
            float angle = (_spinAngle + progress * 360f) * Mathf.Deg2Rad;
            return transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * OrbitRadius + Vector3.up * 1.1f;
        }

        void HoldAndReflect()
        {
            if (!_held.isActiveAndEnabled || _held.State != BallState.Stuck)
            {
                _held = null;
                return;
            }
            float progress = 1f - Mathf.Clamp01((_heldUntil - Time.time) / Mathf.Max(0.01f, definition.reflectHold));
            _held.HoldAt(OrbitPoint(progress));
            if (Time.time < _heldUntil)
                return;
            var target = Targetable.FindNearest(transform.position, Team.Player);
            if (target == null)
            {
                ReleaseHeld();
                return;
            }
            var ball = _held;
            _held = null;
            Vector3 origin = ball.Position;
            Vector3 flat = Flat(target.AimPoint - origin);
            float distance = Mathf.Max(0.5f, flat.magnitude);
            float time = distance / definition.reflectSpeed;
            float up = (target.AimPoint.y - origin.y + 0.5f * definition.reflectGravity * time * time) / time;
            // Хозяин у броска не задан: мяч игрока остаётся мячом игрока, упав — не исчезнет.
            ball.Launch(new BallThrow
            {
                Origin = origin,
                Direction = flat / distance,
                Team = Team.Enemy,
                Thrower = gameObject,
                Stats = new ThrowStats
                {
                    Speed = definition.reflectSpeed,
                    UpVelocity = up,
                    Gravity = definition.reflectGravity,
                    Damage = 1,
                    Knockback = 5f,
                    Flags = HitFlags.None,
                },
            });
        }

        void ReleaseHeld()
        {
            if (_held != null && _held.isActiveAndEnabled && _held.State == BallState.Stuck)
                _held.Drop(_held.Position, Vector3.up * 2f);
            _held = null;
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
                _knockback += Flat(hit.Direction) * (hit.Force * definition.knockbackScale);
                // Сильный удар сбивает пируэт.
                if (_state == State.Spin && strong)
                {
                    Halt();
                    Enter(State.Stagger);
                }
            }
            return true;
        }

        void OnDied(HitInfo hit)
        {
            ReleaseHeld();
            if (debrisPrefab)
            {
                var debris = PoolService.Spawn(debrisPrefab, transform.position, transform.rotation).GetComponent<Debris>();
                if (debris)
                    debris.Burst(hit.Direction, definition.debrisForce + hit.Force * 0.25f, Vector3.zero);
            }
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.RolyPolyPop, transform.position);
            GameFeel.Shake(0.25f);
            PoolService.Despawn(gameObject);
        }

        // ---------- Вид ----------

        void LateUpdate()
        {
            if (!body)
                return;
            float dt = Time.deltaTime;
            bool spinning = _state == State.Spin && !_self.IsFrozen;
            if (spinning)
                _spinAngle += definition.spinSpeed * dt;
            else
                _spinAngle = Mathf.MoveTowardsAngle(_spinAngle, 0f, 720f * dt);
            float curtsey = _state == State.Rest ? Mathf.Sin(Mathf.Clamp01(_stateTime / 0.5f) * Mathf.PI * 0.5f) * 12f : 0f;
            float wobble = _state == State.Stagger ? Mathf.Sin(_stateTime * 30f) * 14f * (1f - _stateTime / definition.staggerTime) : 0f;
            body.localRotation = _bodyRest * Quaternion.Euler(curtsey, _spinAngle, wobble);
            // На пружинке покачивается.
            float bob = spinning ? Mathf.Sin(Time.time * 7f) * 0.03f : 0f;
            body.localPosition = _bodyRestPosition + Vector3.up * bob;

            _arms = Mathf.MoveTowards(_arms, spinning ? 0f : armsDownAngle, 400f * dt);
            // Левая рука — на -X в Unity: вниз к боку = поворот вокруг Z в плюс.
            if (armL)
                armL.localRotation = Quaternion.Euler(0f, 0f, _arms);
            if (armR)
                armR.localRotation = Quaternion.Euler(0f, 0f, -_arms);
        }

        void Enter(State state)
        {
            _state = state;
            _stateTime = 0f;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
