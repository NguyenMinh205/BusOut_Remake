using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Core
{
    /// Thời gian hoạt ảnh mở panel (mọi popup lấy từ UIConfig.PanelAnimation).
    public struct PanelAnimationSettings
    {
        public float DimFadeDuration;
        public float ItemDuration;
        public float ItemInterval;
        /// Object "Late" (nút đóng / bỏ qua) hiện trễ thêm bao lâu sau object thường cuối cùng.
        public float LateDelay;
        /// Quãng trượt (đơn vị UI) của DropIn / RiseIn.
        public float SlideDistance;
    }

    /// Cách một object xuất hiện.
    public enum RevealStyle
    {
        Pop,    // phóng 0 → cỡ gốc, nảy nhẹ (khung popup, cúp, icon)
        DropIn, // rơi từ trên xuống + hiện dần (banner, tiêu đề)
        RiseIn, // trồi từ dưới lên + hiện dần (hàng nút)
        FadeIn, // hiện dần + phóng nhẹ (nút phụ hiện trễ)
    }

    [Serializable]
    public class RevealItem
    {
        public RectTransform target;
        public RevealStyle style = RevealStyle.Pop;
        [Tooltip("Hiện sau cùng, trễ thêm LateDelay, và chỉ bấm được khi đã hiện (nút đóng / \"Tap to collect\").")]
        public bool late;
    }

    /// Một panel popup: bật panel, nền tối (Dim) mờ dần vào, rồi từng object hiện lần lượt theo kiểu riêng.
    /// Không có hoạt ảnh lặp: hiện xong là đứng yên.
    [Serializable]
    public class AnimatedPanel
    {
        [Tooltip("Object panel (WinPanel / ContinuePanel / LosePanel): bật khi hiện, tắt khi ẩn.")]
        public GameObject root;
        [Tooltip("Nền tối của panel (Dim): mờ dần từ 0 tới độ mờ đang đặt trên Image.")]
        public Image dim;
        [Tooltip("Các object hiện lần lượt theo thứ tự (trên → dưới). Object Late luôn hiện sau cùng.")]
        public RevealItem[] revealItems;

        private Sequence sequence;
        private float dimAlpha;
        private Vector3[] scales;
        private Vector2[] positions;
        private CanvasGroup[] groups;
        private float[] startTimes;

        public bool IsValid(out string missing)
        {
            missing = "";
            if (root == null) missing += " Root";
            if (dim == null) missing += " Dim";
            if (revealItems != null)
                for (int i = 0; i < revealItems.Length; i++)
                    if (revealItems[i] == null || revealItems[i].target == null) missing += $" RevealItems[{i}]";
            return missing.Length == 0;
        }

        /// ⚠️ Đọc độ mờ Dim, cỡ và vị trí gốc từng object MỘT lần lúc Init (khi chưa tween): đọc lúc Show là đọc trúng
        /// giá trị tween dở của lần trước (vd. cỡ 0) → lần sau không hiện.
        public void Init()
        {
            dimAlpha = dim.color.a;
            int count = revealItems != null ? revealItems.Length : 0;
            scales = new Vector3[count];
            positions = new Vector2[count];
            groups = new CanvasGroup[count];
            startTimes = new float[count];
            for (int i = 0; i < count; i++)
            {
                RectTransform target = revealItems[i].target;
                scales[i] = target.localScale;
                positions[i] = target.anchoredPosition;
                // Kiểu cần hiện dần / khoá bấm thì cần CanvasGroup; thiếu thì thêm lúc chạy (không lưu vào scene).
                if (revealItems[i].style != RevealStyle.Pop || revealItems[i].late)
                {
                    groups[i] = target.GetComponent<CanvasGroup>();
                    if (groups[i] == null) groups[i] = target.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        /// Thời điểm (giây, tính từ lúc Show) object này bắt đầu hiện; -1 nếu không có trong danh sách.
        /// Dùng để chạy thêm hiệu ứng khớp lúc object hiện (vd. đếm số coin).
        public float RevealTimeOf(RectTransform target)
        {
            for (int i = 0; i < startTimes.Length; i++)
                if (revealItems[i].target == target) return startTimes[i];
            return -1f;
        }

        public Sequence Show(PanelAnimationSettings settings)
        {
            Kill();
            root.SetActive(true);
            SetDimAlpha(0f);
            sequence = DOTween.Sequence().SetLink(root).SetTarget(root);
            // ⚠️ Không dùng Image.DOFade / CanvasGroup.DOFade: nằm trong DOTween Modules (biên dịch vào firstpass),
            // asmdef không thấy → dùng DOTween.To.
            sequence.Insert(0f, DOTween.To(() => dim.color.a, SetDimAlpha, dimAlpha, settings.DimFadeDuration));

            float time = settings.DimFadeDuration * 0.5f;
            int normalCount = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                bool late = pass == 1;
                if (late) time += Mathf.Max(0, normalCount - 1) * settings.ItemInterval + settings.LateDelay;
                for (int i = 0; i < revealItems.Length; i++)
                {
                    if (revealItems[i].late != late) continue;
                    float at = late ? time : time + normalCount * settings.ItemInterval;
                    if (!late) normalCount++;
                    startTimes[i] = at;
                    Reveal(i, at, settings);
                }
            }
            return sequence;
        }

        public void Hide()
        {
            Kill();
            // Trả trạng thái gốc: panel bị ẩn giữa chừng (chơi lại khi đang hiện) thì lần hiện sau vẫn đúng.
            for (int i = 0; i < scales.Length; i++) ResetItem(i);
            SetDimAlpha(dimAlpha);
            root.SetActive(false);
        }

        private void Reveal(int i, float at, PanelAnimationSettings settings)
        {
            RevealItem item = revealItems[i];
            RectTransform target = item.target;
            CanvasGroup group = groups[i];
            float duration = settings.ItemDuration;

            // Trạng thái trước khi hiện.
            switch (item.style)
            {
                case RevealStyle.Pop:
                    target.localScale = Vector3.zero;
                    break;
                case RevealStyle.DropIn:
                    target.anchoredPosition = positions[i] + Vector2.up * settings.SlideDistance;
                    break;
                case RevealStyle.RiseIn:
                    target.anchoredPosition = positions[i] + Vector2.down * settings.SlideDistance;
                    break;
                case RevealStyle.FadeIn:
                    target.localScale = scales[i] * 0.9f;
                    break;
            }
            if (group != null)
            {
                group.alpha = item.style == RevealStyle.Pop ? 1f : 0f;
                // Chưa hiện thì không bấm được: tránh bấm trúng nút còn vô hình.
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            // Hoạt ảnh.
            if (item.style == RevealStyle.Pop || item.style == RevealStyle.FadeIn)
                sequence.Insert(at, target.DOScale(scales[i], duration)
                    .SetEase(item.style == RevealStyle.Pop ? Ease.OutBack : Ease.OutQuad));
            if (item.style == RevealStyle.DropIn || item.style == RevealStyle.RiseIn)
                // ⚠️ Không dùng RectTransform.DOAnchorPos: cũng nằm trong DOTween Modules.
                sequence.Insert(at, DOTween.To(() => target.anchoredPosition, p => target.anchoredPosition = p,
                    positions[i], duration).SetEase(Ease.OutBack));
            if (group != null && item.style != RevealStyle.Pop)
                sequence.Insert(at, DOTween.To(() => group.alpha, a => group.alpha = a, 1f, duration * 0.6f));
            if (group != null)
                sequence.InsertCallback(at, () =>
                {
                    group.interactable = true;
                    group.blocksRaycasts = true;
                });
        }

        private void ResetItem(int i)
        {
            RectTransform target = revealItems[i].target;
            target.localScale = scales[i];
            target.anchoredPosition = positions[i];
            if (groups[i] == null) return;
            groups[i].alpha = 1f;
            groups[i].interactable = true;
            groups[i].blocksRaycasts = true;
        }

        private void Kill()
        {
            if (sequence != null && sequence.IsActive()) sequence.Kill();
            sequence = null;
        }

        private void SetDimAlpha(float alpha)
        {
            Color color = dim.color;
            color.a = alpha;
            dim.color = color;
        }
    }
}
