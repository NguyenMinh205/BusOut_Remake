using UnityEngine;

namespace BusOut.Menu
{
    /// Chuyển trang Menu: chạm tab hoặc vuốt ngang → trang đích; thanh tab vẽ theo vị trí trang.
    /// MenuPresenter tạo.
    public sealed class MenuNavigationPresenter
    {
        private readonly MenuConfig config;
        private readonly MenuPageView pageView;
        private readonly NavigationTabBarView tabBar;

        public MenuNavigationPresenter(MenuConfig config, MenuPageView pageView, NavigationTabBarView tabBar)
        {
            this.config = config;
            this.pageView = pageView;
            this.tabBar = tabBar;
            if (pageView.PageCount != tabBar.TabCount)
                Debug.LogError($"[BusOut] Menu có {pageView.PageCount} trang nhưng {tabBar.TabCount} tab: chạm tab sẽ ra sai trang. " +
                               "Kéo cùng số trang (MenuPageView) và tab (NavigationTabBarView), cùng thứ tự.", pageView);
            pageView.ProgressChanged += HandleProgressChanged;
            pageView.SwipeReleased += HandleSwipeReleased;
            tabBar.TabClicked += HandleTabClicked;
            pageView.ScrollTo(ClampPage(config.startPage), 0f);
        }

        /// Đang đứng ở trang <paramref name="page"/> (không ở giữa hai trang).
        public bool IsOnPage(int page) => Mathf.Abs(pageView.Progress - page) < 0.01f;

        private void HandleProgressChanged(float progress) => tabBar.Render(progress, config.selectedTabY);

        private void HandleTabClicked(int tab) => pageView.ScrollTo(ClampPage(tab), config.pageSnapDuration);

        /// Thả tay: về trang gần nhất; vuốt nhanh mà chưa qua nửa trang thì vẫn sang trang kế theo hướng vuốt (như gốc).
        private void HandleSwipeReleased(PageSwipe swipe)
        {
            int startPage = Mathf.RoundToInt(swipe.StartProgress);
            int target = Mathf.RoundToInt(pageView.Progress);
            bool isFlick = swipe.Duration <= config.fastSwipeMaxTime && Mathf.Abs(swipe.Distance) >= config.fastSwipeMinDistance;
            if (target == startPage && isFlick) target += swipe.Distance < 0f ? 1 : -1;
            pageView.ScrollTo(ClampPage(target), config.pageSnapDuration);
        }

        private int ClampPage(int page) => Mathf.Clamp(page, 0, pageView.PageCount - 1);
    }
}
