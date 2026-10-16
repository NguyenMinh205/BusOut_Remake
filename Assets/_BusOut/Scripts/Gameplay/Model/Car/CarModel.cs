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

        /// Ghế đã có khách nhận (khách đang đi tới hoặc đã ngồi). Giữ ghế lúc khách rời hàng để 2 khách liền nhau
        /// không cùng nhắm vào ghế cuối.
        public int ReservedSeats { get; private set; }

        /// Khách đã tới xe.
        public int BoardedSeats { get; private set; }

        /// Còn ghế chưa ai nhận.
        public bool HasFreeSeat => ReservedSeats < SeatCount;

        /// Mọi ghế đã có khách ngồi: xe rời bến được.
        public bool IsFull => BoardedSeats >= SeatCount;

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

        public void ReserveSeat()
        {
            if (HasFreeSeat) ReservedSeats++;
        }

        public void BoardSeat()
        {
            if (BoardedSeats < ReservedSeats) BoardedSeats++;
        }

        public void StartLeaving() => State = CarState.Leaving;

        public void MarkGone()
        {
            SlotIndex = -1;
            State = CarState.Gone;
        }
    }
}
