using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>Рисует окружность в плоскости XZ через LineRenderer (кольца ловли, метки приземления и спавна).</summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class CircleLine : MonoBehaviour
    {
        [SerializeField, Min(3)] int segments = 40;
        [SerializeField, Min(0f)] float radius = 1f;

        LineRenderer _line;
        float _builtRadius = -1f;
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

        void OnEnable() => Rebuild();

        void OnValidate() => Rebuild();

        public void Rebuild()
        {
            var line = Line;
            if (line == null || (Mathf.Approximately(_builtRadius, radius) && _builtSegments == segments))
                return;

            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            _builtRadius = radius;
            _builtSegments = segments;
        }
    }
}
