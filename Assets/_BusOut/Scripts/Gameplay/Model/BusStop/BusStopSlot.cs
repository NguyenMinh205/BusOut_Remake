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

        /// Cơ chế ô công trường: còn bấy nhiêu khách lên xe nữa thì ô mở. 0 = không phải ô công trường.
        public int ConstructionRemaining { get; private set; }
        public bool IsUnderConstruction => ConstructionRemaining > 0;

        /// Mở, không bị công trường chắn, chưa có xe nào giữ: xe mới giữ được.
        public bool IsFree => !IsLocked && !IsUnderConstruction && Occupancy == SlotOccupancy.Empty;

        public BusStopSlot(int index) => Index = index;

        internal void Reset(bool locked)
        {
            IsLocked = locked;
            Occupancy = SlotOccupancy.Empty;
            CarId = NoCar;
            ConstructionRemaining = 0;
        }

        internal void SetConstruction(int remaining) => ConstructionRemaining = remaining > 0 ? remaining : 0;

        /// Một khách vừa lên xe. True khi ô vừa mở đúng lần này.
        internal bool CountDownConstruction()
        {
            if (ConstructionRemaining <= 0) return false;
            ConstructionRemaining--;
            return ConstructionRemaining == 0;
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
