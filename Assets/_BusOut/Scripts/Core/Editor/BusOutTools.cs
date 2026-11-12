using System;
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
        private const string FpsOverlayMenu = MenuRoot + "Hiện FPS";
        private const string TimeMenu = MenuRoot + "Thời gian/";
        private const string AdsMenu = MenuRoot + "Quảng cáo/";
        private const string NoAdsMenu = AdsMenu + "Remove Ads (noAds)";
        private const string SimulateResumeMenu = AdsMenu + "Giả lập quay lại app sau 60s (App Open)";

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

        /// Bật / tắt dòng FPS (FpsOverlay). Nhớ qua các lần Play; đổi lúc đang Play thì hiện / ẩn ngay.
        [MenuItem(FpsOverlayMenu)]
        private static void ToggleFpsOverlay()
        {
            bool visible = PlayerPrefs.GetInt(FpsOverlay.EditorPrefKey, 0) != 1;
            PlayerPrefs.SetInt(FpsOverlay.EditorPrefKey, visible ? 1 : 0);
            PlayerPrefs.Save();
            FpsOverlay.SetVisible(visible);
        }

        // Dấu tích cạnh mục menu cho biết đang bật hay tắt.
        [MenuItem(FpsOverlayMenu, true)]
        private static bool ValidateFpsOverlay()
        {
            Menu.SetChecked(FpsOverlayMenu, PlayerPrefs.GetInt(FpsOverlay.EditorPrefKey, 0) == 1);
            return true;
        }

        // Tua giờ game (TimeService) để thử quà ngày / giới hạn theo ngày mà không đổi giờ máy. Nhớ qua các lần Play;
        // đang Play thì ăn ngay (sang ngày → NewDayStartedEvent). Không đụng save: giờ lưu trong save luôn là giờ thật.
        [MenuItem(TimeMenu + "Tua +1 giờ")]
        private static void ShiftTimeHour() => ShiftTime(TimeSpan.FromHours(1));

        [MenuItem(TimeMenu + "Tua +1 ngày")]
        private static void ShiftTimeDay() => ShiftTime(TimeSpan.FromDays(1));

        [MenuItem(TimeMenu + "Về giờ thật")]
        private static void ResetTimeShift()
        {
            TimeService.SetDebugOffset(TimeSpan.Zero);
            Debug.Log("[BusOut] Giờ game = giờ thật (bỏ tua).");
        }

        // Bật / tắt Remove Ads để thử luồng không quảng cáo. Đang Play: qua AdService (ẩn banner ngay); không Play: sửa
        // thẳng file save (như "Đặt level...").
        [MenuItem(NoAdsMenu)]
        private static void ToggleNoAds()
        {
            if (EditorApplication.isPlaying)
            {
                AdService.SetNoAds(!DataManager.Instance.GameData.noAds);
                return;
            }
            SecureGameData data = LoadPlayerSave();
            data.noAds = !data.noAds;
            data.Save();
            Debug.Log($"[BusOut] Remove Ads = {data.noAds} (đã ghi save).");
        }

        [MenuItem(NoAdsMenu, true)]
        private static bool ValidateNoAds()
        {
            bool noAds = EditorApplication.isPlaying ? DataManager.HasInstance && DataManager.Instance.GameData.noAds
                : LoadPlayerSave().noAds;
            Menu.SetChecked(NoAdsMenu, noAds);
            return true;
        }

        // Editor không đưa game vào nền được → giả lập lần quay lại để thử App Open (vẫn theo quy tắc: level, noAds…).
        [MenuItem(SimulateResumeMenu)]
        private static void SimulateResume() => AdService.Instance.DebugSimulateResume(60);

        [MenuItem(SimulateResumeMenu, true)]
        private static bool CanSimulateResume() => EditorApplication.isPlaying && AdService.HasInstance;

        private static void ShiftTime(TimeSpan delta)
        {
            TimeSpan offset = TimeService.DebugOffset + delta;
            TimeService.SetDebugOffset(offset);
            string now = EditorApplication.isPlaying && TimeService.HasInstance
                ? $", giờ game {TimeService.Instance.LocalNow:yyyy-MM-dd HH:mm}" : " (áp dụng từ lần Play sau)";
            Debug.Log($"[BusOut] Tua giờ game: lệch {offset.TotalHours:0.#} giờ so với giờ thật{now}.");
        }

        /// Nạp save như lúc chạy game: chưa có file → người chơi mới theo NewPlayerConfig.
        public static SecureGameData LoadPlayerSave()
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
