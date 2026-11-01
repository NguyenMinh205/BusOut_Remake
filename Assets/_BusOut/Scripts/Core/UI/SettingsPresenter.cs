using System;
using BusOut.DesignPattern;
using UnityEngine;

namespace BusOut.Core
{
    /// Popup cài đặt: đọc / ghi SecureGameData (lưu ngay mỗi lần đổi) và phát SettingsChangedEvent / LanguageChangedEvent
    /// cho hệ thống âm thanh, rung, đa ngôn ngữ (chưa có) nghe. Không dừng ván: Dim chặn chạm xuống bãi là đủ.
    /// SharedPopupsPresenter tạo; Menu / màn chơi mở qua SharedPopupsPresenter.OpenSettings.
    public sealed class SettingsPresenter
    {
        private readonly UIConfig config;
        private readonly SecureGameData gameData;
        private readonly SettingsView view;
        private readonly Action homeRequested;
        private readonly Action quitRequested;

        /// <paramref name="homeRequested"/>: bấm Home (chỉ có trong ván). <paramref name="quitRequested"/>: bấm Leave game
        /// (thoát ứng dụng, ở cả Menu lẫn trong ván). Popup cài đặt đã tự đóng trước khi gọi.
        public SettingsPresenter(UIConfig config, SecureGameData gameData, SettingsView view, Action homeRequested,
            Action quitRequested)
        {
            this.config = config;
            this.gameData = gameData;
            this.view = view;
            this.homeRequested = homeRequested;
            this.quitRequested = quitRequested;
            view.MusicChanged += HandleMusicChanged;
            view.SoundChanged += HandleSoundChanged;
            view.VibrationChanged += HandleVibrationChanged;
            view.LanguageClicked += view.ShowLanguageList;
            view.LanguageChosen += HandleLanguageChosen;
            view.LanguageListClosed += view.HideLanguageList;
            view.CloseClicked += Hide;
            view.HomeClicked += HandleHomeClicked;
            view.QuitClicked += HandleQuitClicked;
        }

        public void Open(PopupContext context) =>
            view.Show(context, gameData.musicOn, gameData.sfxOn, gameData.vibrationOn, LanguageName(gameData.language));

        public void Hide() => view.Hide();

        private void HandleHomeClicked()
        {
            Hide();
            homeRequested();
        }

        private void HandleQuitClicked()
        {
            Hide();
            quitRequested();
        }

        private void HandleMusicChanged(bool isOn)
        {
            gameData.musicOn = isOn;
            SaveAndPublish();
        }

        private void HandleSoundChanged(bool isOn)
        {
            gameData.sfxOn = isOn;
            SaveAndPublish();
        }

        private void HandleVibrationChanged(bool isOn)
        {
            gameData.vibrationOn = isOn;
            // Vừa bật rung: rung một nhịp cho người chơi biết rung đang hoạt động (chỉ máy thật mới rung).
#if UNITY_ANDROID || UNITY_IOS
            if (isOn) Handheld.Vibrate();
#endif
            SaveAndPublish();
        }

        /// Chọn ngôn ngữ trong bảng: lưu + báo LanguageChangedEvent, đóng bảng. TẠM: chữ trong game chưa đổi (Task 10).
        private void HandleLanguageChosen(string code)
        {
            LanguageOption option = config.languages?.Find(l => l.code == code);
            if (option == null)
            {
                Debug.LogError($"[BusOut] Nút ngôn ngữ mã \"{code}\" không có trong UIConfig.languages: không đổi ngôn ngữ. " +
                               "Sửa mã trên nút (SettingsView › Language Choices) hoặc thêm ngôn ngữ vào UIConfig.");
                return;
            }
            view.HideLanguageList();
            if (option.code == gameData.language) return;
            gameData.language = option.code;
            gameData.Save();
            view.SetLanguageName(option.displayName);
            EventBus.Publish(new LanguageChangedEvent { Code = option.code });
        }

        private void SaveAndPublish()
        {
            gameData.Save();
            EventBus.Publish(new SettingsChangedEvent
            {
                MusicOn = gameData.musicOn, SoundOn = gameData.sfxOn, VibrationOn = gameData.vibrationOn,
            });
        }

        private string LanguageName(string code)
        {
            LanguageOption option = config.languages?.Find(l => l.code == code);
            return option != null ? option.displayName : code;
        }
    }
}
