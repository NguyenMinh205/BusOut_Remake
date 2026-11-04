using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusOut.Core
{
    /// Số chỉnh được của UI dùng chung Menu + màn chơi: hoạt ảnh mọi popup, popup Cài đặt.
    [CreateAssetMenu(menuName = "BusOut/UI Config", fileName = "UIConfig")]
    public class UIConfig : ScriptableObject
    {
        [Header("Hoạt ảnh popup (mọi popup: cài đặt, rời ván, kết quả, mua)")]
        [Tooltip("Mở popup: nền tối mờ dần vào trong bao lâu (giây).")]
        public float popupDimFadeDuration = 0.2f;

        [Tooltip("Mở popup: mỗi object hiện (phóng / rơi / trồi / hiện dần) trong bao lâu (giây).")]
        public float popupItemDuration = 0.35f;

        [Tooltip("Mở popup: object sau hiện trễ hơn object trước bao lâu (giây).")]
        public float popupItemInterval = 0.12f;

        [Tooltip("Mở popup: nút phụ (đóng, \"Tap to collect\") hiện trễ thêm bao lâu sau object cuối (giây). Để người " +
                 "chơi kịp nhìn nút chính trước.")]
        public float popupLateDelay = 0.8f;

        [Tooltip("Mở popup: quãng trượt của kiểu rơi xuống / trồi lên (đơn vị UI, theo Reference Resolution 1170×2532).")]
        public float popupSlideDistance = 160f;

        [Header("Chuyển cảnh Menu ↔ màn chơi")]
        [Tooltip("Tranh Loading hiện dần / mờ đi trong bao lâu (giây). Gốc: 0.25.")]
        public float transitionFadeDuration = 0.25f;

        [Tooltip("Tranh Loading che kín ít nhất bao lâu (giây) trước khi mờ đi: ngắn quá thì tranh chớp qua, không đọc kịp.")]
        public float transitionMinHoldDuration = 1f;

        // Hiệu ứng chạm + nút nhún khi bấm: chuyển sang FxConfig (4/11/2026).

        [Header("Cài đặt")]
        [Tooltip("Ngôn ngữ chọn được (code lưu vào save, displayName hiện ở SettingsView.languageText nếu có). Mã trên từng nút " +
                 "trong bảng chọn ngôn ngữ (SettingsView › Language Choices) phải có ở đây. ⚠️ Font chữ phải có đủ glyph của " +
                 "displayName (vd. dấu tiếng Việt), không thì hiện ô vuông.")]
        public List<LanguageOption> languages = new List<LanguageOption>
        {
            new LanguageOption { code = "en", displayName = "English" },
            new LanguageOption { code = "vi", displayName = "Tiếng Việt" },
        };

        public PanelAnimationSettings PanelAnimation => new PanelAnimationSettings
        {
            DimFadeDuration = popupDimFadeDuration,
            ItemDuration = popupItemDuration,
            ItemInterval = popupItemInterval,
            LateDelay = popupLateDelay,
            SlideDistance = popupSlideDistance,
        };

        public static UIConfig CreateDefault() => CreateInstance<UIConfig>();
    }

    /// Một ngôn ngữ trong cài đặt.
    [Serializable]
    public class LanguageOption
    {
        public string code;
        public string displayName;
    }
}
