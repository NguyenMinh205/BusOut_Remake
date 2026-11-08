using BusOut.Core;

namespace BusOut.Menu
{
    public enum DailyRewardCellState
    {
        Locked,  // chưa tới lượt
        Today,   // nhận được ngay bây giờ
        Claimed, // đã nhận
    }

    /// Tiến độ quà hằng ngày theo CHU KỲ 7 ngày (đọc / ghi trên SecureGameData; không lưu file — Presenter lưu sau khi trao).
    /// Mỗi ngày mới mở thêm một ô, nhận lần lượt 1 → 7. Bỏ lỡ ngày không mất ô đang tới, nhưng hết chu kỳ thì các ô chưa nhận
    /// mất và quay về ô 1 (như gốc: "Refresh in"). Chu kỳ bắt đầu từ ngày người chơi vào lần đầu; quay lại sau khi đã hết chu
    /// kỳ thì tính từ hôm đó, để luôn có đủ 7 ngày.
    /// Ngày tính bằng TimeService.TodayIndex. "Nhận được" = hôm nay LỚN HƠN ngày nhận gần nhất, không phải "khác": giờ máy tua
    /// tới rồi giờ máy chủ kéo về thì phải chờ tới đúng ngày đó, không nhận lại được.
    public sealed class DailyRewardProgress
    {
        private readonly SecureGameData data;
        private readonly int dayCount;

        public DailyRewardProgress(SecureGameData data, int dayCount)
        {
            this.data = data;
            this.dayCount = dayCount;
            // Save hỏng / đổi số ngày: không để chỉ số ra ngoài danh sách.
            if (data.dailyNextIndex < 0 || data.dailyNextIndex > dayCount) data.dailyNextIndex = 0;
        }

        /// Ô sẽ nhận tiếp theo; = số ô khi đã nhận hết chu kỳ này.
        public int NextIndex => data.dailyNextIndex;

        /// Số thứ tự ngày (TimeService.TodayIndex) mà chu kỳ này kết thúc, lúc 0h ngày đó.
        public int CycleEndDay => data.dailyCycleStartDay + dayCount;

        /// Mở chu kỳ mới nếu chưa có hoặc đã hết. Gọi trước khi đọc trạng thái.
        public void EnsureCycle(int today)
        {
            if (data.dailyCycleStartDay > 0 && today < CycleEndDay) return;
            data.dailyCycleStartDay = today;
            data.dailyNextIndex = 0;
        }

        public bool CanClaim(int today) => today > data.dailyLastClaimDay && data.dailyNextIndex < dayCount;

        public DailyRewardCellState StateOf(int cell, int today)
        {
            if (cell < data.dailyNextIndex) return DailyRewardCellState.Claimed;
            return cell == data.dailyNextIndex && CanClaim(today) ? DailyRewardCellState.Today : DailyRewardCellState.Locked;
        }

        public void MarkClaimed(int today)
        {
            data.dailyLastClaimDay = today;
            data.dailyNextIndex++;
        }
    }
}
