using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BusOut.Core
{
    /// Mở popup ở đâu: quyết định nhóm nút nào hiện (SettingsView.inGameOnly / menuOnly).
    public enum PopupContext
    {
        Menu,
        InGame,
    }

    /// Một nút trong bảng chọn ngôn ngữ: nút nào ứng với ngôn ngữ nào.
    [Serializable]
    public class LanguageChoice
    {
        [Tooltip("Mã ngôn ngữ, phải có trong UIConfig.languages (vd. en, vi).")]
        public string code;
        public Button button;
    }

    /// Popup cài đặt dùng chung Menu + màn chơi: công tắc Music / Sound / Vibration, nút Language, nút đóng; nút chỉ có
    /// trong ván (Continue, Home) hoặc chỉ có ở Menu (About Us…) bật / tắt theo PopupContext. Nút Leave game (thoát ứng
    /// dụng) có ở cả hai nơi.
    /// Gắn trên SharedUI/.../SettingPanel. Panel có thể là chính object này (view không cần Update, tắt vẫn gọi được).
    public class SettingsView : MonoBehaviour
    {
        [SerializeField] private AnimatedPanel panel;
        [SerializeField] private ToggleSwitch musicToggle;
        [SerializeField] private ToggleSwitch soundToggle;
        [SerializeField] private ToggleSwitch vibrationToggle;
        [Tooltip("Nút Language: mở bảng chọn ngôn ngữ (SelectLanguage).")]
        [SerializeField] private Button languageButton;
        [Tooltip("Tuỳ chọn: chữ hiện tên ngôn ngữ đang chọn (vd. trên nút Language). Để trống nếu nút chỉ ghi chữ cố định.")]
        [SerializeField] private TMP_Text languageText;
        [SerializeField] private Button closeButton;
        [Tooltip("Nút \"Continue\" (tiếp tục chơi) — cũng đóng popup. Để trống nếu không có.")]
        [SerializeField] private Button continueButton;
        [Tooltip("Nút \"Home\": rời ván về Menu (chỉ có trong ván). Để trống nếu không có.")]
        [SerializeField] private Button homeButton;
        [Tooltip("Nút chữ \"Leave game?\": thoát hẳn ứng dụng (hỏi lại bằng popup Leave game). Có ở cả Menu lẫn trong ván. " +
                 "Để trống nếu không có.")]
        [SerializeField] private Button leaveGameButton;

        [Header("Bảng chọn ngôn ngữ (bấm nút Language)")]
        [Tooltip("SettingPopup/SelectLanguage: Root = SelectLanguage, Dim = SelectLanguage/Dim.")]
        [SerializeField] private AnimatedPanel languagePanel;
        [Tooltip("Nút đóng bảng chọn ngôn ngữ (SelectLanguage/.../CloseBtn).")]
        [SerializeField] private Button languageCloseButton;
        [Tooltip("Các nút ngôn ngữ trong ListLanguage, mỗi nút một mã ngôn ngữ.")]
        [SerializeField] private LanguageChoice[] languageChoices;

        [Header("Hiện theo nơi mở")]
        [Tooltip("Object chỉ hiện khi mở trong ván (ContinueBtn, HomeBtn…). Ở Menu thì ẩn. KHÔNG cho LeaveGameBtn vào " +
                 "(thoát ứng dụng — cần ở cả hai nơi).")]
        [SerializeField] private GameObject[] inGameOnly;
        [Tooltip("Object chỉ hiện khi mở ở Menu (About Us…). Trong ván thì ẩn.")]
        [SerializeField] private GameObject[] menuOnly;

        public event Action<bool> MusicChanged;
        public event Action<bool> SoundChanged;
        public event Action<bool> VibrationChanged;
        public event Action LanguageClicked;
        /// Chọn một ngôn ngữ trong bảng: mã ngôn ngữ (LanguageChoice.code).
        public event Action<string> LanguageChosen;
        public event Action LanguageListClosed;
        public event Action CloseClicked;
        public event Action HomeClicked;
        public event Action QuitClicked;

        private PanelAnimationSettings panelAnimation;
        private UnityAction[] languageChoiceHandlers;

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
            if (closeButton == null) missing += " CloseButton";
            if (languagePanel == null) missing += " LanguagePanel";
            else if (!languagePanel.IsValid(out string languagePanelMissing)) missing += $" LanguagePanel:{languagePanelMissing.Trim()}";
            if (languageCloseButton == null) missing += " LanguageCloseButton";
            if (languageChoices == null || languageChoices.Length == 0) missing += " LanguageChoices";
            else
                for (int i = 0; i < languageChoices.Length; i++)
                    if (languageChoices[i] == null || languageChoices[i].button == null || string.IsNullOrEmpty(languageChoices[i].code))
                        missing += $" LanguageChoices[{i}](nút / mã)";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] SettingsView thiếu tham chiếu:{missing}. Kéo object trong SettingPanel vào.", this);
                return false;
            }
            panel.Init();
            languagePanel.Init();
            languageCloseButton.onClick.AddListener(HandleLanguageListClosed);
            languageChoiceHandlers = new UnityAction[languageChoices.Length];
            for (int i = 0; i < languageChoices.Length; i++)
            {
                string code = languageChoices[i].code;
                languageChoiceHandlers[i] = () => LanguageChosen?.Invoke(code);
                languageChoices[i].button.onClick.AddListener(languageChoiceHandlers[i]);
            }
            musicToggle.ValueChanged += HandleToggle;
            soundToggle.ValueChanged += HandleToggle;
            vibrationToggle.ValueChanged += HandleToggle;
            languageButton.onClick.AddListener(HandleLanguage);
            closeButton.onClick.AddListener(HandleClose);
            if (continueButton != null) continueButton.onClick.AddListener(HandleClose);
            if (homeButton != null) homeButton.onClick.AddListener(HandleHome);
            if (leaveGameButton != null) leaveGameButton.onClick.AddListener(HandleQuit);
            Hide();
            return true;
        }

        private void OnDestroy()
        {
            if (musicToggle != null) musicToggle.ValueChanged -= HandleToggle;
            if (soundToggle != null) soundToggle.ValueChanged -= HandleToggle;
            if (vibrationToggle != null) vibrationToggle.ValueChanged -= HandleToggle;
            if (languageButton != null) languageButton.onClick.RemoveListener(HandleLanguage);
            if (languageCloseButton != null) languageCloseButton.onClick.RemoveListener(HandleLanguageListClosed);
            if (languageChoiceHandlers != null)
                for (int i = 0; i < languageChoiceHandlers.Length; i++)
                    if (languageChoices[i]?.button != null) languageChoices[i].button.onClick.RemoveListener(languageChoiceHandlers[i]);
            if (closeButton != null) closeButton.onClick.RemoveListener(HandleClose);
            if (continueButton != null) continueButton.onClick.RemoveListener(HandleClose);
            if (homeButton != null) homeButton.onClick.RemoveListener(HandleHome);
            if (leaveGameButton != null) leaveGameButton.onClick.RemoveListener(HandleQuit);
        }

        /// Mở popup với trạng thái đang lưu: công tắc đặt ngay (không trượt), nhóm nút theo nơi mở, rồi popup hiện.
        public void Show(PopupContext context, bool musicOn, bool soundOn, bool vibrationOn, string languageName)
        {
            SetActive(inGameOnly, context == PopupContext.InGame);
            SetActive(menuOnly, context == PopupContext.Menu);
            musicToggle.SetIsOn(musicOn, false);
            soundToggle.SetIsOn(soundOn, false);
            vibrationToggle.SetIsOn(vibrationOn, false);
            SetLanguageName(languageName);
            languagePanel.Hide();
            panel.Show(panelAnimation);
        }

        public void SetLanguageName(string languageName)
        {
            if (languageText != null) languageText.SetText(languageName);
        }

        /// Đóng popup cài đặt (kể cả bảng chọn ngôn ngữ đang mở bên trong).
        public void Hide()
        {
            languagePanel.Hide();
            panel.Hide();
        }

        public void ShowLanguageList() => languagePanel.Show(panelAnimation);

        public void HideLanguageList() => languagePanel.Hide();

        private static void SetActive(GameObject[] objects, bool isActive)
        {
            if (objects == null) return;
            foreach (GameObject item in objects)
                if (item != null) item.SetActive(isActive);
        }

        private void HandleToggle(ToggleSwitch toggle, bool isOn)
        {
            if (toggle == musicToggle) MusicChanged?.Invoke(isOn);
            else if (toggle == soundToggle) SoundChanged?.Invoke(isOn);
            else if (toggle == vibrationToggle) VibrationChanged?.Invoke(isOn);
        }

        private void HandleLanguage() => LanguageClicked?.Invoke();
        private void HandleLanguageListClosed() => LanguageListClosed?.Invoke();
        private void HandleClose() => CloseClicked?.Invoke();
        private void HandleHome() => HomeClicked?.Invoke();
        private void HandleQuit() => QuitClicked?.Invoke();
    }
}
