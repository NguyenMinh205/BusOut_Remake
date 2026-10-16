namespace BusOut.Gameplay
{
    /// Một hành khách. Màu dùng chung bảng màu với xe (0..7 = A..H).
    public sealed class PassengerModel
    {
        public readonly int Id;
        public readonly int ColorIndex;
        public readonly bool IsVip;

        public PassengerModel(int id, int colorIndex, bool isVip)
        {
            Id = id;
            ColorIndex = colorIndex;
            IsVip = isVip;
        }
    }
}
