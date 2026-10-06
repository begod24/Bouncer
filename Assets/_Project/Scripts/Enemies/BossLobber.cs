using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Большая неваляшка кидается маленькими: откидывается назад, на земле у игрока появляется круг, маленькая
    /// неваляшка летит дугой и, упав, бьёт всех в круге — дальше она обычный враг. Замах иногда оказывается финтом.
    /// После броска босс открыт (<see cref="RolyPolyEnemy.MarkOpen"/>).
    /// По сети кидается только у хозяина комнаты.
    /// </summary>
    [RequireComponent(typeof(RolyPolyEnemy))]
    public sealed class BossLobber : MonoBehaviour
    {
        [Tooltip("Кого кидает (обычная неваляшка)")]
        [SerializeField] GameObject minionPrefab;
        [Tooltip("Круг на земле там, куда упадёт")]
        [SerializeField] GroundMarker markerPrefab;
        [SerializeField] ParticleBurst landingDust;

        [Header("Бросок")]
        [SerializeField] float firstDelay = 5f;
        [SerializeField] Vector2 interval = new(6f, 9f);
        [SerializeField] float windup = 0.75f;
        [SerializeField] float flightTime = 1.05f;
        [SerializeField] float minRange = 4f;
        [SerializeField] float maxRange = 18f;
        [Tooltip("Высота над ногами босса, откуда вылетает маленькая")]
        [SerializeField] float launchHeight = 3f;
        [SerializeField] float landRadius = 1.4f;
        [SerializeField, Min(0)] int damage = 1;
        [SerializeField] float knockback = 11f;
        [Tooltip("Сколько секунд босс открыт после броска")]
        [SerializeField] float openAfter = 1.2f;

        RolyPolyEnemy _body;
        Health _health;
        float _nextLob;
        float _windupEnd;
        float _landAt;
        bool _winding;
        bool _feinted;
        Vector3 _landing;
        GroundMarker _marker;

        void Awake()
        {
            _body = GetComponent<RolyPolyEnemy>();
            _health = GetComponent<Health>();
        }

        void OnEnable()
        {
            _nextLob = Time.time + firstDelay;
            _winding = false;
            _landAt = 0f;
            _feinted = false;
        }

        void OnDisable() => HideMarker();

        void Update()
        {
            // По сети кидается и бьёт только настоящий босс — у хозяина комнаты; метку на земле гости видят от него.
            if (NetHooks.IsGuest)
                return;
            if (_landAt > 0f && Time.time >= _landAt)
                Land();
            if (_health.IsDead || !GameSession.IsGameplayActive || minionPrefab == null)
                return;

            var target = Targetable.FindNearest(transform.position, Team.Player);
            if (_winding)
            {
                if (Time.time < _windupEnd)
                    return;
                _winding = false;
                // Финт: откинулся — и не бросил; через миг бросит по-настоящему.
                if (!_feinted && Random.value < Danger.FeintChance)
                {
                    _feinted = true;
                    HideMarker();
                    _nextLob = Time.time + 0.45f;
                    return;
                }
                _feinted = false;
                Throw();
                return;
            }
            if (Time.time < _nextLob || _body.IsStunned || target == null || !target.IsAlive)
                return;
            Vector3 delta = target.Position - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < minRange || distance > maxRange)
                return;

            // Целится туда, где игрок будет к приземлению.
            Vector3 lead = target.Velocity;
            lead.y = 0f;
            _landing = target.Position + lead * (windup + flightTime) * 0.5f;
            _landing.y = 0f;
            _winding = true;
            _windupEnd = Time.time + windup;
            _body.Stun(windup);
            if (markerPrefab)
            {
                _marker = PoolService.Spawn(markerPrefab, _landing, Quaternion.identity);
                _marker.ShowCircle(_landing, landRadius, windup + flightTime);
            }
            GameEvents.PlaySound(SoundCue.RolyPolyChime, transform.position);
        }

        void Throw()
        {
            Vector3 origin = transform.position + Vector3.up * launchHeight;
            var minion = PoolService.Spawn(minionPrefab, origin, Quaternion.identity);
            if (minion.TryGetComponent(out Rigidbody body))
            {
                Vector3 gravity = Physics.gravity;
                Vector3 velocity = (_landing - origin - 0.5f * gravity * flightTime * flightTime) / flightTime;
                body.linearVelocity = velocity;
                body.angularVelocity = Random.insideUnitSphere * 6f;
            }
            if (minion.TryGetComponent(out RolyPolyEnemy roly))
                roly.Stun(flightTime + 0.4f);
            _landAt = Time.time + flightTime;
            _nextLob = Time.time + Random.Range(interval.x, interval.y) * EnemyScaling.BossCooldown;
            _body.MarkOpen(openAfter);
            GameEvents.PlaySound(SoundCue.ThrowCharged, origin);
        }

        void Land()
        {
            _landAt = 0f;
            HideMarker();
            GameEvents.PlaySound(SoundCue.AreaThud, _landing);
            GameFeel.Shake(0.35f);
            if (landingDust)
                PoolService.Spawn(landingDust, _landing + Vector3.up * 0.1f, Quaternion.identity).Play(1f);
            var target = Targetable.FindNearest(_landing, Team.Player, landRadius);
            if (target == null || !target.TryGetComponent(out IDamageable damageable))
                return;
            Vector3 away = target.Position - _landing;
            away.y = 0f;
            damageable.ApplyHit(new HitInfo
            {
                Damage = damage,
                Point = target.AimPoint,
                Direction = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward,
                Force = knockback,
                SourceTeam = Team.Enemy,
                Source = gameObject,
                Flags = HitFlags.Area,
            });
        }

        void HideMarker()
        {
            if (_marker && _marker.isActiveAndEnabled)
                _marker.Hide();
            _marker = null;
        }
    }
}
