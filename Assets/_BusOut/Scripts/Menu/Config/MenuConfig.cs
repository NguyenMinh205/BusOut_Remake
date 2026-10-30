using UnityEngine;

namespace BusOut.Menu
{
    [CreateAssetMenu(menuName = "BusOut/Menu Config", fileName = "MenuConfig")]
    public class MenuConfig : ScriptableObject
    {
        [Header("Trang và thanh tab")]
        [Tooltip("Trang mở ra khi vào Menu, tính từ 0 theo thứ tự trang trong MenuPageView (0 Shop, 1 Home, 2 Achievement). " +
                 "Gốc: Home.")]
        public int startPage = 1;

        [Tooltip("Thời gian trượt tới trang đích sau khi thả tay hoặc chạm tab (giây).")]
        public float pageSnapDuration = 0.3f;

        [Tooltip("Vuốt nhanh: thả tay trong vòng bấy nhiêu giây (và kéo đủ xa, dòng dưới) thì sang trang kế dù chưa kéo " +
                 "quá nửa trang. Gốc: 0.5.")]
        public float fastSwipeMaxTime = 0.5f;

        [Tooltip("Vuốt nhanh: kéo ngang tối thiểu bao nhiêu (đơn vị UI, màn rộng 1170) mới tính. Gốc: 50.")]
        public float fastSwipeMinDistance = 50f;

        [Tooltip("Tab đang chọn tiến lên tới y này (anchoredPosition.y). Tab khác giữ y dựng trong scene.")]
        public float selectedTabY = 0f;

        public static MenuConfig CreateDefault() => CreateInstance<MenuConfig>();
    }
}
