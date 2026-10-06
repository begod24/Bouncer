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

        NetworkManager _network;
        UnityTransport _transport;
        bool _runInBackground;
        ISession _session;
        string _rejectKey;

        public static NetSession Instance { get; private set; }
        public NetStatus Status { get; private set; }
        public string Code { get; private set; } = "";
        public bool IsLan { get; private set; }
        public bool IsHost => _network != null && _network.IsHost;
        public string ErrorKey { get; private set; } = "";

        public static string PendingNotice { get; set; }

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
            if (!TryGetComponent<NetStatsOverlay>(out _))
                gameObject.AddComponent<NetStatsOverlay>();
        }

        void OnDestroy()
        {
            if (Instance != this)
                return;
            Instance = null;
            if (_network != null)
                _network.OnClientDisconnectCallback -= OnClientDisconnect;
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
            _network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(Handshake);
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
                    if (!await SignIn())
                        return Fail("net.error.signin");
                    _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.ToUpperInvariant());
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
            EnterRoom();
            return true;
        }

        public void Leave()
        {
            if (Status == NetStatus.Offline && !_network.IsListening && _session == null)
                return;
            Status = NetStatus.Offline;
            Online.Active = false;
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
            Leave();
            ErrorKey = key;
            Changed?.Invoke();
            return false;
        }

        async Task<bool> WaitConnected()
        {
            float until = Time.realtimeSinceStartup + connectTimeout;
            while (!_network.IsConnectedClient)
            {
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
            string handshake = request.Payload != null ? Encoding.UTF8.GetString(request.Payload) : "";
            if (handshake != Handshake)
                reason = "net.error.version";
            else if (room == null || room.Started)
                reason = "net.error.started";
            else if (_network.ConnectedClientsIds.Count >= MaxPlayers(room.Mode))
                reason = "net.error.full";
            response.Approved = reason == null;
            response.Reason = reason ?? "";
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
            PendingNotice = _rejectKey ?? "net.error.hostleft";
            var game = GameSession.Instance;
            bool inGame = game != null && game.State != Bouncer.Core.SessionState.Title;
            Leave();
            if (inGame)
                game.ToTitle();
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
        }
    }
}
