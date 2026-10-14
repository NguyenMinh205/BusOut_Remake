using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BusOut.Gameplay
{
    /// Nhận chạm: bắn tia từ camera, báo xe trúng tia cho Presenter.
    public class CarInput : MonoBehaviour
    {
        [Tooltip("Camera gameplay (GameContext/Main Camera).")]
        [SerializeField] private Camera worldCamera;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private float rayDistance = 200f;

        public event Action<CarView> CarTapped;

        public bool Init(float tapRayDistance)
        {
            rayDistance = tapRayDistance;
            if (worldCamera != null) return true;
            Debug.LogError("[BusOut] CarInput thiếu worldCamera. Kéo GameContext/Main Camera vào ô World Camera.", this);
            enabled = false;
            return false;
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            // Chạm trúng nút UI (booster, cài đặt…) thì không tính là chạm xe nằm bên dưới.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = worldCamera.ScreenPointToRay(pointer.position.ReadValue());
            int count = Physics.RaycastNonAlloc(ray, hitBuffer, rayDistance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            CarView nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hitBuffer[i].distance >= nearestDistance) continue;
                var car = hitBuffer[i].collider.GetComponentInParent<CarView>();
                if (car == null) continue;
                nearest = car;
                nearestDistance = hitBuffer[i].distance;
            }

            if (nearest != null) CarTapped?.Invoke(nearest);
        }
    }
}
