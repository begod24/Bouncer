using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    public sealed class TargetMarkView : MonoBehaviour, IPoolable
    {
        [Tooltip("Пунктирный меловой круг под целью")]
        [SerializeField] CircleLine circle;
        [Tooltip("Дуги радиоволн над головой цели")]
        [SerializeField] Transform waves;
        [SerializeField] float radius = 0.9f;

        Targetable _target;
        float _until;
        float _start;

        public void OnSpawned() => _start = Time.time;

        public void OnDespawned() => _target = null;

        public void Play(Targetable target, float seconds)
        {
            _target = target;
            _start = Time.time;
            _until = Time.time + seconds;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (_target == null || !_target.IsAlive || Time.time >= _until
                || (TargetMark.Target != _target && Time.time - _start > 0.1f))
            {
                PoolService.Despawn(gameObject);
                return;
            }
            float age = Time.time - _start;
            Vector3 feet = _target.Position;
            transform.position = new Vector3(feet.x, feet.y + 0.06f, feet.z);
            if (circle)
            {
                circle.Radius = radius * (1f + 0.08f * Mathf.Sin(age * 6f)) * Mathf.Clamp01(age / 0.15f);
                circle.transform.localRotation = Quaternion.Euler(0f, age * 60f, 0f);
                float fade = Mathf.Clamp01((_until - Time.time) / 0.4f);
                var line = circle.Line;
                line.startColor = line.endColor = new Color(1f, 0.82f, 0.25f, 0.9f * fade);
            }
            if (waves)
            {
                waves.position = _target.AimPoint + Vector3.up * 1.1f;
                var camera = Camera.main;
                if (camera)
                    waves.rotation = Quaternion.LookRotation(camera.transform.forward);
                waves.localScale = Vector3.one * (0.8f + 0.25f * Mathf.Repeat(age * 2f, 1f));
            }
        }
    }
}
