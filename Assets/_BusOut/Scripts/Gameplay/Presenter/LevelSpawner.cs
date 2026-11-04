using System.Collections.Generic;
using BusOut.DesignPattern;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Dựng xe của một level và giữ danh sách xe đang có mặt. GameplayPresenter tạo và gọi.
    public sealed class LevelSpawner
    {
        private readonly GameplayConfig config;
        private readonly ParkingLotView parkingLot;
        private readonly CarView fourSeatPrefab;
        private readonly CarView sixSeatPrefab;
        private readonly CarView tenSeatPrefab;

        // Chỉ số trong 2 list = CarId. View của xe đã rời đi được đặt null, không xoá để giữ đúng chỉ số.
        private readonly List<CarModel> cars = new List<CarModel>();
        private readonly List<CarView> carViews = new List<CarView>();

        /// Mọi xe của level (kể cả xe đã rời đi: State = Gone). Chỉ số = CarId.
        public IReadOnlyList<CarModel> Cars => cars;

        public LevelSpawner(GameplayConfig config, ParkingLotView parkingLot,
            CarView fourSeatPrefab, CarView sixSeatPrefab, CarView tenSeatPrefab)
        {
            this.config = config;
            this.parkingLot = parkingLot;
            this.fourSeatPrefab = fourSeatPrefab;
            this.sixSeatPrefab = sixSeatPrefab;
            this.tenSeatPrefab = tenSeatPrefab;
        }

        public void Spawn(LevelData level)
        {
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
                Debug.LogWarning($"[BusOut] Level {level.index}: bỏ qua {skippedInTool} xe trong garage/băng chuyền và {skippedVip} xe VIP (chưa hỗ trợ).");
        }

        /// Xe "bung" ra từ tâm bãi: xe gần tâm hiện trước, xe xa nhất trễ <paramref name="spread"/> giây.
        /// Trả về thời gian tới khi xe cuối cùng hiện xong (để khoá chạm trong lúc đó).
        public float PlayIntro(float duration, float spread)
        {
            if (carViews.Count == 0) return 0f;
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (CarView view in carViews)
            {
                if (view == null) continue;
                center += view.transform.position;
                count++;
            }
            if (count == 0) return 0f;
            center /= count;
            float maxDistance = 0f;
            foreach (CarView view in carViews)
                if (view != null) maxDistance = Mathf.Max(maxDistance, Vector3.Distance(view.transform.position, center));
            foreach (CarView view in carViews)
            {
                if (view == null) continue;
                float t = maxDistance > 0f ? Vector3.Distance(view.transform.position, center) / maxDistance : 0f;
                view.PlayIntro(spread * t, duration);
            }
            return spread + duration;
        }

        /// Tạo sẵn xe trong pool, mỗi loại đủ cho level đông loại đó nhất của file curve: vào level / chơi lại không còn
        /// Instantiate. Gọi một lần lúc load (TẠM: GameplayPresenter.Start; có màn Loading thì gọi trong lúc Loading).
        public void Prewarm(LevelCurveData curve)
        {
            int four = 0, six = 0, ten = 0;
            if (curve?.levels != null)
                foreach (LevelData level in curve.levels)
                {
                    if (level?.vehicles == null) continue;
                    int levelFour = 0, levelSix = 0, levelTen = 0;
                    foreach (VehicleData data in level.vehicles)
                    {
                        if (data.IsInTool) continue; // như Spawn: xe garage / băng chuyền chưa spawn
                        if (data.Type == VehicleType.FourSeat) levelFour++;
                        else if (data.Type == VehicleType.SixSeat) levelSix++;
                        else if (data.Type == VehicleType.TenSeat) levelTen++;
                    }
                    four = Mathf.Max(four, levelFour);
                    six = Mathf.Max(six, levelSix);
                    ten = Mathf.Max(ten, levelTen);
                }
            PoolingManager.Prewarm(fourSeatPrefab.gameObject, four);
            PoolingManager.Prewarm(sixSeatPrefab.gameObject, six);
            PoolingManager.Prewarm(tenSeatPrefab.gameObject, ten);
        }

        /// Trả mọi xe về pool, dừng mọi chuyển động. Gọi trước khi dựng level mới.
        /// ⚠️ Pool TẮT object ngay nên collider ra khỏi vật lý ngay. Đừng đổi lại thành Destroy mà không tắt collider trước:
        /// Destroy cuối frame mới xoá, dựng đồ thị chặn cho level mới (cùng frame) sẽ bắn trúng xe cũ nằm đúng chỗ đó.
        public void DespawnAll()
        {
            foreach (CarView view in carViews)
                if (view != null) PoolingManager.Despawn(view);
            cars.Clear();
            carViews.Clear();
        }

        /// Xe đã rời bãi hẳn: trả hình về pool, giữ chỗ trong list để CarId của xe khác không đổi.
        public void Despawn(CarView view)
        {
            if (!TryGetCar(view, out CarModel car)) return;
            car.MarkGone();
            carViews[view.CarId] = null;
            PoolingManager.Despawn(view);
        }

        /// Mọi xe đã rời đi hẳn (hoặc level không có xe).
        public bool AllCarsGone
        {
            get
            {
                foreach (CarModel car in cars)
                    if (car.State != CarState.Gone) return false;
                return true;
            }
        }

        /// Có xe đang chạy lên bến hoặc đang rời bến: tới nơi / đi khỏi sẽ đổi tình thế (ô trống ra, xe mới tới).
        public bool HasMovingCar
        {
            get
            {
                foreach (CarModel car in cars)
                    if (car.State == CarState.MovingToSlot || car.State == CarState.Leaving) return true;
                return false;
            }
        }

        /// Đồ thị chặn của level hiện tại (lần dựng gần nhất); null trước khi dựng.
        public BlockingGraph Graph { get; private set; }

        /// Dựng đồ thị chặn cho các xe vừa spawn: mỗi xe ghi lại mọi xe nằm trên đường ra của nó.
        public BlockingGraph BuildBlockingGraph()
        {
            // ⚠️ Xe vừa Instantiate rồi đổi scale trong cùng frame: collider vật lý chưa cập nhật theo scale mới,
            // bắn tia lúc này là trúng collider to gấp 1/0.85. Đồng bộ trước.
            Physics.SyncTransforms();
            var graph = new BlockingGraph(cars.Count);
            var aheadViews = new List<CarView>();
            var aheadIds = new List<int>();
            LaneRing ring = parkingLot.Ring;
            for (int id = 0; id < carViews.Count; id++)
            {
                CarView view = carViews[id];
                if (view == null || !view.gameObject.activeSelf) continue;
                aheadViews.Clear();
                aheadIds.Clear();
                Transform t = view.transform;
                view.FindAllAhead(ParkingRoute.DistanceToRing(t.position, t.forward, ring), config.blockerRayInset, aheadViews);
                foreach (CarView other in aheadViews)
                    if (TryGetCar(other, out CarModel otherCar)) aheadIds.Add(otherCar.Id);
                graph.SetAhead(id, aheadIds);
            }
            Graph = graph;
            return graph;
        }

        /// Hình của xe đang có mặt; null nếu xe đã rời đi hoặc CarId sai.
        public CarView GetView(int carId) => carId >= 0 && carId < carViews.Count ? carViews[carId] : null;

        /// Tìm Model của một xe đang có mặt. False nếu view không thuộc level hiện tại (vd. xe của level cũ).
        public bool TryGetCar(CarView view, out CarModel car)
        {
            int id = view.CarId;
            if (id >= 0 && id < carViews.Count && carViews[id] == view)
            {
                car = cars[id];
                return true;
            }
            car = null;
            return false;
        }

        private void SpawnCar(VehicleData data, CarView prefab)
        {
            int id = cars.Count;
            Vector3 position = parkingLot.LevelToWorld(data.x, data.z);
            Quaternion rotation = Quaternion.Euler(0f, data.rotY, 0f);
            // ⚠️ Pool thay Instantiate: dựng ~84 xe mỗi lần vào level / chơi lại là khung giật. Xe lấy lại giữ scale lần
            // trước (có thể đang 0 nếu bị trả về giữa hoạt ảnh vào level) → luôn đặt lại scale ở đây, trước PlayIntro.
            CarView view = PoolingManager.Spawn(prefab, position, rotation, parkingLot.CarsRoot);
            // Co cùng tỉ lệ với bố cục (ParkingLotView.LayoutScale): bố cục gốc xếp sát, chỉ co vị trí là xe chồng lên nhau.
            view.transform.localScale = prefab.transform.localScale * (config.carScale * parkingLot.LayoutScale);
#if UNITY_EDITOR
            // Tên theo CarId chỉ để dò trong Hierarchy; bản build bỏ (mỗi xe một chuỗi mới mỗi level).
            view.name = $"{prefab.name}_{id:00}";
#endif

            cars.Add(new CarModel(id, data.Type, data.color));
            carViews.Add(view);
            // Ra khỏi bãi xe về nguyên cỡ prefab (carScale + độ co bố cục chỉ để bãi vừa camera).
            if (!view.Init(id, config.GetCarColor(data.color), CarModel.SeatsOf(data.Type), prefab.transform.localScale))
                view.gameObject.SetActive(false);
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
    }
}
