using TMPro;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Biển đếm số khách còn phải chở ("300 Left"). Gắn trên ENVIRONMENT/CountSign.
    public class PassengerCountView : MonoBehaviour
    {
        [Tooltip("Chữ số trên biển (CountSign/PassengerCountTxt).")]
        [SerializeField] private TMP_Text countText;

        private int shownCount = -1;

        public bool Init()
        {
            shownCount = -1;
            if (countText != null) return true;
            Debug.LogError("[BusOut] PassengerCountView thiếu Count Text. Kéo CountSign/PassengerCountTxt vào.", this);
            return false;
        }

        /// Chỉ ghi chữ khi số đổi: gọi mỗi frame cũng không tạo chuỗi mới.
        public void SetCount(int count)
        {
            if (count == shownCount) return;
            shownCount = count;
            countText.SetText("{0}", count);
        }
    }
}
