using BusOut.DesignPattern;
using UnityEngine;

namespace BusOut.Core
{
    /// Popup dùng chung Menu + màn chơi: Cài đặt, Leave game, Mua. Một bản trong scene (SharedUI, ngoài cả hai context, luôn bật),
    /// Menu và màn chơi mở qua đây với PopupContext riêng (trong ván thì có thêm Continue / Home).
    /// Home (trong ván) → GoHomeRequestedEvent ngay. Leave game (cả hai nơi) → hỏi lại bằng popup Leave game → Yes → thoát
    /// ứng dụng.
    /// Popup mua: ở đây chỉ khởi tạo + đóng khi chuyển màn; bán gì / trao gì do nơi mở quyết (màn chơi: PurchasePresenter —
    /// lượt booster, ô đỗ; sau này Shop ở Menu) — lấy view qua PurchasePopup.
    /// Chuyển Menu ↔ màn chơi thì đóng hết (popup đang mở không được theo sang màn kia).
    /// Gắn trên object SharedUI.
    public class SharedPopupsPresenter : MonoBehaviour
    {
        [SerializeField] private UIConfig config;
        [Tooltip("SettingsView trên SharedUI/.../SettingPanel.")]
        [SerializeField] private SettingsView settingsView;
        [Tooltip("LeaveGameView trên SharedUI/.../LeaveGamePanel: hỏi lại trước khi thoát ứng dụng (nút Leave game).")]
        [SerializeField] private LeaveGameView leaveGameView;
        [Tooltip("PurchasePopupView trên SharedUI/.../PurchasePanel: popup mua chung.")]
        [SerializeField] private PurchasePopupView purchasePopup;

        private SettingsPresenter settings; // null khi thiếu popup cài đặt: nút bánh răng không làm gì
        private bool hasLeaveGamePopup;     // false khi thiếu popup Leave game: nút Leave game không làm gì
        private bool hasPurchasePopup;      // false khi thiếu popup mua: PurchasePopup trả null

        /// Popup mua chung đã khởi tạo; null khi thiếu / sai tham chiếu (đã báo lỗi). Sẵn từ Awake.
        public PurchasePopupView PurchasePopup => hasPurchasePopup ? purchasePopup : null;

        /// Popup mua khởi tạo ở Awake (không cần save): presenter của nơi dùng lấy view trong Start của nó, thứ tự Start
        /// giữa các object không cố định.
        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("[BusOut] SharedPopupsPresenter thiếu UI Config: popup chạy số mặc định trong code. " +
                               "Kéo Assets/_BusOut/Resources/UIConfig.asset vào.", this);
                config = UIConfig.CreateDefault();
            }
            hasPurchasePopup = InitPurchasePopup();
        }

        private void Start()
        {
            settings = CreateSettings();
            hasLeaveGamePopup = InitLeaveGamePopup();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<HomeShownEvent>(HandleHomeShown);
            EventBus.Subscribe<GameplayShownEvent>(HandleGameplayShown);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<HomeShownEvent>(HandleHomeShown);
            EventBus.Unsubscribe<GameplayShownEvent>(HandleGameplayShown);
        }

        /// Mở popup cài đặt. False khi không có popup (thiếu tham chiếu — đã báo lỗi lúc khởi tạo).
        public bool OpenSettings(PopupContext context)
        {
            if (settings == null) return false;
            settings.Open(context);
            return true;
        }

        /// Đóng mọi popup dùng chung (vào level mới, thắng / thua, chuyển màn).
        public void HideAll()
        {
            settings?.Hide();
            if (hasLeaveGamePopup) leaveGameView.Hide();
            if (hasPurchasePopup) purchasePopup.Hide();
        }

        private void HandleHomeShown(HomeShownEvent _) => HideAll();
        private void HandleGameplayShown(GameplayShownEvent _) => HideAll();

        /// Bấm Leave game trong popup cài đặt: hỏi lại trước khi thoát. Thiếu popup thì KHÔNG thoát thẳng (lỡ tay là mất
        /// game) — lỗi đã báo lúc khởi tạo.
        private void HandleQuitRequested()
        {
            if (hasLeaveGamePopup) leaveGameView.Show();
        }

        /// Thoát ứng dụng. Trong Editor Application.Quit không làm gì — chỉ thấy dòng log này.
        /// ⚠️ iOS: Application.Quit gần như không dùng được và Apple khuyên app không tự thoát (có thể bị từ chối lúc duyệt) —
        /// build iOS thì nên ẩn nút Leave game.
        private void QuitGame()
        {
            GameLog.Info("[BusOut] Thoát ứng dụng (Leave game). Trong Editor lệnh này bị bỏ qua.", this);
            Application.Quit();
        }

        /// Bấm Home trong popup cài đặt (chỉ có trong ván): về Menu ngay.
        private void GoHome()
        {
            if (!EventBus<GoHomeRequestedEvent>.HasListeners)
                Debug.LogError("[BusOut] Không có ai chuyển về Menu (thiếu GameFlowController): nút về Home không làm gì. " +
                               "Tạo object GameFlow ở gốc scene, gắn GameFlowController, kéo HomeContext + GameContext vào.", this);
            EventBus.Publish(new GoHomeRequestedEvent());
        }

        private SettingsPresenter CreateSettings()
        {
            if (settingsView == null)
            {
                Debug.LogError("[BusOut] SharedPopupsPresenter thiếu Settings View: nút cài đặt không mở popup. " +
                               "Kéo SettingPanel (có SettingsView) vào.", this);
                return null;
            }
            return settingsView.Init(config.PanelAnimation)
                ? new SettingsPresenter(config, DataManager.Instance.GameData, settingsView, GoHome, HandleQuitRequested)
                : null;
        }

        private bool InitPurchasePopup()
        {
            if (purchasePopup == null)
            {
                Debug.LogError("[BusOut] SharedPopupsPresenter thiếu Purchase Popup: không mua thêm lượt booster / ô đỗ được. " +
                               "Kéo PurchasePanel (có PurchasePopupView) vào.", this);
                return false;
            }
            return purchasePopup.Init(config.PanelAnimation);
        }

        /// Thiếu popup thì nút Leave game không làm gì, game vẫn chơi được.
        private bool InitLeaveGamePopup()
        {
            if (leaveGameView == null)
            {
                Debug.LogError("[BusOut] SharedPopupsPresenter thiếu Leave Game View: nút Leave game (thoát ứng dụng) không làm gì. " +
                               "Kéo LeaveGamePanel (có LeaveGameView) vào.", this);
                return false;
            }
            if (!leaveGameView.Init(config.PanelAnimation)) return false;
            leaveGameView.Confirmed += QuitGame;
            leaveGameView.Cancelled += leaveGameView.Hide;
            return true;
        }
    }
}
