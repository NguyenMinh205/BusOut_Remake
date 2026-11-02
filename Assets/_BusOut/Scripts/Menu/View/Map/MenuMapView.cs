using BusOut.DesignPattern;
using TMPro;
using UnityEngine;

namespace BusOut.Menu
{
    /// Bản đồ level ở Menu (trống quay) + trời. Chỉ vẽ theo lệnh: ô nào gắn module nào, bệ nào số mấy / đã qua chưa,
    /// xe ở bệ nào, trống quay tới đâu, trời chủ đề nào. Level nào ở ô nào do presenter quyết (MenuMapLayout).
    /// Gắn trên ENVIRONMENT/Map. Code tạo ô Slot0..7 trong Road lúc chạy.
    public class MenuMapView : MonoBehaviour
    {
        // Đường dẫn trong mọi prefab module (Prefab/Menu/RoadModule/...). ⚠️ Đổi tên trong prefab là mất bệ: không đặt
        // được xe / số level, không bật được lớp "đã qua" (có log lỗi).
        private const string PadPath = "ParkingBase";
        private const string DoneMarkName = "Base_Done";

        private static readonly int SkyTexId = Shader.PropertyToID("_SkyTex");
        private static readonly int CloudTexId = Shader.PropertyToID("_CloudTex");
        private static readonly int BlendSkyTexId = Shader.PropertyToID("_BlendSkyTex");
        private static readonly int BlendCloudTexId = Shader.PropertyToID("_BlendCloudTex");
        private static readonly int BlendFactorId = Shader.PropertyToID("_BlendFactor");

        [Tooltip("Road: trống quay, con của Map. Gốc: Map scale Y 0.65 (dẹt), Road xoay X −32.")]
        [SerializeField] private Transform road;
        [Tooltip("Xe buýt đứng ở bệ level hiện tại. Local position / rotation / scale của nó trong scene = tư thế SO VỚI " +
                 "TÂM BỆ (ParkingBase): lúc chạy code chuyển nó vào bệ, giữ nguyên các số này.")]
        [SerializeField] private Transform car;
        [Tooltip("Prefab chữ số level (TextMeshPro 3D). Local position / rotation trong prefab = so với tâm bệ.")]
        [SerializeField] private TMP_Text levelLabelPrefab;
        [Tooltip("Camera của Menu (HomeContext/Main Camera): để biết xe còn trong màn hình không (nút \"về level hiện tại\").")]
        [SerializeField] private Camera menuCamera;

        private Transform[] slots;
        private GameObject[] slotModules;       // instance đang gắn ở ô (null = ô trống)
        private GameObject[] slotModuleSources; // prefab của instance đó, để biết có cần đổi không
        private Transform[] slotPads;
        private GameObject[] slotDoneMarks;
        private TMP_Text[] labels;
        private Quaternion roadBaseRotation;
        private float degreesPerSlot;
        // Tư thế xe so với tâm bệ (dựng trong scene). Nhớ lại vì lúc chạy sang bệ mới xe tạm làm con của carPivot.
        private Vector3 carLocalPosition;
        private Quaternion carLocalRotation;
        private Vector3 carLocalScale;
        // Trục quay của xe khi chạy sang bệ mới: con của Road, quay ngược chiều Road → xe đứng yên trên màn hình, đường
        // lăn bên dưới (như gốc).
        private Transform carPivot;
        private Material sceneSkybox;
        private Material skybox; // bản sao lúc chạy, null khi skybox scene không dùng shader BusOut/Skybox

