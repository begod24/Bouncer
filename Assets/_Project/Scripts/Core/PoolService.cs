using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>Получает колбэки при выдаче из пула и возврате в него.</summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    /// <summary>
    /// Пулы по префабам. Сам объект пула живёт в сцене и уничтожается вместе с ней,
    /// поэтому перезапуск сцены начинает с чистого листа.
    /// </summary>
    public sealed class PoolService : MonoBehaviour
    {
        static PoolService s_instance;

        readonly Dictionary<GameObject, Stack<GameObject>> _free = new();
        readonly List<IPoolable> _poolables = new();

        static PoolService Instance
        {
            get
            {
                if (s_instance == null)
                    s_instance = new GameObject("[Pools]").AddComponent<PoolService>();
                return s_instance;
            }
        }

        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component =>
            Spawn(prefab.gameObject, position, rotation).GetComponent<T>();

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var pool = Instance;
            GameObject instance = null;
            if (pool._free.TryGetValue(prefab, out var stack))
            {
                while (instance == null && stack.Count > 0)
                    instance = stack.Pop();
            }

            if (instance == null)
            {
                instance = Instantiate(prefab, position, rotation, pool.transform);
                instance.AddComponent<PooledObject>().Prefab = prefab;
            }
            else
            {
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.SetActive(true);
            }

            instance.GetComponent<PooledObject>().IsInPool = false;
            pool.Notify(instance, spawned: true);
            return instance;
        }

        public static void Despawn(GameObject instance)
        {
            if (instance == null)
                return;
            var tag = instance.GetComponent<PooledObject>();
            if (tag == null)
            {
                Destroy(instance);
                return;
            }
            if (tag.IsInPool)
                return;

            var pool = Instance;
            tag.IsInPool = true;
            pool.Notify(instance, spawned: false);
            instance.SetActive(false);
            if (!pool._free.TryGetValue(tag.Prefab, out var stack))
                pool._free[tag.Prefab] = stack = new Stack<GameObject>();
            stack.Push(instance);
        }

        void Notify(GameObject instance, bool spawned)
        {
            instance.GetComponentsInChildren(true, _poolables);
            foreach (var poolable in _poolables)
            {
                if (spawned)
                    poolable.OnSpawned();
                else
                    poolable.OnDespawned();
            }
            _poolables.Clear();
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }
    }
}
