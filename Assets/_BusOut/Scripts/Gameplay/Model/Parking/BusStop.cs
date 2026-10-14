namespace BusOut.Gameplay
{
    /// Các ô đỗ ở bến. Xe giữ ô ngay lúc được chạm (trước khi chạy tới) để hai xe chạm liền nhau không tranh một ô.
    public sealed class BusStop
    {
        private const int Empty = -1;

        private readonly int[] occupantIds;

        public int SlotCount => occupantIds.Length;
        public int OpenCount { get; private set; }

        public BusStop(int slotCount, int openCount)
        {
            occupantIds = new int[slotCount];
            for (int i = 0; i < slotCount; i++) occupantIds[i] = Empty;
            OpenCount = openCount < slotCount ? openCount : slotCount;
        }

        /// Giữ ô trống có số nhỏ nhất. Trả -1 khi hết ô.
        public int TryReserve(int carId)
        {
            for (int i = 0; i < OpenCount; i++)
            {
                if (occupantIds[i] != Empty) continue;
                occupantIds[i] = carId;
                return i;
            }
            return -1;
        }

        public void Release(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < occupantIds.Length) occupantIds[slotIndex] = Empty;
        }
    }
}
