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
    }

    /// <summary>
    /// Мяч. В полёте (Live/Popped) движется сам через SphereCast — так рикошеты точные
    /// и предсказуемые, мяч не пролетает сквозь тонкие стены. Лежащий мяч — обычная физика.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class Ball : MonoBehaviour, IPoolable
    {
        const float Skin = 0.01f;
        const int MaxSweepIterations = 4;
        const float KillY = -5f;

        static readonly List<Ball> s_active = new();
        static readonly List<Ball> s_loose = new();

        public static IReadOnlyList<Ball> Active => s_active;
        public static int LooseCount => s_loose.Count;

        [SerializeField] BallDefinition definition;
        [Tooltip("Дочерний меш — масштабируется под радиус из определения")]
        [SerializeField] Transform mesh;

        readonly RaycastHit[] _hits = new RaycastHit[16];
        readonly List<Collider> _ignored = new(4);
        Rigidbody _rb;
        SphereCollider _collider;
        Vector3 _velocity;
        float _gravity;
        float _stateTime;

        public BallDefinition Definition => definition;
        public BallState State { get; private set; }
        /// <summary>Команда бросившего. У лежащего мяча — Neutral.</summary>
        public Team Team { get; private set; }
        public GameObject Thrower { get; private set; }
        public ThrowStats Stats { get; private set; }
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
            Ricochets = 0;
            _velocity = Vector3.zero;
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
            Stats = t.Stats;
            Ricochets = 0;

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

        /// <summary>Положить мяч на арену (например, если игроку некуда его взять).</summary>
        public void Drop(Vector3 position, Vector3 velocity) => BecomeLoose(position, velocity);

        /// <summary>Мяч забрали — вернуть в пул.</summary>
        public void Consume() => PoolService.Despawn(gameObject);

        public bool IsCatchableBy(Team catcher) =>
            State == BallState.Popped || (State == BallState.Live && this.Team.IsHostileTo(catcher));

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
                case BallState.Loose:
                    if (_rb.position.y < KillY)
                        Consume();
                    break;
            }
        }

        void TickFlight(float dt, bool live)
        {
            _stateTime += dt;
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
                        if (result == BallContactResult.PassThrough)
                        {
                            _ignored.Add(hit.collider);
                            continue;
                        }
                        // Цель забрала мяч или сама сменила ему состояние.
                        if (result == BallContactResult.Caught || State != BallState.Live)
                            return;
                        if (result == BallContactResult.Hit)
                        {
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
            if (live && _stateTime > definition.maxLiveTime)
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
                if (hit.collider == _collider || _ignored.Contains(hit.collider))
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

        void Pop(Vector3 position, Vector3 hitNormal)
        {
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
