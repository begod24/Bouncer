using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Upgrades;
using UnityEngine;

namespace Bouncer.Run
{
    /// <summary>
    /// Портфель с вкладышами: падает с элитного врага или лежит где-нибудь у края арены. Кто подобрал,
    /// выбирает 1 карточку из 3 (<see cref="OfferKind.Portfolio"/>). Покачивается и поблёскивает, чтобы его было видно.
    /// </summary>
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
        float _spawnTime;

        /// <summary>Портфель появился на месте, а не выпал из врага: сразу лежит.</summary>
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
            Vector2 side = Random.insideUnitCircle * popSpeed;
            _velocity = new Vector3(side.x, popUpSpeed, side.y);
            _landed = false;
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
            if (Time.time - _spawnTime >= pickupDelay && GameSession.IsPlayerActive)
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
                cards.QueueOffer(OfferKind.Portfolio);
                GameEvents.PlaySound(SoundCue.Portfolio, position);
                PoolService.Despawn(gameObject);
                return;
            }
        }
    }
}
