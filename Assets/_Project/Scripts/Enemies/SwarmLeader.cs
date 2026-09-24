using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Пупс в чепчике с соской — вожак роя. Пока он жив, пупсы рядом бегают быстрее, а сам он время от времени
    /// хнычет и созывает новых пупсов. Выбей вожака — рой снова обычный.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class SwarmLeader : MonoBehaviour, IPoolable
    {
        static readonly List<SwarmLeader> s_active = new();

        [Header("Рой рядом бегает быстрее")]
        [SerializeField] float auraRadius = 7f;
        [Tooltip("Прибавка к скорости пупсов: 0.35 = +35%")]
        [SerializeField] float speedBonus = 0.35f;

        [Header("Хнычет — зовёт новых пупсов")]
        [SerializeField] GameObject followerPrefab;
        [SerializeField, Min(1)] int followersPerCry = 3;
        [SerializeField] float cryInterval = 8f;
        [Tooltip("Сколько раз за жизнь зовёт подмогу")]
        [SerializeField, Min(0)] int maxCries = 4;
        [SerializeField] float callRadius = 2.5f;

        Health _health;
        float _nextCry;
        int _cries;

        /// <summary>Во сколько раз быстрее бегает пупс в этой точке (рядом с живым вожаком).</summary>
        public static float SpeedMultiplierAt(Vector3 position)
        {
            foreach (var leader in s_active)
            {
                if (leader._health.IsDead)
                    continue;
                Vector3 delta = leader.transform.position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= leader.auraRadius * leader.auraRadius)
                    return 1f + leader.speedBonus;
            }
            return 1f;
        }

        void Awake() => _health = GetComponent<Health>();

        void OnEnable() => s_active.Add(this);

        void OnDisable() => s_active.Remove(this);

        public void OnSpawned()
        {
            _nextCry = Time.time + cryInterval * 0.5f;
            _cries = 0;
        }

        public void OnDespawned() { }

        void Update()
        {
            if (followerPrefab == null || _health.IsDead || _cries >= maxCries || Time.time < _nextCry || !GameSession.IsGameplayActive)
                return;
            _cries++;
            _nextCry = Time.time + cryInterval;
            GameEvents.PlaySound(SoundCue.PupsikSqueak, transform.position);
            for (int i = 0; i < followersPerCry; i++)
            {
                float angle = (i / (float)followersPerCry + Random.value * 0.2f) * Mathf.PI * 2f;
                Vector3 position = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * callRadius;
                if (UnityEngine.AI.NavMesh.SamplePosition(position, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                    position = hit.position;
                PoolService.Spawn(followerPrefab, position, transform.rotation);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_active.Clear();
    }
}
