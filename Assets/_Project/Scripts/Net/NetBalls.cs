using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using Bouncer.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bouncer.Net
{
    [DefaultExecutionOrder(-40)]
    public sealed class NetBalls : NetworkBehaviour, IBallNetwork
    {
        const float SendError = 0.15f;
        const float LooseSendError = 0.3f;
        const float SendVelocityError = 1.5f;
        const float Keepalive = 0.5f;
        const float MaxFastForward = 0.3f;
        const float CorrectionTime = 0.08f;
        const float HandBlendTime = 0.2f;
        const float SnapDistance = 3f;
        const float PendingTimeout = 3f;
        const float TakeMemory = 3f;
        const float ReachSlack = 4f;
        const float ContactSlack = 6f;
        const float ColumnBottom = 0.25f;
        const float Skin = 0.01f;

        const byte TakePickup = 0;
        const byte TakeCatch = 1;
        const byte ContactHit = 0;
        const byte ContactBounce = 1;

        static readonly RaycastHit[] s_hits = new RaycastHit[8];

        [Tooltip("Все мячи, которые бывают в игре: по сети мяч — номер в этом списке")]
        [SerializeField] Ball[] prefabs;

        sealed class Tracked
        {
            public ushort Id;
            public int Life;
            public byte Prefab;
            public NetBallState Sent;
            public GameObject Owner;
            public GameObject Thrower;
            public bool Dirty;
        }

        sealed class Puppet
        {
            public ushort Id;
            public ushort Seq;
            public Ball Ball;
            public int Life;
            public BallState State;
            public Vector3 P0;
            public Vector3 V0;
            public float G;
            public double T0;
            public Vector3 LastTarget;
            public Vector3 Shown;
            public Vector3 Error;
            public float Blend = CorrectionTime;
            public float PendingUntil;
            public bool Contacted;

            public bool Alive => Ball != null && Ball.isActiveAndEnabled && Ball.IsPuppet && Ball.Life == Life;
        }

        struct Taken
        {
            public GameObject Owner;
            public bool Yoyo;
            public float Until;
        }

        readonly Dictionary<Ball, Tracked> _tracked = new();
        readonly Dictionary<ushort, Ball> _byId = new();
        readonly List<NetBallState> _outbox = new();
        readonly List<Ball> _gone = new();
        ushort _nextId;
        bool _warnedPrefab;

        readonly List<Puppet> _all = new();
        readonly List<Puppet> _step = new();
        readonly Dictionary<ushort, Puppet> _puppets = new();
        readonly Dictionary<ushort, Puppet> _pending = new();
        readonly Dictionary<Ball, Puppet> _byBall = new();
        readonly Dictionary<ushort, Taken> _taken = new();
        readonly List<ushort> _expired = new();
        ushort _throwSeq;

        readonly struct Orphan
        {
            public readonly Ball Ball;
            public readonly int Life;

            public Orphan(Ball ball, int life)
            {
                Ball = ball;
                Life = life;
            }
        }

        readonly List<Orphan>[] _orphans = { new(), new(), new(), new() };

        public static NetBalls Instance { get; private set; }

        public bool IsAuthority => IsServer;

        public int Count => IsServer ? _tracked.Count : _all.Count;

        double HostNow => NetClock.HostNow(NetworkManager);

        public override void OnNetworkSpawn()
        {
            Instance = this;
            Ball.Network = this;
            SceneManager.activeSceneChanged += OnSceneChanged;
            if (IsServer)
            {
                NetworkManager.NetworkTickSystem.Tick += OnTick;
                Ball.OwnBallLost += OnOwnBallLost;
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
            if (Instance == this)
                Instance = null;
            if (ReferenceEquals(Ball.Network, this))
                Ball.Network = null;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            Ball.OwnBallLost -= OnOwnBallLost;
            if (NetworkManager != null && NetworkManager.NetworkTickSystem != null)
                NetworkManager.NetworkTickSystem.Tick -= OnTick;
        }

        void OnSceneChanged(Scene previous, Scene next) => Clear();

        void Clear()
        {
            _tracked.Clear();
            _byId.Clear();
            _outbox.Clear();
            _all.Clear();
            _puppets.Clear();
            _pending.Clear();
            _byBall.Clear();
            _taken.Clear();
            foreach (var orphans in _orphans)
                orphans.Clear();
        }

        void OnTick()
        {
            if (!IsServer || !IsSpawned)
                return;
            double physicsTime = HostNow - (Time.timeAsDouble - Time.fixedTimeAsDouble);
            var balls = Ball.Active;
            for (int i = 0; i < balls.Count; i++)
            {
                var ball = balls[i];
                if (ball != null && !ball.IsPuppet)
                    Watch(ball, physicsTime, -1, 0);
            }

            _gone.Clear();
            foreach (var pair in _tracked)
                if (pair.Key == null || !pair.Key.isActiveAndEnabled || pair.Key.Life != pair.Value.Life)
                    _gone.Add(pair.Key);
            foreach (var ball in _gone)
            {
                var tracked = _tracked[ball];
                _tracked.Remove(ball);
                Forget(tracked);
            }
            Flush();
        }

        void Forget(Tracked tracked)
        {
            _byId.Remove(tracked.Id);
            _outbox.Add(new NetBallState { Id = tracked.Id, Kind = NetBallState.KindDespawn, PredictSlot = -1 });
        }

        void Watch(Ball ball, double time, sbyte predictSlot, ushort predictSeq)
        {
            bool fresh = false;
            if (!_tracked.TryGetValue(ball, out var tracked) || tracked.Life != ball.Life)
            {
                if (tracked != null)
                    Forget(tracked);
                tracked = new Tracked { Id = NextId(), Life = ball.Life, Prefab = PrefabOf(ball) };
                _tracked[ball] = tracked;
                _byId[tracked.Id] = ball;
                fresh = true;
            }

            var now = StateOf(ball, tracked, time, tracked.Sent.OwnerSlot, tracked.Sent.ThrowerSlot);
            now.PredictSlot = predictSlot;
            now.PredictSeq = predictSeq;
            if (fresh || ball.Owner != tracked.Owner)
            {
                tracked.Owner = ball.Owner;
                now.OwnerSlot = SlotOf(ball.Owner);
            }
            if (fresh || ball.Thrower != tracked.Thrower)
            {
                tracked.Thrower = ball.Thrower;
                now.ThrowerSlot = SlotOf(ball.Thrower);
            }

            var sent = tracked.Sent;
            bool send = fresh || tracked.Dirty || predictSlot >= 0
                        || now.State != sent.State || now.Team != sent.Team || now.Flags != sent.Flags
                        || now.Bits != sent.Bits || now.Damage != sent.Damage || now.OwnerSlot != sent.OwnerSlot
                        || now.ThrowerSlot != sent.ThrowerSlot || !Mathf.Approximately(now.Knockback, sent.Knockback);
            if (!send)
            {
                BallMotion.Extrapolate(sent.State, sent.Position, sent.Velocity, sent.Gravity, ball.Radius,
                    (float)(time - sent.Time), out Vector3 position, out Vector3 velocity);
                float limit = now.State == BallState.Loose ? LooseSendError : SendError;
                bool moving = now.Velocity.sqrMagnitude > 0.0025f || sent.Velocity.sqrMagnitude > 0.0025f;
                send = (position - now.Position).sqrMagnitude > limit * limit
                       || (velocity - now.Velocity).sqrMagnitude > SendVelocityError * SendVelocityError
                       || (moving && time - sent.Time > Keepalive);
            }
            if (!send)
                return;
            tracked.Sent = now;
            tracked.Dirty = false;
            _outbox.Add(now);
        }

        static NetBallState StateOf(Ball ball, Tracked tracked, double time, sbyte ownerSlot, sbyte throwerSlot)
        {
            var state = new NetBallState
            {
                Id = tracked.Id,
                Kind = NetBallState.KindUpsert,
                Prefab = tracked.Prefab,
                State = ball.State,
                Team = ball.Team,
                Flags = ball.Stats.Flags,
                Damage = (byte)Mathf.Clamp(ball.Stats.Damage, 0, 255),
                Knockback = ball.Stats.Knockback,
                OwnerSlot = ownerSlot,
                ThrowerSlot = throwerSlot,
                Time = time,
                Position = ball.Position,
                Velocity = ball.Velocity,
                Gravity = GravityOf(ball),
                PredictSlot = -1,
            };
            state.Phantom = ball.IsPhantom;
            state.YoyoString = ball.IsYoyoString;
            state.Hot = ball.BlastPending;
            return state;
        }

        public void SendAllTo(ulong clientId)
        {
            if (!IsServer || !IsSpawned)
                return;
            double time = HostNow - (Time.timeAsDouble - Time.fixedTimeAsDouble);
            var entries = new List<NetBallState>(_tracked.Count);
            foreach (var pair in _tracked)
            {
                var ball = pair.Key;
                var tracked = pair.Value;
                if (ball == null || !ball.isActiveAndEnabled || ball.Life != tracked.Life)
                    continue;
                entries.Add(StateOf(ball, tracked, time, SlotOf(ball.Owner), SlotOf(ball.Thrower)));
            }
            if (entries.Count > 0)
                BallsRpc(new NetBallBatch { Entries = entries }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        public void NoteOrphans(int slot, GameObject owner)
        {
            if (!IsServer || slot < 0 || slot >= _orphans.Length || owner == null)
                return;
            var orphans = _orphans[slot];
            orphans.Clear();
            foreach (var ball in _tracked.Keys)
                if (ball != null && ball.isActiveAndEnabled && ball.Owner == owner)
                    orphans.Add(new Orphan(ball, ball.Life));
        }

        public int AdoptOrphans(int slot, GameObject owner)
        {
            if (!IsServer || slot < 0 || slot >= _orphans.Length)
                return 0;
            int adopted = 0;
            var orphans = _orphans[slot];
            foreach (var orphan in orphans)
                if (orphan.Ball != null && orphan.Ball.isActiveAndEnabled && orphan.Ball.Life == orphan.Life && orphan.Ball.Adopt(owner))
                    adopted++;
            orphans.Clear();
            return adopted;
        }

        public int IndexOf(Ball prefab)
        {
            if (prefab == null || prefabs == null)
                return -1;
            for (int i = 0; i < prefabs.Length; i++)
                if (prefabs[i] == prefab)
                    return i;
            return -1;
        }

        public Ball PrefabAt(int index) => prefabs != null && index >= 0 && index < prefabs.Length ? prefabs[index] : null;

        public void Carry(Ball puppetBall, Vector3 center, Quaternion turn)
        {
            if (!_byBall.TryGetValue(puppetBall, out var puppet) || puppet.State != BallState.Loose)
                return;
            puppet.P0 = center + turn * (puppet.P0 - center);
            puppet.V0 = turn * puppet.V0;
            puppet.LastTarget = center + turn * (puppet.LastTarget - center);
            puppet.Shown = center + turn * (puppet.Shown - center);
            puppet.Error = turn * puppet.Error;
        }

        void Flush()
        {
            if (_outbox.Count == 0)
                return;
            BallsRpc(new NetBallBatch { Entries = _outbox });
            _outbox.Clear();
        }

        ushort NextId()
        {
            do
                _nextId++;
            while (_nextId == 0 || _byId.ContainsKey(_nextId));
            return _nextId;
        }

        static float GravityOf(Ball ball) => ball.State switch
        {
            BallState.Live or BallState.Popped => ball.Gravity,
            BallState.Loose => ball.Definition.looseDamping,
            _ => 0f,
        };

        byte PrefabOf(Ball ball)
        {
            var source = ball.TryGetComponent(out PooledObject tag) ? tag.Prefab : null;
            for (int i = 0; i < prefabs.Length; i++)
                if (prefabs[i] != null && prefabs[i].gameObject == source)
                    return (byte)i;
            WarnPrefab(ball.name);
            return 0;
        }

        byte IndexOfPrefab(Ball prefab)
        {
            for (int i = 0; i < prefabs.Length; i++)
                if (prefabs[i] == prefab)
                    return (byte)i;
            WarnPrefab(prefab != null ? prefab.name : "null");
            return 0;
        }

        void WarnPrefab(string ballName)
        {
            if (_warnedPrefab)
                return;
            _warnedPrefab = true;
            Debug.LogWarning($"[Net] Мяча «{ballName}» нет в списке NetBalls — по сети он будет обычным мячом.");
        }

        static sbyte SlotOf(GameObject go) => go != null && go.TryGetComponent(out PlayerController player) ? (sbyte)player.Slot : (sbyte)-1;

        static GameObject SlotObject(sbyte slot)
        {
            if (slot < 0)
                return null;
            var player = Players.InSlot(slot);
            return player != null ? player.gameObject : null;
        }

        PlayerController PlayerOf(ulong clientId) =>
            NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null
                ? client.PlayerObject.GetComponent<PlayerController>()
                : null;

        bool TryGetNear(ushort id, PlayerController player, float slack, out Ball ball)
        {
            if (!_byId.TryGetValue(id, out ball) || ball == null || !ball.isActiveAndEnabled || player == null)
                return false;
            Vector3 delta = ball.Position - player.transform.position;
            delta.y = 0f;
            float reach = slack + ball.Velocity.magnitude * 0.3f;
            return delta.sqrMagnitude <= reach * reach;
        }

        void MarkDirty(ushort id)
        {
            if (_byId.TryGetValue(id, out var ball) && ball != null && _tracked.TryGetValue(ball, out var tracked))
                tracked.Dirty = true;
        }

        public bool GiveToRemote(Ball ball, GameObject player)
        {
            if (!IsServer || ball == null || player == null || !player.TryGetComponent(out NetPlayer net) || !net.IsSpawned
                || net.IsOwner || !net.HasRoomForBall)
                return false;
            net.NoteBallGiven();
            GiveRpc(SlotOf(ball.Owner), ball.IsYoyoString, RpcTarget.Single(net.OwnerClientId, RpcTargetUse.Temp));
            ball.TakeInHands();
            return true;
        }

        void OnOwnBallLost(Ball ball, GameObject owner)
        {
            if (!IsServer || owner == null || !owner.TryGetComponent(out NetPlayer net) || !net.IsSpawned || net.IsOwner)
                return;
            LostRpc(ball.IsYoyoString, RpcTarget.Single(net.OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.Server)]
        void ThrowRpc(NetThrowRequest request, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            var player = PlayerOf(sender);
            if (player == null || request.Prefab >= prefabs.Length || prefabs[request.Prefab] == null)
            {
                CancelPrediction(player != null ? (sbyte)player.Slot : (sbyte)-1, request.Seq);
                return;
            }
            var ball = PoolService.Spawn(prefabs[request.Prefab], request.Origin, Quaternion.identity);
            ball.Launch(new BallThrow
            {
                Origin = request.Origin,
                Direction = request.Direction,
                Stats = request.Stats,
                Team = Team.Player,
                Thrower = player.gameObject,
                Perks = request.Perks,
                Phantom = request.Phantom,
                Owner = SlotObject(request.OwnerSlot),
                YoyoString = request.YoyoString,
            });
            double now = HostNow;
            ball.FastForward(Mathf.Clamp((float)(now - request.HostTime), 0f, MaxFastForward));
            if (!ball.isActiveAndEnabled)
            {
                CancelPrediction((sbyte)player.Slot, request.Seq);
                return;
            }
            ball.ShowFrom(HandOf(player), HandBlendTime);
            Watch(ball, now, (sbyte)player.Slot, request.Seq);
        }

        void CancelPrediction(sbyte slot, ushort seq)
        {
            if (slot >= 0)
                _outbox.Add(new NetBallState { Id = 0, Kind = NetBallState.KindDespawn, PredictSlot = slot, PredictSeq = seq });
        }

        [Rpc(SendTo.Server)]
        void TakeRpc(ushort id, byte kind, RpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            var player = PlayerOf(sender);
            if (TryGetNear(id, player, ReachSlack, out var ball) && CanTake(ball, kind))
            {
                ball.TakeInHands();
                return;
            }
            TakeDeniedRpc(id, RpcTarget.Single(sender, RpcTargetUse.Temp));
            MarkDirty(id);
        }

        static bool CanTake(Ball ball, byte kind) => kind == TakePickup
            ? ball.State == BallState.Loose
            : !ball.IsPhantom && ball.State is BallState.Live or BallState.Popped or BallState.Loose;

        [Rpc(SendTo.Server)]
        void DropRpc(ushort id, Vector3 position, Vector3 velocity, RpcParams rpc = default)
        {
            var player = PlayerOf(rpc.Receive.SenderClientId);
            if (TryGetNear(id, player, ReachSlack, out var ball) && ball.State is not (BallState.Idle or BallState.Stuck))
                ball.Drop(position, velocity);
            else
                MarkDirty(id);
        }

        [Rpc(SendTo.Server)]
        void SummonRpc(ushort id, RpcParams rpc = default)
        {
            var player = PlayerOf(rpc.Receive.SenderClientId);
            if (player != null && _byId.TryGetValue(id, out var ball) && ball != null && ball.isActiveAndEnabled)
                ball.Summon(player.gameObject);
        }

        [Rpc(SendTo.Server)]
        void ContactRpc(ushort id, byte kind, Vector3 point, Vector3 normal, RpcParams rpc = default)
        {
            var player = PlayerOf(rpc.Receive.SenderClientId);
            if (!TryGetNear(id, player, ContactSlack, out var ball))
                return;
            if (kind == ContactHit)
                ball.ForcePop(point, normal);
            else if (kind == ContactBounce)
                ball.ForceBounce(point, normal);
        }

        [Rpc(SendTo.NotServer, AllowTargetOverride = true)]
        void BallsRpc(NetBallBatch batch, RpcParams rpc = default)
        {
            if (batch.Entries == null)
                return;
            foreach (var entry in batch.Entries)
                Apply(entry);
        }

        void Apply(in NetBallState state)
        {
            if (state.Kind == NetBallState.KindDespawn)
            {
                if (state.Id == 0)
                {
                    if (state.PredictSlot == LocalSlot && _pending.TryGetValue(state.PredictSeq, out var predicted))
                        Remove(predicted, despawn: true);
                    return;
                }
                if (_puppets.TryGetValue(state.Id, out var gone))
                    Remove(gone, despawn: true);
                return;
            }
            if (_taken.ContainsKey(state.Id))
                return;
            if (!_puppets.TryGetValue(state.Id, out var puppet) || !puppet.Alive)
            {
                if (puppet != null)
                    Remove(puppet, despawn: false);
                if (state.PredictSlot >= 0 && state.PredictSlot == LocalSlot
                    && _pending.TryGetValue(state.PredictSeq, out puppet) && puppet.Alive)
                {
                    _pending.Remove(state.PredictSeq);
                    puppet.Id = state.Id;
                    _puppets[state.Id] = puppet;
                }
                else
                {
                    puppet = Create(state);
                    if (puppet == null)
                        return;
                }
            }
            Retarget(puppet, state);
        }

        Puppet Create(in NetBallState state)
        {
            if (state.Prefab >= prefabs.Length || prefabs[state.Prefab] == null)
                return null;
            var ball = PoolService.Spawn(prefabs[state.Prefab], state.Position, Quaternion.identity);
            ball.BeginPuppet();
            var puppet = new Puppet { Id = state.Id, Ball = ball, Life = ball.Life, Shown = state.Position, LastTarget = state.Position };
            _puppets[state.Id] = puppet;
            _byBall[ball] = puppet;
            _all.Add(puppet);
            return puppet;
        }

        void Retarget(Puppet puppet, in NetBallState state)
        {
            var ball = puppet.Ball;
            bool fresh = ball.State == BallState.Idle;
            Vector3 shown = ball.Position;
            puppet.State = state.State;
            puppet.P0 = state.Position;
            puppet.V0 = state.Velocity;
            puppet.G = state.Gravity;
            puppet.T0 = state.Time;
            if (state.State != BallState.Live)
                puppet.Contacted = false;

            var stats = new ThrowStats { Flags = state.Flags, Damage = state.Damage, Knockback = state.Knockback };
            ball.SetPuppetInfo(state.Team, stats, SlotObject(state.ThrowerSlot), SlotObject(state.OwnerSlot), state.Phantom,
                state.YoyoString, state.Hot);
            ball.SetPuppetState(state.State);

            BallMotion.Extrapolate(puppet.State, puppet.P0, puppet.V0, puppet.G, ball.Radius, (float)(HostNow - puppet.T0),
                out Vector3 target, out Vector3 velocity);
            puppet.LastTarget = target;
            puppet.Error = fresh ? Vector3.zero : shown - target;
            puppet.Blend = CorrectionTime;
            if (puppet.Error.sqrMagnitude > SnapDistance * SnapDistance)
                puppet.Error = Vector3.zero;
            if (fresh && state.State == BallState.Live && ball.Thrower != null
                && ball.Thrower.TryGetComponent(out PlayerController thrower) && !thrower.IsLocal)
            {
                Vector3 hand = HandOf(thrower);
                if ((hand - target).sqrMagnitude < 4f * SnapDistance * SnapDistance)
                {
                    puppet.Error = hand - target;
                    puppet.Blend = HandBlendTime;
                }
            }
            puppet.Shown = target + puppet.Error;
            ball.SetPuppetPose(puppet.Shown, velocity, puppet.G);
        }

        void Update()
        {
            if (!IsSpawned || IsServer)
                return;
            ExpireTakes();
            if (_all.Count == 0)
                return;
            double now = HostNow;
            _step.Clear();
            _step.AddRange(_all);
            foreach (var puppet in _step)
            {
                if (!puppet.Alive)
                {
                    Remove(puppet, despawn: false);
                    continue;
                }
                if (puppet.Id == 0 && Time.time > puppet.PendingUntil)
                {
                    Remove(puppet, despawn: true);
                    continue;
                }
                Step(puppet, now, Mathf.Exp(-Time.deltaTime / puppet.Blend));
            }
        }

        void Step(Puppet puppet, double now, float decay)
        {
            var ball = puppet.Ball;
            BallMotion.Extrapolate(puppet.State, puppet.P0, puppet.V0, puppet.G, ball.Radius, (float)(now - puppet.T0),
                out Vector3 target, out Vector3 velocity);
            if (puppet.State is BallState.Live or BallState.Popped)
                BounceOffWalls(puppet, now, ref target, ref velocity);
            Vector3 from = puppet.Shown;
            puppet.LastTarget = target;
            puppet.Error *= decay;
            puppet.Shown = target + puppet.Error;
            ball.SetPuppetPose(puppet.Shown, velocity, puppet.G);
            if (puppet.Id != 0 && puppet.State == BallState.Live && !puppet.Contacted)
                CheckContact(puppet, from, puppet.Shown);
        }

        void BounceOffWalls(Puppet puppet, double now, ref Vector3 target, ref Vector3 velocity)
        {
            var ball = puppet.Ball;
            Vector3 from = puppet.LastTarget;
            Vector3 step = target - from;
            float distance = step.magnitude;
            if (distance < 1e-4f)
                return;
            Vector3 direction = step / distance;
            int count = Physics.SphereCastNonAlloc(from, ball.Radius, direction, s_hits, distance + Skin, Layers.BallSolidMask,
                QueryTriggerInteraction.Ignore);
            RaycastHit best = default;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (s_hits[i].distance > 0f && s_hits[i].distance < bestDistance)
                {
                    bestDistance = s_hits[i].distance;
                    best = s_hits[i];
                }
            }
            if (float.IsPositiveInfinity(bestDistance))
                return;

            var definition = ball.Definition;
            Vector3 point = from + direction * Mathf.Max(0f, bestDistance - Skin);
            if (best.normal.y > 0.6f)
            {
                puppet.State = BallState.Loose;
                puppet.V0 = Vector3.Reflect(velocity, best.normal) * definition.floorBounceKeep;
                puppet.G = definition.looseDamping;
            }
            else
            {
                Vector3 normal = Flat(best.normal);
                if (normal.sqrMagnitude < 1e-4f)
                    normal = -Flat(direction);
                normal.Normalize();
                var surface = best.collider.GetComponentInParent<RicochetSurface>();
                bool live = puppet.State == BallState.Live;
                float keep = !live ? definition.poppedWallKeep : surface ? surface.SpeedKeep : definition.wallSpeedKeep;
                Vector3 reflected = Vector3.Reflect(velocity, normal);
                puppet.V0 = new Vector3(reflected.x * keep, reflected.y, reflected.z * keep);
                if (live && surface && surface.KeepHeight && puppet.V0.y < 0f)
                    puppet.V0.y = 0f;
                GameEvents.PlaySound(SoundCue.BallWall, best.point);
            }
            puppet.P0 = point;
            puppet.T0 = now;
            target = point;
            velocity = puppet.V0;
        }

        void CheckContact(Puppet puppet, Vector3 from, Vector3 to)
        {
            var ball = puppet.Ball;
            if (!ball.Team.IsHostileTo(Team.Player))
                return;
            if (SweepShield(ball, from, to, out Vector3 shieldNormal))
            {
                puppet.Contacted = true;
                PredictBounce(puppet, shieldNormal);
                return;
            }
            var local = Players.Local;
            if (local == null || local.IsDead)
                return;
            if (!SweepPlayer(local, ball.Radius, from, to, out RaycastHit hit))
                return;
            ushort id = puppet.Id;
            var result = local.OnBallContact(ball, hit);
            if (result == BallContactResult.Hit)
            {
                puppet.Contacted = true;
                ContactRpc(id, ContactHit, hit.point, hit.normal);
                PredictPop(puppet, hit.normal);
            }
            else if (result == BallContactResult.Bounce)
            {
                puppet.Contacted = true;
                ContactRpc(id, ContactBounce, hit.point, hit.normal);
                PredictBounce(puppet, hit.normal);
            }
        }

        static bool SweepShield(Ball ball, Vector3 from, Vector3 to, out Vector3 normal)
        {
            normal = default;
            IBallShield struck = null;
            Vector3 point = default;
            Vector3 step = to - from;
            float distance = step.magnitude;
            if (distance < 1e-4f)
                return false;
            int count = Physics.SphereCastNonAlloc(from, ball.Radius, step / distance, s_hits, distance + Skin, Layers.PlayerMask,
                QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = s_hits[i];
                if (hit.distance >= best || hit.collider.GetComponentInParent<IBallShield>() is not { } shield || !shield.Blocks(ball))
                    continue;
                best = hit.distance;
                normal = hit.distance <= 0f ? -step / distance : hit.normal;
                struck = shield;
                point = hit.distance <= 0f ? from : hit.point;
            }
            if (struck == null)
                return false;
            struck.Struck(point);
            return true;
        }

        public void Neutralize(Ball puppetBall)
        {
            if (!_byBall.TryGetValue(puppetBall, out var puppet))
                return;
            puppet.Contacted = true;
            puppet.State = BallState.Stuck;
            puppet.P0 = puppetBall.Position;
            puppet.V0 = Vector3.zero;
            puppet.T0 = HostNow;
            puppet.LastTarget = puppet.P0;
            puppet.Error = Vector3.zero;
            puppetBall.SetPuppetState(BallState.Stuck);
        }

        static bool SweepPlayer(PlayerController player, float radius, Vector3 from, Vector3 to, out RaycastHit best)
        {
            best = default;
            Vector3 step = to - from;
            float distance = step.magnitude;
            Vector3 direction = distance > 1e-4f ? step / distance : Vector3.forward;
            float bestDistance = float.PositiveInfinity;
            int count = Physics.SphereCastNonAlloc(from, radius, direction, s_hits, distance + Skin, Layers.PlayerMask,
                QueryTriggerInteraction.Ignore);
            Consider(player, from, direction, count, ref best, ref bestDistance);
            float bottom = ColumnBottom + radius;
            if (from.y > bottom + 0.05f)
            {
                count = Physics.CapsuleCastNonAlloc(new Vector3(from.x, bottom, from.z), from, radius, direction, s_hits,
                    distance + Skin, Layers.PlayerMask, QueryTriggerInteraction.Ignore);
                Consider(player, from, direction, count, ref best, ref bestDistance);
            }
            return !float.IsPositiveInfinity(bestDistance);
        }

        static void Consider(PlayerController player, Vector3 from, Vector3 direction, int count, ref RaycastHit best,
            ref float bestDistance)
        {
            for (int i = 0; i < count; i++)
            {
                var hit = s_hits[i];
                if (hit.collider.GetComponentInParent<PlayerController>() != player)
                    continue;
                if (hit.distance <= 0f)
                {
                    hit.point = hit.collider.ClosestPoint(from);
                    hit.normal = -direction;
                }
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = hit;
                }
            }
        }

        void PredictPop(Puppet puppet, Vector3 hitNormal)
        {
            var definition = puppet.Ball.Definition;
            Vector3 horizontal = Flat(puppet.Ball.Velocity);
            Vector3 normal = Flat(hitNormal);
            if (normal.sqrMagnitude < 1e-4f)
                normal = -horizontal;
            if (normal.sqrMagnitude < 1e-4f)
                normal = Vector3.forward;
            normal.Normalize();
            puppet.State = BallState.Popped;
            puppet.P0 = puppet.Ball.Position;
            puppet.V0 = Vector3.Reflect(horizontal, normal) * definition.popHorizontalKeep + Vector3.up * definition.popUpSpeed;
            puppet.G = definition.popGravity;
            puppet.T0 = HostNow;
            puppet.LastTarget = puppet.P0;
            puppet.Error = Vector3.zero;
            puppet.Ball.SetPuppetState(BallState.Popped);
        }

        void PredictBounce(Puppet puppet, Vector3 hitNormal)
        {
            Vector3 normal = Flat(hitNormal);
            Vector3 velocity = puppet.Ball.Velocity;
            if (normal.sqrMagnitude < 1e-4f)
                normal = -Flat(velocity);
            if (normal.sqrMagnitude < 1e-4f)
                return;
            float keep = puppet.Ball.Definition.wallSpeedKeep;
            Vector3 reflected = Vector3.Reflect(velocity, normal.normalized);
            puppet.P0 = puppet.Ball.Position;
            puppet.V0 = new Vector3(reflected.x * keep, reflected.y, reflected.z * keep);
            puppet.T0 = HostNow;
            puppet.LastTarget = puppet.P0;
            puppet.Error = Vector3.zero;
        }

        int LocalSlot => Players.Local != null ? Players.Local.Slot : -1;

        static Vector3 HandOf(PlayerController player)
        {
            var transform = player.transform;
            var stats = player.Stats;
            return transform.position + Vector3.up * stats.throwHeight + transform.forward * stats.throwForwardOffset;
        }

        void Remove(Puppet puppet, bool despawn)
        {
            _all.Remove(puppet);
            if (puppet.Id != 0 && _puppets.TryGetValue(puppet.Id, out var known) && known == puppet)
                _puppets.Remove(puppet.Id);
            if (puppet.Id == 0 && _pending.TryGetValue(puppet.Seq, out var pending) && pending == puppet)
                _pending.Remove(puppet.Seq);
            if (puppet.Ball != null && _byBall.TryGetValue(puppet.Ball, out var byBall) && byBall == puppet)
                _byBall.Remove(puppet.Ball);
            if (despawn && puppet.Alive)
                PoolService.Despawn(puppet.Ball.gameObject);
        }

        void ExpireTakes()
        {
            if (_taken.Count == 0)
                return;
            _expired.Clear();
            foreach (var pair in _taken)
                if (Time.time > pair.Value.Until)
                    _expired.Add(pair.Key);
            foreach (ushort id in _expired)
                _taken.Remove(id);
        }

        public Ball Throw(Ball prefab, in BallThrow t)
        {
            var ball = PoolService.Spawn(prefab, t.Origin, Quaternion.identity);
            ball.BeginPuppet();
            Vector3 direction = Flat(t.Direction);
            if (direction.sqrMagnitude < 1e-6f)
                direction = Flat(ball.transform.forward);
            direction.Normalize();
            Vector3 velocity = direction * t.Stats.Speed + Vector3.up * t.Stats.UpVelocity;
            ball.SetPuppetInfo(t.Team, t.Stats, t.Thrower, t.Owner, t.Phantom, t.YoyoString, t.Perks.blastRadius > 0f);
            ball.SetPuppetState(BallState.Live);
            ball.SetPuppetPose(t.Origin, velocity, t.Stats.Gravity);

            double now = HostNow;
            do
                _throwSeq++;
            while (_pending.ContainsKey(_throwSeq));
            var puppet = new Puppet
            {
                Seq = _throwSeq,
                Ball = ball,
                Life = ball.Life,
                State = BallState.Live,
                P0 = t.Origin,
                V0 = velocity,
                G = t.Stats.Gravity,
                T0 = now,
                LastTarget = t.Origin,
                Shown = t.Origin,
                PendingUntil = Time.time + PendingTimeout,
            };
            _pending[puppet.Seq] = puppet;
            _byBall[ball] = puppet;
            _all.Add(puppet);

            ThrowRpc(new NetThrowRequest
            {
                Seq = puppet.Seq,
                Prefab = IndexOfPrefab(prefab),
                HostTime = now,
                Origin = t.Origin,
                Direction = t.Direction,
                Stats = t.Stats,
                Perks = t.Perks,
                Phantom = t.Phantom,
                YoyoString = t.YoyoString,
                OwnerSlot = SlotOf(t.Owner),
            });
            return ball;
        }

        public void Take(Ball puppetBall)
        {
            if (!_byBall.TryGetValue(puppetBall, out var puppet) || puppet.Id == 0)
            {
                if (puppet != null)
                    Remove(puppet, despawn: true);
                else
                    PoolService.Despawn(puppetBall.gameObject);
                return;
            }
            _taken[puppet.Id] = new Taken { Owner = puppetBall.Owner, Yoyo = puppetBall.IsYoyoString, Until = Time.time + TakeMemory };
            TakeRpc(puppet.Id, puppetBall.State == BallState.Loose ? TakePickup : TakeCatch);
            Remove(puppet, despawn: true);
        }

        public void Drop(Ball puppetBall, Vector3 position, Vector3 velocity)
        {
            if (!_byBall.TryGetValue(puppetBall, out var puppet) || puppet.Id == 0)
                return;
            DropRpc(puppet.Id, position, velocity);
            puppet.Error = puppetBall.Position - position;
            puppet.State = BallState.Loose;
            puppet.P0 = position;
            puppet.V0 = velocity;
            puppet.G = puppetBall.Definition.looseDamping;
            puppet.T0 = HostNow;
            puppet.LastTarget = position;
            puppet.Contacted = true;
            puppetBall.SetPuppetState(BallState.Loose);
        }

        public bool Summon(Ball puppetBall, GameObject taker)
        {
            if (!_byBall.TryGetValue(puppetBall, out var puppet) || puppet.Id == 0)
                return false;
            SummonRpc(puppet.Id);
            return true;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void TakeDeniedRpc(ushort id, RpcParams rpc)
        {
            if (!_taken.TryGetValue(id, out var taken))
                return;
            _taken.Remove(id);
            var local = Players.Local;
            if (local != null)
                local.Balls.RevokeFromNetwork(taken.Owner, taken.Yoyo);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void GiveRpc(sbyte ownerSlot, bool yoyoString, RpcParams rpc)
        {
            var local = Players.Local;
            if (local != null)
                local.Balls.ReceiveFromNetwork(SlotObject(ownerSlot), yoyoString);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void LostRpc(bool yoyoString, RpcParams rpc)
        {
            var local = Players.Local;
            if (local != null)
                local.Balls.LoseOwnBall(yoyoString);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
