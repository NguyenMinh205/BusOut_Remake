using System;
using System.Globalization;

namespace BusOut.Core
{
    /// Định dạng thời gian hiện cho người chơi (đếm ngược quà ngày, lượt quảng cáo…).
    public static class TimeFormat
    {
        /// Luôn đủ ngày + giờ: "3d 12:59:59", "0d 05:10:46". Âm → "0d 00:00:00".
        public static string DaysClock(TimeSpan time)
        {
            if (time < TimeSpan.Zero) time = TimeSpan.Zero;
            return string.Format(CultureInfo.InvariantCulture, "{0}d {1:00}:{2:00}:{3:00}", time.Days, time.Hours, time.Minutes, time.Seconds);
        }
    }
}
