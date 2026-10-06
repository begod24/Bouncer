using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        const float Gravity = 25f;

        CharacterController _controller;
        PlayerStats _stats;
        PlayerModifiers _mods;
        Vector3 _velocity;
        Vector3 _knockback;
        float _verticalSpeed;
        float _dashStart = float.NegativeInfinity;
        float _dashEnd = float.NegativeInfinity;
        float _dashReadyAt;
        Vector3 _dashDirection;
        float _slowUntil;
        float _slowMultiplier = 1f;
        float _downUntil;

        public bool IsDashing => Time.time < _dashEnd;
        public bool IsDown => Time.time < _downUntil;
        public bool IsSlowed => Time.time < _slowUntil;
        public bool IsDashInvulnerable => Time.time < _dashStart + _stats.dashInvulnerability;
        public bool DashReady => Time.time >= _dashReadyAt;
        public float DashStartTime => _dashStart;

        public bool WasDashInvulnerable(float within) =>
            _dashStart > 0f && Time.time < _dashStart + _stats.dashInvulnerability + within;
        public Vector3 DashDirection => _dashDirection;
        public float DashReady01
        {
            get
            {
                float total = _stats.dashDuration + DashCooldown;
                return total <= 0f ? 1f : Mathf.Clamp01(1f - (_dashReadyAt - Time.time) / total);
            }
        }
        public Vector3 Velocity => (IsDashing ? DashVelocity : _velocity) + _knockback;
        public Vector3 ActualVelocity
        {
            get
            {
                if (_controller == null || !_controller.enabled)
                    return Vector3.zero;
                Vector3 velocity = _controller.velocity;
                velocity.y = 0f;
                return velocity;
            }
        }
        float DashCooldown => Mathf.Max(_stats.dashCooldownMin, _stats.dashCooldown * _mods.DashCooldown);
        Vector3 DashVelocity => _dashDirection * (_stats.dashDistance * _mods.DashDistance / Mathf.Max(0.01f, _stats.dashDuration));

        void Awake() => _controller = GetComponent<CharacterController>();

        public void Init(PlayerStats stats, PlayerModifiers mods)
        {
            _stats = stats;
            _mods = mods;
        }

        public bool TryDash(Vector3 direction)
        {
            if (!DashReady || IsDashing || IsDown)
                return false;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                direction = transform.forward;
            direction.y = 0f;
            _dashDirection = direction.normalized;
            _dashStart = Time.time;
            _dashEnd = Time.time + _stats.dashDuration;
            _dashReadyAt = _dashEnd + DashCooldown;
            _velocity = _dashDirection * (_stats.moveSpeed * _mods.MoveSpeed);
            GameEvents.PlaySound(SoundCue.Dash, transform.position);
            return true;
        }

        public void Tick(Vector3 move, float speedMultiplier, float dt)
        {
            if (dt <= 0f)
                return;

            float ground = _mods.IgnoreGround ? 1f : GroundZone.MoveMultiplierAt(transform.position);
            float slow = IsSlowed ? _slowMultiplier : 1f;
            if (IsDown)
                move = Vector3.zero;
            Vector3 target = move * (_stats.moveSpeed * _mods.MoveSpeed * speedMultiplier * ground * slow);
            float rate = target.sqrMagnitude >= _velocity.sqrMagnitude ? _stats.acceleration : _stats.deceleration;
            _velocity = Vector3.MoveTowards(_velocity, target, rate * dt);
            _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-_stats.knockbackDamping * dt));

            if (_controller.isGrounded && _verticalSpeed < 0f)
                _verticalSpeed = -2f;
            else
                _verticalSpeed -= Gravity * dt;

            Vector3 horizontal = IsDashing ? DashVelocity : _velocity;
            _controller.Move((horizontal + _knockback + Vector3.up * _verticalSpeed) * dt);
        }

        public void Face(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f)
                return;
            var target = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _stats.turnSpeed * dt);
        }

        public void AddKnockback(Vector3 velocity)
        {
            velocity.y = 0f;
            _knockback += velocity;
        }

        public void Slow(float multiplier, float seconds)
        {
            if (!IsSlowed)
                _slowMultiplier = 1f;
            _slowMultiplier = Mathf.Min(_slowMultiplier, Mathf.Clamp01(multiplier));
            _slowUntil = Mathf.Max(_slowUntil, Time.time + seconds);
        }

        public void KnockDown(float seconds)
        {
            _downUntil = Mathf.Max(_downUntil, Time.time + seconds);
            _velocity = Vector3.zero;
            _dashEnd = float.NegativeInfinity;
        }

        public void Stop()
        {
            _velocity = Vector3.zero;
            _knockback = Vector3.zero;
            _dashEnd = float.NegativeInfinity;
        }

        public void SetScripted(bool scripted)
        {
            Stop();
            _verticalSpeed = 0f;
            _controller.enabled = !scripted;
        }
    }
}
