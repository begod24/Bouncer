using System;
using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Balls
{
    public enum BallState
    {
        /// <summary>Только что выдан из пула.</summary>
        Idle,
        /// <summary>Брошен и опасен: летит почти плоско, рикошетит от стен.</summary>
        Live,
        /// <summary>Отскочил вверх от тела: безопасен, можно поймать «свечкой».</summary>
        Popped,
        /// <summary>Лежит или катится по полу: можно подобрать.</summary>
        Loose,
        /// <summary>Мяч на резинке летит обратно в руки бросившему: безопасен, подобрать нельзя.</summary>
        Returning,
    }

    /// <summary>
    /// Мяч. В полёте (Live/Popped) движется сам через SphereCast — так рикошеты точные
    /// и предсказуемые, мяч не пролетает сквозь тонкие стены. Лежащий мяч — обычная физика.
    /// Эффекты типа мяча и карточек (<see cref="BallPerks"/>) срабатывают здесь же: цепочка, урон по площади,
    /// раскол на двойников, бумеранг и возврат на резинке.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class Ball : MonoBehaviour, IPoolable
    {
        const float Skin = 0.01f;
        const int MaxSweepIterations = 4;
        const float KillY = -5f;
        /// <summary>С какого расстояния вернувшийся мяч попадает в руки.</summary>
        const float ReceiveDistance = 1.2f;
        /// <summary>На какой высоте над ногами бросившего летит возвращающийся мяч.</summary>
        const float ReturnHeight = 1.1f;
        const float MaxReturnTime = 4f;

        static readonly List<Ball> s_active = new();
        static readonly List<Ball> s_loose = new();
        static readonly Collider[] s_area = new Collider[32];
        static readonly List<IDamageable> s_areaDamaged = new();

        public static IReadOnlyList<Ball> Active => s_active;
        public static int LooseCount => s_loose.Count;

        [SerializeField] BallDefinition definition;
        [Tooltip("Дочерний меш — масштабируется под радиус из определения")]
        [SerializeField] Transform mesh;

        readonly RaycastHit[] _hits = new RaycastHit[16];
        readonly List<Collider> _ignored = new(4);
        readonly List<(Collider collider, float until)> _ignoredFor = new(4);
        Rigidbody _rb;
        SphereCollider _collider;
        IBallReceiver _receiver;
        Vector3 _velocity;
        float _gravity;
        float _stateTime;
        int _chainsLeft;
        bool _splitDone;
        bool _elasticDone;

        public BallDefinition Definition => definition;
        public BallState State { get; private set; }
        /// <summary>Команда бросившего. У лежащего мяча — Neutral.</summary>
        public Team Team { get; private set; }
        public GameObject Thrower { get; private set; }
        public ThrowStats Stats { get; private set; }
        public BallPerks Perks { get; private set; }
        /// <summary>Мяч-двойник: исчезает, коснувшись пола, подобрать и поймать нельзя.</summary>
        public bool IsPhantom { get; private set; }
        /// <summary>Бумеранг развернулся и летит обратно к бросившему.</summary>
        public bool IsComingBack { get; private set; }
        public int Ricochets { get; private set; }
        public float Radius => definition.radius;
        public Vector3 Position => _rb.position;
        public Vector3 Velocity => State == BallState.Loose ? _rb.linearVelocity : _velocity;
        public bool IsDangerous => State == BallState.Live;

        public event Action<Ball> StateChanged;
        /// <summary>Рикошет от стены: мяч, точка, нормаль.</summary>
        public event Action<Ball, Vector3, Vector3> Ricocheted;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<SphereCollider>();
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _rb.isKinematic = true;
            ApplyDefinition();
        }

        public void ApplyDefinition()
        {
            _collider.radius = definition.radius;
            if (mesh)
                mesh.localScale = Vector3.one * (definition.radius * 2f);
            _rb.mass = definition.mass;
            _rb.linearDamping = definition.looseDamping;
            _rb.angularDamping = 0.5f;
        }

        void OnEnable() => s_active.Add(this);

        void OnDisable()
        {
            s_active.Remove(this);
            s_loose.Remove(this);
        }

        public void OnSpawned()
        {
            Team = Team.Neutral;
            Thrower = null;
            _receiver = null;
            Perks = default;
            IsPhantom = false;
            IsComingBack = false;
            Ricochets = 0;
            _velocity = Vector3.zero;
            _ignoredFor.Clear();
            MakeKinematicAt(transform.position);
            SetState(BallState.Idle);
        }

        public void OnDespawned()
        {
            s_loose.Remove(this);
            State = BallState.Idle;
        }

        // ---------- API ----------

        public void Launch(in BallThrow t)
        {
            Team = t.Team;
            Thrower = t.Thrower;
            _receiver = t.Thrower ? t.Thrower.GetComponent<IBallReceiver>() : null;
            Stats = t.Stats;
            Perks = t.Perks;
            IsPhantom = t.Phantom;
            IsComingBack = false;
            Ricochets = 0;
            _chainsLeft = t.Perks.chainBounces;
            _splitDone = false;
            _elasticDone = false;
            _ignoredFor.Clear();

            Vector3 direction = Flat(t.Direction);
            if (direction.sqrMagnitude < 1e-6f)
                direction = Flat(transform.forward);
            direction.Normalize();

            _velocity = direction * t.Stats.Speed + Vector3.up * t.Stats.UpVelocity;
            _gravity = t.Stats.Gravity;
            s_loose.Remove(this);
            MakeKinematicAt(t.Origin);
            SetState(BallState.Live);
        }

        /// <summary>
        /// Отбить летящий мяч (качели): новая скорость и параметры удара, время полёта считается заново.
        /// Команда не меняется — мяч игрока остаётся мячом игрока.
        /// </summary>
        public void Redirect(Vector3 velocity, in ThrowStats stats)
        {
            if (State != BallState.Live)
                return;
            Stats = stats;
            _velocity = velocity;
            _gravity = stats.Gravity;
            SetState(BallState.Live);
        }

        /// <summary>Не сталкиваться с коллайдером столько секунд (двойник вылетает из тела врага).</summary>
        public void IgnoreFor(Collider other, float seconds)
        {
            if (other)
                _ignoredFor.Add((other, Time.time + seconds));
        }

        /// <summary>Положить мяч на арену (например, если игроку некуда его взять).</summary>
        public void Drop(Vector3 position, Vector3 velocity)
        {
            _elasticDone = true;
            BecomeLoose(position, velocity);
        }

        /// <summary>Мяч забрали — вернуть в пул.</summary>
        public void Consume() => PoolService.Despawn(gameObject);

        public bool IsCatchableBy(Team catcher) =>
            !IsPhantom && (State == BallState.Popped || (State == BallState.Live && this.Team.IsHostileTo(catcher)));

        /// <summary>Куда упадёт летящий мяч (пол арены на y = 0).</summary>
        public bool TryPredictLanding(out Vector3 point)
        {
            point = default;
            if (State != BallState.Live && State != BallState.Popped)
                return false;
            Vector3 position = transform.position;
            float height = Mathf.Max(0f, position.y - definition.radius);
            float gravity = Mathf.Max(0.01f, _gravity);
            float time = (_velocity.y + Mathf.Sqrt(_velocity.y * _velocity.y + 2f * gravity * height)) / gravity;
            point = position + Flat(_velocity) * time;
            point.y = 0f;
            return true;
        }

        public static void DespawnAllLoose()
        {
            for (int i = s_loose.Count - 1; i >= 0; i--)
                s_loose[i].Consume();
        }

        // ---------- Симуляция ----------

        void FixedUpdate()
        {
            switch (State)
            {
                case BallState.Live:
                    TickFlight(Time.fixedDeltaTime, live: true);
                    break;
                case BallState.Popped:
                    TickFlight(Time.fixedDeltaTime, live: false);
                    break;
                case BallState.Returning:
                    TickReturn(Time.fixedDeltaTime);
                    break;
                case BallState.Loose:
                    if (_rb.position.y < KillY)
                    {
                        Consume();
                        break;
                    }
                    // В песке мяч быстро останавливается.
                    _rb.linearDamping = definition.looseDamping + GroundZone.BallDampingAt(_rb.position);
                    break;
            }
        }

        void TickFlight(float dt, bool live)
        {
            _stateTime += dt;
            if (live && Perks.boomerang && _stateTime >= definition.boomerangDelay && CanReturn())
                SteerBack(dt);
            else
                _velocity.y -= _gravity * dt;

            Vector3 position = _rb.position;
            float remaining = _velocity.magnitude * dt;
            int mask = live ? Layers.LiveBallMask(Team) : Layers.EnvironmentMask;
            _ignored.Clear();

            for (int i = 0; i < MaxSweepIterations && remaining > 1e-5f; i++)
            {
                Vector3 direction = _velocity.normalized;
                if (!Sweep(position, direction, remaining, mask, out RaycastHit hit))
                {
                    position += direction * remaining;
                    break;
                }

                float travel = Mathf.Max(0f, hit.distance - Skin);
                position += direction * travel;
                remaining -= travel;

                if (live)
                {
                    var target = hit.collider.GetComponentInParent<IBallTarget>();
                    if (target != null)
                    {
                        var result = target.OnBallContact(this, hit);
                        if (result is BallContactResult.PassThrough or BallContactResult.Redirected)
                        {
                            _ignored.Add(hit.collider);
                            continue;
                        }
                        if (result == BallContactResult.Pierce)
                        {
                            _ignored.Add(hit.collider);
                            OnTargetHit(hit, position);
                            _velocity = new Vector3(_velocity.x * definition.pierceSpeedKeep, _velocity.y,
                                _velocity.z * definition.pierceSpeedKeep);
                            remaining *= definition.pierceSpeedKeep;
                            if (Flat(_velocity).magnitude < definition.minLiveSpeed)
                            {
                                Pop(position, hit.normal);
                                return;
                            }
                            continue;
                        }
                        // Цель забрала мяч или сама сменила ему состояние.
                        if (result == BallContactResult.Caught || State != BallState.Live)
                            return;
                        if (result == BallContactResult.Hit)
                        {
                            OnTargetHit(hit, position);
                            if (_chainsLeft > 0 && TryChain(hit, position))
                                continue;
                            Pop(position, hit.normal);
                            return;
                        }
                        // Bounce — отражаемся, как от стены.
                    }
                }

                // Пол или верх препятствия: мяч «умер» и дальше катится по физике.
                if (hit.normal.y > 0.6f)
                {
                    BecomeLoose(position, Vector3.Reflect(_velocity, hit.normal) * definition.floorBounceKeep);
                    return;
                }

                Vector3 normal = Flat(hit.normal);
                if (normal.sqrMagnitude < 1e-4f)
                    normal = -Flat(direction);
                normal.Normalize();

                float keep = live ? definition.wallSpeedKeep : definition.poppedWallKeep;
                Vector3 reflected = Vector3.Reflect(_velocity, normal);
                _velocity = new Vector3(reflected.x * keep, reflected.y, reflected.z * keep);
                remaining *= keep;
                Ricocheted?.Invoke(this, hit.point, normal);
                GameEvents.PlaySound(SoundCue.BallWall, hit.point);

                if (live)
                {
                    Ricochets++;
                    if (Ricochets > definition.maxRicochets || Flat(_velocity).magnitude < definition.minLiveSpeed)
                    {
                        BecomeLoose(position, _velocity * 0.5f);
                        return;
                    }
                }
            }

            if (position.y < KillY)
            {
                Consume();
                return;
            }
            if (live && IsComingBack && TryHandBack(position))
                return;
            float lifetime = Perks.boomerang ? definition.maxLiveTime + 2f : definition.maxLiveTime;
            if (live && _stateTime > lifetime)
            {
                BecomeLoose(position, _velocity * 0.5f);
                return;
            }
            _rb.MovePosition(position);
        }

        bool Sweep(Vector3 origin, Vector3 direction, float distance, int mask, out RaycastHit best)
        {
            best = default;
            int count = Physics.SphereCastNonAlloc(origin, definition.radius, direction, _hits, distance + Skin, mask,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider == _collider || IsIgnored(hit.collider))
                    continue;

                if (hit.distance <= 0f)
                {
                    // Мяч появился внутри коллайдера. Персонажа считаем попаданием в упор,
                    // окружение пропускаем, чтобы мяч не застрял.
                    if (hit.collider.gameObject.layer != Layers.Environment)
                    {
                        hit.point = origin;
                        hit.normal = -direction;
                        hit.distance = 0f;
                        best = hit;
                        return true;
                    }
                    _ignored.Add(hit.collider);
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = hit;
                    found = true;
                }
            }
            return found;
        }

        bool IsIgnored(Collider other)
        {
            if (_ignored.Contains(other))
                return true;
            for (int i = _ignoredFor.Count - 1; i >= 0; i--)
            {
                if (Time.time > _ignoredFor[i].until)
                    _ignoredFor.RemoveAt(i);
                else if (_ignoredFor[i].collider == other)
                    return true;
            }
            return false;
        }

        void Pop(Vector3 position, Vector3 hitNormal)
        {
            // Двойник свечки не даёт — ударил и исчез.
            if (IsPhantom)
            {
                Consume();
                return;
            }

            Vector3 horizontal = Flat(_velocity);
            Vector3 normal = Flat(hitNormal);
            if (normal.sqrMagnitude < 1e-4f)
                normal = -horizontal;
            if (normal.sqrMagnitude < 1e-4f)
                normal = Vector3.forward;
            normal.Normalize();

            Vector3 bounce = Vector3.Reflect(horizontal, normal) * definition.popHorizontalKeep;
            _velocity = bounce + Vector3.up * definition.popUpSpeed;
            _gravity = definition.popGravity;
            _rb.MovePosition(position);
            SetState(BallState.Popped);
        }

        void BecomeLoose(Vector3 position, Vector3 velocity)
        {
            if (IsPhantom)
            {
                Consume();
                return;
            }
            if (Perks.elastic && !_elasticDone && CanReturn())
            {
                StartReturn(position);
                return;
            }

            _rb.isKinematic = false;
            _rb.position = position;
            _rb.linearVelocity = velocity;
            _rb.angularVelocity = Vector3.zero;
            Team = Team.Neutral;
            SetState(BallState.Loose);

            s_loose.Remove(this);
            s_loose.Add(this);
            while (s_loose.Count > definition.maxLooseBalls)
                s_loose[0].Consume();
        }

        // ---------- Эффекты мяча ----------

        /// <summary>Мяч задел врага: урон по площади и раскол на двойников.</summary>
        void OnTargetHit(in RaycastHit hit, Vector3 position)
        {
            if (Perks.areaRadius > 0f && Perks.areaDamage > 0)
                DamageArea(hit);
            if (Perks.splitOnHit && !IsPhantom && !_splitDone)
                Split(hit, position);
        }

        void DamageArea(in RaycastHit hit)
        {
            int mask = Layers.LiveBallMask(Team) & ~Layers.EnvironmentMask;
            int count = Physics.OverlapSphereNonAlloc(hit.point, Perks.areaRadius, s_area, mask, QueryTriggerInteraction.Ignore);
            var direct = hit.collider.GetComponentInParent<IDamageable>();
            s_areaDamaged.Clear();
            for (int i = 0; i < count; i++)
            {
                var other = s_area[i];
                var target = other.GetComponentInParent<IDamageable>();
                if (target == null || target == direct || s_areaDamaged.Contains(target))
                    continue;
                s_areaDamaged.Add(target);
                Vector3 away = Flat(other.transform.position - hit.point);
                target.ApplyHit(new HitInfo
                {
                    Damage = Perks.areaDamage,
                    Point = other.ClosestPoint(hit.point),
                    Direction = away.sqrMagnitude > 1e-4f ? away.normalized : Flat(_velocity).normalized,
                    Force = Stats.Knockback * 0.7f,
                    SourceTeam = Team,
                    Source = Thrower,
                    Flags = HitFlags.Area,
                });
            }
            if (definition.areaEffect)
            {
                var ring = PoolService.Spawn(definition.areaEffect, new Vector3(hit.point.x, 0.05f, hit.point.z), Quaternion.identity);
                if (ring.TryGetComponent(out ExpandingRing expanding))
                    expanding.Play(Perks.areaRadius);
            }
            GameEvents.PlaySound(SoundCue.AreaThud, hit.point);
        }

        /// <summary>Раскол: два двойника разлетаются в стороны от направления мяча.</summary>
        void Split(in RaycastHit hit, Vector3 position)
        {
            _splitDone = true;
            if (!TryGetComponent(out PooledObject tag) || tag.Prefab == null)
                return;
            Vector3 forward = Flat(_velocity);
            if (forward.sqrMagnitude < 1e-4f)
                forward = -Flat(hit.normal);
            if (forward.sqrMagnitude < 1e-4f)
                return;
            forward.Normalize();

            var stats = Stats;
            stats.Speed = Mathf.Max(Flat(_velocity).magnitude * definition.splitSpeedKeep, definition.minLiveSpeed + 2f);
            stats.UpVelocity = 1f;
            for (int side = -1; side <= 1; side += 2)
            {
                var twin = PoolService.Spawn(tag.Prefab, position, Quaternion.identity).GetComponent<Ball>();
                twin.Launch(new BallThrow
                {
                    Origin = position,
                    Direction = Quaternion.Euler(0f, side * definition.splitAngle, 0f) * forward,
                    Stats = stats,
                    Team = Team,
                    Thrower = Thrower,
                    Perks = Perks.ForTwin(),
                    Phantom = true,
                });
                twin.IgnoreFor(hit.collider, 0.3f);
            }
        }

        /// <summary>Цепочка (волейбольный): после попадания мяч перелетает к ближайшему другому врагу.</summary>
        bool TryChain(in RaycastHit hit, Vector3 position)
        {
            var struck = hit.collider.GetComponentInParent<Targetable>();
            Targetable next = null;
            float bestSqr = definition.chainRange * definition.chainRange;
            foreach (var target in Targetable.All)
            {
                if (target == struck || !target.IsAlive || !Team.IsHostileTo(target.Team))
                    continue;
                float sqr = Flat(target.AimPoint - position).sqrMagnitude;
                if (sqr > 0.25f && sqr < bestSqr)
                {
                    bestSqr = sqr;
                    next = target;
                }
            }
            if (next == null)
                return false;

            _chainsLeft--;
            Vector3 to = next.AimPoint - position;
            Vector3 flat = Flat(to);
            float speed = Mathf.Max(Flat(_velocity).magnitude * definition.chainSpeedKeep, definition.minLiveSpeed + 2f);
            float time = Mathf.Max(0.05f, flat.magnitude / speed);
            // Вертикальная скорость — чтобы долететь до груди следующего врага.
            float up = (to.y + 0.5f * _gravity * time * time) / time;
            _velocity = flat.normalized * speed + Vector3.up * up;
            _stateTime = 0f;
            IgnoreFor(hit.collider, 0.25f);
            return true;
        }

        bool CanReturn() => !IsPhantom && _receiver != null && Thrower != null && Thrower.activeInHierarchy;

        /// <summary>Бумеранг: плавно разворачивает мяч к бросившему и держит на высоте груди.</summary>
        void SteerBack(float dt)
        {
            IsComingBack = true;
            Vector3 position = _rb.position;
            Vector3 target = Thrower.transform.position + Vector3.up * ReturnHeight;
            Vector3 toTarget = Flat(target - position);
            Vector3 horizontal = Flat(_velocity);
            float speed = Mathf.Max(horizontal.magnitude, definition.minLiveSpeed + 1f);
            Vector3 heading = horizontal.sqrMagnitude > 1e-4f ? horizontal.normalized : toTarget.normalized;
            if (toTarget.sqrMagnitude > 1e-4f)
                heading = Vector3.RotateTowards(heading, toTarget.normalized, definition.boomerangTurnRate * Mathf.Deg2Rad * dt, 0f);
            _velocity = heading * speed + Vector3.up * Mathf.Clamp((target.y - position.y) * 4f, -4f, 4f);
        }

        /// <summary>Вернувшийся мяч долетел до бросившего: в руки, а если руки заняты — под ноги.</summary>
        bool TryHandBack(Vector3 position)
        {
            if (!CanReturn())
                return false;
            Vector3 delta = Thrower.transform.position + Vector3.up * ReturnHeight - position;
            if (Flat(delta).sqrMagnitude > ReceiveDistance * ReceiveDistance)
                return false;
            if (_receiver.TryReceive(this))
                return true;
            _elasticDone = true;
            BecomeLoose(position, Vector3.zero);
            return true;
        }

        /// <summary>На резинке: упавший мяч сам летит обратно в руки.</summary>
        void StartReturn(Vector3 position)
        {
            _elasticDone = true;
            _velocity = Vector3.zero;
            MakeKinematicAt(position);
            SetState(BallState.Returning);
        }

        void TickReturn(float dt)
        {
            _stateTime += dt;
            Vector3 position = _rb.position;
            if (!CanReturn() || _stateTime > MaxReturnTime)
            {
                BecomeLoose(position, Vector3.zero);
                return;
            }

            Vector3 delta = Thrower.transform.position + Vector3.up * ReturnHeight - position;
            float distance = delta.magnitude;
            if (distance <= ReceiveDistance)
            {
                if (!_receiver.TryReceive(this))
                    BecomeLoose(position, Vector3.zero);
                return;
            }
            // Резинка тянет всё сильнее: мяч разгоняется к рукам.
            float speed = definition.elasticReturnSpeed * Mathf.Clamp01(0.3f + _stateTime * 3f);
            _velocity = delta / distance * speed;
            _rb.MovePosition(position + delta / distance * Mathf.Min(distance, speed * dt));
        }

        void MakeKinematicAt(Vector3 position)
        {
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
            }
            _rb.position = position;
            transform.position = position;
        }

        void SetState(BallState state)
        {
            State = state;
            _stateTime = 0f;
            StateChanged?.Invoke(this);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_active.Clear();
            s_loose.Clear();
        }
    }
}
