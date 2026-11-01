using System;
using UnityEngine;
using UnityEngine.UI;

namespace BusOut.Core
{
    /// Popup "Leave game?" = thoát hẳn ứng dụng (out game), mở từ nút Leave game trong popup Cài đặt — ở Menu lẫn trong ván.
    /// Yes → thoát, No / nút đóng → ở lại. Dùng chung (SharedUI).
    /// Gắn trên SharedUI/.../LeaveGamePanel. Panel có thể là chính object này (view không cần Update, tắt vẫn gọi được).
    public class LeaveGameView : MonoBehaviour
    {
        [SerializeField] private AnimatedPanel panel;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;
        [SerializeField] private Button closeButton;

        public event Action Confirmed;
        public event Action Cancelled;

        private PanelAnimationSettings panelAnimation;

        public bool Init(PanelAnimationSettings animationSettings)
        {
            panelAnimation = animationSettings;
            string missing = "";
            if (panel == null) missing += " Panel";
            else if (!panel.IsValid(out string panelMissing)) missing += $" Panel:{panelMissing.Trim()}";
            if (yesButton == null) missing += " YesButton";
            if (noButton == null) missing += " NoButton";
            if (closeButton == null) missing += " CloseButton";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] LeaveGameView thiếu tham chiếu:{missing}. Kéo object trong LeaveGamePanel vào.", this);
                return false;
            }
            panel.Init();
            yesButton.onClick.AddListener(HandleYes);
            noButton.onClick.AddListener(HandleCancel);
            closeButton.onClick.AddListener(HandleCancel);
            Hide();
            return true;
        }

        private void OnDestroy()
        {
            if (yesButton != null) yesButton.onClick.RemoveListener(HandleYes);
            if (noButton != null) noButton.onClick.RemoveListener(HandleCancel);
            if (closeButton != null) closeButton.onClick.RemoveListener(HandleCancel);
        }

        public void Show() => panel.Show(panelAnimation);

        public void Hide() => panel.Hide();

        private void HandleYes() => Confirmed?.Invoke();
        private void HandleCancel() => Cancelled?.Invoke();
    }
}
