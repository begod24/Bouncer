using UnityEngine;

namespace Bouncer.Core
{
    [DisallowMultipleComponent]
    public sealed class SpawnPreference : MonoBehaviour
    {
        [Tooltip("Только в точках, куда не достаёт свет фонарей (LightZone). Если тёмных нет — где получится")]
        [SerializeField] bool darkOnly;

        public bool DarkOnly => darkOnly;
    }
}
