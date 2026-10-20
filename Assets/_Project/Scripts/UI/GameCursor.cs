using Bouncer.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Bouncer.UI
{
    /// <summary>
    /// Курсор мышью, нарисованный мелом. Системный курсор прячем всегда: в геймплее курсора нет вовсе,
    /// в меню (заставка, пауза, карточки, киоск, итоги) вместо него рисуется этот, пока игрок водит мышью.
    /// Взялся за геймпад или клавиатуру — курсор уходит, как только мышь снова двинется — возвращается.
    /// </summary>
    public sealed class GameCursor : MonoBehaviour
    {
        const string PrefabPath = "GameCursor";

        [SerializeField] RectTransform root;
        [SerializeField] CanvasGroup group;
        [Tooltip("Размер курсора в пикселях при высоте экрана 1080")]
        [SerializeField] float size = 56f;
        [SerializeField] float hoverScale = 1.12f;
        [SerializeField] float pressScale = 0.86f;
        [SerializeField] float fadeSpeed = 14f;
        [Tooltip("Сколько пикселей мышь должна пройти, чтобы курсор вернулся после геймпада")]
        [SerializeField] float wakeDistance = 2f;

        static GameCursor s_instance;

        bool _mouseActive = true;
        Vector2 _lastPosition;
        float _scale = 1f;
        PointerEventData _pointer;
        readonly System.Collections.Generic.List<RaycastResult> _hits = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (s_instance != null)
                return;
            var prefab = Resources.Load<GameCursor>(PrefabPath);
            if (prefab == null)
                return;
            s_instance = Instantiate(prefab);
            s_instance.name = "GameCursor";
            DontDestroyOnLoad(s_instance.gameObject);
        }

        void Awake()
        {
            Cursor.visible = false;
            group.alpha = 0f;
            if (Mouse.current != null)
                _lastPosition = Mouse.current.position.ReadValue();
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
            Cursor.visible = true;
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused)
                Cursor.visible = false;
        }

        void Update()
        {
            Cursor.visible = false;
            var mouse = Mouse.current;
            if (mouse == null)
            {
                group.alpha = 0f;
                return;
            }

            Vector2 position = mouse.position.ReadValue();
            bool clicked = mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame;
            if (clicked || (position - _lastPosition).sqrMagnitude > wakeDistance * wakeDistance)
                _mouseActive = true;
            _lastPosition = position;
            if (OtherDeviceUsed())
                _mouseActive = false;

            bool inside = Application.isFocused && position.x >= 0f && position.y >= 0f
                          && position.x <= Screen.width && position.y <= Screen.height;
            bool show = _mouseActive && inside && MenuContext();
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
            if (group.alpha <= 0f)
                return;

            float pixels = size * Screen.height / 1080f;
            bool hover = OverButton(position);
            float target = mouse.leftButton.isPressed ? pressScale : hover ? hoverScale : 1f;
            _scale = Mathf.MoveTowards(_scale, target, Time.unscaledDeltaTime * 6f);
            root.position = position;
            root.sizeDelta = new Vector2(pixels, pixels);
            root.localScale = Vector3.one * _scale;
        }

        bool OverButton(Vector2 position)
        {
            if (EventSystem.current == null)
                return false;
            _pointer ??= new PointerEventData(EventSystem.current);
            _pointer.position = position;
            _hits.Clear();
            EventSystem.current.RaycastAll(_pointer, _hits);
            if (_hits.Count == 0)
                return false;
            var selectable = _hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Selectable>();
            return selectable != null && selectable.IsInteractable();
        }

        /// <summary>Есть ли сейчас на экране меню, в котором нужен курсор.</summary>
        static bool MenuContext()
        {
            if (ScreenFade.IsBusy && GameSession.Instance != null && GameSession.Instance.State != SessionState.Title)
                return false;
            var session = GameSession.Instance;
            if (session == null)
                return true;
            if (session.OverlayOpen || session.Menu != LocalMenu.None)
                return true;
            if (session.State is SessionState.Playing or SessionState.Cleared)
                return GameFeel.Paused;
            return true;
        }

        static bool OtherDeviceUsed()
        {
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.leftStick.ReadValue().sqrMagnitude > 0.25f || pad.dpad.ReadValue().sqrMagnitude > 0.1f
                    || pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame
                    || pad.startButton.wasPressedThisFrame)
                    return true;
            }
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame
                                        || keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame
                                        || keyboard.tabKey.wasPressedThisFrame);
        }
    }
}
