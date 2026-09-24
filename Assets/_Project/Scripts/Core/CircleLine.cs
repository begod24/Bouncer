using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Рисует окружность в плоскости XZ через LineRenderer (кольца ловли, метки приземления и спавна).
    /// Может рисовать и дугу — она смотрит вперёд по локальной оси Z (сектор ловли перед игроком).
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class CircleLine : MonoBehaviour
    {
        [SerializeField, Min(3)] int segments = 40;
        [SerializeField, Min(0f)] float radius = 1f;
        [Tooltip("Угол дуги, градусы. 360 — полная окружность")]
        [SerializeField, Range(1f, 360f)] float arc = 360f;

        LineRenderer _line;
        float _builtRadius = -1f;
        float _builtArc = -1f;
        int _builtSegments = -1;

        public LineRenderer Line => _line ? _line : _line = GetComponent<LineRenderer>();

        public float Radius
        {
            get => radius;
            set
            {
                radius = Mathf.Max(0f, value);
                Rebuild();
            }
        }

        public float Arc
        {
            get => arc;
            set
            {
                arc = Mathf.Clamp(value, 1f, 360f);
                Rebuild();
            }
        }

        void OnEnable() => Rebuild();

        void OnValidate() => Rebuild();

        public void Rebuild()
        {
            var line = Line;
            if (line == null || (Mathf.Approximately(_builtRadius, radius) && Mathf.Approximately(_builtArc, arc)
                                 && _builtSegments == segments))
                return;

            bool full = arc >= 360f;
            int count = full ? segments : Mathf.Max(2, Mathf.CeilToInt(segments * arc / 360f) + 1);
            float arcRadians = arc * Mathf.Deg2Rad;
            // Дуга по центру на +Z: угол π/2 в плоскости XZ.
            float start = full ? 0f : Mathf.PI * 0.5f - arcRadians * 0.5f;
            float step = full ? Mathf.PI * 2f / segments : arcRadians / (count - 1);

            line.useWorldSpace = false;
            line.loop = full;
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float angle = start + i * step;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            _builtRadius = radius;
            _builtArc = arc;
            _builtSegments = segments;
        }
    }
}
