using System.Collections.Generic;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Nhịp gameplay: dựng level, nhận chạm xe, quyết định xe bị chặn / chạy lên bến / rời bến.
    /// Chỗ duy nhất biết cả Model (CarModel, BusStop) lẫn View (CarView, ParkingLotView).
    public class GameplayPresenter : MonoBehaviour
    {
        [SerializeField] private GameplayConfig config;
        [SerializeField] private ParkingLotView parkingLot;
        [SerializeField] private CarInput carInput;

        [Header("Level")]
        [Tooltip("File level đã giải mã (Assets/_BusOut/Data/Levels/curve_*.json).")]
        [SerializeField] private TextAsset levelCurve;
        [Tooltip("Số level trong file (trường \"index\"), bắt đầu từ 1.")]
        [SerializeField, Min(1)] private int levelNumber = 1;

        [Header("Prefab xe (có CarView trên root)")]
        [SerializeField] private CarView fourSeatPrefab;
        [SerializeField] private CarView sixSeatPrefab;
        [SerializeField] private CarView tenSeatPrefab;

        // Chỉ số trong 2 list = CarId. View của xe đã rời đi được đặt null, không xoá để giữ đúng chỉ số.
        private readonly List<CarModel> cars = new List<CarModel>();
        private readonly List<CarView> carViews = new List<CarView>();
        private readonly List<Vector3> routeBuffer = new List<Vector3>(16);

        private LevelCurveData curveData;
        private BusStop busStop;
        private bool isReady;

        private void Start()
        {
            if (config == null) config = GameplayConfig.CreateDefault();
            if (!ValidateReferences()) return;
            if (!parkingLot.Init()) return;
            if (!carInput.Init(config.tapRayDistance)) return;

            curveData = LevelCurveData.Parse(levelCurve.text);
            isReady = true;
            LoadLevel(levelNumber);
        }

        private void OnEnable()
        {
            if (carInput != null) carInput.CarTapped += HandleCarTapped;
        }

        private void OnDisable()
        {
            if (carInput != null) carInput.CarTapped -= HandleCarTapped;
        }

        [ContextMenu("Chơi lại level")]
        private void ReloadLevel()
        {
            if (isReady) LoadLevel(levelNumber);
        }

        public void LoadLevel(int number)
        {
            ClearLevel();
            LevelData level = curveData.FindLevel(number);
            if (level == null || level.vehicles == null)
            {
                Debug.LogError($"[BusOut] Không có level {number} trong {levelCurve.name}. Đổi Level Number trên GameplayPresenter.", this);
                return;
            }

            int skippedInTool = 0, skippedVip = 0;
            foreach (VehicleData data in level.vehicles)
            {
                // TODO(garage/băng chuyền/bàn xoay, xe VIP): chưa làm, bỏ qua để phần còn lại vẫn chơi được.
                if (data.IsInTool) { skippedInTool++; continue; }
                CarView prefab = GetPrefab(data.Type);
                if (prefab == null) { skippedVip++; continue; }
                SpawnCar(data, prefab);
            }
            if (skippedInTool > 0 || skippedVip > 0)
                Debug.LogWarning($"[BusOut] Level {number}: bỏ qua {skippedInTool} xe trong garage/băng chuyền và {skippedVip} xe VIP (chưa hỗ trợ).", this);

            busStop = new BusStop(parkingLot.SlotCount, config.openSlotCount);
        }

        private void SpawnCar(VehicleData data, CarView prefab)
        {
            int id = cars.Count;
            Vector3 position = parkingLot.LevelToWorld(data.x, data.z);
            Quaternion rotation = Quaternion.Euler(0f, data.rotY, 0f);
            CarView view = Instantiate(prefab, position, rotation, parkingLot.CarsRoot);
            view.transform.localScale = prefab.transform.localScale * config.carScale;
            view.name = $"{prefab.name}_{id:00}";

            cars.Add(new CarModel(id, data.Type, data.color));
            carViews.Add(view);
            if (!view.Init(id, config.GetCarColor(data.color))) view.gameObject.SetActive(false);
        }

        private void ClearLevel()
        {
            foreach (CarView view in carViews)
            {
                if (view == null) continue;
                view.StopAll();
                Destroy(view.gameObject);
            }
            cars.Clear();
            carViews.Clear();
        }

        private void HandleCarTapped(CarView view)
        {
            if (!isReady || view.CarId >= cars.Count || carViews[view.CarId] != view) return;
            CarModel car = cars[view.CarId];
            switch (car.State)
            {
                case CarState.Parked:
                    TryMoveOut(car, view);
                    break;
                case CarState.InSlot:
                    // ⚠️ TẠM: chưa có hành khách nên xe không bao giờ đầy. Chạm xe ở bến cho nó rời đi để thử tiếp được.
                    // Bỏ nhánh này khi làm xong khách lên xe (xe đầy tự rời bến).
                    StartLeaving(car, view);
                    break;
            }
        }

        private void TryMoveOut(CarModel car, CarView view)
        {
            if (view.IsBusy) return;

            Transform carTransform = view.transform;
            LaneRing ring = parkingLot.Ring;
            float distanceToLane = ParkingRoute.DistanceToRing(carTransform.position, carTransform.forward, ring);
            if (view.FindBlocker(distanceToLane, config.blockerRayInset, out CarView blocker, out float hitDistance))
            {
                float travel = Mathf.Max(0f, hitDistance - view.HalfLength - config.rammedGap);
                Vector3 hitDirection = carTransform.forward;
                view.PlayRam(travel, config, () => blocker.PlayRammed(hitDirection, config));
                return;
            }

            int slotIndex = busStop.TryReserve(car.Id);
            if (slotIndex < 0)
            {
                view.PlayRejectShake(config);
                return;
            }

            car.StartMoveToSlot(slotIndex);
            // Tắt collider ngay: xe đã rời chỗ không còn chặn ai, và không chạm được khi đang chạy.
            view.SetCollidersEnabled(false, false);

            Vector3 slotEntry = parkingLot.GetSlotEntryPoint(slotIndex);
            Vector3 slotPark = parkingLot.GetSlotParkPoint(slotIndex);
            ParkingRoute.BuildToSlot(carTransform.position, carTransform.forward, ring, slotEntry, slotPark, routeBuffer);
            Vector3 parkDirection = slotPark - slotEntry;
            parkDirection.y = 0f;
            view.Drive(routeBuffer, config.carMoveSpeed, config.carTurnSharpness,
                Quaternion.LookRotation(parkDirection), HandleCarArrivedSlot);
        }

        private void HandleCarArrivedSlot(CarView view)
        {
            cars[view.CarId].ArriveSlot();
            // Bật lại vùng chạm cho nhánh TẠM "chạm xe ở bến để rời đi"; thân xe vẫn tắt vì bến nằm ngoài bãi.
            view.SetCollidersEnabled(false, true);
        }

        private void StartLeaving(CarModel car, CarView view)
        {
            int slotIndex = car.SlotIndex;
            busStop.Release(slotIndex);
            car.StartLeaving();
            view.SetCollidersEnabled(false, false);
            // Random trái/phải cho đa dạng (game gốc chỉ rời sang phải).
            Vector3 leavingPoint = Random.value < 0.5f ? parkingLot.LeftLeavingPoint : parkingLot.RightLeavingPoint;
            int reverseCount = ParkingRoute.BuildLeave(view.transform.position, parkingLot.GetSlotEntryPoint(slotIndex),
                leavingPoint, routeBuffer);
            view.Drive(routeBuffer, config.carMoveSpeed, config.carTurnSharpness, null, HandleCarLeft, reverseCount);
        }

        private void HandleCarLeft(CarView view)
        {
            cars[view.CarId].MarkGone();
            carViews[view.CarId] = null;
            Destroy(view.gameObject);
        }

        private CarView GetPrefab(VehicleType type)
        {
            switch (type)
            {
                case VehicleType.FourSeat: return fourSeatPrefab;
                case VehicleType.SixSeat: return sixSeatPrefab;
                case VehicleType.TenSeat: return tenSeatPrefab;
                default: return null;
            }
        }

        private bool ValidateReferences()
        {
            string missing = "";
            if (parkingLot == null) missing += " ParkingLot";
            if (carInput == null) missing += " CarInput";
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
