using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Отклик модели на попадание вместо стоп-кадра всей игры: модель врага на миг приплющивает,
    /// она дрожит и отшатывается от удара, а мир при этом не останавливается. Слушает <see cref="Health.Damaged"/>
    /// и двигает только корень модели (Visual): физика, агент и аниматоры его положение и размер не трогают.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class HitPunch : MonoBehaviour, IPoolable
    {
        [Tooltip("Корень модели: вздрагивает он, а не объект с физикой")]
        [SerializeField] Transform visual;
        [SerializeField, Min(0.01f)] float duration = 0.2f;
        [Tooltip("Насколько модель приплющивает в момент удара: 0.2 — на 20% ниже")]
        [SerializeField, Range(0f, 0.5f)] float squash = 0.18f;
        [Tooltip("Насколько модель отшатывается от удара, м")]
        [SerializeField, Min(0f)] float recoil = 0.12f;
        [Tooltip("Дрожь, м")]
        [SerializeField, Min(0f)] float jitter = 0.05f;
        [Tooltip("Во сколько раз сильнее отклик на мощный удар (заряженный бросок, «свечка», подкат, взрыв)")]
        [SerializeField, Min(1f)] float strongMultiplier = 1.5f;

        Health _health;
        Vector3 _restPosition;
        Vector3 _restScale = Vector3.one;
        Vector3 _recoilDirection;
        float _start;
        float _strength;
        bool _playing;

        void Awake()
        {
            _health = GetComponent<Health>();
            if (visual)
            {
                _restPosition = visual.localPosition;
                _restScale = visual.localScale;
            }
        }

        void OnEnable() => _health.Damaged += OnDamaged;

        void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            Stop();
        }

        public void OnSpawned() => Stop();

        public void OnDespawned() => Stop();

        void OnDamaged(HitInfo hit)
        {
            if (!visual)
                return;
            Vector3 direction = hit.Direction;
            direction.y = 0f;
            // Направление удара — в осях родителя модели: тело могло развернуться или накрениться.
            _recoilDirection = visual.parent ? visual.parent.InverseTransformDirection(direction) : direction;
            _strength = hit.Has(HitFlags.Charged) ? strongMultiplier : 1f;
            _start = Time.time;
            _playing = true;
        }

        void LateUpdate()
        {
            if (!_playing)
                return;
            float t = (Time.time - _start) / duration;
            if (t >= 1f)
            {
                Stop();
                return;
            }
            float fade = (1f - t) * (1f - t);
            // Приплюснуло и отпружинило: сначала ниже и шире, потом чуть выше обычного.
            float s = squash * _strength * Mathf.Cos(t * Mathf.PI * 3f) * (1f - t);
            visual.localScale = Vector3.Scale(_restScale, new Vector3(1f + s * 0.5f, 1f - s, 1f + s * 0.5f));
            // Отшатнулся от удара и мелко дрожит, всё быстро гаснет.
            float shake = jitter * _strength * fade;
            var wobble = new Vector3(Mathf.Sin(t * 70f) * shake, 0f, Mathf.Cos(t * 55f) * shake);
            visual.localPosition = _restPosition + _recoilDirection * (recoil * _strength * fade) + wobble;
        }

        void Stop()
        {
            _playing = false;
            if (!visual)
                return;
            visual.localPosition = _restPosition;
            visual.localScale = _restScale;
        }
    }
}
