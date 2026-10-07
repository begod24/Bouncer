using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Bouncer.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bouncer.Net
{
    public enum NetMode : byte
    {
        Coop,
        Versus,
    }

    public enum NetStatus
    {
        Offline,
        Busy,
        InRoom,
    }

    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class NetSession : MonoBehaviour, IOnlineSession
    {
        const ushort LanPort = 7777;
        const int ProtocolVersion = 1;
        static readonly string s_profile = "p" + Guid.NewGuid().ToString("N").Substring(0, 12);

        [SerializeField] NetRoom roomPrefab;
        [Tooltip("Сколько ждать подключения, с")]
        [SerializeField] float connectTimeout = 10f;
        [Tooltip("Сколько ещё ждать, пока грузится арена идущей прогулки, с")]
        [SerializeField] float syncTimeout = 40f;
        [Tooltip("Сколько ждать своё место после входа в идущую прогулку, с")]
        [SerializeField] float rejoinTimeout = 15f;

        NetworkManager _network;
        UnityTransport _transport;
        bool _runInBackground;
        ISession _session;
        string _rejectKey;
        bool _syncStarted;
        float _joinedAt;
        int _joinScenes;

        public static NetSession Instance { get; private set; }
        public NetStatus Status { get; private set; }
        public string Code { get; private set; } = "";
        public bool IsLan { get; private set; }
        public bool IsHost => _network != null && _network.IsHost;
        public string ErrorKey { get; private set; } = "";

        public static string PendingNotice { get; set; }
        public static string LastJoinCode { get; private set; }

        public double HostTime => _network != null && _network.IsListening ? NetClock.HostNow(_network) : Time.timeAsDouble;

        public event Action Changed;

        public static int MaxPlayers(NetMode mode) => mode == NetMode.Versus ? 4 : RunState.MaxPlayers;

        static string Handshake => Application.version + "/" + ProtocolVersion;

        public static NetSession Ensure(NetSession prefab)
        {
            if (Instance != null || prefab == null)
                return Instance;
            var session = Instantiate(prefab);
            session.name = prefab.name;
            DontDestroyOnLoad(session.gameObject);
            return session;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _runInBackground = Application.runInBackground;
            _network = GetComponent<NetworkManager>();
            _transport = GetComponent<UnityTransport>();
            _network.NetworkConfig.ConnectionApproval = true;
            _network.ConnectionApprovalCallback = Approve;
            _network.OnClientDisconnectCallback += OnClientDisconnect;
            _network.OnClientStarted += OnClientStarted;
            if (!TryGetComponent<NetStatsOverlay>(out _))
                gameObject.AddComponent<NetStatsOverlay>();
        }

        void OnDestroy()
        {
            if (Instance != this)
                return;
            Instance = null;
            if (_network != null)
            {
                _network.OnClientDisconnectCallback -= OnClientDisconnect;
                _network.OnClientStarted -= OnClientStarted;
                if (_network.SceneManager != null)
                    _network.SceneManager.OnSynchronize -= OnSynchronize;
            }
            if (Online.Session == this)
            {
                Online.Session = null;
                Online.Active = false;
            }
        }

        public async Task<bool> CreateRoom(NetMode mode, bool lan)
        {
            if (Status != NetStatus.Offline)
                return false;
            SetBusy();
            try
            {
                if (lan)
                {
                    _transport.SetConnectionData("127.0.0.1", LanPort, "0.0.0.0");
                    if (!_network.StartHost())
                        return Fail("net.error.create");
                    IsLan = true;
                    Code = LocalAddress();
                }
                else
                {
                    if (!await SignIn())
                        return Fail("net.error.signin");
                    var options = new SessionOptions { MaxPlayers = MaxPlayers(mode), IsPrivate = true }.WithRelayNetwork();
                    _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                    Code = _session.Code;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Net] Комнату создать не вышло: {e}");
                return Fail("net.error.create");
            }

            var room = Instantiate(roomPrefab);
            room.name = roomPrefab.name;
            room.Setup(mode);
            DontDestroyOnLoad(room.gameObject);
            room.NetworkObject.Spawn();
            EnterRoom();
            return true;
        }

        public async Task<bool> JoinRoom(string code)
        {
            code = (code ?? "").Trim();
            if (Status != NetStatus.Offline || code.Length == 0)
                return false;
            SetBusy();
            _rejectKey = null;
            _syncStarted = false;
            bool signedIn = !LooksLikeAddress(code) && await SignIn();
            string playerId = signedIn ? AuthenticationService.Instance.PlayerId : "";
            _network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(
                Handshake + "\n" + s_profile + "\n" + CleanName(GameSettings.PlayerName) + "\n" + playerId);
            try
            {
                if (LooksLikeAddress(code))
                {
                    _transport.SetConnectionData(code, LanPort);
                    if (!_network.StartClient())
                        return Fail("net.error.join");
                    IsLan = true;
                }
                else
                {
                    if (!signedIn)
                        return Fail("net.error.signin");
                    _session = await JoinByCode(code.ToUpperInvariant());
                }
                Code = _session != null ? _session.Code : code;
                if (!await WaitConnected())
                    return Fail(_rejectKey ?? "net.error.join");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Net] Войти не вышло: {e}");
                return Fail(_rejectKey ?? "net.error.join");
            }
            LastJoinCode = Code;
            EnterRoom();
            _joinedAt = Time.realtimeSinceStartup;
            return true;
        }

        static async Task<ISession> JoinByCode(string code)
        {
            try
            {
                return await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            }
            catch (Exception e)
            {
                var session = await RejoinSession(code);
                if (session != null)
                    return session;
                Debug.LogWarning($"[Net] Войти по коду не вышло: {e.Message}");
                throw;
            }
        }

        static async Task<ISession> RejoinSession(string code)
        {
            try
            {
                foreach (string id in await MultiplayerService.Instance.GetJoinedSessionIdsAsync())
                {
                    var session = await MultiplayerService.Instance.ReconnectToSessionAsync(id);
                    if (session != null && string.Equals(session.Code, code, StringComparison.OrdinalIgnoreCase))
                        return session;
                    if (session != null)
                        await session.LeaveAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Net] Вернуться в старую сессию не вышло: {e.Message}");
            }
            return null;
        }

        public void RemoveFromSession(string playerId)
        {
            if (Status != NetStatus.InRoom || _session == null || !_session.IsHost || string.IsNullOrEmpty(playerId))
                return;
            _ = RemovePlayer(_session, playerId);
        }

        static async Task RemovePlayer(ISession session, string playerId)
        {
            try
            {
                foreach (var player in session.Players)
                {
                    if (player.Id != playerId)
                        continue;
                    await session.AsHost().RemovePlayerAsync(playerId);
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Net] Убрать игрока из сессии не вышло: {e.Message}");
            }
        }

        public void Leave()
        {
            if (Status == NetStatus.Offline && !_network.IsListening && _session == null)
                return;
            Status = NetStatus.Offline;
            Online.Active = false;
            Online.Joining = false;
            Application.runInBackground = _runInBackground;
            if (Online.Session == (IOnlineSession)this)
                Online.Session = null;
            var session = _session;
            _session = null;
            if (_network.IsListening)
                _network.Shutdown();
            if (session != null)
                _ = LeaveSession(session);
            Code = "";
            IsLan = false;
            Changed?.Invoke();
        }

        void SetBusy()
        {
            Status = NetStatus.Busy;
            ErrorKey = "";
            Changed?.Invoke();
        }

        void EnterRoom()
        {
            Status = NetStatus.InRoom;
            Online.Session = this;
            Online.Active = true;
            Application.runInBackground = true;
            Changed?.Invoke();
        }

        bool Fail(string key)
        {
            bool synced = _syncStarted;
            Leave();
            ErrorKey = key;
            Changed?.Invoke();
            if (synced)
            {
                PendingNotice = key;
                BackToTitle();
            }
            return false;
        }

        static void BackToTitle()
        {
            var game = GameSession.Instance;
            if (game != null)
            {
                game.ToTitle();
                return;
            }
            string first = RunState.FirstScene;
            if (!string.IsNullOrEmpty(first) && Application.CanStreamedLevelBeLoaded(first))
                SceneManager.LoadScene(first);
            else
                SceneManager.LoadScene(0);
        }

        void OnClientStarted()
        {
            if (_network.IsServer || _network.SceneManager == null)
                return;
            _network.SceneManager.OnSynchronize -= OnSynchronize;
            _network.SceneManager.OnSynchronize += OnSynchronize;
        }

        void OnSynchronize(ulong clientId)
        {
            if (_network.IsServer || clientId != _network.LocalClientId || Status != NetStatus.Busy)
                return;
            _syncStarted = true;
            Online.Session = this;
            Online.Active = true;
            Online.Joining = true;
            ScreenFade.CoverNow();
            var placeholder = SceneManager.CreateScene("NetJoining" + ++_joinScenes);
            SceneManager.SetActiveScene(placeholder);
        }

        void Update()
        {
            if (!Online.Joining || Status != NetStatus.InRoom || Time.realtimeSinceStartup - _joinedAt < rejoinTimeout)
                return;
            Debug.LogWarning("[Net] Своё место в идущей прогулке так и не пришло.");
            PendingNotice = "net.error.join";
            Leave();
            BackToTitle();
        }

        async Task<bool> WaitConnected()
        {
            float until = Time.realtimeSinceStartup + connectTimeout;
            while (!_network.IsConnectedClient)
            {
                if (_syncStarted)
                    until = Mathf.Max(until, Time.realtimeSinceStartup + syncTimeout);
                if (_rejectKey != null || !_network.IsListening || Time.realtimeSinceStartup > until)
                    return false;
                await Task.Delay(100);
            }
            return true;
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                response.Approved = true;
                return;
            }
            string reason = null;
            var room = NetRoom.Current;
            string[] payload = (request.Payload != null ? Encoding.UTF8.GetString(request.Payload) : "").Split('\n');
            string profile = payload.Length > 1 ? payload[1] : "";
            string playerName = payload.Length > 2 ? payload[2] : "";
            string playerId = payload.Length > 3 ? payload[3] : "";
            if (payload[0] != Handshake)
                reason = "net.error.version";
            else if (room == null)
                reason = "net.error.started";
            else if (room.Started && !room.ApproveRejoin(request.ClientNetworkId, profile, playerName))
                reason = "net.error.started";
            else if (_network.ConnectedClientsIds.Count >= MaxPlayers(room.Mode))
                reason = "net.error.full";
            response.Approved = reason == null;
            response.Reason = reason ?? "";
            if (response.Approved)
                room.NoteIdentity(request.ClientNetworkId, profile, playerName, playerId);
        }

        void OnClientDisconnect(ulong clientId)
        {
            if (_network.IsServer)
                return;
            string reason = _network.DisconnectReason;
            if (!string.IsNullOrEmpty(reason))
                _rejectKey = reason;
            if (Status != NetStatus.InRoom)
                return;
            bool running = RunState.Active || (NetRoom.Current != null && NetRoom.Current.Started);
            PendingNotice = _rejectKey ?? (running ? "net.error.lost" : "net.error.hostleft");
            var game = GameSession.Instance;
            bool inGame = game != null && game.State != Bouncer.Core.SessionState.Title;
            Leave();
            if (inGame)
                game.ToTitle();
        }

        static string CleanName(string name)
        {
            name = (name ?? "").Trim().Replace("\n", " ");
            return name.Length > GameSettings.PlayerNameLength ? name.Substring(0, GameSettings.PlayerNameLength) : name;
        }

        static async Task<bool> SignIn()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                    await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(s_profile));
                while (UnityServices.State == ServicesInitializationState.Initializing)
                    await Task.Yield();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return AuthenticationService.Instance.IsSignedIn;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Net] Сетевые сервисы недоступны: {e}");
                return false;
            }
        }

        static async Task LeaveSession(ISession session)
        {
            try
            {
                await session.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Net] Уйти из сессии не вышло: {e.Message}");
            }
        }

        static string LocalAddress()
        {
            try
            {
                foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (network.OperationalStatus != OperationalStatus.Up || network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;
                    foreach (var address in network.GetIPProperties().UnicastAddresses)
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address.Address))
                            return address.Address.ToString();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Net] Не узнать свой IP: {e.Message}");
            }
            return "127.0.0.1";
        }

        static bool LooksLikeAddress(string code) =>
            code.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || (code.Contains(".") && IPAddress.TryParse(code, out var address) && address.AddressFamily == AddressFamily.InterNetwork);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            PendingNotice = null;
            LastJoinCode = null;
        }
    }
}
