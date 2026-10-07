using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Upgrades;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Settings;

namespace Bouncer.Net
{
    [RequireComponent(typeof(PlayerController))]
    [DefaultExecutionOrder(-30)]
    public sealed class NetPlayer : NetworkBehaviour
    {
        const float MotionKeepalive = 0.2f;
        const int MotionEveryTicks = 2;
        const float HitResendInterval = 0.25f;
        const float SaveInterval = 0.5f;
        const byte NoBallType = byte.MaxValue;
        public const float ReviveTime = 2f;
        const float ReviveRange = 1.8f;

        [SerializeField] NamePlate namePlate;

        readonly NetworkVariable<byte> _slot = new();
        readonly NetworkVariable<sbyte> _kid = new(-1);
        readonly NetworkVariable<FixedString64Bytes> _name = new();
        readonly NetworkVariable<byte> _balls = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> _maxBalls = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> _lives = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> _maxLives = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> _status = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<NetPlayerMods> _mods = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> _ballType = new(NoBallType, writePerm: NetworkVariableWritePermission.Owner);

        const byte StatusCard = 1 << 0;
        const byte StatusShop = 1 << 1;
        const byte StatusStart = 1 << 2;
        const byte StatusHome = 1 << 3;
        float _hitSentAt = float.NegativeInfinity;
        float _reviveShown;
        float _reviveShownUntil;
        NetPlayer _reviving;
        float _reviveProgress;
        string _downedText;
        string _reviveHint;
        string _cardText;
        string _shopText;
        PlayerCards _cards;
        bool _wasDown;
        int _ballsGiven;
        readonly MotionBuffer _motion = new();
        readonly byte[] _poseBytes = new byte[NetPose.Size];
        bool _motionDue;
        int _ticksSinceMotion;
        Vector3 _sentPosition;
        float _sentYaw;
        double _sentTime;
        NetPose _sentPose;
        bool _sentMoving;
        double _lastFrameTime;
        Vector3 _lastFramePosition;
        float _lastFrameYaw;
        bool _hasLastFrame;
        float _nextSaveAt;
        NetRunSave _sentSave;
        bool _saveSent;
        readonly List<OfferKind> _saveOffers = new();

        RoomMember _member;
        bool _hasMember;
        PlayerController _player;
        PlayerKid _kidView;
        KidAnimator _animator;
        PlayerInput _input;

        public bool HasRoomForBall => _balls.Value + _ballsGiven < _maxBalls.Value;

        public void NoteBallGiven() => _ballsGiven++;

        void Awake()
        {
            _player = GetComponent<PlayerController>();
            _kidView = GetComponent<PlayerKid>();
            _animator = GetComponent<KidAnimator>();
            _input = GetComponent<PlayerInput>();
            _cards = GetComponent<PlayerCards>();
        }

        public void Init(in RoomMember member)
        {
            _member = member;
            _hasMember = true;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && _hasMember)
            {
                _slot.Value = _member.Slot;
                _kid.Value = _member.Kid;
                _name.Value = _member.Name;
            }
            _player.Setup(_slot.Value, IsOwner);
            if (_input != null)
                _input.enabled = IsOwner;
            ShowKid(_kid.Value);
            ShowName();
            _slot.OnValueChanged += OnSlotChanged;
            _kid.OnValueChanged += OnKidChanged;
            _name.OnValueChanged += OnNameChanged;
            _balls.OnValueChanged += OnBallsChanged;
            _mods.OnValueChanged += OnModsChanged;
            _ballType.OnValueChanged += OnBallTypeChanged;
            if (!IsOwner)
            {
                ApplyMods(_mods.Value);
                ApplyBallType(_ballType.Value);
            }
            if (IsOwner)
            {
                NetworkManager.NetworkTickSystem.Tick += OnTick;
                _player.Hurt += OnHurt;
                _player.Balls.Thrown += OnThrown;
                _player.Balls.Caught += OnCaught;
                _player.Balls.Fumbled += OnFumbled;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && !IsOwner)
            {
                var room = NetRoom.Current;
                if (room != null)
                    room.NotePlayerGone(_slot.Value, transform.position, transform.rotation, _lives.Value, _balls.Value,
                        gameObject.scene.name);
                if (NetBalls.Instance != null)
                    NetBalls.Instance.NoteOrphans(_slot.Value, gameObject);
            }
            _ballType.OnValueChanged -= OnBallTypeChanged;
            _slot.OnValueChanged -= OnSlotChanged;
            _kid.OnValueChanged -= OnKidChanged;
            _name.OnValueChanged -= OnNameChanged;
            _balls.OnValueChanged -= OnBallsChanged;
            _mods.OnValueChanged -= OnModsChanged;
            if (IsOwner)
            {
                if (NetworkManager != null && NetworkManager.NetworkTickSystem != null)
                    NetworkManager.NetworkTickSystem.Tick -= OnTick;
                _player.Hurt -= OnHurt;
                _player.Balls.Thrown -= OnThrown;
                _player.Balls.Caught -= OnCaught;
                _player.Balls.Fumbled -= OnFumbled;
            }
        }

