using Bouncer.Balls;
using Bouncer.Core;
using Bouncer.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class CryDollEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable, INetEnemy
    {
        public enum State
        {
            Walk,
            Cry,
            Stagger,
        }

        const float RepathInterval = 0.4f;

        [SerializeField] CryDollDefinition definition;
        [Tooltip("Кого зовёт (обычный пупс)")]
        [SerializeField] GameObject pupsikPrefab;
        [SerializeField] ExpandingRing waveRingPrefab;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        [Header("Вид")]
        [Tooltip("Корень модели: переваливается на ходу")]
        [SerializeField] Transform visual;
        [SerializeField] Transform head;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;
        [SerializeField] Transform legL;
        [SerializeField] Transform legR;

        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        Targetable _target;
        State _state;
        float _stateTime;
        float _nextRepath;
        float _nextCry;
        float _nextWave;
        int _cries;
        Vector3 _knockback;
        float _phase;
        Quaternion _headRest;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _agent.updateRotation = false;
            _health.Died += OnDied;
            if (head)
                _headRest = head.localRotation;
            ApplyDefinition();
        }

        public void ApplyDefinition()
        {
            _agent.speed = definition.moveSpeed;
            _agent.acceleration = 10f;
            _agent.stoppingDistance = definition.preferredDistance;
            _health.Configure(EnemyScaling.Hits(definition.hitsToKill, gameObject), 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _knockback = Vector3.zero;
            _nextRepath = 0f;
            _nextCry = Time.time + definition.firstCryDelay;
            _cries = 0;
            _target = null;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            Enter(State.Walk);
        }

        public void OnDespawned() { }

        void Update()
        {
            if (NetHooks.IsGuest)
            {
                if (_state == State.Cry && !_self.IsFrozen && GameSession.IsGameplayActive)
                    SlowListeners();
                return;
            }
            float dt = Time.deltaTime;
            if (!_agent.isOnNavMesh)
                return;
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
                if (_target != null && _state == State.Walk && GameSession.IsGameplayActive)
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
                case State.Walk:
                    _agent.isStopped = !hasTarget;
                    _agent.speed = definition.moveSpeed * _self.SpeedMultiplier * GumSpot.EnemyMoveMultiplierAt(transform.position)
                                   * GroundZone.MoveMultiplierAt(transform.position);
                    if (!hasTarget)
                        break;
                    Vector3 toTarget = Flat(_target.Position - transform.position);
                    Vector3 velocity = Flat(_agent.velocity);
                    Face(velocity.sqrMagnitude > 0.3f ? velocity : toTarget, dt);
                    if (Time.time >= _nextCry && toTarget.magnitude <= definition.cryRadius + 2f)
                        BeginCry();
                    break;

                case State.Cry:
                    if (hasTarget)
                        Face(_target.Position - transform.position, dt);
                    SlowListeners();
                    if (Time.time >= _nextWave)
                    {
                        _nextWave = Time.time + definition.waveInterval;
                        if (waveRingPrefab)
                            PoolService.Spawn(waveRingPrefab, transform.position + Vector3.up * 0.05f, Quaternion.identity)
                                .Play(definition.cryRadius);
                    }
                    if (_stateTime >= definition.cryTime)
                    {
                        _nextCry = Time.time + definition.cryInterval;
                        Enter(State.Walk);
                    }
                    break;

                case State.Stagger:
                    if (_stateTime >= definition.staggerTime)
                        Enter(State.Walk);
                    break;
            }
        }

        void BeginCry()
        {
            Halt();
            Enter(State.Cry);
            _cries++;
            _nextWave = 0f;
            GameEvents.PlaySound(SoundCue.DollCry, transform.position);
            GameEvents.PlaySound(SoundCue.SpawnWarning, transform.position);
            if (pupsikPrefab && _cries % definition.summonEvery == 0 && PupsiksNear() < definition.maxPupsiksNear)
            {
                Vector3 side = Quaternion.AngleAxis(Random.Range(-60f, 60f), Vector3.up) * -transform.forward;
                GameEvents.RequestSpawn(new SpawnRequest
                {
                    Prefab = pupsikPrefab,
                    Count = definition.summonCount,
                    Position = transform.position + side * 1.8f,
                    Line = false,
                });
            }
        }

        int PupsiksNear()
        {
            int count = 0;
            float radiusSqr = definition.pupsikCountRadius * definition.pupsikCountRadius;
            foreach (var t in Targetable.All)
                if (t.Team == Team.Enemy && t.IsAlive && Flat(t.Position - transform.position).sqrMagnitude <= radiusSqr
                    && t.GetComponent<PupsikEnemy>() != null)
                    count++;
            return count;
        }

        void SlowListeners()
        {
            float radiusSqr = definition.cryRadius * definition.cryRadius;
            foreach (var player in Players.All)
            {
                if (!player.IsLocal || player.IsDead || Flat(player.transform.position - transform.position).sqrMagnitude > radiusSqr)
                    continue;
                player.Motor.Slow(definition.slowMultiplier, 0.3f);
            }
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
            if (hitFlash)
                hitFlash.Flash(Color.white, 0.12f);
            GameEvents.PlaySound(strong ? SoundCue.EnemyHitStrong : SoundCue.EnemyHit, hit.Point);
            _health.TryDamage(hit);
            if (!_health.IsDead)
            {
                _knockback += Flat(hit.Direction) * (hit.Force * definition.knockbackScale);
                if (_state == State.Cry)
                    _nextCry = Time.time + definition.cryInterval * 0.6f;
                Halt();
                Enter(State.Stagger);
            }
            return true;
        }

        void OnDied(HitInfo hit)
        {
            if (debrisPrefab)
            {
                var debris = PoolService.Spawn(debrisPrefab, transform.position, transform.rotation).GetComponent<Debris>();
                if (debris)
                    debris.Burst(hit.Direction, definition.debrisForce + hit.Force * 0.25f, Vector3.zero);
            }
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.PupsikPop, transform.position);
            PoolService.Despawn(gameObject);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 velocity = NetHooks.IsGuest ? _self.Velocity : _agent.isOnNavMesh ? _agent.velocity : Vector3.zero;
            float speed01 = Mathf.Clamp01(Flat(velocity).magnitude / Mathf.Max(0.1f, definition.moveSpeed));
            _phase += dt * 3.2f * Mathf.PI * Mathf.Max(speed01, 0.001f);
            float sin = Mathf.Sin(_phase);
            bool crying = _state == State.Cry && !_self.IsFrozen;
            if (visual)
                visual.localRotation = Quaternion.Euler(0f, 0f, sin * 7f * speed01 + (_state == State.Stagger ? Mathf.Sin(_stateTime * 30f) * 8f : 0f));
            if (legL)
                legL.localRotation = Quaternion.Euler(sin * 20f * speed01, 0f, 0f);
            if (legR)
                legR.localRotation = Quaternion.Euler(-sin * 20f * speed01, 0f, 0f);
            if (head)
                head.localRotation = _headRest * Quaternion.Euler(crying ? -8f : 0f, crying ? Mathf.Sin(Time.time * 22f) * 14f : 0f, 0f);
            float rub = crying ? Mathf.Sin(Time.time * 16f) * 10f : sin * 6f * speed01;
            if (armL)
                armL.localRotation = Quaternion.Euler(rub, 0f, 0f);
            if (armR)
                armR.localRotation = Quaternion.Euler(-rub, 0f, 0f);
        }

        public void WriteNet(NetWriter writer)
        {
            writer.Byte((byte)_state);
            writer.Seconds(_stateTime);
        }

        public void ReadNet(NetReader reader, float age)
        {
            _state = (State)reader.Byte();
            _stateTime = reader.Seconds() + age;
        }

        void Enter(State state)
        {
            _state = state;
            _stateTime = 0f;
            if (state == State.Walk && _agent.isOnNavMesh)
                _agent.isStopped = false;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
