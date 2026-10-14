namespace BusOut.DesignPattern
{
    /// Component trên object được pool cần dọn / dựng lại mỗi lần tái sử dụng (reset máu, tắt trail, kill tween...).
    /// Pool tự tìm mọi IPoolable trên object và các con (kể cả con đang tắt) một lần lúc tạo, rồi gọi ở mỗi lần dùng.
    public interface IPoolable
    {
        /// Gọi sau khi object đã được đặt vị trí, gắn cha và bật lên (sau OnEnable).
        void OnSpawned();

        /// Gọi TRƯỚC khi object bị tắt và trả về pool: dừng mọi thứ đang chạy (tween, coroutine, âm thanh).
        void OnDespawned();
    }
}
