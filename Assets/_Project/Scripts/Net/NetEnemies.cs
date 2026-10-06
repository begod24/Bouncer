using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Enemies;
using Bouncer.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Bouncer.Net
{
    /// <summary>
    /// Враги по сети. Настоящие враги — только у хозяина комнаты: их выпускают волны, они думают, бьют и умирают
    /// там. Гостям хозяин сообщает, какой враг появился, а потом раз в несколько тактов — где он, как повёрнут и что
    /// показывает его анимация (<see cref="INetEnemy"/>); отдельно — попадания (здоровье, вспышка), сброс серии и
    /// исчезновение (кто выбил). У гостя враг — копия: «мозги» не работают (<see cref="NetHooks.IsGuest"/>), тело не
    /// подчиняется физике, положение плавно ведёт <see cref="MotionBuffer"/>. Звуки, обломки и взрывы врагов
    /// показывает <see cref="NetWorld"/>. То, что гость делает с копией (подкат, «Свисток», «Домино»), уходит
    /// хозяину, и тот применяет это к настоящему врагу.
    /// </summary>
    [DefaultExecutionOrder(-35)]
    public sealed class NetEnemies : NetworkBehaviour
    {
        /// <summary>Точки движения — каждый такой такт (60 / 3 = 20 раз в секунду).</summary>
        const int SendEveryTicks = 3;
        const float PositionError = 0.02f;
        const float AngleError = 1f;
        /// <summary>Стоящий враг напоминает о себе не реже этого, с.</summary>
        const float Keepalive = 0.5f;
        /// <summary>Сколько врагов в одном ненадёжном сообщении (оно должно влезть в один пакет).</summary>
        const int MaxPerMessage = 20;

        [Tooltip("Все враги, которые бывают в игре: по сети враг — номер в этом списке")]
        [SerializeField] GameObject[] prefabs;

        /// <summary>Враг хозяина и что о нём знают гости.</summary>
        sealed class Tracked
        {
            public ushort Id;
            public GameObject Go;
            public Targetable Self;
            public Health Health;
            public INetEnemy Net;
            public Rigidbody Body;
            public Vector3 SentPosition;
            public Quaternion SentRotation;
            public Vector3 SentVelocity;
            public double SentTime;
            public readonly byte[] SentPayload = new byte[NetWriter.Capacity];
            public int SentLength;
            public bool SentFrozen;
            public System.Action<HitInfo> Damaged;
            public System.Action<HitInfo> Died;
            public System.Action Restored;
        }

        /// <summary>Копия врага у гостя.</summary>
        sealed class Puppet
        {
            public ushort Id;
            public GameObject Go;
            public Targetable Self;
            public Health Health;
            public INetEnemy Net;
            public HitFlash Flash;
            public readonly MotionBuffer Motion = new();
        }

        // Хозяин.
        readonly Dictionary<GameObject, Tracked> _tracked = new();
        readonly Dictionary<ushort, Tracked> _trackedById = new();
        readonly HashSet<GameObject> _unknown = new();
        readonly List<Tracked> _gone = new();
        readonly List<NetEnemyMotion> _outbox = new();
        readonly NetWriter _writer = new();
        ushort _nextId;
        int _tick;

        // Гость.
        readonly Dictionary<ushort, Puppet> _puppets = new();
        readonly Dictionary<GameObject, Puppet> _puppetsByObject = new();
        readonly List<Puppet> _step = new();
        readonly NetReader _reader = new();

        public override void OnNetworkSpawn()
        {
            SceneManager.activeSceneChanged += OnSceneChanged;
            if (IsServer)
            {
                NetworkManager.NetworkTickSystem.Tick += OnTick;
            }
            else
            {
                NetHooks.ForwardEnemyHit = ForwardHit;
                NetHooks.ForwardFreeze = ForwardFreeze;
                NetHooks.ForwardFreezeEnemies = ForwardFreezeEnemies;
            }
        }

        public override void OnNetworkDespawn()
        {
            Unhook();
            Clear();
        }

        public override void OnDestroy()
        {
            Unhook();
            base.OnDestroy();
        }

        void Unhook()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            if (NetworkManager != null && NetworkManager.NetworkTickSystem != null)
                NetworkManager.NetworkTickSystem.Tick -= OnTick;
            if (NetHooks.ForwardEnemyHit == (System.Func<Component, HitInfo, bool>)ForwardHit)
                NetHooks.ForwardEnemyHit = null;
            if (NetHooks.ForwardFreeze == (System.Func<Targetable, float, bool>)ForwardFreeze)
                NetHooks.ForwardFreeze = null;
            if (NetHooks.ForwardFreezeEnemies == (System.Func<float, bool>)ForwardFreezeEnemies)
                NetHooks.ForwardFreezeEnemies = null;
        }

        void OnSceneChanged(Scene previous, Scene next) => Clear();

        void Clear()
        {
            foreach (var tracked in _tracked.Values)
                Unsubscribe(tracked);
            _tracked.Clear();
            _trackedById.Clear();
            _unknown.Clear();
            _outbox.Clear();
            _puppets.Clear();
            _puppetsByObject.Clear();
        }

        /// <summary>Сколько врагов идёт по сети (для отладки, F3).</summary>
        public int Count => IsServer ? _tracked.Count : _puppets.Count;

        // ================= Хозяин =================

        void OnTick()
        {
            if (!IsServer || !IsSpawned)
                return;
            var all = Targetable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var target = all[i];
                if (target.Team == Team.Enemy && target.isActiveAndEnabled && !_tracked.ContainsKey(target.gameObject))
                    Track(target);
            }

            _gone.Clear();
            foreach (var tracked in _tracked.Values)
                if (tracked.Go == null || !tracked.Go.activeInHierarchy)
                    _gone.Add(tracked);
            foreach (var tracked in _gone)
            {
                Untrack(tracked);
                GoneRpc(new NetEnemyGone { Id = tracked.Id, KillerSlot = -1 });
            }

            if (++_tick % SendEveryTicks == 0)
                SendMotion();
        }

        void Track(Targetable target)
        {
            var go = target.gameObject;
            if (_unknown.Contains(go))
                return;
            int prefab = PrefabOf(go);
            if (prefab < 0)
            {
                _unknown.Add(go);
                Debug.LogWarning($"[Net] Врага «{go.name}» нет в списке NetEnemies — гости его не увидят.");
                return;
            }
            do
                _nextId++;
            while (_nextId == 0 || _trackedById.ContainsKey(_nextId));
            var tracked = new Tracked
            {
                Id = _nextId,
                Go = go,
                Self = target,
                Health = target.Health,
                Net = go.GetComponent<INetEnemy>(),
                Body = go.TryGetComponent(out Rigidbody body) && !body.isKinematic ? body : null,
                SentTime = double.NegativeInfinity,
            };
            tracked.Damaged = hit => OnDamaged(tracked, hit);
            tracked.Died = hit => OnDied(tracked, hit);
            tracked.Restored = () => OnRestored(tracked);
            if (tracked.Health != null)
            {
                tracked.Health.Damaged += tracked.Damaged;
                tracked.Health.Died += tracked.Died;
                tracked.Health.Restored += tracked.Restored;
            }
            _tracked[go] = tracked;
            _trackedById[tracked.Id] = tracked;

            var affix = go.TryGetComponent(out EliteAffix elite) ? elite.Kind : AffixKind.None;
            SpawnRpc(new NetEnemySpawn
            {
                Id = tracked.Id,
                Prefab = (ushort)prefab,
                Position = go.transform.position,
                Rotation = go.transform.rotation,
                Affix = (byte)affix,
                Health = (ushort)(tracked.Health ? tracked.Health.Current : 1),
                MaxHealth = (ushort)(tracked.Health ? tracked.Health.Max : 1),
            });
        }

        void Untrack(Tracked tracked)
        {
            Unsubscribe(tracked);
            _tracked.Remove(tracked.Go);
            _trackedById.Remove(tracked.Id);
        }

        static void Unsubscribe(Tracked tracked)
        {
            if (tracked.Health == null)
                return;
            tracked.Health.Damaged -= tracked.Damaged;
            tracked.Health.Died -= tracked.Died;
            tracked.Health.Restored -= tracked.Restored;
        }

        int PrefabOf(GameObject go)
        {
            var source = go.TryGetComponent(out PooledObject tag) ? tag.Prefab : null;
            if (source == null)
                return -1;
            for (int i = 0; i < prefabs.Length; i++)
                if (prefabs[i] == source)
                    return i;
            return -1;
        }

        void OnDamaged(Tracked tracked, HitInfo hit)
        {
            if (!IsSpawned || tracked.Health == null)
                return;
            HitRpc(new NetEnemyHit
            {
                Id = tracked.Id,
                Health = (ushort)tracked.Health.Current,
                MaxHealth = (ushort)tracked.Health.Max,
                Point = hit.Point,
                Direction = hit.Direction,
                Force = hit.Force,
                Flags = hit.Flags,
            });
        }

        void OnRestored(Tracked tracked)
        {
            if (!IsSpawned || tracked.Health == null)
                return;
            HealthRpc(tracked.Id, (ushort)tracked.Health.Current, (ushort)tracked.Health.Max);
        }

        void OnDied(Tracked tracked, HitInfo hit)
        {
            Untrack(tracked);
            if (!IsSpawned)
                return;
            GoneRpc(new NetEnemyGone
            {
                Id = tracked.Id,
                Killed = true,
                KillerSlot = SlotOf(hit.Source),
                Hit = NetHit.From(hit),
            });
        }

        void SendMotion()
        {
            // Свои часы хозяина (ровные), сдвинутые на шаг физики: положения тел — с последнего шага.
            double time = Time.unscaledTimeAsDouble - (Time.timeAsDouble - Time.fixedTimeAsDouble);
            _outbox.Clear();
            foreach (var tracked in _tracked.Values)
            {
                if (tracked.Go == null || !tracked.Go.activeInHierarchy)
                    continue;
                var body = tracked.Body;
                Vector3 position = body ? body.position : tracked.Go.transform.position;
                Quaternion rotation = body ? body.rotation : tracked.Go.transform.rotation;
                Vector3 velocity = body ? body.linearVelocity : tracked.Self.Velocity;
                _writer.Reset();
                tracked.Net?.WriteNet(_writer);
                bool frozen = tracked.Self.FrozenLeft > 0f;
                bool moved = (position - tracked.SentPosition).sqrMagnitude > PositionError * PositionError
                             || Quaternion.Angle(rotation, tracked.SentRotation) > AngleError;
                // Встал — сказать сразу, иначе у гостя он ещё проедет по старой скорости и откатится.
                bool stopped = tracked.SentVelocity.sqrMagnitude > 0.01f && velocity.sqrMagnitude < 0.01f;
                if (!moved && !stopped && frozen == tracked.SentFrozen && SamePayload(tracked) && time - tracked.SentTime < Keepalive)
                    continue;
                tracked.SentVelocity = velocity;
                tracked.SentPosition = position;
                tracked.SentRotation = rotation;
                tracked.SentTime = time;
                tracked.SentFrozen = frozen;
                tracked.SentLength = _writer.Length;
                System.Array.Copy(_writer.Bytes, tracked.SentPayload, _writer.Length);
                var payload = new byte[_writer.Length];
                System.Array.Copy(_writer.Bytes, payload, _writer.Length);
                _outbox.Add(new NetEnemyMotion
                {
                    Id = tracked.Id,
                    Position = position,
                    Rotation = rotation,
                    Velocity = velocity,
                    Frozen = tracked.Self.FrozenLeft,
                    PayloadLength = (byte)_writer.Length,
                    Payload = payload,
                });
                if (_outbox.Count >= MaxPerMessage)
                {
                    MotionRpc(new NetEnemyMotionBatch { Time = time, Entries = _outbox });
                    _outbox.Clear();
                }
            }
            if (_outbox.Count > 0)
                MotionRpc(new NetEnemyMotionBatch { Time = time, Entries = _outbox });
            _outbox.Clear();
        }

        bool SamePayload(Tracked tracked)
        {
            if (_writer.Length != tracked.SentLength)
                return false;
            var bytes = _writer.Bytes;
            for (int i = 0; i < _writer.Length; i++)
                if (bytes[i] != tracked.SentPayload[i])
                    return false;
            return true;
        }

        PlayerController PlayerOf(ulong clientId) =>
            NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null
                ? client.PlayerObject.GetComponent<PlayerController>()
                : null;

        static sbyte SlotOf(GameObject go) =>
            go != null && go.TryGetComponent(out PlayerController player) ? (sbyte)player.Slot : (sbyte)-1;

        [Rpc(SendTo.Server)]
        void EnemyHitRpc(ushort id, NetHit hit, RpcParams rpc = default)
        {
            if (!_trackedById.TryGetValue(id, out var tracked) || tracked.Go == null || !tracked.Go.activeInHierarchy)
                return;
            var player = PlayerOf(rpc.Receive.SenderClientId);
            if (tracked.Go.TryGetComponent(out IDamageable damageable))
                damageable.ApplyHit(hit.ToHit(player != null ? player.gameObject : null));
        }

        [Rpc(SendTo.Server)]
        void FreezeRpc(ushort id, float seconds)
        {
            if (_trackedById.TryGetValue(id, out var tracked) && tracked.Self != null)
                tracked.Self.Freeze(Mathf.Clamp(seconds, 0f, 10f));
        }

        [Rpc(SendTo.Server)]
        void FreezeAllRpc(float seconds) => Targetable.FreezeEnemies(Mathf.Clamp(seconds, 0f, 10f));

        // ================= Гость =================

        [Rpc(SendTo.NotServer)]
        void SpawnRpc(NetEnemySpawn spawn)
        {
            if (_puppets.ContainsKey(spawn.Id) || spawn.Prefab >= prefabs.Length || prefabs[spawn.Prefab] == null)
                return;
            var go = PoolService.Spawn(prefabs[spawn.Prefab], spawn.Position, spawn.Rotation);
            BecomePuppet(go);
            var puppet = new Puppet
            {
                Id = spawn.Id,
                Go = go,
                Self = go.GetComponent<Targetable>(),
                Health = go.GetComponent<Health>(),
                Net = go.GetComponent<INetEnemy>(),
                Flash = go.GetComponentInChildren<HitFlash>(true),
            };
            if (puppet.Health)
                puppet.Health.Mirror(spawn.Health, spawn.MaxHealth, false);
            EliteAffix.Assign(go, (AffixKind)spawn.Affix);
            _puppets[spawn.Id] = puppet;
            _puppetsByObject[go] = puppet;
        }

        /// <summary>Копия врага: тело не подчиняется физике, путь не ищет, здоровье само не восстанавливается.</summary>
        static void BecomePuppet(GameObject go)
        {
            foreach (var body in go.GetComponentsInChildren<Rigidbody>())
            {
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.None;
            }
            foreach (var agent in go.GetComponentsInChildren<NavMeshAgent>())
                agent.enabled = false;
            if (go.TryGetComponent(out Health health))
                health.ResetAfterSeconds = 0f;
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        void MotionRpc(NetEnemyMotionBatch batch)
        {
            if (batch.Entries == null)
                return;
            double now = Time.unscaledTimeAsDouble;
            foreach (var entry in batch.Entries)
            {
                if (!_puppets.TryGetValue(entry.Id, out var puppet))
                    continue;
                puppet.Motion.Add(batch.Time, entry.Position, entry.Rotation, entry.Velocity, now, entry.Payload, entry.PayloadLength);
                if (puppet.Self != null && entry.Frozen > 0.1f && puppet.Self.FrozenLeft < entry.Frozen - 0.15f)
                    puppet.Self.FreezeLocal(entry.Frozen);
            }
        }

        [Rpc(SendTo.NotServer)]
        void HitRpc(NetEnemyHit hit)
        {
            if (!_puppets.TryGetValue(hit.Id, out var puppet) || puppet.Health == null)
                return;
            var info = new HitInfo
            {
                Damage = 1,
                Point = hit.Point,
                Direction = hit.Direction,
                Force = hit.Force,
                SourceTeam = Team.Player,
                Flags = hit.Flags,
            };
            puppet.Health.Mirror(hit.Health, hit.MaxHealth, false, info);
            if (puppet.Flash)
                puppet.Flash.Flash(Color.white, 0.12f);
        }

        [Rpc(SendTo.NotServer)]
        void HealthRpc(ushort id, ushort health, ushort maxHealth)
        {
            if (_puppets.TryGetValue(id, out var puppet) && puppet.Health != null)
                puppet.Health.Mirror(health, maxHealth, false);
        }

        [Rpc(SendTo.NotServer)]
        void GoneRpc(NetEnemyGone gone)
        {
            if (!_puppets.TryGetValue(gone.Id, out var puppet))
                return;
            _puppets.Remove(gone.Id);
            _puppetsByObject.Remove(puppet.Go);
            if (puppet.Go == null)
                return;
            // Выбит: «Домино», бестиарий и счёт у гостя узнают, кто выбил. Монетки и половинки роняет хозяин.
            if (gone.Killed)
            {
                var killer = gone.KillerSlot >= 0 ? Players.InSlot(gone.KillerSlot) : null;
                GameEvents.RaiseEnemyKilled(puppet.Go, gone.Hit.ToHit(killer != null ? killer.gameObject : null));
            }
            if (puppet.Go.activeInHierarchy)
                PoolService.Despawn(puppet.Go);
        }

        void Update()
        {
            if (!IsSpawned || IsServer || _puppets.Count == 0)
                return;
            double now = Time.unscaledTimeAsDouble;
            float dt = Time.unscaledDeltaTime;
            _step.Clear();
            _step.AddRange(_puppets.Values);
            foreach (var puppet in _step)
            {
                if (puppet.Go == null || !puppet.Go.activeInHierarchy)
                {
                    _puppets.Remove(puppet.Id);
                    if (puppet.Go != null)
                        _puppetsByObject.Remove(puppet.Go);
                    continue;
                }
                if (!puppet.Motion.Sample(now, dt, out Vector3 position, out Quaternion rotation, out _, out byte[] payload,
                        out int length, out float age))
                    continue;
                puppet.Go.transform.SetPositionAndRotation(position, rotation);
                if (puppet.Net != null && payload != null)
                {
                    _reader.Reset(payload, length);
                    puppet.Net.ReadNet(_reader, age);
                }
            }
        }

        bool ForwardHit(Component target, HitInfo hit)
        {
            if (target == null || !_puppetsByObject.TryGetValue(target.gameObject, out var puppet))
                return false;
            EnemyHitRpc(puppet.Id, NetHit.From(hit));
            return true;
        }

        bool ForwardFreezeEnemies(float seconds)
        {
            if (!IsSpawned)
                return false;
            FreezeAllRpc(seconds);
            // Сразу видно у себя; хозяин пришлёт ту же заморозку.
            Targetable.FreezeEnemiesLocal(seconds);
            return true;
        }

        bool ForwardFreeze(Targetable target, float seconds)
        {
            if (target == null || !_puppetsByObject.TryGetValue(target.gameObject, out var puppet))
                return false;
            FreezeRpc(puppet.Id, seconds);
            return true;
        }
    }

    /// <summary>Враг хозяина исчез: выбит ли, кем (номер игрока, -1 — не игроком) и каким ударом.</summary>
    public struct NetEnemyGone : INetworkSerializable
    {
        public ushort Id;
        public bool Killed;
        public sbyte KillerSlot;
        public NetHit Hit;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Killed);
            serializer.SerializeValue(ref KillerSlot);
            if (Killed)
                Hit.NetworkSerialize(serializer);
        }
    }
}
