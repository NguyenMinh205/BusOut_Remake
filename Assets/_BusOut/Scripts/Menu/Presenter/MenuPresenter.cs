using UnityEngine;

namespace BusOut.Menu
{
    /// Màn Menu (HomeContext). MonoBehaviour duy nhất của tầng Presenter phía Menu; giữ tham chiếu scene và tạo các
    /// presenter con: MenuNavigationPresenter (trang + thanh tab). Bản đồ level, nút Play: các mốc sau.
    public class MenuPresenter : MonoBehaviour
    {
        [SerializeField] private MenuConfig config;

        [Header("Trang và thanh tab")]
        [Tooltip("MenuPageView trên UI/Canvas/SafeArea/PanelContent/ViewPort.")]
        [SerializeField] private MenuPageView pageView;
        [Tooltip("NavigationTabBarView trên UI/Canvas/SafeArea/NavigationTab/TabArea.")]
        [SerializeField] private NavigationTabBarView tabBar;

        private MenuNavigationPresenter navigation;

        private void Start()
        {
            if (config == null)
            {
                Debug.LogError("[BusOut] MenuPresenter thiếu Menu Config: đang chạy số mặc định trong code. " +
                               "Kéo Assets/_BusOut/Resources/MenuConfig.asset vào (chưa có thì Create → BusOut → Menu Config).", this);
                config = MenuConfig.CreateDefault();
            }
            navigation = CreateNavigation();
        }

        /// Thiếu trang hoặc thanh tab thì chỉ mất chuyển trang, Menu vẫn hiện trang đang bật.
        private MenuNavigationPresenter CreateNavigation()
        {
            if (pageView == null || tabBar == null)
            {
                Debug.LogError("[BusOut] MenuPresenter thiếu Page View hoặc Tab Bar: không chuyển trang được. " +
                               "Kéo ViewPort (có MenuPageView) và NavigationTab/TabArea (có NavigationTabBarView) vào.", this);
                return null;
            }
            if (!pageView.Init() || !tabBar.Init()) return null;
            return new MenuNavigationPresenter(config, pageView, tabBar);
        }
    }
}
