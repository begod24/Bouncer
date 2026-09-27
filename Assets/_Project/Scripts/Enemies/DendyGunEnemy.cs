using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Пистолет от «Денди». Скачет на рукоятке, держится на дистанции, потом наводит лазер: тонкий красный луч
    /// и полоса на земле ведут игрока, за мгновение до выстрела замирают — и выстрел бьёт вдоль полосы.
    /// Кто успел уйти с линии — цел. Поймать нечего: это свет, а не мяч. Попадание сбивает прицел.
    /// Кинематический, без NavMesh-агента: точки прыжков берутся с NavMesh.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Health), typeof(Targetable))]
    public sealed class DendyGunEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        public enum State
        {
            Hop,
            Aim,
            Recoil,
            Stagger,
        }

        const float FlashTime = 0.12f;

        [SerializeField] DendyGunDefinition definition;
        [Tooltip("Дуло: отсюда лазер и выстрел (DendyGun_Muzzle)")]
        [SerializeField] Transform muzzle;
        [Tooltip("Тонкий луч в воздухе; при выстреле становится толстым и ярким")]
        [SerializeField] LineRenderer laser;
        [Tooltip("Полоса на земле вдоль выстрела")]
        [SerializeField] GroundMarker lineMarkerPrefab;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        [Header("Вид")]
        [Tooltip("Модель: прыгает, сплющивается, кренится от отдачи")]
        [SerializeField] Transform visual;
        [SerializeField] Transform trigger;
        [SerializeField] float laserWidth = 0.035f;
        [SerializeField] float beamWidth = 0.22f;
        [SerializeField] Color laserColor = new(1f, 0.15f, 0.1f, 0.85f);
        [SerializeField] Color beamColor = new(1f, 0.95f, 0.75f, 1f);

        Rigidbody _rb;
        Health _health;
        Targetable _self;
        State _state;
        float _stateTime;
        float _nextAim;
        Vector3 _hopFrom;
        Vector3 _hopTo;
        bool _hopping;
        float _hopTime;
        Vector3 _aimDirection;
        float _flashUntil;
        Vector3 _beamEnd;
        GroundMarker _marker;
        Vector3 _knockback;
        float _squash;

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
            _hopping = false;
            _nextAim = Time.time + definition.firstAimDelay;
            _flashUntil = 0f;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _rb.position = transform.position = hit.position;
            SetLaser(false);
            Enter(State.Hop);
        }

        public void OnDespawned()
        {
            HideMarker();
            SetLaser(false);
        }

        // ---------- Мозги ----------

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (_self.IsFrozen)
                return;
            _stateTime += dt;
            var target = Targetable.FindNearest(_rb.position, Team.Player);
            bool hasTarget = target != null && target.IsAlive && GameSession.IsGameplayActive;
            Vector3 position = _rb.position;
            if (_knockback.sqrMagnitude > 0.01f)
            {
                if (NavMesh.SamplePosition(position + _knockback * dt, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
                    position = hit.position;
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-8f * dt));
            }

            switch (_state)
            {
                case State.Hop:
                    if (hasTarget)
                        Face(target.Position - position, dt);
                    position = StepHop(position, dt, hasTarget ? target : null, 1f);
                    if (!_hopping && hasTarget && Time.time >= _nextAim && HasLineOfSight(target))
                    {
                        _aimDirection = Flat(target.Position - position).normalized;
                        GameEvents.PlaySound(SoundCue.SpawnWarning, position);
                        Enter(State.Aim);
                    }
                    break;

                case State.Aim:
                    if (hasTarget && _stateTime < definition.trackTime)
                    {
                        Vector3 to = Flat(target.Position - position);
                        if (to.sqrMagnitude > 0.01f)
                            _aimDirection = Vector3.RotateTowards(_aimDirection, to.normalized, 3.5f * dt, 0f);
                    }
                    Face(_aimDirection, dt * 3f);
                    if (_stateTime >= definition.aimTime)
                    {
                        Fire(position);
                        _hopFrom = position;
                        _hopTo = Land(position - _aimDirection * definition.recoilDistance);
                        _hopping = true;
                        _hopTime = 0f;
                        Enter(State.Recoil);
                    }
                    break;

                case State.Recoil:
                    position = StepHop(position, dt, null, 0.6f);
                    if (!_hopping)
                    {
                        _nextAim = Time.time + definition.aimCooldown;
                        Enter(State.Hop);
                    }
                    break;

                case State.Stagger:
                    if (_stateTime >= definition.staggerTime)
                        Enter(State.Hop);
                    break;
            }
            _rb.MovePosition(position);
        }

        /// <summary>Прыжок за прыжком: к нужной дистанции от игрока (recoil — просто долететь начатый).</summary>
        Vector3 StepHop(Vector3 position, float dt, Targetable target, float heightScale)
        {
            if (!_hopping)
            {
                if (target == null || _stateTime < definition.hopPause)
                    return position;
                Vector3 away = Flat(position - target.Position);
                float distance = away.magnitude;
                away = distance > 0.1f ? away / distance : Vector3.back;
                Vector3 goal = distance < definition.minDistance ? position + away * definition.hopDistance
                    : distance > definition.preferredDistance + 1.5f ? position - away * definition.hopDistance
                    : position + Vector3.Cross(Vector3.up, away) * (definition.hopDistance * (Random.value < 0.5f ? -1f : 1f));
                _hopFrom = position;
                _hopTo = Land(goal);
                _hopping = true;
                _hopTime = 0f;
            }
            _hopTime += dt;
            float t = Mathf.Clamp01(_hopTime / Mathf.Max(0.05f, definition.hopTime));
            Vector3 p = Vector3.Lerp(_hopFrom, _hopTo, t) + Vector3.up * (4f * definition.hopHeight * heightScale * t * (1f - t));
            if (t >= 1f)
            {
                _hopping = false;
                _stateTime = 0f;
                _squash = 1f;
                p = _hopTo;
            }
            return p;
        }

        Vector3 Land(Vector3 goal) =>
            NavMesh.SamplePosition(goal, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) ? hit.position : _rb.position;

        bool HasLineOfSight(Targetable target) =>
            !Physics.Linecast(MuzzlePosition, target.AimPoint, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);

        Vector3 MuzzlePosition => muzzle ? muzzle.position : transform.position + Vector3.up * 0.8f;

        float ShotLength(Vector3 from)
        {
            Vector3 dir = new Vector3(_aimDirection.x, 0f, _aimDirection.z);
            return Physics.Raycast(from, dir, out RaycastHit hit, definition.shotRange, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore)
                ? hit.distance
                : definition.shotRange;
        }

        void Fire(Vector3 position)
        {
            HideMarker();
            Vector3 from = MuzzlePosition;
            float length = ShotLength(from);
            _beamEnd = from + _aimDirection * length;
            _flashUntil = Time.time + FlashTime;
            GameEvents.PlaySound(SoundCue.ZapperShot, from);
            GameFeel.Shake(0.12f);
            foreach (var t in Targetable.All)
            {
                if (t.Team != Team.Player || !t.IsAlive)
                    continue;
                Vector3 rel = Flat(t.Position - from);
                float along = Vector3.Dot(rel, _aimDirection);
                if (along < 0f || along > length)
                    continue;
                float side = (rel - _aimDirection * along).magnitude;
                if (side > definition.shotWidth || !t.TryGetComponent(out IDamageable damageable))
                    continue;
                damageable.ApplyHit(new HitInfo
                {
                    Damage = definition.damage,
                    Point = t.AimPoint,
                    Direction = _aimDirection,
                    Force = definition.knockback,
                    SourceTeam = Team.Enemy,
                    Source = gameObject,
                    Flags = HitFlags.None,
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

        void SetLaser(bool on)
        {
            if (laser && laser.enabled != on)
                laser.enabled = on;
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
                // Сбили прицел.
                if (_state == State.Aim)
                {
                    HideMarker();
                    _nextAim = Time.time + definition.aimCooldown * 0.5f;
                }
                if (_state != State.Recoil)
                {
                    _hopping = false;
                    Enter(State.Stagger);
                }
            }
            return true;
        }

        void OnDied(HitInfo hit)
        {
            HideMarker();
            SetLaser(false);
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
            float dt = Time.deltaTime;
            bool aiming = _state == State.Aim && !_self.IsFrozen;
            bool flashing = Time.time < _flashUntil;

            if (laser)
            {
                SetLaser(aiming || flashing);
                if (aiming || flashing)
                {
                    Vector3 from = MuzzlePosition;
                    Vector3 to = flashing ? _beamEnd : from + _aimDirection * ShotLength(from);
                    laser.positionCount = 2;
                    laser.SetPosition(0, from);
                    laser.SetPosition(1, to);
                    float pulse = aiming ? 0.6f + 0.4f * Mathf.Sin(Time.time * 30f) : 1f;
                    laser.widthMultiplier = flashing ? beamWidth : laserWidth;
                    var c = flashing ? beamColor : new Color(laserColor.r, laserColor.g, laserColor.b, laserColor.a * pulse);
                    laser.startColor = laser.endColor = c;
                }
            }
            if (aiming && _stateTime >= definition.trackTime && _marker == null && lineMarkerPrefab)
            {
                // Прицел замер: полоса на земле показывает, куда ударит.
                Vector3 from = MuzzlePosition;
                _marker = PoolService.Spawn(lineMarkerPrefab, from, Quaternion.identity);
                _marker.ShowLine(from, from + _aimDirection * ShotLength(from), definition.aimTime - definition.trackTime + 0.1f);
            }

            _squash = Mathf.MoveTowards(_squash, 0f, dt * 6f);
            if (visual)
            {
                float recoil = _state == State.Recoil ? Mathf.Sin(Mathf.Clamp01(_stateTime / 0.25f) * Mathf.PI) * -25f : 0f;
                float wobble = _state == State.Stagger ? Mathf.Sin(_stateTime * 35f) * 12f : 0f;
                visual.localRotation = Quaternion.Euler(recoil, 0f, wobble);
                visual.localScale = new Vector3(1f + 0.1f * _squash, 1f - 0.18f * _squash, 1f + 0.1f * _squash);
            }
            if (trigger)
                trigger.localRotation = Quaternion.Euler(flashing ? -25f : 0f, 0f, 0f);
        }

        void Enter(State state)
        {
            _state = state;
            _stateTime = 0f;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
