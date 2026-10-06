using UnityEngine;
using UnityEngine.UI;

namespace Bouncer.UI
{
    public static class UiVisibility
    {
        public static bool IsShown(Graphic graphic)
        {
            if (graphic == null || !graphic.isActiveAndEnabled)
                return false;
            for (var t = graphic.transform; t != null; t = t.parent)
            {
                if (!t.TryGetComponent(out CanvasGroup group) || !group.enabled)
                    continue;
                if (group.alpha <= 0f)
                    return false;
                if (group.ignoreParentGroups)
                    break;
            }
            return true;
        }
    }
}
