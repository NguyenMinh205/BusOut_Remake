using System;
using System.Globalization;

namespace BusOut.Core
{
    /// Định dạng thời gian hiện cho người chơi (đếm ngược quà ngày, lượt quảng cáo…).
    public static class TimeFormat
    {
        /// Tự co theo độ dài: "1d 05h" (≥ 1 ngày) · "5:04:09" (≥ 1 giờ) · "04:09". Âm → "00:00".
        /// Invariant: chữ số Latin trên mọi máy.
        public static string Countdown(TimeSpan time)
        {
            if (time < TimeSpan.Zero) time = TimeSpan.Zero;
            if (time.Days > 0)
                return string.Format(CultureInfo.InvariantCulture, "{0}d {1:00}h", time.Days, time.Hours);
            if (time.Hours > 0)
                return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", time.Hours, time.Minutes, time.Seconds);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", time.Minutes, time.Seconds);
        }
    }
}