        public bool Init(int slotCount, float degreesPerSlot)
        {
            string missing = "";
            if (road == null) missing += " Road";
            if (car == null) missing += " Car";
            if (levelLabelPrefab == null) missing += " LevelLabelPrefab";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] MenuMapView thiếu tham chiếu:{missing}. Kéo Map/Road, xe buýt và prefab chữ số level vào.", this);
                return false;
            }
            this.degreesPerSlot = degreesPerSlot;
            roadBaseRotation = road.localRotation;
            slots = new Transform[slotCount];
            slotModules = new GameObject[slotCount];
            slotModuleSources = new GameObject[slotCount];
            slotPads = new Transform[slotCount];
            slotDoneMarks = new GameObject[slotCount];
            labels = new TMP_Text[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                var slot = new GameObject($"Slot{i}").transform;
                slot.SetParent(road, false);
                slot.localRotation = Quaternion.Euler(degreesPerSlot * i, 0f, 0f);
                slots[i] = slot;
                labels[i] = Instantiate(levelLabelPrefab, slot, false);
                labels[i].gameObject.SetActive(false);
            }
            carLocalPosition = car.localPosition;
            carLocalRotation = car.localRotation;
            carLocalScale = car.localScale;
            carPivot = new GameObject("CarPivot").transform;
            carPivot.SetParent(road, false);
            car.gameObject.SetActive(false);
            if (menuCamera == null)
                Debug.LogError("[BusOut] MenuMapView thiếu Menu Camera: lăn bản đồ là nút \"về level hiện tại\" hiện ngay, kể cả " +
                               "khi xe còn trong màn hình. Kéo HomeContext/Main Camera vào.", this);
            InitSkybox();
            return true;
        }

        /// Xe (đứng ở bệ level hiện tại) còn nằm trong vùng nhìn thấy không: viewport x trong [0, 1], y trong
        /// [<paramref name="viewportBottom"/>, <paramref name="viewportTop"/>]. Xe đang ẩn (bệ đã ra khỏi cửa sổ 8 level)
        /// hoặc thiếu camera → coi như khuất.
        public bool IsCarOnScreen(float viewportBottom, float viewportTop)
        {
            if (menuCamera == null || !car.gameObject.activeInHierarchy) return false;
            Vector3 point = menuCamera.WorldToViewportPoint(car.position);
            return point.z > 0f && point.x >= 0f && point.x <= 1f && point.y >= viewportBottom && point.y <= viewportTop;
        }

#if UNITY_EDITOR
        private static readonly int SpeedId = Shader.PropertyToID("_Speed");

        /// Chỉ Editor: chỉnh Tiling / Offset / Speed của Skybox.mat lúc đang Play là thấy ngay — mỗi khung chép từ material gốc
        /// sang bản sao (bản sao mới là cái đang vẽ). Sửa trên asset nên số được giữ sau khi thoát Play. Texture thì KHÔNG
        /// chép: lúc Play trời do chủ đề quyết.
        private void Update()
        {
            if (skybox == null) return;
            Vector2 scale = sceneSkybox.GetTextureScale(SkyTexId);
            Vector2 offset = sceneSkybox.GetTextureOffset(SkyTexId);
            skybox.SetTextureScale(SkyTexId, scale);
            skybox.SetTextureOffset(SkyTexId, offset);
            skybox.SetTextureScale(BlendSkyTexId, scale);
            skybox.SetTextureOffset(BlendSkyTexId, offset);
            skybox.SetFloat(SpeedId, sceneSkybox.GetFloat(SpeedId));
        }
