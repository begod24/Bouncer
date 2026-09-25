using Bouncer.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bouncer.Visuals
{
    /// <summary>
    /// Время суток: ведёт ключи (утро → день → …) по ходу забега и применяет их к сцене —
    /// глобальная палитра шейдера Bouncer/PaletteLit, солнце, окружающий свет, туман, фон камеры.
    /// Цветокоррекция — через два Volume: A (текущий ключ, вес 1) и B (следующий, вес t, приоритет выше),
    /// итог = lerp(A, B, t). Работает и в редакторе: ползунок Progress — превью времени суток.
    /// Погода (<see cref="WeatherController"/>) поверх этого приглушает свет в дождь и сгущает туман.
    /// </summary>
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

        /// <summary>Погода: wet — дождь (свет тусклее, туман серее), fog — туман, flash — вспышка молнии. Всё 0..1.</summary>
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

        /// <summary>
        /// Арена прогулки задаёт свои ключи (двор утром, двор ночью) и длину боя, за которую их пройти.
        /// Меняется только в игре, в сцене остаются ключи из инспектора.
        /// </summary>
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
            // Без контроллера шейдер берёт палитру из материала.
            Shader.SetGlobalFloat(PaletteShader.GlobalPaletteActive, 0f);
            Shader.SetGlobalFloat(PaletteShader.GlobalEmissionStrength, 0f);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // В OnValidate нельзя трогать другие компоненты — применяем на следующем тике редактора.
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
            Shader.SetGlobalFloat(PaletteShader.GlobalEmissionStrength, Mathf.Lerp(a.emissionStrength, b.emissionStrength, t));

            // Дождь: солнце и небо тусклее. Туман: всё вдали тонет в сером.
            float dim = 1f - 0.4f * _wet;
            if (sun)
            {
                sun.color = Color.Lerp(a.sunColor, b.sunColor, t);
                sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t) * dim;
                sun.shadowStrength = Mathf.Lerp(a.shadowStrength, b.shadowStrength, t) * (1f - 0.5f * Mathf.Max(_wet, _fog));
                sun.transform.rotation = Quaternion.Slerp(SunRotation(a), SunRotation(b), t);
            }

            // Молния на миг заливает всё холодным светом.
            Color flash = new Color(0.75f, 0.8f, 1f) * (_flash * 1.6f);
            float ambientDim = 1f - 0.25f * _wet;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(a.ambientSky, b.ambientSky, t) * ambientDim + flash;
            RenderSettings.ambientEquatorColor = Color.Lerp(a.ambientEquator, b.ambientEquator, t) * ambientDim + flash;
            RenderSettings.ambientGroundColor = Color.Lerp(a.ambientGround, b.ambientGround, t) * ambientDim + flash * 0.5f;

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
                Color sky = Color.Lerp(a.skyColor, b.skyColor, t) * (1f - 0.3f * _wet);
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

        /// <summary>Цвет тумана сейчас — им же красится туман вокруг игрока.</summary>
        public Color FogColor { get; private set; } = Color.grey;

        static Quaternion SunRotation(TimeOfDayProfile profile) =>
            Quaternion.Euler(profile.sunElevation, profile.sunAzimuth, 0f);
    }
}
