namespace BusOut.Menu
{
    /// Bản đồ level ở Menu là một trống quay 8 ô (như gốc): ô i xoay 45°·i quanh trục X, mỗi ô một level.
    /// 8 ô dùng xoay vòng: level L luôn nằm ở ô (L − 1) % 8; quanh level đang xem chỉ cần 8 level liên tiếp
    /// [giữa − 3, giữa + 4] — ô nào lăn ra sau lưng thì nhận level mới phía trước.
    /// Chủ đề đổi mỗi levelsPerTheme level: level đầu khối dùng mảnh cổng (Start), các level sau dùng mảnh 1..4.
    public static class MenuMapLayout
    {
        /// ⚠️ Hằng số hình học, không phải số cân bằng: decor của mọi prefab module dựng cho cung 45° (8 ô kín một vòng).
        /// Đổi là đường của hai module kề nhau hở / chồng lên nhau.
        public const int SlotCount = 8;
        public const float DegreesPerLevel = 360f / SlotCount;

        /// Số level phía sau level đang xem vẫn giữ module (còn lại SlotCount − 1 − WindowBehind level phía trước).
        public const int WindowBehind = 3;

        public static int SlotOf(int level) => Mod(level - 1, SlotCount);

        public static int FirstLevelInWindow(int centerLevel) => centerLevel - WindowBehind;

        /// Chủ đề của level (≥ 1); hết danh sách thì quay lại chủ đề đầu.
        public static int ThemeOf(int level, int levelsPerTheme, int themeCount) => (level - 1) / levelsPerTheme % themeCount;

        /// Mảnh module trong chủ đề: 0 = mảnh cổng (Start), 1.. = mảnh đường thường.
        public static int PieceOf(int level, int levelsPerTheme) => (level - 1) % levelsPerTheme;

        private static int Mod(int value, int divisor) => (value % divisor + divisor) % divisor;
    }
}
