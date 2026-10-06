using System;
using Bouncer.Core;
using Bouncer.Player;
using UnityEngine;

namespace Bouncer.Run
{
    [DisallowMultipleComponent]
    public sealed class HealPickup : MonoBehaviour, IPoolable
    {
        [Tooltip("Модель: покачивается и поворачивается, корень остаётся на месте")]
        [SerializeField] Transform visual;
        [SerializeField, Min(1)] int heal = 1;
        [SerializeField] float pickupRadius = 1.3f;
        [SerializeField] float hoverHeight = 0.15f;
        [SerializeField] float bobHeight = 0.12f;
        [SerializeField] float turnSpeed = 60f;

        public static event Action<HealPickup> Spawned;
        public static event Action<HealPickup> Taken;

        public bool IsPuppet { get; set; }

        public void OnSpawned()
        {
            IsPuppet = false;
            var position = transform.position;
            position.y = hoverHeight;
            transform.position = position;
            Spawned?.Invoke(this);
        }

        public void OnDespawned() { }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            if (visual)
            {
                visual.localPosition = new Vector3(0f, (Mathf.Sin(Time.time * 3f) * 0.5f + 0.5f) * bobHeight, 0f);
                visual.localRotation = Quaternion.Euler(0f, turnSpeed * dt, 0f) * visual.localRotation;
            }
            if (GameSession.IsPlayerActive && !IsPuppet)
                TryPickUp(transform.position);
        }

        void TryPickUp(Vector3 position)
        {
            foreach (var target in Targetable.All)
            {
                if (target.Team != Team.Player || !target.IsAlive || target.Health == null)
                    continue;
                Vector3 delta = target.Position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude > pickupRadius * pickupRadius || target.Health.Current >= target.Health.Max)
                    continue;
                if (target.TryGetComponent(out PlayerController player) && !player.IsLocal)
                {
                    if (NetHooks.HealRemotePlayer == null || !NetHooks.HealRemotePlayer(player.gameObject, heal))
                        continue;
                }
                else
                {
                    target.Health.Heal(heal);
                }
                GameEvents.PlaySound(SoundCue.Purchase, position);
                Taken?.Invoke(this);
                PoolService.Despawn(gameObject);
                return;
            }
        }
    }
}
