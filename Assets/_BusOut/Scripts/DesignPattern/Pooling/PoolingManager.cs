using System.Collections.Generic;
using UnityEngine;

namespace BusOut.DesignPattern
{
    /// Pool object theo prefab. Thay Instantiate / Destroy bằng Spawn / Despawn.
    /// - Mỗi prefab một pool, tạo khi Spawn hoặc Prewarm lần đầu.
    /// - Object trả về pool được tắt và chuyển vào "[Pools]" (giữ qua đổi scene), nên cha cũ bị xoá cũng không kéo theo nó.
    /// - Component cần reset mỗi lần dùng thì implement IPoolable.
    public static class PoolingManager
    {
        private static readonly Dictionary<int, Pool> pools = new Dictionary<int, Pool>();
        private static Transform poolsRoot;

        public static GameObject Spawn(GameObject prefab, Transform parent = null) =>
            Spawn(prefab, Vector3.zero, Quaternion.identity, parent);

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[Pool] Spawn với prefab null. Kiểm tra tham chiếu prefab trong Inspector.");
                return null;
            }
            return GetPool(prefab).Spawn(position, rotation, parent).gameObject;
        }

        public static T Spawn<T>(T prefab, Transform parent = null) where T : Component =>
            Spawn(prefab, Vector3.zero, Quaternion.identity, parent);

        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            if (prefab == null)
            {
                Debug.LogError($"[Pool] Spawn<{typeof(T).Name}> với prefab null. Kiểm tra tham chiếu prefab trong Inspector.");
                return null;
            }
            return GetPool(prefab.gameObject).Spawn(position, rotation, parent).GetComponent<T>();
        }

        /// Trả object về pool. Object không do pool tạo thì Destroy (gọi nhầm vẫn không rò rỉ).
        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;
            if (!instance.TryGetComponent(out PooledObject pooled) || pooled.Owner == null)
            {
                Object.Destroy(instance);
                return;
            }
            if (!pooled.IsSpawned) return;
            pooled.Owner.Despawn(pooled);
        }

        public static void Despawn(Component instance)
        {
            if (instance != null) Despawn(instance.gameObject);
        }

        /// Tạo sẵn <paramref name="count"/> object (tắt) để lúc chơi không phải Instantiate. Gọi khi load level.
        public static void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return;
            GetPool(prefab).Prewarm(count);
        }

        /// Huỷ mọi object đang nằm chờ trong pool (object đang dùng không bị đụng) và xoá các pool.
        public static void ClearAll()
        {
            foreach (Pool pool in pools.Values) pool.DestroyIdle();
            pools.Clear();
        }

        private static Pool GetPool(GameObject prefab)
        {
            int key = prefab.GetInstanceID();
            if (pools.TryGetValue(key, out Pool pool)) return pool;

            if (poolsRoot == null)
            {
                var rootObject = new GameObject("[Pools]");
                Object.DontDestroyOnLoad(rootObject);
                poolsRoot = rootObject.transform;
            }
            var container = new GameObject($"[Pool] {prefab.name}").transform;
            container.SetParent(poolsRoot, false);
            pool = new Pool(prefab, container);
            pools.Add(key, pool);
            return pool;
        }

        // ⚠️ Chạy cả khi tắt Domain Reload: không reset thì Dictionary giữ pool của lần Play trước, đầy object đã bị huỷ.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            pools.Clear();
            poolsRoot = null;
        }
    }

    internal sealed class Pool
    {
        private readonly GameObject prefab;
        private readonly Transform container;
        private readonly Stack<PooledObject> idle = new Stack<PooledObject>();

        public Pool(GameObject prefab, Transform container)
        {
            this.prefab = prefab;
            this.container = container;
        }

        public PooledObject Spawn(Vector3 position, Quaternion rotation, Transform parent)
        {
            PooledObject pooled = null;
            // Object trong pool có thể đã bị huỷ từ bên ngoài (Destroy thẳng, đổi scene): bỏ qua.
            while (idle.Count > 0 && pooled == null) pooled = idle.Pop();

            if (pooled == null)
            {
                pooled = Create(position, rotation, parent);
            }
            else
            {
                Transform t = pooled.transform;
                t.SetParent(parent, false);
                t.SetPositionAndRotation(position, rotation);
                pooled.gameObject.SetActive(true);
            }

            pooled.IsSpawned = true;
            for (int i = 0; i < pooled.Poolables.Length; i++) pooled.Poolables[i].OnSpawned();
            return pooled;
        }

        public void Despawn(PooledObject pooled)
        {
            pooled.IsSpawned = false;
            for (int i = 0; i < pooled.Poolables.Length; i++) pooled.Poolables[i].OnDespawned();
            pooled.gameObject.SetActive(false);
            pooled.transform.SetParent(container, false);
            idle.Push(pooled);
        }

        public void Prewarm(int count)
        {
            for (int i = idle.Count; i < count; i++)
            {
                PooledObject pooled = Create(Vector3.zero, Quaternion.identity, container);
                pooled.gameObject.SetActive(false);
                idle.Push(pooled);
            }
        }

        public void DestroyIdle()
        {
            while (idle.Count > 0)
            {
                PooledObject pooled = idle.Pop();
                if (pooled != null) Object.Destroy(pooled.gameObject);
            }
            if (container != null) Object.Destroy(container.gameObject);
        }

        private PooledObject Create(Vector3 position, Quaternion rotation, Transform parent)
        {
            GameObject instance = Object.Instantiate(prefab, position, rotation, parent);
            instance.name = prefab.name;
            if (!instance.TryGetComponent(out PooledObject pooled)) pooled = instance.AddComponent<PooledObject>();
            pooled.Owner = this;
            pooled.Poolables = instance.GetComponentsInChildren<IPoolable>(true);
            return pooled;
        }
    }
}
