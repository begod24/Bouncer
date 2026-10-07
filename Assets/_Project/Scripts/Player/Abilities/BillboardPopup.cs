using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class BillboardPopup : MonoBehaviour, IPoolable
    {
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Картинки: какая — задаёт тот, кто показывает")]
        [SerializeField] Texture2D[] frames;
        [SerializeField, Min(0.1f)] float life = 0.7f;
        [Tooltip("Насколько всплывает вверх за жизнь, м")]
        [SerializeField] float rise = 0.6f;
        [SerializeField] float size = 0.9f;
        [SerializeField] Color color = Color.white;

        MeshRenderer _renderer;
        MaterialPropertyBlock _block;
        Vector3 _from;
        float _start;
        float _tilt;
        Color _tint;

        void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _block = new MaterialPropertyBlock();
        }

        public void OnSpawned()
        {
            _start = Time.time;
            _tilt = Random.Range(-12f, 12f);
        }

        public void OnDespawned() { }

        public void Play(int frame, Color? tint = null)
        {
            _from = transform.position;
            _start = Time.time;
            _tint = tint ?? color;
            _renderer.GetPropertyBlock(_block);
            if (frames != null && frames.Length > 0)
                _block.SetTexture(BaseMapId, frames[Mathf.Clamp(frame, 0, frames.Length - 1)]);
            _renderer.SetPropertyBlock(_block);
            LateUpdate();
        }

        void LateUpdate()
        {
            float t = (Time.time - _start) / life;
            if (t >= 1f)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            float pop = t < 0.15f ? Mathf.Lerp(0.3f, 1.15f, t / 0.15f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - 0.15f) / 0.15f));
            transform.localScale = Vector3.one * (size * pop);
            transform.position = _from + Vector3.up * (rise * (1f - (1f - t) * (1f - t)));
            var camera = Camera.main;
            if (camera)
                transform.rotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up) * Quaternion.Euler(0f, 0f, _tilt);
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, new Color(_tint.r, _tint.g, _tint.b, _tint.a * Mathf.Clamp01((1f - t) / 0.3f)));
            _renderer.SetPropertyBlock(_block);
        }

        public static void Show(BillboardPopup prefab, Vector3 position, int frame, Color? tint = null)
        {
            if (prefab != null)
                PoolService.Spawn(prefab, position, Quaternion.identity).Play(frame, tint);
        }
    }
}
