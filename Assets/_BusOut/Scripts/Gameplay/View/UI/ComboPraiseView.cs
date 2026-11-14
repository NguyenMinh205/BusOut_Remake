using BusOut.Core;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Gameplay
{
    /// Chữ khen combo (GOOD!, NICE!…): nảy lên, đứng yên, vừa trôi lên vừa mờ. Một lần, không lặp.
    /// Gắn trên object ComboPraise (Image + CanvasGroup) trong canvas màn chơi.
    public class ComboPraiseView : MonoBehaviour
    {
        [Tooltip("Image trên chính object: sprite đổi theo mốc combo (Preserve Aspect bật để chữ ngắn / dài đều không méo).")]
        [SerializeField] private Image image;
        [Tooltip("CanvasGroup trên chính object: điều khiển độ mờ, không chặn chạm.")]
        [SerializeField] private CanvasGroup canvasGroup;

        private RectTransform rect;
        private Vector2 restPosition;
        private Sequence showSequence;

        public bool Init()
        {
            if (image == null || canvasGroup == null)
            {
                Debug.LogError("[BusOut] ComboPraiseView thiếu Image / Canvas Group. Kéo 2 component trên object ComboPraise vào.", this);
                return false;
            }
            rect = (RectTransform)transform;
            restPosition = rect.anchoredPosition;
            // Không chặn chạm: chữ nằm đè lên bãi xe, người chơi vẫn chạm xe bên dưới được.
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            image.raycastTarget = false;
            image.preserveAspect = true;
            // ⚠️ Ẩn bằng độ mờ, object luôn bật (như NoSlotMessageView): tắt object thì tween không chạy.
            gameObject.SetActive(true);
            Hide();
            return true;
        }

        /// Combo mới thay chữ đang hiện (không chồng tween).
        public void Show(Sprite sprite, FxConfig fx)
        {
            showSequence?.Kill();
            image.sprite = sprite;
            canvasGroup.alpha = 1f;
            rect.anchoredPosition = restPosition;
            rect.localScale = Vector3.one * fx.comboPopFromScale;
            Vector2 floatTarget = restPosition + Vector2.up * fx.comboFloatDistance;
            // ⚠️ DOTween.To thay DOScale / DOFade / DOAnchorPos: các hàm đó nằm trong DOTween Modules, asmdef không thấy.
            showSequence = DOTween.Sequence()
                .Append(DOTween.To(() => rect.localScale.x, v => rect.localScale = Vector3.one * v, 1f, fx.comboPopDuration)
                    .SetEase(Ease.OutBack))
                .AppendInterval(fx.comboHoldDuration)
                .Append(DOTween.To(() => canvasGroup.alpha, v => canvasGroup.alpha = v, 0f, fx.comboFadeDuration))
                .Join(DOTween.To(() => rect.anchoredPosition, v => rect.anchoredPosition = v, floatTarget, fx.comboFadeDuration))
                .SetLink(gameObject)
                .SetTarget(this);
        }

        public void Hide()
        {
            showSequence?.Kill();
            canvasGroup.alpha = 0f;
            rect.anchoredPosition = restPosition;
            rect.localScale = Vector3.one;
        }
    }
}
