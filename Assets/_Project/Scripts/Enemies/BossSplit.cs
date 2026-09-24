using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.Localization;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Часть босса «Большая неваляшка»: выбитая, раскалывается на несколько неваляшек поменьше
    /// (большая → две средние → по две маленькие). Держит общий реестр живых частей — по нему
    /// спавнер понимает, что босс побеждён, а HUD рисует общую полосу здоровья.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class BossSplit : MonoBehaviour, IPoolable
    {
        static readonly List<BossSplit> s_alive = new();

        [Tooltip("Имя на полосе босса — строка таблицы «Content»")]
        [SerializeField] LocalizedString bossName = new("Content", "boss.big_roly_poly");
        [Tooltip("Кто появляется, когда эта часть выбита. Пусто — последняя ступень")]
        [SerializeField] GameObject childPrefab;
        [SerializeField, Min(0)] int childCount = 2;
        [Tooltip("На каком расстоянии в стороны появляются половинки")]
        [SerializeField] float spread = 1.2f;
        [Tooltip("С какой скоростью половинки разлетаются")]
        [SerializeField] float childImpulse = 5f;
        [SerializeField] float childStun = 0.8f;

        [Header("Общая полоса здоровья босса (доли от 1)")]
        [Tooltip("Сколько полосы стоит здоровье этой части")]
        [SerializeField] float barShare = 0.3333f;
        [Tooltip("Сколько полосы стоят все её будущие половинки")]
        [SerializeField] float barReserve = 0.6667f;

        Health _health;

        public static IReadOnlyList<BossSplit> Alive => s_alive;
        public LocalizedString BossName => bossName;

        /// <summary>Сколько осталось от всего босса: 1 — целый, 0 — выбит полностью.</summary>
        public static float Remaining01
        {
            get
            {
                float remaining = 0f;
                foreach (var part in s_alive)
                    remaining += part.barShare * part._health.Current / Mathf.Max(1, part._health.Max) + part.barReserve;
                return Mathf.Clamp01(remaining);
            }
        }

        void Awake()
        {
            _health = GetComponent<Health>();
            _health.Died += OnDied;
        }

        void OnEnable() => s_alive.Add(this);

        void OnDisable() => s_alive.Remove(this);

        public void OnSpawned() { }

        public void OnDespawned() { }

        void OnDied(HitInfo hit)
        {
            if (childPrefab == null || childCount <= 0)
                return;

            Vector3 origin = transform.position;
            GameEvents.PlaySound(SoundCue.BossSplit, origin);
            Vector3 away = hit.Direction;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f)
                away = transform.forward;
            away.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, away);

            for (int i = 0; i < childCount; i++)
            {
                // Половинки разлетаются в стороны поперёк удара и немного вперёд по нему.
                float t = childCount == 1 ? 0f : i / (childCount - 1f) * 2f - 1f;
                Vector3 offset = side * (t * spread);
                var child = PoolService.Spawn(childPrefab, origin + offset + Vector3.up * 0.2f, transform.rotation);
                if (child.TryGetComponent(out Rigidbody body))
                    body.AddForce((side * t + away * 0.6f).normalized * childImpulse + Vector3.up * (childImpulse * 0.4f), ForceMode.VelocityChange);
                if (child.TryGetComponent(out RolyPolyEnemy rolyPoly))
                    rolyPoly.Stun(childStun);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_alive.Clear();
    }
}
