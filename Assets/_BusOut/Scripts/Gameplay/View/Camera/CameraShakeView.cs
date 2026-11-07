using BusOut.Core;
using BusOut.DesignPattern;
using DG.Tweening;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Rung camera gameplay khi có CameraShakeEvent (xe đâm xe chặn — CarFlowPresenter phát, cường độ trong FxConfig).
    /// Gắn trên GameContext/Main Camera (cùng CameraFitView). Rung quanh vị trí đặt trong scene rồi trả về đúng chỗ.
    public class CameraShakeView : MonoBehaviour
    {
        private Vector3 restLocalPosition;
        private Tween shakeTween;
        private TweenCallback restorePosition;

        private void Awake()
        {
            restLocalPosition = transform.localPosition;
            restorePosition = RestorePosition;
        }

        private void OnEnable() => EventBus.Subscribe<CameraShakeEvent>(HandleShake);

        private void OnDisable()
        {
            EventBus.Unsubscribe<CameraShakeEvent>(HandleShake);
            // Tắt GameContext giữa lúc rung (về Menu): trả camera về chỗ, không thì lần vào sau camera lệch.
            if (shakeTween != null && shakeTween.IsActive()) shakeTween.Kill();
            RestorePosition();
        }

        private void HandleShake(CameraShakeEvent e)
        {
            if (e.Intensity <= 0f || e.Duration <= 0f) return;
            // ⚠️ Rung mới đè rung cũ: dừng + trả về chỗ trước, không thì lấy chỗ đang lệch làm gốc → camera trôi dần.
            if (shakeTween != null && shakeTween.IsActive()) shakeTween.Kill();
            transform.localPosition = restLocalPosition;
            shakeTween = transform.DOShakePosition(e.Duration, e.Intensity, FxConfig.Instance.cameraShakeVibrato, 90f, false, true)
                .SetLink(gameObject)
                .SetTarget(transform)
                .OnKill(restorePosition);
        }

        private void RestorePosition() => transform.localPosition = restLocalPosition;
    }
}
