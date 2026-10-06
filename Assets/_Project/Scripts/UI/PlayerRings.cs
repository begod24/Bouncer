using Bouncer.Core;
using Bouncer.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Bouncer.UI
{
    /// <summary>
    /// Кольца перезарядки у ног игрока — чтобы не смотреть в угол экрана: рывок, ловля, лечение ловлей,
    /// «Крышка от кастрюли», заряд фонарика. Кольцо видно, пока идёт перезарядка, и чуть после — потом гаснет.
    /// Картинка кольца рисуется в коде.
    /// </summary>
    public sealed class PlayerRings : MonoBehaviour
    {
        enum Ring
        {
            Dash,
            Catch,
            Heal,
            Lid,
            Light,
        }

        const int Count = 5;

        [SerializeField] float size = 40f;
        [SerializeField] float gap = 10f;
        [Tooltip("Насколько ниже ног (в пикселях экрана) ряд колец")]
        [SerializeField] float below = 34f;
        [Tooltip("Сколько секунд кольцо ещё видно после перезарядки")]
        [SerializeField] float linger = 0.6f;
        [SerializeField] Color dashColor = new(0.96f, 0.95f, 0.92f);
        [SerializeField] Color catchColor = new(0.49f, 0.83f, 0.36f);
        [SerializeField] Color healColor = new(0.93f, 0.35f, 0.31f);
        [SerializeField] Color lidColor = new(0.45f, 0.72f, 1f);
        [SerializeField] Color lightColor = new(1f, 0.86f, 0.35f);

        static Sprite s_ring;

        readonly Image[] _fills = new Image[Count];
        readonly Image[] _backs = new Image[Count];
        readonly float[] _shownUntil = new float[Count];
        readonly float[] _alpha = new float[Count];
        RectTransform _canvas;
        PlayerController _player;
        PlayerFlashlight _flashlight;

        void Awake()
        {
            _canvas = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
            var sprite = RingSprite();
            Color[] colors = { dashColor, catchColor, healColor, lidColor, lightColor };
            for (int i = 0; i < Count; i++)
            {
                _backs[i] = MakeImage($"Ring_{(Ring)i}_Back", sprite, new Color(0f, 0f, 0f, 0.45f), false);
                _fills[i] = MakeImage($"Ring_{(Ring)i}", sprite, colors[i], true);
                _fills[i].transform.SetParent(_backs[i].transform, false);
            }
        }

        Image MakeImage(string name, Sprite sprite, Color color, bool filled)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (filled)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = (int)Image.Origin360.Top;
                image.fillClockwise = true;
            }
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            return image;
        }

        void LateUpdate()
        {
            if (_player == null)
            {
                _player = Players.Local;
                if (_player == null)
                {
                    Hide();
                    return;
                }
                _player.TryGetComponent(out _flashlight);
            }
            var camera = Camera.main;
            bool active = camera != null && !_player.IsDead && GameSession.IsPlayerActive;
            if (!active)
            {
                Hide();
                return;
            }

            var balls = _player.Balls;
            float now = Time.unscaledTime;
            Set(Ring.Dash, _player.Motor.DashReady01, true, now);
            Set(Ring.Catch, balls.CatchOnCooldown ? 1f - balls.CatchCooldown01 : 1f, true, now);
            Set(Ring.Heal, _player.CatchHeal01, true, now);
            Set(Ring.Lid, _player.LidReady01, _player.HasLid, now);
            bool light = _flashlight != null && _flashlight.Available;
            Set(Ring.Light, light ? _flashlight.Charge01 : 1f, light, now, _flashlight != null && _flashlight.IsOn);

            Vector3 screen = camera.WorldToScreenPoint(_player.transform.position);
            if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out Vector2 local))
            {
                Hide();
                return;
            }
            // Видимые кольца — рядом, по центру под игроком.
            int visible = 0;
            for (int i = 0; i < Count; i++)
                if (_alpha[i] > 0.01f)
                    visible++;
            float x = -(visible - 1) * 0.5f * (size + gap);
            for (int i = 0; i < Count; i++)
            {
                var back = _backs[i];
                bool on = _alpha[i] > 0.01f;
                if (back.gameObject.activeSelf != on)
                    back.gameObject.SetActive(on);
                if (!on)
                    continue;
                back.rectTransform.anchoredPosition = local + new Vector2(x, -below);
                x += size + gap;
            }
        }

        /// <summary>Показать кольцо, пока идёт перезарядка (value < 1), и немного после.</summary>
        void Set(Ring ring, float value, bool exists, float now, bool forceShow = false)
        {
            int i = (int)ring;
            value = Mathf.Clamp01(value);
            if (exists && (value < 0.999f || forceShow))
                _shownUntil[i] = now + linger;
            float target = exists && now < _shownUntil[i] ? 1f : 0f;
            _alpha[i] = Mathf.MoveTowards(_alpha[i], target, Time.unscaledDeltaTime * 5f);
            var fill = _fills[i];
            fill.fillAmount = value;
            var color = fill.color;
            color.a = _alpha[i] * (value >= 0.999f ? 1f : 0.85f);
            fill.color = color;
            var back = _backs[i].color;
            back.a = 0.45f * _alpha[i];
            _backs[i].color = back;
        }

        void Hide()
        {
            for (int i = 0; i < Count; i++)
            {
                _alpha[i] = 0f;
                if (_backs[i] && _backs[i].gameObject.activeSelf)
                    _backs[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Кольцо с мягкими краями, белое — цвет задаёт картинка.</summary>
        static Sprite RingSprite()
        {
            if (s_ring != null)
                return s_ring;
            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            float center = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float r = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float outer = Mathf.Clamp01(31f - r);
                    float inner = Mathf.Clamp01(r - 22f);
                    byte a = (byte)(255f * Mathf.Min(outer, inner));
                    pixels[y * n + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            s_ring = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return s_ring;
        }
    }
}
