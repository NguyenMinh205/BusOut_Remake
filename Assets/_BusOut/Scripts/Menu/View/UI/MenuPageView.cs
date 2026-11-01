using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BusOut.Menu
{
    /// Thả tay sau một lần vuốt ngang trang.
    public readonly struct PageSwipe
    {
        /// Vị trí trang lúc bắt đầu kéo (số thực, xem MenuPageView.Progress).
        public readonly float StartProgress;
        /// Quãng kéo ngang (đơn vị UI): âm = kéo sang trái (đi tới trang bên phải).
        public readonly float Distance;
        /// Thời gian kéo (giây).
        public readonly float Duration;

        public PageSwipe(float startProgress, float distance, float duration)
        {
            StartProgress = startProgress;
            Distance = distance;
            Duration = duration;
        }
    }

    /// Các trang Menu (Shop / Home / Achievement) xếp ngang, vuốt ngang để chuyển trang. Vị trí đang xem là Progress
    /// (số thực: 1.5 = nửa Home nửa Achievement) — thanh tab vẽ theo số này nên vuốt dở thì khối sáng cũng dừng giữa hai tab.
    /// View chỉ kéo theo tay và chạy tới trang được bảo; thả tay thì về trang nào do presenter quyết.
    /// Gắn trên ViewPort. ViewPort cần Image trong suốt (Raycast Target bật), không thì vuốt ở chỗ trống không ăn.
    /// ⚠️ Không dùng ScrollRect: nó nhận cả vuốt dọc, mà vuốt dọc là để quay bản đồ level (cùng một vùng màn hình).
    /// Ở đây hướng vuốt chốt ngay lúc bắt đầu kéo: ngang → trang, dọc → chuyển nguyên cho ai nghe VerticalDrag* (bản đồ).
    public class MenuPageView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("Content: cha của các trang, căn kín ViewPort. Code dời nó sang ngang để đổi trang.")]
        [SerializeField] private RectTransform content;
        [Tooltip("Các trang theo thứ tự trái → phải, khớp thứ tự tab (Shop, Home, Achievement).")]
        [SerializeField] private RectTransform[] pages;

        public event Action<float> ProgressChanged;
        public event Action<PageSwipe> SwipeReleased;
        /// Vuốt dọc: bắt đầu / mỗi lần ngón tay di chuyển (độ dời dọc, pixel; dương = lên) / thả tay.
        public event Action VerticalDragBegan;
        public event Action<float> VerticalDragged;
        public event Action VerticalDragEnded;

        public int PageCount => pages.Length;
        public float Progress { get; private set; }

        private RectTransform viewport;
        private Canvas rootCanvas;
        private bool isReady;
        private bool isDraggingPages;
        private bool isDraggingVertically;
        private float dragStartProgress;
        private float dragStartTime;
        private Tween scrollTween;

        public bool Init()
        {
            viewport = (RectTransform)transform;
            Canvas canvas = GetComponentInParent<Canvas>();
            string missing = "";
            if (content == null) missing += " Content";
            if (pages == null || pages.Length == 0) missing += " Pages";
            else if (Array.IndexOf(pages, null) >= 0) missing += " Pages(có ô trống)";
            if (canvas == null) missing += " Canvas(cha)";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] MenuPageView thiếu tham chiếu:{missing}. Kéo Content và các panel trang vào.", this);
                return false;
            }
            if (GetComponent<ScrollRect>() != null)
                Debug.LogError("[BusOut] ViewPort còn ScrollRect: nó kéo Content cùng lúc với MenuPageView → trang giật / lệch. " +
                               "Xoá component ScrollRect trên ViewPort.", this);
            rootCanvas = canvas.rootCanvas;
            // Dựng trong scene các trang chồng lên nhau (Shop / Achievement đang tắt); giờ chúng nằm cạnh nhau nên bật hết.
            foreach (RectTransform page in pages) page.gameObject.SetActive(true);
            isReady = true;
            LayoutPages();
            return true;
        }

        /// Chạy tới trang <paramref name="page"/>; <paramref name="duration"/> ≤ 0 thì đặt ngay.
        public void ScrollTo(int page, float duration)
        {
            KillScrollTween();
            if (duration <= 0f)
            {
                SetProgress(page);
                return;
            }
            scrollTween = DOTween.To(() => Progress, SetProgress, page, duration)
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject)
                .SetTarget(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isReady) return;
            // Hướng tính từ chỗ bắt đầu chạm (đã qua ngưỡng kéo của EventSystem) — delta của một khung quá nhỏ, dễ sai hướng.
            Vector2 movement = eventData.position - eventData.pressPosition;
            isDraggingPages = Mathf.Abs(movement.x) >= Mathf.Abs(movement.y);
            isDraggingVertically = !isDraggingPages;
            if (isDraggingVertically)
            {
                VerticalDragBegan?.Invoke();
                return;
            }
            KillScrollTween();
            dragStartProgress = Progress;
            dragStartTime = Time.unscaledTime;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isDraggingVertically)
            {
                VerticalDragged?.Invoke(eventData.delta.y);
                return;
            }
            if (!isDraggingPages) return;
            float pageWidthPixels = viewport.rect.width * rootCanvas.scaleFactor;
            if (pageWidthPixels <= 0f) return;
            // Kéo sang trái (delta âm) → nội dung đi theo tay → trang bên phải hiện ra → Progress tăng.
            SetProgress(Progress - eventData.delta.x / pageWidthPixels);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (isDraggingVertically)
            {
                isDraggingVertically = false;
                VerticalDragEnded?.Invoke();
                return;
            }
            if (!isDraggingPages) return;
            isDraggingPages = false;
            float distance = (eventData.position.x - eventData.pressPosition.x) / rootCanvas.scaleFactor;
            SwipeReleased?.Invoke(new PageSwipe(dragStartProgress, distance, Time.unscaledTime - dragStartTime));
        }

        // Màn đổi cỡ (xoay máy, đổi máy trong Simulator): trang luôn rộng đúng một ViewPort.
        private void OnRectTransformDimensionsChange()
        {
            if (isReady) LayoutPages();
        }

        private void LayoutPages()
        {
            float width = viewport.rect.width;
            for (int i = 0; i < pages.Length; i++)
                pages[i].anchoredPosition = new Vector2(i * width, pages[i].anchoredPosition.y);
            // Báo lại Progress (dù không đổi) để thanh tab vẽ lại: vị trí tab cũng vừa đổi theo màn.
            SetProgress(Progress);
        }

        private void SetProgress(float progress)
        {
            Progress = Mathf.Clamp(progress, 0f, pages.Length - 1);
            ApplyContentPosition();
            ProgressChanged?.Invoke(Progress);
        }

        private void ApplyContentPosition() =>
            content.anchoredPosition = new Vector2(-Progress * viewport.rect.width, content.anchoredPosition.y);

        private void KillScrollTween()
        {
            if (scrollTween != null && scrollTween.IsActive()) scrollTween.Kill();
            scrollTween = null;
        }
    }
}
