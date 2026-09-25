using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Bouncer.Visuals
{
    /// <summary>
    /// Погода на арене — случайное событие прогулки (что выпало, решает арена): дождь — капли, мокрая тусклая
    /// палитра и лужи (в луже мяч гаснет, бег медленнее); гроза — дождь и молнии, которые на миг освещают всё
    /// (на стройке тень в этот миг твёрдая); туман — видно метров на десять вокруг игрока, дальше всё тонет в сером.
    /// Туман считает шейдер палитры от игрока, а не от камеры (<see cref="Shader.SetGlobalVector"/> _Bouncer_Fog*).
    /// </summary>
    // Раньше ArenaDirector: он включает погоду из своего Awake, а этот Awake сначала всё гасит.
    [DefaultExecutionOrder(-90)]
    public sealed class WeatherController : MonoBehaviour
    {
        static readonly int FogCenterId = Shader.PropertyToID("_Bouncer_FogCenter");
        static readonly int FogParamsId = Shader.PropertyToID("_Bouncer_FogParams");
        static readonly int FogColorId = Shader.PropertyToID("_Bouncer_FogColor");

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
        [Tooltip("Туман вокруг игрока: где начинается и где уже ничего не видно, м")]
        [SerializeField] float fogStart = 6f;
        [SerializeField] float fogEnd = 13f;
        [Tooltip("Клочья тумана у земли")]
        [SerializeField] ParticleSystem mist;

        readonly List<Puddle> _puddles = new();
        float _wet;
        float _fog;
        float _nextStrike = float.PositiveInfinity;
        float _strikeStart = float.NegativeInfinity;
        float _thunderAt = float.PositiveInfinity;

        public WeatherKind Kind { get; private set; }
        bool Rainy => Kind is WeatherKind.Rain or WeatherKind.Storm;

        void Awake()
        {
            // Префаб погоды один на все сцены: время суток — своё в каждой, его ищем сами.
            if (!timeOfDay)
                timeOfDay = FindFirstObjectByType<TimeOfDayController>();
            if (rain)
                rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (mist)
                mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (lightning)
                lightning.enabled = false;
            if (wetVolume)
                wetVolume.weight = 0f;
        }

        void OnDisable()
        {
            Shader.SetGlobalVector(FogParamsId, Vector4.zero);
            if (timeOfDay)
                timeOfDay.SetWeather(0f, 0f, 0f);
        }

        /// <summary>Включить погоду на эту арену. Clear — ясно.</summary>
        public void Begin(WeatherKind kind)
        {
            Kind = kind;
            if (rain)
            {
                if (Rainy)
                    rain.Play(true);
                else
                    rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (mist)
            {
                if (kind == WeatherKind.Fog)
                    mist.Play(true);
                else
                    mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (rainLoop)
            {
                rainLoop.loop = true;
                if (Rainy && !rainLoop.isPlaying)
                    rainLoop.Play();
            }
            if (Rainy)
                SpawnPuddles();
            _nextStrike = kind == WeatherKind.Storm ? Time.time + Random.Range(3f, 6f) : float.PositiveInfinity;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            // Погода набегает за пару секунд, а не включается рывком.
            _wet = Mathf.MoveTowards(_wet, Rainy ? 1f : 0f, dt * 0.5f);
            _fog = Mathf.MoveTowards(_fog, Kind == WeatherKind.Fog ? 1f : 0f, dt * 0.4f);

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

            var player = Targetable.FindNearest(Vector3.zero, Team.Player);
            Vector3 center = player ? player.Position : Vector3.zero;
            Shader.SetGlobalVector(FogCenterId, center);
            Shader.SetGlobalVector(FogParamsId, new Vector4(fogStart, fogEnd, _fog, 0f));
            Shader.SetGlobalColor(FogColorId, timeOfDay ? timeOfDay.FogColor : Color.grey);
            if (rain)
                rain.transform.position = center + Vector3.up * rainHeight;
            if (mist)
                mist.transform.position = new Vector3(center.x, 0f, center.z);
            if (rainLoop)
                rainLoop.volume = rainVolume * _wet * GameSettings.SfxGain * (GameFeel.Paused ? 0.3f : 1f);
        }

        /// <summary>Молния: две вспышки подряд, всё освещено, через миг гром.</summary>
        void Strike()
        {
            _strikeStart = Time.time;
            _nextStrike = Time.time + Random.Range(lightningInterval.x, lightningInterval.y);
            _thunderAt = Time.time + Random.Range(thunderDelay.x, thunderDelay.y);
            LightZone.Flash(0.35f);
        }

        /// <summary>Яркость вспышки молнии через t секунд после удара: вспыхнула, мигнула, вспыхнула и погасла.</summary>
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

        /// <summary>Лужи в случайных местах на проходимом асфальте, подальше от стен.</summary>
        void SpawnPuddles()
        {
            if (puddlePrefab == null || _puddles.Count > 0)
                return;
            int placed = 0;
            for (int attempt = 0; attempt < puddles * 6 && placed < puddles; attempt++)
            {
                var point = new Vector3(Random.Range(-area.x, area.x) * 0.5f, 0f, Random.Range(-area.y, area.y) * 0.5f);
                if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                    continue;
                var size = new Vector2(Random.Range(puddleSize.x, puddleSize.y), Random.Range(puddleSize.x, puddleSize.y));
                if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edge, NavMesh.AllAreas) && edge.distance < Mathf.Max(size.x, size.y) * 0.5f)
                    continue;
                bool overlaps = false;
                foreach (var other in _puddles)
                    if ((other.transform.position - hit.position).sqrMagnitude < 16f)
                        overlaps = true;
                if (overlaps)
                    continue;
                var puddle = Instantiate(puddlePrefab, transform);
                puddle.Place(new Vector3(hit.position.x, 0f, hit.position.z), size, placed);
                _puddles.Add(puddle);
                placed++;
            }
        }
    }
}
