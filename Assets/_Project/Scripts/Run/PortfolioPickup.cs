using System;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Upgrades;
using UnityEngine;

namespace Bouncer.Run
{
    [DisallowMultipleComponent]
    public sealed class PortfolioPickup : MonoBehaviour, IPoolable
    {
        [Tooltip("Модель: покачивается и поворачивается, корень остаётся на месте")]
        [SerializeField] Transform visual;
        [Tooltip("Блик над портфелем (включается, когда портфель лёг)")]
        [SerializeField] GameObject glint;
        [SerializeField] float pickupRadius = 1.3f;
        [Tooltip("Первые доли секунды подобрать нельзя — видно, что он выпал")]
        [SerializeField] float pickupDelay = 0.35f;

        [Header("Выпрыгивание и вид")]
        [SerializeField] float popUpSpeed = 6f;
        [SerializeField] float popSpeed = 1.2f;
        [SerializeField] float gravity = 22f;
        [SerializeField] float hoverHeight = 0.1f;
        [SerializeField] float bobHeight = 0.12f;
        [SerializeField] float turnSpeed = 60f;

        Vector3 _velocity;
        bool _landed;
        bool _announce;
        float _spawnTime;

        public bool IsPuppet { get; private set; }
        public Vector3 PopVelocity => _landed ? Vector3.zero : _velocity;
        public bool Landed => _landed;

        public static event Action<PortfolioPickup> Spawned;
        public static event Action<PortfolioPickup> Taken;

        public void BeginPuppet(Vector3 popVelocity)
        {
            IsPuppet = true;
            if (popVelocity.sqrMagnitude < 1e-4f)
                PlaceOnGround();
            else
                _velocity = popVelocity;
        }

        public void PlaceOnGround()
        {
            _velocity = Vector3.zero;
            var position = transform.position;
            position.y = hoverHeight;
            transform.position = position;
            _landed = true;
            if (glint)
                glint.SetActive(true);
        }

        public void OnSpawned()
        {
            Vector2 side = UnityEngine.Random.insideUnitCircle * popSpeed;
            _velocity = new Vector3(side.x, popUpSpeed, side.y);
            _landed = false;
            IsPuppet = false;
            _announce = true;
            _spawnTime = Time.time;
            if (glint)
                glint.SetActive(false);
        }

        public void OnDespawned() { }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            var position = transform.position;
            if (!_landed)
            {
                _velocity.y -= gravity * dt;
                position += _velocity * dt;
                if (position.y <= hoverHeight && _velocity.y < 0f)
                {
                    position.y = hoverHeight;
                    _landed = true;
                    if (glint)
                        glint.SetActive(true);
                }
                transform.position = position;
            }
            if (visual)
            {
                visual.localPosition = new Vector3(0f, _landed ? (Mathf.Sin(Time.time * 3f) * 0.5f + 0.5f) * bobHeight : 0f, 0f);
                visual.localRotation = Quaternion.Euler(0f, turnSpeed * dt, 0f) * visual.localRotation;
            }
            if (_announce)
            {
                _announce = false;
                if (!IsPuppet)
                    Spawned?.Invoke(this);
            }
            if (!IsPuppet && Time.time - _spawnTime >= pickupDelay && GameSession.IsPlayerActive)
                TryPickUp(position);
        }

        void TryPickUp(Vector3 position)
        {
            foreach (var target in Targetable.All)
            {
                if (target.Team != Team.Player || !target.IsAlive)
                    continue;
                Vector3 delta = target.Position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude > pickupRadius * pickupRadius || !target.TryGetComponent(out PlayerCards cards))
                    continue;
                if (!Online.Active)
                {
                    cards.QueueOffer(OfferKind.Portfolio);
                    GameEvents.PlaySound(SoundCue.Portfolio, position);
                }
                else if (cards.Player.IsLocal)
                {
                    cards.AddToBackpack();
                }
                else if (NetHooks.GivePortfolio == null || !NetHooks.GivePortfolio(target.gameObject))
                {
                    continue;
                }
                Taken?.Invoke(this);
                PoolService.Despawn(gameObject);
                return;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Spawned = null;
            Taken = null;
        }
    }
}
