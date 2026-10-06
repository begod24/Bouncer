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
    /// <summary>
    /// Игрок по сети. Бегает он сам у себя и раз в такт рассылает, где он, как бежит на самом деле и что делает
    /// (<see cref="MotionSample"/> с позой <see cref="NetPose"/>: замах, ловля, рывок, выбит), а остальные ведут его
    /// плавно по этим точкам (<see cref="MotionBuffer"/>) — положение, поза и скорость для анимации берутся из одного
    /// момента. Встал — сразу шлёт точку «стоит» (иначе у остальных он пробежал бы дальше и откатился назад),
    /// побежал после стояния — сперва точку, где ещё стоял. Разовые движения (бросок, ловля, удар) — отдельным
    /// сигналом. Видно его ребёнка — того, кого он выбрал в комнате. Над чужими — имя цветом их номера.
    /// Ввод (PlayerInput) включается только у своего — чужие не перехватывают клавиатуру и геймпады.
    /// Сколько мячей у игрока в руках, знает хозяин комнаты: мяч, вернувшийся сам (бумеранг, резинка, хват), он
    /// отдаёт только тому, у кого есть место (<see cref="NetBalls"/>).
    /// Сердца считает компьютер игрока, остальные видят их копию (выбит ли — враги не гонятся за выбитым). Удары
    /// врагов и лимонад хозяин отправляет компьютеру игрока (<see cref="SendHit"/>, <see cref="SendHeal"/>).
    /// Выбитого товарища поднимают, зажав взаимодействие рядом с ним на <see cref="ReviveTime"/> с: он встаёт с
    /// одним сердцем. Над выбитым — «выбит» и сколько осталось держать.
    /// Ещё свой игрок рассказывает остальным, чем занят (выбирает карточку, у ларька, не выбрал стартовую — тогда
    /// волны ждут, <see cref="StartPending"/>), и то из своих карточек, что нужно хозяину для врагов и монеток:
    /// «Длинные руки», «Зеркальце», «Фонарик». Портфель, который подобрал игрок, хозяин отдаёт в его рюкзак
    /// (<see cref="SendPortfolio"/>).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [DefaultExecutionOrder(-30)]
    public sealed class NetPlayer : NetworkBehaviour
    {
        /// <summary>Стоящий игрок напоминает о себе не реже этого, с.</summary>
        const float MotionKeepalive = 0.2f;
        /// <summary>Удары одному игроку уходят не чаще этого, с.</summary>
        const float HitResendInterval = 0.25f;
        /// <summary>Сколько держать взаимодействие рядом с выбитым, чтобы поднять, с.</summary>
        public const float ReviveTime = 2f;
        /// <summary>Поднять можно с такого расстояния, м.</summary>
        const float ReviveRange = 1.8f;

        [SerializeField] NamePlate namePlate;

        readonly NetworkVariable<byte> _slot = new();
        readonly NetworkVariable<sbyte> _kid = new(-1);
        readonly NetworkVariable<FixedString64Bytes> _name = new();
        readonly NetworkVariable<byte> _balls = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> _maxBalls = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> _lives = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> _maxLives = new(writePerm: NetworkVariableWritePermission.Owner);
        /// <summary>Чем занят: <see cref="StatusCard"/>, <see cref="StatusShop"/>, <see cref="StatusStart"/>.</summary>
        readonly NetworkVariable<byte> _status = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<NetPlayerMods> _mods = new(writePerm: NetworkVariableWritePermission.Owner);

        const byte StatusCard = 1 << 0;
        const byte StatusShop = 1 << 1;
        const byte StatusStart = 1 << 2;
        const byte StatusHome = 1 << 3;
        /// <summary>Кто сейчас поднимает этого игрока и насколько (только для надписи у того, кто поднимает).</summary>
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
        /// <summary>У хозяина: сколько мячей он отдал этому игроку, пока тот не прислал, сколько у него в руках.</summary>
        int _ballsGiven;
        readonly MotionBuffer _motion = new();
        readonly byte[] _poseBytes = new byte[NetPose.Size];
        bool _motionDue;
        Vector3 _sentPosition;
        float _sentYaw;
        double _sentTime;
        NetPose _sentPose;
        bool _sentMoving;
        double _lastFrameTime;
        Vector3 _lastFramePosition;
        float _lastFrameYaw;
        bool _hasLastFrame;

        /// <summary>Кем быть — хозяин узнаёт из комнаты до появления, а в сетевые поля пишет при появлении.</summary>
        RoomMember _member;
        bool _hasMember;
        PlayerController _player;
        PlayerKid _kidView;
        KidAnimator _animator;
        PlayerInput _input;

        /// <summary>У хозяина: есть ли у игрока место в руках для ещё одного мяча.</summary>
        public bool HasRoomForBall => _balls.Value + _ballsGiven < _maxBalls.Value;

        /// <summary>У хозяина: игроку только что отдан мяч — место в руках занято, пока не придёт новый счёт.</summary>
        public void NoteBallGiven() => _ballsGiven++;

        void Awake()
        {
            _player = GetComponent<PlayerController>();
            _kidView = GetComponent<PlayerKid>();
            _animator = GetComponent<KidAnimator>();
            _input = GetComponent<PlayerInput>();
            _cards = GetComponent<PlayerCards>();
        }

        /// <summary>Хозяин заполняет игрока из комнаты до того, как он появится у всех.</summary>
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
            if (!IsOwner)
                ApplyMods(_mods.Value);
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

        /// <summary>Не выбрал стартовую карточку: волны в начале прогулки ждут.</summary>
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

        void OnModsChanged(NetPlayerMods previous, NetPlayerMods current)
        {
            if (!IsOwner)
                ApplyMods(current);
        }

        /// <summary>Копия чужого игрока: его карточки, которые важны здесь (монетки, манекены, тень).</summary>
        void ApplyMods(in NetPlayerMods mods)
        {
            var modifiers = _player.Modifiers;
            modifiers.PickupRadius = mods.PickupRadius;
            modifiers.MirrorAngle = mods.MirrorAngle;
            modifiers.LanternRadius = mods.LanternRadius;
            modifiers.NotifyChanged();
        }

        /// <summary>Хозяин: игрок подобрал портфель — в его рюкзак, на его компьютере.</summary>
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

        void OnTick() => _motionDue = true;

        /// <summary>Выбит (по копии сердец у остальных).</summary>
        public bool IsDown => _player.IsDead;

        /// <summary>
        /// Хозяин: удар врага — компьютеру этого игрока. Удары подряд (огонь под ногами каждый кадр) не шлются чаще
        /// <see cref="HitResendInterval"/>: после удара у игрока всё равно неуязвимость.
        /// </summary>
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

        /// <summary>Хозяин: лимонад — компьютеру этого игрока.</summary>
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

        /// <summary>Свой игрок: держит взаимодействие рядом с выбитым товарищем — поднимает его.</summary>
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

        /// <summary>Свой игрок поднимает этого — показать над ним, сколько уже.</summary>
        void ShowRevive(float progress01)
        {
            _reviveShown = Mathf.Clamp01(progress01);
            _reviveShownUntil = Time.time + 0.2f;
        }

        /// <summary>Надпись над чужим: имя, а у выбитого — «выбит» или сколько уже подняли.</summary>
        void ShowStatus()
        {
            if (namePlate == null)
                return;
            string name = _name.Value.ToString();
            if (!_player.IsDead)
            {
                // Занят своим экраном (игра у него идёт): «выбирает карточку», «у ларька».
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
            // Свой игрок рядом — подсказать, как поднять; иначе просто «выбит».
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

        /// <summary>
        /// Свой игрок: раз в такт (уже подвинувшись в этом кадре) — где он, как бежит и что делает, если что-то
        /// поменялось или давно молчал. Часы — свои (ровные): остальные сами переводят их в свои.
        /// </summary>
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
                // В сценке (дорога к подъезду) контроллер выключен — скорость по смещению за кадр.
                Vector3 velocity = _player.IsScripted && _hasLastFrame && now > _lastFrameTime
                    ? (position - _lastFramePosition) / (float)(now - _lastFrameTime)
                    : _player.Motor.ActualVelocity;
                velocity.y = 0f;
                var pose = NetPose.From(_player.Action);
                bool moving = velocity.sqrMagnitude >= 0.01f;
                bool moved = (position - _sentPosition).sqrMagnitude > 1e-6f || Mathf.Abs(Mathf.DeltaAngle(yaw, _sentYaw)) > 0.5f;
                // Встал — сказать сразу: иначе у остальных он ещё пробежит по старой скорости и откатится назад.
                bool stopped = _sentMoving && !moving;
                if (moved || stopped || !pose.Equals(_sentPose) || now - _sentTime >= MotionKeepalive)
                {
                    // Побежал после стояния — сперва точка, где он ещё стоял, иначе бег у остальных начнётся раньше.
                    if (moving && !_sentMoving && _hasLastFrame && _lastFrameTime > _sentTime)
                        SendMotion(_lastFrameTime, _lastFramePosition, _lastFrameYaw, Vector3.zero, _sentPose);
                    SendMotion(now, position, yaw, velocity, pose);
                    _sentMoving = moving;
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

        /// <summary>Как плавно видно этого игрока (отладка, F3).</summary>
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

        /// <summary>Разовое движение своего игрока — показать у остальных.</summary>
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

    /// <summary>Карточки игрока, которые нужны остальным: «Длинные руки», «Зеркальце», «Фонарик» — по байту.</summary>
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
