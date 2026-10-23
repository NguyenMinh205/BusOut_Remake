using UnityEngine;

namespace BusOut.Core
{
    /// Co RectTransform này vào vùng an toàn của màn hình (tránh tai thỏ, đục lỗ camera, thanh home).
    /// Gắn trên object "SafeArea" kéo giãn toàn Canvas; UI cần tránh tai thỏ (TopUI, BottomUI…) là con của nó.
    /// ⚠️ Nền tối của popup đặt NGOÀI SafeArea: đặt trong là nền không phủ kín màn, lộ dải sáng ở tai thỏ / thanh home.
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect appliedSafeArea;
        private Vector2Int appliedScreenSize;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            Apply();
        }

        /// Safe area đổi khi đổi máy trong Device Simulator, xoay màn, hay chia đôi màn hình: so mỗi frame (rẻ, không
        /// cấp phát), chỉ đặt lại anchor khi thật sự đổi.
        private void Update()
        {
            if (Screen.safeArea != appliedSafeArea || Screen.width != appliedScreenSize.x || Screen.height != appliedScreenSize.y)
                Apply();
        }

        private void Apply()
        {
            appliedSafeArea = Screen.safeArea;
            appliedScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (appliedScreenSize.x <= 0 || appliedScreenSize.y <= 0) return;

            Vector2 anchorMin = appliedSafeArea.position;
            Vector2 anchorMax = appliedSafeArea.position + appliedSafeArea.size;
            anchorMin.x /= appliedScreenSize.x;
            anchorMin.y /= appliedScreenSize.y;
            anchorMax.x /= appliedScreenSize.x;
            anchorMax.y /= appliedScreenSize.y;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            // Anchor đã đúng mép vùng an toàn: lề phải bằng 0, không thì lệch thêm đúng bằng lề cũ.
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
