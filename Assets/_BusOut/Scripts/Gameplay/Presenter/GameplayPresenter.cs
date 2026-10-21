using BusOut.Core;
using BusOut.DesignPattern;
using UnityEngine;
using UnityEngine.Serialization;

namespace BusOut.Gameplay
{
    /// Vòng đời một ván: khởi tạo, dựng level, thắng / thua, chơi lại (sau: hồi sinh).
    /// MonoBehaviour duy nhất của tầng Presenter; giữ tham chiếu scene và tạo các presenter con:
    /// LevelSpawner (xe trong level), BusStopPresenter (ô đỗ), CarFlowPresenter (điều khiển xe),
    /// PassengerPresenter (hàng khách), BoardingPresenter (khách lên xe, xe đầy rời bến).
    public class GameplayPresenter : MonoBehaviour
    {
        [SerializeField] private GameplayConfig config;
        [SerializeField] private ParkingLotView parkingLot;
        [SerializeField] private BusStopView busStopView;
        [FormerlySerializedAs("carInput")]
        [SerializeField] private GameplayInput gameplayInput;

        [Header("Level")]
        [Tooltip("File level đã giải mã (Assets/_BusOut/Data/Levels/curve_*.json).")]
        [SerializeField] private TextAsset levelCurve;

        [Header("Prefab xe (có CarView trên root)")]
        [SerializeField] private CarView fourSeatPrefab;
        [SerializeField] private CarView sixSeatPrefab;
        [SerializeField] private CarView tenSeatPrefab;

        [Header("Hành khách")]
        [Tooltip("PassengerLineView trên GAMEOBJECT/ObjectPoint/PassengerPoint.")]
        [SerializeField] private PassengerLineView passengerLine;
        [Tooltip("Prefab hành khách (có PassengerView trên root).")]
        [SerializeField] private PassengerView passengerPrefab;
        [Tooltip("Biển đếm khách còn lại (PassengerCountView trên ENVIRONMENT/CountSign).")]
        [SerializeField] private PassengerCountView passengerCount;

        [Header("Giao diện trong ván")]
        [Tooltip("TopBarView trên Canvas/TopUI.")]
        [SerializeField] private TopBarView topBar;
        [Tooltip("BoosterBarView trên Canvas/BottomUI/Boosters.")]
        [SerializeField] private BoosterBarView boosterBar;

        private LevelCurveData curveData;
        private LevelSpawner spawner;
        private BusStopPresenter busStop;
        private CarFlowPresenter carFlow;
        private PassengerPresenter passengers; // null khi thiếu tham chiếu hành khách: xe vẫn chơi được
        private BoardingPresenter boarding;    // null cùng lúc với passengers
        private HudPresenter hud;
        private SecureGameData gameData;
        private bool isReady;
        // true khi không có ván nào đang chơi (đã thắng / thua, hoặc level không tải được): ngừng kiểm tra thắng thua,
        // khoá chạm xe và chạm ô.
        private bool levelFinished = true;
        private int currentLevel;

        private void Start()
        {
            if (config == null) config = GameplayConfig.CreateDefault();
            if (!ValidateReferences()) return;
            if (!parkingLot.Init()) return;
            if (!busStopView.Init()) return;
            if (!gameplayInput.Init(config.tapRayDistance)) return;

            curveData = LevelCurveData.Parse(levelCurve.text);
            spawner = new LevelSpawner(config, parkingLot, fourSeatPrefab, sixSeatPrefab, tenSeatPrefab);
            busStop = new BusStopPresenter(busStopView, config);
            carFlow = new CarFlowPresenter(config, parkingLot, spawner, busStop);
            passengers = CreatePassengerPresenter();
            if (passengers != null) boarding = new BoardingPresenter(config, spawner, busStop, passengers, carFlow);
            // Thiếu biển đếm chỉ mất con số trên biển, game vẫn chơi được.
            if (passengerCount == null)
                Debug.LogError("[BusOut] GameplayPresenter thiếu Passenger Count: biển đếm khách không cập nhật. " +
                               "Kéo CountSign (có PassengerCountView) vào mục Hành khách.", this);
            else if (!passengerCount.Init()) passengerCount = null;
            gameData = DataManager.Instance.GameData;
            hud = CreateHudPresenter();
            isReady = true;
            LoadLevel(gameData.currentLevel);
        }

        private void OnDestroy()
        {
            hud?.Dispose();
        }

        private void OnEnable()
        {
            if (gameplayInput == null) return;
            gameplayInput.CarTapped += HandleCarTapped;
            gameplayInput.SlotTapped += HandleSlotTapped;
        }

        private void OnDisable()
        {
            if (gameplayInput == null) return;
            gameplayInput.CarTapped -= HandleCarTapped;
            gameplayInput.SlotTapped -= HandleSlotTapped;
        }

        [ContextMenu("Chơi lại level")]
        private void ReloadLevel()
        {
            if (isReady) LoadLevel(gameData.currentLevel);
        }

