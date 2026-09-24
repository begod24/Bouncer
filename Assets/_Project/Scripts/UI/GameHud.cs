using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Enemies;
using Bouncer.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Serialization;

namespace Bouncer.UI
{
    /// <summary>
    /// HUD «мелом на асфальте»: жизни, мячи и «свечка», готовность ловли, рывка и крышки, монетки,
    /// время арены и счёт, полоса босса, полоса заряда над игроком, подсказка управления (H).
    /// Только показывает состояние игрока и прогулки, ничего в них не меняет. Экраны (пауза и т.п.) — в RunScreens.
    /// Надписи со счётом переписываются, только когда меняется число или язык.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        [Tooltip("Пусто — найдёт игрока в сцене сам")]
        [SerializeField] PlayerController player;
        [Tooltip("Весь HUD: прячется на заставке")]
        [SerializeField] CanvasGroup hudGroup;

        [Header("Жизни и мячи")]
        [SerializeField] RectTransform livesRow;
        [SerializeField] RectTransform ballsRow;
        [Tooltip("Выключенный образец значка: из него строятся сердечки и мячи")]
        [SerializeField] UnityEngine.UI.Image iconTemplate;
        [SerializeField] Sprite heartFull;
        [SerializeField] Sprite heartEmpty;
        [SerializeField] Sprite ballFull;
        [SerializeField] Sprite ballEmpty;
        [SerializeField] Color heartColor = new(0.93f, 0.35f, 0.31f);
        [SerializeField] Color ballColor = new(0.97f, 0.69f, 0.29f);
        [SerializeField] Color emptyColor = new(1f, 1f, 1f, 0.45f);
        [SerializeField] Color candleColor = new(0.96f, 0.82f, 0.24f);
        [Tooltip("Горячая картошка: следующий бросок взорвётся")]
        [SerializeField] Color hotColor = new(0.93f, 0.33f, 0.16f);
        [SerializeField] TMP_Text candleLabel;

        [Header("Ловля и рывок")]
        [SerializeField] UnityEngine.UI.Image catchIcon;
        [SerializeField] UnityEngine.UI.Image dashIcon;
        [Tooltip("«Крышка от кастрюли»: видна, только когда карточка взята")]
        [SerializeField] UnityEngine.UI.Image lidIcon;
        [SerializeField] Color readyColor = new(0.96f, 0.95f, 0.92f);
        [SerializeField] Color activeColor = new(0.49f, 0.73f, 0.31f);
        [SerializeField] Color cooldownColor = new(1f, 1f, 1f, 0.3f);

        [Header("Монетки")]
        [FormerlySerializedAs("levelLabel")]
        [SerializeField] TMP_Text coinsLabel;
        [Tooltip("Значок монетки: подпрыгивает, когда монетки прибавились")]
        [SerializeField] RectTransform coinIcon;
        [SerializeField] float coinCountSpeed = 40f;

        [Header("Забег")]
        [SerializeField] TMP_Text timerLabel;
        [SerializeField] TMP_Text killsLabel;

        [Header("Босс")]
        [SerializeField] CanvasGroup bossBar;
        [SerializeField] TMP_Text bossName;
        [Tooltip("Заливка полосы босса: растягивается по ширине от левого края")]
        [SerializeField] RectTransform bossFill;

        [Header("Заряд броска над игроком")]
        [SerializeField] RectTransform chargeBar;
        [SerializeField] RectTransform chargeFill;
        [SerializeField] UnityEngine.UI.Image chargeFillImage;
        [SerializeField] float chargeBarHeight = 2.4f;

        [Header("Подсказка управления")]
        [SerializeField] GameObject help;

        readonly List<UnityEngine.UI.Image> _hearts = new();
        readonly List<UnityEngine.UI.Image> _balls = new();
        RectTransform _canvasRect;
        float _coinsShown = -1f;
        int _coinsLabelValue = -1;
        float _coinBump;
        int _killsShown = -1;
        float _bossShown = 1f;
        BossSplit _bossNamed;
        bool _helpWanted = true;

        void Awake()
        {
            iconTemplate.gameObject.SetActive(false);
            _canvasRect = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
        }

