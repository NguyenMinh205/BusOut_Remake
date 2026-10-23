using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace BusOut.Gameplay
{
    /// Thời gian hoạt ảnh mở panel (lấy từ GameplayConfig).
    public struct PanelAnimationSettings
    {
        public float DimFadeDuration;
        public float ItemPopDuration;
        public float ItemInterval;
    }

    /// Một panel popup: bật object panel, nền tối (Dim) mờ dần vào, rồi từng object trong RevealItems lần lượt
    /// phóng từ 0 lên cỡ gốc (OutBack). Không có hoạt ảnh lặp (đứng yên sau khi hiện xong).
    [Serializable]
    public class AnimatedPanel
    {
        [Tooltip("Object panel (WinPanel / ContinuePanel / LosePanel): bật khi hiện, tắt khi ẩn.")]
        public GameObject root;
        [Tooltip("Nền tối của panel (Dim): mờ dần từ 0 tới độ mờ đang đặt trên Image.")]
        [FormerlySerializedAs("dime")]
        public Image dim;
        [Tooltip("Các object hiện lần lượt theo thứ tự (trên → dưới).")]
        public RectTransform[] revealItems;

        private Sequence sequence;
        private float dimAlpha;
        private Vector3[] itemScales;

        public bool IsValid(out string missing)
        {
            missing = "";
            if (root == null) missing += " Root";
            if (dim == null) missing += " Dim";
            if (revealItems != null)
                for (int i = 0; i < revealItems.Length; i++)
                    if (revealItems[i] == null) missing += $" RevealItems[{i}]";
            return missing.Length == 0;
        }

        /// ⚠️ Đọc độ mờ Dim và cỡ gốc từng object MỘT lần lúc Init (khi chưa tween): đọc lúc Show là đọc trúng giá trị
        /// tween dở của lần trước (vd. 0) → lần sau không hiện.
        public void Init()
        {
            dimAlpha = dim.color.a;
            int count = revealItems != null ? revealItems.Length : 0;
            itemScales = new Vector3[count];
            for (int i = 0; i < count; i++) itemScales[i] = revealItems[i].localScale;
        }

        public void Show(PanelAnimationSettings settings)
        {
            Kill();
            root.SetActive(true);
            SetDimAlpha(0f);
            for (int i = 0; i < itemScales.Length; i++) revealItems[i].localScale = Vector3.zero;

            sequence = DOTween.Sequence().SetLink(root).SetTarget(root);
            // ⚠️ Không dùng Image.DOFade: nằm trong DOTween Modules (biên dịch vào firstpass), asmdef không thấy.
            sequence.Append(DOTween.To(() => dim.color.a, SetDimAlpha, dimAlpha, settings.DimFadeDuration));
            for (int i = 0; i < itemScales.Length; i++)
            {
                float at = settings.DimFadeDuration * 0.5f + i * settings.ItemInterval;
                sequence.Insert(at, revealItems[i].DOScale(itemScales[i], settings.ItemPopDuration).SetEase(Ease.OutBack));
            }
        }

        public void Hide()
        {
            Kill();
            // Trả cỡ gốc: panel bị ẩn giữa chừng (chơi lại khi đang hiện) thì lần hiện sau vẫn đúng cỡ.
            for (int i = 0; i < itemScales.Length; i++) revealItems[i].localScale = itemScales[i];
            SetDimAlpha(dimAlpha);
            root.SetActive(false);
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
