using Bouncer.Core;
using Bouncer.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bouncer.UI
{
    /// <summary>
    /// Временный HUD прототипа на IMGUI: жизни, мячи, заряд, ловля, рывок, время, счёт, пауза и поражение.
    /// Финальный HUD (мел на асфальте, карточки-вкладыши) — этап 2.
    /// </summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] float referenceHeight = 1080f;
        [SerializeField] bool showHelp = true;

        [Header("Цвета")]
        [SerializeField] Color heartColor = new(0.95f, 0.25f, 0.3f);
        [SerializeField] Color ballColor = new(1f, 0.45f, 0.2f);
        [SerializeField] Color emptyColor = new(1f, 1f, 1f, 0.15f);
        [SerializeField] Color candleColor = new(1f, 0.8f, 0.2f);
        [SerializeField] Color catchColor = new(0.35f, 1f, 0.45f);

        GUIStyle _label;
        GUIStyle _big;
        GUIStyle _huge;
        GUIStyle _small;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.hKey.wasPressedThisFrame)
                showHelp = !showHelp;
            if (player == null)
                player = FindFirstObjectByType<PlayerController>();
        }

        void OnGUI()
        {
            if (player == null)
                return;
            EnsureStyles();

            float scale = Screen.height / referenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = referenceHeight;

            DrawPlayerPanel();
            DrawRunPanel(width);
            DrawChargeBar(scale);
            if (showHelp)
                DrawHelp(height);
            DrawOverlays(width, height);
        }

        void DrawPlayerPanel()
        {
            var balls = player.Balls;
            float x = 24f, y = 20f;

            GUI.Label(new Rect(x, y, 200f, 28f), "ЖИЗНИ", _small);
            DrawPips(x, y + 24f, player.Health.Current, player.Health.Max, heartColor, 26f);

            y += 64f;
            GUI.Label(new Rect(x, y, 200f, 28f), "МЯЧИ", _small);
            int shown = Mathf.Max(balls.MaxBalls, balls.Balls);
            DrawPips(x, y + 24f, balls.Balls, shown, ballColor, 26f);

            y += 64f;
            string catchText = balls.IsCatching ? "ЛОВЛЮ!" : balls.CatchOnCooldown ? "ловля..." : "ловля готова";
            Color catchTextColor = balls.IsCatching ? catchColor : balls.CatchOnCooldown ? emptyColor * 3f : Color.white;
            DrawText(new Rect(x, y, 260f, 26f), catchText, _label, catchTextColor);
            string dashText = player.Motor.DashReady ? "рывок готов" : "рывок...";
            DrawText(new Rect(x, y + 26f, 260f, 26f), dashText, _label, player.Motor.DashReady ? Color.white : emptyColor * 3f);

            if (balls.CandleReady)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f);
                DrawText(new Rect(x, y + 60f, 460f, 36f), "СВЕЧКА! Следующий бросок усиленный", _big, candleColor * pulse);
            }
            if (GameSettings.GodMode)
                DrawText(new Rect(x, y + 100f, 300f, 26f), "[бессмертие]", _small, emptyColor * 4f);
        }

        void DrawRunPanel(float width)
        {
            var session = GameSession.Instance;
            if (session == null)
                return;
            int seconds = Mathf.FloorToInt(session.SurvivalTime);
            DrawText(new Rect(width - 244f, 20f, 220f, 40f), $"{seconds / 60}:{seconds % 60:00}", _big, Color.white, TextAnchor.UpperRight);
            DrawText(new Rect(width - 244f, 60f, 220f, 30f), $"выбито: {session.Kills}", _label, Color.white, TextAnchor.UpperRight);
        }

        void DrawChargeBar(float scale)
        {
            var balls = player.Balls;
            var camera = Camera.main;
            if (!balls.IsCharging || camera == null || player.IsDead)
                return;
            Vector3 screen = camera.WorldToScreenPoint(player.transform.position + Vector3.up * 2.3f);
            if (screen.z < 0f)
                return;
            float x = screen.x / scale - 40f;
            float y = (Screen.height - screen.y) / scale;
            var back = new Rect(x, y, 80f, 10f);
            GUI.DrawTexture(back, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.5f), 0f, 4f);
            var fill = new Rect(x, y, 80f * Mathf.Max(0.02f, balls.Charge01), 10f);
            Color color = balls.Charge01 >= 1f ? candleColor : Color.white;
            GUI.DrawTexture(fill, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 4f);
        }

        void DrawHelp(float height)
        {
            const string help =
                "WASD — бег    мышь — прицел    ЛКМ — бросок (держи — заряд)\n" +
                "ПКМ / E — ловля    Space / Shift — рывок    Esc — пауза\n" +
                "Геймпад: левый стик, правый стик, RT бросок, LT ловля, A рывок\n" +
                "H — скрыть подсказку    F1 — отладка";
            DrawText(new Rect(24f, height - 110f, 900f, 100f), help, _small, new Color(1f, 1f, 1f, 0.6f));
        }

        void DrawOverlays(float width, float height)
        {
            var session = GameSession.Instance;
            if (session == null)
                return;

            if (session.State == SessionState.GameOver)
            {
                Dim(width, height, 0.45f);
                int seconds = Mathf.FloorToInt(session.SurvivalTime);
                DrawText(new Rect(0f, height * 0.33f, width, 90f), "ВЫБИТ!", _huge, heartColor, TextAnchor.MiddleCenter);
                DrawText(new Rect(0f, height * 0.33f + 90f, width, 40f),
                    $"продержался {seconds / 60}:{seconds % 60:00}    выбито врагов: {session.Kills}",
                    _big, Color.white, TextAnchor.MiddleCenter);
                if (session.CanRestart)
                    DrawText(new Rect(0f, height * 0.33f + 140f, width, 40f), "R / Enter / Start — ещё раз",
                        _label, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
            }
            else if (GameFeel.Paused)
            {
                Dim(width, height, 0.35f);
                DrawText(new Rect(0f, height * 0.4f, width, 90f), "ПАУЗА", _huge, Color.white, TextAnchor.MiddleCenter);
            }
        }

        // ---------- Рисование ----------

        static void DrawPips(float x, float y, int filled, int total, Color color, float size)
        {
            for (int i = 0; i < total; i++)
            {
                var rect = new Rect(x + i * (size + 8f), y, size, size);
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                    i < filled ? color : new Color(1f, 1f, 1f, 0.15f), 0f, size * 0.5f);
            }
        }

        static void Dim(float width, float height, float alpha) =>
            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                new Color(0f, 0f, 0f, alpha), 0f, 0f);

        static void DrawText(Rect rect, string text, GUIStyle style, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            style.alignment = anchor;
            var shadow = new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height);
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, color.a * 0.6f);
            GUI.Label(shadow, text, style);
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        void EnsureStyles()
        {
            if (_label != null)
                return;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 20, normal = { textColor = Color.white } };
            _big = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _huge = new GUIStyle(GUI.skin.label) { fontSize = 72, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }
    }
}
