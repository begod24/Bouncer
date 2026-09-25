using System;
using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Физрук-манекен — босс хоккейной коробки. Ходит всегда (на взгляд не замирает, он же босс), вблизи бьёт
    /// планшетом, издалека бросает сильные мячи (удержит только идеальная ловля). Каждые несколько секунд свистит
    /// и объявляет правило раунда — по очереди из мешка, без повторов подряд:
    /// «Замри!» — все враги стоят, а в того, кто побежит, летит сильный мяч; бросать можно;
    /// «Штрафной!» — веер из пяти мячей, все можно поймать;
    /// «Мяч в игре!» — из калитки выкатывается огромный мяч и сбивает всех, врагов тоже;
    /// «Замена!» — со скамейки выбегает подмога.
    /// Жизнь — на общей полосе босса (<see cref="BossSplit"/> без половинок).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class FizrukBoss : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        enum State
        {
            Chase,
            Windup,
            Smash,
            Recover,
            Aim,
            Whistle,
            Penalty,
        }

        public enum Rule
        {
            Freeze,
            Penalty,
            BallInPlay,
            Substitution,
        }

        /// <summary>Кто выбегает со скамейки по «Замене!».</summary>
        [Serializable]
        public sealed class Substitute
        {
            public GameObject prefab;
            [Min(1)] public int count = 3;
            [Tooltip("Шеренгой (солдатики) или кучкой")]
            public bool line = true;
        }

        const float RepathInterval = 0.3f;

        [SerializeField] FizrukDefinition definition;

        [Header("Части модели")]
        [SerializeField] Transform body;
        [SerializeField] Transform head;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;
        [SerializeField] Transform legL;
        [SerializeField] Transform legR;

        [Header("Мячи")]
        [SerializeField] Ball ballPrefab;
        [Tooltip("Откуда вылетают мячи")]
        [SerializeField] Transform hand;
        [SerializeField] GiantBall giantBallPrefab;

        [Header("Замена")]
        [SerializeField] Substitute[] substitutes = Array.Empty<Substitute>();

        [Header("Прочее")]
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        readonly List<Rule> _bag = new();
        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        Targetable _target;
        State _state;
        float _stateTime;
        float _nextRepath;
        float _nextAttack;
        float _nextThrow;
        float _nextWhistle;
        float _nextPunish;
        float _freezeUntil;
        bool _smashed;
        bool _hasLastRule;
        Rule _lastRule;
        int _nextSubstitute;
        Quaternion _bodyRest, _headRest, _armLRest, _armRRest, _legLRest, _legRRest;
        Vector3 _bodyEuler, _headEuler, _armLEuler, _armREuler, _legLEuler, _legREuler;
        float _walkPhase;

        public FizrukDefinition Definition => definition;
        bool Angry => _health.Current <= _health.Max * definition.angryAt;
        Vector3 HandPosition => hand ? hand.position : transform.position + Vector3.up * 2.6f + transform.forward * 0.6f;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _agent.updateRotation = false;
            _health.Died += OnDied;
            _bodyRest = Rest(body);
            _headRest = Rest(head);
            _armLRest = Rest(armL);
            _armRRest = Rest(armR);
            _legLRest = Rest(legL);
            _legRRest = Rest(legR);
            ApplyDefinition();
        }

        static Quaternion Rest(Transform part) => part ? part.localRotation : Quaternion.identity;

        public void ApplyDefinition()
        {
            _agent.speed = definition.moveSpeed;
            _agent.acceleration = 10f;
            _agent.stoppingDistance = definition.attackRange * 0.8f;
            _health.Configure(definition.hitsToKill, 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _nextRepath = 0f;
            _nextAttack = Time.time + 2f;
            _nextThrow = Time.time + 3f;
            _nextWhistle = Time.time + definition.firstWhistle;
            _freezeUntil = 0f;
            _target = null;
            _bag.Clear();
            _hasLastRule = false;
            _nextSubstitute = 0;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            Enter(State.Chase);
        }

        public void OnDespawned() { }

        // ---------- Мозги ----------

        void Update()
        {
            float dt = Time.deltaTime;
            if (!_agent.isOnNavMesh)
                return;
            if (Time.time >= _nextRepath)
            {
                _nextRepath = Time.time + RepathInterval;
                _target = Targetable.FindNearest(transform.position, Team.Player);
                if (_target != null && _state == State.Chase)
                    _agent.SetDestination(_target.Position);
            }
            bool hasTarget = _target != null && _target.IsAlive && GameSession.IsGameplayActive;
            Vector3 toTarget = hasTarget ? Flat(_target.Position - transform.position) : Vector3.zero;
            float distance = toTarget.magnitude;

            // «Замри!»: все стоят, и Физрук тоже, но следит — кто побежит, получит мяч.
            if (_self.IsFrozen)
            {
                Halt();
                if (hasTarget)
                {
                    Face(toTarget, dt);
                    PunishRunner();
                }
                return;
            }
            _stateTime += dt;

            if (hasTarget && Time.time >= _nextWhistle && _state is State.Chase or State.Recover)
            {
                Halt();
                Enter(State.Whistle);
            }

            switch (_state)
            {
                case State.Chase:
                    _agent.isStopped = !hasTarget;
                    if (!hasTarget)
                        break;
                    _agent.speed = definition.moveSpeed * GumSpot.EnemyMoveMultiplierAt(transform.position);
                    Vector3 velocity = Flat(_agent.velocity);
                    Face(velocity.sqrMagnitude > 0.3f ? velocity : toTarget, dt);
                    if (distance <= definition.attackRange && Time.time >= _nextAttack)
                    {
                        Halt();
                        Enter(State.Windup);
                    }
                    else if (distance >= definition.throwMinRange && distance <= definition.throwMaxRange
                             && Time.time >= _nextThrow && CanSee(_target))
                    {
                        Halt();
                        Enter(State.Aim);
                    }
                    break;

                case State.Windup:
                    if (hasTarget)
                        Face(toTarget, dt);
                    if (_stateTime >= definition.windupTime)
                    {
                        _smashed = false;
                        Enter(State.Smash);
                    }
                    break;

                case State.Smash:
                    if (!_smashed)
                    {
                        _smashed = true;
                        Smash();
                    }
                    if (_stateTime >= definition.smashTime)
                        Enter(State.Recover);
                    break;

                case State.Recover:
                    if (_stateTime >= definition.recoverTime)
                    {
                        _nextAttack = Time.time + definition.attackCooldown;
                        Enter(State.Chase);
                    }
                    break;

                case State.Aim:
                    if (hasTarget)
                        Face(toTarget, dt);
                    if (_stateTime >= definition.throwWindup)
                    {
                        if (hasTarget)
                            ThrowStrong(_target);
                        _nextThrow = Time.time + definition.throwCooldown;
                        Enter(State.Chase);
                    }
                    break;

                case State.Whistle:
                    if (hasTarget)
                        Face(toTarget, dt);
                    if (_stateTime >= definition.whistleTime)
                        Blow();
                    break;

                case State.Penalty:
                    if (hasTarget)
                        Face(toTarget, dt);
                    if (_stateTime >= definition.penaltyWindup)
                    {
                        if (hasTarget)
                            ThrowFan(_target);
                        Enter(State.Chase);
                    }
                    break;
            }
        }

        // ---------- Свисток ----------

        void Blow()
        {
            GameEvents.PlaySound(SoundCue.Whistle, HandPosition);
            var rule = NextRule();
            float interval = Angry ? definition.angryWhistleInterval : definition.whistleInterval;
            _nextWhistle = Time.time + interval;
            Enter(State.Chase);
            switch (rule)
            {
                case Rule.Freeze:
                    Targetable.FreezeEnemies(definition.freezeTime);
                    _freezeUntil = Time.time + definition.freezeTime;
                    _nextPunish = Time.time + definition.freezeGrace;
                    _nextWhistle += definition.freezeTime;
                    Announce("rule.freeze", definition.freezeTime);
                    break;
                case Rule.Penalty:
                    Announce("rule.penalty", definition.penaltyWindup + 1.5f);
                    Enter(State.Penalty);
                    break;
                case Rule.BallInPlay:
                    Announce("rule.ball", definition.ballInPlayTime);
                    RollGiantBall();
                    break;
                case Rule.Substitution:
                    Announce("rule.substitution", 2.5f);
                    CallSubstitutes();
                    break;
            }
        }

        static void Announce(string key, float seconds) =>
            GameEvents.Announce(new Announcement { Title = key, Hint = key + ".hint", Seconds = seconds });

        /// <summary>Следующее правило из мешка: все по разу в случайном порядке, одно и то же два раза подряд не выходит.</summary>
        Rule NextRule()
        {
            if (_bag.Count == 0)
            {
                foreach (Rule rule in Enum.GetValues(typeof(Rule)))
                    if (IsAvailable(rule))
                        _bag.Add(rule);
                for (int i = _bag.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
                }
                if (_hasLastRule && _bag.Count > 1 && _bag[0] == _lastRule)
                    (_bag[0], _bag[1]) = (_bag[1], _bag[0]);
            }
            var next = _bag.Count > 0 ? _bag[0] : Rule.Penalty;
            if (_bag.Count > 0)
                _bag.RemoveAt(0);
            _lastRule = next;
            _hasLastRule = true;
            return next;
        }

        bool IsAvailable(Rule rule) => rule switch
        {
            Rule.BallInPlay => giantBallPrefab != null,
            Rule.Substitution => substitutes != null && substitutes.Length > 0,
            _ => true,
        };

        /// <summary>«Замри!»: кто бежит — тому сильный мяч, без замаха.</summary>
        void PunishRunner()
        {
            if (Time.time >= _freezeUntil || Time.time < _nextPunish)
                return;
            if (Flat(_target.Velocity).magnitude <= definition.freezeMoveSpeed)
                return;
            _nextPunish = Time.time + definition.freezePunishCooldown;
            ThrowStrong(_target);
        }

        void RollGiantBall()
        {
            if (giantBallPrefab == null || _target == null)
                return;
            var gate = ArenaSpot.FindFarthest(ArenaSpotKind.BallGate, _target.Position);
            Vector3 position = gate ? gate.Position + gate.Inward * 2f : transform.position + transform.forward * 2.5f;
            Vector3 direction = Flat(_target.Position - position);
            direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : transform.forward;
            // Мяч катится не точно в игрока, а чуть мимо — от него можно отойти.
            direction = Quaternion.Euler(0f, Random.Range(-12f, 12f), 0f) * direction;
            if (gate && Vector3.Dot(direction, gate.Inward) < 0.2f)
                direction = gate.Inward;
            var ball = PoolService.Spawn(giantBallPrefab, new Vector3(position.x, 0f, position.z), Quaternion.identity);
            ball.Launch(direction);
        }

        void CallSubstitutes()
        {
            if (substitutes == null || substitutes.Length == 0)
                return;
            var group = substitutes[_nextSubstitute++ % substitutes.Length];
            if (group == null || group.prefab == null)
                return;
            var bench = ArenaSpot.FindFarthest(ArenaSpotKind.Bench, _target ? _target.Position : transform.position);
            Vector3 position = bench ? bench.Position + bench.Inward * 1.5f : transform.position - transform.forward * 3f;
            GameEvents.RequestSpawn(new SpawnRequest
            {
                Prefab = group.prefab,
                Count = group.count,
                Position = position,
                Line = group.line,
            });
        }

        // ---------- Удары и броски ----------

        void Smash()
        {
            GameEvents.PlaySound(SoundCue.AreaThud, transform.position);
            GameFeel.Shake(0.35f);
            if (_target == null || !_target.IsAlive)
                return;
            Vector3 toTarget = Flat(_target.Position - transform.position);
            if (toTarget.magnitude > definition.smashRange || Vector3.Angle(transform.forward, toTarget) > definition.smashHalfAngle)
                return;
            if (_target.TryGetComponent(out IDamageable damageable))
            {
                damageable.ApplyHit(new HitInfo
                {
                    Damage = definition.smashDamage,
                    Point = _target.AimPoint,
                    Direction = toTarget.normalized,
                    Force = definition.smashKnockback,
                    SourceTeam = Team.Enemy,
                    Source = gameObject,
                    Flags = HitFlags.Melee | HitFlags.Charged,
                });
            }
        }

        /// <summary>Сильный мяч: удержит только идеальная ловля.</summary>
        void ThrowStrong(Targetable target) =>
            ThrowAt(target, 0f, definition.ballSpeed, definition.ballGravity, HitFlags.Charged, SoundCue.ThrowCharged);

        /// <summary>«Штрафной!»: веер обычных мячей — их можно поймать.</summary>
        void ThrowFan(Targetable target)
        {
            int count = Mathf.Max(1, definition.penaltyBalls);
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (count - 1f);
                float angle = Mathf.Lerp(-definition.penaltySpread * 0.5f, definition.penaltySpread * 0.5f, t);
                ThrowAt(target, angle, definition.penaltySpeed, definition.ballGravity, HitFlags.None,
                    i == 0 ? SoundCue.SoldierThrow : (SoundCue?)null);
            }
        }

        void ThrowAt(Targetable target, float angle, float speed, float gravity, HitFlags flags, SoundCue? cue)
        {
            if (ballPrefab == null || target == null)
                return;
            Vector3 origin = HandPosition;
            Vector3 aim = target.AimPoint;
            Vector3 flat = Flat(aim - origin);
            aim += Flat(target.Velocity) * (flat.magnitude / speed * 0.5f);
            flat = Flat(aim - origin);
            float distance = flat.magnitude;
            if (distance < 0.5f)
                return;
            float time = distance / speed;
            float up = (aim.y - origin.y + 0.5f * gravity * time * time) / time;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * (flat / distance);
            var ball = PoolService.Spawn(ballPrefab, origin, Quaternion.identity);
            ball.Launch(new BallThrow
            {
                Origin = origin,
                Direction = direction,
                Team = Team.Enemy,
                Thrower = gameObject,
                Stats = new ThrowStats
                {
                    Speed = speed,
                    UpVelocity = up,
                    Gravity = gravity,
                    Damage = definition.ballDamage,
                    Knockback = definition.ballKnockback,
                    Flags = flags,
                },
            });
            if (cue.HasValue)
                GameEvents.PlaySound(cue.Value, origin);
        }

        bool CanSee(Targetable target) =>
            !Physics.Linecast(HandPosition, target.AimPoint, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);

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
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), definition.turnSpeed * dt);
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
            GameFeel.Shake(strong ? 0.3f : 0.12f);
            if (hitFlash)
                hitFlash.Flash(Color.white, 0.12f);
            GameEvents.PlaySound(strong ? SoundCue.EnemyHitStrong : SoundCue.EnemyHit, hit.Point);
            _health.TryDamage(hit);
            return true;
        }

        void OnDied(HitInfo hit)
        {
            if (debrisPrefab)
            {
                var debris = PoolService.Spawn(debrisPrefab, transform.position, transform.rotation).GetComponent<Debris>();
                if (debris)
                    debris.Burst(hit.Direction, definition.debrisForce + hit.Force * 0.2f, Vector3.zero);
            }
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.BossSplit, transform.position);
            GameFeel.HitStop(0.08f);
            GameFeel.Shake(0.9f);
            PoolService.Despawn(gameObject);
        }

        // ---------- Вид ----------

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 bodyE = Vector3.zero, headE = Vector3.zero, armLE = Vector3.zero, armRE = Vector3.zero, legLE = Vector3.zero, legRE = Vector3.zero;
            float follow = 14f;
            if (_self.IsFrozen)
            {
                // «Замри!»: судья указывает на игрока свистком.
                armRE = new Vector3(-95f, 0f, 0f);
                headE = new Vector3(-5f, 0f, 0f);
            }
            else
            {
                switch (_state)
                {
                    case State.Windup:
                    {
                        float k = Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, definition.windupTime));
                        bodyE = new Vector3(-10f * k, 0f, 0f);
                        armRE = new Vector3(-175f * k, 0f, 0f);
                        armLE = new Vector3(-40f * k, 0f, 0f);
                        follow = 20f;
                        break;
                    }
                    case State.Smash:
                        bodyE = new Vector3(22f, 0f, 0f);
                        armRE = new Vector3(-35f, 0f, 0f);
                        armLE = new Vector3(20f, 0f, 0f);
                        headE = new Vector3(12f, 0f, 0f);
                        follow = 40f;
                        break;
                    case State.Aim:
                    {
                        float k = Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, definition.throwWindup));
                        bodyE = new Vector3(-6f * k, -18f * k, 0f);
                        armRE = new Vector3(-160f * k, 0f, 15f * k);
                        follow = 20f;
                        break;
                    }
                    case State.Whistle:
                        // Свисток у рта, голова запрокинута — сейчас засвистит.
                        armRE = new Vector3(-135f, 0f, -35f);
                        headE = new Vector3(-18f, 0f, 0f);
                        bodyE = new Vector3(-6f, 0f, 0f);
                        follow = 18f;
                        break;
                    case State.Penalty:
                    {
                        float k = Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, definition.penaltyWindup));
                        armLE = new Vector3(40f * k, 0f, -30f * k);
                        armRE = new Vector3(40f * k, 0f, 30f * k);
                        bodyE = new Vector3(-12f * k, 0f, 0f);
                        follow = 16f;
                        break;
                    }
                    case State.Recover:
                        bodyE = new Vector3(10f, 0f, 0f);
                        armRE = new Vector3(-20f, 0f, 0f);
                        break;
                    default:
                    {
                        // Негнущаяся ходьба большого манекена.
                        float speed01 = _agent.isOnNavMesh ? Mathf.Clamp01(_agent.velocity.magnitude / Mathf.Max(0.1f, definition.moveSpeed)) : 0f;
                        _walkPhase += dt * 4.5f * speed01;
                        float swing = Mathf.Sin(_walkPhase) * 26f * speed01;
                        legLE = new Vector3(-swing, 0f, 0f);
                        legRE = new Vector3(swing, 0f, 0f);
                        armLE = new Vector3(swing * 0.6f, 0f, -4f);
                        armRE = new Vector3(-swing * 0.6f, 0f, 4f);
                        bodyE = new Vector3(4f, 0f, Mathf.Sin(_walkPhase) * 3f * speed01);
                        break;
                    }
                }
            }
            float k2 = 1f - Mathf.Exp(-follow * dt);
            _bodyEuler = Vector3.Lerp(_bodyEuler, bodyE, k2);
            _headEuler = Vector3.Lerp(_headEuler, headE, k2);
            _armLEuler = Vector3.Lerp(_armLEuler, armLE, k2);
            _armREuler = Vector3.Lerp(_armREuler, armRE, k2);
            _legLEuler = Vector3.Lerp(_legLEuler, legLE, k2);
            _legREuler = Vector3.Lerp(_legREuler, legRE, k2);
            Apply(body, _bodyRest, _bodyEuler);
            Apply(head, _headRest, _headEuler);
            Apply(armL, _armLRest, _armLEuler);
            Apply(armR, _armRRest, _armREuler);
            Apply(legL, _legLRest, _legLEuler);
            Apply(legR, _legRRest, _legREuler);
        }

        static void Apply(Transform part, Quaternion rest, Vector3 euler)
        {
            if (part)
                part.localRotation = rest * Quaternion.Euler(euler);
        }

        void Enter(State state)
        {
            _state = state;
            _stateTime = 0f;
            if (state == State.Chase && _agent.isOnNavMesh)
                _agent.isStopped = false;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
