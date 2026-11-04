using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BusOut.Core
{
    /// Nút nhún khi bấm (gốc: AnimatedButton): nhấn → thu nhỏ còn pressScale, thả → về cỡ lúc nhấn.
    /// Tự gắn cho MỌI Button của scene lúc nạp (AttachAll, GameFlowController gọi — cùng chỗ gắn tiếng bấm), khỏi gắn tay.
    /// ⚠️ Nhún TƯƠNG ĐỐI theo cỡ đang có lúc nhấn, không ép về 1: nút Crane đang "giương" phóng 1.15 (BoosterButtonView),
    /// nút trong popup đang phóng dần lúc hiện (AnimatedPanel) — ép về 1 là phá các trạng thái đó.
    [DisallowMultipleComponent]
    public sealed class ButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Selectable selectable;
        private float pressScale;
        private float duration;
        private bool isPressed;
        private Vector3 restScale;
        private Tween tween;

        /// Gắn cho mọi Button trong <paramref name="scene"/> (kể cả đang tắt). Trả về số nút đã gắn.
        /// Nút sinh ra SAU lúc này (Instantiate) không có — gắn tay hoặc gọi lại với object đó.
        public static int AttachAll(Scene scene, float pressScale, float duration)
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                {
                    if (!button.TryGetComponent(out ButtonPressFeedback feedback))
                        feedback = button.gameObject.AddComponent<ButtonPressFeedback>();
                    feedback.Setup(button, pressScale, duration);
                    count++;
                }
            }
            return count;
        }

        private void Setup(Selectable target, float scale, float time)
        {
            selectable = target;
            pressScale = scale;
            duration = Mathf.Max(0f, time);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Nút đang khoá (vd. không đủ coin hồi sinh) thì không nhún: nhún mà không có tác dụng là đánh lừa người chơi.
            if (isPressed || selectable == null || !selectable.IsInteractable()) return;
            isPressed = true;
            restScale = transform.localScale;
            ScaleTo(restScale * pressScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isPressed) return;
            isPressed = false;
            ScaleTo(restScale);
        }

        /// Nút bị tắt khi đang nhấn (đóng popup ngay lúc bấm): trả cỡ ngay, không để nút kẹt ở cỡ nhỏ lần mở sau.
        private void OnDisable()
        {
            if (!isPressed) return;
            isPressed = false;
            KillTween();
            transform.localScale = restScale;
        }

        private void ScaleTo(Vector3 target)
        {
            KillTween();
            tween = transform.DOScale(target, duration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .SetTarget(this);
        }

        private void KillTween()
        {
            if (tween != null && tween.IsActive()) tween.Kill();
            tween = null;
        }
    }
}
