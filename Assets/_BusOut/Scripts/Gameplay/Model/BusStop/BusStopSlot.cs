namespace BusOut.Gameplay
{
    /// Ô có xe hay không. Khoá/mở là chuyện riêng (IsLocked): ô khoá luôn Empty.
    public enum SlotOccupancy
    {
        Empty,
        Reserved, // xe đã giữ ô và đang chạy tới
        Occupied, // xe đã đỗ trong ô
    }

    /// Trạng thái một ô đỗ ở bến. Chỉ BusStop được đổi.
    public sealed class BusStopSlot
    {
        public const int NoCar = -1;

        public readonly int Index;

        public bool IsLocked { get; private set; }
        public SlotOccupancy Occupancy { get; private set; } = SlotOccupancy.Empty;
        public int CarId { get; private set; } = NoCar;

        /// Mở và chưa có xe nào giữ: xe mới giữ được.
        public bool IsFree => !IsLocked && Occupancy == SlotOccupancy.Empty;

        public BusStopSlot(int index) => Index = index;

        internal void Reset(bool locked)
        {
            IsLocked = locked;
            Occupancy = SlotOccupancy.Empty;
            CarId = NoCar;
        }

        internal void Unlock() => IsLocked = false;

        internal void Reserve(int carId)
        {
            Occupancy = SlotOccupancy.Reserved;
            CarId = carId;
        }

        internal void Occupy() => Occupancy = SlotOccupancy.Occupied;

        internal void Release()
        {
            Occupancy = SlotOccupancy.Empty;
            CarId = NoCar;
        }
    }
}
