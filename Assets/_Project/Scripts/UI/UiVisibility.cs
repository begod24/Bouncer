using UnityEngine;
using UnityEngine.UI;

namespace Bouncer.UI
{
    /// <summary>
    /// Виден ли элемент: экраны меню не выключаются сразу, а гаснут альфой своей CanvasGroup
    /// (<see cref="RunScreens"/>), так что включённый элемент может быть уже невидим — и наоборот, ещё виден
    /// после того, как экран закрыли.
    /// </summary>
    public static class UiVisibility
    {
        /// <summary>Элемент включён и ни одна CanvasGroup над ним не погасла до нуля.</summary>
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
