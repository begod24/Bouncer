using Bouncer.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Bouncer.UI
{
    [RequireComponent(typeof(RawImage))]
    public sealed class RewindOverlay : MonoBehaviour
    {
        [Tooltip("Полосы VHS: бегут вверх, пока идёт перемотка")]
        [SerializeField] float scrollSpeed = 3f;
        [SerializeField, Range(0f, 1f)] float maxAlpha = 0.55f;

        RawImage _image;

        void Awake()
        {
            _image = GetComponent<RawImage>();
            _image.raycastTarget = false;
        }

        void LateUpdate()
        {
            float strength = RewindScreen.Strength01;
            bool on = strength > 0.001f;
            if (_image.enabled != on)
                _image.enabled = on;
            if (!on)
                return;
            var c = _image.color;
            c.a = maxAlpha * strength * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 60f));
            _image.color = c;
            var rect = _image.uvRect;
            rect.y = Mathf.Repeat(rect.y + Time.unscaledDeltaTime * scrollSpeed, 1f);
            rect.x = Random.Range(-0.01f, 0.01f) * strength;
            _image.uvRect = rect;
        }
    }
}
