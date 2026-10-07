using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    // Мигалка Трансформера, пока он машина: маячок на крыше то красный, то синий,
    // и такие же отсветы на асфальте по бокам. Квадраты на шейдере Bouncer/Glow, цвет — через MaterialPropertyBlock.
    public sealed class SirenLights : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] TransformerRig rig;
        [SerializeField] Health health;
        [Tooltip("Огонёк на маячке")]
        [SerializeField] Renderer lamp;
        [Tooltip("Отсветы на асфальте: красный слева, синий справа")]
        [SerializeField] Renderer groundRed;
        [SerializeField] Renderer groundBlue;
        [Tooltip("Смен цвета в секунду")]
        [SerializeField, Min(0.1f)] float rate = 2.6f;
        [SerializeField] Color red = new(1f, 0.12f, 0.1f, 1f);
        [SerializeField] Color blue = new(0.15f, 0.35f, 1f, 1f);
        [SerializeField, Min(0f)] float lampBrightness = 1.6f;
        [SerializeField, Min(0f)] float groundBrightness = 0.55f;

        MaterialPropertyBlock _block;
        bool _shown = true;

        void Awake() => _block = new MaterialPropertyBlock();

        void LateUpdate()
        {
            bool on = rig && rig.IsCar && (!health || !health.IsDead);
            if (on != _shown)
            {
                _shown = on;
                foreach (var r in new[] { lamp, groundRed, groundBlue })
                    if (r)
                        r.enabled = on;
            }
            if (!on)
                return;
            // мигает чётко, но с коротким нарастанием — не «мерцание», а вспышки
            float phase = Mathf.Repeat(Time.time * rate, 2f);
            float redPulse = Pulse(phase);
            float bluePulse = Pulse(Mathf.Repeat(phase + 1f, 2f));
            Set(lamp, redPulse >= bluePulse ? red : blue, Mathf.Max(redPulse, bluePulse) * lampBrightness);
            Set(groundRed, red, redPulse * groundBrightness);
            Set(groundBlue, blue, bluePulse * groundBrightness);
        }

        static float Pulse(float phase) => phase >= 1f ? 0f : Mathf.Clamp01(phase / 0.12f) * Mathf.Clamp01((1f - phase) / 0.25f);

        void Set(Renderer target, Color color, float brightness)
        {
            if (!target)
                return;
            target.GetPropertyBlock(_block);
            _block.SetColor(ColorId, new Color(color.r, color.g, color.b, brightness));
            target.SetPropertyBlock(_block);
        }
    }
}
