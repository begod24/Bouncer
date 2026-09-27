using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Пионер-барабанщик — поддержка. Сам не нападает: держится позади своей кучки (или на дистанции от игрока)
    /// и бьёт в барабан. Под его барабан соседи в радиусе бегают быстрее (<see cref="Targetable.Hurry"/>),
    /// каждые несколько ударов по земле расходится кольцо — видно, кого он подгоняет. Попадание сбивает ритм:
    /// барабан замолкает. Выбивать первым.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class DrummerEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        const float RepathInterval = 0.5f;

        [SerializeField] DrummerDefinition definition;
        [Tooltip("Кольцо по земле на каждый такт (BlastRing)")]
        [SerializeField] ExpandingRing ringPrefab;
        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        [Header("Вид")]
        [Tooltip("Корень модели: качается от попадания")]
        [SerializeField] Transform visual;
        [SerializeField] Transform body;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;
        [SerializeField] Transform drum;
        [SerializeField] Transform legL;
        [SerializeField] Transform legR;
        [SerializeField] float stepRate = 3f;
        [SerializeField] float legSwing = 24f;
        [Tooltip("Удар палочкой: рука поднимается и падает, градусы")]
        [SerializeField] float strikeAngle = 45f;

        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        Targetable _target;
        float _nextRepath;
        float _nextBeat;
        int _beat;
        float _silentUntil;
        float _lastBeatTime = -10f;
        Vector3 _knockback;
        Vector3 _lastHitDirection = Vector3.back;
        float _staggerStart = -10f;
        Vector3 _bodyRest;
        Vector3 _drumRest;
        float _phase;

        public DrummerDefinition Definition => definition;
        public bool IsDrumming => Time.time >= _silentUntil && !_self.IsFrozen && GameSession.IsGameplayActive;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _agent.updateRotation = false;
            _health.Died += OnDied;
            if (body)
                _bodyRest = body.localPosition;
            if (drum)
                _drumRest = drum.localPosition;
            ApplyDefinition();
        }

        public void ApplyDefinition()
        {
            _agent.speed = definition.moveSpeed;
            _agent.acceleration = 12f;
            _agent.stoppingDistance = 0.6f;
            _health.Configure(EnemyScaling.Hits(definition.hitsToKill), 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _knockback = Vector3.zero;
            _nextRepath = 0f;
            _nextBeat = Time.time + 0.6f;
            _beat = 0;
            _silentUntil = 0f;
            _target = null;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
        }

        public void OnDespawned() { }

        // ---------- Мозги ----------

        void Update()
        {
            float dt = Time.deltaTime;
            if (!_agent.isOnNavMesh)
                return;
            if (_self.IsFrozen)
            {
                Halt();
                return;
            }
            if (_knockback.sqrMagnitude > 0.01f)
            {
                _agent.Move(_knockback * dt);
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-8f * dt));
            }

            bool stunned = Time.time < _silentUntil;
            if (Time.time >= _nextRepath)
            {
                _nextRepath = Time.time + RepathInterval;
                _target = Targetable.FindNearest(transform.position, Team.Player);
                if (!stunned)
                    Reposition();
            }
            _agent.speed = definition.moveSpeed * _self.SpeedMultiplier * GumSpot.EnemyMoveMultiplierAt(transform.position)
                           * GroundZone.MoveMultiplierAt(transform.position);
            _agent.isStopped = stunned || _target == null || !GameSession.IsGameplayActive;

            // Лицом к игроку, пока стоит; на ходу — куда идёт.
            Vector3 velocity = Flat(_agent.velocity);
            Vector3 look = velocity.sqrMagnitude > 0.5f || _target == null ? velocity : Flat(_target.Position - transform.position);
            Face(look, dt);

            if (IsDrumming && Time.time >= _nextBeat)
            {
                _nextBeat = Time.time + definition.beatInterval;
                Beat();
            }
        }

        /// <summary>Встать позади своих (от игрока), а без них — на дистанции; слишком близко — отступить.</summary>
        void Reposition()
        {
            if (_target == null || !_target.IsAlive)
                return;
            Vector3 self = transform.position;
            Vector3 player = _target.Position;
            Vector3 away = Flat(self - player);
            float distance = away.magnitude;
            away = distance > 0.1f ? away / distance : Vector3.back;

            Vector3 desired;
            if (distance < definition.keepAwayDistance)
            {
                desired = self + away * 4f;
            }
            else
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                foreach (var t in Targetable.All)
                {
                    if (t == _self || t.Team != Team.Enemy || !t.IsAlive)
                        continue;
                    if (Flat(t.Position - self).sqrMagnitude > definition.packRadius * definition.packRadius)
                        continue;
                    sum += t.Position;
                    count++;
                }
                if (count > 0)
                {
                    Vector3 pack = sum / count;
                    Vector3 fromPlayer = Flat(pack - player);
                    fromPlayer = fromPlayer.sqrMagnitude > 0.01f ? fromPlayer.normalized : away;
                    desired = pack + fromPlayer * definition.behindPack;
                    // Но не ближе своей дистанции к игроку.
                    if (Flat(desired - player).magnitude < definition.keepAwayDistance)
                        desired = player + fromPlayer * definition.keepAwayDistance;
                }
                else
                {
                    desired = player + away * definition.preferredDistance;
                }
            }
            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        void Beat()
        {
            _beat++;
            _lastBeatTime = Time.time;
            GameEvents.PlaySound(SoundCue.DrumBeat, transform.position);
            float radiusSqr = definition.auraRadius * definition.auraRadius;
            Vector3 self = transform.position;
            foreach (var t in Targetable.All)
            {
                if (t == _self || t.Team != Team.Enemy || !t.IsAlive)
                    continue;
                if (Flat(t.Position - self).sqrMagnitude <= radiusSqr)
                    t.Hurry(definition.auraBoost, definition.beatInterval + 0.35f);
            }
            if (_beat % definition.ringEveryBeats == 0 && ringPrefab)
                PoolService.Spawn(ringPrefab, self + Vector3.up * 0.05f, Quaternion.identity).Play(definition.auraRadius);
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
            if (!_health.IsDead)
            {
                _knockback += Flat(hit.Direction) * (hit.Force * definition.knockbackScale);
                _silentUntil = Time.time + definition.staggerTime;
                _staggerStart = Time.time;
                Halt();
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
            GameEvents.PlaySound(SoundCue.SoldierPop, transform.position);
            GameFeel.Shake(0.25f);
            PoolService.Despawn(gameObject);
        }

        // ---------- Вид ----------

        void LateUpdate()
        {
            if (!body)
                return;
            float dt = Time.deltaTime;
            float speed01 = _agent.isOnNavMesh ? Mathf.Clamp01(_agent.velocity.magnitude / Mathf.Max(0.1f, definition.moveSpeed)) : 0f;
            _phase += dt * stepRate * Mathf.PI * Mathf.Max(speed01, 0.001f);
            float sin = Mathf.Sin(_phase);
            body.localPosition = _bodyRest + Vector3.up * (Mathf.Abs(sin) * 0.03f * speed01);
            if (legL)
                legL.localRotation = Quaternion.Euler(sin * legSwing * speed01, 0f, 0f);
            if (legR)
                legR.localRotation = Quaternion.Euler(-sin * legSwing * speed01, 0f, 0f);

            // Палочки по очереди: удар — рука падает на барабан, между ударами поднимается.
            float sinceBeat = Time.time - _lastBeatTime;
            float up = IsDrumming ? Mathf.Clamp01(sinceBeat / Mathf.Max(0.05f, definition.beatInterval)) : 0.3f;
            float lift = Mathf.Sin(up * Mathf.PI) * strikeAngle;
            bool left = _beat % 2 == 0;
            // Минус вокруг X — кисть уходит вверх-вперёд.
            if (armL)
                armL.localRotation = Quaternion.Euler(-(left ? lift : lift * 0.3f), 0f, 0f);
            if (armR)
                armR.localRotation = Quaternion.Euler(-(left ? lift * 0.3f : lift), 0f, 0f);
            if (drum)
                drum.localPosition = _drumRest + Vector3.down * (0.02f * Mathf.Exp(-sinceBeat * 18f));

            if (visual)
            {
                float t = Time.time - _staggerStart;
                Quaternion tilt = Quaternion.identity;
                if (t < 1.2f)
                {
                    float angle = 16f * Mathf.Exp(-5f * t) * Mathf.Cos(t * 16f);
                    Vector3 axis = Vector3.Cross(Vector3.up, _lastHitDirection);
                    if (axis.sqrMagnitude > 1e-4f)
                        tilt = Quaternion.AngleAxis(angle, axis.normalized);
                }
                visual.rotation = tilt * transform.rotation;
            }
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
