using System;
using System.Collections;
using BusOut.Core;
using BusOut.DesignPattern;
using UnityEngine;

namespace BusOut.Menu
{
    /// Màn Menu (HomeContext). MonoBehaviour duy nhất của tầng Presenter phía Menu; giữ tham chiếu scene và tạo các
    /// presenter con: MenuNavigationPresenter (trang + thanh tab), MenuMapPresenter (bản đồ level + trời, vuốt dọc để xem).
    /// Nút bánh răng → popup Cài đặt dùng chung (SharedPopupsPresenter, ngữ cảnh Menu).
    /// Nút Play → PlayLevelRequestedEvent (GameFlowController chuyển sang màn chơi). Về từ màn chơi (HomeShownEvent)
    /// → dựng lại bản đồ; vừa thắng thì xe chạy sang bệ mới.
    public class MenuPresenter : MonoBehaviour, IPreloadStep
    {
        [SerializeField] private MenuConfig config;

        [Header("Trang và thanh tab")]
        [Tooltip("MenuPageView trên UI/Canvas/SafeArea/PanelContent/ViewPort.")]
        [SerializeField] private MenuPageView pageView;
        [Tooltip("NavigationTabBarView trên UI/Canvas/SafeArea/NavigationTab/TabArea.")]
        [SerializeField] private NavigationTabBarView tabBar;

        [Header("Bản đồ level")]
        [Tooltip("MenuMapView trên ENVIRONMENT/Map.")]
        [SerializeField] private MenuMapView mapView;
        [Tooltip("Nút \"về level hiện tại\" trên trang Home (có MapReturnButtonView).")]
        [SerializeField] private MapReturnButtonView mapReturnButton;

        [Header("Nút Play")]
        [Tooltip("PlayButtonView trên HomePanel/CenterUI/Buttons/PlayButton.")]
        [SerializeField] private PlayButtonView playButton;

        [Header("Thanh trên + popup dùng chung")]
        [Tooltip("MenuTopBarView trên UI/Canvas/SafeArea/TopUI.")]
        [SerializeField] private MenuTopBarView topBar;
        [Tooltip("SharedPopupsPresenter trên SharedUI: popup Cài đặt dùng chung với màn chơi.")]
        [SerializeField] private SharedPopupsPresenter sharedPopups;

        private MenuNavigationPresenter navigation;
        private MenuMapPresenter map;
        private SecureGameData gameData;
        private bool hasPlayButton;
        private bool hasTopBar;

        private void Start()
        {
            if (config == null)
            {
                Debug.LogError("[BusOut] MenuPresenter thiếu Menu Config: đang chạy số mặc định trong code (không có chủ đề " +
                               "nào nên không có bản đồ). Kéo Assets/_BusOut/Resources/MenuConfig.asset vào.", this);
                config = MenuConfig.CreateDefault();
            }
            gameData = DataManager.Instance.GameData;
            navigation = CreateNavigation();
            hasPlayButton = InitPlayButton();
            InitTopBar();
            map = CreateMap();
            // Lần mở Menu đầu tiên dựng ngay dưới đây; các lần về từ màn chơi thì nghe sự kiện này.
            EventBus.Subscribe<HomeShownEvent>(HandleHomeShown);
            EventBus.Subscribe<ScreenRevealedEvent>(HandleScreenRevealed);
            // Coin đổi ở đâu (nhận thưởng, mua…) thì số trên Menu cũng đúng.
            EventBus.Subscribe<CurrencyUpdatedEvent>(HandleCurrencyUpdated);
            RefreshPlayButton();
            RefreshCoins();
            if (map == null) return;
            map.ShowCurrentLevel();
            // Vuốt dọc đi qua ViewPort (cùng chỗ nhận vuốt ngang đổi trang); thiếu trang thì bản đồ đứng yên.
            if (navigation == null) return;
            pageView.VerticalDragBegan += HandleMapDragBegan;
            pageView.VerticalDragged += map.Drag;
            pageView.VerticalDragEnded += map.EndDrag;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<HomeShownEvent>(HandleHomeShown);
            EventBus.Unsubscribe<ScreenRevealedEvent>(HandleScreenRevealed);
            EventBus.Unsubscribe<CurrencyUpdatedEvent>(HandleCurrencyUpdated);
        }

        /// Màn Loading (GameFlowController): tạo sẵn module bản đồ. Start đã chạy trước đó (HomeContext bật sẵn, màn Loading
        /// đợi một khung sau khi nạp scene).
        public IEnumerator Preload(Action<float> reportProgress)
        {
            if (map == null)
            {
                reportProgress(1f);
                yield break;
            }
            yield return map.PrewarmModules(reportProgress);
        }

