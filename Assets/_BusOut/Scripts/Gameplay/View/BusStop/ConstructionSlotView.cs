using TMPro;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Hình ô công trường: biển chắn + số khách còn thiếu để mở ô. Gắn trên root prefab; BusStopSlotView tạo và bật / tắt.
    public class ConstructionSlotView : MonoBehaviour
    {
        [Tooltip("Chữ TextMeshPro (3D) hiện số khách còn phải lên xe để ô mở.")]
        [SerializeField] private TMP_Text countText;

        public void SetCount(int remaining)
        {
            if (countText != null) countText.SetText("{0}", remaining);
        }
    }
}
