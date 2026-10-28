using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BusOut.Core
{
    /// Công tắc bật / tắt dạng viên thuốc: nền đổi sprite On / Off, núm trượt sang phải (bật) / trái (tắt).
    /// Gắn trên object nền (có Image, Raycast Target bật); núm là con của nền, neo giữa (anchor 0.5, 0.5).
    /// Vị trí núm TỰ TÍNH theo bề rộng nền và núm: chỉnh cỡ nền / núm trong scene thoải mái, không phải đặt số.
    /// Chạm vào bất kỳ đâu trên công tắc (cả núm) đều đổi trạng thái. Không phụ thuộc gì ngoài UnityEngine.UI + DOTween.
    [RequireComponent(typeof(RectTransform))]
    public class ToggleSwitch : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Image nền của công tắc (thường là chính object này).")]
        [SerializeField] private Image background;
        [Tooltip("Sprite nền khi bật. Để trống thì không đổi sprite.")]
        [SerializeField] private Sprite onSprite;
        [Tooltip("Sprite nền khi tắt. Để trống thì không đổi sprite.")]
        [SerializeField] private Sprite offSprite;
        [Tooltip("Núm tròn trượt qua lại (con của nền, anchor giữa).")]
        [SerializeField] private RectTransform knob;
        [Tooltip("Khoảng hở giữa núm và mép nền (đơn vị UI).")]
        [SerializeField] private float knobPadding = 2f;
        [Tooltip("Thời gian núm trượt (giây).")]
        [SerializeField] private float slideDuration = 0.15f;

        private RectTransform rectTransform;
        private Tween slideTween;

        public bool IsOn { get; private set; }
        public bool Interactable { get; set; } = true;

        /// Người chơi bấm đổi trạng thái (không phát khi code gọi SetIsOn).
        public event Action<ToggleSwitch, bool> ValueChanged;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            if (background == null) background = GetComponent<Image>();
            if (background == null || knob == null)
                Debug.LogError($"[ToggleSwitch] '{name}' thiếu Background (Image) hoặc Knob. Kéo Image nền và núm vào.", this);
        }

        /// Đặt trạng thái từ code (vd. mở popup đọc từ save). <paramref name="animate"/> false → đặt ngay, không trượt —
        /// mở popup mà thấy núm trượt là trông như vừa bị bấm.
        public void SetIsOn(bool isOn, bool animate)
        {
            IsOn = isOn;
            Render(animate);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Interactable || !isActiveAndEnabled) return;
            IsOn = !IsOn;
            Render(true);
            ValueChanged?.Invoke(this, IsOn);
        }

        private void Render(bool animate)
        {
            if (background == null || knob == null) return;
            Sprite sprite = IsOn ? onSprite : offSprite;
            if (sprite != null) background.sprite = sprite;

            if (rectTransform == null) rectTransform = (RectTransform)transform;
            // Núm neo giữa nền: lệch sang phải / trái nửa phần thừa của nền so với núm (trừ khoảng hở).
            float travel = Mathf.Max(0f, (rectTransform.rect.width - knob.rect.width) * 0.5f - knobPadding);
            Vector2 target = new Vector2(IsOn ? travel : -travel, knob.anchoredPosition.y);

            if (slideTween != null && slideTween.IsActive()) slideTween.Kill();
            if (!animate || slideDuration <= 0f)
            {
                knob.anchoredPosition = target;
                return;
            }
            // ⚠️ Không dùng DOAnchorPos: nằm trong DOTween Modules (biên dịch vào firstpass), asmdef không thấy.
            slideTween = DOTween.To(() => knob.anchoredPosition, p => knob.anchoredPosition = p, target, slideDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .SetTarget(this);
        }
    }
}