        /// ⚠️ Thứ tự có nghĩa: xoá xe cũ (kèm tween/đường chạy) TRƯỚC khi đặt lại ô, không thì xe cũ tới nơi
        /// sau khi reset sẽ đánh dấu ô của level mới là có xe.
        public void LoadLevel(int number)
        {
            passengers?.DespawnAll();
            spawner.DespawnAll();
            busStop.ResetForLevel(config.openSlotCount);

            LevelData level = curveData.FindLevel(number);
            if (level == null || level.vehicles == null)
            {
                Debug.LogError($"[BusOut] Không có level {number} trong {levelCurve.name}. Level lấy từ save (SecureGameData.currentLevel): xoá save hoặc sửa file curve.", this);
                // ⚠️ Dọn hết trạng thái ván cũ: giữ lại thì bộ lên kế hoạch cũ chạy trên danh sách xe đã xoá (lỗi mỗi frame),
                // và "không xe + không khách" bị tính là thắng.
                passengers?.ResetForLevel(null);
                boarding?.ResetForLevel();
                levelFinished = true;
                return;
            }
            spawner.Spawn(level);
            currentLevel = number;
            levelFinished = false;
            if (passengers != null)
            {
                // Hạt giống = số level: cùng level luôn ra cùng thứ tự màu, dễ thử lại.
                var planner = new PassengerColorPlanner(spawner.Cars, spawner.BuildBlockingGraph(), new System.Random(number));
                passengers.ResetForLevel(planner);
            }
            boarding?.ResetForLevel();
            EventBus.Publish(new LevelStartedEvent { LevelIndex = number, LevelName = level.index.ToString() });
        }

        private void Update()
        {
            if (!isReady) return;
            float deltaTime = Time.deltaTime;
            passengers?.Tick(deltaTime);
            boarding?.Tick(deltaTime);
            if (passengers != null && passengerCount != null) passengerCount.SetCount(passengers.RemainingCount);
            CheckLevelCompleted();
            CheckLevelFailed();
        }

        /// Thắng: mọi xe đã rời đi và không còn khách nào phải chở (tổng khách = tổng ghế nên hai điều kiện đi cùng nhau).
        private void CheckLevelCompleted()
        {
            if (levelFinished || passengers == null) return;
            if (!spawner.AllCarsGone || passengers.RemainingCount > 0) return;
            levelFinished = true;
            Debug.Log($"[BusOut] Thắng level {currentLevel}.", this);
            // Lưu ngay khi thắng (không chờ app vào nền): tắt app ngang lúc này vẫn giữ level mới và coin thưởng.
            gameData.CompleteLevel(curveData.LastLevelNumber);
            gameData.AddCoins(config.winCoinReward);
            gameData.Save();
            EventBus.Publish(new LevelCompletedEvent { LevelIndex = currentLevel });
        }

        /// Thua: hết ô và không còn nước nào gỡ được (xem BoardingPresenter.IsStuck). Báo đúng một lần mỗi ván.
        private void CheckLevelFailed()
        {
            if (levelFinished || boarding == null || !boarding.IsStuck()) return;
            levelFinished = true;
            Debug.Log($"[BusOut] Thua level {currentLevel}: hết ô đỗ, không xe nào ở bến cùng màu người đầu hàng.", this);
            EventBus.Publish(new LevelFailedEvent { LevelIndex = currentLevel, Reason = "Out of slots" });
        }

        /// Thiếu thanh nào thì chỉ mất thanh đó, game vẫn chơi được.
        private HudPresenter CreateHudPresenter()
        {
            if (topBar == null)
                Debug.LogError("[BusOut] GameplayPresenter thiếu Top Bar: coin / level không hiện. Kéo Canvas/TopUI (có TopBarView) vào.", this);
            else if (!topBar.Init()) topBar = null;
            if (boosterBar == null)
                Debug.LogError("[BusOut] GameplayPresenter thiếu Booster Bar: nút booster không cập nhật. Kéo Canvas/BottomUI/Boosters (có BoosterBarView) vào.", this);
            else if (!boosterBar.Init()) boosterBar = null;
            return new HudPresenter(config, gameData, topBar, boosterBar, ReloadLevel);
        }

        private PassengerPresenter CreatePassengerPresenter()
        {
            if (passengerLine == null || passengerPrefab == null)
            {
                Debug.LogError("[BusOut] GameplayPresenter thiếu Passenger Line hoặc Passenger Prefab: chưa có hàng khách " +
                               "(xe vẫn chơi được). Kéo PassengerPoint và prefab Passenger vào mục Hành khách.", this);
                return null;
            }
            return passengerLine.Init() ? new PassengerPresenter(config, passengerLine, passengerPrefab) : null;
        }

        private void HandleCarTapped(CarView view)
        {
            if (isReady && !levelFinished) carFlow.HandleCarTapped(view);
        }

        private void HandleSlotTapped(BusStopSlotView slot)
        {
            if (isReady && !levelFinished) busStop.HandleSlotTapped(slot.Index);
        }

        private bool ValidateReferences()
        {
            string missing = "";
            if (parkingLot == null) missing += " ParkingLot";
            if (busStopView == null) missing += " BusStopView(SlotParking)";
            if (gameplayInput == null) missing += " GameplayInput";
            if (levelCurve == null) missing += " LevelCurve";
            if (fourSeatPrefab == null) missing += " FourSeatPrefab(Car)";
            if (sixSeatPrefab == null) missing += " SixSeatPrefab(Van)";
            if (tenSeatPrefab == null) missing += " TenSeatPrefab(Bus)";
            if (missing.Length == 0) return true;
            Debug.LogError($"[BusOut] GameplayPresenter thiếu tham chiếu:{missing}. Kéo vào trong Inspector.", this);
            return false;
        }
    }
}
