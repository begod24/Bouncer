using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Огромный мяч по свистку Физрука («Мяч в игре!»): катится по коробке, отскакивает от бортов без потери
    /// скорости и сбивает всех на пути — игрока и врагов. Коллайдер на слое окружения: мячи отскакивают
    /// от него, как от стены. Через несколько секунд сдувается и исчезает. Из пула.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class GiantBall : MonoBehaviour, IPoolable
    {
        const float Skin = 0.02f;

        [SerializeField, Min(0.3f)] float radius = 1.1f;
        [SerializeField] float speed = 10f;
        [Tooltip("Сколько секунд катается, прежде чем сдуться")]
        [SerializeField] float lifetime = 8f;
        [SerializeField] float deflateTime = 0.6f;
        [Tooltip("Урон игроку")]
        [SerializeField, Min(0)] int playerDamage = 1;
        [Tooltip("Урон врагу: мячом можно сбить и их")]
        [SerializeField, Min(0)] int enemyDamage = 2;
        [SerializeField] float knockback = 13f;
        [Tooltip("Одного и того же не бьёт чаще, чем раз в столько секунд")]
        [SerializeField] float sameTargetCooldown = 0.9f;
        [Tooltip("Модель мяча: центр в центре мяча, крутится по ходу качения")]
        [SerializeField] Transform visual;

        readonly RaycastHit[] _hits = new RaycastHit[8];
        readonly Collider[] _touch = new Collider[16];
        readonly Dictionary<IDamageable, float> _lastHit = new();
        readonly List<IDamageable> _hitThisStep = new();
        SphereCollider _collider;
        Vector3 _velocity;
        float _spawnTime;

        void Awake()
        {
            _collider = GetComponent<SphereCollider>();
            _collider.radius = radius;
            _collider.center = Vector3.up * radius;
        }

        public void OnSpawned()
        {
            _spawnTime = Time.time;
            _velocity = Vector3.zero;
            _lastHit.Clear();
            _collider.enabled = true;
            transform.localScale = Vector3.one;
        }

        public void OnDespawned() => _lastHit.Clear();

        /// <summary>Покатить мяч в эту сторону.</summary>
        public void Launch(Vector3 direction)
        {
            direction.y = 0f;
            _velocity = (direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward) * speed;
            GameEvents.PlaySound(SoundCue.AreaThud, transform.position);
        }

        void FixedUpdate()
        {
            float age = Time.time - _spawnTime;
            if (age >= lifetime + deflateTime)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            if (age >= lifetime)
            {
                // Сдувается: замедляется, больше никого не бьёт.
                _collider.enabled = false;
                _velocity *= 0.9f;
            }
            if (GameFeel.Paused || !GameSession.IsGameplayActive)
                return;
            Move(Time.fixedDeltaTime);
            if (age < lifetime)
                Knock();
        }

        /// <summary>Шаг по полу с зеркальными отскоками от стен, как у мяча на бортах коробки.</summary>
        void Move(float dt)
        {
            Vector3 position = transform.position;
            float remaining = _velocity.magnitude * dt;
            for (int i = 0; i < 3 && remaining > 1e-4f; i++)
            {
                Vector3 direction = _velocity.normalized;
                Vector3 origin = position + Vector3.up * radius;
                int count = Physics.SphereCastNonAlloc(origin, radius * 0.95f, direction, _hits, remaining + Skin,
                    Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);
                float best = float.PositiveInfinity;
                Vector3 normal = Vector3.zero;
                for (int k = 0; k < count; k++)
                {
                    var hit = _hits[k];
                    // Себя и пол не считаем: катится по полу, а не упирается в него.
                    if (hit.collider == _collider || hit.distance <= 0f || hit.normal.y > 0.6f)
                        continue;
                    if (hit.distance < best)
                    {
                        best = hit.distance;
                        normal = hit.normal;
                    }
                }
                if (float.IsPositiveInfinity(best))
                {
                    position += direction * remaining;
                    break;
                }
                float travel = Mathf.Max(0f, best - Skin);
                position += direction * travel;
                remaining -= travel;
                normal.y = 0f;
                if (normal.sqrMagnitude < 1e-4f)
                    break;
                _velocity = Vector3.Reflect(_velocity, normal.normalized);
                _velocity.y = 0f;
                GameEvents.PlaySound(SoundCue.AreaThud, position);
                GameFeel.Shake(0.15f);
            }
            position.y = 0f;
            transform.position = position;
        }

        /// <summary>Сбивает всех, кого коснулся: игрока и врагов — каждого не чаще раза в sameTargetCooldown.</summary>
        void Knock()
        {
            Vector3 center = transform.position + Vector3.up * radius;
            int count = Physics.OverlapSphereNonAlloc(center, radius + 0.3f, _touch, Layers.PlayerMask | Layers.EnemyMask,
                QueryTriggerInteraction.Ignore);
            _hitThisStep.Clear();
            for (int i = 0; i < count; i++)
            {
                var other = _touch[i];
                var target = other.GetComponentInParent<IDamageable>();
                if (target == null || _hitThisStep.Contains(target))
                    continue;
                _hitThisStep.Add(target);
                if (_lastHit.TryGetValue(target, out float last) && Time.time - last < sameTargetCooldown)
                    continue;
                _lastHit[target] = Time.time;
                bool player = other.gameObject.layer == Layers.Player;
                Vector3 away = other.transform.position - center;
                away.y = 0f;
                Vector3 push = (_velocity.normalized + (away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.zero)).normalized;
                target.ApplyHit(new HitInfo
                {
                    Damage = player ? playerDamage : enemyDamage,
                    Point = other.ClosestPoint(center),
                    Direction = push.sqrMagnitude > 1e-4f ? push : Vector3.forward,
                    Force = knockback,
                    // Враги от мяча страдают «от игрока»: за них падают монетки, как за подорванных цыплёнком.
                    SourceTeam = player ? Team.Enemy : Team.Player,
                    Source = gameObject,
                    Flags = HitFlags.Charged | HitFlags.Area,
                });
            }
        }

        void LateUpdate()
        {
            if (!visual)
                return;
            float dt = Time.deltaTime;
            Vector3 flat = new(_velocity.x, 0f, _velocity.z);
            float distance = flat.magnitude * dt;
            if (distance > 1e-5f)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, flat.normalized);
                visual.Rotate(axis, distance / radius * Mathf.Rad2Deg, Space.World);
            }
            float age = Time.time - _spawnTime;
            if (age >= lifetime)
            {
                // Сдувается и оседает на пол: корень мяча стоит на полу, поэтому сжатие по высоте прижимает к земле.
                float k = 1f - Mathf.Clamp01((age - lifetime) / Mathf.Max(0.01f, deflateTime));
                float spread = 1f + (1f - k) * 0.3f;
                transform.localScale = new Vector3(spread, Mathf.Max(0.05f, k), spread);
            }
        }
    }
}
