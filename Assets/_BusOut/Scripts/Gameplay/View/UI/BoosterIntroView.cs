using System;
using BusOut.Core;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BusOut.Gameplay
{
    /// Màn giới thiệu booster vừa mở khoá, hoặc cơ chế mới gặp lần đầu: tên, hình, mô tả. Chạm bất kỳ đâu để đóng.
    /// Gắn trên object BoosterTut.
    public class BoosterIntroView : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Root = BoosterTut, Dim = BoosterTut/Dim, Reveal Items = tên, hình, mô tả.")]
        [SerializeField] private AnimatedPanel panel;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text descriptionText;

        private PanelAnimationSettings panelAnimation;
        private Action closed;
        private bool canClose;

        public bool Init(PanelAnimationSettings animationSettings)
        {
            panelAnimation = animationSettings;
            string missing = "";
            if (panel == null) missing += " Panel";
            else if (!panel.IsValid(out string panelMissing)) missing += $" Panel:{panelMissing.Trim()}";
            if (nameText == null) missing += " NameText";
            if (iconImage == null) missing += " IconImage";
            if (descriptionText == null) missing += " DescriptionText";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] BoosterIntroView thiếu tham chiếu:{missing}. Kéo các object trong BoosterTut vào. " +
                               "Tạm thời bài hướng dẫn booster bỏ qua màn giới thiệu.", this);
                return false;
            }
            panel.Init();
            Hide();
            return true;
        }

        public void Show(BoosterDefinition booster, Action onClosed) =>
            Show(booster.displayName, booster.icon, booster.description, onClosed);

        public void Show(MechanicIntroDefinition mechanic, Action onClosed) =>
            Show(mechanic.displayName, mechanic.icon, mechanic.description, onClosed);

        private void Show(string title, Sprite icon, string description, Action onClosed)
        {
            nameText.SetText(title);
            descriptionText.SetText(description);
            iconImage.sprite = icon;
            // Cơ chế chưa có hình thì ẩn ô hình; để bật thì Image không sprite hiện thành ô trắng.
            iconImage.enabled = icon != null;
            closed = onClosed;
            canClose = false;
            // Chỉ cho đóng khi đã hiện xong: người chơi đang chạm liên tục lúc vào ván sẽ bỏ qua màn này mà chưa kịp đọc.
            panel.Show(panelAnimation).OnComplete(() => canClose = true);
        }

        public void Hide()
        {
            canClose = false;
            closed = null;
            panel.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!canClose) return;
            Action callback = closed;
            Hide();
            callback?.Invoke();
        }
    }
}
