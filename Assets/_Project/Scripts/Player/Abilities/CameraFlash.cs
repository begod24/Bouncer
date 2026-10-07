using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    public sealed class CameraFlash : MonoBehaviour, IPoolable, ILightBeam
    {
        [SerializeField] Light flash;
        [Tooltip("Вспышка горит столько секунд (и столько тени в ней плотные)")]
        [SerializeField, Min(0.05f)] float life = 0.45f;
        [SerializeField] float intensity = 60f;

        float _start;
        Vector3 _direction = Vector3.forward;
        float _range;
        float _halfAngle;

        public bool BeamOn => Time.time - _start < life;
        public Vector3 BeamOrigin => transform.position;
        public Vector3 BeamDirection => _direction;
        public float BeamRange => _range;
        public float BeamHalfAngle => _halfAngle;

        public void OnSpawned()
        {
            _start = Time.time;
            LightBeams.Register(this);
        }

        public void OnDespawned() => LightBeams.Unregister(this);

        void OnDisable() => LightBeams.Unregister(this);

        public void Play(Vector3 direction, float range, float halfAngle)
        {
            _direction = direction;
            _range = range;
            _halfAngle = halfAngle;
            _start = Time.time;
            LightBeams.Register(this);
            Update();
        }

        void Update()
        {
            float t = (Time.time - _start) / life;
            if (t >= 1f)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            if (flash)
            {
                flash.enabled = true;
                flash.intensity = intensity * (t < 0.1f ? 1f : Mathf.Pow(1f - (t - 0.1f) / 0.9f, 2f));
                flash.range = _range + 2f;
                flash.transform.rotation = Quaternion.LookRotation(_direction + Vector3.down * 0.25f);
            }
        }
    }
}
