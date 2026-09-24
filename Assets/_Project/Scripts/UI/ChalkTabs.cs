using System;
using Bouncer.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Bouncer.UI
{
    /// <summary>
    /// Вкладки меню «мелом»: ряд надписей, выбранная — жёлтая и подчёркнута. Сам ряд — пункт меню:
    /// влево/вправо листают вкладки по кругу, мышью — клик по надписи (надписи — кнопки без навигации).
    /// Панели не прячет: о смене вкладки сообщает <see cref="Changed"/>.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.UI.Selectable))]
    public sealed class ChalkTabs : MonoBehaviour, IMoveHandler, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        [SerializeField] UnityEngine.UI.Button[] tabs;
        [SerializeField] TMP_Text[] labels;
        [Tooltip("Подчёркивание под выбранной вкладкой: в том же ряду, что и кнопки вкладок")]
        [SerializeField] RectTransform underline;
        [SerializeField] Color activeColor = new(0.965f, 0.816f, 0.235f);
        [SerializeField] Color idleColor = new(0.957f, 0.949f, 0.925f, 0.55f);
        [Tooltip("Выбранная надпись чуть больше, когда ряд вкладок выбран стрелками или геймпадом")]
        [SerializeField] float focusedScale = 1.1f;

        UnityEngine.UI.Selectable _selectable;
        bool _focused;
        float _scale = 1f;
        int _underlinedIndex = -1;
        string _underlinedText;

        public int Index { get; private set; }

        /// <summary>Игрок выбрал другую вкладку (из кода через <see cref="Select"/> не зовётся).</summary>
        public event Action<int> Changed;

        void Awake()
        {
            _selectable = GetComponent<UnityEngine.UI.Selectable>();
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i].onClick.AddListener(() => Pick(index));
            }
        }

        public void Select(int index)
        {
            Index = Mathf.Clamp(index, 0, tabs.Length - 1);
            Apply();
        }

        /// <summary>Соседняя вкладка по кругу: -1 — левее, 1 — правее.</summary>
        public void Step(int delta) => Pick((Index + delta + tabs.Length) % tabs.Length);

        public void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left)
                Step(-1);
            else if (eventData.moveDir == MoveDirection.Right)
                Step(1);
        }

        public void OnSelect(BaseEventData eventData)
        {
            _focused = true;
            GameEvents.PlaySound(SoundCue.UiMove, Vector3.zero);
        }

        public void OnDeselect(BaseEventData eventData) => _focused = false;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_selectable.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        void Pick(int index)
        {
            if (index == Index || !_selectable.IsInteractable())
                return;
            Select(index);
            GameEvents.PlaySound(SoundCue.UiMove, Vector3.zero);
            Changed?.Invoke(Index);
        }

        void Update()
        {
            _scale = Mathf.MoveTowards(_scale, _focused ? focusedScale : 1f, Time.unscaledDeltaTime * 1.5f);
            Apply();
        }

        void Apply()
        {
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].color = i == Index ? activeColor : idleColor;
                labels[i].rectTransform.localScale = Vector3.one * (i == Index ? _scale : 1f);
            }
            // Подчёркивание — по ширине надписи: она меняется вместе с языком.
            var active = labels[Index];
            if (underline == null || (Index == _underlinedIndex && ReferenceEquals(active.text, _underlinedText)))
                return;
            _underlinedIndex = Index;
            _underlinedText = active.text;
            var tab = (RectTransform)tabs[Index].transform;
            underline.anchoredPosition = new Vector2(tab.anchoredPosition.x, underline.anchoredPosition.y);
            underline.sizeDelta = new Vector2(active.GetPreferredValues(active.text).x + 24f, underline.sizeDelta.y);
        }
    }
}
