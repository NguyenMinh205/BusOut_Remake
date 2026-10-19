using System.Collections.Generic;
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

        /// Xoá mọi xe, dừng mọi chuyển động. Gọi trước khi dựng level mới.
        public void DespawnAll()
        {
            foreach (CarView view in carViews)
            {
                if (view == null) continue;
                view.StopAll();
                // ⚠️ Destroy chỉ xoá cuối frame: tắt collider NGAY, không thì dựng đồ thị chặn cho level mới
                // (cùng frame) vẫn bắn trúng xe cũ đang nằm đúng chỗ đó.
                view.SetCollidersEnabled(false, false);
                view.DespawnSeatedPassengers();
                Object.Destroy(view.gameObject);
            }
            cars.Clear();
            carViews.Clear();
        }

        /// Xe đã rời bãi hẳn: xoá hình, giữ chỗ trong list để CarId của xe khác không đổi.
        public void Despawn(CarView view)
        {
            if (!TryGetCar(view, out CarModel car)) return;
            car.MarkGone();
            carViews[view.CarId] = null;
            view.DespawnSeatedPassengers();
            Object.Destroy(view.gameObject);
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
            CarView view = Object.Instantiate(prefab, position, rotation, parkingLot.CarsRoot);
            view.transform.localScale = prefab.transform.localScale * config.carScale;
            view.name = $"{prefab.name}_{id:00}";

            cars.Add(new CarModel(id, data.Type, data.color));
            carViews.Add(view);
            if (!view.Init(id, config.GetCarColor(data.color), CarModel.SeatsOf(data.Type))) view.gameObject.SetActive(false);
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