        void Update()
        {
            if (!IsSpawned)
                return;
            if (IsOwner)
            {
                _balls.Value = (byte)Mathf.Clamp(_player.Balls.Balls, 0, 255);
                _maxBalls.Value = (byte)Mathf.Clamp(_player.Balls.MaxBalls, 0, 255);
                var health = _player.Health;
                _lives.Value = (byte)Mathf.Clamp(health.IsDead ? 0 : health.Current, 0, 255);
                _maxLives.Value = (byte)Mathf.Clamp(health.Max, 1, 255);
                _status.Value = OwnStatus();
                _mods.Value = NetPlayerMods.From(_player.Modifiers);
                _ballType.Value = BallTypeIndex();
                if (!IsServer)
                    UpdateSave();
                UpdateRevive();
                if (health.IsDead && !_wasDown)
                    GameEvents.AnnounceLocal(new Announcement { Title = "net.downed.title", Hint = "net.downed.hint", Seconds = 4f });
                _wasDown = health.IsDead;
            }
            else
            {
                _player.Balls.SetRemoteBalls(_balls.Value);
                _player.IsHome = (_status.Value & StatusHome) != 0;
                if (_maxLives.Value > 0)
                    _player.Health.Mirror(_lives.Value, _maxLives.Value, _lives.Value == 0);
                ShowStatus();
                if (_motion.Sample(Time.unscaledTimeAsDouble, Time.unscaledDeltaTime, out Vector3 position, out Quaternion rotation,
                        out Vector3 velocity, out byte[] pose, out int poseLength, out _))
                {
                    transform.SetPositionAndRotation(position, rotation);
                    _player.RemoteVelocity = velocity;
                    if (pose != null && poseLength >= NetPose.Size)
                        _player.RemoteAction = NetPose.Read(pose).ToAction();
                }
            }
        }

        public bool StartPending => (_status.Value & StatusStart) != 0;

        byte OwnStatus()
        {
            byte status = 0;
            var session = GameSession.Instance;
            if (session != null && session.Menu == LocalMenu.Card)
                status |= StatusCard;
            if (session != null && session.Menu == LocalMenu.Shop)
                status |= StatusShop;
            if (_cards != null && _cards.StartPending)
                status |= StatusStart;
            if (_player.IsHome)
                status |= StatusHome;
            return status;
        }

        byte BallTypeIndex()
        {
            var balls = NetBalls.Instance;
            int index = balls != null ? balls.IndexOf(_player.Balls.BallPrefab) : -1;
            return index is >= 0 and < NoBallType ? (byte)index : NoBallType;
        }

        void OnBallTypeChanged(byte previous, byte current) => ApplyBallType(current);

        void ApplyBallType(byte index)
        {
            if (IsOwner || index == NoBallType)
                return;
            var balls = NetBalls.Instance;
            var prefab = balls != null ? balls.PrefabAt(index) : null;
            if (prefab != null)
                _player.Balls.SetBallPrefab(prefab);
        }

        void UpdateSave()
        {
            if (_cards == null || !RunState.Active || Time.unscaledTime < _nextSaveAt)
                return;
            _nextSaveAt = Time.unscaledTime + SaveInterval;
            var room = NetRoom.Current;
            if (room == null || !room.Started)
                return;
            var save = NetRunSave.Capture(_cards, _saveOffers);
            if (_saveSent && save.Equals(_sentSave))
                return;
            room.SendSave(save);
            _sentSave = save;
            _saveSent = true;
        }

        void OnModsChanged(NetPlayerMods previous, NetPlayerMods current)
        {
            if (!IsOwner)
                ApplyMods(current);
        }

        void ApplyMods(in NetPlayerMods mods)
        {
            var modifiers = _player.Modifiers;
            modifiers.PickupRadius = mods.PickupRadius;
            modifiers.MirrorAngle = mods.MirrorAngle;
            modifiers.LanternRadius = mods.LanternRadius;
            modifiers.NotifyChanged();
        }

        public bool SendPortfolio()
        {
            if (!IsSpawned)
                return false;
            PortfolioRpc();
            return true;
        }

