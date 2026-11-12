using BusOut.Core;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Gameplay
{
    /// Bàn tay hướng dẫn chỉ vào một xe hoặc một nút và nhấn lặp. Gắn trên một object trong Canvas gameplay; bàn tay là Image con.
    public class TutorialHandView : MonoBehaviour
    {
        [Tooltip("RectTransform của Image bàn tay. Đặt Pivot của RectTransform đúng đầu ngón tay: đó là điểm chạm vào xe.")]
        [SerializeField] private RectTransform hand;
        [Tooltip("Camera gameplay (GameContext/Main Camera): đổi vị trí xe sang vị trí trên màn hình.")]
        [SerializeField] private Camera worldCamera;

        private Canvas canvas;
        private Vector3 baseScale;
        private Tween pressTween;

        public bool Init()
        {
            if (hand == null || worldCamera == null)
            {
                Debug.LogError("[BusOut] TutorialHandView thiếu tham chiếu: kéo Image bàn tay vào ô Hand và GameContext/Main Camera vào ô World Camera. " +
                               "Tạm thời ván sẽ không có hướng dẫn.", this);
                return false;
            }
            // ⚠️ Phải có includeInactive: Init chạy trong màn Loading lúc GameContext còn tắt, không có thì không tìm thấy Canvas.
            canvas = GetComponentInParent<Canvas>(true);
            if (canvas == null)
            {
                Debug.LogError("[BusOut] TutorialHandView phải nằm trong Canvas gameplay. Tạm thời ván sẽ không có hướng dẫn.", this);
                return false;
            }
            // ⚠️ Chỉ tắt raycast của bàn tay: nó nằm đúng trên thứ cần chạm, còn nhận raycast thì bị coi là chạm UI. Đừng mở rộng ra
            // cả object này: BoosterTut cũng là con ở đây và cần raycast để chạm đóng màn giới thiệu.
            foreach (Graphic graphic in hand.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            baseScale = hand.localScale;
            Hide();
            return true;
        }

        public void ShowAt(Vector3 worldPosition) => Show(worldCamera.WorldToScreenPoint(worldPosition));

        public void ShowAtButton(RectTransform button) => Show(RectTransformUtility.WorldToScreenPoint(UiCamera, button.position));

        public void Hide()
        {
            KillPress();
            hand.gameObject.SetActive(false);
        }

        private Camera UiCamera => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        private void Show(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)hand.parent, screenPoint, UiCamera, out Vector2 localPoint);
            hand.localPosition = localPoint;
            hand.gameObject.SetActive(true);

            KillPress();
            hand.localScale = baseScale;
            FxConfig fx = FxConfig.Instance;
            pressTween = hand.DOScale(baseScale * fx.tutorialHandPressScale, fx.tutorialHandPressDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(hand.gameObject)
                .SetTarget(hand);
        }

        private void KillPress()
        {
            if (pressTween != null && pressTween.IsActive()) pressTween.Kill();
            pressTween = null;
        }
    }
}
