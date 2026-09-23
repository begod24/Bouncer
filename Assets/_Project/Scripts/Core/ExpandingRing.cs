using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>Кольцо на земле, которое расходится и гаснет (волна от набивного мяча), потом возвращается в пул.</summary>
    [RequireComponent(typeof(CircleLine))]
    public sealed class ExpandingRing : MonoBehaviour, IPoolable
    {
        [SerializeField] float duration = 0.35f;
        [SerializeField] Color color = new(1f, 0.92f, 0.65f, 0.9f);
        [SerializeField] float startWidth = 0.3f;
        [SerializeField] float endWidth = 0.05f;

        CircleLine _circle;
        float _start;
        float _radius = 2f;

        void Awake() => _circle = GetComponent<CircleLine>();

        public void OnSpawned() => _start = Time.time;

        public void OnDespawned() { }

        public void Play(float radius)
        {
            _radius = radius;
            _start = Time.time;
            Update();
        }

        void Update()
        {
            float t = (Time.time - _start) / Mathf.Max(0.01f, duration);
            if (t >= 1f)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            float eased = 1f - (1f - t) * (1f - t);
            _circle.Radius = Mathf.Lerp(0.3f, _radius, eased);
            var line = _circle.Line;
            var c = new Color(color.r, color.g, color.b, color.a * (1f - t));
            line.startColor = line.endColor = c;
            line.widthMultiplier = Mathf.Lerp(startWidth, endWidth, t);
        }
    }
}
