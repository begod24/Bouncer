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
        /// <summary>Застрял во враге (плюшевый мишка): висит на нём, пока его не выбьют.</summary>
        Stuck,
    }

    /// <summary>
    /// Мяч. В полёте (Live/Popped) движется сам через SphereCast — так рикошеты точные
    /// и предсказуемые, мяч не пролетает сквозь тонкие стены. Лежащий мяч — обычная физика.
    /// Эффекты типа мяча и карточек (<see cref="BallPerks"/>) срабатывают здесь же: цепочка, урон по площади,
    /// раскол на двойников, бумеранг и возврат на резинке, отскоки от асфальта, взрыв, след жвачки,
    /// полёт змейкой и задевание врагов по пути. Борта с <see cref="RicochetSurface"/> отражают мяч «идеально».
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
        /// <summary>Йо-йо: длина нити — пролетев столько метров, мяч летит обратно.</summary>
        const float YoyoRange = 9f;
        /// <summary>Йо-йо: насколько шире мяча полоса, в которой он задевает врагов на обратном пути.</summary>
        const float YoyoGrazeExtra = 0.35f;
        /// <summary>Глаз-алмаз: дальше этого мяч врагов не замечает.</summary>
        const float HomingRange = 14f;
        /// <summary>Глаз-алмаз: половина угла, в котором мяч видит врагов впереди, градусы.</summary>
        const float HomingCone = 75f;
        /// <summary>Доводка после рикошета от борта (<see cref="RicochetSurface"/>): дальше этого врагов не ищет.</summary>
        const float RicochetAssistRange = 30f;

        static readonly List<Ball> s_active = new();
        static readonly List<Ball> s_loose = new();
        static readonly Collider[] s_area = new Collider[32];
        static readonly Collider[] s_graze = new Collider[32];
        static readonly List<IDamageable> s_areaDamaged = new();

        public static IReadOnlyList<Ball> Active => s_active;
        public static int LooseCount => s_loose.Count;

        [SerializeField] BallDefinition definition;
        [Tooltip("Дочерний меш — масштабируется под радиус из определения")]
        [SerializeField] Transform mesh;

        [Header("Эффекты карточек")]
        [Tooltip("Пятно жвачки («Жвачка»), из пула")]
        [SerializeField] GumSpot gumSpot;
        [Tooltip("Кольцо взрыва («Горячая картошка»), из пула")]
        [SerializeField] ExpandingRing blastRing;

        readonly RaycastHit[] _hits = new RaycastHit[16];
        readonly List<Collider> _ignored = new(4);
        readonly List<(Collider collider, float until)> _ignoredFor = new(4);
        readonly List<IDamageable> _grazed = new(8);
        Rigidbody _rb;
        SphereCollider _collider;
        IBallReceiver _receiver;
        Vector3 _velocity;
        float _gravity;
        float _stateTime;
        int _chainsLeft;
        bool _splitDone;
        bool _elasticDone;
        int _floorBouncesLeft;
        bool _blastDone;
        float _gumDistance;
        Vector3 _snakeHeading;
        float _snakeDistance;
        float _snakeSide = 1f;
        /// <summary>Сколько метров пролетел по горизонтали с броска (длина нити йо-йо).</summary>
        float _travel;
        bool _yoyoBack;

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
        /// <summary>Горячая картошка летит и ещё не взорвалась.</summary>
        public bool BlastPending => State == BallState.Live && Perks.blastRadius > 0f && !_blastDone;
        /// <summary>Сдутый мяч: пролетает сквозь врагов, задевая всех по пути.</summary>
        bool Grazes => Perks.grazeRadius > 0f;

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
            _grazed.Clear();
            _floorBouncesLeft = 0;
            _blastDone = false;
            _travel = 0f;
            _yoyoBack = false;
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
            _grazed.Clear();
            _floorBouncesLeft = t.Perks.floorBounces;
            _blastDone = false;
            _travel = 0f;
            _yoyoBack = false;
            // Первое пятно жвачки — вскоре после броска, а не через целый шаг.
            _gumDistance = definition.gumSpacing * 0.5f;

            Vector3 direction = Flat(t.Direction);
            if (direction.sqrMagnitude < 1e-6f)
                direction = Flat(transform.forward);
            direction.Normalize();

            _snakeHeading = direction;
            _snakeDistance = 0f;
            _snakeSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
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
            // Отбитый мяч — новый удар: змейка вьётся вокруг нового направления, задеть можно снова всех.
            Vector3 heading = Flat(velocity);
            if (heading.sqrMagnitude > 1e-4f)
                _snakeHeading = heading.normalized;
            _snakeDistance = 0f;
            _grazed.Clear();
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

        /// <summary>Мяч застрял во враге: безопасен, не ловится и не подбирается, пока враг его не отпустит.</summary>
        public void Stick(Vector3 position)
        {
            Team = Team.Neutral;
            _receiver = null;
            _yoyoBack = false;
            IsComingBack = false;
            s_loose.Remove(this);
            MakeKinematicAt(position);
            SetState(BallState.Stuck);
        }

        /// <summary>Застрявший мяч едет вместе с врагом.</summary>
        public void HoldAt(Vector3 position)
        {
            if (State == BallState.Stuck)
                MakeKinematicAt(position);
        }

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
            if (live && _yoyoBack)
            {
                TickYoyoBack(dt);
                return;
            }
            _stateTime += dt;
            if (live && Perks.boomerang && !Perks.yoyo && _stateTime >= definition.boomerangDelay && CanReturn())
            {
                SteerBack(dt);
            }
            else
            {
                _velocity.y -= _gravity * dt;
                if (live && Perks.homing > 0f && !IsComingBack)
                    Home(dt);
                if (live && Perks.snakeAmplitude > 0f)
                    Snake(dt);
            }

            Vector3 position = _rb.position;
            float remaining = _velocity.magnitude * dt;
            // Сдутый мяч врагов не сбивает телом, а задевает по пути (Graze) — сталкивается только с окружением.
            int mask = live && !Grazes ? Layers.LiveBallMask(Team) : Layers.EnvironmentMask;
            _ignored.Clear();

            for (int i = 0; i < MaxSweepIterations && remaining > 1e-5f; i++)
            {
                Vector3 direction = _velocity.normalized;
                if (!Sweep(position, direction, remaining, mask, out RaycastHit hit))
                {
                    Advance(ref position, direction * remaining, live);
                    break;
                }

                float travel = Mathf.Max(0f, hit.distance - Skin);
                Advance(ref position, direction * travel, live);
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
                            OnTargetHit(hit.point, hit.normal, hit.collider, position);
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
                            OnTargetHit(hit.point, hit.normal, hit.collider, position);
                            // Рогатка: заряженный мяч пробивает врага и летит дальше, не теряя скорости.
                            if (Perks.chargedPierce && Stats.Has(HitFlags.Charged))
                            {
                                _ignored.Add(hit.collider);
                                continue;
                            }
                            if (_chainsLeft > 0 && TryChain(hit, position))
                                continue;
                            if (Perks.yoyo && TryStartYoyo(hit.collider, position))
                                return;
                            Pop(position, hit.normal);
                            return;
                        }
                        // Bounce — отражаемся, как от стены.
                    }
                }

                // Пол или верх препятствия: мяч «умер» и дальше катится по физике.
                // Горячая картошка тут взрывается, попрыгунчик отскакивает и летит дальше опасным.
                if (hit.normal.y > 0.6f)
                {
                    if (BlastPending)
                        Blast(hit.point, null);
                    if (live && _floorBouncesLeft > 0 && TryFloorBounce(hit.point, ref remaining))
                        continue;
                    if (live && Perks.yoyo && TryStartYoyo(null, position))
                        return;
                    BecomeLoose(position, Vector3.Reflect(_velocity, hit.normal) * definition.floorBounceKeep);
                    return;
                }

                Vector3 normal = Flat(hit.normal);
                if (normal.sqrMagnitude < 1e-4f)
                    normal = -Flat(direction);
                normal.Normalize();

                // Борта коробки: рикошет без потери скорости и высоты, лимит рикошетов не тратится.
                var surface = live ? hit.collider.GetComponentInParent<RicochetSurface>() : null;
                float keep = !live ? definition.poppedWallKeep : surface ? surface.SpeedKeep : definition.wallSpeedKeep;
                Vector3 reflected = Vector3.Reflect(_velocity, normal);
                _velocity = new Vector3(reflected.x * keep, reflected.y, reflected.z * keep);
                _snakeHeading = Vector3.Reflect(_snakeHeading, normal);
                remaining *= keep;
                if (surface)
                {
                    if (surface.KeepHeight && _velocity.y < 0f)
                        _velocity.y = 0f;
                    if (surface.AimAssist > 0f && Team == Team.Player)
                        AssistAim(position, surface.AimAssist);
                }
                Ricocheted?.Invoke(this, hit.point, normal);
                GameEvents.PlaySound(SoundCue.BallWall, hit.point);

                if (live)
                {
                    // Стеночка: каждый рикошет делает этот бросок сильнее.
                    if (Perks.wallDamage > 0)
                        AddDamage(Perks.wallDamage);
                    if (Perks.yoyo && TryStartYoyo(null, position))
                        return;
                    if (surface == null || !surface.Free)
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
            if (live && Perks.yoyo && _travel >= YoyoRange && TryStartYoyo(null, position))
                return;
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
            // Горячая картошка, так ни во что и не попав, взрывается, когда перестаёт быть опасной.
            if (BlastPending)
                Blast(position, null);
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

        /// <summary>Мяч задел врага: урон по площади, взрыв горячей картошки, раскол на двойников.</summary>
        void OnTargetHit(Vector3 point, Vector3 normal, Collider struck, Vector3 position)
        {
            var direct = struck ? struck.GetComponentInParent<IDamageable>() : null;
            if (Perks.areaRadius > 0f && Perks.areaDamage > 0)
                DamageArea(point, direct);
            if (BlastPending)
                Blast(point, direct);
            // Двойник раскалывается, только если ему это передали («Град»): см. BallPerks.ForTwin.
            if (Perks.splitOnHit && !_splitDone)
                Split(normal, struck, position);
        }

        void DamageArea(Vector3 point, IDamageable direct)
        {
            HitAround(point, Perks.areaRadius, Perks.areaDamage, direct, Stats.Knockback * 0.7f, HitFlags.Area, Perks.areaStun);
            // Гиря: оглушает и того, в кого попал сам мяч.
            if (Perks.areaStun > 0f && direct is Component struck && struck && struck.TryGetComponent(out Targetable target))
                target.Freeze(Perks.areaStun);
            if (definition.areaEffect)
            {
                var ring = PoolService.Spawn(definition.areaEffect, new Vector3(point.x, 0.05f, point.z), Quaternion.identity);
                if (ring.TryGetComponent(out ExpandingRing expanding))
                    expanding.Play(Perks.areaRadius);
            }
            GameEvents.PlaySound(SoundCue.AreaThud, point);
        }

        /// <summary>Горячая картошка: взрыв бьёт всех вокруг, кроме того, в кого мяч попал сам.</summary>
        void Blast(Vector3 point, IDamageable direct)
        {
            _blastDone = true;
            if (Perks.blastDamage > 0)
                HitAround(point, Perks.blastRadius, Perks.blastDamage, direct, definition.blastKnockback, HitFlags.Area | HitFlags.Charged);
            if (blastRing)
                PoolService.Spawn(blastRing, new Vector3(point.x, 0.05f, point.z), Quaternion.identity).Play(Perks.blastRadius);
            GameEvents.PlaySound(SoundCue.AreaThud, point);
            GameFeel.Shake(0.5f);
        }

        /// <summary>Урон всем противникам в радиусе, кроме exclude: их отбрасывает от центра, stun — оглушает (Гиря).</summary>
        void HitAround(Vector3 center, float radius, int damage, IDamageable exclude, float force, HitFlags flags, float stun = 0f)
        {
            int mask = Layers.LiveBallMask(Team) & ~Layers.EnvironmentMask;
            int count = Physics.OverlapSphereNonAlloc(center, radius, s_area, mask, QueryTriggerInteraction.Ignore);
            s_areaDamaged.Clear();
            for (int i = 0; i < count; i++)
            {
                var other = s_area[i];
                var target = other.GetComponentInParent<IDamageable>();
                if (target == null || target == exclude || s_areaDamaged.Contains(target))
                    continue;
                s_areaDamaged.Add(target);
                Vector3 away = Flat(other.transform.position - center);
                target.ApplyHit(new HitInfo
                {
                    Damage = damage,
                    Point = other.ClosestPoint(center),
                    Direction = away.sqrMagnitude > 1e-4f ? away.normalized : Flat(_velocity).normalized,
                    Force = force,
                    SourceTeam = Team,
                    Source = Thrower,
                    Flags = flags,
                });
                if (stun > 0f && target is Component component && component && component.TryGetComponent(out Targetable targetable))
                    targetable.Freeze(stun);
            }
        }

        /// <summary>Раскол: два двойника разлетаются в стороны от направления мяча.</summary>
        void Split(Vector3 normal, Collider struck, Vector3 position)
        {
            _splitDone = true;
            if (!TryGetComponent(out PooledObject tag) || tag.Prefab == null)
                return;
            Vector3 forward = Flat(_velocity);
            if (forward.sqrMagnitude < 1e-4f)
                forward = -Flat(normal);
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
                twin.IgnoreFor(struck, 0.3f);
            }
        }

        /// <summary>Отрезок пути в полёте: сдутый мяч задевает всех рядом, жвачка капает на асфальт.</summary>
        void Advance(ref Vector3 position, Vector3 step, bool live)
        {
            Vector3 from = position;
            position += step;
            if (!live)
                return;
            _travel += Flat(step).magnitude;
            if (Grazes)
                Graze(from, position, Perks.grazeRadius);
            if (Perks.gumTrail)
                TrailGum(from, position);
        }

        /// <summary>
        /// Задеть всех противников рядом с отрезком пути (каждого — раз за бросок) и лететь дальше:
        /// сдутый мяч всегда, йо-йо — на обратном пути.
        /// </summary>
        void Graze(Vector3 from, Vector3 to, float radius)
        {
            int mask = Layers.LiveBallMask(Team) & ~Layers.EnvironmentMask;
            int count = Physics.OverlapCapsuleNonAlloc(from, to, radius, s_graze, mask, QueryTriggerInteraction.Ignore);
            Vector3 direction = Flat(_velocity);
            direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Flat(transform.forward).normalized;
            for (int i = 0; i < count; i++)
            {
                var other = s_graze[i];
                if (IsIgnored(other))
                    continue;
                var target = other.GetComponentInParent<IDamageable>();
                if (target == null || _grazed.Contains(target))
                    continue;
                _grazed.Add(target);
                Vector3 point = other.ClosestPoint(to);
                target.ApplyHit(new HitInfo
                {
                    Damage = Stats.Damage,
                    Point = point,
                    Direction = direction,
                    Force = Stats.Knockback * definition.grazeKnockbackScale,
                    SourceTeam = Team,
                    Source = Thrower,
                    Flags = Stats.Flags,
                });
                OnTargetHit(point, -direction, other, to);
            }
        }

        /// <summary>Сдутый мяч: виляет вокруг направления броска — путь похож на синусоиду.</summary>
        void Snake(float dt)
        {
            Vector3 horizontal = Flat(_velocity);
            float speed = horizontal.magnitude;
            if (speed < 1e-3f)
                return;
            // Путь: смещение A·r(x)·sin(k·x) вбок от направления броска, x — пройденное вдоль него.
            // r(x) за первые полволны растёт от 0 до 1, чтобы мяч вылетал из рук по прицелу. Угол — наклон этой кривой.
            float k = 2f * Mathf.PI / definition.snakeWavelength;
            float rampLength = definition.snakeWavelength * 0.5f;
            float ramp = Mathf.Clamp01(_snakeDistance / rampLength);
            float rampSlope = ramp < 1f ? 1f / rampLength : 0f;
            float phase = _snakeDistance * k;
            float angle = Mathf.Atan(Perks.snakeAmplitude * (rampSlope * Mathf.Sin(phase) + ramp * k * Mathf.Cos(phase)));
            _snakeDistance += speed * Mathf.Cos(angle) * dt;
            Vector3 direction = Quaternion.AngleAxis(angle * Mathf.Rad2Deg * _snakeSide, Vector3.up) * _snakeHeading;
            _velocity = direction * speed + Vector3.up * _velocity.y;
        }

        /// <summary>Жвачка: каждые gumSpacing метров полёта на асфальте под мячом остаётся липкое пятно.</summary>
        void TrailGum(Vector3 from, Vector3 to)
        {
            if (gumSpot == null)
                return;
            float length = Flat(to - from).magnitude;
            _gumDistance += length;
            while (_gumDistance >= definition.gumSpacing)
            {
                _gumDistance -= definition.gumSpacing;
                // Пятно там, где мяч был в момент, когда набежал шаг, а не в конце отрезка.
                float back = length > 1e-4f ? Mathf.Clamp01(_gumDistance / length) : 0f;
                GumSpot.Drop(gumSpot, Vector3.Lerp(to, from, back));
            }
        }

        /// <summary>Попрыгунчик: мяч отскакивает от асфальта на одну и ту же высоту и летит дальше опасным.</summary>
        bool TryFloorBounce(Vector3 point, ref float remaining)
        {
            Vector3 horizontal = Flat(_velocity) * definition.floorBounceSpeedKeep;
            if (horizontal.magnitude < definition.minLiveSpeed)
                return false;
            _floorBouncesLeft--;
            float up = Mathf.Sqrt(2f * Mathf.Max(0.01f, _gravity) * definition.floorBounceHeight);
            _velocity = horizontal + Vector3.up * up;
            remaining *= definition.floorBounceSpeedKeep;
            // Отскок — новый полёт: время жизни мяча отсчитывается заново.
            if (!IsComingBack)
                _stateTime = 0f;
            GameEvents.PlaySound(SoundCue.BallWall, point);
            // Прыгающая бомба: каждый отскок — взрыв.
            if (Perks.bounceBlastRadius > 0f && Perks.bounceBlastDamage > 0 && !IsPhantom)
                BounceBlast(point);
            return true;
        }

        /// <summary>Прыгающая бомба: взрыв на отскоке от асфальта, послабее горячей картошки.</summary>
        void BounceBlast(Vector3 point)
        {
            HitAround(point, Perks.bounceBlastRadius, Perks.bounceBlastDamage, null, definition.blastKnockback * 0.7f,
                HitFlags.Area | HitFlags.Charged);
            if (blastRing)
                PoolService.Spawn(blastRing, new Vector3(point.x, 0.05f, point.z), Quaternion.identity).Play(Perks.bounceBlastRadius);
            GameEvents.PlaySound(SoundCue.Explosion, point);
            GameFeel.Shake(0.3f);
        }

        /// <summary>
        /// Доводка после рикошета от борта: если враг почти на линии отражения, мяч летит точно в него.
        /// Змейка вьётся вокруг нового направления.
        /// </summary>
        void AssistAim(Vector3 position, float maxAngle)
        {
            Vector3 horizontal = Flat(_velocity);
            float speed = horizontal.magnitude;
            if (speed < 1e-3f)
                return;
            Vector3 heading = horizontal / speed;
            Targetable best = null;
            float bestAngle = maxAngle;
            foreach (var target in Targetable.All)
            {
                if (!target.IsAlive || !Team.IsHostileTo(target.Team))
                    continue;
                Vector3 to = Flat(target.AimPoint - position);
                float sqr = to.sqrMagnitude;
                if (sqr < 0.25f || sqr > RicochetAssistRange * RicochetAssistRange)
                    continue;
                float angle = Vector3.Angle(heading, to);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = target;
                }
            }
            if (best == null)
                return;
            Vector3 want = Flat(best.AimPoint - position).normalized;
            _velocity = want * speed + Vector3.up * _velocity.y;
            _snakeHeading = want;
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

        /// <summary>Стеночка: бросок бьёт сильнее на столько.</summary>
        void AddDamage(int amount)
        {
            var stats = Stats;
            stats.Damage += amount;
            Stats = stats;
        }

        /// <summary>Глаз-алмаз: мяч плавно доворачивает к ближайшему врагу впереди.</summary>
        void Home(float dt)
        {
            Vector3 horizontal = Flat(_velocity);
            float speed = horizontal.magnitude;
            if (speed < 1e-3f)
                return;
            Vector3 heading = Perks.snakeAmplitude > 0f ? _snakeHeading : horizontal / speed;
            Vector3 position = _rb.position;
            Targetable best = null;
            float bestSqr = HomingRange * HomingRange;
            foreach (var target in Targetable.All)
            {
                if (!target.IsAlive || !Team.IsHostileTo(target.Team))
                    continue;
                Vector3 to = Flat(target.AimPoint - position);
                float sqr = to.sqrMagnitude;
                if (sqr < 0.09f || sqr > bestSqr || Vector3.Angle(heading, to) > HomingCone)
                    continue;
                bestSqr = sqr;
                best = target;
            }
            if (best == null)
                return;
            Vector3 want = Flat(best.AimPoint - position).normalized;
            Vector3 turned = Vector3.RotateTowards(heading, want, Perks.homing * Mathf.Deg2Rad * dt, 0f);
            // Змейка вьётся вокруг своего направления — доворачиваем его, а не саму скорость.
            if (Perks.snakeAmplitude > 0f)
                _snakeHeading = turned;
            else
                _velocity = turned * speed + Vector3.up * _velocity.y;
        }

        /// <summary>Йо-йо: мяч разворачивается и летит в руки бросившему, задевая всех по пути.</summary>
        bool TryStartYoyo(Collider struck, Vector3 position)
        {
            if (_yoyoBack || !CanReturn())
                return false;
            _yoyoBack = true;
            IsComingBack = true;
            _stateTime = 0f;
            _grazed.Clear();
            // Того, в кого мяч только что попал, на развороте второй раз не бьём.
            var direct = struck ? struck.GetComponentInParent<IDamageable>() : null;
            if (direct != null)
                _grazed.Add(direct);
            _rb.MovePosition(position);
            return true;
        }

        /// <summary>Обратный путь йо-йо: прямо к рукам, сквозь препятствия (мяч на нитке), задевая врагов.</summary>
        void TickYoyoBack(float dt)
        {
            _stateTime += dt;
            Vector3 position = _rb.position;
            if (!CanReturn() || _stateTime > MaxReturnTime)
            {
                _yoyoBack = false;
                IsComingBack = false;
                _elasticDone = true;
                BecomeLoose(position, Flat(_velocity) * 0.3f);
                return;
            }

            Vector3 delta = Thrower.transform.position + Vector3.up * ReturnHeight - position;
            float distance = delta.magnitude;
            float speed = Mathf.Max(Flat(_velocity).magnitude, definition.elasticReturnSpeed, definition.minLiveSpeed + 4f);
            Vector3 direction = distance > 1e-4f ? delta / distance : Vector3.zero;
            _velocity = direction * speed;
            Vector3 from = position;
            position += direction * Mathf.Min(distance, speed * dt);
            Graze(from, position, definition.radius + YoyoGrazeExtra);
            if (State != BallState.Live)
                return;
            if (TryHandBack(position))
                return;
            _rb.MovePosition(position);
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
