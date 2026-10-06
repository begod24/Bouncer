using UnityEngine;

namespace Bouncer.Core
{
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        public GameObject Prefab { get; internal set; }
        internal bool IsInPool;

        public static bool IsSpawnedFrom(GameObject instance, GameObject prefab) =>
            instance != null && instance.TryGetComponent(out PooledObject tag) && !tag.IsInPool && tag.Prefab == prefab;
    }
}
