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

        /// Cơ chế xe ẩn màu: người chơi chưa thấy màu tới khi hết xe chắn đường ra. Màu thật (ColorIndex) vẫn dùng cho mọi
        /// luật, chỉ phần hiển thị bị che.
        public bool IsColorHidden { get; private set; }

        /// Cơ chế khoá và chìa: xe có LockId > 0 chỉ chạy được khi xe mang chìa cùng số (KeyId) đã rời bãi. 0 = không có.
        public readonly int LockId;
        public readonly int KeyId;

        /// Cơ chế bãi chờ: xe thuộc bãi chờ số GarageId (> 0), ra theo GarageOrder tăng dần. 0 = đỗ tự do.
        public readonly int GarageId;
        public readonly int GarageOrder;

        public CarModel(int id, VehicleType type, int colorIndex, bool colorHidden = false, int lockId = 0, int keyId = 0,
            int garageId = 0, int garageOrder = 0)
        {
            GarageId = garageId;
            GarageOrder = garageOrder;
            Id = id;
            Type = type;
            ColorIndex = colorIndex;
            SeatCount = SeatsOf(type);
            IsColorHidden = colorHidden;
            LockId = lockId;
            KeyId = keyId;
        }

        public void RevealColor() => IsColorHidden = false;

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

        /// Booster Undo: xe chưa có khách nào nhận ghế quay về chỗ cũ trong bãi.
        public void ReturnToParking()
        {
            SlotIndex = -1;
            State = CarState.Parked;
        }

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
