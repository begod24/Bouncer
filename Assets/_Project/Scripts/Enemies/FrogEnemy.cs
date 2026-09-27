using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Заводная лягушка — прыгун. Садится (на земле уже виден круг, куда она упадёт), прыгает дугой к игроку,
    /// приземление бьёт по кругу. После нескольких прыжков завод кончается — сидит и заводится (ключ крутится
    /// назад): окно для бросков. Мяч в прыжке сбивает её — падает на спину без удара. Кинематическая, без NavMesh-агента:
    /// точки приземления берутся с NavMesh, поэтому за арену не выпрыгнет.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Health), typeof(Targetable))]
    public sealed class FrogEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        public enum State
        {
            Sit,
            Crouch,
            Air,
            Rewind,
            Fallen,
            Stagger,
        }

        const float FallTime = 0.25f;

        [SerializeField] FrogDefinition definition;
        [SerializeField] GroundMarker markerPrefab;
        [SerializeField] ExpandingRing ringPrefab;
        [SerializeField] ParticleBurst landingDust;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        [Header("Вид")]
        [Tooltip("Корень модели: сплющивается и тянется")]
        [SerializeField] Transform visual;
        [SerializeField] Transform key;
        [SerializeField] Transform legL;
        [SerializeField] Transform legR;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;

        Rigidbody _rb;
        Health _health;
        Targetable _self;
        State _state;
        float _stateTime;
        int _jumpsLeft;
        Vector3 _jumpFrom;
        Vector3 _jumpTo;
        Vector3 _fallFrom;
        Vector3 _knockback;
        Vector3 _lastHitDirection = Vector3.back;
        GroundMarker _marker;
        float _squash;
        float _keyAngle;

        public State CurrentState => _state;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _rb.isKinematic = true;
            _health.Died += OnDied;
            ApplyDefinition();
        }

        public void ApplyDefinition() => _health.Configure(EnemyScaling.Hits(definition.hitsToKill), 0f);

        public void OnSpawned()
        {
            ApplyDefinition();
            _knockback = Vector3.zero;
            _jumpsLeft = definition.jumpsPerWind;
            _squash = 0f;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _rb.position = transform.position = hit.position;
            Enter(State.Sit);
            _stateTime = -0.6f;
        }

        public void OnDespawned() => HideMarker();

        void OnDisable() => HideMarker();

        // ---------- Мозги ----------

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (_self.IsFrozen && _state != State.Air)
                return;
            _stateTime += dt;
            var target = Targetable.FindNearest(_rb.position, Team.Player);
            bool hasTarget = target != null && target.IsAlive && GameSession.IsGameplayActive;
            Vector3 position = _rb.position;

            if (_knockback.sqrMagnitude > 0.01f && _state != State.Air)
            {
                Vector3 step = _knockback * dt;
                if (NavMesh.SamplePosition(position + step, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
                    position = hit.position;
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-8f * dt));
            }

            switch (_state)
            {
                case State.Sit:
                    if (hasTarget)
                        Face(target.Position - position, dt);
                    if (_stateTime >= definition.sitTime && hasTarget)
                    {
                        if (_jumpsLeft <= 0)
                        {
                            GameEvents.PlaySound(SoundCue.WindUp, position);   // тр-р-р: заводится
                            Enter(State.Rewind);
                        }
                        else if (PickLanding(target, position))
                        {
                            Enter(State.Crouch);
                        }
                    }
                    break;

                case State.Crouch:
                    Face(_jumpTo - position, dt * 2f);
                    if (_stateTime >= definition.crouchTime)
                    {
                        _jumpFrom = position;
                        _jumpsLeft--;
                        GameEvents.PlaySound(SoundCue.PupsikSqueak, position);
                        Enter(State.Air);
                    }
                    break;

                case State.Air:
                    float t = Mathf.Clamp01(_stateTime / Mathf.Max(0.05f, definition.jumpTime));
                    position = Vector3.Lerp(_jumpFrom, _jumpTo, t) + Vector3.up * (4f * definition.jumpHeight * t * (1f - t));
                    if (t >= 1f)
                    {
                        position = _jumpTo;
                        Land(position);
                        Enter(State.Sit);
                    }
                    break;

                case State.Rewind:
                    if (_stateTime >= definition.rewindTime)
                    {
                        _jumpsLeft = definition.jumpsPerWind;
                        Enter(State.Sit);
                    }
                    break;

                case State.Fallen:
                    // Сбили в прыжке: падает вниз, где была, и лежит на спине.
                    float f = Mathf.Clamp01(_stateTime / FallTime);
                    Vector3 ground = new(_fallFrom.x, _jumpTo.y, _fallFrom.z);
                    position = Vector3.Lerp(_fallFrom, ground, f * f);
                    if (_stateTime >= definition.fallenTime)
                    {
                        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                            position = hit.position;
                        Enter(State.Sit);
                    }
                    break;

                case State.Stagger:
                    if (_stateTime >= definition.staggerTime)
                        Enter(State.Sit);
                    break;
            }
            _rb.MovePosition(position);
        }

        /// <summary>Куда прыгнуть: к игроку (с упреждением), не короче и не длиннее прыжка, на NavMesh.</summary>
        bool PickLanding(Targetable target, Vector3 position)
        {
            Vector3 aim = target.Position + Flat(target.Velocity) * ((definition.crouchTime + definition.jumpTime) * definition.lead);
            Vector3 delta = Flat(aim - position);
            float distance = delta.magnitude;
            if (distance < 0.3f)
                return false;
            float jump = distance < definition.jumpRangeMin ? distance : Mathf.Min(distance, definition.jumpRangeMax);
            Vector3 landing = position + delta / distance * jump;
            if (!NavMesh.SamplePosition(landing, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                return false;
            _jumpTo = hit.position;
            if (markerPrefab)
            {
                _marker = PoolService.Spawn(markerPrefab, _jumpTo, Quaternion.identity);
                _marker.ShowCircle(_jumpTo, definition.landRadius, definition.crouchTime + definition.jumpTime);
            }
            return true;
        }

        void Land(Vector3 position)
        {
            HideMarker();
            _squash = 1f;
            GameEvents.PlaySound(SoundCue.AreaThud, position);
            GameFeel.Shake(0.2f);
            if (ringPrefab)
                PoolService.Spawn(ringPrefab, position + Vector3.up * 0.05f, Quaternion.identity).Play(definition.landRadius);
            if (landingDust)
                PoolService.Spawn(landingDust, position + Vector3.up * 0.1f, Quaternion.identity).Play(0.6f);
            foreach (var t in Targetable.All)
            {
                if (t.Team != Team.Player || !t.IsAlive)
                    continue;
                Vector3 away = Flat(t.Position - position);
                if (away.sqrMagnitude > definition.landRadius * definition.landRadius || !t.TryGetComponent(out IDamageable damageable))
                    continue;
                damageable.ApplyHit(new HitInfo
                {
                    Damage = definition.landDamage,
                    Point = t.AimPoint,
                    Direction = away.sqrMagnitude > 1e-4f ? away.normalized : transform.forward,
                    Force = definition.landKnockback,
                    SourceTeam = Team.Enemy,
                    Source = gameObject,
                    Flags = HitFlags.Area,
                });
            }
        }

        void Face(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f)
                return;
            _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, Quaternion.LookRotation(direction), definition.turnSpeed * dt));
        }

        void HideMarker()
        {
            if (_marker && _marker.isActiveAndEnabled)
                _marker.Hide();
            _marker = null;
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
            _lastHitDirection = hit.Direction;
            _health.TryDamage(hit);
            if (_health.IsDead)
                return true;

            if (_state == State.Air)
            {
                // Сбили в прыжке: удара не будет.
                HideMarker();
                _fallFrom = _rb.position;
                Enter(State.Fallen);
            }
            else if (_state != State.Fallen)
            {
                HideMarker();
                _knockback += Flat(hit.Direction) * (hit.Force * definition.knockbackScale);
                if (_state != State.Rewind)
                    Enter(State.Stagger);
            }
            return true;
        }

        void OnDied(HitInfo hit)
        {
            HideMarker();
            if (debrisPrefab)
            {
                var debris = PoolService.Spawn(debrisPrefab, transform.position, transform.rotation).GetComponent<Debris>();
                if (debris)
                    debris.Burst(hit.Direction, definition.debrisForce + hit.Force * 0.25f, Vector3.zero);
            }
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.SoldierPop, transform.position);
            GameFeel.Shake(0.25f);
            PoolService.Despawn(gameObject);
        }

        // ---------- Вид ----------

        void LateUpdate()
        {
            if (!visual)
                return;
            float dt = Time.deltaTime;
            _squash = Mathf.MoveTowards(_squash, 0f, dt * 5f);
            float crouch = _state == State.Crouch ? Mathf.Clamp01(_stateTime / Mathf.Max(0.05f, definition.crouchTime)) : 0f;
            float stretch = _state == State.Air ? Mathf.Sin(Mathf.Clamp01(_stateTime / definition.jumpTime) * Mathf.PI) : 0f;
            float squashY = 1f - 0.22f * crouch - 0.25f * _squash + 0.15f * stretch;
            float squashXZ = 1f + 0.12f * crouch + 0.15f * _squash - 0.06f * stretch;
            visual.localScale = new Vector3(squashXZ, squashY, squashXZ);

            // Лежит на спине — перевёрнута.
            Quaternion lie = _state == State.Fallen
                ? Quaternion.AngleAxis(170f * Mathf.Clamp01(_stateTime / 0.2f), Vector3.forward)
                : Quaternion.identity;
            visual.localRotation = lie;

            // Задние лапы: в прыжке вытянуты назад, присев — поджаты. Передние — вперёд в полёте.
            float legs = 55f * stretch - 15f * crouch;
            if (legL)
                legL.localRotation = Quaternion.Euler(legs, 0f, 0f);
            if (legR)
                legR.localRotation = Quaternion.Euler(legs, 0f, 0f);
            float arms = -40f * stretch;
            if (armL)
                armL.localRotation = Quaternion.Euler(arms, 0f, 0f);
            if (armR)
                armR.localRotation = Quaternion.Euler(arms, 0f, 0f);

            // Ключ: крутится, пока есть завод; заводясь — быстро в обратную сторону.
            float keySpeed = _state == State.Rewind ? -900f : _jumpsLeft > 0 ? 160f * _jumpsLeft : 0f;
            if (_self.IsFrozen)
                keySpeed = 0f;
            _keyAngle += keySpeed * dt;
            if (key)
                key.localRotation = Quaternion.Euler(0f, 0f, _keyAngle);
        }

        void Enter(State state)
        {
            _state = state;
            _stateTime = 0f;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
