using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    public sealed class SoapBubble : MonoBehaviour, IPoolable
    {
        [Tooltip("Радиус пузыря, м: мяч ближе этого застревает")]
        [SerializeField, Min(0.1f)] float radius = 0.5f;
        [Tooltip("На какой высоте плывёт пузырь, м")]
        [SerializeField] float height = 1.1f;
        [Tooltip("Как быстро гаснет скорость пузыря")]
        [SerializeField, Min(0f)] float drag = 1.1f;
        [Tooltip("С пойманным мячом пузырь плывёт во столько раз медленнее")]
        [SerializeField, Range(0.05f, 1f)] float heldSlow = 0.3f;
        [Tooltip("Капли и кольцо, когда пузырь лопается, из пула")]
        [SerializeField] ParticleBurst popBurst;
        [Tooltip("Модель пузыря (масштабируется под радиус)")]
        [SerializeField] Transform body;

        Vector3 _velocity;
        float _start;
        float _life;
        float _hold;
        float _phase;
        bool _authority;
        Ball _held;
        int _heldLife;
        float _heldAt;
        bool _popped;

        public void OnSpawned()
        {
            _held = null;
            _popped = false;
            _start = Time.time;
            _phase = Random.value * 10f;
            if (body)
                body.localScale = Vector3.zero;
        }

        public void OnDespawned() => _held = null;

        public void Play(Vector3 velocity, float life, float hold, bool authority)
        {
            _velocity = velocity;
            _life = life;
            _hold = hold;
            _authority = authority;
            _start = Time.time;
        }

        void Update()
        {
            float age = Time.time - _start;
            float dt = Time.deltaTime;
            if (_popped)
                return;
            bool holding = _held != null;
            _velocity *= Mathf.Exp(-drag * dt);
            Vector3 position = transform.position;
            Vector3 step = _velocity * (holding ? heldSlow : 1f) * dt;
            step.y = (height + Mathf.Sin(age * 2.3f + _phase) * 0.15f - position.y) * Mathf.Min(1f, dt * 3f);
            step += new Vector3(Mathf.Sin(age * 1.7f + _phase), 0f, Mathf.Cos(age * 1.3f + _phase)) * (0.25f * dt);
            position += step;
            transform.position = position;

            if (body)
            {
                float grow = Mathf.Clamp01(age / 0.25f);
                float wob = 1f + 0.06f * Mathf.Sin(age * 11f + _phase);
                body.localScale = new Vector3(wob, 2f - wob, wob) * (radius * 2f * grow);
            }

            if (age >= _life || (holding && Time.time - _heldAt >= _hold)
                || Physics.CheckSphere(position, radius * 0.5f, Layers.BallSolidMask, QueryTriggerInteraction.Ignore))
            {
                Pop();
                return;
            }
            if (holding)
            {
                if (!_held.isActiveAndEnabled || _held.Life != _heldLife)
                    _held = null;
                else if (_authority && _held.State == BallState.Stuck)
                    _held.HoldAt(position);
                return;
            }
            TryCatch(position);
        }

        void TryCatch(Vector3 position)
        {
            var balls = Ball.Active;
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                var ball = balls[i];
                bool live = ball.State == BallState.Live && ball.Team.IsHostileTo(Team.Player);
                if (!live || ball.IsPhantom)
                    continue;
                float reach = radius + ball.Radius;
                if ((ball.Position - position).sqrMagnitude > reach * reach)
                    continue;
                _held = ball;
                _heldLife = ball.Life;
                _heldAt = Time.time;
                if (ball.IsPuppet)
                    ball.Neutralize();
                else if (_authority)
                    ball.Stick(position);
                GameEvents.PlaySound(SoundCue.BubbleBlow, position);
                return;
            }
        }

        void Pop()
        {
            _popped = true;
            Vector3 position = transform.position;
            if (_held != null && _authority && !_held.IsPuppet && _held.isActiveAndEnabled && _held.Life == _heldLife
                && _held.State == BallState.Stuck)
                _held.Drop(position, Vector3.down * 0.5f);
            _held = null;
            if (popBurst)
                PoolService.Spawn(popBurst, position, Quaternion.identity);
            GameEvents.PlaySound(SoundCue.BubblePop, position);
            PoolService.Despawn(gameObject);
        }
    }
}
