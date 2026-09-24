using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Разовый эффект из частиц (взрыв цыплёнка): при выдаче из пула все дочерние системы частиц играют заново,
    /// через lifetime эффект возвращается в пул. Масштаб задаёт тот, кто его вызвал (<see cref="Play"/>).
    /// </summary>
    public sealed class ParticleBurst : MonoBehaviour, IPoolable
    {
        [Tooltip("Через сколько секунд эффект уходит в пул — не меньше самой долгой частицы")]
        [SerializeField, Min(0.1f)] float lifetime = 1.6f;

        ParticleSystem[] _systems;
        float _spawnTime;

        void Awake() => _systems = GetComponentsInChildren<ParticleSystem>(true);

        public void OnSpawned()
        {
            _spawnTime = Time.time;
            transform.localScale = Vector3.one;
            foreach (var system in _systems)
            {
                system.Clear(true);
                system.Play(true);
            }
        }

        public void OnDespawned()
        {
            foreach (var system in _systems)
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>Эффект побольше или поменьше (взрыв петушка крупнее).</summary>
        public void Play(float scale) => transform.localScale = Vector3.one * Mathf.Max(0.1f, scale);

        void Update()
        {
            if (Time.time - _spawnTime >= lifetime)
                PoolService.Despawn(gameObject);
        }
    }
}
