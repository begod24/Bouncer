using Bouncer.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Bouncer.UI
{
    /// <summary>
    /// Пункт меню «мелом» (кнопка, ползунок, переключатель): надпись, под которой появляется меловое
    /// подчёркивание, когда пункт выбран (стрелками, геймпадом) или под мышью. Мышь выбирает пункт
    /// наведением — так у всех способов ввода одна и та же подсветка.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.UI.Selectable))]
    public sealed class ChalkButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        [SerializeField] UnityEngine.UI.Graphic underline;
        [SerializeField] RectTransform label;
        [SerializeField] float selectedScale = 1.1f;

        UnityEngine.UI.Selectable _selectable;
        bool _selected;
        float _scale = 1f;

        void Awake() => _selectable = GetComponent<UnityEngine.UI.Selectable>();

        void OnEnable()
        {
            _selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            _scale = _selected ? selectedScale : 1f;
            Apply();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            GameEvents.PlaySound(SoundCue.UiMove, Vector3.zero);
        }

        public void OnDeselect(BaseEventData eventData) => _selected = false;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_selectable.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        void Update()
        {
            _scale = Mathf.MoveTowards(_scale, _selected ? selectedScale : 1f, Time.unscaledDeltaTime * 1.5f);
            Apply();
        }

        void Apply()
        {
            if (label)
                label.localScale = Vector3.one * _scale;
            if (underline)
                underline.enabled = _selected;
        }
    }
}
