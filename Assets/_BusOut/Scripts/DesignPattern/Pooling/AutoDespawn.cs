using UnityEngine;

namespace BusOut.DesignPattern
{
    /// Tự trả object về pool sau <see cref="lifetime"/> giây. Dùng cho object sống tạm: particle, chữ nổi, hiệu ứng va chạm.
    /// Object không do pool tạo (Instantiate thẳng) thì tự Destroy.
    [DisallowMultipleComponent]
    public sealed class AutoDespawn : MonoBehaviour
    {
        [Tooltip("Thời gian sống (giây), tính từ lúc object được bật / Spawn ra.")]
        [SerializeField, Min(0.01f)] private float lifetime = 2f;

        [Tooltip("Đếm theo thời gian thật, không bị Time.timeScale làm chậm (vd. hiệu ứng trên UI lúc pause).")]
        [SerializeField] private bool useUnscaledTime;

        private float remaining;

        /// Đặt lại thời gian sống cho lần dùng này (vd. chữ nổi dài ngắn khác nhau). Gọi ngay sau Spawn.
        public void Restart(float seconds) => remaining = seconds;

        // OnEnable chạy mỗi lần object được Spawn ra từ pool (pool bật lại object), nên đếm lại từ đầu mỗi lần dùng.
        private void OnEnable() => remaining = lifetime;

        // Đếm trong Update thay vì coroutine: WaitForSeconds cấp phát rác mỗi lần Spawn.
        private void Update()
        {
            remaining -= useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (remaining <= 0f) PoolingManager.Despawn(gameObject);
        }
    }
}
