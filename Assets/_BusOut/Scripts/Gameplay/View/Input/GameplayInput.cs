using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BusOut.Gameplay
{
    public class GameplayInput : MonoBehaviour
    {
        [Tooltip("Camera gameplay (GameContext/Main Camera).")]
        [SerializeField] private Camera worldCamera;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        // Dùng lại mỗi lần chạm: kiểm chạm trúng UI không cấp phát rác.
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>(8);
        private PointerEventData uiPointer;
        private EventSystem uiPointerOwner; // EventSystem đã tạo uiPointer (đổi scene / EventSystem thì tạo lại)
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
            Vector2 screenPosition = pointer.position.ReadValue();
            // Chạm trúng nút UI (booster, cài đặt, popup…) thì không tính là chạm xe nằm bên dưới.
            if (IsOverUI(screenPosition)) return;

            Ray ray = worldCamera.ScreenPointToRay(screenPosition);
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

        /// ⚠️ Không dùng EventSystem.IsPointerOverGameObject(): không truyền pointerId thì nó trả dữ liệu FRAME TRƯỚC —
        /// trên điện thoại frame đầu của một lần chạm chưa biết ngón tay đang trên nút → chạm nút lọt xuống xe.
        /// Raycast UI ngay tại vị trí chạm của frame này thì đúng cho cả chuột lẫn cảm ứng.
        private bool IsOverUI(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            if (uiPointer == null || uiPointerOwner != eventSystem)
            {
                uiPointer = new PointerEventData(eventSystem);
                uiPointerOwner = eventSystem;
            }
            uiPointer.position = screenPosition;
            uiHits.Clear();
            eventSystem.RaycastAll(uiPointer, uiHits);
            return uiHits.Count > 0;
        }
    }
}
