using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Core
{
    /// Một trang của màn Loading chuyển cảnh: tranh + tên + mô tả (hiện là các booster — GameplayPresenter đổ vào từ
    /// GameplayConfig.boosters, vì Core không đọc được config của Gameplay).
    public readonly struct LoadingTip
    {
        public readonly Sprite Image;
        public readonly string Title;
        public readonly string Description;

        public LoadingTip(Sprite image, string title, string description)
        {
            Image = image;
            Title = title;
            Description = description;
        }
    }

    /// Màn che lúc chuyển Menu ↔ màn chơi: một trang Loading (chọn ngẫu nhiên mỗi lần) hiện dần, che kín, rồi mờ đi.
    /// GameFlowController điều khiển (FadeIn → đổi context → FadeOut). Lúc ẩn thì tắt hẳn object (không tốn vẽ).
    /// Gắn trên TransitionPanel trong SharedUI (con cuối → nằm trên mọi popup). Image phải che kín màn hình
    /// (AspectRatioFitter Envelope Parent) và bật Raycast Target: đang che thì chặn mọi cú chạm.
    public class ScreenTransitionView : MonoBehaviour
    {
        [Tooltip("CanvasGroup trên TransitionPanel (mờ dần cả panel). Trống thì lấy trên chính object này.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Image hiện tranh Loading. Chưa có trang nào (SetTips) thì giữ sprite đặt sẵn trong scene.")]
        [SerializeField] private Image image;
        [Tooltip("Tên booster trên màn Loading.")]
        [SerializeField] private TMP_Text titleText;
        [Tooltip("Mô tả booster trên màn Loading.")]
        [SerializeField] private TMP_Text descriptionText;

        private readonly List<LoadingTip> tips = new List<LoadingTip>();
        private Tween fadeTween;

        /// False = thiếu CanvasGroup: không dùng được màn che (GameFlowController đổi cảnh tức thì).
        public bool Init()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                Debug.LogError("[BusOut] ScreenTransitionView thiếu CanvasGroup: chuyển Menu ↔ màn chơi tức thì, không có màn " +
                               "Loading. Thêm CanvasGroup vào TransitionPanel.", this);
                return false;
            }
            if (image == null || titleText == null || descriptionText == null)
                Debug.LogError("[BusOut] ScreenTransitionView thiếu Image / Title Text / Description Text: màn Loading thiếu " +
                               "phần đó. Kéo Image và 2 chữ TMP của TransitionPanel vào.", this);
            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            return true;
        }

        /// Các trang Loading chọn ngẫu nhiên mỗi lần chuyển cảnh. Gọi lại thì thay cả danh sách.
        public void SetTips(IReadOnlyList<LoadingTip> loadingTips)
        {
            tips.Clear();
            if (loadingTips == null) return;
            for (int i = 0; i < loadingTips.Count; i++) tips.Add(loadingTips[i]);
        }

        /// Chọn trang, hiện dần tới che kín.
        public Tween FadeIn(float duration)
        {
            ShowRandomTip();
            gameObject.SetActive(true);
            canvasGroup.blocksRaycasts = true;
            return FadeTo(1f, duration);
        }

        /// Mờ đi; xong thì tắt object.
        public Tween FadeOut(float duration)
        {
            return FadeTo(0f, duration).OnComplete(() => gameObject.SetActive(false));
        }

        private Tween FadeTo(float alpha, float duration)
        {
            fadeTween?.Kill();
            // Thời gian thật: không phụ thuộc Time.timeScale (pause sau này không làm kẹt màn che).
            fadeTween = DOTween.To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, alpha, duration)
                .SetUpdate(true)
                .SetLink(gameObject)
                .SetTarget(this);
            return fadeTween;
        }

        private void ShowRandomTip()
        {
            // Chưa có trang (chạy thẳng GameScene, lần Play đầu: Gameplay chưa khởi tạo): tranh đặt sẵn, không chữ.
            bool hasTip = tips.Count > 0;
            if (titleText != null) titleText.gameObject.SetActive(hasTip);
            if (descriptionText != null) descriptionText.gameObject.SetActive(hasTip);
            if (!hasTip) return;
            LoadingTip tip = tips[Random.Range(0, tips.Count)];
            if (image != null) image.sprite = tip.Image;
            if (titleText != null) titleText.SetText(tip.Title);
            if (descriptionText != null) descriptionText.SetText(tip.Description);
        }
    }
}
