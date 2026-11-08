using BusOut.DesignPattern;
using UnityEngine;

namespace BusOut.Core
{
    /// Màn "Ad break" hiện 1–2s trước interstitial (AdsConfig.adsBreakSeconds) để báo trước, rồi quảng cáo che lên; quảng cáo
    /// đóng thì ẩn (nghe AdsBreakChangedEvent). Ẩn lúc quảng cáo ĐÓNG chứ không lúc quảng cáo mở: ẩn sớm là lộ màn game một
    /// khung trước khi quảng cáo kịp che.
    /// Gắn trên object AdsBreak (con của SharedUI, LUÔN bật) có CanvasGroup; nội dung (nền tối, Icon_AdsBreak, chữ) là con.
    /// Ẩn / hiện bằng alpha, không bật / tắt object (object tắt thì không nghe được sự kiện).
    [RequireComponent(typeof(CanvasGroup))]
    public class AdsBreakView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        private void Reset() => canvasGroup = GetComponent<CanvasGroup>();

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            SetVisible(false);
        }

        private void OnEnable() => EventBus.Subscribe<AdsBreakChangedEvent>(HandleAdsBreakChanged);

        private void OnDisable()
        {
            EventBus.Unsubscribe<AdsBreakChangedEvent>(HandleAdsBreakChanged);
            SetVisible(false);
        }

        private void HandleAdsBreakChanged(AdsBreakChangedEvent e) => SetVisible(e.IsShowing);

        private void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            // Chặn chạm lúc hiện (AdBlocker cũng chặn, đây là thêm một lớp); lúc ẩn không được nuốt chạm của game.
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = false;
        }
    }
}
