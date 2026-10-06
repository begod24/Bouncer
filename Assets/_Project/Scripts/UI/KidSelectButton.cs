using UnityEngine;
using UnityEngine.EventSystems;

namespace Bouncer.UI
{
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
