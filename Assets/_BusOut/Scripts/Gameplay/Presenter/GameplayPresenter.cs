using BusOut.Core;
using BusOut.DesignPattern;
using UnityEngine;
using UnityEngine.Serialization;

namespace BusOut.Gameplay
{
    /// Vòng đời một ván: khởi tạo, dựng level, chơi lại (sau: thắng / thua / hồi sinh).
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
        [Tooltip("Số level trong file (trường \"index\"), bắt đầu từ 1.")]
        [SerializeField, Min(1)] private int levelNumber = 1;

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

        private LevelCurveData curveData;
        private LevelSpawner spawner;
        private BusStopPresenter busStop;
        private CarFlowPresenter carFlow;
        private PassengerPresenter passengers; // null khi thiếu tham chiếu hành khách: xe vẫn chơi được
        private BoardingPresenter boarding;    // null cùng lúc với passengers
        private bool isReady;
        // true khi không có ván nào đang chơi (đã thắng, hoặc level không tải được): ngừng kiểm tra thắng.
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
            isReady = true;
            LoadLevel(levelNumber);
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
            if (isReady) LoadLevel(levelNumber);
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
                Debug.LogError($"[BusOut] Không có level {number} trong {levelCurve.name}. Đổi Level Number trên GameplayPresenter.", this);
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
        }

        /// Thắng: mọi xe đã rời đi và không còn khách nào phải chở (tổng khách = tổng ghế nên hai điều kiện đi cùng nhau).
        private void CheckLevelCompleted()
        {
            if (levelFinished || passengers == null) return;
            if (!spawner.AllCarsGone || passengers.RemainingCount > 0) return;
            levelFinished = true;
            Debug.Log($"[BusOut] Thắng level {currentLevel}.", this);
            EventBus.Publish(new LevelCompletedEvent { LevelIndex = currentLevel });
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
            if (isReady) carFlow.HandleCarTapped(view);
        }

        private void HandleSlotTapped(BusStopSlotView slot)
        {
            if (isReady) busStop.HandleSlotTapped(slot.Index);
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
