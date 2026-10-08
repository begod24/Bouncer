using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Bouncer.Visuals
{
    [DefaultExecutionOrder(-90)]
    public sealed class WeatherController : MonoBehaviour
    {
        static readonly int FogCenterId = Shader.PropertyToID("_Bouncer_FogCenter");
        static readonly int FogParamsId = Shader.PropertyToID("_Bouncer_FogParams");
        static readonly int FogColorId = Shader.PropertyToID("_Bouncer_FogColor");
        static readonly int FogShapeId = Shader.PropertyToID("_Bouncer_FogShape");
        static readonly int FogWindId = Shader.PropertyToID("_Bouncer_FogWind");
        static readonly int FogLampsId = Shader.PropertyToID("_Bouncer_FogLamps");
        static readonly int FogLampColorsId = Shader.PropertyToID("_Bouncer_FogLampColors");
        static readonly int FogLampCountId = Shader.PropertyToID("_Bouncer_FogLampCount");
        const int FogLampSlots = 6;

        [SerializeField] TimeOfDayController timeOfDay;

        [Header("Дождь")]
        [Tooltip("Капли: система частиц висит над игроком и идёт за ним")]
        [SerializeField] ParticleSystem rain;
        [SerializeField] float rainHeight = 12f;
        [SerializeField] AudioSource rainLoop;
        [SerializeField, Range(0f, 1f)] float rainVolume = 0.35f;
        [Tooltip("Мокрая цветокоррекция: чуть холоднее и тусклее")]
        [SerializeField] Volume wetVolume;
        [SerializeField] Puddle puddlePrefab;
        [SerializeField, Min(0)] int puddles = 9;
        [Tooltip("Размер луж: от и до, м")]
        [SerializeField] Vector2 puddleSize = new(1.8f, 3.6f);
        [Tooltip("Где на арене бывают лужи: прямоугольник по центру сцены, м")]
        [SerializeField] Vector2 area = new(38f, 24f);

        [Header("Гроза")]
        [SerializeField] Light lightning;
        [SerializeField] float lightningIntensity = 2.5f;
        [Tooltip("Пауза между молниями: от и до, с")]
        [SerializeField] Vector2 lightningInterval = new(7f, 15f);
        [Tooltip("Гром после вспышки: от и до, с")]
        [SerializeField] Vector2 thunderDelay = new(0.3f, 1.4f);

        [Header("Туман")]
        [Tooltip("Чистый пузырь вокруг игрока: где туман ещё еле заметен и где набирает полную силу, м")]
        [SerializeField] float fogStart = 7f;
        [SerializeField] float fogEnd = 20f;
        [Tooltip("Потолок густоты: даже в самом густом клубе туман не закрывает землю целиком")]
        [SerializeField, Range(0.2f, 1f)] float fogDensity = 0.62f;
        [Tooltip("Как быстро туман редеет с высотой, 1/м. Больше — тоньше слой у земли, выше видно чище")]
        [SerializeField, Range(0.2f, 3f)] float fogHeightFalloff = 0.8f;
        [Tooltip("Размер клубов — масштаб шума, 1/м. Меньше — клубы крупнее")]
        [SerializeField, Range(0.03f, 0.3f)] float fogNoiseScale = 0.075f;
        [Tooltip("Насколько туман не берёт ребят, врагов и мячи: 1 — они его совсем не видят")]
        [SerializeField, Range(0f, 1f)] float fogActorResist = 0.75f;
        [Tooltip("Куда и как быстро плывут клубы, м/с (малый слой плывёт в другую сторону быстрее)")]
        [SerializeField] Vector2 fogWind = new(0.9f, 0.35f);
        [Tooltip("Ореол в тумане вокруг горящих фонарей: 0 — нет")]
        [SerializeField, Range(0f, 1f)] float fogLampHalo = 0.6f;

        readonly List<Puddle> _puddles = new();
        float _wet;
        float _fog;
        Vector4 _fogWind;
        readonly Vector4[] _lampPositions = new Vector4[FogLampSlots];
        readonly Vector4[] _lampColors = new Vector4[FogLampSlots];
        float _nextStrike = float.PositiveInfinity;
        float _strikeStart = float.NegativeInfinity;
        float _thunderAt = float.PositiveInfinity;

        public WeatherKind Kind { get; private set; }
        bool Rainy => Kind is WeatherKind.Rain or WeatherKind.Storm;

        public static event System.Action Struck;

        void Awake()
        {
            if (!timeOfDay)
                timeOfDay = FindFirstObjectByType<TimeOfDayController>();
            if (rain)
                rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (lightning)
                lightning.enabled = false;
            if (wetVolume)
                wetVolume.weight = 0f;
        }

        void OnDisable()
        {
            Shader.SetGlobalVector(FogParamsId, Vector4.zero);
            Shader.SetGlobalFloat(FogLampCountId, 0f);
            if (timeOfDay)
                timeOfDay.SetWeather(0f, 0f, 0f);
        }

        public void Begin(WeatherKind kind, int seed = 0, bool remote = false)
        {
            Kind = kind;
            Weather.Current = kind;
            if (rain)
            {
                if (Rainy)
                    rain.Play(true);
                else
                    rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (rainLoop)
            {
                rainLoop.loop = true;
                if (Rainy && !rainLoop.isPlaying)
                    rainLoop.Play();
            }
            if (Rainy)
                SpawnPuddles(seed != 0 ? seed : Random.Range(1, int.MaxValue));
            _nextStrike = kind == WeatherKind.Storm && !remote ? Time.time + Random.Range(3f, 6f) : float.PositiveInfinity;
        }

        public void StrikeFromNetwork()
        {
            _strikeStart = Time.time;
            _thunderAt = Time.time + Random.Range(thunderDelay.x, thunderDelay.y);
            LightZone.Flash(0.35f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _wet = Mathf.MoveTowards(_wet, Rainy ? 1f : 0f, dt * 0.5f);
            _fog = Mathf.MoveTowards(_fog, Kind == WeatherKind.Fog ? 1f : 0f, dt * 0.25f);

            if (Time.time >= _nextStrike)
                Strike();
            if (Time.time >= _thunderAt)
            {
                _thunderAt = float.PositiveInfinity;
                GameEvents.PlaySound(SoundCue.Thunder, Vector3.zero);
                GameFeel.Shake(0.35f);
            }
            float flash = Flash01(Time.time - _strikeStart);
            if (lightning)
            {
                lightning.enabled = flash > 0.01f;
                lightning.intensity = lightningIntensity * flash;
            }
            if (timeOfDay)
                timeOfDay.SetWeather(_wet, _fog, flash);
            if (wetVolume)
                wetVolume.weight = _wet;

            var player = Targetable.LocalPlayer ? Targetable.LocalPlayer : Targetable.FindNearest(Vector3.zero, Team.Player);
            Vector3 center = player ? player.Position : Vector3.zero;
            UpdateFog(center, dt);
            if (rain)
                rain.transform.position = center + Vector3.up * rainHeight;
            if (rainLoop)
                rainLoop.volume = rainVolume * _wet * GameSettings.SfxGain * (GameFeel.Paused ? 0.3f : 1f);
        }

        // Туман: ставит глобальные переменные шейдеров (BouncerFogCore.hlsl), клубы плывут по двум слоям, фонари дают ореол
        void UpdateFog(Vector3 center, float dt)
        {
            Shader.SetGlobalVector(FogCenterId, center);
            // чуть дышит: то гуще, то реже, раз в ~20 с
            float breath = 1f + 0.08f * Mathf.Sin(Time.time * 0.31f) + 0.04f * Mathf.Sin(Time.time * 0.83f + 1.7f);
            Shader.SetGlobalVector(FogParamsId, new Vector4(fogStart, fogEnd, _fog * breath, 0f));
            Shader.SetGlobalColor(FogColorId, timeOfDay ? timeOfDay.FogColor : Color.grey);
            Shader.SetGlobalVector(FogShapeId, new Vector4(fogHeightFalloff, fogNoiseScale, fogDensity, fogActorResist));
            if (_fog <= 0.001f)
            {
                Shader.SetGlobalFloat(FogLampCountId, 0f);
                return;
            }

            // большой слой плывёт по ветру, малый — повёрнутый и быстрее, поэтому клубы всё время перестраиваются
            Vector2 slow = fogWind * (fogNoiseScale * dt);
            Vector2 fast = new Vector2(-fogWind.y * 1.2f - fogWind.x * 0.4f, fogWind.x * 1.2f - fogWind.y * 0.4f) * (fogNoiseScale * 1.6f * dt);
            _fogWind += new Vector4(slow.x, slow.y, fast.x, fast.y);
            // шум в шейдере держится в окне float: не даём смещению уйти в миллионы
            _fogWind = new Vector4(Mathf.Repeat(_fogWind.x, 4096f), Mathf.Repeat(_fogWind.y, 4096f), Mathf.Repeat(_fogWind.z, 4096f), Mathf.Repeat(_fogWind.w, 4096f));
            Shader.SetGlobalVector(FogWindId, _fogWind);

            int count = 0;
            if (fogLampHalo > 0f)
            {
                foreach (var lamp in LampLight.All)
                {
                    if (!lamp || lamp.Level < 0.02f)
                        continue;
                    Vector3 head = lamp.HeadPosition;
                    float sqr = (head.x - center.x) * (head.x - center.x) + (head.z - center.z) * (head.z - center.z);
                    // держим FogLampSlots ближайших к игроку: на место дальнего садится ближний
                    int slot = count < FogLampSlots ? count : -1;
                    if (slot < 0)
                    {
                        float worst = 0f;
                        for (int i = 0; i < FogLampSlots; i++)
                        {
                            float other = (_lampPositions[i].x - center.x) * (_lampPositions[i].x - center.x) + (_lampPositions[i].z - center.z) * (_lampPositions[i].z - center.z);
                            if (other > worst)
                            {
                                worst = other;
                                slot = i;
                            }
                        }
                        if (sqr >= worst)
                            continue;
                    }
                    else
                        count++;
                    Color glow = lamp.GlowColor;
                    float level = lamp.Level * fogLampHalo;
                    _lampPositions[slot] = new Vector4(head.x, head.y, head.z, lamp.GlowRadius);
                    _lampColors[slot] = new Vector4(glow.r * level, glow.g * level, glow.b * level, level * 0.5f);
                }
            }
            Shader.SetGlobalVectorArray(FogLampsId, _lampPositions);
            Shader.SetGlobalVectorArray(FogLampColorsId, _lampColors);
            Shader.SetGlobalFloat(FogLampCountId, count);
        }

        void Strike()
        {
            _strikeStart = Time.time;
            _nextStrike = Time.time + Random.Range(lightningInterval.x, lightningInterval.y);
            _thunderAt = Time.time + Random.Range(thunderDelay.x, thunderDelay.y);
            LightZone.Flash(0.35f);
            Struck?.Invoke();
        }

        static float Flash01(float t)
        {
            if (t < 0f || t > 0.6f)
                return 0f;
            if (t < 0.06f)
                return 1f;
            if (t < 0.12f)
                return 0.2f;
            if (t < 0.22f)
                return 0.9f;
            return Mathf.Lerp(0.9f, 0f, (t - 0.22f) / 0.38f);
        }

        void SpawnPuddles(int seed)
        {
            if (puddlePrefab == null || _puddles.Count > 0)
                return;
            var random = new System.Random(seed);
            float Range(float from, float to) => Mathf.Lerp(from, to, (float)random.NextDouble());
            int placed = 0;
            for (int attempt = 0; attempt < puddles * 6 && placed < puddles; attempt++)
            {
                var point = new Vector3(Range(-area.x, area.x) * 0.5f, 0f, Range(-area.y, area.y) * 0.5f);
                var size = new Vector2(Range(puddleSize.x, puddleSize.y), Range(puddleSize.x, puddleSize.y));
                float yaw = Range(0f, 360f);
                if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                    continue;
                if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edge, NavMesh.AllAreas) && edge.distance < Mathf.Max(size.x, size.y) * 0.5f)
                    continue;
                bool overlaps = false;
                foreach (var other in _puddles)
                    if ((other.transform.position - hit.position).sqrMagnitude < 16f)
                        overlaps = true;
                if (overlaps)
                    continue;
                var puddle = Instantiate(puddlePrefab, transform);
                puddle.Place(new Vector3(hit.position.x, 0f, hit.position.z), size, placed, yaw);
                _puddles.Add(puddle);
                placed++;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Struck = null;
    }
}