#endif

        private void OnDestroy()
        {
            if (skybox == null) return;
            RenderSettings.skybox = sceneSkybox;
            Destroy(skybox);
        }

        /// Quay trống tới vị trí <paramref name="levelPosition"/> (= level − 1, số thực khi đang lăn giữa hai level):
        /// bệ của level đó nằm đúng chỗ bệ ô 0 lúc dựng.
        public void SetRoadPosition(float levelPosition) =>
            road.localRotation = roadBaseRotation * Quaternion.Euler(-degreesPerSlot * levelPosition, 0f, 0f);

        /// Tạo sẵn một bản module trong pool (màn Loading): lần đầu lăn tới không phải Instantiate.
        public void PrewarmModule(GameObject prefab) => PoolingManager.Prewarm(prefab, 1);

        /// Gắn module <paramref name="prefab"/> vào ô (null = để trống). Cùng prefab đang gắn thì giữ nguyên.
        public void SetModule(int slot, GameObject prefab)
        {
            if (slotModuleSources[slot] == prefab) return;
            ReleaseModule(slot);
            if (prefab == null) return;
            GameObject module = PoolingManager.Spawn(prefab, slots[slot].position, slots[slot].rotation, slots[slot]);
            slotModules[slot] = module;
            slotModuleSources[slot] = prefab;
            Transform pad = module.transform.Find(PadPath);
            if (pad == null)
            {
                Debug.LogError($"[BusOut] Module '{prefab.name}' không có con '{PadPath}': không đặt được xe / số level. " +
                               "Đặt lại tên bệ trong prefab.", prefab);
                return;
            }
            slotPads[slot] = pad;
            Transform doneMark = pad.Find(DoneMarkName);
            slotDoneMarks[slot] = doneMark != null ? doneMark.gameObject : null;
        }

        /// Số level trên bệ và lớp "đã qua". Ô trống (level &lt; 1) thì ẩn số.
        public void SetPad(int slot, int level, bool isDone)
        {
            TMP_Text label = labels[slot];
            Transform pad = slotPads[slot];
            bool hasPad = pad != null && level >= 1;
            label.gameObject.SetActive(hasPad);
            if (hasPad)
            {
                // worldPositionStays false: giữ local pose của prefab chữ = vị trí so với tâm bệ.
                if (label.transform.parent != pad) label.transform.SetParent(pad, false);
                label.SetText("{0}", level);
            }
            if (slotDoneMarks[slot] != null) slotDoneMarks[slot].SetActive(isDone);
        }

        /// Đặt xe lên bệ của ô, đúng tư thế dựng trong scene. Ô không có bệ → giấu xe.
        public void PlaceCar(int slot)
        {
            Transform pad = slotPads[slot];
            car.gameObject.SetActive(pad != null);
            if (pad == null) return;
            car.SetParent(pad, false);
            car.localPosition = carLocalPosition;
            car.localRotation = carLocalRotation;
            car.localScale = carLocalScale;
        }

        /// Bắt đầu chạy sang bệ kế: xe rời bệ, sang carPivot, giữ nguyên chỗ đang đứng trên màn hình.
        public void BeginCarAdvance()
        {
            carPivot.localRotation = Quaternion.identity;
            car.SetParent(carPivot, true);
        }

        /// Đã chạy được <paramref name="levelsMoved"/> level (số thực). Road quay −45°/level nên pivot quay +45°/level:
        /// xe đứng yên trên màn hình; chạy đủ 1 level thì xe nằm đúng bệ của ô kế.
        public void SetCarAdvance(float levelsMoved) =>
            carPivot.localRotation = Quaternion.Euler(degreesPerSlot * levelsMoved, 0f, 0f);

        /// Trời + mây trộn giữa hai chủ đề: <paramref name="blend"/> 0 = hẳn chủ đề đầu, 1 = hẳn chủ đề sau.
        /// Một chủ đề thì truyền cùng texture cho cả hai, blend 0.
        public void SetSky(Texture sky, Texture cloud, Texture nextSky, Texture nextCloud, float blend)
        {
            if (skybox == null) return;
            skybox.SetTexture(SkyTexId, sky);
            skybox.SetTexture(CloudTexId, cloud);
            skybox.SetTexture(BlendSkyTexId, nextSky);
            skybox.SetTexture(BlendCloudTexId, nextCloud);
            skybox.SetFloat(BlendFactorId, blend);
        }

        /// Trả module của ô về pool. ⚠️ Gỡ xe và số level ra TRƯỚC: chúng đang là con của bệ, đi theo module vào pool
        /// thì bị tắt và lần sau xe / số biến mất.
        private void ReleaseModule(int slot)
        {
            Transform pad = slotPads[slot];
            if (pad != null)
            {
                if (car.parent == pad)
                {
                    car.SetParent(slots[slot], false);
                    car.gameObject.SetActive(false);
                }
                if (labels[slot].transform.parent == pad)
                {
                    labels[slot].transform.SetParent(slots[slot], false);
                    labels[slot].gameObject.SetActive(false);
                }
                // Instance nằm trong pool chờ dùng lại cho level khác: không để sót lớp "đã qua".
                if (slotDoneMarks[slot] != null) slotDoneMarks[slot].SetActive(false);
            }
            if (slotModules[slot] != null) PoolingManager.Despawn(slotModules[slot]);
            slotModules[slot] = null;
            slotModuleSources[slot] = null;
            slotPads[slot] = null;
            slotDoneMarks[slot] = null;
        }

        /// ⚠️ Đổi texture trên BẢN SAO, không trên material của scene: trong Editor, sửa material asset lúc Play là ghi thẳng
        /// xuống file Skybox.mat (git thấy đổi mỗi lần chơi). Ánh sáng môi trường không tự tính lại khi đổi skybox lúc chạy —
        /// cảnh vẫn sáng theo skybox lúc bake / lúc mở scene.
        private void InitSkybox()
        {
            sceneSkybox = RenderSettings.skybox;
            if (sceneSkybox == null || !sceneSkybox.HasProperty(SkyTexId) || !sceneSkybox.HasProperty(BlendFactorId))
            {
                Debug.LogError("[BusOut] Skybox của scene không dùng shader BusOut/Skybox: trời không đổi theo chủ đề. " +
                               "Lighting → Environment → Skybox Material = Assets/_BusOut/Material/Skybox.mat.", this);
                return;
            }
            skybox = new Material(sceneSkybox) { name = $"{sceneSkybox.name} (runtime)" };
            // Bộ trời thứ hai (để chuyển dần sang chủ đề kế) dùng chung Tiling / Offset với bộ chính, không thì lúc chuyển
            // tranh nhảy chỗ.
            skybox.SetTextureScale(BlendSkyTexId, skybox.GetTextureScale(SkyTexId));
            skybox.SetTextureOffset(BlendSkyTexId, skybox.GetTextureOffset(SkyTexId));
            RenderSettings.skybox = skybox;
        }
    }
}
