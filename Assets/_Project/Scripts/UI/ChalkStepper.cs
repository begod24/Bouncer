using System;
using System.Collections.Generic;
using Bouncer.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Bouncer.UI
{
    /// <summary>
    /// Переключатель «‹ значение ›» для меню. Влево/вправо (стрелки, крестовина, стик) листают значения до края,
    /// Enter/A — следующее по кругу, клик по левой или правой половине — назад или вперёд по кругу.
    /// Выбор и навигацию вверх-вниз даёт Selectable на том же объекте.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.UI.Selectable))]
    public sealed class ChalkStepper : MonoBehaviour, IMoveHandler, ISubmitHandler, IPointerClickHandler
    {
        [SerializeField] TMP_Text valueLabel;

        readonly List<string> _options = new();
        UnityEngine.UI.Selectable _selectable;

        public int Index { get; private set; }

        /// <summary>Игрок выбрал другое значение (из кода через <see cref="SetOptions"/> не зовётся).</summary>
        public event Action<int> Changed;

        void Awake() => _selectable = GetComponent<UnityEngine.UI.Selectable>();

        public void SetOptions(IEnumerable<string> options, int index)
        {
            _options.Clear();
            _options.AddRange(options);
            Show(index);
        }

        /// <summary>Новые подписи при том же выбранном значении (например, сменился язык).</summary>
        public void SetOptions(IEnumerable<string> options) => SetOptions(options, Index);

        public void SetIndex(int index) => Show(index);

        public void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left)
                Step(-1, false);
            else if (eventData.moveDir == MoveDirection.Right)
                Step(1, false);
        }

        public void OnSubmit(BaseEventData eventData) => Step(1, true);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local);
            Step(local.x < rect.rect.center.x ? -1 : 1, true);
        }

        void Step(int delta, bool wrap)
        {
            if (_options.Count < 2 || !_selectable.IsInteractable())
                return;
            int index = wrap
                ? (Index + delta + _options.Count) % _options.Count
                : Mathf.Clamp(Index + delta, 0, _options.Count - 1);
            if (index == Index)
                return;
            Show(index);
            GameEvents.PlaySound(SoundCue.UiMove, Vector3.zero);
            Changed?.Invoke(Index);
        }

        void Show(int index)
        {
            Index = Mathf.Clamp(index, 0, Mathf.Max(0, _options.Count - 1));
            if (valueLabel)
                valueLabel.text = _options.Count > 0 ? _options[Index] : string.Empty;
        }
    }
}
