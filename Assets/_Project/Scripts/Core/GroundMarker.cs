using System;
using UnityEngine;

namespace Bouncer.Core
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class GroundMarker : MonoBehaviour, IPoolable
    {
        [SerializeField] Color color = new(1f, 0.32f, 0.22f, 0.9f);
        [SerializeField] float width = 0.2f;
        [Tooltip("Сколько раз в секунду мигает в начале и в конце")]
        [SerializeField] Vector2 blinkRate = new(3f, 12f);
        [SerializeField] float height = 0.06f;
        [Header("Заливка")]
        [Tooltip("Плоский квад с шейдером Bouncer/Telegraph: заполняется к моменту удара")]
        [SerializeField] Renderer fill;
        [Tooltip("Ширина заливки у линии, м")]
        [SerializeField] float fillWidth = 0.9f;

        static readonly int ProgressId = Shader.PropertyToID("_Progress");
        static readonly int ModeId = Shader.PropertyToID("_Mode");
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int LengthId = Shader.PropertyToID("_Length");
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        LineRenderer _line;
        CircleLine _circle;
        float _start;
        float _until;
        MaterialPropertyBlock _block;
        bool _isLine;
        float _size;
        float _length;
        bool _steady;
        float _steadyProgress;
        float _steadyAlpha = 1f;
        Color _defaultColor;

        public bool IsPuppet { get; set; }

        public static event Action<GroundMarker, bool, Vector3, Vector3, float> Shown;
        public static event Action<GroundMarker> Hidden;

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            TryGetComponent(out _circle);
            _block = new MaterialPropertyBlock();
            _defaultColor = color;
        }

        public void OnSpawned()
        {
            IsPuppet = false;
            _steady = false;
            color = _defaultColor;
            _start = Time.time;
            _until = _start + 1f;
        }

        public void OnDespawned() { }

        public void SetColor(Color value) => color = value;

        // Метка без мигания и без срока: держится, пока её двигают каждый кадр и не спрятали (точка падения своего мяча).
        public void ShowSteady(Vector3 center, float radius, float progress, float alpha)
        {
            _steady = true;
            _steadyProgress = Mathf.Clamp01(progress);
            _steadyAlpha = Mathf.Clamp01(alpha);
            PlaceCircle(center, radius);
            Update();
        }

        public void ShowCircle(Vector3 center, float radius, float seconds)
        {
            _steady = false;
            PlaceCircle(center, radius);
            Begin(seconds);
            if (!IsPuppet)
                Shown?.Invoke(this, true, center, new Vector3(radius, 0f, 0f), seconds);
        }

        void PlaceCircle(Vector3 center, float radius)
        {
            transform.SetPositionAndRotation(new Vector3(center.x, height, center.z), Quaternion.identity);
            if (_circle)
                _circle.Radius = radius;
            _isLine = false;
            _size = radius;
            if (fill)
            {
                fill.transform.SetPositionAndRotation(new Vector3(center.x, height - 0.01f, center.z), Quaternion.Euler(90f, 0f, 0f));
                fill.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            }
        }

        public void ShowLine(Vector3 from, Vector3 to, float seconds)
        {
            _steady = false;
            transform.SetPositionAndRotation(new Vector3(from.x, height, from.z), Quaternion.identity);
            _line.useWorldSpace = true;
            _line.loop = false;
            _line.positionCount = 2;
            _line.SetPosition(0, new Vector3(from.x, height, from.z));
            _line.SetPosition(1, new Vector3(to.x, height, to.z));
            Vector3 along = new(to.x - from.x, 0f, to.z - from.z);
            _isLine = true;
            _size = fillWidth;
            _length = Mathf.Max(0.05f, along.magnitude);
            if (fill)
            {
                Vector3 middle = new((from.x + to.x) * 0.5f, height - 0.01f, (from.z + to.z) * 0.5f);
                var look = along.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(along.normalized) : Quaternion.identity;
                fill.transform.SetPositionAndRotation(middle, look * Quaternion.Euler(90f, 0f, 0f));
                fill.transform.localScale = new Vector3(fillWidth, _length, 1f);
            }
            Begin(seconds);
            if (!IsPuppet)
                Shown?.Invoke(this, false, from, to, seconds);
        }

        public void Hide()
        {
            if (!IsPuppet)
                Hidden?.Invoke(this);
            PoolService.Despawn(gameObject);
        }

        void Begin(float seconds)
        {
            _start = Time.time;
            _until = _start + Mathf.Max(0.05f, seconds);
            Update();
        }

        void Update()
        {
            if (_steady)
            {
                var steady = new Color(color.r, color.g, color.b, color.a * _steadyAlpha);
                _line.startColor = _line.endColor = steady;
                _line.widthMultiplier = width;
                SetFill(_steadyProgress);
                return;
            }
            float total = Mathf.Max(0.01f, _until - _start);
            float t = (Time.time - _start) / total;
            if (t >= 1f)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            float rate = Mathf.Lerp(blinkRate.x, blinkRate.y, t);
            float blink = 0.55f + 0.45f * Mathf.Cos((Time.time - _start) * rate * Mathf.PI * 2f);
            var c = new Color(color.r, color.g, color.b, color.a * Mathf.Lerp(0.45f, 1f, t) * blink);
            _line.startColor = _line.endColor = c;
            _line.widthMultiplier = width * Mathf.Lerp(0.7f, 1.3f, t);
            SetFill(t);
        }

        void SetFill(float progress)
        {
            if (fill)
            {
                fill.GetPropertyBlock(_block);
                _block.SetFloat(ProgressId, progress);
                _block.SetFloat(ModeId, _isLine ? 1f : 0f);
                _block.SetFloat(SizeId, _size);
                _block.SetFloat(LengthId, _length);
                _block.SetColor(ColorId, color);
                fill.SetPropertyBlock(_block);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Shown = null;
            Hidden = null;
        }
    }
}
