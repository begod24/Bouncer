using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Ворона из стаи «Того, кто в сумерках». Кружит высоко над игроком — мячом не достать, автоприцел её не видит,
    /// а где она, выдаёт тень на асфальте. Потом пикирует по прямой через то место, где стоит игрок: полоса
    /// на асфальте мигает заранее. Низко над землёй её сбивает любой мяч. Иногда вместо пике хватает мяч
    /// с земли и несёт боссу в мешок; сбитая — роняет его. Через несколько заходов улетает.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(Targetable))]
    public sealed class CrowEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        enum State
        {
            Arrive,
            Circle,
            Mark,
            Dive,
            Climb,
            Grab,
            Carry,
            Leave,
        }

        const float LeaveTime = 2.5f;

        [SerializeField] CrowDefinition definition;

        [Header("Вид")]
        [SerializeField] Transform visual;
        [SerializeField] Transform wingL;
        [SerializeField] Transform wingR;
        [Tooltip("Тень на асфальте под вороной")]
        [SerializeField] Transform shadowBlob;
        [Tooltip("Где висит украденный мяч")]
        [SerializeField] Transform feet;
        [SerializeField] GroundMarker diveMarkerPrefab;
        [SerializeField] ParticleBurst feathersPrefab;
        [SerializeField] HitFlash hitFlash;

        Health _health;
        Targetable _self;
        DuskBoss _boss;
        Targetable _target;
        State _state;
        float _stateTime;
        float _stateLength;
        Vector3 _velocity;
        float _orbitAngle;
        float _orbitSide = 1f;
        int _divesLeft;
        float _leaveAt;
        Vector3 _diveFrom;
        Vector3 _diveTo;
        float _diveStartHeight;
        bool _hitThisDive;
        GroundMarker _marker;
        Ball _carried;
        Ball _wanted;
        Quaternion _wingLRest, _wingRRest;
        float _flap;

        public CrowDefinition Definition => definition;
        bool Low => transform.position.y < definition.hittableHeight;
        Vector3 FeetPosition => feet ? feet.position : transform.position - Vector3.up * 0.3f;

        void Awake()
        {
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _health.Died += OnDied;
            if (wingL)
                _wingLRest = wingL.localRotation;
            if (wingR)
                _wingRRest = wingR.localRotation;
        }

        public void OnSpawned()
        {
            _health.Configure(definition.hitsToKill, 0f);
            _boss = null;
            _carried = null;
            _wanted = null;
            _velocity = Vector3.zero;
            _divesLeft = definition.dives;
            _leaveAt = Time.time + definition.lifetime;
            _orbitAngle = Random.value * 360f;
            _orbitSide = Random.value < 0.5f ? -1f : 1f;
            _flap = Random.value * 10f;
            Enter(State.Arrive);
        }

        public void OnDespawned()
        {
            DropCarried();
            ClearMarker();
            if (_boss)
                _boss.OnCrowGone(this);
            _boss = null;
        }

        /// <summary>Вылетела из стаи босса (с его плеч, из-за края двора или из разбитого чучела).</summary>
        public void Launch(DuskBoss boss)
        {
            _boss = boss;
            GameEvents.PlaySound(SoundCue.CrowCaw, transform.position);
        }

        /// <summary>Улететь со двора: стая разогнана или время вышло.</summary>
        public void FlyAway()
        {
            DropCarried();
            ClearMarker();
            if (_state != State.Leave)
                Enter(State.Leave, LeaveTime);
        }

        // ---------- Мозги ----------

        void Update()
        {
            if (!GameSession.IsGameplayActive)
                return;
            float dt = Time.deltaTime;
            _target = Targetable.FindNearest(transform.position, Team.Player);
            bool hasTarget = _target != null && _target.IsAlive;
            if (_self.IsFrozen)
            {
                _velocity = Vector3.zero;
                return;
            }
            _stateTime += dt;
            if (!hasTarget && _state != State.Leave)
                FlyAway();
            if (Time.time >= _leaveAt && _state is State.Arrive or State.Circle or State.Climb)
                FlyAway();

            switch (_state)
            {
                case State.Arrive:
                    Steer(OrbitPoint(), definition.flySpeed, dt);
                    if ((OrbitPoint() - transform.position).sqrMagnitude < 2.5f)
                        Enter(State.Circle, Random.Range(definition.circleTime.x, definition.circleTime.y));
                    break;
                case State.Circle:
                    _orbitAngle += _orbitSide * definition.circleSpeed * dt;
                    Steer(OrbitPoint(), definition.flySpeed, dt);
                    if (_stateTime >= _stateLength)
                        ChooseAttack();
                    break;
                case State.Mark:
                    // Зависает и примеряется: хлопает крыльями на месте.
                    _velocity = Vector3.MoveTowards(_velocity, Vector3.zero, definition.acceleration * dt);
                    Move(dt);
                    if (_stateTime >= definition.markTime)
                    {
                        // Пике начинается от того места, где она зависла.
                        _diveFrom = Flat(transform.position);
                        _diveStartHeight = transform.position.y;
                        _hitThisDive = false;
                        Enter(State.Dive, Flat(_diveTo - _diveFrom).magnitude / Mathf.Max(1f, definition.diveSpeed));
                    }
                    break;
                case State.Dive:
                    Dive();
                    break;
                case State.Climb:
                    Steer(transform.position + Flat(_velocity).normalized * 3f + Vector3.up * 4f, definition.flySpeed, dt);
                    if (transform.position.y >= definition.circleHeight - 0.4f)
                    {
                        _divesLeft--;
                        if (_divesLeft <= 0)
                            FlyAway();
                        else
                            Enter(State.Circle, Random.Range(definition.circleTime.x, definition.circleTime.y));
                    }
                    break;
                case State.Grab:
                    Grab(dt);
                    break;
                case State.Carry:
                    Carry(dt);
                    break;
                case State.Leave:
                {
                    Vector3 away = Flat(transform.position);
                    away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
                    Steer(transform.position + away * 10f + Vector3.up * 6f, definition.flySpeed * 1.3f, dt);
                    if (_stateTime >= _stateLength)
                        PoolService.Despawn(gameObject);
                    break;
                }
            }
            _self.HiddenFromAim = !Low;
        }

        Vector3 OrbitPoint()
        {
            Vector3 center = _target ? _target.Position : Vector3.zero;
            float angle = _orbitAngle * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * definition.circleRadius
                          + Vector3.up * definition.circleHeight;
        }

        /// <summary>Следующий заход: чаще — пике на игрока, иногда — за мячом с земли в мешок босса.</summary>
        void ChooseAttack()
        {
            if (_boss && _boss.SackCount < _boss.Definition.sackCapacity && Random.value < definition.stealChance
                && TryFindLooseBall(out _wanted))
            {
                Enter(State.Grab);
                return;
            }
            Vector3 from = Flat(transform.position);
            Vector3 to = Flat(_target.Position);
            Vector3 direction = to - from;
            direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward;
            _diveFrom = from;
            _diveTo = to + direction * definition.diveOvershoot;
            ClearMarker();
            if (diveMarkerPrefab)
            {
                _marker = PoolService.Spawn(diveMarkerPrefab, from, Quaternion.identity);
                float diveTime = Flat(_diveTo - _diveFrom).magnitude / Mathf.Max(1f, definition.diveSpeed);
                _marker.ShowLine(_diveFrom, _diveTo, definition.markTime + diveTime * 0.6f);
            }
            GameEvents.PlaySound(SoundCue.CrowCaw, transform.position);
            Enter(State.Mark);
        }

        /// <summary>Пике: снижается к асфальту, проносится над ним и снова набирает высоту.</summary>
        void Dive()
        {
            float t = Mathf.Clamp01(_stateTime / Mathf.Max(0.05f, _stateLength));
            Vector3 flat = Vector3.Lerp(_diveFrom, _diveTo, t);
            float height = t < 0.35f ? Mathf.Lerp(_diveStartHeight, definition.diveHeight, Mathf.SmoothStep(0f, 1f, t / 0.35f))
                : t > 0.85f ? Mathf.Lerp(definition.diveHeight, definition.diveHeight + 1.5f, (t - 0.85f) / 0.15f)
                : definition.diveHeight;
            Vector3 next = new(flat.x, height, flat.z);
            _velocity = (next - transform.position) / Mathf.Max(1e-4f, Time.deltaTime);
            transform.position = next;
            Face(_velocity);

            if (!_hitThisDive && _target && _target.IsAlive
                && (_target.AimPoint - transform.position).sqrMagnitude <= definition.hitRadius * definition.hitRadius)
            {
                _hitThisDive = true;
                Vector3 direction = Flat(_diveTo - _diveFrom);
                if (_target.TryGetComponent(out IDamageable damageable))
                {
                    damageable.ApplyHit(new HitInfo
                    {
                        Damage = definition.damage,
                        Point = _target.AimPoint,
                        Direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward,
                        Force = definition.knockback,
                        SourceTeam = Team.Enemy,
                        Source = gameObject,
                        Flags = HitFlags.Melee,
                    });
                }
            }
            if (t >= 1f)
            {
                ClearMarker();
                Enter(State.Climb);
            }
        }

        void Grab(float dt)
        {
            if (_wanted == null || !_wanted.isActiveAndEnabled || _wanted.State != BallState.Loose)
            {
                _wanted = null;
                Enter(State.Circle, 0.5f);
                return;
            }
            Vector3 goal = _wanted.Position + Vector3.up * 0.5f;
            Steer(goal, definition.flySpeed * 1.2f, dt);
            if ((FeetPosition - _wanted.Position).sqrMagnitude <= 0.6f)
            {
                _carried = _wanted;
                _wanted = null;
                _carried.Stick(FeetPosition);
                GameEvents.PlaySound(SoundCue.CrowCaw, transform.position);
                Enter(State.Carry);
            }
        }

        void Carry(float dt)
        {
            if (_carried == null || _carried.State != BallState.Stuck)
            {
                _carried = null;
                Enter(State.Circle, 0.5f);
                return;
            }
            if (_boss == null || !_boss.isActiveAndEnabled)
            {
                FlyAway();
                return;
            }
            Vector3 sack = _boss.SackPosition;
            Steer(new Vector3(sack.x, Mathf.Max(sack.y + 1f, definition.carryHeight), sack.z), definition.flySpeed, dt);
            if (Flat(sack - transform.position).sqrMagnitude <= 1.5f)
            {
                var ball = _carried;
                _carried = null;
                if (!_boss.StuffBall(ball))
                    ball.Drop(FeetPosition, Vector3.down);
                Enter(State.Circle, Random.Range(definition.circleTime.x, definition.circleTime.y));
            }
        }

        bool TryFindLooseBall(out Ball found)
        {
            found = null;
            if (_target == null)
                return false;
            float best = float.PositiveInfinity;
            float minSqr = definition.stealMinDistance * definition.stealMinDistance;
            foreach (var ball in Ball.Active)
            {
                if (ball.State != BallState.Loose || Flat(ball.Position - _target.Position).sqrMagnitude < minSqr)
                    continue;
                float distance = (ball.Position - transform.position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    found = ball;
                }
            }
            return found != null;
        }

        void DropCarried()
        {
            if (_carried != null && _carried.State == BallState.Stuck)
                _carried.Drop(FeetPosition, Flat(_velocity) * 0.3f);
            _carried = null;
            _wanted = null;
        }

        void ClearMarker()
        {
            if (_marker && _marker.isActiveAndEnabled)
                _marker.Hide();
            _marker = null;
        }

        // ---------- Полёт ----------

        void Steer(Vector3 goal, float speed, float dt)
        {
            Vector3 to = goal - transform.position;
            Vector3 desired = to.sqrMagnitude > 1e-4f ? to.normalized * Mathf.Min(speed, to.magnitude * 3f) : Vector3.zero;
            _velocity = Vector3.MoveTowards(_velocity, desired, definition.acceleration * dt);
            Move(dt);
            Face(_velocity);
        }

        void Move(float dt) => transform.position += _velocity * dt;

        void Face(Vector3 velocity)
        {
            Vector3 flat = Flat(velocity);
            if (flat.sqrMagnitude > 0.05f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 0.25f);
        }

        // ---------- Попадания ----------

        public BallContactResult OnBallContact(Ball ball, in RaycastHit hit)
        {
            if (_health.IsDead)
                return BallContactResult.PassThrough;
            Vector3 direction = Flat(ball.Velocity);
            ApplyHit(new HitInfo
            {
                Damage = Mathf.Max(1, ball.Stats.Damage),
                Point = hit.point,
                Direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.up,
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
            if (hitFlash)
                hitFlash.Flash(Color.white, 0.1f);
            GameEvents.PlaySound(SoundCue.EnemyHit, hit.Point);
            _health.TryDamage(hit);
            return true;
        }

        void OnDied(HitInfo hit)
        {
            DropCarried();
            ClearMarker();
            if (feathersPrefab)
                PoolService.Spawn(feathersPrefab, transform.position, Quaternion.identity).Play(1f);
            if (!hit.Has(HitFlags.Despawn))
                GameEvents.PlaySound(SoundCue.CrowCaw, transform.position);
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            PoolService.Despawn(gameObject);
        }

        // ---------- Вид ----------

        void LateUpdate()
        {
            if (_carried != null && _carried.State == BallState.Stuck)
                _carried.HoldAt(FeetPosition);
            float dt = Time.deltaTime;
            bool gliding = _state == State.Dive;
            _flap += dt * (_state == State.Mark ? 16f : gliding ? 0f : 11f);
            float flap = gliding ? -12f : Mathf.Sin(_flap) * 40f;
            if (wingL)
                wingL.localRotation = _wingLRest * Quaternion.Euler(0f, 0f, flap);
            if (wingR)
                wingR.localRotation = _wingRRest * Quaternion.Euler(0f, 0f, -flap);
            if (visual)
            {
                // Нос вниз на пике, вверх на подъёме.
                float pitch = _state switch
                {
                    State.Dive => 12f,
                    State.Climb => -20f,
                    State.Mark => -10f,
                    _ => 0f,
                };
                visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(pitch, 0f, 0f), dt * 8f);
            }
            if (shadowBlob)
            {
                Vector3 p = transform.position;
                shadowBlob.SetPositionAndRotation(new Vector3(p.x, 0.04f, p.z), Quaternion.Euler(90f, 0f, 0f));
                float size = Mathf.Lerp(1.1f, 0.6f, Mathf.Clamp01(p.y / 7f));
                shadowBlob.localScale = new Vector3(size, size * 0.7f, 1f);
            }
        }

        void Enter(State state, float length = 0f)
        {
            _state = state;
            _stateTime = 0f;
            _stateLength = length;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
