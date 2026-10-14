namespace BusOut.Gameplay
{
    public enum CarState { Parked, MovingToSlot, InSlot, Leaving, Gone }

    /// Trạng thái một xe. Chỉ Presenter được đổi trạng thái; View chỉ đọc.
    public sealed class CarModel
    {
        public readonly int Id;
        public readonly VehicleType Type;
        public readonly int ColorIndex;
        public readonly int SeatCount;

        public CarState State { get; private set; } = CarState.Parked;

        /// Ô đỗ đang giữ; -1 khi chưa giữ ô nào.
        public int SlotIndex { get; private set; } = -1;

        public CarModel(int id, VehicleType type, int colorIndex)
        {
            Id = id;
            Type = type;
            ColorIndex = colorIndex;
            SeatCount = SeatsOf(type);
        }

        public static int SeatsOf(VehicleType type)
        {
            switch (type)
            {
                case VehicleType.FourSeat: return 4;
                case VehicleType.SixSeat: return 6;
                case VehicleType.TenSeat: return 10;
                default: return 1;
            }
        }

        public void StartMoveToSlot(int slotIndex)
        {
            SlotIndex = slotIndex;
            State = CarState.MovingToSlot;
        }

        public void ArriveSlot() => State = CarState.InSlot;

        public void StartLeaving() => State = CarState.Leaving;

        public void MarkGone()
        {
            SlotIndex = -1;
            State = CarState.Gone;
        }
    }
}
