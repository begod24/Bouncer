using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// «Большой плюшевый заяц» — босс детского сада. Перепрыгивает через забор (<see cref="ArenaSpotKind.BossEntrance"/>)
    /// и скачет за игроком. Прыжок с ударом: приседает (на земле круг), взлетает и бьёт приземлением по кругу, вокруг
    /// остаются облака ваты — в них игрок вязнет (<see cref="SlowCloud"/>); после приземления сидит открытый.
    /// Морковка-бумеранг летит петлёй к игроку и обратно; вблизи бьёт ушами-хлыстом. На половине жизни рвётся шов:
    /// из бока лезет вата, заяц скачет быстрее, прыгает чаще и теряет вату за собой. Держит удар (<see cref="BossArmor"/>):
    /// открыт, пока сидит после прыжка, после удара ушами и пока рвётся шов. Жизнь — на полосе босса (<see cref="BossSplit"/>).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class HareBoss : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        public enum State
        {
            Enter,
            Hop,
            Crouch,
            Air,
            Stuck,
            CarrotWindup,
            WhipWindup,
            Whip,
            Feint,
            Rip,
        }

        const float RepathInterval = 0.3f;
        const float FeintTime = 0.4f;
        const float WhipTime = 0.25f;
        const float OpenAfterWhip = 0.9f;
        const float RipTime = 1.1f;

        [SerializeField] HareDefinition definition;

        [Header("Части модели")]
        [Tooltip("Корень модели: подпрыгивает и сплющивается")]
        [SerializeField] Transform visual;
        [SerializeField] Transform body;
        [SerializeField] Transform head;
        [SerializeField] Transform earL;
        [SerializeField] Transform earR;
        [SerializeField] Transform armR;
        [SerializeField] Transform legL;
        [SerializeField] Transform legR;
        [Tooltip("Морковка в лапе: прячется, пока летит бумеранг")]
        [SerializeField] GameObject carrotInHand;
        [Tooltip("Вата из разорванного шва: видна после разрыва")]
        [SerializeField] GameObject stuffing;

        [Header("Эффекты")]
        [SerializeField] GroundMarker circleMarkerPrefab;
        [SerializeField] ExpandingRing slamRing;
        [SerializeField] ParticleBurst landingDust;
        [SerializeField] ParticleBurst ripBurst;
        [SerializeField] SlowCloud cloudPrefab;
        [SerializeField] CarrotBoomerang carrotPrefab;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        Targetable _target;
        readonly BossArmor _armor = new();
        State _state;
        float _stateTime;
        float _nextRepath;
        float _nextSlam;
        float _nextCarrot;
        float _nextWhip;
        float _nextTrailCloud;
        float _openUntil;
        bool _ripped;
        bool _feinted;
        bool _carrotOut;
        bool _whipHit;
        Vector3 _jumpFrom;
        Vector3 _jumpTo;
        float _jumpHeight;
        float _jumpTime;
        GroundMarker _marker;
        float _hopPhase;
        float _squash;
        Quaternion _headRest, _earLRest, _earRRest, _armRRest, _legLRest, _legRRest, _bodyRest;

        public HareDefinition Definition => definition;
        public State CurrentState => _state;
        /// <summary>Открыт: сидит после прыжка, только что хлестнул ушами или у него рвётся шов.</summary>
        public bool IsOpen => _state is State.Stuck or State.Rip || Time.time < _openUntil;
        float SpeedBoost => _ripped ? definition.rippedSpeedMultiplier : 1f;
        float CooldownScale => _ripped ? definition.rippedCooldownMultiplier : 1f;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _agent.updateRotation = false;
            _health.Died += OnDied;
            _health.Damaged += OnDamaged;
            _headRest = Rest(head);
            _earLRest = Rest(earL);
            _earRRest = Rest(earR);
            _armRRest = Rest(armR);
            _legLRest = Rest(legL);
            _legRRest = Rest(legR);
            _bodyRest = Rest(body);
            ApplyDefinition();
        }

        static Quaternion Rest(Transform t) => t ? t.localRotation : Quaternion.identity;

        public void ApplyDefinition()
        {
            _agent.speed = definition.hopSpeed;
            _agent.acceleration = 10f;
            _agent.stoppingDistance = 1.5f;
            _health.Configure(EnemyScaling.BossHits(definition.hitsToKill), 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _armor.Reset();
            _openUntil = 0f;
            _ripped = false;
            _feinted = false;
            _carrotOut = false;
            _target = null;
            _nextRepath = 0f;
            _nextSlam = Time.time + definition.firstSlamDelay;
            _nextCarrot = Time.time + definition.firstSlamDelay + 3f;
            _nextWhip = 0f;
            if (stuffing)
                stuffing.SetActive(false);
            if (carrotInHand)
                carrotInHand.SetActive(true);
            // Перепрыгивает через забор к отметке входа.
            var entrance = ArenaSpot.Find(ArenaSpotKind.BossEntrance);
            Vector3 landing = entrance ? entrance.Position : transform.position;
            Vector3 inward = entrance ? entrance.Inward : Vector3.back;
            if (NavMesh.SamplePosition(landing, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                landing = hit.position;
            _agent.enabled = false;
            _jumpFrom = landing - inward * 9f;
            _jumpTo = landing;
            _jumpHeight = definition.jumpHeight * 1.2f;
            _jumpTime = definition.jumpTime * 1.4f;
            transform.SetPositionAndRotation(_jumpFrom, Quaternion.LookRotation(inward));
            // Приземление бьёт, как обычный прыжок, — и так же видно заранее.
            if (circleMarkerPrefab)
            {
                _marker = PoolService.Spawn(circleMarkerPrefab, landing, Quaternion.identity);
                _marker.ShowCircle(landing, definition.slamRadius, _jumpTime);
            }
            GameEvents.PlaySound(SoundCue.SwingBat, landing);
            Enter(State.Enter);
        }

        // Агент остаётся выключенным, если заяц пропал в прыжке: OnSpawned всё равно начинает с прыжка через забор.
        public void OnDespawned() => HideMarker();

        // ---------- Мозги ----------

        void Update()
        {
            float dt = Time.deltaTime;
            _stateTime += dt;
            if (Time.time >= _nextRepath)
            {
                _nextRepath = Time.time + RepathInterval;
                _target = Targetable.FindNearest(transform.position, Team.Player);
                if (_target != null && _state == State.Hop && _agent.enabled && _agent.isOnNavMesh)
                    _agent.SetDestination(_target.Position);
            }
            bool hasTarget = _target != null && _target.IsAlive && GameSession.IsGameplayActive;
            Vector3 toTarget = hasTarget ? Flat(_target.Position - transform.position) : Vector3.zero;
            float distance = toTarget.magnitude;

            if (_state is State.Enter or State.Air)
            {
                Fly();
                return;
            }
            if (!_agent.enabled || !_agent.isOnNavMesh)
                return;
            if (_self.IsFrozen)
            {
                Halt();
                return;
            }
            if (_ripped && Time.time >= _nextTrailCloud)
            {
                _nextTrailCloud = Time.time + definition.trailCloudInterval;
                SpawnCloud(transform.position - transform.forward * 1.5f);
            }

            switch (_state)
            {
                case State.Hop:
                    _agent.isStopped = !hasTarget;
                    _agent.speed = definition.hopSpeed * SpeedBoost * _self.SpeedMultiplier;
                    if (!hasTarget)
                        break;
                    Face(toTarget, dt);
                    if (distance <= definition.whipRange && Time.time >= _nextWhip)
                    {
                        Halt();
                        Enter(State.WhipWindup);
                    }
                    else if (Time.time >= _nextSlam)
                    {
                        Halt();
                        BeginCrouch();
                    }
                    else if (!_carrotOut && Time.time >= _nextCarrot && distance > definition.whipRange + 1f && distance <= definition.carrotRange + 4f)
                    {
                        Halt();
                        Enter(State.CarrotWindup);
                    }
                    break;

                case State.Crouch:
                    if (hasTarget && _stateTime < definition.crouchTime * 0.6f)
                        Face(toTarget, dt * 2f);
                    if (_stateTime >= definition.crouchTime)
                    {
                        // Финт: присел — и не прыгнул; через миг прыгнет по-настоящему.
                        if (!_feinted && Random.value < Danger.FeintChance)
                        {
                            _feinted = true;
                            HideMarker();
                            Enter(State.Feint);
                            break;
                        }
                        _feinted = false;
                        Jump();
                    }
                    break;

                case State.Feint:
                    if (_stateTime >= FeintTime)
                        BeginCrouch();
                    break;

                case State.Stuck:
                    if (_stateTime >= definition.stuckTime)
                        Enter(State.Hop);
                    break;

                case State.CarrotWindup:
                    if (hasTarget)
                        Face(toTarget, dt * 2f);
                    if (_stateTime >= definition.carrotWindup)
                    {
                        if (hasTarget)
                            ThrowCarrot(_target);
                        _nextCarrot = Time.time + definition.carrotCooldown * CooldownScale;
                        Enter(State.Hop);
                    }
                    break;

                case State.WhipWindup:
                    if (hasTarget)
                        Face(toTarget, dt * 1.5f);
                    if (_stateTime >= definition.whipWindup)
                    {
                        _whipHit = false;
                        GameEvents.PlaySound(SoundCue.SwingBat, transform.position);
                        Enter(State.Whip);
                    }
                    break;

                case State.Whip:
                    TryWhipHit();
                    if (_stateTime >= WhipTime)
                    {
                        _nextWhip = Time.time + definition.whipCooldown * CooldownScale;
                        _openUntil = Time.time + OpenAfterWhip;
                        Enter(State.Hop);
                    }
                    break;

                case State.Rip:
                    if (_stateTime >= RipTime)
                        Enter(State.Hop);
                    break;
            }
        }

        void BeginCrouch()
        {
            if (_target == null)
            {
                Enter(State.Hop);
                return;
            }
            Vector3 aim = _target.Position + Flat(_target.Velocity) * ((definition.crouchTime + definition.jumpTime) * 0.5f);
            _jumpTo = NavMesh.SamplePosition(aim, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : _target.Position;
            if (circleMarkerPrefab)
            {
                _marker = PoolService.Spawn(circleMarkerPrefab, _jumpTo, Quaternion.identity);
                _marker.ShowCircle(_jumpTo, definition.slamRadius, definition.crouchTime + definition.jumpTime);
            }
            GameEvents.PlaySound(SoundCue.SpawnWarning, transform.position);
            Enter(State.Crouch);
        }

        void Jump()
        {
            _jumpFrom = transform.position;
            _jumpHeight = definition.jumpHeight;
            _jumpTime = definition.jumpTime;
            _agent.enabled = false;
            GameEvents.PlaySound(SoundCue.SwingBat, transform.position);
            Enter(State.Air);
        }

        void Fly()
        {
            float t = Mathf.Clamp01(_stateTime / Mathf.Max(0.1f, _jumpTime));
            Vector3 position = Vector3.Lerp(_jumpFrom, _jumpTo, t) + Vector3.up * (4f * _jumpHeight * t * (1f - t));
            transform.position = position;
            Vector3 dir = Flat(_jumpTo - _jumpFrom);
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 360f * Time.deltaTime);
            if (t < 1f)
                return;
            _agent.enabled = true;
            _agent.Warp(_jumpTo);
            Land(_jumpTo);
            Enter(State.Stuck);
            _nextSlam = Time.time + definition.slamCooldown * CooldownScale;
        }

        void Land(Vector3 center)
        {
            HideMarker();
            _squash = 1f;
            GameEvents.PlaySound(SoundCue.PlushThump, center);
            GameEvents.PlaySound(SoundCue.AreaThud, center);
            GameFeel.Shake(0.7f);
            if (slamRing)
                PoolService.Spawn(slamRing, center + Vector3.up * 0.05f, Quaternion.identity).Play(definition.slamRadius);
            if (landingDust)
                PoolService.Spawn(landingDust, center + Vector3.up * 0.1f, Quaternion.identity).Play(2f);
            foreach (var t in Targetable.All)
            {
                if (t.Team != Team.Player || !t.IsAlive)
                    continue;
                Vector3 away = Flat(t.Position - center);
                if (away.sqrMagnitude > definition.slamRadius * definition.slamRadius || !t.TryGetComponent(out IDamageable damageable))
                    continue;
                damageable.ApplyHit(new HitInfo
                {
                    Damage = definition.slamDamage,
                    Point = t.AimPoint,
                    Direction = away.sqrMagnitude > 1e-4f ? away.normalized : transform.forward,
                    Force = definition.slamKnockback,
                    SourceTeam = Team.Enemy,
                    Source = gameObject,
                    Flags = HitFlags.Area,
                });
            }
            int clouds = _ripped ? definition.cloudsPerSlamRipped : definition.cloudsPerSlam;
            for (int i = 0; i < clouds; i++)
            {
                float angle = 360f * i / Mathf.Max(1, clouds) + Random.Range(-25f, 25f);
                Vector3 p = center + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * (definition.slamRadius * Random.Range(0.55f, 0.9f));
                SpawnCloud(p);
            }
        }

        void SpawnCloud(Vector3 position)
        {
            if (cloudPrefab == null)
                return;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                position = hit.position;
            PoolService.Spawn(cloudPrefab, position + Vector3.up * 0.05f, Quaternion.identity)
                .Play(definition.cloudRadius, definition.cloudLife, definition.cloudSlow);
        }

        void ThrowCarrot(Targetable target)
        {
            if (carrotPrefab == null || _carrotOut)
                return;
            Vector3 start = carrotInHand ? carrotInHand.transform.position : transform.position + Vector3.up * 1.5f;
            start.y = 1.1f;
            Vector3 to = Flat(target.Position - transform.position);
            float range = Mathf.Min(definition.carrotRange, to.magnitude + 3f);
            Vector3 far = transform.position + (to.sqrMagnitude > 0.01f ? to.normalized : transform.forward) * range;
            far.y = 1.1f;
            _carrotOut = true;
            if (carrotInHand)
                carrotInHand.SetActive(false);
            GameEvents.PlaySound(SoundCue.ThrowCharged, start);
            var carrot = PoolService.Spawn(carrotPrefab, start, Quaternion.identity);
            carrot.Throw(transform, start, far, () => transform.position + Vector3.up * 1.1f, definition.carrotFlightTime,
                definition.carrotRadius, definition.carrotDamage, definition.carrotKnockback, OnCarrotBack);
        }

        void OnCarrotBack()
        {
            _carrotOut = false;
            if (carrotInHand)
                carrotInHand.SetActive(true);
        }

        void TryWhipHit()
        {
            if (_whipHit || _target == null || !_target.IsAlive)
                return;
            Vector3 delta = Flat(_target.Position - transform.position);
            if (delta.sqrMagnitude > definition.whipRange * definition.whipRange
                || Vector3.Angle(transform.forward, delta) > definition.whipHalfAngle
                || !_target.TryGetComponent(out IDamageable damageable))
                return;
            _whipHit = true;
            damageable.ApplyHit(new HitInfo
            {
                Damage = definition.whipDamage,
                Point = _target.AimPoint,
                Direction = delta.normalized,
                Force = definition.whipKnockback,
                SourceTeam = Team.Enemy,
                Source = gameObject,
                Flags = HitFlags.Melee,
            });
        }

        void Halt()
        {
            if (_agent.enabled && _agent.isOnNavMesh && !_agent.isStopped)
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
            bool open = IsOpen;
            bool strong = hit.Has(HitFlags.Charged);
            GameFeel.Shake(strong ? 0.3f : 0.12f);
            BossArmor.Flash(hitFlash, open);
            GameEvents.PlaySound(strong || open ? SoundCue.EnemyHitStrong : SoundCue.EnemyHit, hit.Point);
            var counted = hit;
            counted.Damage = _armor.Take(hit.Damage, open);
            if (counted.Damage > 0)
                _health.TryDamage(counted);
            return true;
        }

        /// <summary>На половине жизни рвётся шов: вата наружу, заяц злее.</summary>
        void OnDamaged(HitInfo hit)
        {
            if (_ripped || _health.IsDead || _health.Current > _health.Max * definition.ripAt)
                return;
            _ripped = true;
            _nextTrailCloud = Time.time + definition.trailCloudInterval;
            if (stuffing)
                stuffing.SetActive(true);
            if (ripBurst)
                PoolService.Spawn(ripBurst, transform.position + Vector3.up * 1.6f, Quaternion.identity).Play(1.5f);
            GameEvents.PlaySound(SoundCue.SeamRip, transform.position);
            GameEvents.PlaySound(SoundCue.DecoyBurst, transform.position);
            GameFeel.Shake(0.6f);
            GameEvents.Announce(new Announcement { Title = "rule.hare_rip", Hint = "rule.hare_rip.hint", Seconds = 3f });
            if (_state is not State.Air and not State.Enter)
            {
                HideMarker();
                Halt();
                Enter(State.Rip);
            }
        }

        void OnDied(HitInfo hit)
        {
            HideMarker();
            if (debrisPrefab)
            {
                var debris = PoolService.Spawn(debrisPrefab, transform.position, transform.rotation).GetComponent<Debris>();
                if (debris)
                    debris.Burst(hit.Direction, definition.debrisForce + hit.Force * 0.2f, Vector3.zero);
            }
            for (int i = 0; i < 4; i++)
                SpawnCloud(transform.position + Quaternion.AngleAxis(90f * i, Vector3.up) * Vector3.forward * 1.5f);
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.SeamRip, transform.position);
            GameEvents.PlaySound(SoundCue.BossSplit, transform.position);
            GameFeel.HitStop(0.08f);
            GameFeel.Shake(0.9f);
            PoolService.Despawn(gameObject);
        }

        // ---------- Вид ----------

        void LateUpdate()
        {
            if (!visual)
                return;
            float dt = Time.deltaTime;
            _squash = Mathf.MoveTowards(_squash, 0f, dt * 3f);
            float speed01 = _agent.enabled && _agent.isOnNavMesh
                ? Mathf.Clamp01(_agent.velocity.magnitude / Mathf.Max(0.1f, definition.hopSpeed)) : 0f;
            _hopPhase += dt * definition.hopRate * Mathf.PI * 2f * Mathf.Max(speed01, 0.001f);
            float hop = _state == State.Hop ? Mathf.Abs(Mathf.Sin(_hopPhase)) * 0.35f * speed01 : 0f;
            float crouch = _state == State.Crouch ? Mathf.Clamp01(_stateTime / Mathf.Max(0.05f, definition.crouchTime))
                : _state == State.Feint ? 0.6f : 0f;
            bool flying = _state is State.Air or State.Enter;
            float stretch = flying ? Mathf.Sin(Mathf.Clamp01(_stateTime / Mathf.Max(0.1f, _jumpTime)) * Mathf.PI) : 0f;

            float sy = 1f - 0.2f * crouch - 0.25f * _squash + 0.12f * stretch;
            float sxz = 1f + 0.1f * crouch + 0.15f * _squash - 0.05f * stretch;
            visual.localScale = new Vector3(sxz, sy, sxz);
            visual.localPosition = Vector3.up * hop;
            float shake = _state == State.Rip ? Mathf.Sin(_stateTime * 40f) * 6f : 0f;
            if (body)
                body.localRotation = _bodyRest * Quaternion.Euler(crouch * 12f, _state == State.Whip ? 25f : 0f, shake);

            // Уши: назад на замахе, вперёд хлыстом; в прыжке развеваются.
            float ear = _state switch
            {
                State.WhipWindup => -45f * Mathf.Clamp01(_stateTime / definition.whipWindup),
                State.Whip => 70f,
                State.Crouch => -20f * crouch,
                _ => flying ? -30f * stretch : Mathf.Sin(_hopPhase * 2f) * 6f * speed01,
            };
            if (earL)
                earL.localRotation = _earLRest * Quaternion.Euler(ear, 0f, 0f);
            if (earR)
                earR.localRotation = _earRRest * Quaternion.Euler(ear * 0.8f, 0f, 0f);
            if (head)
                head.localRotation = _headRest * Quaternion.Euler(_state == State.WhipWindup ? -10f : 0f, 0f, 0f);
            // Правая лапа: замах морковкой.
            float arm = _state == State.CarrotWindup ? 120f * Mathf.Clamp01(_stateTime / definition.carrotWindup) : 0f;
            if (armR)
                armR.localRotation = _armRRest * Quaternion.Euler(arm, 0f, 0f);
            float legs = flying ? 35f * stretch : Mathf.Sin(_hopPhase) * 20f * speed01;
            if (legL)
                legL.localRotation = _legLRest * Quaternion.Euler(legs, 0f, 0f);
            if (legR)
                legR.localRotation = _legRRest * Quaternion.Euler(legs, 0f, 0f);
        }

        void Enter(State state)
        {
            _state = state;
            _stateTime = 0f;
            if (state == State.Hop && _agent.enabled && _agent.isOnNavMesh)
                _agent.isStopped = false;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
