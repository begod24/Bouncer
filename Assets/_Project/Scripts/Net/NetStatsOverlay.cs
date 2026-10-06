using System.Text;
using Bouncer.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bouncer.Net
{
    /// <summary>
    /// Отладка сети по F3, пока есть комната: тип комнаты, пинг (у хозяина — до каждого гостя), кадры в секунду и
    /// такт сети. Для закрытых тестов — понять, откуда задержка: связь, слабый компьютер или сглаживание.
    /// По каждому чужому игроку: на сколько он показан в прошлом (буфер), разброс доставки его точек, сколько
    /// времени точек не хватало («голод» — тогда он бежит наугад) и сколько было рывков-телепортов.
    /// </summary>
    public sealed class NetStatsOverlay : MonoBehaviour
    {
        NetworkManager _network;
        UnityTransport _transport;
        readonly StringBuilder _text = new();
        GUIStyle _style;
        bool _visible;
        float _frameTime = 1f / 60f;
        float _worstFrame;
        float _worstFrameUntil;

        void Awake()
        {
            _network = GetComponent<NetworkManager>();
            _transport = GetComponent<UnityTransport>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
                _visible = !_visible;
            float dt = Time.unscaledDeltaTime;
            _frameTime = Mathf.Lerp(_frameTime, dt, 0.1f);
            // Худший кадр за последнюю секунду — рывки видно, даже когда в среднем всё хорошо.
            if (dt > _worstFrame || Time.unscaledTime > _worstFrameUntil)
            {
                _worstFrame = dt;
                _worstFrameUntil = Time.unscaledTime + 1f;
            }
        }

        void OnGUI()
        {
            if (!_visible || _network == null || !_network.IsListening)
                return;
            _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = false };

            _text.Clear();
            var session = NetSession.Instance;
            _text.Append(session != null && session.IsLan ? "LAN " : "Relay ").Append(session != null ? session.Code : "")
                .Append(_network.IsHost ? "  host" : "  client").AppendLine();
            if (_network.IsServer)
            {
                foreach (ulong id in _network.ConnectedClientsIds)
                    if (id != NetworkManager.ServerClientId)
                        _text.Append("ping #").Append(id).Append(": ").Append(_transport.GetCurrentRtt(id)).AppendLine(" ms");
            }
            else
            {
                _text.Append("ping: ").Append(_transport.GetCurrentRtt(NetworkManager.ServerClientId)).AppendLine(" ms");
            }
            _text.Append("fps: ").Append(Mathf.RoundToInt(1f / Mathf.Max(0.001f, _frameTime)))
                .Append("  worst frame: ").Append(Mathf.RoundToInt(_worstFrame * 1000f)).AppendLine(" ms");
            _text.Append("tick: ").Append(_network.NetworkConfig.TickRate).Append(" Hz");
            var room = NetRoom.Current;
            if (room != null && room.TryGetComponent(out NetBalls balls))
                _text.AppendLine().Append("balls: ").Append(balls.Count);
            if (room != null && room.TryGetComponent(out NetEnemies enemies))
                _text.Append("  enemies: ").Append(enemies.Count);
            foreach (var player in Players.All)
            {
                if (player.IsLocal || !player.TryGetComponent(out NetPlayer net))
                    continue;
                var motion = net.Motion;
                _text.AppendLine().Append(net.DisplayName).Append(": buffer ").Append(Mathf.RoundToInt(motion.Delay * 1000f))
                    .Append(" ms  jitter ").Append(Mathf.RoundToInt(motion.Jitter * 1000f))
                    .Append(" ms  starved ").Append(Mathf.RoundToInt(motion.StarvedShare * 100f))
                    .Append("%  snaps ").Append(motion.Snaps);
            }

            GUI.Box(new Rect(12f, 12f, 420f, 24f + 20f * CountLines()), _text.ToString(), _style);
        }

        int CountLines()
        {
            int lines = 1;
            for (int i = 0; i < _text.Length; i++)
                if (_text[i] == '\n')
                    lines++;
            return lines;
        }
    }
}
