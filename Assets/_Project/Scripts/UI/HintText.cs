using Bouncer.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Bouncer.UI
{
    [RequireComponent(typeof(Graphic))]
    public sealed class HintText : MonoBehaviour
    {
        Graphic _graphic;

        void Awake() => _graphic = GetComponent<Graphic>();

        void LateUpdate() => _graphic.enabled = GameSettings.ShowHints;
    }
}
