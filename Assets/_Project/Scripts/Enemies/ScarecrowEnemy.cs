using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Чучело. Прыгает на своём шесте, держится на расстоянии и всегда поворачивается к игроку.
    /// Руки ловят мячи, пролетающие рядом спереди, — и через секунду бросают обратно (их можно поймать,
    /// идеальная ловля лечит). Заряженный мяч пробивает руки, сбоку и сзади попадание обычное: чучело
    /// поворачивается медленно, его можно обойти. Элитное ловит и заряженные, а бросает сильным мячом.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(Targetable))]
    public sealed class ScarecrowEnemy : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        const float RepathInterval = 0.4f;
        const float ChestHeight = 1.3f;

        [SerializeField] ScarecrowDefinition definition;

        [Header("Части модели")]
        [Tooltip("Корень модели: подпрыгивает")]
        [SerializeField] Transform visual;
        [SerializeField] Transform body;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;
        [Tooltip("Где висят пойманные мячи: левая и правая рука")]
        [SerializeField] Transform[] hands;

        [SerializeField] GameObject debrisPrefab;
        [SerializeField] HitFlash hitFlash;

        readonly List<Ball> _held = new();
        readonly List<float> _throwAt = new();
        NavMeshAgent _agent;
        Health _health;
        Targetable _self;
        Targetable _target;
        float _nextRepath;
        float _nextHop;
        float _hopStart = float.NegativeInfinity;
        Vector3 _hopDirection;
        Vector3 _knockback;
        Vector3 _visualRest;
        Quaternion _bodyRest, _armLRest, _armRRest;
        float _catchPose;
        float _throwPose;
        int _strafeSide = 1;

        public ScarecrowDefinition Definition => definition;
        public int HeldBalls => _held.Count;
        Vector3 Chest => transform.position + Vector3.up * ChestHeight;
        bool Hopping => Time.time - _hopStart < definition.hopTime;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _agent.updateRotation = false;
            _agent.updatePosition = true;
            _health.Died += OnDied;
            if (visual)
                _visualRest = visual.localPosition;
            if (body)
                _bodyRest = body.localRotation;
            if (armL)
                _armLRest = armL.localRotation;
            if (armR)
                _armRRest = armR.localRotation;
            ApplyDefinition();
        }

        public void ApplyDefinition()
        {
            // Двигается только прыжками (agent.Move), агент нужен для NavMesh и обхода соседей.
            _agent.speed = 0.01f;
            _agent.acceleration = 8f;
            _health.Configure(definition.hitsToKill, 0f);
        }

        public void OnSpawned()
        {
            ApplyDefinition();
            _held.Clear();
            _throwAt.Clear();
            _knockback = Vector3.zero;
            _nextRepath = 0f;
            _nextHop = Time.time + Random.Range(0.3f, 0.9f);
            _hopStart = float.NegativeInfinity;
            _target = null;
            _catchPose = 0f;
            _throwPose = 0f;
            _strafeSide = Random.value < 0.5f ? -1 : 1;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
        }

        public void OnDespawned() => ReleaseAll(Vector3.back);

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
            }
            bool hasTarget = _target != null && _target.IsAlive && GameSession.IsGameplayActive;
            bool frozen = _self.IsFrozen;

            if (_knockback.sqrMagnitude > 0.01f)
            {
                _agent.Move(_knockback * dt);
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-8f * dt));
            }
            if (!hasTarget || frozen)
                return;

            Vector3 toTarget = Flat(_target.Position - transform.position);
            float distance = toTarget.magnitude;
            Face(toTarget, dt);
            Hop(toTarget, distance, dt);
            TryCatchNearby();
            ThrowBack();
        }

        /// <summary>Прыжок на шесте: ближе, дальше или вбок — чтобы держать свою дистанцию.</summary>
        void Hop(Vector3 toTarget, float distance, float dt)
        {
            if (Hopping)
            {
                float speed = definition.hopDistance / Mathf.Max(0.05f, definition.hopTime);
                _agent.Move(_hopDirection * (speed * dt * GumSpot.EnemyMoveMultiplierAt(transform.position)
                                             * GroundZone.MoveMultiplierAt(transform.position)));
                return;
            }
            if (Time.time < _nextHop)
                return;
            _nextHop = Time.time + definition.hopInterval * Random.Range(0.85f, 1.15f);
            Vector3 forward = distance > 0.01f ? toTarget / distance : transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward) * _strafeSide;
            Vector3 direction;
            if (distance > definition.keepDistance + 1.5f)
                direction = forward + side * 0.3f;
            else if (distance < definition.keepDistance - 1.5f)
                direction = -forward + side * 0.3f;
            else
            {
                direction = side;
                if (Random.value < 0.3f)
                    _strafeSide = -_strafeSide;
            }
            _hopDirection = direction.normalized;
            _hopStart = Time.time;
        }

        void Face(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f)
                return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), definition.turnSpeed * dt);
        }

        // ---------- Ловля и бросок ----------

        /// <summary>Мяч пролетает рядом спереди — руки его хватают, даже если он летел мимо.</summary>
        void TryCatchNearby()
        {
            if (_held.Count >= definition.maxHeld)
                return;
            Vector3 chest = Chest;
            float radiusSqr = definition.catchRadius * definition.catchRadius;
            var balls = Ball.Active;
            for (int i = balls.Count - 1; i >= 0 && _held.Count < definition.maxHeld; i--)
            {
                var ball = balls[i];
                if (!CanCatch(ball))
                    continue;
                Vector3 toBall = ball.Position - chest;
                if (toBall.sqrMagnitude > radiusSqr)
                    continue;
                // Только мяч, который летит к чучелу, а не уже пролетевший мимо.
                if (Vector3.Dot(Flat(ball.Velocity), -Flat(toBall)) <= 0f)
                    continue;
                Catch(ball);
            }
        }

        bool CanCatch(Ball ball)
        {
            if (ball.State != BallState.Live || ball.IsPhantom || !ball.Team.IsHostileTo(Team.Enemy))
                return false;
            if (ball.Stats.Has(HitFlags.Charged) && !definition.catchCharged)
                return false;
            return InFront(ball.Position);
        }

        bool InFront(Vector3 point)
        {
            Vector3 to = Flat(point - transform.position);
            return to.sqrMagnitude < 1e-4f || Vector3.Angle(transform.forward, to) <= definition.catchHalfAngle;
        }

        void Catch(Ball ball)
        {
            ball.Stick(HandPosition(_held.Count));
            _held.Add(ball);
            _throwAt.Add(Time.time + definition.holdTime);
            _catchPose = 1f;
            GameEvents.PlaySound(SoundCue.ScarecrowCatch, ball.Position);
        }

        Vector3 HandPosition(int index)
        {
            if (hands != null && hands.Length > 0)
            {
                var hand = hands[index % hands.Length];
                if (hand)
                    return hand.position;
            }
            return Chest + transform.forward * 0.4f + transform.right * (index % 2 == 0 ? -0.5f : 0.5f);
        }

        void HoldBalls()
        {
            for (int i = _held.Count - 1; i >= 0; i--)
            {
                if (_held[i] == null || !_held[i].isActiveAndEnabled || _held[i].State != BallState.Stuck)
                {
                    _held.RemoveAt(i);
                    _throwAt.RemoveAt(i);
                }
            }
            for (int i = 0; i < _held.Count; i++)
                _held[i].HoldAt(HandPosition(i));
        }

        void ThrowBack()
        {
            if (_held.Count == 0 || Time.time < _throwAt[0])
                return;
            var ball = _held[0];
            _held.RemoveAt(0);
            _throwAt.RemoveAt(0);
            _throwPose = 1f;

            Vector3 origin = ball.Position;
            Vector3 aim = _target.AimPoint;
            Vector3 flat = Flat(aim - origin);
            aim += Flat(_target.Velocity) * (flat.magnitude / definition.throwSpeed * definition.lead);
            flat = Flat(aim - origin);
            float distance = Mathf.Max(0.5f, flat.magnitude);
            float time = distance / definition.throwSpeed;
            float up = (aim.y - origin.y + 0.5f * definition.throwGravity * time * time) / time;
            ball.Launch(new BallThrow
            {
                Origin = origin,
                Direction = flat / distance,
                Team = Team.Enemy,
                Thrower = gameObject,
                Stats = new ThrowStats
                {
                    Speed = definition.throwSpeed,
                    UpVelocity = up,
                    Gravity = definition.throwGravity,
                    Damage = definition.throwDamage,
                    Knockback = definition.throwKnockback,
                    Flags = definition.throwStrong ? HitFlags.Charged : HitFlags.None,
                },
            });
            GameEvents.PlaySound(definition.throwStrong ? SoundCue.ThrowCharged : SoundCue.SoldierThrow, origin);
        }

        void ReleaseAll(Vector3 direction)
        {
            for (int i = 0; i < _held.Count; i++)
            {
                var ball = _held[i];
                if (ball != null && ball.State == BallState.Stuck)
                {
                    Vector3 away = Flat(direction + Random.insideUnitSphere * 0.5f);
                    ball.Drop(ball.Position, away.normalized * 3f + Vector3.up * 3.5f);
                }
            }
            _held.Clear();
            _throwAt.Clear();
        }

        // ---------- Попадания ----------

        public BallContactResult OnBallContact(Ball ball, in RaycastHit hit)
        {
            if (_health.IsDead)
                return BallContactResult.PassThrough;
            // Спереди руки ловят — если есть свободная и мяч не пробивает их.
            if (_held.Count < definition.maxHeld && CanCatch(ball) && !_self.IsFrozen)
            {
                Catch(ball);
                return BallContactResult.Caught;
            }
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
                _knockback += Flat(hit.Direction) * (hit.Force * definition.knockbackScale);
            return true;
        }

        void OnDied(HitInfo hit)
        {
            ReleaseAll(hit.Direction);
            if (debrisPrefab)
            {
                var debris = PoolService.Spawn(debrisPrefab, transform.position, transform.rotation).GetComponent<Debris>();
                if (debris)
                    debris.Burst(hit.Direction, definition.debrisForce + hit.Force * 0.2f, Vector3.zero);
            }
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            GameEvents.PlaySound(SoundCue.SoldierPop, transform.position);
            GameFeel.Shake(0.3f);
            PoolService.Despawn(gameObject);
        }

        // ---------- Вид ----------

        void LateUpdate()
        {
            HoldBalls();
            float dt = Time.deltaTime;
            float hop = Mathf.Clamp01((Time.time - _hopStart) / Mathf.Max(0.05f, definition.hopTime));
            bool hopping = hop < 1f;
            if (visual)
                visual.localPosition = _visualRest + Vector3.up * (hopping ? Mathf.Sin(hop * Mathf.PI) * definition.hopHeight : 0f);
            if (body)
            {
                // На прыжке наклоняется вперёд, в полёте выпрямляется; с мячом в руках чуть откидывается.
                float lean = hopping ? Mathf.Sin(hop * Mathf.PI) * 10f : 0f;
                float sway = Mathf.Sin(Time.time * 1.7f + _strafeSide) * 3f;
                body.localRotation = _bodyRest * Quaternion.Euler(lean - _catchPose * 8f, 0f, sway);
            }
            _catchPose = Mathf.MoveTowards(_catchPose, _held.Count > 0 ? 0.6f : 0f, dt * 3f);
            _throwPose = Mathf.MoveTowards(_throwPose, 0f, dt * 4f);
            // Руки смыкаются спереди, когда держат мяч, и выстреливают вперёд на броске.
            float grab = _catchPose * 55f + _throwPose * 35f;
            if (armL)
                armL.localRotation = _armLRest * Quaternion.Euler(0f, grab, _throwPose * -20f);
            if (armR)
                armR.localRotation = _armRRest * Quaternion.Euler(0f, -grab, _throwPose * 20f);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
