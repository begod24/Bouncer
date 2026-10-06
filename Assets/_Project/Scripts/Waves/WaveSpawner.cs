using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Waves
{
    public sealed class WaveSpawner : MonoBehaviour
    {
        sealed class PendingGroup
        {
            public readonly List<Vector3> Positions = new();
            public readonly List<GameObject> Markers = new();
            public GameObject Prefab;
            public Quaternion Rotation;
            public float SpawnAt;
            public bool Boss;
            public bool Elite;
        }

        [SerializeField] WaveDefinition wave;
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] GameObject spawnMarkerPrefab;
        [Tooltip("Метка под элитным врагом: золотая, заметнее обычной")]
        [SerializeField] GameObject eliteMarkerPrefab;
        [SerializeField] bool spawning = true;

        [Header("Появление")]
        [SerializeField] float telegraphTime = 1f;
        [Tooltip("Не спавнить ближе этого расстояния к игроку")]
        [SerializeField] float minDistanceFromPlayer = 9f;
        [Tooltip("Расстояние между врагами в появляющейся группе")]
        [SerializeField] float groupSpacing = 1.3f;

        readonly List<PendingGroup> _pending = new();
        readonly Stack<PendingGroup> _freeGroups = new();
        readonly List<GameObject> _spawned = new();
        readonly List<Transform> _offScreen = new();
        readonly List<Transform> _onScreen = new();
        readonly List<SpawnBurst> _extraBursts = new();
        readonly List<bool> _extraDone = new();
        readonly List<Targetable> _elites = new();
        float[] _nextTrackTime;
        bool[] _burstDone;
        bool _bossSpawned;
        float _nextStrayCheck;

        public static event System.Action<bool, IReadOnlyList<Vector3>> GroupQueued;

        const float StrayDistance = 1.2f;
        const float StrayMaxHeight = 1.5f;

        public WaveDefinition Wave
        {
            get => wave;
            set
            {
                wave = value;
                _nextTrackTime = null;
                _burstDone = null;
            }
        }
        public Transform[] SpawnPoints => spawnPoints;
        public bool BurstsDone
        {
            get
            {
                if (wave == null)
                    return true;
                if (_burstDone == null || _burstDone.Length != wave.bursts.Count)
                    return wave.bursts.Count == 0;
                foreach (bool done in _burstDone)
                    if (!done)
                        return false;
                foreach (bool done in _extraDone)
                    if (!done)
                        return false;
                return true;
            }
        }
        public int PendingEnemies => PendingCount(null);
        public float TelegraphTime => telegraphTime;
        public float WaveTime { get; set; }
        public bool Spawning
        {
            get => spawning;
            set => spawning = value;
        }
        public int AliveCount => Targetable.CountAlive(Team.Enemy);
        public int MaxAlive => wave ? wave.MaxAliveAt(WaveTime) : 0;
        public bool InBreather => wave && wave.IsBreather(WaveTime);

        public bool EliteAlive
        {
            get
            {
                for (int i = _elites.Count - 1; i >= 0; i--)
                {
                    var elite = _elites[i];
                    if (!elite || !elite.gameObject.activeInHierarchy || !elite.IsAlive)
                        _elites.RemoveAt(i);
                }
                return _elites.Count > 0;
            }
        }
        public IReadOnlyList<SpawnTrack> Tracks => wave ? wave.tracks : System.Array.Empty<SpawnTrack>();

        void Start() => _ = EnemyFlowField.Instance;

        void OnEnable() => GameEvents.SpawnRequested += OnSpawnRequested;

        void OnDisable() => GameEvents.SpawnRequested -= OnSpawnRequested;

        void OnSpawnRequested(SpawnRequest request)
        {
            if (!GameSession.IsGameplayActive || NetHooks.IsGuest)
                return;
            QueueGroupAt(request.Prefab, request.Count, request.Line ? GroupLayout.Line : GroupLayout.Cluster, request.Position);
        }

        void Update()
        {
            UpdatePending();
            if (wave == null || !GameSession.IsGameplayActive || Online.WavesHeld)
                return;
            if (NetHooks.IsGuest)
            {
                WaveTime += Time.deltaTime;
                return;
            }
            CheckVictory();
            EnsureSchedule();
            WaveTime += Time.deltaTime;
            ReturnStrays();
            if (!spawning)
                return;
            HoldTrackClocks();
            RunBursts();
            RunTracks();
        }

        void HoldTrackClocks()
        {
            float rate = InBreather ? 0f : EliteAlive ? 1f / Mathf.Max(1f, wave.eliteSlowdown) : 1f;
            float lag = Time.deltaTime * (1f - rate);
            if (lag <= 0f)
                return;
            for (int i = 0; i < wave.tracks.Count; i++)
                if (WaveTime >= wave.tracks[i].from)
                    _nextTrackTime[i] += lag;
        }

        void ReturnStrays()
        {
            if (Time.time < _nextStrayCheck)
                return;
            _nextStrayCheck = Time.time + 1f;
            var all = Targetable.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var target = all[i];
                if (target.Team != Team.Enemy || !target.IsAlive)
                    continue;
                Vector3 position = target.Position;
                if (position.y > StrayMaxHeight)
                    continue;
                Vector3 probe = new(position.x, 0.1f, position.z);
                if (NavMesh.SamplePosition(probe, out NavMeshHit near, StrayDistance, NavMesh.AllAreas))
                    continue;
                if (!NavMesh.SamplePosition(probe, out NavMeshHit back, 30f, NavMesh.AllAreas))
                    continue;
                Vector3 inside = back.position;
                if (target.TryGetComponent(out NavMeshAgent agent) && agent.enabled)
                    agent.Warp(inside);
                if (target.TryGetComponent(out Rigidbody body))
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.position = inside + Vector3.up * 0.05f;
                }
                target.transform.position = inside + Vector3.up * 0.05f;
            }
        }

        void CheckVictory()
        {
            if (!_bossSpawned || BossSplit.Alive.Count > 0)
                return;
            foreach (var group in _pending)
                if (group.Boss)
                    return;
            _bossSpawned = false;
            GameEvents.RaiseBossDefeated();
        }

        void EnsureSchedule()
        {
            if (_nextTrackTime == null || _nextTrackTime.Length != wave.tracks.Count)
            {
                _nextTrackTime = new float[wave.tracks.Count];
                for (int i = 0; i < _nextTrackTime.Length; i++)
                    _nextTrackTime[i] = wave.tracks[i].from + wave.tracks[i].firstDelay;
            }
            if (_burstDone == null || _burstDone.Length != wave.bursts.Count)
            {
                _burstDone = new bool[wave.bursts.Count];
                BuildExtraElites();
            }
        }

        void BuildExtraElites()
        {
            _extraBursts.Clear();
            _extraDone.Clear();
            int extra = Danger.ExtraElites + Mathf.Max(0, RunState.PlayerCount - 1) / 2;
            if (extra <= 0)
                return;
            SpawnBurst first = null, second = null;
            foreach (var burst in wave.bursts)
            {
                if (!burst.elite || burst.boss)
                    continue;
                if (first == null)
                    first = burst;
                else if (second == null)
                    second = burst;
            }
            if (first == null)
                return;
            for (int i = 0; i < extra; i++)
            {
                float time = second != null ? (first.time + second.time) * 0.5f : first.time + 45f;
                _extraBursts.Add(new SpawnBurst
                {
                    name = "Лишняя элитка",
                    prefab = first.prefab,
                    variants = first.variants,
                    time = time + i * 20f,
                    count = 1,
                    elite = true,
                    layout = first.layout,
                });
                _extraDone.Add(false);
            }
        }

        void RunTracks()
        {
            if (InBreather)
                return;
            int cap = MaxAlive;
            int total = AliveCount + PendingCount(null);
            for (int i = 0; i < wave.tracks.Count; i++)
            {
                var track = wave.tracks[i];
                if (track.prefab == null || WaveTime < track.from || WaveTime > track.to || WaveTime < _nextTrackTime[i])
                    continue;

                int room = Mathf.Min(track.maxAlive - CountAlive(track.prefab) - PendingCount(track.prefab), cap - total);
                if (room <= 0)
                {
                    _nextTrackTime[i] = WaveTime + 0.5f;
                    continue;
                }
                int size = Mathf.Min(track.GroupSizeAt(WaveTime), room);
                if (QueueGroup(track.prefab, size, track.layout))
                    total += size;
                _nextTrackTime[i] = WaveTime + track.IntervalAt(WaveTime);
            }
        }

        void RunBursts()
        {
            for (int i = 0; i < wave.bursts.Count; i++)
            {
                var burst = wave.bursts[i];
                if (_burstDone[i] || WaveTime < burst.time)
                    continue;
                _burstDone[i] = true;
                QueueGroup(burst.PickPrefab(), burst.count, burst.layout, burst.boss, burst.elite);
            }
            for (int i = 0; i < _extraBursts.Count; i++)
            {
                var burst = _extraBursts[i];
                if (_extraDone[i] || WaveTime < burst.time)
                    continue;
                _extraDone[i] = true;
                QueueGroup(burst.PickPrefab(), burst.count, burst.layout, false, true);
            }
        }

        public void SpawnBurstNow(int index)
        {
            if (wave == null || index < 0 || index >= wave.bursts.Count)
                return;
            var burst = wave.bursts[index];
            QueueGroup(burst.PickPrefab(), burst.count, burst.layout, burst.boss, burst.elite);
        }

        public IReadOnlyList<SpawnBurst> Bursts => wave ? wave.bursts : System.Array.Empty<SpawnBurst>();

        public bool QueueGroup(GameObject prefab, int count, GroupLayout layout, bool boss = false, bool elite = false)
        {
            if (prefab == null || count <= 0)
                return false;
            return QueueGroupAt(prefab, count, layout, PickSpawnPoint(prefab), boss, elite);
        }

        public bool QueueGroupAt(GameObject prefab, int count, GroupLayout layout, Vector3 point, bool boss = false, bool elite = false)
        {
            if (prefab == null || count <= 0)
                return false;

            Vector3 facing = FacingToPlayers(point);
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            var group = _freeGroups.Count > 0 ? _freeGroups.Pop() : new PendingGroup();
            group.Prefab = prefab;
            group.Rotation = Quaternion.LookRotation(facing);
            group.SpawnAt = Time.time + telegraphTime;
            group.Boss = boss;
            group.Elite = elite;
            var marker = elite && eliteMarkerPrefab ? eliteMarkerPrefab : spawnMarkerPrefab;

            for (int i = 0; i < count; i++)
            {
                Vector3 offset = layout == GroupLayout.Line
                    ? right * ((i - (count - 1) * 0.5f) * groupSpacing)
                    : Sunflower(i) * groupSpacing;
                Vector3 position = point + offset;
                position = NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : point;
                group.Positions.Add(position);
                if (marker)
                    group.Markers.Add(PoolService.Spawn(marker, position, Quaternion.identity));
            }
            _pending.Add(group);
            GameEvents.PlaySound(elite ? SoundCue.EliteSpawn : SoundCue.SpawnWarning, point);
            GroupQueued?.Invoke(elite, group.Positions);
            return true;
        }

        public void ShowMarkers(bool elite, IReadOnlyList<Vector3> positions)
        {
            var group = _freeGroups.Count > 0 ? _freeGroups.Pop() : new PendingGroup();
            group.Prefab = null;
            group.SpawnAt = Time.time + telegraphTime;
            group.Elite = elite;
            var marker = elite && eliteMarkerPrefab ? eliteMarkerPrefab : spawnMarkerPrefab;
            foreach (var position in positions)
            {
                group.Positions.Add(position);
                if (marker)
                    group.Markers.Add(PoolService.Spawn(marker, position, Quaternion.identity));
            }
            _pending.Add(group);
        }

        public void SpawnTrackNow(int index)
        {
            if (wave == null || index < 0 || index >= wave.tracks.Count)
                return;
            var track = wave.tracks[index];
            QueueGroup(track.prefab, track.GroupSizeAt(WaveTime), track.layout);
        }

        public void DespawnAll()
        {
            ClearPending();
            var hit = new HitInfo { Damage = 999, Direction = Vector3.up, SourceTeam = Team.Neutral, Flags = HitFlags.Despawn };
            var all = Targetable.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (i >= all.Count)
                    continue;
                var target = all[i];
                if (target.Team == Team.Enemy && target.Health != null && !target.Health.IsDead)
                    target.Health.Kill(hit);
            }
        }

        public void KillAll()
        {
            ClearPending();
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
                var group = _pending[i];
                float left = group.SpawnAt - Time.time;
                float scale = 0.6f + 0.8f * Mathf.Clamp01(left / Mathf.Max(0.01f, telegraphTime));
                foreach (var marker in group.Markers)
                    marker.transform.localScale = Vector3.one * scale;
                if (left > 0f)
                    continue;

                _pending.RemoveAt(i);
                SpawnGroup(group);
                Recycle(group);
            }
        }

        void SpawnGroup(PendingGroup group)
        {
            if (group.Prefab == null)
                return;
            if (group.Boss)
                _bossSpawned = true;
            _spawned.Clear();
            foreach (var position in group.Positions)
                _spawned.Add(PoolService.Spawn(group.Prefab, position, group.Rotation));
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i].TryGetComponent(out IGroupMember member))
                    member.OnGroupSpawned(_spawned, i);
            if (group.Elite)
                foreach (var spawned in _spawned)
                {
                    EliteAffix.Assign(spawned, Random.value < Danger.EliteAffixChance ? EliteAffix.RandomKind() : AffixKind.None);
                    if (spawned.TryGetComponent(out Targetable target))
                        _elites.Add(target);
                }
        }

        void ClearPending()
        {
            foreach (var group in _pending)
                Recycle(group);
            _pending.Clear();
        }

        void Recycle(PendingGroup group)
        {
            foreach (var marker in group.Markers)
                PoolService.Despawn(marker);
            group.Markers.Clear();
            group.Positions.Clear();
            group.Prefab = null;
            group.Boss = false;
            group.Elite = false;
            _freeGroups.Push(group);
        }

        Vector3 PickSpawnPoint(GameObject prefab)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return transform.position;

            bool darkOnly = prefab && prefab.TryGetComponent(out SpawnPreference preference) && preference.DarkOnly && HasDarkPoint();
            var camera = Camera.main;
            float minSqr = minDistanceFromPlayer * minDistanceFromPlayer;
            Transform farthest = null;
            float farthestSqr = -1f;
            _offScreen.Clear();
            _onScreen.Clear();
            foreach (var point in spawnPoints)
            {
                if (!point || (darkOnly && LightZone.IsLit(point.position)))
                    continue;
                float sqr = NearestPlayerSqr(point.position);
                if (sqr > farthestSqr)
                {
                    farthestSqr = sqr;
                    farthest = point;
                }
                if (sqr < minSqr)
                    continue;
                if (camera && IsOnScreen(camera, point.position))
                    _onScreen.Add(point);
                else
                    _offScreen.Add(point);
            }

            var candidates = _offScreen.Count > 0 ? _offScreen : _onScreen;
            if (candidates.Count > 0)
                return candidates[Random.Range(0, candidates.Count)].position;
            return farthest ? farthest.position : transform.position;
        }

        bool HasDarkPoint()
        {
            foreach (var point in spawnPoints)
                if (point && !LightZone.IsLit(point.position))
                    return true;
            return false;
        }

        static bool IsOnScreen(Camera camera, Vector3 position)
        {
            Vector3 viewport = camera.WorldToViewportPoint(position);
            return viewport.z > 0f && viewport.x > -0.05f && viewport.x < 1.05f && viewport.y > -0.05f && viewport.y < 1.05f;
        }

        static Vector3 FacingToPlayers(Vector3 from)
        {
            var nearest = Targetable.FindNearest(from, Team.Player);
            Vector3 direction = nearest ? nearest.Position - from : -from;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
        }

        static Vector3 Sunflower(int index)
        {
            float radius = Mathf.Sqrt(index) * 0.75f;
            float angle = index * 2.39996f;
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
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

        static int CountAlive(GameObject prefab)
        {
            int count = 0;
            foreach (var target in Targetable.All)
                if (target.Team == Team.Enemy && target.IsAlive && PooledObject.IsSpawnedFrom(target.gameObject, prefab))
                    count++;
            return count;
        }

        int PendingCount(GameObject prefab)
        {
            int count = 0;
            foreach (var group in _pending)
                if (prefab == null || group.Prefab == prefab)
                    count += group.Positions.Count;
            return count;
        }
    }
}
