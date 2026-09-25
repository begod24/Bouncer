using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Ложное чучело из «Пряток»: вылезает из земли вместе с настоящим «Тем, кто в сумерках» и выглядит так же,
    /// только глаза не горят. Попали — рассыпается соломой, и из неё вылетают вороны. Когда настоящего нашли
    /// (или он вышел сам), ложные осыпаются без ворон.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(Targetable))]
    public sealed class ScarecrowDecoy : MonoBehaviour, IBallTarget, IDamageable, IPoolable
    {
        [Tooltip("Корень модели: вылезает из-под земли")]
        [SerializeField] Transform model;
        [Tooltip("Та же поза, что у настоящего в «Прятках»: руки чуть в стороны, голова набок")]
        [SerializeField] Transform head;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;
        [SerializeField] ParticleBurst strawPrefab;
        [SerializeField] ParticleBurst smokePrefab;
        [SerializeField] HitFlash hitFlash;
        [Tooltip("Медленно поворачивается к игроку, градусов в секунду")]
        [SerializeField] float turnSpeed = 35f;
        [Tooltip("На сколько метров уходит под землю, пока не вылезло")]
        [SerializeField] float depth = 5.5f;

        Health _health;
        Targetable _self;
        DuskBoss _boss;
        float _riseTime = 0.8f;
        float _start;
        Vector3 _modelRest;
        Quaternion _headRest, _armLRest, _armRRest;

        void Awake()
        {
            _health = GetComponent<Health>();
            _self = GetComponent<Targetable>();
            _self.Team = Team.Enemy;
            _health.Died += OnDied;
            if (model)
                _modelRest = model.localPosition;
            _headRest = head ? head.localRotation : Quaternion.identity;
            _armLRest = armL ? armL.localRotation : Quaternion.identity;
            _armRRest = armR ? armR.localRotation : Quaternion.identity;
        }

        public void OnSpawned()
        {
            _health.Configure(1, 0f);
            _boss = null;
            _start = Time.time;
        }

        public void OnDespawned() => _boss = null;

        /// <summary>Встало на «Прятках»: вылезает из земли за riseTime секунд.</summary>
        public void Init(DuskBoss boss, float riseTime)
        {
            _boss = boss;
            _riseTime = Mathf.Max(0.05f, riseTime);
            _start = Time.time;
            if (smokePrefab)
                PoolService.Spawn(smokePrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity).Play(1.4f);
        }

        /// <summary>
        /// Настоящего нашли — ложное осыпается само, без ворон. Зовёт его сам босс, поэтому обратно ему
        /// не сообщает: он как раз перебирает свой список чучел.
        /// </summary>
        public void Collapse()
        {
            _boss = null;
            if (!_health.IsDead)
                _health.Kill(new HitInfo { Damage = 999, Direction = Vector3.up, SourceTeam = Team.Neutral, Flags = HitFlags.Despawn });
        }

        void Update()
        {
            var target = Targetable.FindNearest(transform.position, Team.Player);
            if (target == null || !GameSession.IsGameplayActive)
                return;
            Vector3 to = target.Position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), turnSpeed * Time.deltaTime);
        }

        void LateUpdate()
        {
            float rise = Mathf.Clamp01((Time.time - _start) / _riseTime);
            if (model)
                model.localPosition = _modelRest + Vector3.down * ((1f - rise) * depth);
            if (head)
                head.localRotation = _headRest * Quaternion.Euler(0f, 0f, 12f);
            if (armL)
                armL.localRotation = _armLRest * Quaternion.Euler(0f, 0f, -20f);
            if (armR)
                armR.localRotation = _armRRest * Quaternion.Euler(0f, 0f, 20f);
        }

        public BallContactResult OnBallContact(Ball ball, in RaycastHit hit)
        {
            if (_health.IsDead)
                return BallContactResult.PassThrough;
            Vector3 direction = ball.Velocity;
            direction.y = 0f;
            ApplyHit(new HitInfo
            {
                Damage = Mathf.Max(1, ball.Stats.Damage),
                Point = hit.point,
                Direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : -transform.forward,
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
            if (hitFlash)
                hitFlash.Flash(Color.white, 0.1f);
            _health.TryDamage(hit);
            return true;
        }

        void OnDied(HitInfo hit)
        {
            bool byPlayer = !hit.Has(HitFlags.Despawn);
            Vector3 center = transform.position + Vector3.up * 2f;
            if (strawPrefab)
                PoolService.Spawn(strawPrefab, center, Quaternion.identity).Play(2f);
            if (smokePrefab)
                PoolService.Spawn(smokePrefab, center, Quaternion.identity).Play(1.6f);
            GameEvents.PlaySound(SoundCue.DecoyBurst, center);
            if (byPlayer)
                GameFeel.Shake(0.25f);
            var boss = _boss;
            _boss = null;
            if (boss)
                boss.OnDecoyBroken(this, byPlayer);
            GameEvents.RaiseEnemyKilled(gameObject, hit);
            PoolService.Despawn(gameObject);
        }
    }
}