        /// Về từ màn chơi: level đang lưu có thể đã tăng (thắng).
        /// ⚠️ Không làm trong OnEnable: lúc đó HomeContext còn đang bật dở, Unity cấm SetParent (gắn module, đặt xe, số
        /// level) — lệnh bị bỏ qua, bản đồ hỏng.
        private void HandleHomeShown(HomeShownEvent _)
        {
            RefreshPlayButton();
            RefreshCoins();
            map?.Refresh();
        }

        /// Màn chuyển cảnh mờ đi: vừa thắng thì giờ mới cho xe chạy sang bệ mới (người chơi thấy). Vào màn chơi thì không có
        /// gì chờ (Refresh chỉ đặt chờ khi về Menu).
        private void HandleScreenRevealed(ScreenRevealedEvent _) => map?.PlayPendingAdvance();

        /// Chỉ quay bản đồ khi đang đứng ở trang có bản đồ (Home): trang Shop / Achievement che bản đồ.
        private void HandleMapDragBegan()
        {
            if (navigation.IsOnPage(config.mapPage)) map.BeginDrag();
        }

        /// Play luôn chơi level đang lưu (bản đồ chỉ để xem), dù đang lăn bản đồ tới đâu.
        private void HandlePlayClicked()
        {
            if (!EventBus<PlayLevelRequestedEvent>.HasListeners)
                Debug.LogError("[BusOut] Không có ai chuyển sang màn chơi (thiếu GameFlowController): nút Play không làm gì. " +
                               "Tạo object GameFlow ở gốc scene, gắn GameFlowController, kéo HomeContext + GameContext vào.", this);
            EventBus.Publish(new PlayLevelRequestedEvent());
        }

        private void HandleCurrencyUpdated(CurrencyUpdatedEvent e)
        {
            if (hasTopBar && e.Type == CurrencyType.SoftCurrency) topBar.SetCoins(e.NewTotalAmount);
        }

        private void RefreshCoins()
        {
            if (hasTopBar) topBar.SetCoins(gameData.coinCount);
        }

        private void RefreshPlayButton()
        {
            if (hasPlayButton) playButton.SetLevel(config.playButtonLevelFormat, Mathf.Max(1, gameData.currentLevel));
        }

        /// Mở Cài đặt ở Menu: không có Continue / Home / Leave game (chỉ có trong ván).
        private void HandleSettingsClicked() => sharedPopups.OpenSettings(PopupContext.Menu);

        /// Thiếu thanh trên hoặc SharedUI thì chỉ mất nút cài đặt ở Menu.
        private void InitTopBar()
        {
            if (topBar == null || sharedPopups == null)
            {
                Debug.LogError("[BusOut] MenuPresenter thiếu Top Bar hoặc Shared Popups: nút cài đặt ở Menu không làm gì. " +
                               "Kéo TopUI (có MenuTopBarView) và SharedUI (có SharedPopupsPresenter) vào.", this);
                return;
            }
            hasTopBar = topBar.Init();
            if (hasTopBar) topBar.SettingsClicked += HandleSettingsClicked;
        }

        /// Thiếu nút Play thì không vào được ván — báo to, Menu vẫn chạy.
        private bool InitPlayButton()
        {
            if (playButton == null)
            {
                Debug.LogError("[BusOut] MenuPresenter thiếu Play Button: không vào được ván. " +
                               "Kéo HomePanel/CenterUI/Buttons/PlayButton (có PlayButtonView) vào.", this);
                return false;
            }
            if (!playButton.Init()) return false;
            playButton.Clicked += HandlePlayClicked;
            return true;
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

        /// Thiếu bản đồ thì Menu vẫn dùng được (trang, tab), chỉ không có đường + xe phía sau.
        private MenuMapPresenter CreateMap()
        {
            if (mapView == null)
            {
                Debug.LogError("[BusOut] MenuPresenter thiếu Map View: không có bản đồ level. Kéo ENVIRONMENT/Map (có MenuMapView) vào.", this);
                return null;
            }
            if (mapReturnButton == null)
                Debug.LogError("[BusOut] MenuPresenter thiếu Map Return Button: lăn bản đồ đi xa thì phải tự lăn về. " +
                               "Kéo nút \"về level hiện tại\" (có MapReturnButtonView) vào.", this);
            return MenuMapPresenter.Create(config, mapView, mapReturnButton, gameData);
        }
    }
}
