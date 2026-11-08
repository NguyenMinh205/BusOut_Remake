using BusOut.DesignPattern;
using UnityEngine;

namespace BusOut.Core
{
    /// Đẩy một khối UI đáy màn (thanh booster) lên đúng bằng chiều cao banner quảng cáo, để banner không che nút
    /// (nghe BannerChangedEvent; không có banner / Remove Ads → về chỗ cũ).
    /// Gắn trên khối cần đẩy (vd. GameContext/UI/Canvas/SafeArea/BottomUI). Vị trí trong scene = vị trí khi không có banner.
    /// Banner AdMob đặt ở đáy vùng an toàn (iOS: trên thanh home) — khối này cũng nằm trong SafeArea → chỉ cần cộng chiều cao.
    [RequireComponent(typeof(RectTransform))]
    public class BannerSpaceView : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Canvas rootCanvas;
        private Vector2 basePosition;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            basePosition = rectTransform.anchoredPosition;
            Canvas canvas = GetComponentInParent<Canvas>(true);
            rootCanvas = canvas != null ? canvas.rootCanvas : null;
            if (rootCanvas == null)
                Debug.LogError($"[BusOut] BannerSpaceView trên '{name}' không nằm trong Canvas: không đổi được pixel banner sang " +
                               "đơn vị UI. Gắn lên object trong Canvas.", this);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<BannerChangedEvent>(HandleBannerChanged);
            // Đọc trạng thái hiện tại: banner có thể đã hiện / ẩn lúc object này đang tắt (EventBus không giữ sự kiện cũ).
            Apply(AdService.BannerHeightPixels);
        }

        private void OnDisable() => EventBus.Unsubscribe<BannerChangedEvent>(HandleBannerChanged);

        private void HandleBannerChanged(BannerChangedEvent e) => Apply(e.HeightPixels);

        private void Apply(float heightPixels)
        {
            if (rootCanvas == null) return;
            // Pixel màn hình → đơn vị UI: chia cho hệ số co của Canvas Scaler.
            float scale = Mathf.Max(0.0001f, rootCanvas.scaleFactor);
            rectTransform.anchoredPosition = basePosition + new Vector2(0f, heightPixels / scale);
        }
    }
}
