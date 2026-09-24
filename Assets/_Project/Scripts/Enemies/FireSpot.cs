using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Огненное пятно на асфальте (след коня-огня): пару секунд горит и обжигает игрока, вставшего в него.
    /// Повторно не жжёт, пока у игрока неуязвимость после удара. Из пула.
    /// </summary>
    public sealed class FireSpot : MonoBehaviour, IPoolable
    {
        [SerializeField] float lifetime = 3f;
        [SerializeField] float radius = 0.6f;
        [SerializeField, Min(0)] int damage = 1;
        [SerializeField] float knockback = 5f;
        [Tooltip("Модель пятна: дрожит, как пламя, и сжимается к концу")]
        [SerializeField] Transform visual;
        [SerializeField] float flicker = 0.12f;

        float _spawnTime;
        Vector3 _scale = Vector3.one;

        void Awake()
        {
            if (visual)
                _scale = visual.localScale;
        }

        public void OnSpawned()
        {
            _spawnTime = Time.time;
            if (visual)
                visual.localScale = _scale;
        }

        public void OnDespawned() { }

        void Update()
        {
            float age = Time.time - _spawnTime;
            if (age >= lifetime)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            if (visual)
            {
                float fade = Mathf.Clamp01((lifetime - age) / 0.5f);
                float jitter = 1f + Mathf.Sin(Time.time * 23f + _spawnTime * 7f) * flicker;
                visual.localScale = _scale * (fade * jitter);
            }
            if (!GameSession.IsGameplayActive)
                return;
            var target = Targetable.FindNearest(transform.position, Team.Player, radius);
            if (target == null || !target.TryGetComponent(out IDamageable damageable))
                return;
            Vector3 away = target.Position - transform.position;
            away.y = 0f;
            damageable.ApplyHit(new HitInfo
            {
                Damage = damage,
                Point = target.Position,
                Direction = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward,
                Force = knockback,
                SourceTeam = Team.Enemy,
                Source = gameObject,
                Flags = HitFlags.Area,
            });
        }
    }
}
