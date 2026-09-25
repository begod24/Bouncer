using Bouncer.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Bouncer.UI
{
    /// <summary>
    /// Строка с управлением внизу экрана (меню, пауза, ларёк, карточки): видна, только пока в настройках включены
    /// подсказки (<see cref="GameSettings.ShowHints"/>). Прячется сама надпись, объект остаётся — перевод идёт как обычно.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class HintText : MonoBehaviour
    {
        Graphic _graphic;

        void Awake() => _graphic = GetComponent<Graphic>();

        void LateUpdate() => _graphic.enabled = GameSettings.ShowHints;
    }
}
