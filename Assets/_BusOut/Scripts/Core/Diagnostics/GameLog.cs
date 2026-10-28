using System.Diagnostics;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace BusOut.Core
{
    /// Log thông tin để dò luồng game (thắng / thua, booster, hard point…): chỉ có trong Editor và bản Development Build.
    /// Bản phát hành bỏ hẳn cả lời gọi lẫn việc dựng chuỗi ($"..." ở nơi gọi) nhờ [Conditional].
    /// ⚠️ Debug.Log trong bản phát hành vẫn chạy và chụp stack trace (Player Settings đang để ScriptOnly) — mỗi lần
    /// cỡ 0.5–1 ms trên điện thoại, đúng lúc thắng / dùng booster là giật nhẹ. Lỗi cấu hình vẫn dùng Debug.LogError
    /// / LogWarning: những cái đó phải thấy được cả ở bản phát hành.
    public static class GameLog
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string message, Object context = null) => Debug.Log(message, context);
    }
}
