using UnityEngine;

namespace BusOut.DesignPattern
{
    /// Pool tự gắn lên mỗi object nó tạo: biết object thuộc pool nào (Despawn tra ngay, không duyệt mọi pool)
    /// và giữ sẵn danh sách IPoolable. Không gắn tay.
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class PooledObject : MonoBehaviour
    {
        internal Pool Owner;
        internal IPoolable[] Poolables;

        /// Đang được dùng (đã Spawn, chưa Despawn). Chặn Despawn hai lần đẩy cùng object vào pool hai lần.
        public bool IsSpawned { get; internal set; }
    }
}
