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

        LineRenderer _line;
        CircleLine _circle;
        float _start;
        float _until;

        public bool IsPuppet { get; set; }

        public static event Action<GroundMarker, bool, Vector3, Vector3, float> Shown;
        public static event Action<GroundMarker> Hidden;

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            TryGetComponent(out _circle);
        }

        public void OnSpawned()
        {
            IsPuppet = false;
            _start = Time.time;
            _until = _start + 1f;
        }

        public void OnDespawned() { }

        public void ShowCircle(Vector3 center, float radius, float seconds)
        {
            transform.SetPositionAndRotation(new Vector3(center.x, height, center.z), Quaternion.identity);
            if (_circle)
                _circle.Radius = radius;
            Begin(seconds);
            if (!IsPuppet)
                Shown?.Invoke(this, true, center, new Vector3(radius, 0f, 0f), seconds);
        }

        public void ShowLine(Vector3 from, Vector3 to, float seconds)
        {
            transform.SetPositionAndRotation(new Vector3(from.x, height, from.z), Quaternion.identity);
            _line.useWorldSpace = true;
            _line.loop = false;
            _line.positionCount = 2;
            _line.SetPosition(0, new Vector3(from.x, height, from.z));
            _line.SetPosition(1, new Vector3(to.x, height, to.z));
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
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Shown = null;
            Hidden = null;
        }
    }
}
