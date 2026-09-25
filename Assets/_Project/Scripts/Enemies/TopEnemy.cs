using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Юла. Крутится и катится к игроку по дуге, сбивает при касании. Попадание мячом запускает её, как бильярдный шар:
    /// она летит по направлению мяча, отскакивает от стен и сбивает врагов на пути, потом снова катится к игроку.
    /// Тело кинематическое: юла движется сама (как мяч — через SphereCast), чтобы отскоки были точными.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Health), typeof(Targetable))]
    public sealed class TopEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        const float Skin = 0.02f;

        [SerializeField] TopDefinition definition;
        [Tooltip("Модель: крутится и покачивается, корень едет ровно")]
        [SerializeField] Transform spinner;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        readonly Collider[] _bumpHits = new Collider[16];
        readonly List<IDamageable> _bumped = new();
        Rigidbody _rb;
        Health _health;
        Targetable _self;
        Vector3 _velocity;
        bool _launched;
        float _arcPhase;
        float _nextTouch;
        float _spin;
        float _wobblePhase;

        public TopDefinition Definition => definition;
        public bool IsLaunched => _launched;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _health.Died += OnDied;
            ApplyDefinition();
        }

        public void ApplyDefinition() => _health.Configure(definition.hitsToKill, 0f);

        public void OnSpawned()
        {
            ApplyDefinition();
            _velocity = Vector3.zero;
            _launched = false;
            _arcPhase = Random.value * Mathf.PI * 2f;
            _nextTouch = Time.time + 0.5f;
            _bumped.Clear();
        }

        public void OnDespawned() { }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            // Запущенная ударом юла катится дальше и заморозку не замечает — это уже снаряд игрока.
            if ((!GameSession.IsGameplayActive || _self.IsFrozen) && !_launched)
                return;

            if (_launched)
            {
                _velocity *= Mathf.Exp(-definition.launchDrag * dt);
                Bump();
                if (Flat(_velocity).magnitude <= definition.launchEndSpeed)
                {
                    _launched = false;
                    _bumped.Clear();
                }
            }
            else
            {
                Chase(dt);
            }
            Move(dt);
            TouchPlayer();
        }

        /// <summary>К игроку по дуге: направление качается то вправо, то влево от прямой.</summary>
        void Chase(float dt)
        {
            var target = Targetable.FindNearest(_rb.position, Team.Player);
            if (target == null)
            {
                _velocity = Vector3.MoveTowards(_velocity, Vector3.zero, definition.acceleration * dt);
                return;
            }
            _arcPhase += dt * Mathf.PI * 2f / Mathf.Max(0.1f, definition.arcPeriod);
            Vector3 toTarget = Flat(target.Position - _rb.position);
            Vector3 direction = toTarget.sqrMagnitude > 1e-4f ? toTarget.normalized : Vector3.forward;
            direction = Quaternion.Euler(0f, Mathf.Sin(_arcPhase) * definition.arcAngle, 0f) * direction;
            float speed = definition.moveSpeed * GumSpot.EnemyMoveMultiplierAt(_rb.position) * GroundZone.MoveMultiplierAt(_rb.position);
            _velocity = Vector3.MoveTowards(_velocity, direction * speed, definition.acceleration * dt);
        }

        /// <summary>Шаг с отскоками от стен (в разгоне) или скольжением вдоль них (когда катится сама).</summary>
        void Move(float dt)
        {
            Vector3 position = _rb.position;
            float remaining = Flat(_velocity).magnitude * dt;
            for (int i = 0; i < 3 && remaining > 1e-4f; i++)
            {
                Vector3 direction = Flat(_velocity).normalized;
                Vector3 origin = position + Vector3.up * definition.radius;
                if (!Physics.SphereCast(origin, definition.radius * 0.9f, direction, out RaycastHit hit, remaining + Skin,
                        Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                {
                    position += direction * remaining;
                    break;
                }
                float travel = Mathf.Max(0f, hit.distance - Skin);
                position += direction * travel;
                remaining -= travel;
                Vector3 normal = Flat(hit.normal).normalized;
                if (normal.sqrMagnitude < 1e-4f)
                    break;
                if (_launched)
                {
                    _velocity = Vector3.Reflect(_velocity, normal) * definition.wallBounceKeep;
                    GameEvents.PlaySound(SoundCue.BallWall, hit.point);
                }
                else
                {
                    _velocity -= normal * Vector3.Dot(_velocity, normal);
                }
            }
            position.y = 0f;
            _rb.MovePosition(position);
        }

        /// <summary>Разогнанная юла сбивает врагов на пути — каждого по разу за разгон.</summary>
        void Bump()
        {
            Vector3 center = _rb.position + Vector3.up * definition.radius;
            int count = Physics.OverlapSphereNonAlloc(center, definition.radius + 0.35f, _bumpHits, Layers.EnemyMask,
                QueryTriggerInteraction.Ignore);
            Vector3 forward = Flat(_velocity).normalized;
            for (int i = 0; i < count; i++)
            {
                var other = _bumpHits[i];
                if (other.attachedRigidbody == _rb)
                    continue;
                var target = other.GetComponentInParent<IDamageable>();
                if (target == null || ReferenceEquals(target, this) || _bumped.Contains(target))
                    continue;
                _bumped.Add(target);
                Vector3 away = Flat(other.transform.position - center);
                Vector3 push = (forward + (away.sqrMagnitude > 1e-4f ? away.normalized * 0.6f : Vector3.zero)).normalized;
                target.ApplyHit(new HitInfo
                {
                    Damage = definition.bumpDamage,
                    Point = other.ClosestPoint(center),
                    Direction = push,
                    Force = definition.bumpKnockback,
                    SourceTeam = Team.Player,
                    Source = gameObject,
                    Flags = HitFlags.Charged,
                });
            }
        }

        void TouchPlayer()
        {
            if (Time.time < _nextTouch || !GameSession.IsGameplayActive)
                return;
            var target = Targetable.FindNearest(_rb.position, Team.Player, definition.touchDistance);
            if (target == null || !target.TryGetComponent(out IDamageable damageable))
                return;
            _nextTouch = Time.time + definition.touchCooldown;
            Vector3 away = Flat(target.Position - _rb.position);
            damageable.ApplyHit(new HitInfo
            {
                Damage = definition.damage,
                Point = target.AimPoint,
                Direction = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward,
                Force = definition.knockback,
                SourceTeam = Team.Enemy,
                Source = gameObject,
                Flags = HitFlags.Melee,
            });
            // Отскочила от игрока — не липнет к нему.
            _velocity = -away.normalized * definition.moveSpeed;
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
                Direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward,
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
            GameFeel.Shake(strong ? 0.2f : 0.1f);
            if (hitFlash)
                hitFlash.Flash(Color.white, 0.1f);
            GameEvents.PlaySound(strong ? SoundCue.EnemyHitStrong : SoundCue.EnemyHit, hit.Point);
            _health.TryDamage(hit);
            // Бильярд: удар от игрока запускает юлу; толчок от других врагов — только отбрасывает.
            if (!_health.IsDead && hit.SourceTeam == Team.Player)
            {
                Vector3 direction = Flat(hit.Direction);
                if (direction.sqrMagnitude > 1e-4f)
                {
                    _velocity = direction.normalized * definition.launchSpeed;
                    _launched = true;
                    _bumped.Clear();
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
                    debris.Burst(hit.Direction, definition.debrisForce + hit.Force * 0.2f, _velocity * 0.3f);
            }
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.SoldierPop, transform.position);
            GameFeel.Shake(0.25f);
            PoolService.Despawn(gameObject);
        }

        // ---------- Вид ----------

        void Update()
        {
            if (!spinner)
                return;
            float dt = Time.deltaTime;
            float speed01 = Mathf.Clamp01(Flat(_velocity).magnitude / Mathf.Max(0.1f, definition.launchSpeed));
            _spin = (_spin + definition.spinSpeed * (1f + speed01) * dt) % 360f;
            _wobblePhase += dt * 5f;
            // Ось вращения медленно ходит по кругу — юла «гуляет».
            Vector3 tiltAxis = new Vector3(Mathf.Cos(_wobblePhase), 0f, Mathf.Sin(_wobblePhase));
            float tilt = definition.wobbleAngle * (_launched ? 2f : 1f);
            spinner.localRotation = Quaternion.AngleAxis(tilt, tiltAxis) * Quaternion.Euler(0f, _spin, 0f);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
