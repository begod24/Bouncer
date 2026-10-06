using UnityEngine;
using UnityEngine.EventSystems;

namespace Bouncer.UI
{
    public sealed class BestiaryEntryButton : MonoBehaviour, ISelectHandler, IMoveHandler
    {
        BestiaryScreen _screen;
        int _index;

        public void Bind(BestiaryScreen screen, int index)
        {
            _screen = screen;
            _index = index;
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (_screen != null)
                _screen.ShowEntry(_index, (RectTransform)transform);
        }

        public void OnMove(AxisEventData eventData)
        {
            if (_screen == null)
                return;
            if (eventData.moveDir == MoveDirection.Left)
                _screen.StepSection(-1);
            else if (eventData.moveDir == MoveDirection.Right)
                _screen.StepSection(1);
        }
    }
}
