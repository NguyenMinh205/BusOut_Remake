using UnityEditor;
using UnityEngine;

namespace BusOut.Core.Editor
{
    /// Menu Tools/BusOut Tools: tiện ích thử game trong Editor (dùng lâu dài, không vào bản build). Tiện ích mới thêm vào đây.
    public static class BusOutTools
    {
        private const string MenuRoot = "Tools/BusOut Tools/";
        private const string DeleteSaveMenu = MenuRoot + "Xoá save người chơi";

        /// Xoá file save → lần Play sau là người chơi mới (giá trị trong Resources/NewPlayerConfig.asset).
        [MenuItem(DeleteSaveMenu)]
        private static void DeletePlayerSave()
        {
            if (!SaveGameManager.HasSave(eData.SecureGameData))
            {
                EditorUtility.DisplayDialog("Xoá save", "Chưa có file save nào — lần Play sau đã là người chơi mới.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Xoá save",
                    "Xoá save người chơi (level, coin, booster, cài đặt)?\nLần Play sau là người chơi mới theo NewPlayerConfig.",
                    "Xoá", "Huỷ"))
                return;
            SaveGameManager.DeleteSave(eData.SecureGameData);
            Debug.Log($"[BusOut] Đã xoá save: {SaveGameManager.GetFilePath(eData.SecureGameData)}");
        }

        /// ⚠️ Không cho xoá lúc đang Play: DataManager đang giữ dữ liệu trong bộ nhớ và lưu lại khi dừng Play → file bị ghi
        /// lại ngay, coi như chưa xoá.
        [MenuItem(DeleteSaveMenu, true)]
        private static bool CanDeletePlayerSave() => !EditorApplication.isPlaying;
    }
}
