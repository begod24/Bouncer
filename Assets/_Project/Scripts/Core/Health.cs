using System;
using UnityEngine;

namespace Bouncer.Core
{
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1)] int max = 3;
        [Tooltip("Если больше 0 — здоровье восстанавливается до максимума, когда столько секунд не было урона. " +
                 "Так работает «серия попаданий» у неваляшки.")]
        [SerializeField, Min(0f)] float resetAfterSeconds;

        public int Max => max;
        public int Current { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsInvulnerable => Time.time < _invulnerableUntil;
        public float ResetAfterSeconds
        {
            get => resetAfterSeconds;
            set => resetAfterSeconds = Mathf.Max(0f, value);
        }

        public event Action<HitInfo> Damaged;
        public event Action<HitInfo> Died;
        public event Action<int> Healed;
        /// <summary>Здоровье снова полное (спавн или сброс серии).</summary>
        public event Action Restored;

        float _invulnerableUntil;
        float _lastDamageTime;

        void OnEnable() => Restore();

        public void Configure(int maxHealth, float resetAfter)
        {
            max = Mathf.Max(1, maxHealth);
            resetAfterSeconds = Mathf.Max(0f, resetAfter);
            Restore();
        }

        public void Restore()
        {
            Current = max;
            IsDead = false;
            _invulnerableUntil = 0f;
            Restored?.Invoke();
        }

        public bool TryDamage(in HitInfo hit)
        {
            if (IsDead || IsInvulnerable || hit.Damage <= 0)
                return false;

            Current = Mathf.Max(0, Current - hit.Damage);
            _lastDamageTime = Time.time;
            Damaged?.Invoke(hit);
            if (Current == 0)
            {
                IsDead = true;
                Died?.Invoke(hit);
            }
            return true;
        }

        public void Kill(in HitInfo hit)
        {
            if (IsDead)
                return;
            Current = 0;
            IsDead = true;
            Died?.Invoke(hit);
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0 || Current >= max)
                return;
            Current = Mathf.Min(max, Current + amount);
            Healed?.Invoke(amount);
        }

        /// <summary>Задать здоровье напрямую — например, сердца, перенесённые с прошлой арены.</summary>
        public void SetCurrent(int value)
        {
            if (IsDead)
                return;
            Current = Mathf.Clamp(value, 1, max);
        }

        public void SetInvulnerable(float seconds) =>
            _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + seconds);

        void Update()
        {
            if (resetAfterSeconds > 0f && !IsDead && Current < max && Time.time - _lastDamageTime >= resetAfterSeconds)
            {
                Current = max;
                Restored?.Invoke();
            }
        }
    }
}
