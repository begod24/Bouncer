using UnityEngine;

namespace Bouncer.Core
{
    // Пыль из-под колёс и линии скорости, пока враг несётся (таран коня, машинки, Трансформера).
    // Смотрит только на скорость по земле, поэтому у гостя работает сама по себе.
    public sealed class SpeedFx : MonoBehaviour
    {
        [Tooltip("С какой скорости по земле, м/с, включаются эффекты (обычный шаг — медленнее)")]
        [SerializeField, Min(0.1f)] float minSpeed = 6f;
        [Tooltip("Системы частиц, которые сыплют только на большой скорости")]
        [SerializeField] ParticleSystem[] systems;

        // за кадр дальше этого — телепорт (появление из пула, варп), а не бег
        const float TeleportDistance = 2.5f;

        Vector3 _last;
        float _speed;
        bool _on;

        void OnEnable()
        {
            _last = transform.position;
            _speed = 0f;
            Set(false);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 position = transform.position;
            Vector3 delta = position - _last;
            _last = position;
            if (dt <= 0f)
                return;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance > TeleportDistance)
                return;
            _speed = Mathf.Lerp(_speed, distance / dt, 0.5f);
            bool on = _on ? _speed > minSpeed * 0.8f : _speed > minSpeed;
            if (on != _on)
                Set(on);
        }

        void Set(bool on)
        {
            _on = on;
            if (systems == null)
                return;
            foreach (var system in systems)
            {
                if (!system)
                    continue;
                var emission = system.emission;
                emission.enabled = on;
                if (on && !system.isPlaying)
                    system.Play(true);
            }
        }
    }
}