        [Rpc(SendTo.Owner)]
        void PortfolioRpc()
        {
            if (_cards != null)
                _cards.AddToBackpack();
        }

        void OnSlotChanged(byte previous, byte current)
        {
            _player.Setup(current, IsOwner);
            ShowName();
        }

        void OnKidChanged(sbyte previous, sbyte current) => ShowKid(current);

        void OnTick()
        {
            _motionDue = true;
            _ticksSinceMotion++;
        }

        public bool IsDown => _player.IsDead;

        public bool SendHit(in HitInfo hit)
        {
            if (!IsSpawned)
                return false;
            if (Time.time < _hitSentAt + HitResendInterval)
                return true;
            _hitSentAt = Time.time;
            HitRpc(NetHit.From(hit));
            return true;
        }

        public bool SendHeal(int amount)
        {
            if (!IsSpawned)
                return false;
            HealRpc((byte)Mathf.Clamp(amount, 1, 255));
            return true;
        }

        [Rpc(SendTo.Owner)]
        void HitRpc(NetHit hit) => _player.ApplyNetworkHit(hit.ToHit(null));

        [Rpc(SendTo.Owner)]
        void HealRpc(byte amount) => _player.Health.Heal(amount);

        [Rpc(SendTo.Owner)]
        void ReviveRpc() => _player.Revive(1);

        void UpdateRevive()
        {
            NetPlayer target = null;
            if (!_player.IsDead && _player.LastIntent.InteractHeld && GameSession.IsPlayerActive)
                target = DownedNear();
            if (target == null || target != _reviving)
            {
                _reviving = target;
                _reviveProgress = 0f;
            }
            if (target == null)
                return;
            _reviveProgress += Time.deltaTime;
            target.ShowRevive(_reviveProgress / ReviveTime);
            if (_reviveProgress < ReviveTime)
                return;
            target.ReviveRpc();
            _reviving = null;
            _reviveProgress = 0f;
        }

