using UnityEngine;
using UnityEngine.Serialization;

namespace BusOut.Gameplay
{
    /// Vòng đời một ván: khởi tạo, dựng level, chơi lại (sau: thắng / thua / hồi sinh).
    /// MonoBehaviour duy nhất của tầng Presenter; giữ tham chiếu scene và tạo các presenter con:
    /// LevelSpawner (xe trong level), BusStopPresenter (ô đỗ), CarFlowPresenter (điều khiển xe).
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

        private LevelCurveData curveData;
        private LevelSpawner spawner;
        private BusStopPresenter busStop;
        private CarFlowPresenter carFlow;
        private bool isReady;

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
            spawner.DespawnAll();
            busStop.ResetForLevel(config.openSlotCount);

            LevelData level = curveData.FindLevel(number);
            if (level == null || level.vehicles == null)
            {
                Debug.LogError($"[BusOut] Không có level {number} trong {levelCurve.name}. Đổi Level Number trên GameplayPresenter.", this);
                return;
            }
            spawner.Spawn(level);
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
