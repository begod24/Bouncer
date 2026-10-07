using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerRewind : MonoBehaviour
    {
        const int Capacity = 160;
        const float SampleInterval = 1f / 40f;

        [Tooltip("Лента-след: тянется по пути назад и сматывается")]
        [SerializeField] LineRenderer tape;
        [SerializeField] AbilityProp cassette;

        readonly Vector3[] _positions = new Vector3[Capacity];
        readonly float[] _times = new float[Capacity];
        readonly Vector3[] _path = new Vector3[Capacity];
        int _head;
        int _count;
        float _nextSample;
        PlayerController _player;
        CharacterController _controller;
        int _pathCount;
        float _start;
        float _duration;
        float _tapeUntil;
        Vector3 _tapeFrom;
        Vector3 _tapeTo;
        bool _tapeStraight;

        public bool Active { get; private set; }

        void Awake()
        {
            _player = GetComponent<PlayerController>();
            _controller = GetComponent<CharacterController>();
            if (tape)
                tape.enabled = false;
        }

        void LateUpdate()
        {
            if (Active)
                Step();
            else if (_player.IsLocal && Time.time >= _nextSample && !_player.IsDead)
                Record();
            UpdateTape();
        }

        void Record()
        {
            _nextSample = Time.time + SampleInterval;
            _positions[_head] = transform.position;
            _times[_head] = Time.time;
            _head = (_head + 1) % Capacity;
            _count = Mathf.Min(_count + 1, Capacity);
        }

        public bool TryFindBack(float seconds, out Vector3 destination)
        {
            destination = transform.position;
            if (_count < 2)
                return false;
            float want = Time.time - seconds;
            _pathCount = 0;
            for (int i = 0; i < _count; i++)
            {
                int index = (_head - 1 - i + Capacity) % Capacity;
                _path[_pathCount++] = _positions[index];
                if (_times[index] <= want)
                    break;
            }
            destination = _path[_pathCount - 1];
            return Flat(destination - transform.position).sqrMagnitude > 0.25f;
        }

        public void Begin(float duration, float invulnerable)
        {
            if (_pathCount < 2)
                return;
            Active = true;
            _start = Time.time;
            _duration = duration;
            _player.Health.SetInvulnerable(duration + invulnerable);
            _player.Balls.CancelCharge();
            _player.Balls.CancelCatch();
            if (_controller)
                _controller.enabled = false;
            ShowTape(_path[0], _path[_pathCount - 1], straight: false, duration + 0.35f);
        }

        public void ShowRemote(Vector3 from, Vector3 to, float duration) => ShowTape(from, to, straight: true, duration + 0.35f);

        void Step()
        {
            float t = Mathf.Clamp01((Time.time - _start) / _duration);
            float eased = t * t * (3f - 2f * t);
            float along = eased * (_pathCount - 1);
            int i = Mathf.Min(Mathf.FloorToInt(along), _pathCount - 2);
            transform.position = Vector3.Lerp(_path[i], _path[i + 1], along - i);
            if (t < 1f)
                return;
            Active = false;
            if (_controller)
                _controller.enabled = true;
            _count = 0;
        }

        void ShowTape(Vector3 from, Vector3 to, bool straight, float seconds)
        {
            _tapeFrom = from;
            _tapeTo = to;
            _tapeStraight = straight;
            _tapeUntil = Time.time + seconds;
            if (cassette)
                AbilityProp.Show(cassette, transform, new Vector3(0f, 2.3f, 0f), Quaternion.identity, seconds);
            GameEvents.PlaySound(SoundCue.Rewind, from);
        }

        void UpdateTape()
        {
            if (tape == null)
                return;
            float left = _tapeUntil - Time.time;
            bool on = left > 0f;
            if (tape.enabled != on)
                tape.enabled = on;
            if (!on)
                return;
            Vector3 lift = Vector3.up * 0.9f;
            if (_tapeStraight || _pathCount < 2)
            {
                tape.positionCount = 2;
                tape.SetPosition(0, transform.position + lift);
                tape.SetPosition(1, _tapeTo + lift);
            }
            else
            {
                tape.positionCount = _pathCount;
                for (int i = 0; i < _pathCount; i++)
                    tape.SetPosition(i, _path[i] + lift + Vector3.up * (Mathf.Sin(i * 0.9f + Time.time * 20f) * 0.05f));
                if (Active)
                    tape.SetPosition(0, transform.position + lift);
            }
            tape.widthMultiplier = 0.12f * Mathf.Clamp01(left / 0.3f);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
