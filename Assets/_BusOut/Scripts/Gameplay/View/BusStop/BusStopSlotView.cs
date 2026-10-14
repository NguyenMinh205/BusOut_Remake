using DG.Tweening;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Hình và các điểm của một ô đỗ. Gắn trên mỗi SlotN trong SlotParking.
    public class BusStopSlotView : MonoBehaviour
    {
        [Header("Hình")]
        [Tooltip("Nền ô thường (Slot): bật khi ô mở, tắt khi ô khoá (thay bằng AdsSlot).")]
        [SerializeField] private GameObject normalObject;
        [Tooltip("Sprite viền đỏ cảnh báo (WarningSlot): bật khi đây là ô trống cuối cùng.")]
        [SerializeField] private SpriteRenderer warningSprite;
        [Tooltip("Hình ô khoá xem quảng cáo (AdsSlot): bật khi ô đang khoá, thay cho nền Slot.")]
        [SerializeField] private GameObject adsObject;

        [Header("Điểm")]
        [Tooltip("Miệng ô (DataPoint/EntryPoint): xe từ làn trên đi tới đây trước.")]
        [SerializeField] private Transform entryPoint;
        [Tooltip("Điểm dừng giữa ô (DataPoint/ParkPoint). Hướng EntryPoint → ParkPoint là hướng đầu xe khi đỗ.")]
        [SerializeField] private Transform parkPoint;

        [Header("Chạm")]
        [Tooltip("Collider để chạm vào ô (DataPoint/TapCollider): chỉ bật khi ô khoá, chạm để mở.")]
        [SerializeField] private Collider tapCollider;

        private SlotVisual currentVisual;
        private bool hasVisual;
        private Tween blinkTween;

        /// Vị trí trong BusStopView (0 = ô trái nhất). BusStopView gán lúc Init.
        public int Index { get; private set; } = -1;

        public Vector3 EntryPoint => entryPoint.position;
        public Vector3 ParkPoint => parkPoint.position;

        public bool Init(int index)
        {
            Index = index;
            if (normalObject != null && warningSprite != null && adsObject != null && entryPoint != null && parkPoint != null &&
                tapCollider != null)
                return true;
            Debug.LogError($"[BusOut] BusStopSlotView '{name}' thiếu tham chiếu. Kéo Slot, WarningSlot (SpriteRenderer), AdsSlot, " +
                           "DataPoint/EntryPoint, DataPoint/ParkPoint, DataPoint/TapCollider của ô này vào.", this);
            return false;
        }

        /// Chỉ làm gì khi hình thật sự đổi: Presenter gọi lại nhiều lần với cùng hình thì nhấp nháy không bị chạy lại.
        public void SetVisual(SlotVisual visual, GameplayConfig config)
        {
            if (hasVisual && visual == currentVisual) return;
            hasVisual = true;
            currentVisual = visual;

            StopBlink();
            bool locked = visual == SlotVisual.Ads;
            // Ô khoá: AdsSlot thay hẳn nền Slot (không chồng 2 hình).
            normalObject.SetActive(!locked);
            adsObject.SetActive(locked);
            // Chỉ ô khoá mới chạm được: ô mở mà vẫn bật collider sẽ nuốt mất cú chạm vào xe đang đỗ trong ô.
            tapCollider.enabled = locked;
            warningSprite.gameObject.SetActive(visual == SlotVisual.Warning);
            if (visual == SlotVisual.Warning) PlayWarningBlink(config);
        }

        /// Cảnh báo nguy hiểm (sắp hết chỗ) — ngoại lệ duy nhất được nhấp nháy: nháy vài lần lúc vừa thành ô cuối,
        /// rồi sáng đứng, không nháy mãi.
        private void PlayWarningBlink(GameplayConfig config)
        {
            int count = Mathf.Max(0, config.warningBlinkCount);
            if (count == 0 || config.warningBlinkDuration <= 0f) return;
            // Mỗi lần nháy = mờ đi + sáng lại; số vòng Yoyo chẵn nên kết thúc ở trạng thái sáng.
            // ⚠️ Không dùng SpriteRenderer.DOFade: nằm trong DOTween Modules (biên dịch vào firstpass), asmdef không thấy.
            blinkTween = DOTween.ToAlpha(() => warningSprite.color, color => warningSprite.color = color,
                    0f, config.warningBlinkDuration * 0.5f)
                .SetLoops(count * 2, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(gameObject)
                .SetTarget(warningSprite)
                .OnKill(RestoreWarningAlpha);
        }

        private void StopBlink()
        {
            if (blinkTween != null && blinkTween.IsActive()) blinkTween.Kill();
            blinkTween = null;
            RestoreWarningAlpha();
        }

        private void RestoreWarningAlpha()
        {
            if (warningSprite == null) return;
            Color color = warningSprite.color;
            color.a = 1f;
            warningSprite.color = color;
        }
    }
}
