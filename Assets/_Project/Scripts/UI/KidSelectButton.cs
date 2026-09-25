using UnityEngine;
using UnityEngine.EventSystems;

namespace Bouncer.UI
{
    /// <summary>Кнопка с именем на экране выбора: стала выбранной (мышь, стрелки, геймпад) — подсветить её ребёнка.</summary>
    public sealed class KidSelectButton : MonoBehaviour, ISelectHandler
    {
        KidSelectScreen _screen;
        int _index;

        public void Bind(KidSelectScreen screen, int index)
        {
            _screen = screen;
            _index = index;
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (_screen != null)
                _screen.Highlight(_index);
        }
    }
}
