using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    public sealed class ChalkMark : MonoBehaviour, IPoolable
    {
        const float SurfaceOffset = 0.025f;

        static readonly List<ChalkMark> s_all = new();

        [Tooltip("Наступил ближе этого к центру крестика — ускорение, м")]
        [SerializeField, Min(0.1f)] float radius = 0.7f;
        [SerializeField, Min(0.1f)] float lifetime = 9f;
        [Tooltip("Последние секунды крестик бледнеет")]
        [SerializeField, Min(0.01f)] float fadeTime = 1.5f;
        [Tooltip("Крестик проявляется за столько секунд — мел рисует")]
        [SerializeField, Min(0.01f)] float drawTime = 0.15f;
        [SerializeField, Min(1)] int maxMarks = 30;
        [Tooltip("Облачко меловой пыли, когда крестик появился")]
        [SerializeField] ParticleSystem dust;

        Renderer _renderer;
        MaterialPropertyBlock _block;
        float _age;
        float _size = 1f;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        Color _color = Color.white;

        public static event System.Action<ChalkMark, Vector3> Dropped;

        void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            _block = new MaterialPropertyBlock();
            if (_renderer && _renderer.sharedMaterial && _renderer.sharedMaterial.HasProperty(ColorId))
                _color = _renderer.sharedMaterial.GetColor(ColorId);
        }

        void OnEnable() => s_all.Add(this);

        void OnDisable() => s_all.Remove(this);

        public void OnSpawned()
        {
            _age = 0f;
            _size = Random.Range(0.9f, 1.1f);
            Apply();
            if (dust)
            {
                dust.Clear(true);
                dust.Play(true);
            }
            while (s_all.Count > maxMarks)
                PoolService.Despawn(s_all[0].gameObject);
        }

        public void OnDespawned() { }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= lifetime)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            Apply();
        }

        void Apply()
        {
            float draw = Mathf.Clamp01(_age / drawTime);
            transform.localScale = Vector3.one * (radius * 1.6f * _size * Mathf.Lerp(0.4f, 1f, draw));
            if (_renderer == null)
                return;
            float alpha = Mathf.Clamp01((lifetime - _age) / fadeTime) * draw;
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, new Color(_color.r, _color.g, _color.b, _color.a * alpha));
            _renderer.SetPropertyBlock(_block);
        }

        public static void Drop(ChalkMark prefab, Vector3 above, bool quiet = false)
        {
            if (prefab == null
                || !Physics.Raycast(above + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 8f, Layers.EnvironmentMask,
                    QueryTriggerInteraction.Ignore)
                || hit.normal.y < 0.6f)
                return;
            var rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            PoolService.Spawn(prefab, hit.point + hit.normal * SurfaceOffset, rotation);
            if (quiet)
                return;
            GameEvents.PlaySound(SoundCue.ChalkScribble, hit.point);
            Dropped?.Invoke(prefab, above);
        }

        public static bool At(Vector3 position)
        {
            foreach (var mark in s_all)
            {
                Vector3 delta = position - mark.transform.position;
                if (delta.x * delta.x + delta.z * delta.z <= mark.radius * mark.radius && Mathf.Abs(delta.y) < 1.2f
                    && mark._age < mark.lifetime - mark.fadeTime)
                    return true;
            }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_all.Clear();
            Dropped = null;
        }
    }
}
