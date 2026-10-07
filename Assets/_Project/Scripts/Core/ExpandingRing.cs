using UnityEngine;

namespace Bouncer.Core
{
    [RequireComponent(typeof(CircleLine))]
    public sealed class ExpandingRing : MonoBehaviour, IPoolable
    {
        static readonly int FrontId = Shader.PropertyToID("_Front");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int TintId = Shader.PropertyToID("_Tint");

        [SerializeField] float duration = 0.35f;
        [SerializeField] Color color = new(1f, 0.92f, 0.65f, 0.9f);
        [SerializeField] float startWidth = 0.3f;
        [SerializeField] float endWidth = 0.05f;
        [Tooltip("Кольцо сжимается к центру, а не расходится")]
        [SerializeField] bool inward;

        [Header("Объёмная волна (необязательно)")]
        [Tooltip("Квадрат на шейдере Bouncer/Wave лицом вверх. Если задан — тонкую линию не рисуем")]
        [SerializeField] Renderer wave;
        [Tooltip("Толщина полосы волны, м: в начале и в конце")]
        [SerializeField] Vector2 waveBand = new(0.9f, 0.35f);
        [Tooltip("Сколько ещё держим эффект после волны, с — чтобы частицы долетели")]
        [SerializeField, Min(0f)] float linger;

        public static event System.Action<ExpandingRing, float> Played;

        CircleLine _circle;
        MaterialPropertyBlock _block;
        ParticleSystem[] _systems;
        ParticleSystem.MinMaxCurve[] _speeds;
        float _start;
        float _radius = 2f;

        void Awake()
        {
            _circle = GetComponent<CircleLine>();
            if (wave)
            {
                _block = new MaterialPropertyBlock();
                _circle.Line.enabled = false;
            }
            // частицы летят вместе с фронтом: в префабе скорость задана «на метр радиуса за длительность»
            _systems = GetComponentsInChildren<ParticleSystem>(true);
            _speeds = new ParticleSystem.MinMaxCurve[_systems.Length];
            for (int i = 0; i < _systems.Length; i++)
                _speeds[i] = _systems[i].main.startSpeed;
        }

        public void OnSpawned()
        {
            _start = Time.time;
            _tint = Color.white;
        }

        public void OnDespawned()
        {
            foreach (var system in _systems)
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        Color _tint = Color.white;

        public void Tint(Color tint) => _tint = tint;

        public void Play(float radius)
        {
            _radius = radius;
            _start = Time.time;
            for (int i = 0; i < _systems.Length; i++)
            {
                var system = _systems[i];
                var main = system.main;
                main.startSpeed = Scaled(_speeds[i], radius / Mathf.Max(0.01f, duration));
                system.Clear(true);
                system.Play(true);
            }
            Update();
            Played?.Invoke(this, radius);
        }

        static ParticleSystem.MinMaxCurve Scaled(ParticleSystem.MinMaxCurve curve, float k) =>
            curve.mode == ParticleSystemCurveMode.TwoConstants
                ? new ParticleSystem.MinMaxCurve(curve.constantMin * k, curve.constantMax * k)
                : new ParticleSystem.MinMaxCurve(curve.constant * k);

        void Update()
        {
            float age = Time.time - _start;
            float t = age / Mathf.Max(0.01f, duration);
            if (age >= duration + linger)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            t = Mathf.Min(t, 1f);
            float eased = 1f - (1f - t) * (1f - t);
            _circle.Radius = inward ? Mathf.Lerp(_radius, 0.3f, eased) : Mathf.Lerp(0.3f, _radius, eased);
            var c = new Color(color.r * _tint.r, color.g * _tint.g, color.b * _tint.b, color.a * _tint.a * (1f - t));
            if (wave)
            {
                UpdateWave(c, t);
                return;
            }
            var line = _circle.Line;
            line.startColor = line.endColor = c;
            line.widthMultiplier = Mathf.Lerp(startWidth, endWidth, t);
        }

        void UpdateWave(Color c, float t)
        {
            float band = Mathf.Lerp(waveBand.x, waveBand.y, t);
            float half = _radius + band + 0.3f;
            wave.transform.localScale = new Vector3(half * 2f, half * 2f, 1f);
            wave.GetPropertyBlock(_block);
            _block.SetFloat(FrontId, _circle.Radius / half);
            _block.SetFloat(ThicknessId, band / half);
            _block.SetFloat(FadeId, t >= 1f ? 0f : 1f);
            _block.SetColor(TintId, c);
            wave.SetPropertyBlock(_block);
        }
    }
}
