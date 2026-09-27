using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Bouncer.UI
{
    /// <summary>
    /// Шрифт под язык: в Neucha нет казахских букв (Ә Ғ Қ Ң Ө Ұ Ү Һ), поэтому на казахском все тексты интерфейса,
    /// написанные Neucha, переходят на Caveat, а при смене языка обратно — возвращаются.
    /// </summary>
    public sealed class LocaleFontSwap : MonoBehaviour
    {
        [Tooltip("Основной шрифт интерфейса")]
        [SerializeField] TMP_FontAsset regular;
        [Tooltip("Шрифт для языков, которых основной не умеет")]
        [SerializeField] TMP_FontAsset fallback;
        [Tooltip("Материал основного шрифта с тенью и такой же у второго")]
        [SerializeField] Material regularShadow;
        [SerializeField] Material fallbackShadow;
        [Tooltip("Коды языков, которым нужен второй шрифт")]
        [SerializeField] string[] localeCodes = { "kk" };

        readonly Dictionary<TMP_Text, Material> _swapped = new();
        readonly List<TMP_Text> _texts = new();
        bool _useFallback;

        void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += Apply;
            if (LocalizationSettings.SelectedLocale != null)
                Apply(LocalizationSettings.SelectedLocale);
        }

        void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= Apply;

        // Надписи, созданные позже (карманы, свойства элиток), подхватываются раз в полсекунды.
        float _nextScan;

        void Update()
        {
            if (!_useFallback || Time.unscaledTime < _nextScan)
                return;
            _nextScan = Time.unscaledTime + 0.5f;
            Swap();
        }

        void Apply(Locale locale)
        {
            _useFallback = locale != null && System.Array.IndexOf(localeCodes, locale.Identifier.Code) >= 0;
            if (_useFallback)
            {
                Swap();
                return;
            }
            foreach (var pair in _swapped)
            {
                if (pair.Key == null)
                    continue;
                pair.Key.font = regular;
                if (pair.Value != null)
                    pair.Key.fontSharedMaterial = pair.Value;
            }
            _swapped.Clear();
        }

        void Swap()
        {
            if (regular == null || fallback == null)
                return;
            GetComponentsInChildren(true, _texts);
            foreach (var text in _texts)
            {
                if (text.font != regular)
                    continue;
                var material = text.fontSharedMaterial;
                text.font = fallback;
                if (material != null && material == regularShadow && fallbackShadow != null)
                    text.fontSharedMaterial = fallbackShadow;
                _swapped[text] = material;
            }
            _texts.Clear();
        }
    }
}
