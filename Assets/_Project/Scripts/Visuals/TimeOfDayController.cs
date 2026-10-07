using Bouncer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bouncer.Visuals
{
    [ExecuteAlways]
    [DefaultExecutionOrder(-50)]
    public sealed class TimeOfDayController : MonoBehaviour
    {
        [Tooltip("Ключи по порядку, равномерно распределены по забегу")]
        [SerializeField] TimeOfDayProfile[] keys;
        [Tooltip("Положение между ключами: 0 — первый, 1 — последний. В игре — стартовое значение")]
        [SerializeField, Range(0f, 1f)] float progress;
        [Tooltip("За сколько секунд забега дойти до последнего ключа. 0 — время суток не меняется")]
        [SerializeField, Min(0f)] float runDuration = 420f;

        [Header("Сцена")]
        [SerializeField] Light sun;
        [SerializeField] Camera targetCamera;
        [SerializeField] Volume volumeA;
        [SerializeField] Volume volumeB;

        float _startProgress;
        float _wet;
        float _fog;
        float _flash;

        public void SetWeather(float wet, float fog, float flash)
        {
            _wet = Mathf.Clamp01(wet);
            _fog = Mathf.Clamp01(fog);
            _flash = Mathf.Clamp01(flash);
        }

        public float Progress
        {
            get => progress;
            set
            {
                progress = Mathf.Clamp01(value);
                Apply();
            }
        }

        void OnEnable()
        {
            _startProgress = progress;
            Apply();
        }

        public void Configure(TimeOfDayProfile[] arenaKeys, float duration)
        {
            if (arenaKeys != null && arenaKeys.Length > 0)
                keys = arenaKeys;
            runDuration = Mathf.Max(0f, duration);
            progress = 0f;
            _startProgress = 0f;
            Apply();
        }

        void OnDisable()
        {
            Shader.SetGlobalFloat(PaletteShader.GlobalPaletteActive, 0f);
            Shader.SetGlobalFloat(PaletteShader.GlobalEmissionStrength, 0f);
            FxGlobals.ResetDefaults();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this && isActiveAndEnabled)
                    Apply();
            };
        }
#endif

        void Update()
        {
            if (Application.isPlaying && runDuration > 0f)
            {
                var session = GameSession.Instance;
                if (session != null)
                {
                    float run01 = Mathf.Clamp01(session.SurvivalTime / runDuration);
                    progress = Mathf.Lerp(_startProgress, 1f, run01);
                }
            }
            Apply();
        }

        public void Apply()
        {
            if (keys == null || keys.Length == 0)
                return;

            int last = keys.Length - 1;
            float scaled = progress * last;
            int index = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, Mathf.Max(0, last - 1));
            var a = keys[index];
            var b = keys[Mathf.Min(index + 1, last)];
            if (a == null || b == null)
                return;
            float t = last == 0 ? 0f : Mathf.Clamp01(scaled - index);

            bool palettes = a.palette != null && b.palette != null;
            Shader.SetGlobalFloat(PaletteShader.GlobalPaletteActive, palettes ? 1f : 0f);
            if (palettes)
            {
                Shader.SetGlobalTexture(PaletteShader.GlobalPaletteA, a.palette);
                Shader.SetGlobalTexture(PaletteShader.GlobalPaletteB, b.palette);
                Shader.SetGlobalFloat(PaletteShader.GlobalPaletteBlend, t);
            }
            float dark = Application.isPlaying ? LightsOut.Dark01 : 0f;
            Shader.SetGlobalFloat(PaletteShader.GlobalEmissionStrength,
                Mathf.Lerp(a.emissionStrength, b.emissionStrength, t) * (1f - dark));

            float dim = (1f - 0.4f * _wet) * (1f - 0.85f * dark);
            if (sun)
            {
                sun.color = Color.Lerp(a.sunColor, b.sunColor, t);
                sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t) * dim;
                sun.shadowStrength = Mathf.Lerp(a.shadowStrength, b.shadowStrength, t) * (1f - 0.5f * Mathf.Max(_wet, _fog));
                sun.transform.rotation = Quaternion.Slerp(SunRotation(a), SunRotation(b), t);
            }

            Color flash = new Color(0.75f, 0.8f, 1f) * (_flash * 1.6f);
            float ambientDim = (1f - 0.25f * _wet) * (1f - 0.7f * dark);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(a.ambientSky, b.ambientSky, t) * ambientDim + flash;
            RenderSettings.ambientEquatorColor = Color.Lerp(a.ambientEquator, b.ambientEquator, t) * ambientDim + flash;
            RenderSettings.ambientGroundColor = Color.Lerp(a.ambientGround, b.ambientGround, t) * ambientDim + flash * 0.5f;

            ApplyFx(RenderSettings.ambientSkyColor, sun ? sun.color * sun.intensity : Color.white);

            Color fogColor = Color.Lerp(a.fogColor, b.fogColor, t);
            float grey = fogColor.grayscale;
            fogColor = Color.Lerp(fogColor, new Color(grey, grey, grey * 1.05f), 0.5f * Mathf.Max(_wet, _fog));
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = Mathf.Lerp(Mathf.Lerp(a.fogStart, b.fogStart, t), 22f, _fog);
            RenderSettings.fogEndDistance = Mathf.Lerp(Mathf.Lerp(a.fogEnd, b.fogEnd, t), 45f, _fog);
            FogColor = fogColor;

            if (targetCamera)
            {
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                Color sky = Color.Lerp(a.skyColor, b.skyColor, t) * ((1f - 0.3f * _wet) * (1f - 0.6f * dark));
                targetCamera.backgroundColor = Color.Lerp(sky, fogColor, _fog) + flash * 0.5f;
            }

            if (volumeA)
            {
                if (volumeA.sharedProfile != a.volumeProfile)
                    volumeA.sharedProfile = a.volumeProfile;
                volumeA.weight = 1f;
            }
            if (volumeB)
            {
                if (volumeB.sharedProfile != b.volumeProfile)
                    volumeB.sharedProfile = b.volumeProfile;
                volumeB.weight = a == b ? 0f : t;
            }
        }

        public Color FogColor { get; private set; } = Color.grey;

        // Частицы темнеют и синеют вместе со сценой; контровой свет в темноте сильнее и холоднее — чтобы не терять ребят и мячи
        void ApplyFx(Color ambientSky, Color sunLight)
        {
            Color light = ambientSky + sunLight * 0.45f;
            float peak = Mathf.Max(light.r, Mathf.Max(light.g, light.b));
            Color tint = peak > 1f ? light / peak : light;
            tint = Color.Lerp(tint, Color.white, 0.3f);
            tint.a = 1f;
            Shader.SetGlobalColor(FxGlobals.FxTint, tint);
            float bright = Mathf.Clamp01(peak);
            Shader.SetGlobalFloat(FxGlobals.RimStrength, Mathf.Lerp(0.45f, FxGlobals.DefaultRimStrength, bright));
            Shader.SetGlobalColor(FxGlobals.RimColor, Color.Lerp(new Color(0.7f, 0.82f, 1f), FxGlobals.DefaultRimColor, bright));
            Shader.SetGlobalFloat(FxGlobals.Wet, _wet);
        }

        static Quaternion SunRotation(TimeOfDayProfile profile) =>
            Quaternion.Euler(profile.sunElevation, profile.sunAzimuth, 0f);
    }
}
