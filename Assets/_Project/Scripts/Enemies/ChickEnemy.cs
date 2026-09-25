using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Заводной жестяной цыплёнок — камикадзе. Появившись, заводится (ключ крутится и искрит — видно, что сейчас побежит),
    /// потом бежит по прямой туда, где стоял игрок, и взрывается о стену, об игрока или через несколько секунд.
    /// Взрыв бьёт всех вокруг, врагов тоже. Сбитый мячом взрывается на месте — так можно подорвать толпу.
    /// Петушок (элитный) по дороге дважды поворачивает к игроку, и взрыв у него больше.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Health), typeof(Targetable))]
    public sealed class ChickEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        enum State
        {
            Windup,
            Run,
        }

        [SerializeField] ChickDefinition definition;
        [Tooltip("Ключ на спине: крутится, пока цыплёнок заведён")]
        [SerializeField] Transform key;
        [SerializeField] Transform legL;
        [SerializeField] Transform legR;
        [Tooltip("Модель: дрожит, пока заводится")]
        [SerializeField] Transform body;
        [Tooltip("Кольцо взрыва, из пула")]
        [SerializeField] ExpandingRing blastRing;
        [Tooltip("Вспышка, огонь, дым и искры взрыва, из пула. Сделан под радиус обычного цыплёнка")]
        [SerializeField] ParticleBurst blastEffect;
        [Tooltip("Под какой радиус взрыва сделан эффект: у петушка он крупнее во столько же раз")]
        [SerializeField] float blastEffectRadius = 2.6f;
        [Tooltip("Искры от ключа, пока цыплёнок заведён")]
        [SerializeField] ParticleSystem sparks;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        readonly Collider[] _blastHits = new Collider[24];
        readonly List<IDamageable> _damaged = new();
        Rigidbody _rb;
        Health _health;
        Targetable _self;
        State _state;
        float _stateTime;
        float _nextZigzag;
        int _zigzagsLeft;
        bool _exploded;
        Vector3 _direction;
        Quaternion _keyRest;
        Quaternion _legLRest;
        Quaternion _legRRest;
        Vector3 _bodyRest;
        float _keyAngle;
        float _phase;

        public ChickDefinition Definition => definition;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _health.Died += OnDied;
            if (key)
                _keyRest = key.localRotation;
            if (legL)
                _legLRest = legL.localRotation;
            if (legR)
                _legRRest = legR.localRotation;
            if (body)
                _bodyRest = body.localPosition;
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
            _exploded = false;
            _zigzagsLeft = definition.zigzags;
            _direction = transform.forward;
            if (sparks)
                sparks.Play(true);
            Enter(State.Windup);
        }

        public void OnDespawned() { }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (_exploded)
                return;
            if (_self.IsFrozen)
            {
                // Заморожен: стоит, завод не кончается.
                _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                return;
            }
            _stateTime += dt;
            if (_rb.position.y < -5f)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            var target = Targetable.FindNearest(_rb.position, Team.Player);
            bool active = GameSession.IsGameplayActive;

            switch (_state)
            {
                case State.Windup:
                    _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                    if (target != null)
                        Face(target.Position - _rb.position, dt);
                    if (active && _stateTime >= definition.windupTime)
                    {
                        _direction = target != null ? Flat(target.Position - _rb.position).normalized : Forward;
                        if (_direction.sqrMagnitude < 0.5f)
                            _direction = Forward;
                        _nextZigzag = definition.zigzagInterval;
                        Enter(State.Run);
                    }
                    break;

                case State.Run:
                    if (!active)
                    {
                        _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                        break;
                    }
                    // Петушок поворачивает к игроку несколько раз за разбег.
                    if (_zigzagsLeft > 0 && _stateTime >= _nextZigzag && target != null)
                    {
                        _zigzagsLeft--;
                        _nextZigzag = _stateTime + definition.zigzagInterval;
                        Vector3 toTarget = Flat(target.Position - _rb.position);
                        if (toTarget.sqrMagnitude > 0.25f)
                            _direction = toTarget.normalized;
                    }
                    Face(_direction, dt);
                    Vector3 step = _direction * definition.runSpeed;
                    _rb.linearVelocity = new Vector3(step.x, _rb.linearVelocity.y, step.z);
                    if (_stateTime >= definition.maxRunTime || HitsWall(dt) || NearPlayer(target))
                        Explode(Team.Enemy);
                    break;
            }
        }

        bool HitsWall(float dt) =>
            Physics.SphereCast(_rb.position + Vector3.up * 0.35f, 0.25f, _direction, out _, definition.runSpeed * dt + 0.15f,
                Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);

        bool NearPlayer(Targetable target)
        {
            if (target == null)
                return false;
            Vector3 delta = Flat(target.Position - _rb.position);
            return delta.sqrMagnitude <= definition.triggerDistance * definition.triggerDistance;
        }

        /// <summary>Бах: бьёт всех вокруг — игроков и врагов. source — кто виноват (выбит игроком или сам добежал).</summary>
        void Explode(Team source)
        {
            if (_exploded)
                return;
            _exploded = true;
            Vector3 center = _rb.position + Vector3.up * 0.4f;
            int count = Physics.OverlapSphereNonAlloc(center, definition.blastRadius, _blastHits, Layers.PlayerMask | Layers.EnemyMask,
                QueryTriggerInteraction.Ignore);
            _damaged.Clear();
            for (int i = 0; i < count; i++)
            {
                var other = _blastHits[i];
                if (other.attachedRigidbody == _rb)
                    continue;
                var target = other.GetComponentInParent<IDamageable>();
                if (target == null || ReferenceEquals(target, this) || _damaged.Contains(target))
                    continue;
                _damaged.Add(target);
                bool player = other.gameObject.layer == Layers.Player;
                Vector3 away = Flat(other.transform.position - center);
                target.ApplyHit(new HitInfo
                {
                    Damage = player ? definition.playerDamage : definition.enemyDamage,
                    Point = other.ClosestPoint(center),
                    Direction = away.sqrMagnitude > 1e-4f ? away.normalized : Forward,
                    Force = definition.blastKnockback,
                    SourceTeam = source,
                    Source = gameObject,
                    Flags = HitFlags.Area | HitFlags.Charged,
                });
            }
            if (blastRing)
                PoolService.Spawn(blastRing, new Vector3(center.x, 0.05f, center.z), Quaternion.identity).Play(definition.blastRadius);
            if (blastEffect)
                PoolService.Spawn(blastEffect, center, Quaternion.identity).Play(definition.blastRadius / Mathf.Max(0.1f, blastEffectRadius));
            if (sparks)
                sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            GameEvents.PlaySound(SoundCue.Explosion, center);
            GameFeel.Shake(0.6f);
            if (!_health.IsDead)
                Break(Vector3.up, 0f);
        }

        void Break(Vector3 direction, float force)
        {
            if (debrisPrefab)
            {
                var debris = PoolService.Spawn(debrisPrefab, transform.position, transform.rotation).GetComponent<Debris>();
                if (debris)
                    debris.Burst(direction, definition.debrisForce + force, Vector3.zero);
            }
            PoolService.Despawn(gameObject);
        }

        void Face(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f)
                return;
            _rb.angularVelocity = Vector3.zero;
            _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, Quaternion.LookRotation(direction), definition.turnSpeed * dt));
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
            if (_health.IsDead || _exploded)
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
            if (_health.IsDead || _exploded)
                return false;
            if (hitFlash)
                hitFlash.Flash(Color.white, 0.1f);
            GameEvents.PlaySound(SoundCue.EnemyHit, hit.Point);
            _health.TryDamage(hit);
            if (!_health.IsDead)
                _rb.AddForce(Flat(hit.Direction) * (hit.Force * definition.knockbackScale), ForceMode.VelocityChange);
            return true;
        }

        void OnDied(HitInfo hit)
        {
            if (hit.Has(HitFlags.Despawn))
            {
                // Арена пройдена: цыплёнок просто разваливается, без взрыва.
                _exploded = true;
                GameEvents.RaiseEnemyKilled(gameObject, hit);
                Break(Vector3.up, 0f);
                return;
            }
            // Выбит — взрывается на месте; виноват тот, кто его выбил (игрок получает монетки и за соседей).
            Team source = hit.SourceTeam == Team.Player ? Team.Player : Team.Enemy;
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            Explode(source);
            Break(hit.Direction, hit.Force * 0.2f);
        }

        // ---------- Вид ----------

        void Update()
        {
            float dt = Time.deltaTime;
            float spin = _state == State.Windup
                ? definition.keySpinSpeed * Mathf.Lerp(0.3f, 1.5f, Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, definition.windupTime)))
                : definition.keySpinSpeed;
            _keyAngle = (_keyAngle + spin * dt) % 360f;
            if (key)
                key.localRotation = _keyRest * Quaternion.Euler(0f, 0f, _keyAngle);
            if (body)
                body.localPosition = _state == State.Windup ? _bodyRest + (Vector3)(Random.insideUnitCircle * 0.02f) : _bodyRest;
            _phase += dt * (_state == State.Run ? 26f : 0f);
            float swing = _state == State.Run ? Mathf.Sin(_phase) * 45f : 0f;
            if (legL)
                legL.localRotation = _legLRest * Quaternion.Euler(swing, 0f, 0f);
            if (legR)
                legR.localRotation = _legRRest * Quaternion.Euler(-swing, 0f, 0f);
        }

        void Enter(State state)
        {
            _state = state;
            _stateTime = 0f;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
