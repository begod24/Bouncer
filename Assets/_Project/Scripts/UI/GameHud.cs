using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Enemies;
using Bouncer.Player;
using Bouncer.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bouncer.UI
{
    /// <summary>
    /// HUD «мелом на асфальте»: жизни, мячи и «свечка», готовность ловли и рывка, опыт и уровень,
    /// время и счёт, полоса босса, полоса заряда над игроком, подсказка управления (H).
    /// Только показывает состояние игрока и забега, ничего в них не меняет. Экраны (пауза и т.п.) — в RunScreens.
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
        [SerializeField] TMP_Text candleLabel;

        [Header("Ловля и рывок")]
        [SerializeField] UnityEngine.UI.Image catchIcon;
        [SerializeField] UnityEngine.UI.Image dashIcon;
        [SerializeField] Color readyColor = new(0.96f, 0.95f, 0.92f);
        [SerializeField] Color activeColor = new(0.49f, 0.73f, 0.31f);
        [SerializeField] Color cooldownColor = new(1f, 1f, 1f, 0.3f);

        [Header("Опыт")]
        [SerializeField] TMP_Text levelLabel;
        [Tooltip("Заливка полосы опыта: растягивается по ширине от левого края")]
        [SerializeField] RectTransform xpFill;

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
        PlayerProgression _progression;
        RectTransform _canvasRect;
        float _xpShown;
        int _levelShown;
        float _bossShown = 1f;
        bool _helpWanted = true;

        void Awake()
        {
            iconTemplate.gameObject.SetActive(false);
            _canvasRect = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
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
                bool title = session != null && session.State == SessionState.Title;
                hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, title ? 0f : 1f, Time.unscaledDeltaTime * 4f);
            }

            if (player == null)
            {
                player = FindFirstObjectByType<PlayerController>();
                if (player == null)
                    return;
                player.TryGetComponent(out _progression);
            }

            UpdateLives();
            UpdateBalls();
            UpdateAbilities();
            UpdateExperience();
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
            Color full = balls.CandleReady ? Color.Lerp(candleColor, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f)) : ballColor;
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
        }

        void UpdateExperience()
        {
            if (_progression == null)
                return;
            if (_levelShown != _progression.Level)
            {
                // Новый уровень: полоса добегает до конца и начинается заново.
                _xpShown = _levelShown == 0 ? _progression.Experience01 : 0f;
                _levelShown = _progression.Level;
                levelLabel.text = $"ур. {_levelShown}";
            }
            _xpShown = Mathf.MoveTowards(_xpShown, _progression.Experience01, Time.unscaledDeltaTime * 1.5f);
            xpFill.anchorMax = new Vector2(Mathf.Max(0.02f, _xpShown), xpFill.anchorMax.y);
            xpFill.gameObject.SetActive(_xpShown > 0.001f);
        }

        void UpdateRun()
        {
            var session = GameSession.Instance;
            if (session == null)
                return;
            int seconds = Mathf.FloorToInt(session.SurvivalTime);
            timerLabel.text = $"{seconds / 60}:{seconds % 60:00}";
            killsLabel.text = $"выбито: {session.Kills}";
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
            bossName.text = parts[0].BossName;
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
