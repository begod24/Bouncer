using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerSpin : MonoBehaviour
    {
        [Tooltip("Размытое кольцо вокруг кружащегося ребёнка")]
        [SerializeField] CircleLine blur;
        [Tooltip("Меловая спираль на асфальте, из пула")]
        [SerializeField] AbilityProp spiral;
        [Tooltip("Оборотов в секунду")]
        [SerializeField] float turnsPerSecond = 2.2f;

        PlayerController _player;
        PlayerKid _kid;
        float _start;
        float _duration;
        float _catchRadius;
        float _throwAt;
        bool _thrown;
        bool _owner;

        public bool Active { get; private set; }

        void Awake()
        {
            _player = GetComponent<PlayerController>();
            _kid = GetComponent<PlayerKid>();
            if (blur)
                blur.Line.enabled = false;
        }

        public void Begin(float duration, float catchRadius, float throwAt, bool owner)
        {
            Active = true;
            _start = Time.time;
            _duration = duration;
            _catchRadius = catchRadius;
            _throwAt = throwAt;
            _thrown = false;
            _owner = owner;
            GameEvents.PlaySound(SoundCue.SpinWhoosh, transform.position);
            if (spiral)
                AbilityProp.Show(spiral, transform, new Vector3(0f, 0.04f, 0f), Quaternion.identity, duration + 0.3f);
            if (owner)
                _player.Balls.CancelCharge();
        }

        void Update()
        {
            if (!Active)
                return;
            float age = Time.time - _start;
            var slot = _kid != null ? _kid.Slot : null;
            if (age >= _duration || _player.IsDead)
            {
                Active = false;
                if (slot)
                    slot.localRotation = Quaternion.identity;
                if (blur)
                    blur.Line.enabled = false;
                return;
            }
            float angle = age * turnsPerSecond * 360f;
            if (slot)
                slot.localRotation = Quaternion.Euler(0f, angle, 0f);
            if (blur)
            {
                var line = blur.Line;
                line.enabled = true;
                blur.Radius = 0.9f + 0.1f * Mathf.Sin(age * 30f);
                blur.Arc = 300f;
                blur.transform.localRotation = Quaternion.Euler(0f, angle * 1.3f, 0f);
                float fade = Mathf.Clamp01((_duration - age) / 0.2f);
                line.startColor = line.endColor = new Color(1f, 1f, 1f, 0.55f * fade);
            }
            if (!_owner)
                return;
            if (!_thrown && age >= _throwAt)
            {
                _thrown = true;
                _player.Balls.ThrowAround(angle);
            }
            CatchAround();
        }

        void CatchAround()
        {
            var balls = _player.Balls;
            Vector3 chest = transform.position + Vector3.up * _player.Stats.throwHeight;
            var all = Ball.Active;
            for (int i = all.Count - 1; i >= 0 && balls.Balls < balls.MaxBalls; i--)
            {
                var ball = all[i];
                if (!ball.IsCatchableBy(Team.Player, gameObject) || (ball.State == BallState.Live && ball.Stats.Has(HitFlags.Spiky)))
                    continue;
                if ((ball.Position - chest).sqrMagnitude > _catchRadius * _catchRadius)
                    continue;
                balls.ForceCatch(ball);
            }
        }
    }
}
