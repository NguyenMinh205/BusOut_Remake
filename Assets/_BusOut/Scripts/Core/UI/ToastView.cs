using BusOut.DesignPattern;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BusOut.Core
{
    /// Dòng chữ báo ngắn giữa màn ("Ad not ready", "No internet connection"…) dùng chung Menu + màn chơi.
    /// Ai cần báo thì phát ToastRequestedEvent; báo mới đè báo cũ (không xếp hàng). Không chặn chạm.
    /// Gắn trên object Toast (con của SharedUI, luôn bật) có CanvasGroup; chữ có viền + bóng (chỉnh trong material TMP)
    /// để đọc được trên mọi nền. Số thời gian: UIConfig › Chữ báo.
    [RequireComponent(typeof(CanvasGroup))]
    public class ToastView : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private CanvasGroup canvasGroup;

        private UIConfig config;
        private Sequence sequence;

        private void Reset() => canvasGroup = GetComponent<CanvasGroup>();

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            config = Resources.Load<UIConfig>(UIConfig.ResourcePath);
            if (config == null)
            {
                Debug.LogError($"[BusOut] ToastView không thấy Resources/{UIConfig.ResourcePath}.asset: dùng số mặc định trong code.", this);
                config = UIConfig.CreateDefault();
            }
            if (messageText == null)
                Debug.LogError("[BusOut] ToastView thiếu Message Text: chữ báo không hiện. Kéo TextMeshPro con vào.", this);
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void OnEnable() => EventBus.Subscribe<ToastRequestedEvent>(HandleToastRequested);

        private void OnDisable()
        {
            EventBus.Unsubscribe<ToastRequestedEvent>(HandleToastRequested);
            sequence?.Kill();
            canvasGroup.alpha = 0f;
        }

        private void HandleToastRequested(ToastRequestedEvent e)
        {
            if (messageText == null || string.IsNullOrEmpty(e.Message)) return;
            sequence?.Kill();
            messageText.text = e.Message;
            canvasGroup.alpha = 0f;
            // SetUpdate(true): chạy cả khi timeScale = 0. DOFade nằm trong DOTween Modules (asmdef không thấy) → DOTween.To.
            sequence = DOTween.Sequence()
                .Append(DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, 1f, config.toastFadeInDuration))
                .AppendInterval(config.toastHoldDuration)
                .Append(DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, 0f, config.toastFadeOutDuration))
                .SetUpdate(true)
                .SetLink(gameObject)
                .SetTarget(this);
        }
    }
}
