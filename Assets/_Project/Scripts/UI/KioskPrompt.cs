using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Run;
using TMPro;
using UnityEngine;

namespace Bouncer.UI
{
    /// <summary>
    /// Подсказка над открытым ларьком: издалека — бледная «Ларёк открыт», у окошка — яркая «F — ларёк»
    /// (у геймпада — своя кнопка). Висит над ларьком в мире, пересчитывается в экранные координаты.
    /// </summary>
    public sealed class KioskPrompt : MonoBehaviour
    {
        [SerializeField] RectTransform prompt;
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text label;
        [Tooltip("Высота над окошком ларька, м")]
        [SerializeField] float height = 2.4f;
        [SerializeField, Range(0f, 1f)] float farAlpha = 0.55f;
        [SerializeField] string keyboardKey = "F";
        [SerializeField] string gamepadKey = "X";

        RectTransform _canvasRect;
        PlayerController _player;
        int _mode = -1;

        void Awake()
        {
            _canvasRect = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
            group.alpha = 0f;
        }

        void LateUpdate()
        {
            var kiosk = Kiosk.Instance;
            var session = GameSession.Instance;
            var camera = Camera.main;
            bool show = kiosk != null && kiosk.IsOpen && session != null && session.State == SessionState.Cleared
                        && !GameFeel.Paused && camera != null;
            if (show)
            {
                Vector3 screen = camera.WorldToScreenPoint(kiosk.WindowPosition + Vector3.up * height);
                if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local))
                {
                    prompt.anchoredPosition = local;
                    UpdateLabel(kiosk.PlayerNear);
                }
                else
                {
                    show = false;
                }
            }
            float target = !show ? 0f : kiosk.PlayerNear ? 1f : farAlpha;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 6f);
            prompt.localScale = Vector3.one * (show && kiosk.PlayerNear ? 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 6f) : 0.9f);
        }

        void UpdateLabel(bool near)
        {
            if (_player == null)
                _player = FindFirstObjectByType<PlayerController>();
            bool gamepad = _player != null && _player.LastIntent.UsingGamepad;
            int mode = near ? gamepad ? 2 : 1 : 0;
            if (mode == _mode)
                return;
            _mode = mode;
            label.text = near ? Loc.Format("kiosk.prompt", gamepad ? gamepadKey : keyboardKey) : Loc.Get("kiosk.open");
        }
    }
}
