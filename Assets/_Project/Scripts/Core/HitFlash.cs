using UnityEngine;

namespace Bouncer.Core
{
    public sealed class HitFlash : MonoBehaviour
    {
        static readonly int BaseColorId = PaletteShader.BaseColor;

        [Tooltip("Пусто — все MeshRenderer в дочерних объектах")]
        [SerializeField] Renderer[] renderers;

        Color[] _baseColors;
        bool[] _palette;
        MaterialPropertyBlock _block;
        Color _flashColor;
        float _flashStart;
        float _flashDuration;
        Color _tintColor = Color.white;
        float _tintAmount;
        float _frost;
        Color _glow = Color.clear;
        bool _dirty = true;

        void Awake()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<MeshRenderer>(true);
            _block = new MaterialPropertyBlock();
            CacheRenderers();
        }

        public void SetRenderers(Renderer[] list)
        {
            renderers = list ?? System.Array.Empty<Renderer>();
            CacheRenderers();
            _dirty = true;
        }

        void CacheRenderers()
        {
            _baseColors = new Color[renderers.Length];
            _palette = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i] ? renderers[i].sharedMaterial : null;
                _baseColors[i] = material && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
                _palette[i] = PaletteShader.Supports(material);
            }
        }

        void OnEnable()
        {
            _flashDuration = 0f;
            _frost = 0f;
            _glow = Color.clear;
            _dirty = true;
        }

        public void Flash(Color color, float duration)
        {
            _flashColor = color;
            _flashStart = Time.time;
            _flashDuration = Mathf.Max(0.01f, duration);
            _dirty = true;
        }

        public void SetTint(Color color, float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Approximately(amount, _tintAmount) && color == _tintColor)
                return;
            _tintColor = color;
            _tintAmount = amount;
            _dirty = true;
        }

        // Ледяная корка на всё время заморозки (0..1, плавно ведёт Targetable)
        public void SetFrost(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Abs(amount - _frost) < 0.004f && (amount > 0f || _frost == 0f))
                return;
            _frost = amount;
            _dirty = true;
        }

        // Пульсирующий цветной контур (элита). strength 0 — выключить
        public void SetGlow(Color color, float strength)
        {
            var glow = PaletteShader.Tint(color, strength);
            if (glow == _glow)
                return;
            _glow = glow;
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
                r.GetPropertyBlock(_block);
                if (_palette[i])
                {
                    _block.SetColor(PaletteShader.TintColor, PaletteShader.Tint(_tintColor, _tintAmount));
                    _block.SetColor(PaletteShader.FlashColor, PaletteShader.Tint(_flashColor, flash));
                    _block.SetFloat(PaletteShader.FrostAmount, _frost);
                    _block.SetColor(PaletteShader.GlowColor, _glow);
                }
                else
                {
                    Color c = Color.Lerp(_baseColors[i], _tintColor, _tintAmount);
                    c = Color.Lerp(c, _flashColor, flash);
                    _block.SetColor(BaseColorId, c);
                }
                r.SetPropertyBlock(_block);
            }
            _dirty = _flashDuration > 0f;
        }
    }
}
