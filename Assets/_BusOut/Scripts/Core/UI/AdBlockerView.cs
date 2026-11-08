using BusOut.DesignPattern;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Core
{
    /// Lớp chặn chạm toàn màn trong lúc quảng cáo toàn màn đang mở (nghe AdShowingChangedEvent).
    /// Vì sao cần: giữa lúc bấm "xem QC" và lúc QC thật sự che màn có vài khung hình — bấm thêm "Nhận" / chạm xe lúc đó là
    /// nhận thưởng 2 lần hoặc ván chạy dưới QC. Chặn bằng một Image TRONG SUỐT bật / tắt Raycast Target, không bật / tắt
    /// object (object tắt thì không nghe được sự kiện). Chạm xe cũng bị chặn: GameplayInput bỏ qua chạm trúng UI.
    /// Gắn trên object AdBlocker (con của SharedUI, LUÔN bật): gắn vào là tự có Image trong suốt + phủ kín màn.
    // ⚠️ RequireComponent phải là Image, KHÔNG phải Graphic: Graphic là lớp abstract → Unity không tự thêm được, báo
    // "Can't add script behaviour 'Graphic'" và không gắn được script (đã gặp 8/11/2026).
    [RequireComponent(typeof(Image))]
    public class AdBlockerView : MonoBehaviour
    {
        [SerializeField] private Image blocker;

        // Chạy trong Editor lúc vừa gắn script: Image tự thêm có màu TRẮNG đặc → đổi trong suốt, phủ kín màn.
        private void Reset()
        {
            blocker = GetComponent<Image>();
            blocker.color = Color.clear;
            blocker.raycastTarget = false;
            if (transform is RectTransform rect)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
            }
        }

        private void Awake()
        {
            if (blocker == null) blocker = GetComponent<Image>();
            // Luôn trong suốt dù ai đó lỡ đổi màu trong Inspector: lớp này chỉ để chặn chạm, không để nhìn.
            blocker.color = Color.clear;
            // Image alpha 0: không vẽ gì (khỏi tốn một lớp phủ kín màn) nhưng vẫn nhận raycast khi bật Raycast Target.
            blocker.canvasRenderer.cullTransparentMesh = true;
            blocker.raycastTarget = false;
        }

        private void OnEnable() => EventBus.Subscribe<AdShowingChangedEvent>(HandleAdShowingChanged);

        private void OnDisable()
        {
            EventBus.Unsubscribe<AdShowingChangedEvent>(HandleAdShowingChanged);
            blocker.raycastTarget = false;
        }

        private void HandleAdShowingChanged(AdShowingChangedEvent e) => blocker.raycastTarget = e.IsShowing;
    }
}
