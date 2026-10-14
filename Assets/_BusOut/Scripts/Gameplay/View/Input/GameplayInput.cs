using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BusOut.Gameplay
{
    /// Nhận chạm trong ván: bắn tia từ camera, báo cho Presenter chạm trúng xe hay ô đỗ.
    public class GameplayInput : MonoBehaviour
    {
        [Tooltip("Camera gameplay (GameContext/Main Camera).")]
        [SerializeField] private Camera worldCamera;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private float rayDistance = 200f;

        public event Action<CarView> CarTapped;
        public event Action<BusStopSlotView> SlotTapped;

        public bool Init(float tapRayDistance)
        {
            rayDistance = tapRayDistance;
            if (worldCamera != null) return true;
            Debug.LogError("[BusOut] GameplayInput thiếu worldCamera. Kéo GameContext/Main Camera vào ô World Camera.", this);
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

            CarView nearestCar = null;
            BusStopSlotView nearestSlot = null;
            float carDistance = float.MaxValue, slotDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hitBuffer[i];
                var car = hit.collider.GetComponentInParent<CarView>();
                if (car != null)
                {
                    if (hit.distance < carDistance) { nearestCar = car; carDistance = hit.distance; }
                    continue;
                }
                var slot = hit.collider.GetComponentInParent<BusStopSlotView>();
                if (slot != null && hit.distance < slotDistance) { nearestSlot = slot; slotDistance = hit.distance; }
            }

            // Xe ưu tiên hơn ô: vùng chạm của ô to, nằm chồng lên xe đỗ trong ô hoặc đang chạy qua.
            if (nearestCar != null) CarTapped?.Invoke(nearestCar);
            else if (nearestSlot != null) SlotTapped?.Invoke(nearestSlot);
        }
    }
}
