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
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += AddMember;
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
                NetworkManager.OnClientConnectedCallback -= AddMember;
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
                    SpawnPlayer(member);
            foreach (ulong id in timedOut)
                if (id != NetworkManager.ServerClientId)
                    NetworkManager.DisconnectClient(id, "net.error.timeout");
        }

        void SpawnPlayer(in RoomMember member)
        {
            var spawner = PlayerSpawner.Instance;
            var pose = spawner != null ? spawner.StartPose(member.Slot) : new Pose(Vector3.zero, Quaternion.identity);
            var player = Instantiate(playerPrefab, pose.position, pose.rotation);
            player.Init(member);
            player.NetworkObject.SpawnAsPlayerObject(member.ClientId, destroyWithScene: true);
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

        void AddMember(ulong clientId)
        {
            if (!IsServer || IndexOf(clientId) >= 0)
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
            int index = IndexOf(clientId);
            if (IsServer && index >= 0)
                _members.RemoveAt(index);
        }

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
