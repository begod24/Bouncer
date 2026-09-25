using Bouncer.Core;
using Bouncer.Run;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Bouncer.UI
{
    /// <summary>
    /// Часы до зова мамы в HUD финала: панелька мелом, окна в ней загораются одно за другим, последним — наше
    /// (с сердечком); под ней — «до зова», а когда мама позвала — «Домой!». На других аренах спрятаны.
    /// Сколько секунд осталось, показывает таймер HUD (<see cref="GameHud"/>).
    /// </summary>
    public sealed class CallClock : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [Tooltip("Окна панельки в том порядке, в котором загораются (наше — отдельно)")]
        [SerializeField] Image[] windows;
        [SerializeField] Image ourWindow;
        [SerializeField] Sprite darkSprite;
        [SerializeField] Sprite litSprite;
        [SerializeField] Color darkColor = new(1f, 1f, 1f, 0.45f);
        [SerializeField] Color litColor = new(1f, 0.87f, 0.45f);
        [SerializeField] Color ourColor = new(1f, 0.62f, 0.32f);
        [SerializeField] TMP_Text label;
        [Tooltip("Что подпрыгивает, когда загорается окно")]
        [SerializeField] RectTransform punch;
        [SerializeField] float fadeSpeed = 4f;

        int _shown = -1;
        bool _called;
        bool _labelDirty = true;
        float _bump;
        Vector3 _punchScale = Vector3.one;

        void Awake()
        {
            group.alpha = 0f;
            if (punch)
                _punchScale = punch.localScale;
        }

        void OnEnable() => LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

        void OnLocaleChanged(Locale locale) => _labelDirty = true;

        void Update()
        {
            var home = HomeCall.Instance;
            var session = GameSession.Instance;
            bool finale = home != null && home.IsFinale;
            bool visible = finale && (session == null || session.State is not (SessionState.Title or SessionState.Shop));
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
            if (!finale)
                return;

            int count = windows != null ? windows.Length : 0;
            int lit = home.Called ? count : Mathf.FloorToInt(home.Progress01 * count);
            if (lit != _shown)
            {
                if (_shown >= 0 && lit > _shown)
                    _bump = 1f;
                _shown = lit;
                for (int i = 0; i < count; i++)
                    SetWindow(windows[i], i < lit, litColor);
            }
            if (home.Called != _called || _labelDirty)
            {
                if (home.Called != _called)
                    _bump = 1f;
                _called = home.Called;
                _labelDirty = false;
                SetWindow(ourWindow, _called, ourColor);
                if (label)
                    label.text = Loc.Get(_called ? "hud.call.home" : "hud.call");
            }
            if (punch)
            {
                _bump = Mathf.MoveTowards(_bump, 0f, Time.unscaledDeltaTime * 4f);
                punch.localScale = _punchScale * (1f + 0.12f * Mathf.Sin(_bump * Mathf.PI));
            }
        }

        void SetWindow(Image window, bool on, Color onColor)
        {
            if (!window)
                return;
            window.sprite = on ? litSprite : darkSprite;
            window.color = on ? onColor : darkColor;
        }
    }
}
