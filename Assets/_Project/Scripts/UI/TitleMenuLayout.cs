using Bouncer.Core;
using Bouncer.Run;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Bouncer.UI
{
    /// <summary>
    /// Кнопки заставки. Без сохранения: «Играть» крупной кнопкой. Есть сохранение: крупное «Продолжить», под ним
    /// «Новая прогулка», остальные строки сдвигаются вниз.
    /// </summary>
    public sealed class TitleMenuLayout : MonoBehaviour
    {
        [SerializeField] Button continueButton;
        [SerializeField] TMP_Text continueLabel;
        [SerializeField] Button playButton;
        [SerializeField] Button newWalkButton;
        [Tooltip("Тетрадка, Настройки, Выход: их положение без сохранения запоминается само")]
        [SerializeField] RectTransform[] lowerRows;
        [Tooltip("Куда они уходят, когда есть сохранение (y)")]
        [SerializeField] float[] lowerRowsWithSave;
        [SerializeField] float newWalkY = -160f;

        float[] _plainY;
        int _state = -1;

        public Selectable Primary => _state == 1 ? continueButton : playButton;

        void Awake()
        {
            _plainY = new float[lowerRows.Length];
            for (int i = 0; i < lowerRows.Length; i++)
                _plainY[i] = lowerRows[i].anchoredPosition.y;
        }

        void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            _state = -1;
            Refresh();
        }

        void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

        void OnLocaleChanged(UnityEngine.Localization.Locale locale) => _state = -1;

        void Update() => Refresh();

        void Refresh()
        {
            var data = RunSave.Peek();
            int state = data != null ? 1 : 0;
            if (state == _state)
                return;
            _state = state;
            continueButton.gameObject.SetActive(state == 1);
            newWalkButton.gameObject.SetActive(state == 1);
            playButton.gameObject.SetActive(state == 0);
            for (int i = 0; i < lowerRows.Length; i++)
            {
                var rect = lowerRows[i];
                float y = state == 1 && i < lowerRowsWithSave.Length ? lowerRowsWithSave[i] : _plainY[i];
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
            }
            var newWalk = (RectTransform)newWalkButton.transform;
            newWalk.anchoredPosition = new Vector2(newWalk.anchoredPosition.x, newWalkY);
            if (data != null && continueLabel != null)
                continueLabel.text = Loc.Format("title.continue", data.arenaIndex + 1);
        }
    }
}
