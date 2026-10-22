using DG.Tweening;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Chữ "Hết chỗ!" khi chạm xe mà bến hết ô: hiện đứng yên một lúc rồi mờ dần. Gắn trên object chữ (có CanvasGroup).
    public class NoSlotMessageView : MonoBehaviour
    {
        [Tooltip("CanvasGroup trên chính object chữ: điều khiển độ mờ, không chặn chạm.")]
        [SerializeField] private CanvasGroup canvasGroup;

        private Sequence showSequence;

        public bool Init()
        {
            if (canvasGroup == null)
            {
                Debug.LogError("[BusOut] NoSlotMessageView thiếu Canvas Group. Thêm CanvasGroup vào object chữ rồi kéo vào.", this);
                return false;
            }
            // Không chặn chạm: chữ nằm đè lên bãi xe, người chơi vẫn phải chạm được xe bên dưới.
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            canvasGroup.alpha = 0f;
            return true;
        }

        /// Chạm liên tiếp thì hiện lại từ đầu (không chồng tween).
        public void Show(float holdDuration, float fadeDuration)
        {
            showSequence?.Kill();
            canvasGroup.alpha = 1f;
            // ⚠️ Không dùng CanvasGroup.DOFade: nằm trong DOTween Modules (biên dịch vào firstpass), asmdef không thấy.
            showSequence = DOTween.Sequence()
                .AppendInterval(holdDuration)
                .Append(DOTween.To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, 0f, fadeDuration))
                .SetLink(gameObject)
                .SetTarget(this);
        }

        public void Hide()
        {
            showSequence?.Kill();
            canvasGroup.alpha = 0f;
        }
    }
}