        NetPlayer DownedNear()
        {
            NetPlayer best = null;
            float bestSqr = ReviveRange * ReviveRange;
            foreach (var other in Players.All)
            {
                if (other == _player || !other.IsDead || !other.TryGetComponent(out NetPlayer net) || net.IsOwner)
                    continue;
                Vector3 delta = other.transform.position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < bestSqr)
                {
                    bestSqr = delta.sqrMagnitude;
                    best = net;
                }
            }
            return best;
        }

        void ShowRevive(float progress01)
        {
            _reviveShown = Mathf.Clamp01(progress01);
            _reviveShownUntil = Time.time + 0.2f;
        }

        void ShowStatus()
        {
            if (namePlate == null)
                return;
            string name = _name.Value.ToString();
            if (!_player.IsDead)
            {
                byte status = _status.Value;
                if ((status & StatusCard) != 0)
                {
                    _cardText ??= LocalizationSettings.StringDatabase.GetLocalizedString("UI", "net.busy.card");
                    namePlate.SetText($"{name} · {_cardText}");
                }
                else if ((status & StatusShop) != 0)
                {
                    _shopText ??= LocalizationSettings.StringDatabase.GetLocalizedString("UI", "net.busy.shop");
                    namePlate.SetText($"{name} · {_shopText}");
                }
                else
                {
                    namePlate.SetText(name);
                }
                return;
            }
            if (Time.time < _reviveShownUntil)
            {
                namePlate.SetText($"{name} · {Mathf.RoundToInt(_reviveShown * 100f)}%");
                return;
            }
            var local = Players.Local;
            bool near = local != null && !local.IsDead
                        && (local.transform.position - transform.position).sqrMagnitude <= ReviveRange * ReviveRange;
            if (near)
            {
                _reviveHint ??= LocalizationSettings.StringDatabase.GetLocalizedString("UI", "net.revive.hint");
                namePlate.SetText($"{name} · {_reviveHint}");
                return;
            }
            _downedText ??= LocalizationSettings.StringDatabase.GetLocalizedString("UI", "net.downed");
            namePlate.SetText($"{name} · {_downedText}");
        }

        void LateUpdate()
        {
            if (!IsSpawned || !IsOwner)
                return;
            double now = Time.unscaledTimeAsDouble;
            Vector3 position = transform.position;
            float yaw = transform.eulerAngles.y;
            if (_motionDue)
            {
                _motionDue = false;
                Vector3 velocity = _player.IsScripted && _hasLastFrame && now > _lastFrameTime
                    ? (position - _lastFramePosition) / (float)(now - _lastFrameTime)
                    : _player.Motor.ActualVelocity;
                velocity.y = 0f;
                var pose = NetPose.From(_player.Action);
                bool moving = velocity.sqrMagnitude >= 0.01f;
                bool moved = (position - _sentPosition).sqrMagnitude > 1e-6f || Mathf.Abs(Mathf.DeltaAngle(yaw, _sentYaw)) > 0.5f;
                bool started = moving && !_sentMoving;
                bool stopped = _sentMoving && !moving;
                bool urgent = started || stopped || !pose.SameFlags(_sentPose);
                bool changed = moved || !pose.Equals(_sentPose) || now - _sentTime >= MotionKeepalive;
                if (urgent || (changed && _ticksSinceMotion >= MotionEveryTicks))
                {
                    if (started && _hasLastFrame && _lastFrameTime > _sentTime)
                        SendMotion(_lastFrameTime, _lastFramePosition, _lastFrameYaw, Vector3.zero, _sentPose);
                    SendMotion(now, position, yaw, velocity, pose);
                    _sentMoving = moving;
                    _ticksSinceMotion = 0;
                }
            }
            _lastFrameTime = now;
            _lastFramePosition = position;
            _lastFrameYaw = yaw;
            _hasLastFrame = true;
        }

        void SendMotion(double time, Vector3 position, float yaw, Vector3 velocity, NetPose pose)
        {
            _sentPosition = position;
            _sentYaw = yaw;
            _sentTime = time;
            _sentPose = pose;
            MotionRpc(new MotionSample { Time = time, Position = position, Yaw = yaw, Velocity = velocity, Pose = pose });
        }

        [Rpc(SendTo.NotMe, Delivery = RpcDelivery.Unreliable)]
        void MotionRpc(MotionSample sample)
        {
            sample.Pose.Write(_poseBytes);
            _motion.Add(sample.Time, sample.Position, Quaternion.Euler(0f, sample.Yaw, 0f), sample.Velocity, Time.unscaledTimeAsDouble,
                _poseBytes, NetPose.Size);
        }

        public MotionBuffer Motion => _motion;
        public string DisplayName => _name.Value.ToString();

        void OnBallsChanged(byte previous, byte current) => _ballsGiven = 0;

        void OnNameChanged(FixedString64Bytes previous, FixedString64Bytes current) => ShowName();

        void ShowName()
        {
            if (namePlate != null)
                namePlate.Show(IsOwner ? null : _name.Value.ToString(), _slot.Value);
        }

        void ShowKid(int kid)
        {
            if (kid >= 0 && _kidView != null)
                _kidView.ShowKid(kid);
        }

        void OnHurt(HitInfo hit) => CueRpc(PlayerCue.Hurt);

        void OnFumbled() => CueRpc(PlayerCue.Hurt);

        void OnThrown(ThrowStats stats) => CueRpc(PlayerCue.Throw);

        void OnCaught(CatchInfo info) => CueRpc(PlayerCue.Caught);

        [Rpc(SendTo.NotMe)]
        void CueRpc(PlayerCue cue)
        {
            if (_animator != null)
                _animator.Play(cue);
            if (cue == PlayerCue.Throw)
                GameEvents.PlaySound(SoundCue.Throw, transform.position);
            else if (cue == PlayerCue.Caught)
                GameEvents.PlaySound(SoundCue.Catch, transform.position);
        }
    }

    public struct NetPlayerMods : INetworkSerializable, System.IEquatable<NetPlayerMods>
    {
        byte _pickup;
        byte _mirror;
        byte _lantern;

        public float PickupRadius => _pickup > 0 ? _pickup / 20f : 1f;
        public float MirrorAngle => _mirror;
        public float LanternRadius => _lantern / 10f;

        public static NetPlayerMods From(PlayerModifiers modifiers) => new()
        {
            _pickup = (byte)Mathf.Clamp(Mathf.RoundToInt(modifiers.PickupRadius * 20f), 1, 255),
            _mirror = (byte)Mathf.Clamp(Mathf.RoundToInt(modifiers.MirrorAngle), 0, 255),
            _lantern = (byte)Mathf.Clamp(Mathf.RoundToInt(modifiers.LanternRadius * 10f), 0, 255),
        };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _pickup);
            serializer.SerializeValue(ref _mirror);
            serializer.SerializeValue(ref _lantern);
        }

        public bool Equals(NetPlayerMods other) => _pickup == other._pickup && _mirror == other._mirror && _lantern == other._lantern;

        public override bool Equals(object obj) => obj is NetPlayerMods other && Equals(other);

        public override int GetHashCode() => _pickup | (_mirror << 8) | (_lantern << 16);
    }
}
