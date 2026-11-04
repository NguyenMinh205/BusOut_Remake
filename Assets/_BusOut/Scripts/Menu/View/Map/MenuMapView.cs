using BusOut.DesignPattern;
using DG.Tweening;
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
        // Số level: bản prefab lồng LevelLabel làm con ParkingBase trong MỌI module (4/11 — trước đó tạo lúc chạy rồi chuyển
        // cha qua lại). Quầng "đã qua" (DoneAura) lồng sẵn dưới Base_Done nên bật / tắt theo nó, không cần code.
        private const string LabelName = "LevelLabel";
        // Thân xe Menu (con của xe): nhún / chúi riêng thân, không đụng scale của cả xe (đang phóng 1.1 lúc chạy).
        private const string CarBodyName = "Body";

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
        [Tooltip("Camera của Menu (HomeContext/Main Camera): để biết xe còn trong màn hình không (nút \"về level hiện tại\").")]
        [SerializeField] private Camera menuCamera;
        [Tooltip("Khói sau xe khi chạy sang bệ mới: bản CarSmoke (Prefab/VFX) làm con của xe. Trống = xe chạy không khói (báo lỗi).")]
        [SerializeField] private ParticleSystem carSmoke;

        private Transform[] slots;
        private GameObject[] slotModules;       // instance đang gắn ở ô (null = ô trống)
        private GameObject[] slotModuleSources; // prefab của instance đó, để biết có cần đổi không
        private Transform[] slotPads;
        private GameObject[] slotDoneMarks;
        private TMP_Text[] slotLabels;        // số level của module đang gắn ở ô (null = module thiếu LevelLabel)
        private readonly System.Collections.Generic.HashSet<GameObject> missingLabelReported =
            new System.Collections.Generic.HashSet<GameObject>();
        private Quaternion roadBaseRotation;
        private float degreesPerSlot;
        // Tư thế xe so với tâm bệ (dựng trong scene). Nhớ lại vì lúc chạy sang bệ mới xe tạm làm con của carPivot.
        private Vector3 carLocalPosition;
        private Quaternion carLocalRotation;
        private Vector3 carLocalScale;
        // Trục quay của xe khi chạy sang bệ mới: con của Road, cùng trục với trống. Quay pivot = xe chạy dọc mặt đường từ bệ
        // này sang bệ kế (xe tự chạy trên màn hình, không đứng yên như gốc); Road lăn thì xe đi theo đường.
        private Transform carPivot;
        private Vector3 carDriveBaseScale;
        private Tween carScaleTween;
        private bool hasCarSmoke;
        private Transform carBody;
        private Quaternion carBodyRestRotation;
        private Vector3 carBodyRestScale;
        private Tween carBodyTween;
        private ParticleSystem carIdleRings;     // null khi thiếu prefab
        private Material sceneSkybox;
        private Material skybox; // bản sao lúc chạy, null khi skybox scene không dùng shader BusOut/Skybox

        public bool Init(int slotCount, float degreesPerSlot)
        {
            string missing = "";
            if (road == null) missing += " Road";
            if (car == null) missing += " Car";
            if (missing.Length > 0)
            {
                Debug.LogError($"[BusOut] MenuMapView thiếu tham chiếu:{missing}. Kéo Map/Road và xe buýt vào.", this);
                return false;
            }
            this.degreesPerSlot = degreesPerSlot;
            roadBaseRotation = road.localRotation;
            slots = new Transform[slotCount];
            slotModules = new GameObject[slotCount];
            slotModuleSources = new GameObject[slotCount];
            slotPads = new Transform[slotCount];
            slotDoneMarks = new GameObject[slotCount];
            slotLabels = new TMP_Text[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                var slot = new GameObject($"Slot{i}").transform;
                slot.SetParent(road, false);
                slot.localRotation = Quaternion.Euler(degreesPerSlot * i, 0f, 0f);
                slots[i] = slot;
            }
            carLocalPosition = car.localPosition;
            carLocalRotation = car.localRotation;
            carLocalScale = car.localScale;
            carPivot = new GameObject("CarPivot").transform;
            carPivot.SetParent(road, false);
            car.gameObject.SetActive(false);
            hasCarSmoke = carSmoke != null;
            if (!hasCarSmoke)
                Debug.LogError("[BusOut] MenuMapView thiếu Car Smoke: xe Menu chạy sang bệ mới không có khói. Kéo Prefab/VFX/CarSmoke " +
                               "vào làm con của xe Menu rồi kéo vào ô Car Smoke.", this);
            // ⚠️ Khói mô phỏng World + phát theo quãng đường: để chạy là trống quay / vuốt bản đồ cũng phun khói theo xe.
            else carSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            carBody = car.Find(CarBodyName);
            if (carBody == null)
                Debug.LogError($"[BusOut] Xe Menu '{car.name}' không có con '{CarBodyName}': xe chạy không nhún / chúi. Đặt lại tên " +
                               "thân xe.", car);
            else
            {
                carBodyRestRotation = carBody.localRotation;
                carBodyRestScale = carBody.localScale;
            }
            if (menuCamera == null)
                Debug.LogError("[BusOut] MenuMapView thiếu Menu Camera: lăn bản đồ là nút \"về level hiện tại\" hiện ngay, kể cả " +
                               "khi xe còn trong màn hình. Kéo HomeContext/Main Camera vào.", this);
            InitSkybox();
            return true;
        }

        /// Vòng tỏa dưới gầm xe Menu (gọi một lần sau Init): con của xe, tư thế prefab = so với xe. Prefab null → không có
        /// (presenter đã báo lỗi).
        public void InitCarEffects(GameObject idleRingsPrefab)
        {
            if (idleRingsPrefab == null) return;
            carIdleRings = Instantiate(idleRingsPrefab, car, false).GetComponent<ParticleSystem>();
            if (carIdleRings == null)
                Debug.LogError($"[BusOut] Prefab '{idleRingsPrefab.name}' không có Particle System ở gốc: không có vòng dưới xe.",
                    idleRingsPrefab);
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

        /// Tạo sẵn một bản prefab trong pool (màn Loading: module, pháo sáng): lần đầu dùng không phải Instantiate.
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
            Transform label = pad.Find(LabelName);
            slotLabels[slot] = label != null ? label.GetComponent<TMP_Text>() : null;
            // Báo một lần mỗi prefab: module được gắn lại liên tục khi lướt bản đồ, báo mỗi lần là ngập Console.
            if (slotLabels[slot] == null && missingLabelReported.Add(prefab))
                Debug.LogError($"[BusOut] Module '{prefab.name}' không có '{PadPath}/{LabelName}' (TextMeshPro): bệ không có số level. " +
                               "Kéo prefab Prefab/Menu/LevelLabel vào làm con của ParkingBase.", prefab);
        }

        /// Số level trên bệ và lớp "đã qua" (kèm quầng sáng lồng sẵn trong Base_Done). Ô không có module thì không có gì.
        public void SetPad(int slot, int level, bool isDone)
        {
            TMP_Text label = slotLabels[slot];
            if (label != null && level >= 1) label.SetText("{0}", level);
            // Quầng "đã qua" (DoneAura) là con của Base_Done → bật / tắt theo.
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

        /// Bắt đầu chạy sang bệ kế: xe rời bệ sang carPivot (giữ nguyên chỗ đứng), bật khói, phóng to <paramref name="scale"/>.
        public void BeginCarDrive(float scale, float scaleDuration)
        {
            carPivot.localRotation = Quaternion.identity;
            car.SetParent(carPivot, true);
            carDriveBaseScale = car.localScale;
            KillCarScale();
            carScaleTween = car.DOScale(carDriveBaseScale * scale, scaleDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(car.gameObject)
                .SetTarget(car);
            if (hasCarSmoke) carSmoke.Play(true);
            // Quy tắc "một thứ lặp": lúc chạy thì chuyển động của xe là thứ đó — tắt vòng tỏa dưới gầm.
            if (carIdleRings != null) carIdleRings.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// Dáng thân xe lúc chạy: <paramref name="tiltDegrees"/> dương = ngửa đầu (tăng tốc), âm = chúi đầu (phanh);
        /// <paramref name="squash"/> = phần chiều cao bị nén (nhún). Chỉ thân xe, không đụng scale cả xe.
        public void SetCarBody(float tiltDegrees, float squash)
        {
            if (carBody == null) return;
            carBody.localRotation = carBodyRestRotation * Quaternion.Euler(-tiltDegrees, 0f, 0f);
            Vector3 scale = carBodyRestScale;
            scale.y *= 1f - squash;
            carBody.localScale = scale;
        }

        /// Sắp tới bệ: tắt khói, thu dần về cỡ đỗ. ⚠️ Gọi TRƯỚC khi xe dừng hẳn (presenter tính giờ): PlaceCar chuyển xe sang
        /// bệ và đặt lại scale theo hệ toạ độ bệ — tween scale cũ (theo hệ carPivot) còn chạy là ghi đè sai cỡ.
        public void EndCarMove(float scaleDuration)
        {
            KillCarScale();
            carScaleTween = car.DOScale(carDriveBaseScale, scaleDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(car.gameObject)
                .SetTarget(car);
            if (hasCarSmoke) carSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// Đã đỗ ở bệ mới: thân xe nảy một cái, vòng tỏa dưới gầm chạy lại.
        public void PlayCarArrive(float punch, float duration)
        {
            ResetCarBody();
            if (carBody != null)
                carBodyTween = carBody.DOPunchScale(carBodyRestScale * punch, duration, 1, 0.5f)
                    .SetLink(carBody.gameObject)
                    .SetTarget(carBody)
                    .OnKill(ResetCarBodyScale);
            if (carIdleRings != null) carIdleRings.Play(true);
        }

        private void ResetCarBody()
        {
            if (carBodyTween != null && carBodyTween.IsActive()) carBodyTween.Kill();
            carBodyTween = null;
            if (carBody == null) return;
            carBody.localRotation = carBodyRestRotation;
            carBody.localScale = carBodyRestScale;
        }

        private void ResetCarBodyScale()
        {
            if (carBody != null) carBody.localScale = carBodyRestScale;
        }

        /// Xe đã chạy được <paramref name="levelsDriven"/> level dọc mặt đường (số thực, so với bệ lúc bắt đầu). Pivot quay
        /// +45°/level quanh trục trống = đúng hướng các ô: chạy đủ 1 level thì xe nằm đúng bệ của ô kế, dù Road có đang lăn.
        public void SetCarDrive(float levelsDriven) =>
            carPivot.localRotation = Quaternion.Euler(degreesPerSlot * levelsDriven, 0f, 0f);

        /// Hết chạy (xong hẳn, hoặc bị cắt ngang): tắt khói (khói đã phun tự tan), bỏ phóng to, thân xe về dáng đứng, vòng
        /// tỏa dưới gầm chạy lại (xe đứng yên). Gọi bao nhiêu lần cũng được.
        public void EndCarDrive()
        {
            KillCarScale();
            ResetCarBody();
            if (hasCarSmoke) carSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (carIdleRings != null && !carIdleRings.isPlaying) carIdleRings.Play(true);
        }

        /// Pháo sáng ở bệ của ô <paramref name="slot"/> (bệ vừa qua). Tư thế của prefab = so với tâm bệ; làm con của ô
        /// (không phải của bệ): module bị trả về pool giữa chừng thì hiệu ứng không bị kéo theo vào pool.
        public void PlayLevelPassEffect(int slot, GameObject prefab)
        {
            Transform pad = slotPads[slot];
            if (pad == null || prefab == null) return;
            Transform local = prefab.transform;
            PoolingManager.Spawn(prefab, pad.TransformPoint(local.localPosition), pad.rotation * local.localRotation, slots[slot]);
        }

        private void KillCarScale()
        {
            if (carScaleTween != null && carScaleTween.IsActive()) carScaleTween.Kill();
            carScaleTween = null;
        }

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

        /// Trả module của ô về pool. ⚠️ Gỡ xe ra TRƯỚC: xe đang là con của bệ, đi theo module vào pool thì bị tắt và lần sau
        /// xe biến mất. (Số level và quầng là một phần của module, đi theo module là đúng.)
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
                // Instance nằm trong pool chờ dùng lại cho level khác: không để sót lớp "đã qua".
                if (slotDoneMarks[slot] != null) slotDoneMarks[slot].SetActive(false);
            }
            if (slotModules[slot] != null) PoolingManager.Despawn(slotModules[slot]);
            slotModules[slot] = null;
            slotModuleSources[slot] = null;
            slotPads[slot] = null;
            slotDoneMarks[slot] = null;
            slotLabels[slot] = null;
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
