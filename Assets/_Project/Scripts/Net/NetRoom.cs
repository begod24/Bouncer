using System;
using System.Collections;
using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Run;
using Bouncer.Upgrades;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bouncer.Net
{
    public sealed class NetRoom : NetworkBehaviour
    {
        [SerializeField] NetPlayer playerPrefab;
        [SerializeField] KidRoster roster;
        [Tooltip("Сколько гаснет экран перед загрузкой арены, с")]
        [SerializeField] float fadeDelay = 0.5f;
        [Tooltip("Сколько висит поражение, прежде чем все вернутся в комнату, с")]
        [SerializeField] float defeatDelay = 5f;
        [Tooltip("Сколько висит победа, прежде чем все вернутся в комнату, с")]
        [SerializeField] float victoryDelay = 9f;
        [Tooltip("Дольше этого волны в начале арены никого не ждут, с")]
        [SerializeField] float holdLimit = 25f;
        [Tooltip("Сколько хозяин держит место вылетевшего игрока, с")]
        [SerializeField] float awayHold = 60f;

        struct Identity
        {
            public string Key;
            public string PlayerId;
        }

        struct LastSeen
        {
            public bool Valid;
            public string Scene;
            public Vector3 Position;
            public Quaternion Rotation;
            public byte Lives;
            public byte Hands;
        }

        sealed class AwaySlot
        {
            public string Key;
            public RoomMember Member;
            public bool HasSave;
            public NetRunSave Save;
            public int CoinsAtDrop;
            public LastSeen Seen;
            public int ArenaAtDrop;
            public bool ClearSeen;
            public int BossCards;
            public float Until;
        }

        readonly Dictionary<ulong, Identity> _identities = new();
        readonly Dictionary<ulong, string> _rejoinKeys = new();
        readonly Dictionary<ulong, AwaySlot> _rejoining = new();
        readonly List<AwaySlot> _away = new();
        readonly NetRunSave[] _saves = new NetRunSave[RunState.MaxPlayers];
        readonly bool[] _hasSave = new bool[RunState.MaxPlayers];
        readonly LastSeen[] _seen = new LastSeen[RunState.MaxPlayers];
        readonly List<UpgradeCard> _restoreTaken = new();
        readonly List<UpgradeCard> _restoreLocked = new();
        readonly List<OfferKind> _restoreCarried = new();

        bool _finished;
        bool _leaving;
        float _nextWipeCheck;

        NetworkList<RoomMember> _members;
        readonly NetworkVariable<NetMode> _mode = new();
        readonly NetworkVariable<byte> _danger = new(1);
        readonly NetworkVariable<bool> _started = new();
        readonly NetworkVariable<byte> _teamGolds = new();

        public static NetRoom Current { get; private set; }
        public static event Action Changed;

        public NetMode Mode => _mode.Value;
        public int Danger => _danger.Value;
        public bool Started => _started.Value;
        public int Count => _members.Count;
        public RoomMember this[int index] => _members[index];

        public bool CanStart
        {
            get
            {
                if (!IsServer || Started || _members.Count == 0)
                    return false;
                foreach (var member in _members)
                    if (member.Kid < 0 || (member.ClientId != NetworkManager.ServerClientId && !member.Ready))
                        return false;
                return true;
            }
        }

        void Awake() => _members = new NetworkList<RoomMember>();

        public void Setup(NetMode mode)
        {
            _mode.Value = mode;
            _danger.Value = (byte)Bouncer.Core.Danger.Selected;
        }

        public override void OnNetworkSpawn()
        {
            Current = this;
            _members.OnListChanged += OnMembersChanged;
            _danger.OnValueChanged += OnValueChanged;
            _started.OnValueChanged += OnValueChanged;
            _teamGolds.OnValueChanged += OnTeamGoldsChanged;
            RunCards.GoldRecorded += OnGoldRecorded;
            if (!IsServer && !Started)
                Online.Joining = false;
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += RemoveMember;
                if (NetworkManager.SceneManager != null)
                    NetworkManager.SceneManager.OnLoadEventCompleted += OnArenaLoaded;
                foreach (ulong id in NetworkManager.ConnectedClientsIds)
                    AddMember(id);
                NetHooks.LeaveArena = LeaveArena;
            }
            IntroduceRpc(Fixed(Clip(GameSettings.PlayerName)), (sbyte)GameSettings.Kid);
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            _members.OnListChanged -= OnMembersChanged;
            _danger.OnValueChanged -= OnValueChanged;
            _started.OnValueChanged -= OnValueChanged;
            _teamGolds.OnValueChanged -= OnTeamGoldsChanged;
            RunCards.GoldRecorded -= OnGoldRecorded;
            if (NetHooks.LeaveArena == (Func<int, bool>)LeaveArena)
                NetHooks.LeaveArena = null;
            Online.WavesHeld = false;
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= RemoveMember;
                if (NetworkManager.SceneManager != null)
                    NetworkManager.SceneManager.OnLoadEventCompleted -= OnArenaLoaded;
            }
            if (Current == this)
                Current = null;
            Changed?.Invoke();
        }

        public override void OnDestroy()
        {
            _members?.Dispose();
            base.OnDestroy();
        }

        public bool TryGetLocal(out RoomMember member) => TryGet(NetworkManager.LocalClientId, out member);

        public bool TryGet(ulong clientId, out RoomMember member)
        {
            int index = IndexOf(clientId);
            member = index >= 0 ? _members[index] : default;
            return index >= 0;
        }

        public bool KidTaken(int kid, ulong except)
        {
            foreach (var member in _members)
                if (member.Kid == kid && member.ClientId != except)
                    return true;
            return false;
        }

        public void PickKid(int kid) => PickKidRpc((sbyte)kid);

        public void SetReady(bool ready) => SetReadyRpc(ready);

        public void SetDanger(int level)
        {
            if (IsServer && !Started)
                _danger.Value = (byte)Mathf.Clamp(level, 1, Bouncer.Core.Danger.Unlocked);
        }

        public bool StartGame()
        {
            if (!CanStart)
                return false;
            _started.Value = true;
            _finished = false;
            _leaving = false;
            _teamGolds.Value = 0;
            ForgetAway();
            BeginRunRpc((byte)_members.Count, _danger.Value);
            StartCoroutine(LoadFirstArena());
            return true;
        }

        [Rpc(SendTo.Server)]
        void IntroduceRpc(FixedString64Bytes playerName, sbyte kid, RpcParams rpc = default)
        {
            ulong id = rpc.Receive.SenderClientId;
            int index = IndexOf(id);
            if (index < 0)
            {
                AddMember(id);
                index = IndexOf(id);
                if (index < 0)
                    return;
            }
            var member = _members[index];
            member.Name = CleanName(playerName.ToString(), member.Slot);
            if (!Started)
                member.Kid = FreeKid(kid, id);
            _members[index] = member;
        }

        [Rpc(SendTo.Server)]
        void PickKidRpc(sbyte kid, RpcParams rpc = default)
        {
            ulong id = rpc.Receive.SenderClientId;
            int index = IndexOf(id);
            if (Started || index < 0 || kid < 0 || kid >= KidCount || KidTaken(kid, id))
                return;
            var member = _members[index];
            member.Kid = kid;
            _members[index] = member;
        }

        [Rpc(SendTo.Server)]
        void SetReadyRpc(bool ready, RpcParams rpc = default)
        {
            int index = IndexOf(rpc.Receive.SenderClientId);
            if (Started || index < 0)
                return;
            var member = _members[index];
            member.Ready = ready && member.Kid >= 0;
            _members[index] = member;
        }

        [Rpc(SendTo.Everyone)]
        void BeginRunRpc(byte players, byte danger)
        {
            if (TryGetLocal(out var me) && me.Kid >= 0)
            {
                GameSettings.Kid = me.Kid;
                GameSettings.Save();
            }
            RunState.BeginOnline(players, danger);
            RunCards.SharedGolds = 0;
            ScreenFade.Cover();
        }

        IEnumerator LoadFirstArena()
        {
            yield return new WaitForSecondsRealtime(fadeDelay);
            string scene = string.IsNullOrEmpty(RunState.FirstScene) ? SceneManager.GetActiveScene().name : RunState.FirstScene;
            NetworkManager.SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }

        void OnArenaLoaded(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            _leaving = false;
            if (!Started)
                return;
            foreach (var member in _members)
                if (completed.Contains(member.ClientId))
                    SpawnPlayer(member, StartPose(member.Slot));
            foreach (ulong id in timedOut)
                if (id != NetworkManager.ServerClientId)
                    NetworkManager.DisconnectClient(id, "net.error.timeout");
        }

        static Pose StartPose(int slot)
        {
            var spawner = PlayerSpawner.Instance;
            return spawner != null ? spawner.StartPose(slot) : new Pose(Vector3.zero, Quaternion.identity);
        }

        NetPlayer SpawnPlayer(in RoomMember member, Pose pose)
        {
            var player = Instantiate(playerPrefab, pose.position, pose.rotation);
            player.Init(member);
            player.NetworkObject.SpawnAsPlayerObject(member.ClientId, destroyWithScene: true);
            return player;
        }

        void UpdateHold()
        {
            bool hold = false;
            var session = GameSession.Instance;
            if (Started && session != null && session.State == SessionState.Playing && Time.timeSinceLevelLoad < holdLimit)
            {
                var players = Players.All;
                hold = players.Count < _members.Count;
                foreach (var player in players)
                    if (player.TryGetComponent(out NetPlayer net) && net.StartPending)
                        hold = true;
            }
            Online.WavesHeld = hold;
        }

        void OnGoldRecorded()
        {
            if (Started)
                GoldTakenRpc();
        }

        [Rpc(SendTo.Server)]
        void GoldTakenRpc() => _teamGolds.Value = (byte)Mathf.Min(255, _teamGolds.Value + 1);

        void OnTeamGoldsChanged(byte previous, byte current) => RunCards.SharedGolds = current;

        bool LeaveArena(int variant)
        {
            var director = ArenaDirector.Instance;
            string scene = director != null ? director.NextSceneName(variant) : null;
            if (!IsServer || !Started || _leaving || _finished || string.IsNullOrEmpty(scene))
                return false;
            _leaving = true;
            NoteArenaLeft(director);
            NextArenaRpc((byte)variant);
            StartCoroutine(LoadArena(scene));
            return true;
        }

        [Rpc(SendTo.Everyone)]
        void NextArenaRpc(byte variant)
        {
            var director = ArenaDirector.Instance;
            if (director != null)
                director.LeaveOnline(variant);
        }

        IEnumerator LoadArena(string scene)
        {
            yield return new WaitForSecondsRealtime(fadeDelay);
            NetworkManager.SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }

        void Update()
        {
            if (!IsSpawned)
                return;
            UpdateHold();
            if (IsServer)
                ExpireAway();
            if (!IsServer || !Started || _finished || Time.unscaledTime < _nextWipeCheck)
                return;
            _nextWipeCheck = Time.unscaledTime + 0.25f;
            var session = GameSession.Instance;
            if (session == null)
                return;
            if (session.State == SessionState.Victory)
            {
                _finished = true;
                StartCoroutine(ReturnAfter(victoryDelay));
                return;
            }
            if (session.State is not (SessionState.Playing or SessionState.Cleared or SessionState.Upgrade or SessionState.Shop))
                return;
            var players = Players.All;
            if (players.Count == 0)
                return;
            foreach (var player in players)
                if (!player.IsDead)
                    return;
            _finished = true;
            LoseRpc();
            StartCoroutine(ReturnAfter(defeatDelay));
        }

        [Rpc(SendTo.Everyone)]
        void LoseRpc()
        {
            if (GameSession.Instance != null)
                GameSession.Instance.LoseOnline();
        }

        IEnumerator ReturnAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            ReturnToLobby();
        }

        public void ReturnToLobby()
        {
            if (!IsServer || !Started)
                return;
            string scene = string.IsNullOrEmpty(RunState.FirstScene) ? SceneManager.GetActiveScene().name : RunState.FirstScene;
            _started.Value = false;
            ForgetAway();
            for (int i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                if (!member.Ready)
                    continue;
                member.Ready = false;
                _members[i] = member;
            }
            EndRunRpc();
            StartCoroutine(LoadLobby(scene));
        }

        [Rpc(SendTo.Everyone)]
        void EndRunRpc()
        {
            RunState.Clear();
            ScreenFade.Cover();
        }

        IEnumerator LoadLobby(string scene)
        {
            yield return new WaitForSecondsRealtime(fadeDelay);
            _finished = false;
            NetworkManager.SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }

        void OnClientConnected(ulong clientId)
        {
            AddMember(clientId);
            if (!_rejoining.TryGetValue(clientId, out var away))
                return;
            _rejoining.Remove(clientId);
            _rejoinKeys.Remove(clientId);
            FinishRejoin(clientId, away);
        }

        void AddMember(ulong clientId)
        {
            if (!IsServer || IndexOf(clientId) >= 0)
                return;
            if (_rejoinKeys.TryGetValue(clientId, out string key))
            {
                ClaimAway(clientId, key);
                return;
            }
            if (Started)
                return;
            int slot = FreeSlot();
            if (slot < 0)
                return;
            _members.Add(new RoomMember
            {
                ClientId = clientId,
                Slot = (byte)slot,
                Kid = -1,
                Name = CleanName("", slot),
            });
        }

        void RemoveMember(ulong clientId)
        {
            if (!IsServer)
                return;
            _identities.TryGetValue(clientId, out var identity);
            _identities.Remove(clientId);
            _rejoinKeys.Remove(clientId);
            _rejoining.TryGetValue(clientId, out var unfinished);
            _rejoining.Remove(clientId);
            if (NetSession.Instance != null)
                NetSession.Instance.RemoveFromSession(identity.PlayerId);
            int index = IndexOf(clientId);
            if (index >= 0)
            {
                var member = _members[index];
                _members.RemoveAt(index);
                if (unfinished == null && Started && !_finished && !string.IsNullOrEmpty(identity.Key))
                    KeepAway(member, identity.Key);
            }
            if (unfinished != null && Started && !_finished)
                _away.Add(unfinished);
        }

        public void NoteIdentity(ulong clientId, string profile, string playerName, string playerId)
        {
            if (IsServer)
                _identities[clientId] = new Identity { Key = KeyOf(profile, playerName), PlayerId = playerId ?? "" };
        }

        public bool ApproveRejoin(ulong clientId, string profile, string playerName)
        {
            if (!IsServer || !Started || _finished || string.IsNullOrEmpty(profile))
                return false;
            string key = KeyOf(profile, playerName);
            if (FindAway(key) == null)
            {
                ulong stale = ulong.MaxValue;
                foreach (var pair in _identities)
                    if (pair.Key != clientId && pair.Value.Key == key && IndexOf(pair.Key) >= 0)
                        stale = pair.Key;
                if (stale == ulong.MaxValue)
                    return false;
                NetworkManager.DisconnectClient(stale);
            }
            _rejoinKeys[clientId] = key;
            return true;
        }

        static string KeyOf(string profile, string playerName) => (profile ?? "") + "\n" + Clip(playerName);

        AwaySlot FindAway(string key)
        {
            foreach (var away in _away)
                if (away.Key == key)
                    return away;
            return null;
        }

        void KeepAway(in RoomMember member, string key)
        {
            int slot = Mathf.Clamp(member.Slot, 0, RunState.MaxPlayers - 1);
            _away.RemoveAll(away => away.Key == key);
            _away.Add(new AwaySlot
            {
                Key = key,
                Member = member,
                HasSave = _hasSave[slot],
                Save = _saves[slot],
                CoinsAtDrop = RunState.CoinsOf(slot),
                Seen = _seen[slot],
                ArenaAtDrop = RunState.ArenaIndex,
                ClearSeen = _hasSave[slot] && _saves[slot].ClearedArena == RunState.ArenaIndex + 1,
                Until = Time.unscaledTime + awayHold,
            });
            _hasSave[slot] = false;
            _seen[slot] = default;
            AwayRpc(member.Name);
        }

        void ClaimAway(ulong clientId, string key)
        {
            var away = FindAway(key);
            if (away == null)
            {
                _rejoinKeys.Remove(clientId);
                NetworkManager.DisconnectClient(clientId, "net.error.started");
                return;
            }
            _away.Remove(away);
            var member = away.Member;
            member.ClientId = clientId;
            member.Ready = false;
            _members.Add(member);
            _rejoining[clientId] = away;
        }

        void ExpireAway()
        {
            for (int i = _away.Count - 1; i >= 0; i--)
                if (Time.unscaledTime > _away[i].Until)
                    _away.RemoveAt(i);
        }

        void ForgetAway()
        {
            _away.Clear();
            _rejoinKeys.Clear();
            _rejoining.Clear();
            for (int slot = 0; slot < RunState.MaxPlayers; slot++)
            {
                _hasSave[slot] = false;
                _seen[slot] = default;
            }
        }

        void NoteArenaLeft(ArenaDirector director)
        {
            bool boss = director != null && director.IsComplete && director.ClearedByBoss;
            foreach (var away in _away)
            {
                bool sawClear = away.ArenaAtDrop == RunState.ArenaIndex && away.ClearSeen;
                if (boss && !sawClear)
                    away.BossCards++;
                away.ArenaAtDrop = -1;
                away.ClearSeen = false;
            }
        }

        public void NotePlayerGone(int slot, Vector3 position, Quaternion rotation, int lives, int hands, string scene)
        {
            if (!IsServer || !Started || slot < 0 || slot >= RunState.MaxPlayers)
                return;
            _seen[slot] = new LastSeen
            {
                Valid = true,
                Scene = scene,
                Position = position,
                Rotation = rotation,
                Lives = (byte)Mathf.Clamp(lives, 0, 255),
                Hands = (byte)Mathf.Clamp(hands, 0, 254),
            };
        }

        public void SendSave(in NetRunSave save)
        {
            if (IsSpawned && !IsServer && Started)
                SaveRpc(save);
        }

        [Rpc(SendTo.Server)]
        void SaveRpc(NetRunSave save, RpcParams rpc = default)
        {
            if (!Started || !TryGet(rpc.Receive.SenderClientId, out var member) || member.Slot >= RunState.MaxPlayers)
                return;
            _saves[member.Slot] = save;
            _hasSave[member.Slot] = true;
        }

        void FinishRejoin(ulong clientId, AwaySlot away)
        {
            int index = IndexOf(clientId);
            if (index < 0 || !Started)
                return;
            var member = _members[index];
            int slot = member.Slot;
            var director = ArenaDirector.Instance;
            bool sameArena = away.Seen.Valid && away.Seen.Scene == SceneManager.GetActiveScene().name
                                             && away.ArenaAtDrop == RunState.ArenaIndex;
            var save = away.HasSave ? away.Save : NetRunSave.Empty;
            save.Coins = away.HasSave ? save.Coins + Mathf.Max(0, RunState.CoinsOf(slot) - away.CoinsAtDrop) : RunState.CoinsOf(slot);
            save.CoinCarry = RunState.CoinCarryOf(slot);
            bool complete = director != null && director.IsComplete;
            var info = new NetRejoin
            {
                Slot = (byte)slot,
                Kid = member.Kid,
                Players = (byte)RunState.PlayerCount,
                Danger = (byte)RunState.Danger,
                ArenaIndex = (byte)RunState.ArenaIndex,
                ArenaVariant = (byte)RunState.ArenaVariant,
                PastTime = RunState.PastTime,
                PastKills = RunState.PastKills,
                CoinsEarned = RunState.CoinsEarned,
                RunClock = RunState.RunClock,
                ArenaTime = director != null ? director.ArenaTime : 0f,
                Lives = (byte)(!away.Seen.Valid ? 0 : sameArena ? away.Seen.Lives : Mathf.Max(1, (int)away.Seen.Lives)),
                Down = sameArena && away.Seen.Lives == 0,
                Hands = sameArena ? away.Seen.Hands : byte.MaxValue,
                Complete = complete,
                ClearBoss = complete && director.ClearedByBoss && !(sameArena && away.ClearSeen),
                BossCards = (byte)Mathf.Min(255, away.BossCards),
                TeamGolds = _teamGolds.Value,
                Save = save,
            };

            _saves[slot] = save;
            _hasSave[slot] = away.HasSave;
            var pose = sameArena ? new Pose(away.Seen.Position, away.Seen.Rotation) : StartPose(slot);
            var player = SpawnPlayer(member, pose);
            TryGetComponent(out NetBalls balls);
            if (sameArena && balls != null)
                balls.AdoptOrphans(slot, player.gameObject);
            RejoinRpc(info, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            if (TryGetComponent(out NetEnemies enemies))
                enemies.SendAllTo(clientId);
            if (balls != null)
                balls.SendAllTo(clientId);
            if (TryGetComponent(out NetWorld world))
                world.SendStateTo(clientId);
            BackRpc(member.Name, RpcTarget.Not(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void RejoinRpc(NetRejoin info, RpcParams rpc)
        {
            int slot = info.Slot;
            RunState.ResumeOnline(info.Players, info.Danger, info.ArenaIndex, info.ArenaVariant, info.PastTime, info.PastKills,
                info.CoinsEarned, info.RunClock);
            EnemyScaling.Players = info.Players;
            var deck = playerPrefab != null && playerPrefab.TryGetComponent(out PlayerCards prefabCards) ? prefabCards.Deck : null;
            var save = info.Save;
            NetRunSave.ToCards(deck, save.Cards, _restoreTaken);
            NetRunSave.ToCards(deck, save.Locked, _restoreLocked);
            _restoreCarried.Clear();
            if (save.Carried != null)
                foreach (byte kind in save.Carried)
                    _restoreCarried.Add((OfferKind)kind);
            for (int i = 0; i < info.BossCards; i++)
                _restoreCarried.Add(OfferKind.Boss);
            RunCards.Restore(slot, _restoreTaken, _restoreLocked, _restoreCarried, save.Backpack);
            RunCards.SharedGolds = info.TeamGolds;
            int hands = info.Hands == byte.MaxValue ? int.MaxValue : info.Hands;
            RunState.RestorePlayer(slot, save.Coins, save.CoinCarry, save.SecondWindUsed, info.Lives, hands, info.Down);
            if (info.Kid >= 0)
                GameSettings.Kid = info.Kid;

            Online.Joining = false;
            var session = GameSession.Instance;
            if (session != null)
                session.JoinRunning(info.ArenaTime);
            var local = Players.Local;
            if (local != null && local.TryGetComponent(out PlayerCards cards))
                cards.InitFromRun();
            var director = ArenaDirector.Instance;
            if (director != null)
            {
                director.SyncWaveTime(info.ArenaTime);
                if (info.Complete)
                    director.CompleteFromNetwork(info.ClearBoss, last: false);
            }
        }

        [Rpc(SendTo.Everyone)]
        void AwayRpc(FixedString64Bytes playerName) =>
            GameEvents.AnnounceLocal(new Announcement
            {
                Title = "net.away.title",
                Hint = "net.away.hint",
                Seconds = 4f,
                Arg = playerName.ToString(),
            });

        [Rpc(SendTo.Everyone, AllowTargetOverride = true)]
        void BackRpc(FixedString64Bytes playerName, RpcParams rpc = default) =>
            GameEvents.AnnounceLocal(new Announcement { Title = "net.back.title", Seconds = 3f, Arg = playerName.ToString() });

        int IndexOf(ulong clientId)
        {
            for (int i = 0; i < _members.Count; i++)
                if (_members[i].ClientId == clientId)
                    return i;
            return -1;
        }

        int FreeSlot()
        {
            for (int slot = 0; slot < RunState.MaxPlayers; slot++)
            {
                bool taken = false;
                foreach (var member in _members)
                    taken |= member.Slot == slot;
                if (!taken)
                    return slot;
            }
            return -1;
        }

        int KidCount => roster != null ? roster.Count : 4;

        sbyte FreeKid(int wanted, ulong clientId)
        {
            if (wanted >= 0 && wanted < KidCount && !KidTaken(wanted, clientId))
                return (sbyte)wanted;
            for (int kid = 0; kid < KidCount; kid++)
                if (!KidTaken(kid, clientId))
                    return (sbyte)kid;
            return -1;
        }

        static string Clip(string name)
        {
            name = (name ?? "").Trim();
            return name.Length > GameSettings.PlayerNameLength ? name.Substring(0, GameSettings.PlayerNameLength) : name;
        }

        static FixedString64Bytes CleanName(string name, int slot)
        {
            name = Clip(name);
            return Fixed(name.Length > 0 ? name : "#" + (slot + 1));
        }

        static FixedString64Bytes Fixed(string text)
        {
            var result = new FixedString64Bytes();
            result.CopyFromTruncated(text ?? "");
            return result;
        }

        void OnMembersChanged(NetworkListEvent<RoomMember> change) => Changed?.Invoke();

        void OnValueChanged<T>(T previous, T current) => Changed?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            Changed = null;
        }
    }
}
