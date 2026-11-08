using System;
using System.Collections.Generic;
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

        [Header("Bản đồ level")]
        [Tooltip("Mỗi chủ đề kéo dài bấy nhiêu level; mỗi chủ đề phải có đúng chừng ấy module (mảnh cổng + mảnh thường).")]
        public int levelsPerTheme = 5;

        [Tooltip("Chủ đề theo thứ tự xuất hiện; hết danh sách thì quay lại chủ đề đầu.")]
        public List<MenuTheme> themes = new List<MenuTheme>();

        [Tooltip("Màn Loading tạo sẵn mỗi prefab module một bản (lăn bản đồ sang chủ đề mới không khựng): mỗi khung tạo tối đa " +
                 "bấy nhiêu module, để thanh tiến trình vẫn chạy.")]
        [Min(1)] public int preloadModulesPerFrame = 5;

        [Header("Vuốt bản đồ (chỉ để xem, như gốc: Play luôn chơi level hiện tại)")]
        [Tooltip("Trang có bản đồ phía sau (Home). Vuốt dọc ở trang khác không quay bản đồ.")]
        public int mapPage = 1;

        [Tooltip("Kéo dọc hết một chiều cao màn hình thì bản đồ đi bấy nhiêu level.")]
        public float mapLevelsPerScreenHeight = 3f;

        [Tooltip("Xem trước được tới level hiện tại + bấy nhiêu (level chưa chơi); lùi lại thì xem được tới level 1.")]
        public int mapViewAheadLevels = 50;

        [Tooltip("Vuốt mạnh rồi thả: bản đồ trôi thêm (tốc độ lúc thả, level/giây) × số này, rồi dừng ở level gần nhất. " +
                 "0 = không trôi, chỉ hít về level gần nhất.")]
        public float mapFlingTime = 0.3f;

        [Tooltip("Ngón tay đứng yên lâu hơn bấy nhiêu giây rồi mới thả thì coi như không vuốt (không trôi thêm).")]
        public float mapReleaseIdleTime = 0.1f;

        [Tooltip("Thời gian hít về level đích: phần cố định (giây)...")]
        public float mapSnapDuration = 0.3f;

        [Tooltip("... cộng thêm mỗi level phải đi (giây / level)...")]
        public float mapSnapDurationPerLevel = 0.08f;

        [Tooltip("... nhưng không quá bấy nhiêu giây.")]
        public float mapSnapMaxDuration = 1.2f;

        [Tooltip("Nút \"về level hiện tại\" hiện khi xe (ở bệ level hiện tại) ra khỏi vùng màn hình từ mép dưới này (0 = đáy " +
                 "màn, 1 = đỉnh)... Nâng lên (vd. 0.19) nếu thanh tab che xe thì cũng tính là khuất.")]
        public float mapCarVisibleViewportBottom = 0f;

        [Tooltip("... tới mép trên này (1 = đỉnh màn; hạ xuống nếu thanh trên che xe).")]
        public float mapCarVisibleViewportTop = 1f;

        [Header("Thắng về Menu: xe chạy sang bệ level mới")]
        [Tooltip("Đứng yên ở bệ vừa qua bấy nhiêu giây rồi mới chạy (lúc chạy: bệ bật lớp \"đã qua\" + pháo sáng). Gốc: 0.5.")]
        public float mapAdvanceDelay = 0.5f;

        [Tooltip("Xe chạy dọc mặt đường sang bệ mới, bản đồ lăn CÙNG LÚC, trong bấy nhiêu giây (chậm–nhanh–chậm). Gốc: 1.4.")]
        public float mapAdvanceDuration = 1.4f;

        [Tooltip("Lúc giữa đường xe dẫn trước khung nhìn tối đa bấy nhiêu level (0 = xe đứng yên giữa màn như gốc, đường lăn " +
                 "bên dưới). Đầu / cuối đường xe luôn ở giữa màn. Nhìn ra xe đang lăn bánh chứ không trượt.")]
        public float mapCarLeadLevels = 0.25f;

        [Tooltip("Lấy đà: xe lùi bấy nhiêu level trước khi chạy (0 = không lùi).")]
        public float mapCarBackUpLevels = 0.03f;

        [Tooltip("Thời gian lùi lấy đà (giây).")]
        public float mapCarBackUpDuration = 0.12f;

        // Dáng xe khi chạy (phóng to, chúi, nhún, nảy) + pháo sáng bệ + vòng dưới gầm: chuyển sang FxConfig (4/11/2026).

        [Header("Nút Play")]
        [Tooltip("Chữ trên nút Play; {0} = level đang lưu.")]
        public string playButtonLevelFormat = "LEVEL {0}";

        [Header("Remove Ads (mua giả — không có IAP thật)")]
        public string removeAdsTitle = "REMOVE ADS";
        [Tooltip("Icon trong popup mua: Art/UI/Sprites/RemoveAds.")]
        public Sprite removeAdsIcon;
        public string removeAdsDescription = "No more banner and interstitial ads!";
        [Tooltip("Chữ giá trên nút mua (chỉ để hiển thị: mua giả, không trừ tiền).")]
        public string removeAdsPriceText = "$3.99";

        public static MenuConfig CreateDefault() => CreateInstance<MenuConfig>();
    }

    /// Một chủ đề (địa điểm) trên bản đồ: các module đường + trời + mây.
    [Serializable]
    public class MenuTheme
    {
        public string name;

        [Tooltip("Prefab module theo thứ tự level trong khối: mảnh cổng Module<X>Start trước, rồi Module<X>1..4.")]
        public GameObject[] modules;

        [Tooltip("Tranh trời (_SkyTex của shader BusOut/Skybox).")]
        public Texture2D sky;

        [Tooltip("Dải mây trôi (_CloudTex).")]
        public Texture2D cloud;
    }
}
