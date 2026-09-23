using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Waves
{
    /// <summary>
    /// Прототипный спавнер: враги появляются у краёв арены всё чаще, лимит живых растёт со временем.
    /// Перед появлением на полу мигает метка. Полноценные волны на ScriptableObject — этап 2.
    /// </summary>
    public sealed class SimpleWaveSpawner : MonoBehaviour
    {
        struct PendingSpawn
        {
            public Vector3 Position;
            public float SpawnAt;
            public GameObject Marker;
        }

        [SerializeField] GameObject enemyPrefab;
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] GameObject spawnMarkerPrefab;

        [Header("Темп")]
        [SerializeField] bool spawning = true;
        [SerializeField, Min(0)] int initialEnemies = 3;
        [SerializeField] float firstSpawnDelay = 1f;
        [SerializeField] float spawnInterval = 6f;
        [Tooltip("После каждого спавна интервал умножается на это число")]
        [SerializeField, Range(0.5f, 1f)] float intervalMultiplier = 0.96f;
        [SerializeField] float minInterval = 2.2f;
        [SerializeField, Min(1)] int maxAlive = 8;
        [SerializeField, Min(1)] int maxAliveCap = 20;
        [Tooltip("Каждые N секунд лимит живых врагов растёт на 1")]
        [SerializeField] float maxAliveGrowthEvery = 30f;

        [Header("Появление")]
        [SerializeField] float telegraphTime = 1f;
        [Tooltip("Не спавнить ближе этого расстояния к игроку")]
        [SerializeField] float minDistanceFromPlayer = 6f;

        readonly List<PendingSpawn> _pending = new();
        readonly List<Vector3> _candidates = new();
        float _nextSpawn;
        float _currentInterval;
        float _elapsed;
        bool _initialQueued;

        public GameObject EnemyPrefab => enemyPrefab;
        public int AliveCount => Targetable.CountAlive(Team.Enemy);
        public int EffectiveMaxAlive =>
            Mathf.Min(maxAliveCap, maxAlive + Mathf.FloorToInt(_elapsed / Mathf.Max(1f, maxAliveGrowthEvery)));

        public bool Spawning
        {
            get => spawning;
            set => spawning = value;
        }

        public float SpawnInterval
        {
            get => spawnInterval;
            set
            {
                spawnInterval = Mathf.Max(0.5f, value);
                _currentInterval = Mathf.Min(_currentInterval, spawnInterval);
            }
        }

        public int MaxAlive
        {
            get => maxAlive;
            set => maxAlive = Mathf.Max(1, value);
        }

        void Start()
        {
            _currentInterval = spawnInterval;
            _nextSpawn = Time.time + firstSpawnDelay;
        }

        void Update()
        {
            UpdatePending();
            if (!GameSession.IsGameplayActive)
                return;
            _elapsed += Time.deltaTime;
            if (!spawning || Time.time < _nextSpawn)
                return;

            if (!_initialQueued)
            {
                _initialQueued = true;
                for (int i = 0; i < initialEnemies; i++)
                    QueueSpawn();
            }
            else if (AliveCount + _pending.Count < EffectiveMaxAlive)
            {
                QueueSpawn();
            }
            _nextSpawn = Time.time + _currentInterval;
            _currentInterval = Mathf.Max(minInterval, _currentInterval * intervalMultiplier);
        }

        public void QueueSpawn() => QueueSpawnAt(PickSpawnPoint());

        public void QueueSpawnAt(Vector3 position)
        {
            var marker = spawnMarkerPrefab ? PoolService.Spawn(spawnMarkerPrefab, position, Quaternion.identity) : null;
            _pending.Add(new PendingSpawn { Position = position, SpawnAt = Time.time + telegraphTime, Marker = marker });
        }

        public void KillAll()
        {
            var hit = new HitInfo { Damage = 999, Direction = Vector3.forward, SourceTeam = Team.Player };
            var all = Targetable.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (i >= all.Count)
                    continue;
                var target = all[i];
                if (target.Team == Team.Enemy && target.Health != null)
                    target.Health.Kill(hit);
            }
        }

        void UpdatePending()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var pending = _pending[i];
                float left = pending.SpawnAt - Time.time;
                if (pending.Marker)
                {
                    float k = Mathf.Clamp01(left / Mathf.Max(0.01f, telegraphTime));
                    pending.Marker.transform.localScale = Vector3.one * (0.6f + 0.8f * k);
                }
                if (left > 0f)
                    continue;

                _pending.RemoveAt(i);
                if (pending.Marker)
                    PoolService.Despawn(pending.Marker);
                if (enemyPrefab)
                    PoolService.Spawn(enemyPrefab, pending.Position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
        }

        Vector3 PickSpawnPoint()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return transform.position;

            float minSqr = minDistanceFromPlayer * minDistanceFromPlayer;
            Vector3 farthest = transform.position;
            float farthestSqr = -1f;
            _candidates.Clear();
            foreach (var point in spawnPoints)
            {
                if (!point)
                    continue;
                float nearestSqr = NearestPlayerSqr(point.position);
                if (nearestSqr > farthestSqr)
                {
                    farthestSqr = nearestSqr;
                    farthest = point.position;
                }
                if (nearestSqr >= minSqr)
                    _candidates.Add(point.position);
            }
            return _candidates.Count > 0 ? _candidates[Random.Range(0, _candidates.Count)] : farthest;
        }

        static float NearestPlayerSqr(Vector3 position)
        {
            float best = float.PositiveInfinity;
            foreach (var target in Targetable.All)
            {
                if (target.Team != Team.Player || !target.IsAlive)
                    continue;
                Vector3 delta = target.Position - position;
                delta.y = 0f;
                best = Mathf.Min(best, delta.sqrMagnitude);
            }
            return best;
        }
    }
}
