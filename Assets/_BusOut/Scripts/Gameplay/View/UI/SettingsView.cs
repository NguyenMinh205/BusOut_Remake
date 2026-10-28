using System;
using BusOut.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Gameplay
{
    /// Popup cài đặt trong ván: công tắc Music / Sound / Vibration, nút Language, nút đóng.
    /// Gắn trên Popup/SettingPanel. Panel có thể là chính object này (view không cần Update, tắt vẫn gọi được).
    public class SettingsView : MonoBehaviour
    {
        [SerializeField] private AnimatedPanel panel;
        [SerializeField] private ToggleSwitch musicToggle;
        [SerializeField] private ToggleSwitch soundToggle;
        [SerializeField] private ToggleSwitch vibrationToggle;
        [Tooltip("Nút đổi ngôn ngữ (bấm → ngôn ngữ kế tiếp trong GameplayConfig.languages).")]
        [SerializeField] private Button languageButton;
        [Tooltip("Chữ tên ngôn ngữ đang chọn trên nút Language.")]
        [SerializeField] private TMP_Text languageText;
        [SerializeField] private Button closeButton;
        [Tooltip("Nút \"Continue\" (tiếp tục chơi) — cũng đóng popup. Để trống nếu không có.")]
        [SerializeField] private Button continueButton;

        public event Action<bool> MusicChanged;
        public event Action<bool> SoundChanged;
        public event Action<bool> VibrationChanged;
        public event Action LanguageClicked;
        public event Action CloseClicked;

        private PanelAnimationSettings panelAnimation;

        public bool Init(PanelAnimationSettings animationSettings)
        {
            panelAnimation = animationSettings;
            string missing = "";
            if (panel == null) missing += " Panel";
            else if (!panel.IsValid(out string panelMissing)) missing += $" Panel:{panelMissing.Trim()}";
            if (musicToggle == null) missing += " MusicToggle";
            if (soundToggle == null) missing += " SoundToggle";
            if (vibrationToggle == null) missing += " VibrationToggle";
            if (languageButton == null) missing += " LanguageButton";
            if (languageText == null) missing += " LanguageText";
            if (closeButton == null) missing += " CloseButton";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] SettingsView thiếu tham chiếu:{missing}. Kéo object trong SettingPanel vào.", this);
                return false;
            }
            panel.Init();
            musicToggle.ValueChanged += HandleToggle;
            soundToggle.ValueChanged += HandleToggle;
            vibrationToggle.ValueChanged += HandleToggle;
            languageButton.onClick.AddListener(HandleLanguage);
            closeButton.onClick.AddListener(HandleClose);
            if (continueButton != null) continueButton.onClick.AddListener(HandleClose);
            Hide();
            return true;
        }

        private void OnDestroy()
        {
            if (musicToggle != null) musicToggle.ValueChanged -= HandleToggle;
            if (soundToggle != null) soundToggle.ValueChanged -= HandleToggle;
            if (vibrationToggle != null) vibrationToggle.ValueChanged -= HandleToggle;
            if (languageButton != null) languageButton.onClick.RemoveListener(HandleLanguage);
            if (closeButton != null) closeButton.onClick.RemoveListener(HandleClose);
            if (continueButton != null) continueButton.onClick.RemoveListener(HandleClose);
        }

        /// Mở popup với trạng thái đang lưu: công tắc đặt ngay (không trượt), rồi popup hiện.
        public void Show(bool musicOn, bool soundOn, bool vibrationOn, string languageName)
        {
            musicToggle.SetIsOn(musicOn, false);
            soundToggle.SetIsOn(soundOn, false);
            vibrationToggle.SetIsOn(vibrationOn, false);
            SetLanguageName(languageName);
            panel.Show(panelAnimation);
        }

        public void SetLanguageName(string languageName) => languageText.SetText(languageName);

        public void Hide() => panel.Hide();

        private void HandleToggle(ToggleSwitch toggle, bool isOn)
        {
            if (toggle == musicToggle) MusicChanged?.Invoke(isOn);
            else if (toggle == soundToggle) SoundChanged?.Invoke(isOn);
            else if (toggle == vibrationToggle) VibrationChanged?.Invoke(isOn);
        }

        private void HandleLanguage() => LanguageClicked?.Invoke();
        private void HandleClose() => CloseClicked?.Invoke();
    }
}
