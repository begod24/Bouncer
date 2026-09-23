using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Вспышка и подкраска мешей через MaterialPropertyBlock (материалы не дублируются).
    /// Работает с URP Lit/Unlit (_BaseColor).
    /// </summary>
    public sealed class HitFlash : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Пусто — все MeshRenderer в дочерних объектах")]
        [SerializeField] Renderer[] renderers;

        Color[] _baseColors;
        MaterialPropertyBlock _block;
        Color _flashColor;
        float _flashStart;
        float _flashDuration;
        Color _tintColor = Color.white;
        float _tintAmount;
        bool _dirty = true;

        void Awake()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<MeshRenderer>(true);
            _baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i] ? renderers[i].sharedMaterial : null;
                _baseColors[i] = material && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
            }
            _block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            _flashDuration = 0f;
            _dirty = true;
        }

        public void Flash(Color color, float duration)
        {
            _flashColor = color;
            _flashStart = Time.time;
            _flashDuration = Mathf.Max(0.01f, duration);
            _dirty = true;
        }

        /// <summary>Постоянная подкраска (например, «покраснение» от серии попаданий).</summary>
        public void SetTint(Color color, float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Approximately(amount, _tintAmount) && color == _tintColor)
                return;
            _tintColor = color;
            _tintAmount = amount;
            _dirty = true;
        }

        void LateUpdate()
        {
            float flash = 0f;
            if (_flashDuration > 0f)
            {
                flash = 1f - (Time.time - _flashStart) / _flashDuration;
                if (flash <= 0f)
                {
                    flash = 0f;
                    _flashDuration = 0f;
                }
                _dirty = true;
            }
            if (!_dirty)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r)
                    continue;
                Color c = Color.Lerp(_baseColors[i], _tintColor, _tintAmount);
                c = Color.Lerp(c, _flashColor, flash);
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(_block);
            }
            _dirty = _flashDuration > 0f;
        }
    }
}
