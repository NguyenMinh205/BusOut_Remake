using BusOut.Core;
using BusOut.DesignPattern;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Popup cài đặt: đọc / ghi SecureGameData (lưu ngay mỗi lần đổi) và phát SettingsChangedEvent / LanguageChangedEvent
    /// cho hệ thống âm thanh, rung, đa ngôn ngữ (chưa có) nghe. Không dừng ván: Dim chặn chạm xuống bãi là đủ.
    /// GameplayPresenter tạo; HudPresenter gọi Open khi bấm nút cài đặt.
    public sealed class SettingsPresenter
    {
        private readonly GameplayConfig config;
        private readonly SecureGameData gameData;
        private readonly SettingsView view;

        public SettingsPresenter(GameplayConfig config, SecureGameData gameData, SettingsView view)
        {
            this.config = config;
            this.gameData = gameData;
            this.view = view;
            view.MusicChanged += HandleMusicChanged;
            view.SoundChanged += HandleSoundChanged;
            view.VibrationChanged += HandleVibrationChanged;
            view.LanguageClicked += HandleLanguageClicked;
            view.CloseClicked += Hide;
        }

        public void Open() =>
            view.Show(gameData.musicOn, gameData.sfxOn, gameData.vibrationOn, LanguageName(gameData.language));

        public void Hide() => view.Hide();

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

        /// Chuyển sang ngôn ngữ kế tiếp trong config (vòng lại đầu). TẠM: chỉ lưu + báo sự kiện, chữ trong game chưa đổi.
        private void HandleLanguageClicked()
        {
            if (config.languages == null || config.languages.Count == 0) return;
            int index = config.languages.FindIndex(l => l.code == gameData.language);
            LanguageOption next = config.languages[(index + 1) % config.languages.Count];
            gameData.language = next.code;
            gameData.Save();
            view.SetLanguageName(next.displayName);
            EventBus.Publish(new LanguageChangedEvent { Code = next.code });
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
