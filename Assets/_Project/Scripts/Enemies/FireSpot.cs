using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    public sealed class FireSpot : MonoBehaviour, IPoolable
    {
        [SerializeField] float lifetime = 3f;
        [SerializeField] float radius = 0.6f;
        [SerializeField, Min(0)] int damage = 1;
        [SerializeField] float knockback = 5f;
        [Tooltip("Языки пламени и искры: перестают сыпать незадолго до конца, чтобы погаснуть к уходу в пул")]
        [SerializeField] ParticleSystem[] flames;
        [Tooltip("Выжженное пятно с углями: одна частица на всё время жизни, остывает сама (шейдер Bouncer/Scorch)")]
        [SerializeField] ParticleSystem scorch;
        [Tooltip("За сколько секунд до конца пламя перестаёт сыпать")]
        [SerializeField, Min(0f)] float flameStopBefore = 0.6f;

        float _spawnTime;
        bool _flamesStopped;

        public void OnSpawned()
        {
            _spawnTime = Time.time;
            _flamesStopped = false;
            if (scorch)
            {
                var main = scorch.main;
                main.startLifetime = lifetime;
                scorch.Clear(true);
                scorch.Play(true);
            }
            if (flames != null)
                foreach (var system in flames)
                {
                    if (!system)
                        continue;
                    system.Clear(true);
                    system.Play(true);
                }
            if (!NetHooks.IsGuest)
                NetHooks.MirrorSpawn?.Invoke(gameObject);
        }

        public void OnDespawned()
        {
            if (scorch)
                scorch.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (flames != null)
                foreach (var system in flames)
                    if (system)
                        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void Update()
        {
            float age = Time.time - _spawnTime;
            if (age >= lifetime)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            if (!_flamesStopped && age >= lifetime - flameStopBefore && flames != null)
            {
                _flamesStopped = true;
                foreach (var system in flames)
                    if (system)
                        system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (!GameSession.IsGameplayActive || NetHooks.IsGuest)
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
