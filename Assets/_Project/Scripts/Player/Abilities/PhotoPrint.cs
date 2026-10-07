using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class PhotoPrint : MonoBehaviour, IPoolable
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Сколько секунд снимок летит до асфальта")]
        [SerializeField] float fall = 0.9f;
        [Tooltip("Сколько лежит на асфальте")]
        [SerializeField] float rest = 2.5f;
        [SerializeField] float fadeTime = 0.6f;

        MeshRenderer _renderer;
        MaterialPropertyBlock _block;
        Vector3 _from;
        Vector3 _to;
        float _start;
        float _spin;
        Color _color = Color.white;

        void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _block = new MaterialPropertyBlock();
        }

        public void OnSpawned() => _start = Time.time;

        public void OnDespawned() { }

        public void Play(Vector3 from, Vector3 to)
        {
            _from = from;
            _to = to;
            _start = Time.time;
            _spin = Random.Range(-1f, 1f) > 0f ? 1f : -1f;
            LateUpdate();
        }

        void LateUpdate()
        {
            float age = Time.time - _start;
            if (age >= fall + rest)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            float t = Mathf.Clamp01(age / fall);
            Vector3 position = Vector3.Lerp(_from, _to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.6f)
                               + new Vector3(Mathf.Sin(age * 9f), 0f, Mathf.Cos(age * 7f)) * (0.12f * (1f - t));
            float flutter = (1f - t) * 35f * Mathf.Sin(age * 12f);
            var flat = Quaternion.Euler(90f, _spin * age * 120f * (1f - t) + 30f, 0f);
            var upright = Quaternion.Euler(20f + flutter, _spin * age * 200f, flutter);
            transform.SetPositionAndRotation(position, Quaternion.Slerp(upright, flat, t * t));
            float alpha = Mathf.Clamp01((fall + rest - age) / fadeTime);
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, new Color(_color.r, _color.g, _color.b, alpha));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
