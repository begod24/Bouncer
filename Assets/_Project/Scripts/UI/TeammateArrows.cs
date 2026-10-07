using Bouncer.Core;
using Bouncer.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Bouncer.UI
{
    public sealed class TeammateArrows : MonoBehaviour
    {
        [SerializeField] float size = 44f;
        [Tooltip("Отступ стрелки от края экрана (в пикселях канваса)")]
        [SerializeField] float margin = 36f;
        [Tooltip("На какой высоте над ногами целится стрелка, м")]
        [SerializeField] float aimHeight = 0.9f;
        [Tooltip("Насколько напарник может выйти за край экрана и всё ещё считаться видимым (доля экрана)")]
        [SerializeField] float edge = 0.02f;
        [Tooltip("Цвета игроков по номеру — те же, что у имён над головой")]
        [SerializeField] Color[] slotColors =
        {
            new(0.98f, 0.85f, 0.36f),
            new(0.45f, 0.78f, 0.96f),
            new(0.96f, 0.56f, 0.71f),
            new(0.56f, 0.86f, 0.49f),
        };

        static Sprite s_triangle;

        readonly Image[] _arrows = new Image[RunState.MaxPlayers];
        readonly float[] _alpha = new float[RunState.MaxPlayers];
        RectTransform _canvas;

        void Awake()
        {
            _canvas = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
            var sprite = TriangleSprite();
            for (int slot = 0; slot < _arrows.Length; slot++)
            {
                var go = new GameObject($"Arrow_{slot + 1}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var image = go.GetComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = false;
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(size, size);
                go.SetActive(false);
                _arrows[slot] = image;
            }
        }

        void LateUpdate()
        {
            var camera = Camera.main;
            bool active = camera != null && Players.Local != null && GameSession.IsPlayerActive;
            for (int slot = 0; slot < _arrows.Length; slot++)
            {
                var arrow = _arrows[slot];
                var player = active ? Players.InSlot(slot) : null;
                bool show = player != null && !player.IsLocal && player.isActiveAndEnabled && !player.IsHome
                            && Place(camera, player, arrow.rectTransform);
                _alpha[slot] = active ? Mathf.MoveTowards(_alpha[slot], show ? 1f : 0f, Time.unscaledDeltaTime * 6f) : 0f;
                bool on = _alpha[slot] > 0.01f;
                if (arrow.gameObject.activeSelf != on)
                    arrow.gameObject.SetActive(on);
                if (!on)
                    continue;
                var color = slotColors.Length > 0 ? slotColors[slot % slotColors.Length] : Color.white;
                color.a *= _alpha[slot];
                arrow.color = color;
            }
        }

        bool Place(Camera camera, PlayerController player, RectTransform arrow)
        {
            Vector3 viewport = camera.WorldToViewportPoint(player.transform.position + Vector3.up * aimHeight);
            bool behind = viewport.z < 0f;
            if (!behind && viewport.x > -edge && viewport.x < 1f + edge && viewport.y > -edge && viewport.y < 1f + edge)
                return false;

            Vector2 half = _canvas.rect.size * 0.5f;
            var direction = new Vector2((viewport.x - 0.5f) * half.x * 2f, (viewport.y - 0.5f) * half.y * 2f);
            if (behind)
                direction = -direction;
            if (direction.sqrMagnitude < 1e-4f)
                direction = Vector2.down;
            Vector2 limit = new(Mathf.Max(1f, half.x - margin), Mathf.Max(1f, half.y - margin));
            float scale = Mathf.Min(
                Mathf.Abs(direction.x) > 1e-4f ? limit.x / Mathf.Abs(direction.x) : float.PositiveInfinity,
                Mathf.Abs(direction.y) > 1e-4f ? limit.y / Mathf.Abs(direction.y) : float.PositiveInfinity);
            arrow.anchoredPosition = direction * scale;
            arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            return true;
        }

        static Sprite TriangleSprite()
        {
            if (s_triangle != null)
                return s_triangle;
            const int n = 64;
            const float rim = 4f;
            Vector2[] corners = { new(32f, 58f), new(54f, 10f), new(10f, 10f) };
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(x, y);
                    float inside = float.PositiveInfinity;
                    for (int i = 0; i < corners.Length; i++)
                    {
                        Vector2 a = corners[i];
                        Vector2 b = corners[(i + 1) % corners.Length];
                        Vector2 edgeDir = (b - a).normalized;
                        inside = Mathf.Min(inside, (p.x - a.x) * edgeDir.y - (p.y - a.y) * edgeDir.x);
                    }
                    float coverage = Mathf.Clamp01(inside + 0.5f);
                    byte shade = (byte)(255f * Mathf.Clamp01(inside - rim + 0.5f));
                    pixels[y * n + x] = new Color32(shade, shade, shade, (byte)(255f * coverage));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            s_triangle = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return s_triangle;
        }
    }
}
