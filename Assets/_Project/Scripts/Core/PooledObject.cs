using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>Метка экземпляра: из какого префаба он создан. Добавляется пулом автоматически.</summary>
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        public GameObject Prefab { get; internal set; }
        internal bool IsInPool;

        /// <summary>Экземпляр этого префаба, выданный пулом и ещё не возвращённый.</summary>
        public static bool IsSpawnedFrom(GameObject instance, GameObject prefab) =>
            instance != null && instance.TryGetComponent(out PooledObject tag) && !tag.IsInPool && tag.Prefab == prefab;
    }
}
