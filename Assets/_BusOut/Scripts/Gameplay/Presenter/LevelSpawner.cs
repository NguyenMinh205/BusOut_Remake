using System.Collections.Generic;
using BusOut.Core;
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
        private int hiddenColorCount;
        private int lockOrKeyCount;

        // Bãi chờ: số bãi → CarId theo thứ tự ra; xe đứng trước cổng của mỗi bãi (vị trí chung của cả bãi).
        private readonly Dictionary<int, List<int>> garageQueues = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, VehicleData> garageFronts = new Dictionary<int, VehicleData>();
        private readonly Dictionary<int, GarageView> garageViews = new Dictionary<int, GarageView>();
        private readonly Dictionary<int, float> garageRollOutTimers = new Dictionary<int, float>();
        private readonly Dictionary<int, int> garageShownWaiting = new Dictionary<int, int>();
        private readonly List<int> garageIdBuffer = new List<int>();

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
            garageFronts.Clear();
            foreach (VehicleData data in level.vehicles)
                if (data.garageId > 0 && !data.IsInTool &&
                    (!garageFronts.TryGetValue(data.garageId, out VehicleData front) || data.garageOrder < front.garageOrder))
                    garageFronts[data.garageId] = data;
            foreach (VehicleData data in level.vehicles)
            {
                // Xe "trong công cụ" và xe VIP của file curve giải mã: cố ý bỏ qua, cơ chế mới (bãi chờ...) dùng trường riêng.
                if (data.IsInTool) { skippedInTool++; continue; }
                CarView prefab = GetPrefab(data.Type);
                if (prefab == null) { skippedVip++; continue; }
                SpawnCar(data, prefab);
            }
            SetupGarages();
            UpdateLockMarks();
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
            // Xe còn trong bãi chờ (đang tắt) không bung: nó lăn ra sau.
            foreach (CarView view in carViews)
            {
                if (view == null || !view.gameObject.activeSelf) continue;
                center += view.transform.position;
                count++;
            }
            if (count == 0) return 0f;
            center /= count;
            float maxDistance = 0f;
            foreach (CarView view in carViews)
                if (view != null && view.gameObject.activeSelf) maxDistance = Mathf.Max(maxDistance, Vector3.Distance(view.transform.position, center));
            foreach (CarView view in carViews)
            {
                if (view == null || !view.gameObject.activeSelf) continue;
                float t = maxDistance > 0f ? Vector3.Distance(view.transform.position, center) / maxDistance : 0f;
                view.PlayIntro(spread * t, duration);
            }
            return spread + duration;
        }

        /// Tạo sẵn xe trong pool, mỗi loại đủ cho level đông loại đó nhất của file curve: vào level / chơi lại không còn
        /// Instantiate. Gọi một lần trong màn Loading. <paramref name="ownLevels"/> (level riêng) có thể null.
        public void Prewarm(LevelCurveData curve, LevelCurveData ownLevels)
        {
            int four = 0, six = 0, ten = 0;
            CountLargestLevel(curve, ref four, ref six, ref ten);
            CountLargestLevel(ownLevels, ref four, ref six, ref ten);
            PoolingManager.Prewarm(fourSeatPrefab.gameObject, four);
            PoolingManager.Prewarm(sixSeatPrefab.gameObject, six);
            PoolingManager.Prewarm(tenSeatPrefab.gameObject, ten);
        }

        private static void CountLargestLevel(LevelCurveData file, ref int four, ref int six, ref int ten)
        {
            if (file?.levels == null) return;
            foreach (LevelData level in file.levels)
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
            hiddenColorCount = 0;
            lockOrKeyCount = 0;
            foreach (GarageView garage in garageViews.Values)
                if (garage != null) PoolingManager.Despawn(garage);
            garageViews.Clear();
            garageQueues.Clear();
            garageRollOutTimers.Clear();
            garageShownWaiting.Clear();
        }

        /// Xe này thuộc bãi chờ và còn xe ra sau nó (Undo không được đưa nó về chỗ cũ).
        public bool HasGarageSuccessor(CarModel car)
        {
            if (car.GarageId <= 0 || !garageQueues.TryGetValue(car.GarageId, out List<int> ids)) return false;
            foreach (int id in ids)
                if (cars[id].GarageOrder > car.GarageOrder) return true;
            return false;
        }

        /// Bãi chờ: xe trước rời chỗ đỗ → đợi garageRollOutDelay → xe kế lăn ra. Cập nhật số xe chờ + màu xe kế trên cổng.
        public void UpdateGarages(float deltaTime)
        {
            if (garageQueues.Count == 0) return;
            FxConfig fx = FxConfig.Instance;
            garageIdBuffer.Clear();
            garageIdBuffer.AddRange(garageQueues.Keys);
            foreach (int garageId in garageIdBuffer)
            {
                List<int> ids = garageQueues[garageId];
                int next = NextWaitingIndex(ids);
                if (next > 0 && cars[ids[next - 1]].State != CarState.Parked)
                {
                    float timer = garageRollOutTimers.TryGetValue(garageId, out float left) ? left - deltaTime : fx.garageRollOutDelay;
                    if (timer > 0f) garageRollOutTimers[garageId] = timer;
                    else
                    {
                        garageRollOutTimers.Remove(garageId);
                        carViews[ids[next]].PlayGarageRollOut(fx.garageRollOutDistance, fx.garageRollOutDuration);
                        next = NextWaitingIndex(ids);
                    }
                }
                RefreshGarageView(garageId, ids, next);
            }
        }

        /// Chỉ số (trong bãi) của xe đầu tiên còn nằm trong cổng: còn đỗ và đang tắt. -1 khi hết xe chờ.
        private int NextWaitingIndex(List<int> ids)
        {
            for (int k = 0; k < ids.Count; k++)
            {
                CarView view = carViews[ids[k]];
                if (cars[ids[k]].State == CarState.Parked && view != null && !view.gameObject.activeSelf) return k;
            }
            return -1;
        }

        private void RefreshGarageView(int garageId, List<int> ids, int next)
        {
            int waiting = next < 0 ? 0 : ids.Count - next;
            if (garageShownWaiting.TryGetValue(garageId, out int shown) && shown == waiting) return;
            garageShownWaiting[garageId] = waiting;
            if (!garageViews.TryGetValue(garageId, out GarageView view) || view == null) return;
            Color nextColor = Color.white;
            if (next >= 0)
            {
                CarModel car = cars[ids[next]];
                nextColor = car.IsColorHidden ? config.hiddenCarColor : config.GetCarColor(car.ColorIndex);
            }
            view.SetQueue(waiting, nextColor);
        }

        private void SetupGarages()
        {
            foreach (List<int> ids in garageQueues.Values) ids.Sort((a, b) => cars[a].GarageOrder.CompareTo(cars[b].GarageOrder));
            if (garageQueues.Count == 0) return;
            if (config.garagePrefab == null)
            {
                Debug.LogError("[BusOut] GameplayConfig thiếu Garage Prefab: bãi chờ vẫn chạy nhưng không có hình cổng. " +
                               "Tạo prefab có GarageView rồi kéo vào GameplayConfig.", config);
                return;
            }
            foreach (KeyValuePair<int, VehicleData> pair in garageFronts)
            {
                VehicleData front = pair.Value;
                GarageView view = PoolingManager.Spawn(config.garagePrefab, parkingLot.LevelToWorld(front.x, front.z),
                    Quaternion.Euler(0f, front.rotY, 0f), parkingLot.CarsRoot);
                if (view == null) continue;
                view.transform.localScale = config.garagePrefab.transform.localScale * (config.carScale * parkingLot.LayoutScale);
                garageViews[pair.Key] = view;
            }
        }

        /// Xe còn bị khoá: có xe mang chìa cùng số vẫn đang đỗ trong bãi. Cùng điều kiện với cạnh khoá trong đồ thị chặn.
        public bool IsLockedNow(CarModel car)
        {
            if (car.LockId <= 0) return false;
            foreach (CarModel other in cars)
                if (other.KeyId == car.LockId && other.Id != car.Id && other.State == CarState.Parked) return true;
            return false;
        }

        /// Ổ khoá hiện khi xe còn đỗ và còn bị khoá; chìa hiện khi xe mang chìa còn đỗ. Gọi mỗi khung (Undo có thể đưa xe mang
        /// chìa về bãi, xe kia bị khoá lại); level không có khoá thì thoát ngay.
        public void UpdateLockMarks()
        {
            if (lockOrKeyCount == 0) return;
            for (int id = 0; id < cars.Count; id++)
            {
                CarModel car = cars[id];
                CarView view = carViews[id];
                if (view == null) continue;
                bool parked = car.State == CarState.Parked;
                if (car.LockId > 0) view.SetLockVisible(parked && IsLockedNow(car), config.GetLockPairColor(car.LockId), parked);
                if (car.KeyId > 0) view.SetKeyVisible(parked, config.GetLockPairColor(car.KeyId));
            }
        }

        /// Xe ẩn màu lộ màu khi không còn xe đỗ nào chắn đường ra (cùng luật "chạm là ra được"). Gọi mỗi khung khi không trong
        /// hoạt ảnh vào level; level không có xe ẩn thì thoát ngay.
        public void RevealFreedCars()
        {
            if (hiddenColorCount == 0 || Graph == null) return;
            for (int id = 0; id < cars.Count; id++)
            {
                CarModel car = cars[id];
                if (!car.IsColorHidden || car.State != CarState.Parked || HasParkedBlocker(id)) continue;
                // Xe còn trong cổng bãi chờ: lộ màu lúc lăn ra là đủ (cổng đã hiện màu xe kế).
                CarView hiddenView = carViews[id];
                if (hiddenView == null || !hiddenView.gameObject.activeSelf) continue;
                RevealColor(car, true);
            }
        }

        /// Lộ màu một xe ngay (Crane nhấc xe đang ẩn). <paramref name="bounce"/> tắt khi xe đang có tween cỡ khác.
        public void RevealColor(CarModel car, bool bounce)
        {
            if (!car.IsColorHidden) return;
            car.RevealColor();
            hiddenColorCount--;
            GetView(car.Id)?.RevealColor(bounce);
        }

        private bool HasParkedBlocker(int carId)
        {
            IReadOnlyList<int> ahead = Graph.AheadOf(carId);
            for (int i = 0; i < ahead.Count; i++)
                if (cars[ahead[i]].State == CarState.Parked) return true;
            return false;
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
            var ahead = new List<int>[cars.Count];
            LaneRing ring = parkingLot.Ring;
            for (int id = 0; id < carViews.Count; id++)
            {
                ahead[id] = new List<int>();
                CarView view = carViews[id];
                // Xe còn trong cổng bãi chờ đang tắt: không có collider, cạnh của nó do AddGarageEdges thêm.
                if (view == null || !view.gameObject.activeSelf) continue;
                aheadViews.Clear();
                Transform t = view.transform;
                view.FindAllAhead(ParkingRoute.DistanceToRing(t.position, t.forward, ring), config.blockerRayInset, aheadViews);
                foreach (CarView other in aheadViews)
                    if (TryGetCar(other, out CarModel otherCar)) ahead[id].Add(otherCar.Id);
            }
            AddGarageEdges(ahead);
            for (int id = 0; id < cars.Count; id++)
            {
                List<int> aheadIds = ahead[id];
                // Khoá = xe mang chìa "chặn" xe bị khoá. Đưa vào đồ thị thì planner, Sort / Shuffle, hướng dẫn, lộ màu đều tự tính
                // đúng mà không phải sửa riêng. ⚠️ Lúc chạm xe dùng tia vật lý, không thấy cạnh này: CarFlowPresenter chặn riêng.
                int lockId = cars[id].LockId;
                if (lockId > 0)
                    foreach (CarModel keyCar in cars)
                        if (keyCar.KeyId == lockId && keyCar.Id != id && !aheadIds.Contains(keyCar.Id)) aheadIds.Add(keyCar.Id);
                graph.SetAhead(id, aheadIds);
            }
            Graph = graph;
            return graph;
        }

        /// Giống LevelBuilderRules.AddGarageEdges (sửa một bên thì sửa cả bên kia): xe chờ thứ k đứng đúng chỗ xe đầu nên bị chắn
        /// như xe đầu + phải đợi xe k−1; xe nào bị xe đầu chắn thì cũng bị mọi xe chờ chắn (chúng lần lượt ra đứng đúng ô đó).
        /// Cạnh khoá thêm SAU bước này: xe chờ không thừa hưởng khoá của xe đầu.
        private void AddGarageEdges(List<int>[] ahead)
        {
            foreach (List<int> ids in garageQueues.Values)
            {
                if (ids.Count < 2) continue;
                int front = ids[0];
                for (int k = 1; k < ids.Count; k++)
                {
                    var list = new List<int>(ahead[front]);
                    if (!list.Contains(ids[k - 1])) list.Add(ids[k - 1]);
                    list.Remove(ids[k]);
                    ahead[ids[k]] = list;
                }
                for (int i = 0; i < ahead.Length; i++)
                {
                    if (ids.Contains(i) || !ahead[i].Contains(front)) continue;
                    for (int k = 1; k < ids.Count; k++)
                        if (!ahead[i].Contains(ids[k])) ahead[i].Add(ids[k]);
                }
            }
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
            // Xe bãi chờ dùng vị trí của xe đứng trước cổng: cả bãi lần lượt ra đứng đúng chỗ đó.
            VehicleData pose = data.garageId > 0 && garageFronts.TryGetValue(data.garageId, out VehicleData front) ? front : data;
            Vector3 position = parkingLot.LevelToWorld(pose.x, pose.z);
            Quaternion rotation = Quaternion.Euler(0f, pose.rotY, 0f);
            // ⚠️ Pool thay Instantiate: dựng ~84 xe mỗi lần vào level / chơi lại là khung giật. Xe lấy lại giữ scale lần
            // trước (có thể đang 0 nếu bị trả về giữa hoạt ảnh vào level) → luôn đặt lại scale ở đây, trước PlayIntro.
            CarView view = PoolingManager.Spawn(prefab, position, rotation, parkingLot.CarsRoot);
            // Co cùng tỉ lệ với bố cục (ParkingLotView.LayoutScale): bố cục gốc xếp sát, chỉ co vị trí là xe chồng lên nhau.
            view.transform.localScale = prefab.transform.localScale * (config.carScale * parkingLot.LayoutScale);
#if UNITY_EDITOR
            // Tên theo CarId chỉ để dò trong Hierarchy; bản build bỏ (mỗi xe một chuỗi mới mỗi level).
            view.name = $"{prefab.name}_{id:00}";
#endif

            cars.Add(new CarModel(id, data.Type, data.color, data.colorHidden, data.lockId, data.keyId, data.garageId, data.garageOrder));
            if (data.garageId > 0)
            {
                if (!garageQueues.TryGetValue(data.garageId, out List<int> queue))
                {
                    queue = new List<int>();
                    garageQueues[data.garageId] = queue;
                }
                queue.Add(id);
            }
            if (data.lockId > 0 || data.keyId > 0) lockOrKeyCount++;
            carViews.Add(view);
            // Ra khỏi bãi xe về nguyên cỡ prefab (carScale + độ co bố cục chỉ để bãi vừa camera).
            if (!view.Init(id, config.GetCarColor(data.color), CarModel.SeatsOf(data.Type), prefab.transform.localScale))
                view.gameObject.SetActive(false);
            else
            {
                if (data.colorHidden)
                {
                    view.ShowColorHidden(config.hiddenCarColor);
                    hiddenColorCount++;
                }
                // Xe chờ trong cổng: tắt tới lượt lăn ra (không chặn tia, không chạm được, không bung lúc vào level).
                if (pose != data) view.gameObject.SetActive(false);
            }
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
