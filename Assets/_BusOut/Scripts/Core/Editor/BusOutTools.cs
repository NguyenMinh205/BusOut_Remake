using UnityEditor;
using UnityEngine;

namespace BusOut.Core.Editor
{
    /// Menu Tools/BusOut Tools: tiện ích thử game trong Editor (dùng lâu dài, không vào bản build). Tiện ích mới thêm vào đây.
    public static class BusOutTools
    {
        private const string MenuRoot = "Tools/BusOut Tools/";
        private const string DeleteSaveMenu = MenuRoot + "Xoá save người chơi";
        private const string SetLevelMenu = MenuRoot + "Đặt level...";

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

        [MenuItem(SetLevelMenu)]
        private static void OpenSetLevelWindow() => SetLevelWindow.Open();

        /// Nạp save như lúc chạy game: chưa có file → người chơi mới theo NewPlayerConfig.
        internal static SecureGameData LoadPlayerSave()
        {
            NewPlayerConfig newPlayer = Resources.Load<NewPlayerConfig>(NewPlayerConfig.ResourcePath);
            if (newPlayer == null) newPlayer = NewPlayerConfig.CreateDefault();
            SecureGameData data = SecureGameData.CreateNew(newPlayer);
            data.Load();
            return data;
        }
    }

    /// Đặt level đang chơi trong save mà không phải xoá save (giữ coin, booster, cài đặt). Lần Play sau vào thẳng level đó.
    public class SetLevelWindow : EditorWindow
    {
        private int savedLevel;
        private bool hasSave;
        private int targetLevel = 1;

        public static void Open()
        {
            var window = GetWindow<SetLevelWindow>(true, "BusOut: Đặt level");
            window.minSize = new Vector2(320f, 120f);
            window.Refresh();
            window.targetLevel = window.savedLevel;
        }

        // Quay lại cửa sổ sau khi chơi: save có thể đã đổi level (thắng ván) → đọc lại.
        private void OnFocus() => Refresh();

        private void Refresh()
        {
            hasSave = SaveGameManager.HasSave(eData.SecureGameData);
            savedLevel = BusOutTools.LoadPlayerSave().currentLevel;
        }

        private void OnGUI()
        {
            // ⚠️ Không ghi lúc đang Play: DataManager giữ bản trong bộ nhớ và lưu đè lúc dừng Play → mất level vừa đặt.
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Dừng Play rồi mới đặt level (đang Play thì game lưu đè lúc dừng).", MessageType.Warning);
                return;
            }
            EditorGUILayout.LabelField("Level trong save",
                hasSave ? savedLevel.ToString() : $"{savedLevel} (chưa có save — người chơi mới)");
            targetLevel = Mathf.Max(1, EditorGUILayout.IntField("Level muốn chơi", targetLevel));
            EditorGUILayout.Space();
            if (GUILayout.Button($"Ghi level {targetLevel} vào save")) WriteLevel(targetLevel);
        }

        private void WriteLevel(int level)
        {
            SecureGameData data = BusOutTools.LoadPlayerSave();
            data.currentLevel = level;
            data.Save();
            Refresh();
            Debug.Log($"[BusOut] Đã đặt level {level} trong save (coin, booster, cài đặt giữ nguyên).");
        }
    }
}
