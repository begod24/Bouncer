using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>Где спавнер может выпустить этого врага: тень выходит только из темноты.</summary>
    [DisallowMultipleComponent]
    public sealed class SpawnPreference : MonoBehaviour
    {
        [Tooltip("Только в точках, куда не достаёт свет фонарей (LightZone). Если тёмных нет — где получится")]
        [SerializeField] bool darkOnly;

        public bool DarkOnly => darkOnly;
    }
}
