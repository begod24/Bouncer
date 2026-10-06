using System;
using System.Collections;
using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bouncer.Net
{
    /// <summary>
    /// Комната — общая для всех в ней: кто пришёл, кем гуляет (каждый ребёнок только у одного), кто готов,
    /// опасность (её выбирает хозяин из открытых у себя). Хозяин начинает, когда все гости готовы: у всех
    /// начинается сетевая прогулка (<see cref="RunState.BeginOnline"/>), экран гаснет, первая арена грузится по сети,
    /// и на каждой загруженной арене хозяин выпускает игроков (<see cref="NetPlayer"/>) на их точки старта.
    /// Живёт между сценами, пока есть комната.
    /// </summary>
    public sealed class NetRoom : NetworkBehaviour
    {
        [SerializeField] NetPlayer playerPrefab;
        [SerializeField] KidRoster roster;
        [Tooltip("Сколько гаснет экран перед загрузкой арены, с")]
        [SerializeField] float fadeDelay = 0.5f;

        NetworkList<RoomMember> _members;
        readonly NetworkVariable<NetMode> _mode = new();
        readonly NetworkVariable<byte> _danger = new(1);
        readonly NetworkVariable<bool> _started = new();

        public static NetRoom Current { get; private set; }
        /// <summary>В комнате что-то поменялось (или она появилась / пропала).</summary>
        public static event Action Changed;

        public NetMode Mode => _mode.Value;
        public int Danger => _danger.Value;
        /// <summary>Прогулка уже началась: новых не пускают.</summary>
        public bool Started => _started.Value;
        public int Count => _members.Count;
        public RoomMember this[int index] => _members[index];

        /// <summary>Хозяин может начинать: прогулка ещё не идёт, у всех выбраны дети, все гости готовы.</summary>
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

        /// <summary>Хозяин настраивает комнату до того, как она появится у всех.</summary>
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
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += AddMember;
                NetworkManager.OnClientDisconnectCallback += RemoveMember;
                if (NetworkManager.SceneManager != null)
                    NetworkManager.SceneManager.OnLoadEventCompleted += OnArenaLoaded;
                foreach (ulong id in NetworkManager.ConnectedClientsIds)
                    AddMember(id);
            }
            // Хозяину — своё имя и кем хочется гулять.
            IntroduceRpc(Fixed(Clip(GameSettings.PlayerName)), (sbyte)GameSettings.Kid);
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            _members.OnListChanged -= OnMembersChanged;
            _danger.OnValueChanged -= OnValueChanged;
            _started.OnValueChanged -= OnValueChanged;
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

        /// <summary>Свой игрок в комнате. false — его ещё нет в списке.</summary>
        public bool TryGetLocal(out RoomMember member) => TryGet(NetworkManager.LocalClientId, out member);

        public bool TryGet(ulong clientId, out RoomMember member)
        {
            int index = IndexOf(clientId);
            member = index >= 0 ? _members[index] : default;
            return index >= 0;
        }

        /// <summary>Этого ребёнка уже взял кто-то другой.</summary>
        public bool KidTaken(int kid, ulong except)
        {
            foreach (var member in _members)
                if (member.Kid == kid && member.ClientId != except)
                    return true;
            return false;
        }

        // ---------- То, что игрок просит у хозяина ----------

        public void PickKid(int kid) => PickKidRpc((sbyte)kid);

        public void SetReady(bool ready) => SetReadyRpc(ready);

        /// <summary>Хозяин выбирает опасность из открытых у себя.</summary>
        public void SetDanger(int level)
        {
            if (IsServer && !Started)
                _danger.Value = (byte)Mathf.Clamp(level, 1, Bouncer.Core.Danger.Unlocked);
        }

        /// <summary>Хозяин начинает прогулку. false — ещё нельзя (<see cref="CanStart"/>).</summary>
        public bool StartGame()
        {
            if (!CanStart)
                return false;
            _started.Value = true;
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

        /// <summary>Прогулка начинается у всех: свой ребёнок — тот, что выбран в комнате, экран гаснет.</summary>
        [Rpc(SendTo.Everyone)]
        void BeginRunRpc(byte players, byte danger)
        {
            if (TryGetLocal(out var me) && me.Kid >= 0)
            {
                GameSettings.Kid = me.Kid;
                GameSettings.Save();
            }
            RunState.BeginOnline(players, danger);
            ScreenFade.Cover();
        }

        IEnumerator LoadFirstArena()
        {
            yield return new WaitForSecondsRealtime(fadeDelay);
            string scene = string.IsNullOrEmpty(RunState.FirstScene) ? SceneManager.GetActiveScene().name : RunState.FirstScene;
            NetworkManager.SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }

        /// <summary>Арена загрузилась у всех: выпустить игроков. Кто не успел загрузиться — отключить.</summary>
        void OnArenaLoaded(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
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

        // ---------- Список (только хозяин) ----------

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

        /// <summary>Хочется этого ребёнка: свободен — он, занят — первый свободный, свободных нет — никто.</summary>
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

        /// <summary>Имя без лишнего; пустое — «#N» по номеру игрока.</summary>
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
