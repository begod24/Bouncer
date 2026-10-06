using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class TransformerBoss : MonoBehaviour, IBallTarget, IDamageable, IPoolable, INetEnemy
    {
        public enum State
        {
            Enter,
            Drive,
            Rev,
            Ram,
            Crash,
            ToRobot,
            Walk,
            VolleyWindup,
            MineWindup,
            Feint,
            ToCar,
        }

        const float RepathInterval = 0.35f;
        const float EnterTime = 1.2f;
        const float FeintTime = 0.35f;
        const float CurveChance = 0.35f;
        const float CurveOffset = 22f;
        const float NoseLength = 1.6f;

        [SerializeField] TransformerDefinition definition;
        [SerializeField] TransformerRig rig;
        [SerializeField] Ball ballPrefab;
        [Tooltip("Дуло пушки (Transformer_Muzzle)")]
        [SerializeField] Transform muzzle;
        [Tooltip("Левая рука бросает батарейки (Transformer_Hand)")]
        [SerializeField] Transform hand;
        [SerializeField] BatteryMine minePrefab;
        [SerializeField] GameObject rcCarPrefab;
        [Tooltip("Полоса тарана на земле")]
        [SerializeField] GroundMarker lineMarkerPrefab;
        [SerializeField] ParticleBurst crashDust;
        [Tooltip("Мигалка: синий и красный по очереди")]
        [SerializeField] Light beacon;
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
        int _ramsLeft;
        float _robotUntil;
        float _nextAction;
        bool _volleyNext = true;
        bool _feinted;
        bool _announced;
        bool _ramHit;
        float _openUntil;
        Vector3 _ramDirection;
        GroundMarker _marker;
        Vector3 _enterDirection = Vector3.back;

        public TransformerDefinition Definition => definition;
        public State CurrentState => _state;
        public bool IsOpen => _state is State.ToRobot or State.ToCar or State.Crash || Time.time < _openUntil;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _agent.updateRotation = false;
            _health.Died += OnDied;
            ApplyDefinition();
        }

        public void ApplyDefinition()
        {
            _agent.speed = definition.driveSpeed;
            _agent.acceleration = 18f;
            _agent.stoppingDistance = 1f;
            _health.Configure(EnemyScaling.BossHits(definition.hitsToKill), 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _armor.Reset();
            _openUntil = 0f;
            _target = null;
            _nextRepath = 0f;
            _feinted = false;
            _announced = false;
            var entrance = ArenaSpot.Find(ArenaSpotKind.BossEntrance);
            Vector3 start = entrance ? entrance.Position : transform.position;
            _enterDirection = entrance ? entrance.Inward : -transform.position.normalized;
            _ramDirection = _enterDirection;
            if (NavMesh.SamplePosition(start, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            transform.rotation = Quaternion.LookRotation(_enterDirection);
            if (rig)
                rig.SetMode(car: true);
            GameEvents.PlaySound(SoundCue.Siren, transform.position);
            if (!NetHooks.IsGuest)
                SummonCars();
            _ramsLeft = definition.ramsPerCar;
            Enter(State.Enter);
        }

        public void OnDespawned() => HideMarker();

        void Update()
        {
            if (NetHooks.IsGuest)
            {
                UpdateBeacon();
                return;
            }
            float dt = Time.deltaTime;
            if (!_agent.isOnNavMesh)
                return;
            _stateTime += dt;
            bool repath = Time.time >= _nextRepath;
            if (repath)
            {
                _nextRepath = Time.time + RepathInterval;
                _target = Targetable.FindNearest(transform.position, Team.Player);
            }
            bool hasTarget = _target != null && _target.IsAlive && GameSession.IsGameplayActive;
            Vector3 toTarget = hasTarget ? Flat(_target.Position - transform.position) : Vector3.zero;
            float distance = toTarget.magnitude;
            if (_self.IsFrozen)
            {
                Halt();
                UpdateLook(0f, 0f);
                return;
            }

            switch (_state)
            {
                case State.Enter:
                    BreakAhead();
                    _agent.Move(_enterDirection * (definition.driveSpeed * 0.8f * dt));
                    Face(_enterDirection, dt);
                    UpdateLook(definition.driveSpeed * 0.8f, 0f);
                    if (_stateTime >= EnterTime)
                        Enter(State.Drive);
                    break;

                case State.Drive:
                    _agent.isStopped = !hasTarget;
                    _agent.speed = definition.driveSpeed * _self.SpeedMultiplier;
                    if (hasTarget && repath)
                        _agent.SetDestination(DriveGoal());
                    Face(Flat(_agent.velocity).sqrMagnitude > 0.5f ? Flat(_agent.velocity) : toTarget, dt * 2f);
                    UpdateLook(_agent.velocity.magnitude, 0f);
                    if (_stateTime >= definition.driveTime && hasTarget)
                    {
                        Halt();
                        _ramDirection = distance > 0.1f ? toTarget / distance : transform.forward;
                        GameEvents.PlaySound(SoundCue.EngineRev, transform.position);
                        Enter(State.Rev);
                    }
                    break;

                case State.Rev:
                    if (hasTarget && _stateTime < definition.revTime * 0.7f)
                        _ramDirection = distance > 0.1f ? toTarget / distance : _ramDirection;
                    Face(_ramDirection, dt * 3f);
                    UpdateLook(Mathf.Sin(_stateTime * 40f) * 3f, 0f);
                    if (_marker == null && _stateTime >= definition.revTime * 0.35f && lineMarkerPrefab)
                    {
                        _marker = PoolService.Spawn(lineMarkerPrefab, transform.position, Quaternion.identity);
                        _marker.ShowLine(transform.position, transform.position + _ramDirection * (definition.ramSpeed * definition.ramTime),
                            definition.revTime * 0.65f + definition.ramTime);
                    }
                    if (_stateTime >= definition.revTime)
                    {
                        _ramHit = false;
                        transform.rotation = Quaternion.LookRotation(_ramDirection);
                        GameEvents.PlaySound(SoundCue.Dash, transform.position);
                        Enter(State.Ram);
                    }
                    break;

                case State.Ram:
                    Ram(dt);
                    break;

                case State.Crash:
                    UpdateLook(0f, 0f);
                    if (_stateTime >= definition.crashStun)
                        AfterRam();
                    break;

                case State.ToRobot:
                case State.ToCar:
                    Halt();
                    if (rig)
                        rig.SetProgress(_stateTime / Mathf.Max(0.1f, definition.transformTime));
                    UpdateLook(0f, 0f);
                    if (_stateTime >= definition.transformTime)
                    {
                        GameEvents.PlaySound(SoundCue.AreaThud, transform.position);
                        GameFeel.Shake(0.3f);
                        if (_state == State.ToRobot)
                        {
                            _robotUntil = Time.time + definition.robotTime;
                            _nextAction = Time.time + definition.actionPause * EnemyScaling.BossCooldown;
                            Enter(State.Walk);
                        }
                        else
                        {
                            _ramsLeft = definition.ramsPerCar;
                            GameEvents.PlaySound(SoundCue.Siren, transform.position);
                            SummonCars();
                            Enter(State.Drive);
                        }
                    }
                    break;

                case State.Walk:
                    _agent.isStopped = !hasTarget;
                    _agent.speed = definition.walkSpeed * _self.SpeedMultiplier;
                    if (hasTarget && repath)
                        _agent.SetDestination(WalkGoal());
                    if (hasTarget)
                        Face(toTarget, dt);
                    UpdateLook(0f, Mathf.Clamp01(_agent.velocity.magnitude / Mathf.Max(0.1f, definition.walkSpeed)));
                    if (Time.time >= _robotUntil)
                    {
                        Halt();
                        StartTransform(car: true);
                        break;
                    }
                    if (hasTarget && Time.time >= _nextAction)
                    {
                        Halt();
                        Enter(_volleyNext ? State.VolleyWindup : State.MineWindup);
                        _volleyNext = !_volleyNext;
                    }
                    break;

                case State.VolleyWindup:
                    if (hasTarget)
                        Face(toTarget, dt * 2f);
                    UpdateLook(0f, 0f);
                    if (rig)
                        rig.AimCannon = Mathf.Clamp01(_stateTime / Mathf.Max(0.05f, definition.volleyWindup));
                    if (_stateTime >= definition.volleyWindup)
                    {
                        if (!_feinted && Random.value < Danger.FeintChance)
                        {
                            _feinted = true;
                            Enter(State.Feint);
                            break;
                        }
                        _feinted = false;
                        if (hasTarget)
                            Volley(_target);
                        _openUntil = Time.time + definition.openAfterVolley;
                        _nextAction = Time.time + definition.actionPause * EnemyScaling.BossCooldown;
                        Enter(State.Walk);
                    }
                    break;

                case State.Feint:
                    if (rig)
                        rig.AimCannon = 0.4f;
                    if (_stateTime >= FeintTime)
                        Enter(State.VolleyWindup);
                    break;

                case State.MineWindup:
                    if (hasTarget)
                        Face(toTarget, dt * 2f);
                    UpdateLook(0f, 0f);
                    if (rig)
                        rig.WindThrow = Mathf.Clamp01(_stateTime / Mathf.Max(0.05f, definition.mineWindup));
                    if (_stateTime >= definition.mineWindup)
                    {
                        if (hasTarget)
                            ThrowMines(_target);
                        _nextAction = Time.time + definition.actionPause * EnemyScaling.BossCooldown;
                        Enter(State.Walk);
                    }
                    break;
            }
            UpdateBeacon();
        }

        Vector3 DriveGoal()
        {
            Vector3 from = Flat(transform.position - _target.Position);
            if (from.sqrMagnitude < 0.01f)
                from = Vector3.back;
            Vector3 next = Quaternion.AngleAxis(60f, Vector3.up) * from.normalized;
            Vector3 goal = _target.Position + next * 9f;
            return NavMesh.SamplePosition(goal, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : _target.Position;
        }

        Vector3 WalkGoal()
        {
            Vector3 away = Flat(transform.position - _target.Position);
            float distance = away.magnitude;
            away = distance > 0.1f ? away / distance : Vector3.back;
            Vector3 goal = _target.Position + away * definition.preferredDistance;
            return NavMesh.SamplePosition(goal, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : transform.position;
        }

        void Ram(float dt)
        {
            BreakAhead();
            Vector3 before = transform.position;
            float step = definition.ramSpeed * _self.SpeedMultiplier * dt;
            _agent.Move(_ramDirection * step);
            float moved = Flat(transform.position - before).magnitude;
            UpdateLook(definition.ramSpeed, 0f);

            if (!_ramHit && _target != null && _target.IsAlive)
            {
                Vector3 delta = Flat(_target.Position - transform.position);
                if (delta.sqrMagnitude <= 2.2f * 2.2f && Vector3.Dot(delta, _ramDirection) > -0.5f
                    && _target.TryGetComponent(out IDamageable damageable))
                {
                    _ramHit = true;
                    damageable.ApplyHit(new HitInfo
                    {
                        Damage = definition.ramDamage,
                        Point = _target.AimPoint,
                        Direction = _ramDirection,
                        Force = definition.ramKnockback,
                        SourceTeam = Team.Enemy,
                        Source = gameObject,
                        Flags = HitFlags.Melee | HitFlags.Charged,
                    });
                }
            }
            bool blocked = _stateTime > 0.15f && (moved < step * 0.3f || NoseAgainstWall());
            if (blocked)
            {
                HideMarker();
                GameEvents.PlaySound(SoundCue.AreaThud, transform.position);
                GameEvents.PlaySound(SoundCue.Explosion, transform.position);
                GameFeel.Shake(0.5f);
                if (crashDust)
                    PoolService.Spawn(crashDust, transform.position + _ramDirection * 1.5f + Vector3.up * 0.3f, Quaternion.identity).Play(1.2f);
                Enter(State.Crash);
            }
            else if (_stateTime >= definition.ramTime)
            {
                HideMarker();
                AfterRam();
            }
        }

        bool NoseAgainstWall()
        {
            if (!Physics.SphereCast(transform.position + Vector3.up * 0.7f, 0.6f, _ramDirection, out RaycastHit hit, NoseLength,
                    Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                return false;
            if (hit.rigidbody && !hit.rigidbody.isKinematic)
                return false;
            var breakable = hit.collider.GetComponentInParent<IBreakable>();
            return breakable == null || breakable.IsBroken;
        }

        void BreakAhead()
        {
            Vector3 front = transform.position + _ramDirection * 1.6f + Vector3.up * 0.6f;
            var hits = Physics.OverlapSphere(front, definition.breakRadius, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);
            foreach (var c in hits)
            {
                var breakable = c.GetComponentInParent<IBreakable>();
                if (breakable != null && !breakable.IsBroken)
                {
                    breakable.Break(_ramDirection, definition.breakForce);
                    continue;
                }
                var body = c.attachedRigidbody;
                if (body == null || body.isKinematic || Vector3.Dot(body.linearVelocity, _ramDirection) > definition.ramSpeed * 0.5f)
                    continue;
                Vector3 right = Vector3.Cross(Vector3.up, _ramDirection);
                float side = Vector3.Dot(body.position - transform.position, right) >= 0f ? 1f : -1f;
                body.linearVelocity = (_ramDirection * 0.6f + right * (0.8f * side)).normalized * (definition.ramSpeed * 0.9f)
                                      + Vector3.up * 2f;
                GameEvents.PlaySound(SoundCue.AreaThud, body.position);
            }
        }

        void AfterRam()
        {
            _ramsLeft--;
            if (_ramsLeft <= 0)
                StartTransform(car: false);
            else
                Enter(State.Drive);
        }

        void StartTransform(bool car)
        {
            if (rig)
                rig.BeginTransform(car);
            GameEvents.PlaySound(SoundCue.TransformClank, transform.position);
            if (!car && !_announced)
            {
                _announced = true;
                GameEvents.Announce(new Announcement { Title = "rule.transformer", Hint = "rule.transformer.hint", Seconds = 3f });
            }
            Enter(car ? State.ToCar : State.ToRobot);
        }

        void SummonCars()
        {
            if (rcCarPrefab == null || definition.rcCars <= 0)
                return;
            int alive = 0;
            foreach (var t in Targetable.All)
                if (t.Team == Team.Enemy && t.IsAlive && t.GetComponent<RCCarEnemy>() != null)
                    alive++;
            int count = Mathf.Min(definition.rcCars, definition.maxRcCars - alive);
            if (count <= 0)
                return;
            var entrance = ArenaSpot.Find(ArenaSpotKind.BossEntrance);
            GameEvents.RequestSpawn(new SpawnRequest
            {
                Prefab = rcCarPrefab,
                Count = count,
                Position = entrance ? entrance.Position + entrance.Inward * 1.5f : transform.position,
                Line = false,
            });
        }

        void Volley(Targetable target)
        {
            Vector3 origin = muzzle ? muzzle.position : transform.position + Vector3.up * 2.2f;
            Vector3 aim = target.AimPoint;
            Vector3 flat = Flat(aim - origin);
            float distance = Mathf.Max(1f, flat.magnitude);
            float time = distance / definition.ballSpeed;
            float up = (aim.y - origin.y + 0.5f * definition.ballGravity * time * time) / time;
            bool curve = Random.value < CurveChance;
            float side = Random.value < 0.5f ? -1f : 1f;
            int count = Mathf.Max(1, definition.volleyCount);
            GameEvents.PlaySound(SoundCue.ThrowCharged, origin);
            if (ballPrefab == null)
                return;
            for (int i = 0; i < count; i++)
            {
                float k = count == 1 ? 0.5f : i / (count - 1f);
                float angle = Mathf.Lerp(-definition.volleySpread * 0.5f, definition.volleySpread * 0.5f, k);
                var perks = default(BallPerks);
                if (curve)
                {
                    angle += CurveOffset * side;
                    perks.curve = -2f * CurveOffset * side / Mathf.Max(0.2f, time);
                }
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * (flat / distance);
                var ball = PoolService.Spawn(ballPrefab, origin, Quaternion.identity);
                ball.Launch(new BallThrow
                {
                    Origin = origin,
                    Direction = direction,
                    Team = Team.Enemy,
                    Thrower = gameObject,
                    Perks = perks,
                    Stats = new ThrowStats
                    {
                        Speed = definition.ballSpeed,
                        UpVelocity = up,
                        Gravity = definition.ballGravity,
                        Damage = definition.ballDamage,
                        Knockback = definition.ballKnockback,
                        Flags = i % 2 == 1 ? HitFlags.Spiky : HitFlags.None,
                    },
                });
            }
        }

        void ThrowMines(Targetable target)
        {
            if (minePrefab == null)
                return;
            Vector3 from = hand ? hand.position : transform.position + Vector3.up * 2f;
            GameEvents.PlaySound(SoundCue.SoldierThrow, from);
            for (int i = 0; i < definition.mines; i++)
            {
                Vector3 offset = i == 0 ? Vector3.zero
                    : Quaternion.AngleAxis(360f * i / Mathf.Max(1, definition.mines - 1) + Random.Range(-20f, 20f), Vector3.up)
                      * Vector3.forward * definition.mineScatter;
                Vector3 to = target.Position + Flat(target.Velocity) * 0.4f + offset;
                if (NavMesh.SamplePosition(to, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    to = hit.position;
                var mine = PoolService.Spawn(minePrefab, from, Quaternion.identity);
                mine.Throw(from, to, definition.mineFlightTime + i * 0.12f, definition.mineFuse, definition.mineRadius,
                    definition.mineDamage, definition.mineKnockback, gameObject);
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

        void UpdateLook(float driveSpeed, float walk)
        {
            if (!rig)
                return;
            rig.DriveSpeed = driveSpeed;
            rig.Walk = walk;
            if (_state is not State.VolleyWindup and not State.Feint)
                rig.AimCannon = Mathf.MoveTowards(rig.AimCannon, 0f, Time.deltaTime * 3f);
            if (_state != State.MineWindup)
                rig.WindThrow = Mathf.MoveTowards(rig.WindThrow, 0f, Time.deltaTime * 4f);
        }

        void UpdateBeacon()
        {
            if (!beacon)
                return;
            bool red = Mathf.Repeat(Time.time * 3f, 1f) > 0.5f;
            beacon.color = red ? new Color(1f, 0.2f, 0.2f) : new Color(0.3f, 0.5f, 1f);
        }

        void HideMarker()
        {
            if (_marker && _marker.isActiveAndEnabled)
                _marker.Hide();
            _marker = null;
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
            bool open = IsOpen;
            bool strong = hit.Has(HitFlags.Charged);
            GameFeel.Shake(strong ? 0.3f : 0.12f);
            BossArmor.Flash(hitFlash, open);
            GameEvents.PlaySound(strong || open ? SoundCue.EnemyHitStrong : SoundCue.ShieldBlock, hit.Point);
            var counted = hit;
            counted.Damage = _armor.Take(hit.Damage, open);
            if (counted.Damage > 0)
                _health.TryDamage(counted);
            return true;
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
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.Explosion, transform.position);
            GameEvents.PlaySound(SoundCue.BossSplit, transform.position);
            GameFeel.HitStop(0.08f);
            GameFeel.Shake(0.9f);
            PoolService.Despawn(gameObject);
        }

        public void WriteNet(NetWriter writer)
        {
            writer.Byte((byte)_state);
            writer.Seconds(_stateTime);
            if (!rig)
                return;
            writer.Bool(rig.TargetIsCar);
            writer.Byte(Unit(rig.Progress));
            writer.Byte((byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(rig.DriveSpeed) * 8f), 0, 255));
            writer.Byte(Unit(rig.Walk));
            writer.Byte(Unit(rig.AimCannon));
            writer.Byte(Unit(rig.WindThrow));
        }

        public void ReadNet(NetReader reader, float age)
        {
            _state = (State)reader.Byte();
            _stateTime = reader.Seconds() + age;
            if (!rig)
                return;
            bool car = reader.Bool();
            float progress = reader.Byte() / 255f;
            if (progress < 1f && _state is State.ToRobot or State.ToCar)
                progress = Mathf.Clamp01(_stateTime / Mathf.Max(0.1f, definition.transformTime));
            rig.SetNet(car, progress);
            rig.DriveSpeed = reader.Byte() / 8f;
            rig.Walk = reader.Byte() / 255f;
            rig.AimCannon = reader.Byte() / 255f;
            rig.WindThrow = reader.Byte() / 255f;
        }

        static byte Unit(float value) => (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);

        void Enter(State state)
        {
            _state = state;
            _stateTime = 0f;
            if ((state is State.Drive or State.Walk) && _agent.isOnNavMesh)
                _agent.isStopped = false;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
