using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    public sealed class AbilityProp : MonoBehaviour, IPoolable
    {
        [Tooltip("Сколько секунд предмет виден, если не сказано иначе")]
        [SerializeField, Min(0.05f)] float life = 0.8f;
        [Tooltip("Появляется и прячется за столько секунд")]
        [SerializeField, Min(0.01f)] float popTime = 0.12f;
        [Tooltip("Покачивание вверх-вниз, м")]
        [SerializeField] float bob = 0.06f;
        [Tooltip("Вращение вокруг вертикали, градусов в секунду")]
        [SerializeField] float spin;
        [Tooltip("Отдача назад в начале, м (пугач, фотоаппарат)")]
        [SerializeField] float recoil;

        Transform _follow;
        Vector3 _offset;
        Quaternion _rotation;
        Vector3 _scale;
        float _start;
        float _life;
        bool _scaleKnown;

        void Awake()
        {
            _scale = transform.localScale;
            _scaleKnown = true;
        }

        public void OnSpawned()
        {
            if (!_scaleKnown)
                Awake();
            _start = Time.time;
            _life = life;
            transform.localScale = Vector3.zero;
        }

        public void OnDespawned() => _follow = null;

        public void Play(Transform follow, Vector3 localOffset, Quaternion rotation, float seconds = -1f)
        {
            _follow = follow;
            _offset = localOffset;
            _rotation = rotation;
            _start = Time.time;
            _life = seconds > 0f ? seconds : life;
            Place(0f);
        }

        void LateUpdate()
        {
            float age = Time.time - _start;
            if (age >= _life || (_follow == null && age > 0.05f && !ReferenceEquals(_follow, null)))
            {
                PoolService.Despawn(gameObject);
                return;
            }
            Place(age);
        }

        void Place(float age)
        {
            float grow = Mathf.Clamp01(age / popTime);
            float shrink = Mathf.Clamp01((_life - age) / popTime);
            float pop = Mathf.Min(grow, shrink);
            float overshoot = 1f + 0.25f * Mathf.Sin(grow * Mathf.PI);
            transform.localScale = _scale * (pop * (grow < 1f ? overshoot : 1f));
            Vector3 basePosition = _follow ? _follow.position + _follow.rotation * _offset : transform.position;
            Quaternion baseRotation = _follow ? Quaternion.Euler(0f, _follow.eulerAngles.y, 0f) * _rotation : _rotation;
            float kick = recoil * Mathf.Exp(-age * 18f) * Mathf.Clamp01(age * 40f);
            Vector3 back = baseRotation * Vector3.back * kick;
            transform.SetPositionAndRotation(basePosition + back + Vector3.up * (Mathf.Sin(age * 9f) * bob),
                baseRotation * Quaternion.Euler(0f, spin * age, 0f));
        }

        public static AbilityProp Show(AbilityProp prefab, Transform follow, Vector3 localOffset, Quaternion rotation,
            float seconds = -1f)
        {
            if (prefab == null || follow == null)
                return null;
            var prop = PoolService.Spawn(prefab, follow.position, follow.rotation);
            prop.Play(follow, localOffset, rotation, seconds);
            return prop;
        }
    }
}
