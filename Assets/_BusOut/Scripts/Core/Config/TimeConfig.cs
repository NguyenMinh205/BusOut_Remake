using System.Collections.Generic;
using UnityEngine;

namespace BusOut.Core
{
    /// Số chỉnh được của TimeService (giờ thật cho quà ngày, giới hạn quảng cáo / ngày…).
    /// TimeService đọc qua Resources (tự dựng, không có ô kéo) → asset phải nằm đúng Resources/TimeConfig.asset.
    [CreateAssetMenu(menuName = "BusOut/Time Config", fileName = "TimeConfig")]
    public class TimeConfig : ScriptableObject
    {
        public const string ResourcePath = "TimeConfig";

        [Header("Giờ máy chủ")]
        [Tooltip("Lấy giờ từ header \"Date\" của các trang này, thử lần lượt tới khi được. Trang lớn, luôn sống; nên có một " +
                 "trang vào được ở Trung Quốc (Google bị chặn ở đó). Trống = chỉ dùng giờ máy.")]
        public List<string> serverUrls = new List<string>
        {
            "https://www.google.com",
            "https://www.cloudflare.com",
            "https://www.microsoft.com",
            "https://www.amazon.com",
            "https://y.qq.com",
        };

        [Tooltip("Mỗi trang chờ tối đa bấy nhiêu giây rồi chuyển trang kế.")]
        [Min(1)] public int requestTimeoutSeconds = 3;

        [Tooltip("Màn Loading chờ giờ máy chủ tối đa bấy nhiêu giây (tính từ lúc mở game; màn Loading vốn đã hiện ≥ 1s). " +
                 "Quá hạn thì vào game bằng giờ máy, giờ máy chủ về sau sẽ chỉnh lại. Đặt to là mất mạng phải chờ lâu.")]
        [Min(0f)] public float bootMaxWaitSeconds = 2f;

        [Tooltip("Quay lại app sau khi ở nền ít nhất bấy nhiêu giây thì hỏi lại giờ máy chủ. Nhỏ quá: mỗi lần đóng quảng " +
                 "cáo (app tạm dừng) lại gửi một request.")]
        [Min(0f)] public float resyncAfterBackgroundSeconds = 60f;

        public static TimeConfig CreateDefault() => CreateInstance<TimeConfig>();
    }
}