        void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            RunState.CoinsChanged += OnCoinsChanged;
        }

        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            RunState.CoinsChanged -= OnCoinsChanged;
        }

        void OnLocaleChanged(Locale locale)
        {
            _killsShown = -1;
            _bossNamed = null;
        }

        void OnCoinsChanged(int delta)
        {
            if (delta > 0)
                _coinBump = 1f;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.hKey.wasPressedThisFrame)
                _helpWanted = !_helpWanted;
            // Подсказка только посреди игры: под паузой, карточками и «Выбит!» она мешает.
            if (help)
                help.SetActive(_helpWanted && GameSession.IsGameplayActive);
            var session = GameSession.Instance;
            if (hudGroup)
            {
                // На заставке и у витрины ларька HUD прячется: у витрины свои монетки.
                bool hidden = session != null && session.State is (SessionState.Title or SessionState.Shop);
                hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, hidden ? 0f : 1f, Time.unscaledDeltaTime * 4f);
            }

            UpdateCoins();
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerController>();
                if (player == null)
                    return;
            }

            UpdateLives();
            UpdateBalls();
            UpdateAbilities();
            UpdateRun();
            UpdateBoss();
        }

        void LateUpdate()
        {
            if (player != null)
                UpdateChargeBar();
        }

        // ---------- Игрок ----------

        void UpdateLives()
        {
            var health = player.Health;
            Resize(_hearts, livesRow, health.Max);
            for (int i = 0; i < _hearts.Count; i++)
                SetIcon(_hearts[i], i < health.Current, heartFull, heartEmpty, heartColor);
        }

        void UpdateBalls()
        {
            var balls = player.Balls;
            Resize(_balls, ballsRow, Mathf.Max(balls.MaxBalls, balls.Balls));
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
            Color full = balls.CandleReady ? Color.Lerp(candleColor, Color.white, pulse)
                : balls.CatchPerksReady ? Color.Lerp(hotColor, ballColor, pulse)
                : ballColor;
            for (int i = 0; i < _balls.Count; i++)
                SetIcon(_balls[i], i < balls.Balls, ballFull, ballEmpty, full);
            if (candleLabel)
                candleLabel.gameObject.SetActive(balls.CandleReady && !player.IsDead);
        }

        void UpdateAbilities()
        {
            var balls = player.Balls;
            if (catchIcon)
            {
                catchIcon.fillAmount = balls.CatchOnCooldown ? 1f - balls.CatchCooldown01 : 1f;
                catchIcon.color = balls.IsCatching ? activeColor : balls.CatchOnCooldown ? cooldownColor : readyColor;
            }
            if (dashIcon)
            {
                float dash = player.Motor.DashReady01;
                dashIcon.fillAmount = dash;
                dashIcon.color = dash >= 1f ? readyColor : cooldownColor;
            }
            if (lidIcon)
            {
                bool has = player.HasLid;
                if (lidIcon.gameObject.activeSelf != has)
                    lidIcon.gameObject.SetActive(has);
                if (has)
                {
                    float lid = player.LidReady01;
                    lidIcon.fillAmount = lid;
                    lidIcon.color = lid >= 1f ? readyColor : cooldownColor;
                }
            }
        }

        /// <summary>Монетки: число догоняет настоящее, значок подпрыгивает на каждую прибавку.</summary>
        void UpdateCoins()
        {
            if (coinsLabel == null)
                return;
            int coins = RunState.Coins;
            _coinsShown = _coinsShown < 0f || coins < _coinsShown
                ? coins
                : Mathf.MoveTowards(_coinsShown, coins, Time.unscaledDeltaTime * Mathf.Max(coinCountSpeed, (coins - _coinsShown) * 4f));
            int shown = Mathf.RoundToInt(_coinsShown);
            if (shown != _coinsLabelValue)
            {
                _coinsLabelValue = shown;
                coinsLabel.text = shown.ToString();
            }
            if (coinIcon)
            {
                _coinBump = Mathf.MoveTowards(_coinBump, 0f, Time.unscaledDeltaTime * 5f);
                coinIcon.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(_coinBump * Mathf.PI));
            }
        }

        void UpdateRun()
        {
            var session = GameSession.Instance;
            if (session == null)
                return;
            int seconds = Mathf.FloorToInt(session.SurvivalTime);
            timerLabel.text = $"{seconds / 60}:{seconds % 60:00}";
            if (session.RunKills != _killsShown)
            {
                _killsShown = session.RunKills;
                killsLabel.text = Loc.Format("hud.kills", _killsShown);
            }
        }

        void UpdateBoss()
        {
            if (!bossBar)
                return;
            var parts = BossSplit.Alive;
            bool visible = parts.Count > 0;
            bossBar.alpha = Mathf.MoveTowards(bossBar.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 3f);
            if (!visible)
            {
                _bossShown = 1f;
                return;
            }
            if (parts[0] != _bossNamed)
            {
                _bossNamed = parts[0];
                bossName.text = _bossNamed.BossName.GetLocalizedString();
            }
            _bossShown = Mathf.MoveTowards(_bossShown, BossSplit.Remaining01, Time.unscaledDeltaTime * 0.8f);
            bossFill.anchorMax = new Vector2(Mathf.Max(0.01f, _bossShown), bossFill.anchorMax.y);
        }

        void UpdateChargeBar()
        {
            var balls = player.Balls;
            var camera = Camera.main;
            bool show = false;
            if (balls.IsCharging && !player.IsDead && camera != null)
            {
                Vector3 screen = camera.WorldToScreenPoint(player.transform.position + Vector3.up * chargeBarHeight);
                if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local))
                {
                    show = true;
                    chargeBar.anchoredPosition = local;
                    chargeFill.anchorMax = new Vector2(Mathf.Max(0.04f, balls.Charge01), 1f);
                    chargeFillImage.color = balls.Charge01 >= 1f ? candleColor : readyColor;
                }
            }
            if (chargeBar.gameObject.activeSelf != show)
                chargeBar.gameObject.SetActive(show);
        }

        // ---------- Значки ----------

        void Resize(List<UnityEngine.UI.Image> icons, RectTransform row, int count)
        {
            while (icons.Count < count)
            {
                var icon = Instantiate(iconTemplate, row);
                icon.gameObject.SetActive(true);
                icons.Add(icon);
            }
            for (int i = 0; i < icons.Count; i++)
            {
                bool visible = i < count;
                if (icons[i].gameObject.activeSelf != visible)
                    icons[i].gameObject.SetActive(visible);
            }
        }

        void SetIcon(UnityEngine.UI.Image icon, bool full, Sprite fullSprite, Sprite emptySprite, Color color)
        {
            icon.sprite = full ? fullSprite : emptySprite;
            icon.color = full ? color : emptyColor;
        }
    }
}
