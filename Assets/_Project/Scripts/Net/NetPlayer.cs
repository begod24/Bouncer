using Bouncer.Balls;
using Bouncer.Core;
using Bouncer.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bouncer.Net
{
    /// <summary>
    /// Игрок по сети. Бегает он сам у себя (позицию и поворот рассылает NetworkTransform хозяина объекта — его
    /// компьютера), а остальные видят его ребёнка: того, кого он выбрал в комнате, с его позой (<see cref="NetPose"/>:
    /// замах, ловля, рывок, выбит) и разовыми движениями (бросок, ловля, удар). Над чужими — имя цветом их номера.
    /// Ввод (PlayerInput) включается только у своего — чужие не перехватывают клавиатуру и геймпады.
    /// Сколько мячей у игрока в руках, знает хозяин комнаты: мяч, вернувшийся сам (бумеранг, резинка, хват), он
    /// отдаёт только тому, у кого есть место (<see cref="NetBalls"/>).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class NetPlayer : NetworkBehaviour
    {
        [SerializeField] NamePlate namePlate;

        readonly NetworkVariable<byte> _slot = new();
        readonly NetworkVariable<sbyte> _kid = new(-1);
        readonly NetworkVariable<FixedString64Bytes> _name = new();
        readonly NetworkVariable<NetPose> _pose = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> _balls = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> _maxBalls = new(writePerm: NetworkVariableWritePermission.Owner);
        /// <summary>У хозяина: сколько мячей он отдал этому игроку, пока тот не прислал, сколько у него в руках.</summary>
        int _ballsGiven;

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
            if (IsOwner)
            {
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
            if (IsOwner)
            {
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
                _pose.Value = NetPose.From(_player.Action);
                _balls.Value = (byte)Mathf.Clamp(_player.Balls.Balls, 0, 255);
                _maxBalls.Value = (byte)Mathf.Clamp(_player.Balls.MaxBalls, 0, 255);
            }
            else
            {
                _player.RemoteAction = _pose.Value.ToAction();
                _player.Balls.SetRemoteBalls(_balls.Value);
            }
        }

        void OnSlotChanged(byte previous, byte current)
        {
            _player.Setup(current, IsOwner);
            ShowName();
        }

        void OnKidChanged(sbyte previous, sbyte current) => ShowKid(current);

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
}
