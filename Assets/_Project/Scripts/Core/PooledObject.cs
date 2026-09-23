using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>Метка экземпляра: из какого префаба он создан. Добавляется пулом автоматически.</summary>
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        internal GameObject Prefab;
        internal bool IsInPool;
    }
}
