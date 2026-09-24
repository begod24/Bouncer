using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Waves
{
    /// <summary>
    /// Ведёт волны по <see cref="WaveDefinition"/>: дорожки врагов с растущим темпом, разовые выходы
    /// и общий лимит живых. Группа появляется в точке спавна вне экрана и не ближе заданного расстояния
    /// к игрокам; перед появлением на полу мигают метки (у элитных — золотые). Когда вышедший босс выбит
    /// целиком, сообщает <see cref="GameEvents.BossDefeated"/>; что дальше, решает арена.
    /// </summary>
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
        float[] _nextTrackTime;
        bool[] _burstDone;
        bool _bossSpawned;

        /// <summary>Волны арены. Арена прогулки задаёт свои (<c>ArenaDirector</c>) до начала боя.</summary>
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
        /// <summary>Все разовые выходы уже вышли.</summary>
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
                return true;
            }
        }
        /// <summary>Сколько врагов ждут появления (метки уже на полу).</summary>
        public int PendingEnemies => PendingCount(null);
        /// <summary>Секунды забега по часам волн. Отладка может перемотать вперёд.</summary>
        public float WaveTime { get; set; }
        public bool Spawning
        {
            get => spawning;
            set => spawning = value;
        }
        public int AliveCount => Targetable.CountAlive(Team.Enemy);
        public int MaxAlive => wave ? wave.MaxAliveAt(WaveTime) : 0;
        public IReadOnlyList<SpawnTrack> Tracks => wave ? wave.tracks : System.Array.Empty<SpawnTrack>();

        // Поле направлений роя строится при загрузке, а не посреди боя, когда появится первый пупс.
        void Start() => _ = EnemyFlowField.Instance;

        void Update()
        {
            UpdatePending();
            if (wave == null || !GameSession.IsGameplayActive)
                return;
            CheckVictory();
            EnsureSchedule();
            WaveTime += Time.deltaTime;
            if (!spawning)
                return;
            RunBursts();
            RunTracks();
        }

        /// <summary>
        /// Босс вышел и выбит вместе со всеми половинками. Проверка кадром позже смерти части —
        /// к этому моменту её половинки уже появились и учтены.
        /// </summary>
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
                _burstDone = new bool[wave.bursts.Count];
        }

        void RunTracks()
        {
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
                    // Места нет — попробуем чуть позже, а не через целый интервал.
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
        }

        /// <summary>Отладка: сразу выпустить разовый выход (например, босса).</summary>
        public void SpawnBurstNow(int index)
        {
            if (wave == null || index < 0 || index >= wave.bursts.Count)
                return;
            var burst = wave.bursts[index];
            QueueGroup(burst.PickPrefab(), burst.count, burst.layout, burst.boss, burst.elite);
        }

        public IReadOnlyList<SpawnBurst> Bursts => wave ? wave.bursts : System.Array.Empty<SpawnBurst>();

        // ---------- Появление ----------

        /// <summary>Поставить группу в очередь: метки на полу сразу, враги — через telegraphTime.</summary>
        public bool QueueGroup(GameObject prefab, int count, GroupLayout layout, bool boss = false, bool elite = false)
        {
            if (prefab == null || count <= 0)
                return false;

            Vector3 point = PickSpawnPoint();
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
            return true;
        }

        /// <summary>Отладка: сразу поставить в очередь группу с дорожки, не глядя на лимиты.</summary>
        public void SpawnTrackNow(int index)
        {
            if (wave == null || index < 0 || index >= wave.tracks.Count)
                return;
            var track = wave.tracks[index];
            QueueGroup(track.prefab, track.GroupSizeAt(WaveTime), track.layout);
        }

        /// <summary>
        /// Арена пройдена: ждущие появления группы отменяются, живые враги исчезают — без монеток, счёта и домино.
        /// </summary>
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
            if (group.Boss)
                _bossSpawned = true;
            _spawned.Clear();
            foreach (var position in group.Positions)
                _spawned.Add(PoolService.Spawn(group.Prefab, position, group.Rotation));
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i].TryGetComponent(out IGroupMember member))
                    member.OnGroupSpawned(_spawned, i);
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

        /// <summary>Точка вне экрана и подальше от игроков; если таких нет — самая дальняя.</summary>
        Vector3 PickSpawnPoint()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return transform.position;

            var camera = Camera.main;
            float minSqr = minDistanceFromPlayer * minDistanceFromPlayer;
            Transform farthest = null;
            float farthestSqr = -1f;
            _offScreen.Clear();
            _onScreen.Clear();
            foreach (var point in spawnPoints)
            {
                if (!point)
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

        /// <summary>Раскладка «подсолнух»: плотная кучка без наложений.</summary>
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
