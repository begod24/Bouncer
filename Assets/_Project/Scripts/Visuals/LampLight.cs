using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Visuals
{
    /// <summary>
    /// Свет фонаря, прожектора или огня в бочке. Загорается к вечеру — вслед за свечением окон из времени суток
    /// (при включении помигивает, как лампа), а у огня просто дрожит. Если у фонаря есть <see cref="LightZone"/>
    /// и тень его погасила — мигает и гаснет, потом загорается снова.
    /// </summary>
    public sealed class LampLight : MonoBehaviour
    {
        [Tooltip("Источники света фонаря (конус прожектора, ореол вокруг головы)")]
        [SerializeField] Light[] lights;
        [Tooltip("Освещённый круг на земле: если его погасили, гаснет и свет. Пусто — не гаснет")]
        [SerializeField] LightZone zone;

        [Header("Когда горит")]
        [Tooltip("Горит всегда (стройка), а не только к вечеру")]
        [SerializeField] bool alwaysOn;
        [Tooltip("С какой силы свечения окон (из времени суток) фонарь начинает загораться")]
        [SerializeField] float onFromEmission = 0.15f;
        [Tooltip("С какой — горит в полную силу")]
        [SerializeField] float fullAtEmission = 0.8f;

        [Header("Мерцание")]
        [Tooltip("Постоянная дрожь яркости: 0 — лампа, 0.3 — огонь в бочке")]
        [SerializeField, Range(0f, 0.5f)] float flicker = 0.03f;
        [SerializeField] float flickerSpeed = 9f;
        [Tooltip("Сколько секунд лампа помигивает, когда загорается")]
        [SerializeField] float warmUpTime = 1.2f;

        float[] _intensity;
        float _seed;
        bool _lit;
        float _litAt = float.NegativeInfinity;

        void Awake()
        {
            _seed = Random.value * 100f;
            if (lights == null || lights.Length == 0)
                lights = GetComponentsInChildren<Light>(true);
            _intensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
                _intensity[i] = lights[i] ? lights[i].intensity : 0f;
        }

        void Update()
        {
            float on = alwaysOn ? 1f : Mathf.InverseLerp(onFromEmission, fullAtEmission,
                Shader.GetGlobalFloat(PaletteShader.GlobalEmissionStrength));
            float time = Time.time + _seed;
            // Загорается, как газоразрядная лампа: первые секунды помигивает, дальше просто разгорается.
            if (!_lit && on > 0.02f)
                _litAt = Time.time;
            _lit = on > 0.02f;
            bool warmingUp = Time.time - _litAt < warmUpTime;
            float warm = warmingUp && Mathf.PerlinNoise(time * 11f, 0.3f) < 0.45f ? 0.2f : 1f;
            float noise = 1f + (Mathf.PerlinNoise(time * flickerSpeed, _seed) - 0.5f) * 2f * flicker;
            float level = on * warm * noise;
            if (zone)
            {
                // Тень гасит фонарь: он часто мигает и тухнет, а потом так же загорается.
                float out01 = zone.Out01;
                if (out01 > 0f)
                {
                    bool blink = Mathf.PerlinNoise(time * 18f, 1.7f) > 0.5f;
                    level *= out01 >= 1f ? 0f : blink ? 1f - out01 : 0.1f;
                }
            }
            for (int i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (!light)
                    continue;
                float intensity = _intensity[i] * level;
                light.intensity = intensity;
                bool enabled = intensity > 0.01f;
                if (light.enabled != enabled)
                    light.enabled = enabled;
            }
        }
    }
}
