using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Метка опасности на асфальте: круг, куда приземлится босс, или полоса, по которой спикирует ворона.
    /// Мигает всё чаще, пока не придёт время удара, потом возвращается в пул. Рисует LineRenderer:
    /// круг — через <see cref="CircleLine"/> (префаб круга), полоса — двумя точками (префаб без неё).
    /// </summary>
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

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            TryGetComponent(out _circle);
        }

        public void OnSpawned()
        {
            _start = Time.time;
            _until = _start + 1f;
        }

        public void OnDespawned() { }

        /// <summary>Круг на земле на столько секунд.</summary>
        public void ShowCircle(Vector3 center, float radius, float seconds)
        {
            transform.SetPositionAndRotation(new Vector3(center.x, height, center.z), Quaternion.identity);
            if (_circle)
                _circle.Radius = radius;
            Begin(seconds);
        }

        /// <summary>Полоса по земле от from до to на столько секунд.</summary>
        public void ShowLine(Vector3 from, Vector3 to, float seconds)
        {
            transform.SetPositionAndRotation(new Vector3(from.x, height, from.z), Quaternion.identity);
            _line.useWorldSpace = true;
            _line.loop = false;
            _line.positionCount = 2;
            _line.SetPosition(0, new Vector3(from.x, height, from.z));
            _line.SetPosition(1, new Vector3(to.x, height, to.z));
            Begin(seconds);
        }

        /// <summary>Убрать раньше времени.</summary>
        public void Hide() => PoolService.Despawn(gameObject);

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
            // Мигает всё чаще, к удару метка ярче и толще.
            float rate = Mathf.Lerp(blinkRate.x, blinkRate.y, t);
            float blink = 0.55f + 0.45f * Mathf.Cos((Time.time - _start) * rate * Mathf.PI * 2f);
            var c = new Color(color.r, color.g, color.b, color.a * Mathf.Lerp(0.45f, 1f, t) * blink);
            _line.startColor = _line.endColor = c;
            _line.widthMultiplier = width * Mathf.Lerp(0.7f, 1.3f, t);
        }
    }
}
