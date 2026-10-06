using System;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Upgrades;
using UnityEngine;

namespace Bouncer.Run
{
    [DefaultExecutionOrder(100)]
    public sealed class Kiosk : MonoBehaviour
    {
        [Tooltip("Включается, когда ларёк открыт: свет в окошке, стрелка над крышей")]
        [SerializeField] GameObject[] openVisuals = Array.Empty<GameObject>();
        [Tooltip("Включается, пока ларёк закрыт")]
        [SerializeField] GameObject[] closedVisuals = Array.Empty<GameObject>();
        [Tooltip("Прыгающая стрелка над ларьком")]
        [SerializeField] Transform marker;
        [SerializeField] float markerBob = 0.25f;
        [Tooltip("Точка перед окошком: отсюда считается расстояние до игрока")]
        [SerializeField] Transform window;
        [SerializeField] float interactRadius = 2.4f;

        Vector3 _markerRest;

        public static Kiosk Instance { get; private set; }
        public bool IsOpen { get; private set; }
        public bool PlayerNear { get; private set; }
        public ShopStock Stock { get; private set; }
        public PlayerCards Customer { get; private set; }
        public int Interest { get; private set; }
        public Vector3 WindowPosition => window ? window.position : transform.position;

        public event Action ShopOpened;
        public event Action ShopClosed;

        void Awake()
        {
            Instance = this;
            if (marker)
                _markerRest = marker.localPosition;
            SetOpenVisuals(false);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Open(PlayerCards customer, int arenaIndex)
        {
            if (IsOpen || customer == null || customer.Deck == null)
                return;
            IsOpen = true;
            Customer = customer;
            int interest = customer.Player.Modifiers.CoinInterest;
            Interest = interest > 0 ? RunState.CoinsOf(customer.Slot) / customer.Deck.interestPer * interest : 0;
            if (Interest > 0)
                RunState.AddCoins(customer.Slot, Interest);
            Stock = new ShopStock(customer.Deck, customer, arenaIndex);
            SetOpenVisuals(true);
            GameEvents.PlaySound(SoundCue.KioskOpen, WindowPosition);
        }

        void Update()
        {
            if (marker && IsOpen)
                marker.localPosition = _markerRest + Vector3.up * (Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f)) * markerBob);

            PlayerNear = false;
            var session = GameSession.Instance;
            if (!IsOpen || Customer == null || session == null || session.State != SessionState.Cleared)
                return;
            var player = Customer.Player;
            if (player.IsDead)
                return;
            Vector3 delta = player.transform.position - WindowPosition;
            delta.y = 0f;
            PlayerNear = delta.sqrMagnitude <= interactRadius * interactRadius;
            if (PlayerNear && player.LastIntent.InteractPressed)
                OpenShop();
        }

        public void OpenShop()
        {
            var session = GameSession.Instance;
            if (!IsOpen || session == null || !session.BeginShop())
                return;
            GameEvents.PlaySound(SoundCue.UiMove, WindowPosition);
            ShopOpened?.Invoke();
        }

        public void CloseShop()
        {
            var session = GameSession.Instance;
            if (session == null || !session.IsShopOpen)
                return;
            session.EndShop();
            ShopClosed?.Invoke();
        }

        void SetOpenVisuals(bool open)
        {
            foreach (var go in openVisuals)
                if (go)
                    go.SetActive(open);
            foreach (var go in closedVisuals)
                if (go)
                    go.SetActive(!open);
            if (marker)
                marker.gameObject.SetActive(open);
        }
    }
}
