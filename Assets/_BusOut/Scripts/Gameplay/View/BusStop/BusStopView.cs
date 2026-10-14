using UnityEngine;

namespace BusOut.Gameplay
{
    /// Dãy ô đỗ ở bến. Gắn trên SlotParking. Chỉ vẽ theo Model, không giữ luật.
    public class BusStopView : MonoBehaviour
    {
        [Tooltip("Các ô theo thứ tự trái → phải (Slot1..Slot7). Ô đầu danh sách được giữ trước.")]
        [SerializeField] private BusStopSlotView[] slots;

        private int renderedVersion = -1;

        public int SlotCount => slots.Length;

        public BusStopSlotView GetSlot(int index) => slots[index];

        public bool Init()
        {
            if (slots == null || slots.Length == 0)
            {
                Debug.LogError("[BusOut] BusStopView chưa có ô nào. Kéo BusStopSlotView của Slot1..Slot7 vào danh sách Slots.", this);
                return false;
            }
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    Debug.LogError($"[BusOut] BusStopView: phần tử {i} của Slots đang trống. Kéo BusStopSlotView của ô đó vào.", this);
                    return false;
                }
                if (!slots[i].Init(i)) return false;
            }
            return true;
        }

        /// Vẽ lại mọi ô khi Model đổi (so Version: gọi thừa không tốn gì).
        public void Render(BusStop busStop, GameplayConfig config)
        {
            if (busStop.Version == renderedVersion) return;
            renderedVersion = busStop.Version;
            for (int i = 0; i < slots.Length; i++) slots[i].SetVisual(busStop.GetVisual(i), config);
        }
    }
}
