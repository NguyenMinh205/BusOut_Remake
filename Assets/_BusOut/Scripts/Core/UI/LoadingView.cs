using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Core
{
    /// Màn Loading: thanh tiến trình + mờ dần khi xong. Chỉ hiển thị; chạy tới đâu do GameBootstrap quyết.
    /// Gắn trên Canvas GỐC của LoadingScene (phải là object gốc: GameBootstrap giữ nó qua lần đổi scene).
    /// Canvas nên có Sort Order cao (vd. 100) để che GameScene đang dựng phía dưới; nền có Raycast Target để chặn chạm.
    public class LoadingView : MonoBehaviour
    {
        [Tooltip("Thanh tiến trình (LoadingBar). Code đặt min 0, max 1 và tắt kéo tay.")]
        [SerializeField] private Slider progressBar;
        [Tooltip("CanvasGroup trên chính Canvas này: để mờ dần cả màn.")]
        [SerializeField] private CanvasGroup canvasGroup;

        public bool Init()
        {
            string missing = "";
            if (progressBar == null) missing += " ProgressBar";
            if (canvasGroup == null) missing += " CanvasGroup";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] LoadingView thiếu tham chiếu:{missing}. Kéo LoadingBar và CanvasGroup của Canvas vào.", this);
                return false;
            }
            // Thanh chỉ để xem: Slider mặc định kéo được bằng tay / núm Handle.
            // ⚠️ Bỏ Transition TRƯỚC khi tắt interactable: Slider tắt thì Color Tint phủ Disabled Color (xám, alpha ~0.5) lên
            // Target Graphic (núm Handle) → icon trên núm bị mờ.
            progressBar.transition = Selectable.Transition.None;
            progressBar.interactable = false;
            progressBar.minValue = 0f;
            progressBar.maxValue = 1f;
            progressBar.value = 0f;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            return true;
        }

        public void SetProgress(float progress) => progressBar.value = progress;

        /// ⚠️ Không dùng DOFade: nằm trong DOTween Modules (biên dịch vào firstpass), asmdef không thấy.
        public Tween FadeOut(float duration) =>
            DOTween.To(() => canvasGroup.alpha, alpha => canvasGroup.alpha = alpha, 0f, duration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .SetTarget(this);
    }
}
