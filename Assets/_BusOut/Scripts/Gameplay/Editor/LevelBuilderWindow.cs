using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BusOut.Core;
using BusOut.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace BusOut.Gameplay.Editor
{
    /// Cửa sổ dựng level riêng, nhìn bãi từ trên xuống (công cụ dùng lâu dài, không vào bản build).
    /// Lưu mỗi level một file trong Resources/Levels; level trùng số với level trong curve sẽ được game ưu tiên.
    public class LevelBuilderWindow : EditorWindow
    {
        #region Hằng số, trạng thái

        private const string LevelsFolder = "Assets/_BusOut/Resources/" + LevelCurveData.OwnLevelsFolder;
        private const string ConfigPath = "Assets/_BusOut/Resources/GameplayConfig.asset";
        private const string PrefabFolder = "Assets/_BusOut/Prefab/GameObject/";
        private const float SidebarWidth = 300f;
        private const float CanvasPadding = 24f;
        // ⚠️ Số nhỏ sẽ đè đúng level đang chơi (level riêng được ưu tiên), nên level mới mặc định nằm ngoài tầm chơi.
        // Dựng và thử xong mới đổi sang số muốn thay.
        private const int DefaultFirstLevelNumber = 1000;

        private static readonly string[] TypeLabels = { "4 chỗ", "6 chỗ", "10 chỗ" };
        private static readonly VehicleType[] PlaceableTypes = { VehicleType.FourSeat, VehicleType.SixSeat, VehicleType.TenSeat };
        private static readonly string[] PrefabNames = { "Car", "Van", "Bus" };

        private enum DragMode { None, MoveCars, BoxSelect }

        // Phần dữ liệu để [SerializeField]: Undo của Unity ghi lại đúng phần này, và công việc chưa lưu sống qua lúc biên dịch lại.
        [SerializeField] private LevelCurveData file;
        [SerializeField] private int levelCursor;
        // ⚠️ Chỉ xoá file của level người dùng đã xoá / đổi số. Xoá theo "file không có trong cửa sổ" thì cửa sổ mở từ trước khi có
        // file mới (git pull, sinh ngoài) sẽ xoá nhầm cả loạt level.
        [SerializeField] private List<int> removedLevelNumbers = new List<int>();

        private GameplayConfig config;
        private readonly CarShape[] shapes = new CarShape[PlaceableTypes.Length];
        private string setupError;

        private readonly HashSet<int> selection = new HashSet<int>();
        private readonly List<CarFootprint> footprints = new List<CarFootprint>();
        private LevelReport report;

        private VehicleType placeType = VehicleType.FourSeat;
        private int placeColor;
        private float placeAngle;
        private float rotateStep = 15f;
        private bool snapEnabled = true;
        private float snapStep = 0.25f;
        private int newLevelNumber = DefaultFirstLevelNumber;

        private LevelGeneratorConfig generatorConfig;
        private int generatorProfileIndex;
        private int generatorSeed = 1;
        private int batchCount = 10;
        private string generatorInfo;
        private bool generateHiddenCars;
        private bool generateHiddenPassengers;
        private bool generateLocks;
        private bool generateConstruction;
        private bool generateGarages;

        private Rect canvas;
        private float pixelsPerUnit = 1f;
        private DragMode dragMode;
        private Vector2 dragStartLevel;
        private Vector2 dragCurrentLevel;
        private Vector2 appliedDelta;
        private bool dragMoved;
        private bool dragAdditive;
        private int collapseToOnClick = -1;
        private int undoGroup;
        private readonly Dictionary<int, Vector2> dragStartPositions = new Dictionary<int, Vector2>();

        private Vector2 sidebarScroll;
        private GUIStyle seatLabelStyle;
        private readonly Vector2[] cornerBuffer = new Vector2[4];
        private readonly Vector3[] polygonBuffer = new Vector3[4];
        private readonly Vector3[] outlineBuffer = new Vector3[5];
        private readonly Vector3[] arrowBuffer = new Vector3[3];

        private LevelData CurrentLevel =>
            file?.levels != null && levelCursor >= 0 && levelCursor < file.levels.Length ? file.levels[levelCursor] : null;

        [MenuItem("Tools/BusOut Tools/Level Builder...")]
        private static void Open()
        {
            var window = GetWindow<LevelBuilderWindow>("Level Builder");
            window.minSize = new Vector2(820f, 560f);
        }

        private void OnEnable()
        {
            saveChangesMessage = "Level chưa lưu. Lưu trước khi đóng?";
            Undo.undoRedoPerformed += OnUndoRedo;
            LoadSetup();
            // Unity có thể dựng sẵn file rỗng lúc khôi phục cửa sổ, nên "chưa có level" cũng phải đọc đĩa.
            if (file?.levels == null || file.levels.Length == 0) ReadFile();
            else RefreshUnsavedFlag();
            report = null;
        }

        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        private void OnUndoRedo()
        {
            levelCursor = file.levels.Length == 0 ? 0 : Mathf.Clamp(levelCursor, 0, file.levels.Length - 1);
            int count = CurrentLevel?.vehicles?.Length ?? 0;
            selection.RemoveWhere(index => index >= count);
            report = null;
            RefreshUnsavedFlag();
            Repaint();
        }

        /// So với đĩa thay vì đặt cờ thẳng: sự kiện Undo bắn cho cả thao tác ngoài cửa sổ này (vd. undo trong scene), và Undo
        /// có thể quay về trước lần Lưu, lúc đó đĩa đã khác dữ liệu đang mở.
        private void RefreshUnsavedFlag() => hasUnsavedChanges = JsonUtility.ToJson(file) != JsonUtility.ToJson(ReadFromDisk());

        private static string PathOf(int levelNumber) => $"{LevelsFolder}/level_{levelNumber:000}.json";

        private static LevelCurveData ReadFromDisk()
        {
            var levels = new List<LevelData>();
            if (Directory.Exists(LevelsFolder))
                foreach (string path in Directory.GetFiles(LevelsFolder, "level_*.json"))
                {
                    LevelData level = JsonUtility.FromJson<LevelData>(File.ReadAllText(path));
                    if (level != null) levels.Add(level);
                }
            levels.Sort((a, b) => a.index.CompareTo(b.index));
            return new LevelCurveData { curve = "custom", levels = levels.ToArray() };
        }

        /// Kích thước đọc từ prefab + config thật, để không có bản số thứ hai lệch với game.
        private void LoadSetup()
        {
            setupError = null;
            config = AssetDatabase.LoadAssetAtPath<GameplayConfig>(ConfigPath);
            if (config == null)
            {
                setupError = $"Không thấy {ConfigPath}.";
                Debug.LogError($"[BusOut] Level Builder: {setupError} Tạo lại GameplayConfig ở đúng đường dẫn đó.");
                return;
            }
            for (int i = 0; i < PlaceableTypes.Length; i++)
            {
                string path = PrefabFolder + PrefabNames[i] + ".prefab";
                if (TryReadShape(path, config.carScale, out shapes[i])) continue;
                setupError = $"Prefab {path} thiếu CarView hoặc ô Body Collider.";
                Debug.LogError($"[BusOut] Level Builder: {setupError} Kiểm prefab rồi bấm Thử lại trên cửa sổ.");
                return;
            }
        }

        private static bool TryReadShape(string prefabPath, float carScale, out CarShape shape)
        {
            shape = default;
            var view = AssetDatabase.LoadAssetAtPath<CarView>(prefabPath);
            if (view == null) return false;
            var body = new SerializedObject(view).FindProperty("bodyCollider").objectReferenceValue as BoxCollider;
            if (body == null) return false;
            Transform root = view.transform;
            Transform bodyTransform = body.transform;
            Vector3 lossy = bodyTransform.lossyScale;
            Vector3 center = root.InverseTransformPoint(bodyTransform.TransformPoint(body.center));
            shape = new CarShape
            {
                HalfWidth = body.size.x * 0.5f * lossy.x * carScale,
                HalfLength = body.size.z * 0.5f * lossy.z * carScale,
                CenterRight = center.x * root.localScale.x * carScale,
                CenterForward = center.z * root.localScale.z * carScale,
            };
            return true;
        }

        #endregion

        #region Đọc, ghi file

        private void ReadFile()
        {
            file = ReadFromDisk();
            removedLevelNumbers.Clear();
            foreach (LevelData level in file.levels)
            {
                if (level.vehicles == null) level.vehicles = new VehicleData[0];
                if (level.mix == null) level.mix = new MixData[0];
                if (level.hardPoints == null) level.hardPoints = new HardPointData[0];
            }
            levelCursor = 0;
            selection.Clear();
            report = null;
            hasUnsavedChanges = false;
        }

        /// Trả về false nếu người dùng huỷ vì level có vấn đề.
        private bool WriteFile()
        {
            string problems = FindSaveProblems();
            if (problems.Length > 0 && !EditorUtility.DisplayDialog("Level có vấn đề", problems + "\nVẫn lưu?", "Vẫn lưu", "Huỷ"))
                return false;

            int currentNumber = CurrentLevel?.index ?? -1;
            Array.Sort(file.levels, (a, b) => a.index.CompareTo(b.index));
            levelCursor = Mathf.Max(0, Array.FindIndex(file.levels, level => level.index == currentNumber));

            foreach (LevelData level in file.levels)
                foreach (GarageLayout garage in BuildGarageLayouts(level))
                {
                    VehicleData front = level.vehicles[garage.CarIds[0]];
                    for (int k = 1; k < garage.CarIds.Length; k++)
                    {
                        VehicleData queued = level.vehicles[garage.CarIds[k]];
                        queued.x = front.x;
                        queued.z = front.z;
                        queued.rotY = front.rotY;
                    }
                }
            Directory.CreateDirectory(LevelsFolder);
            int written = 0;
            foreach (LevelData level in file.levels)
            {
                string path = PathOf(level.index);
                string json = JsonUtility.ToJson(level, true);
                // So theo nội dung, không so chữ: file sinh ngoài Unity khác định dạng số nhưng cùng dữ liệu thì không ghi lại.
                if (File.Exists(path) && JsonUtility.ToJson(JsonUtility.FromJson<LevelData>(File.ReadAllText(path)), true) == json) continue;
                File.WriteAllText(path, json, new UTF8Encoding(false));
                written++;
            }
            foreach (int number in removedLevelNumbers)
                if (!Array.Exists(file.levels, level => level.index == number)) AssetDatabase.DeleteAsset(PathOf(number));
            removedLevelNumbers.Clear();
            AssetDatabase.Refresh();
            hasUnsavedChanges = false;
            Debug.Log($"[BusOut] Level Builder: {file.levels.Length} level, ghi {written} file vào {LevelsFolder}.");
            return true;
        }

        /// Hai lỗi làm ván hỏng thật: level trống thì vào là thắng ngay, xe kẹt vòng thì không bao giờ thắng được.
        private string FindSaveProblems()
        {
            var text = new StringBuilder();
            var cars = new List<CarFootprint>();
            foreach (LevelData level in file.levels)
            {
                if (level.vehicles.Length == 0)
                {
                    text.AppendLine($"Level {level.index}: chưa có xe (vào là thắng ngay).");
                    continue;
                }
                BuildFootprints(level, cars);
                int stuck = LevelBuilderRules.Analyze(cars, config.levelDataMin, config.levelDataMax, config.blockerRayInset,
                    BuildLockEdges(level), BuildGarageLayouts(level)).Deadlocked.Count;
                if (stuck > 0) text.AppendLine($"Level {level.index}: {stuck} xe kẹt vòng, không ra được.");
            }
            return text.ToString();
        }

        public override void SaveChanges()
        {
            if (WriteFile()) base.SaveChanges();
        }

        private void WriteLevelToSave(int number)
        {
            SecureGameData data = BusOutTools.LoadPlayerSave();
            data.currentLevel = number;
            data.Save();
            Debug.Log($"[BusOut] Level Builder: đã đặt level {number} vào save. Mở LoadingScene rồi bấm Play.");
        }

        #endregion

        #region Sửa dữ liệu

        /// Gọi NGAY TRƯỚC khi đổi dữ liệu: Undo chỉ ghi được thay đổi xảy ra sau lệnh này.
        private void RecordChange(string actionName) => Undo.RecordObject(this, actionName);

        private void MarkChanged()
        {
            hasUnsavedChanges = true;
            report = null;
            Repaint();
        }

        private float Snap(float value)
        {
            if (snapEnabled) return Mathf.Round(value / Mathf.Max(0.01f, snapStep)) * Mathf.Max(0.01f, snapStep);
            return Mathf.Round(value * 100f) / 100f;
        }

        private void PlaceCar(LevelData level, Vector2 levelPoint)
        {
            RecordChange("Đặt xe");
            var car = new VehicleData
            {
                type = (int)placeType,
                color = placeColor,
                x = Snap(levelPoint.x),
                z = Snap(levelPoint.y),
                rotY = placeAngle,
                // ⚠️ Phải là -1 (đỗ tự do): để mặc định 0 thì game coi là xe nằm trong công cụ và bỏ qua không spawn.
                tool = -1,
            };
            ArrayUtility.Add(ref level.vehicles, car);
            selection.Clear();
            selection.Add(level.vehicles.Length - 1);
            MarkChanged();
        }

        private void DeleteSelected(LevelData level)
        {
            if (selection.Count == 0) return;
            RecordChange("Xoá xe");
            var kept = new List<VehicleData>();
            for (int i = 0; i < level.vehicles.Length; i++)
                if (!selection.Contains(i)) kept.Add(level.vehicles[i]);
            level.vehicles = kept.ToArray();
            selection.Clear();
            MarkChanged();
        }

        private void DuplicateSelected(LevelData level)
        {
            if (selection.Count == 0) return;
            RecordChange("Nhân bản xe");
            var copies = new List<VehicleData>();
            foreach (int index in selection)
            {
                VehicleData source = level.vehicles[index];
                copies.Add(new VehicleData
                {
                    type = source.type, color = source.color, x = source.x + 1f, z = source.z - 1f,
                    rotY = source.rotY, hidden = source.hidden, autoMove = source.autoMove, tool = source.tool,
                    // Không chép khoá / chìa: bản sao mang cùng số là thành 2 chìa cho một ổ khoá.
                    colorHidden = source.colorHidden,
                    // Không chép bãi chờ: bản sao cùng số bãi là thành một xe chờ nằm sai chỗ.
                });
            }
            int firstCopy = level.vehicles.Length;
            var all = new List<VehicleData>(level.vehicles);
            all.AddRange(copies);
            level.vehicles = all.ToArray();
            selection.Clear();
            for (int i = firstCopy; i < level.vehicles.Length; i++) selection.Add(i);
            MarkChanged();
        }

        private void ApplyToSelected(LevelData level, string actionName, Action<VehicleData> change)
        {
            if (level == null || selection.Count == 0) return;
            RecordChange(actionName);
            foreach (int index in selection) change(level.vehicles[index]);
            MarkChanged();
        }

        private void RotateSelected(LevelData level, float degrees) =>
            ApplyToSelected(level, "Xoay xe", car => car.rotY = Mathf.Repeat(car.rotY + degrees, 360f));

        private void AddLevel(int number, LevelData copyFrom)
        {
            if (Array.Exists(file.levels, level => level.index == number))
            {
                EditorUtility.DisplayDialog("Trùng số level", $"Đã có level {number} trong file riêng.", "OK");
                return;
            }
            RecordChange("Thêm level");
            string json = copyFrom != null ? JsonUtility.ToJson(copyFrom) : null;
            LevelData created = json != null ? JsonUtility.FromJson<LevelData>(json) : new LevelData
            {
                vehicles = new VehicleData[0], mix = new MixData[0], hardPoints = new HardPointData[0],
            };
            created.index = number;
            ArrayUtility.Add(ref file.levels, created);
            newLevelNumber = number + 1;
            levelCursor = file.levels.Length - 1;
            selection.Clear();
            MarkChanged();
        }

        private void SelectLevel(int cursor)
        {
            levelCursor = Mathf.Clamp(cursor, 0, Mathf.Max(0, file.levels.Length - 1));
            selection.Clear();
            report = null;
            GUI.FocusControl(null);
        }

        private void BuildFootprints(LevelData level, List<CarFootprint> result)
        {
            result.Clear();
            foreach (VehicleData car in level.vehicles)
                result.Add(LevelBuilderRules.BuildFootprint(car.x, car.z, car.rotY, ShapeOf(car)));
        }

        /// Cạnh khoá giống LevelSpawner.BuildBlockingGraph: mọi xe mang chìa cùng số "chắn" xe bị khoá.
        private static List<Vector2Int> BuildLockEdges(LevelData level)
        {
            var edges = new List<Vector2Int>();
            for (int i = 0; i < level.vehicles.Length; i++)
            {
                int lockId = level.vehicles[i].lockId;
                if (lockId <= 0) continue;
                for (int j = 0; j < level.vehicles.Length; j++)
                    if (j != i && level.vehicles[j].keyId == lockId) edges.Add(new Vector2Int(i, j));
            }
            return edges;
        }

        /// Bãi chờ của level: xe theo garageOrder, xe đầu = xe đứng trước cổng; hình cổng theo GameplayConfig (đổi sang đơn vị level).
        private List<GarageLayout> BuildGarageLayouts(LevelData level)
        {
            var layouts = new List<GarageLayout>();
            var ids = new SortedDictionary<int, List<int>>();
            for (int i = 0; i < level.vehicles.Length; i++)
            {
                int garageId = level.vehicles[i].garageId;
                if (garageId <= 0) continue;
                if (!ids.TryGetValue(garageId, out List<int> list)) ids[garageId] = list = new List<int>();
                list.Add(i);
            }
            foreach (List<int> list in ids.Values)
            {
                list.Sort((a, b) => level.vehicles[a].garageOrder.CompareTo(level.vehicles[b].garageOrder));
                VehicleData front = level.vehicles[list[0]];
                layouts.Add(new GarageLayout
                {
                    CarIds = list.ToArray(),
                    Building = LevelBuilderRules.BuildGarageBuilding(front.x, front.z, front.rotY, config.garageBuildingOffset * config.carScale,
                        config.garageBuildingLength * config.carScale, config.garageBuildingWidth * config.carScale),
                });
            }
            return layouts;
        }

        /// Xe đang chờ trong cổng (không phải xe đứng trước cổng): không vẽ, không chọn được trên khung bãi.
        private static bool IsQueued(LevelData level, int index)
        {
            VehicleData car = level.vehicles[index];
            if (car.garageId <= 0) return false;
            foreach (VehicleData other in level.vehicles)
                if (other != car && other.garageId == car.garageId && other.garageOrder < car.garageOrder) return true;
            return false;
        }

        private CarShape ShapeOf(VehicleData car) => shapes[Mathf.Clamp(car.type, 0, shapes.Length - 1)];

        private void EnsureReport(LevelData level)
        {
            if (report != null) return;
            BuildFootprints(level, footprints);
            report = LevelBuilderRules.Analyze(footprints, config.levelDataMin, config.levelDataMax, config.blockerRayInset, BuildLockEdges(level),
                BuildGarageLayouts(level));
        }

        #endregion

        #region Vẽ khung chính

        private void OnGUI()
        {
            if (setupError != null)
            {
                EditorGUILayout.HelpBox(setupError, MessageType.Error);
                if (GUILayout.Button("Thử lại")) LoadSetup();
                return;
            }
            canvas = new Rect(SidebarWidth, 0f, Mathf.Max(1f, position.width - SidebarWidth), position.height);
            Vector2 frameSize = config.levelDataMax - config.levelDataMin;
            pixelsPerUnit = Mathf.Max(1f, Mathf.Min((canvas.width - CanvasPadding * 2f) / frameSize.x,
                (canvas.height - CanvasPadding * 2f) / frameSize.y));

            LevelData level = CurrentLevel;
            if (level != null)
            {
                EnsureReport(level);
                HandleCommands(level);
                HandleMouse(level);
                level = CurrentLevel;
                if (level != null) EnsureReport(level);
            }
            if (Event.current.type == EventType.Repaint) DrawCanvas(level);
            DrawSidebar(CurrentLevel);
        }

        private Vector2 ToScreen(Vector2 point)
        {
            Vector2 size = (config.levelDataMax - config.levelDataMin) * pixelsPerUnit;
            float left = canvas.x + (canvas.width - size.x) * 0.5f;
            float bottom = canvas.yMax - (canvas.height - size.y) * 0.5f;
            return new Vector2(left + (point.x - config.levelDataMin.x) * pixelsPerUnit,
                bottom - (point.y - config.levelDataMin.y) * pixelsPerUnit);
        }

        private Vector2 ToLevel(Vector2 screen)
        {
            Vector2 size = (config.levelDataMax - config.levelDataMin) * pixelsPerUnit;
            float left = canvas.x + (canvas.width - size.x) * 0.5f;
            float bottom = canvas.yMax - (canvas.height - size.y) * 0.5f;
            return new Vector2(config.levelDataMin.x + (screen.x - left) / pixelsPerUnit,
                config.levelDataMin.y + (bottom - screen.y) / pixelsPerUnit);
        }

        private void DrawCanvas(LevelData level)
        {
            EditorGUI.DrawRect(canvas, new Color(0.16f, 0.16f, 0.17f));
            Vector2 min = ToScreen(new Vector2(config.levelDataMin.x, config.levelDataMax.y));
            Vector2 max = ToScreen(new Vector2(config.levelDataMax.x, config.levelDataMin.y));
            EditorGUI.DrawRect(Rect.MinMaxRect(min.x, min.y, max.x, max.y), new Color(0.28f, 0.28f, 0.3f));
            DrawGrid();

            if (level == null)
            {
                GUI.Label(new Rect(canvas.x + 20f, 20f, 400f, 20f), "Chưa có level: bấm Thêm level ở bên trái.", EditorStyles.whiteLabel);
                return;
            }
            foreach (GarageLayout garage in BuildGarageLayouts(level)) DrawGarage(garage);
            for (int i = 0; i < level.vehicles.Length; i++)
                if (!IsQueued(level, i)) DrawCar(i, level.vehicles[i], footprints[i]);
            foreach (int index in selection) DrawExitRays(footprints[index]);
            foreach (Vector2Int edge in BuildLockEdges(level))
            {
                Handles.color = config.GetLockPairColor(level.vehicles[edge.x].lockId);
                Handles.DrawDottedLine(ToScreen(footprints[edge.y].Center), ToScreen(footprints[edge.x].Center), 4f);
            }

            if (dragMode == DragMode.BoxSelect)
            {
                Vector2 a = ToScreen(dragStartLevel);
                Vector2 b = ToScreen(dragCurrentLevel);
                Rect box = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                Handles.DrawSolidRectangleWithOutline(box, new Color(1f, 1f, 1f, 0.08f), Color.white);
            }
            GUI.Label(new Rect(canvas.x + 8f, canvas.yMax - 22f, canvas.width - 16f, 20f),
                "Chuột phải: đặt xe · Kéo nền: chọn nhiều · Shift: thêm bớt · Kéo xe: di chuyển · R / Shift+R: xoay · Ctrl+D: nhân bản · Del: xoá · Ctrl+Z: hoàn tác",
                EditorStyles.miniLabel);
        }

        private void DrawGrid()
        {
            Handles.color = new Color(1f, 1f, 1f, 0.05f);
            for (float x = Mathf.Ceil(config.levelDataMin.x); x <= config.levelDataMax.x; x += 1f)
                Handles.DrawLine(ToScreen(new Vector2(x, config.levelDataMin.y)), ToScreen(new Vector2(x, config.levelDataMax.y)));
            for (float z = Mathf.Ceil(config.levelDataMin.y); z <= config.levelDataMax.y; z += 1f)
                Handles.DrawLine(ToScreen(new Vector2(config.levelDataMin.x, z)), ToScreen(new Vector2(config.levelDataMax.x, z)));
        }

        private void DrawCar(int index, VehicleData car, CarFootprint footprint)
        {
            footprint.GetCorners(cornerBuffer);
            for (int i = 0; i < 4; i++) polygonBuffer[i] = outlineBuffer[i] = ToScreen(cornerBuffer[i]);
            outlineBuffer[4] = outlineBuffer[0];

            Handles.color = ColorOf(car.color);
            Handles.DrawAAConvexPolygon(polygonBuffer);

            // Mũi tên trắng = chạm là ra ngay; xám = đang bị chặn.
            Vector2 tip = footprint.Center + footprint.Forward * (footprint.HalfLength * 0.8f);
            arrowBuffer[0] = ToScreen(tip);
            arrowBuffer[1] = ToScreen(footprint.Center + footprint.Right * (footprint.HalfWidth * 0.65f));
            arrowBuffer[2] = ToScreen(footprint.Center - footprint.Right * (footprint.HalfWidth * 0.65f));
            Handles.color = report.FreeNow.Contains(index) ? Color.white : new Color(0.25f, 0.25f, 0.25f);
            Handles.DrawAAConvexPolygon(arrowBuffer);

            Handles.color = OutlineOf(index, out float thickness);
            Handles.DrawAAPolyLine(thickness, outlineBuffer);

            if (seatLabelStyle == null)
                seatLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.black } };
            Vector2 labelCenter = ToScreen(footprint.Center - footprint.Forward * (footprint.HalfLength * 0.55f));
            string label = car.colorHidden ? "?" : CarModel.SeatsOf((VehicleType)car.type).ToString();
            if (car.lockId > 0) label += " L" + car.lockId;
            if (car.keyId > 0) label += " K" + car.keyId;
            GUI.Label(new Rect(labelCenter.x - 30f, labelCenter.y - 8f, 60f, 16f), label, seatLabelStyle);
        }

        private void DrawGarage(GarageLayout garage)
        {
            garage.Building.GetCorners(cornerBuffer);
            for (int i = 0; i < 4; i++) polygonBuffer[i] = outlineBuffer[i] = ToScreen(cornerBuffer[i]);
            outlineBuffer[4] = outlineBuffer[0];
            Handles.color = new Color(0.55f, 0.5f, 0.42f);
            Handles.DrawAAConvexPolygon(polygonBuffer);
            Handles.color = Color.black;
            Handles.DrawAAPolyLine(2f, outlineBuffer);
            Vector2 center = ToScreen(garage.Building.Center);
            string label = $"G{CurrentLevel.vehicles[garage.CarIds[0]].garageId} ×{garage.CarIds.Length - 1}";
            GUI.Label(new Rect(center.x - 30f, center.y - 8f, 60f, 16f), label, seatLabelStyle ?? EditorStyles.miniBoldLabel);
        }

        private Color OutlineOf(int index, out float thickness)
        {
            thickness = 3f;
            if (selection.Contains(index)) return Color.yellow;
            if (report.Deadlocked.Contains(index)) return Color.magenta;
            if (report.Overlapping.Contains(index)) return Color.red;
            if (report.OutOfFrame.Contains(index)) return new Color(1f, 0.55f, 0f);
            thickness = 1.5f;
            return new Color(0f, 0f, 0f, 0.6f);
        }

        private void DrawExitRays(CarFootprint footprint)
        {
            Handles.color = new Color(1f, 1f, 0f, 0.45f);
            float offset = Mathf.Max(0f, footprint.HalfWidth - config.blockerRayInset);
            for (int side = -1; side <= 1; side++)
            {
                Vector2 origin = footprint.Center + footprint.Right * (offset * side);
                Handles.DrawLine(ToScreen(origin), ToScreen(origin + footprint.Forward * 40f));
            }
        }

        private Color ColorOf(int colorIndex)
        {
            List<Color> colors = config.carColors;
            return colors != null && colorIndex >= 0 && colorIndex < colors.Count ? colors[colorIndex] : Color.gray;
        }

        #endregion

        #region Thao tác chuột, phím

        private void HandleCommands(LevelData level)
        {
            // Đang gõ số trong ô nhập thì Delete, Ctrl+A... phải thuộc về ô nhập.
            if (EditorGUIUtility.editingTextField) return;
            Event e = Event.current;
            if (e.type == EventType.ValidateCommand && (e.commandName == "SelectAll" || e.commandName == "Duplicate"
                                                        || e.commandName == "Delete" || e.commandName == "SoftDelete"))
            {
                e.Use();
                return;
            }
            if (e.type == EventType.ExecuteCommand)
            {
                switch (e.commandName)
                {
                    case "SelectAll":
                        selection.Clear();
                        for (int i = 0; i < level.vehicles.Length; i++) selection.Add(i);
                        e.Use();
                        break;
                    case "Duplicate":
                        DuplicateSelected(level);
                        e.Use();
                        break;
                    case "Delete":
                    case "SoftDelete":
                        DeleteSelected(level);
                        e.Use();
                        break;
                }
                return;
            }
            if (e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            {
                DeleteSelected(level);
                e.Use();
            }
            else if (e.keyCode == KeyCode.R)
            {
                float degrees = e.shift ? -rotateStep : rotateStep;
                if (selection.Count > 0) RotateSelected(level, degrees);
                else placeAngle = Mathf.Repeat(placeAngle + degrees, 360f);
                e.Use();
            }
        }

        private void HandleMouse(LevelData level)
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            Event e = Event.current;
            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (!canvas.Contains(e.mousePosition)) return;
                    // Bỏ focus ô nhập, không thì phím Delete / R vẫn bị ô nhập giữ.
                    GUI.FocusControl(null);
                    EditorGUIUtility.editingTextField = false;
                    if (e.button == 1) OnRightClick(level, ToLevel(e.mousePosition));
                    else if (e.button == 0)
                    {
                        OnLeftDown(ToLevel(e.mousePosition), e.shift);
                        GUIUtility.hotControl = controlId;
                    }
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId) return;
                    OnDrag(level, ToLevel(e.mousePosition));
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != controlId) return;
                    GUIUtility.hotControl = 0;
                    OnLeftUp();
                    e.Use();
                    break;
            }
        }

        private int PickCar(Vector2 point)
        {
            // Duyệt ngược: xe vẽ sau nằm trên, phải được chọn trước.
            for (int i = footprints.Count - 1; i >= 0; i--)
                if (footprints[i].Contains(point) && !IsQueued(CurrentLevel, i)) return i;
            return -1;
        }

        private void OnRightClick(LevelData level, Vector2 point)
        {
            if (PickCar(point) < 0) PlaceCar(level, point);
        }

        private void OnLeftDown(Vector2 point, bool additive)
        {
            dragStartLevel = dragCurrentLevel = point;
            appliedDelta = Vector2.zero;
            dragMoved = false;
            collapseToOnClick = -1;
            int hit = PickCar(point);
            if (hit < 0)
            {
                dragMode = DragMode.BoxSelect;
                dragAdditive = additive;
                if (!additive) selection.Clear();
                return;
            }

            if (additive)
            {
                if (!selection.Remove(hit)) selection.Add(hit);
            }
            else if (selection.Contains(hit)) collapseToOnClick = hit; // Có thể là bắt đầu kéo cả nhóm: đợi nhả chuột mới biết.
            else
            {
                selection.Clear();
                selection.Add(hit);
            }
            if (!selection.Contains(hit))
            {
                dragMode = DragMode.None;
                return;
            }
            dragMode = DragMode.MoveCars;
            dragStartPositions.Clear();
            LevelData level = CurrentLevel;
            foreach (int index in selection) dragStartPositions[index] = new Vector2(level.vehicles[index].x, level.vehicles[index].z);
        }

        private void OnDrag(LevelData level, Vector2 point)
        {
            dragCurrentLevel = point;
            if (dragMode != DragMode.MoveCars) return;

            // Làm tròn ĐỘ DỜI chứ không làm tròn từng xe: kéo nhóm thì khoảng cách giữa các xe phải giữ nguyên.
            Vector2 delta = point - dragStartLevel;
            delta = new Vector2(Snap(delta.x), Snap(delta.y));
            if (delta == appliedDelta) return;
            if (!dragMoved)
            {
                dragMoved = true;
                collapseToOnClick = -1;
                undoGroup = Undo.GetCurrentGroup();
            }
            // Mỗi sự kiện kéo một lần ghi; OnLeftUp gộp lại để Ctrl+Z trả cả cú kéo về một bước.
            RecordChange("Di chuyển xe");
            foreach (KeyValuePair<int, Vector2> start in dragStartPositions)
            {
                level.vehicles[start.Key].x = start.Value.x + delta.x;
                level.vehicles[start.Key].z = start.Value.y + delta.y;
            }
            appliedDelta = delta;
            MarkChanged();
        }

        private void OnLeftUp()
        {
            if (dragMode == DragMode.BoxSelect) ApplyBoxSelection();
            else if (dragMode == DragMode.MoveCars && dragMoved) Undo.CollapseUndoOperations(undoGroup);
            else if (collapseToOnClick >= 0)
            {
                selection.Clear();
                selection.Add(collapseToOnClick);
            }
            dragMode = DragMode.None;
            Repaint();
        }

        private void ApplyBoxSelection()
        {
            Vector2 min = Vector2.Min(dragStartLevel, dragCurrentLevel);
            Vector2 max = Vector2.Max(dragStartLevel, dragCurrentLevel);
            Rect box = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            if (!dragAdditive) selection.Clear();
            for (int i = 0; i < footprints.Count; i++)
                if (box.Contains(footprints[i].Center) && !IsQueued(CurrentLevel, i)) selection.Add(i);
        }

        #endregion

        #region Thanh bên

        private void DrawSidebar(LevelData level)
        {
            Rect area = new Rect(0f, 0f, SidebarWidth, position.height);
            // Xe đặt lố khung bị vẽ tràn sang đây: nền đặc phủ lại.
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(area, new Color(0.22f, 0.22f, 0.22f));
            GUILayout.BeginArea(new Rect(8f, 8f, SidebarWidth - 16f, position.height - 16f));
            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll);
            DrawFileSection(level);
            DrawLevelSection(level);
            DrawGeneratorSection(CurrentLevel);
            level = CurrentLevel;
            if (level != null)
            {
                EnsureReport(level);
                DrawPaletteSection(level);
                DrawReportSection(level);
                DrawPacingSection(level);
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawFileSection(LevelData level)
        {
            EditorGUILayout.LabelField(hasUnsavedChanges ? "File (chưa lưu)" : "File", EditorStyles.boldLabel);
            bool playing = EditorApplication.isPlaying;
            if (playing) EditorGUILayout.HelpBox("Dừng Play rồi mới lưu được: đang Play thì game lưu save đè lúc dừng.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(playing))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Lưu")) WriteFile();
                if (GUILayout.Button("Đọc lại") && (!hasUnsavedChanges
                        || EditorUtility.DisplayDialog("Đọc lại", "Bỏ mọi thay đổi chưa lưu?", "Bỏ", "Huỷ")))
                    ReadFile();
            }
            using (new EditorGUI.DisabledScope(playing || level == null))
                if (GUILayout.Button("Lưu + đặt level này vào save") && WriteFile())
                    WriteLevelToSave(CurrentLevel.index);
            EditorGUILayout.Space();
        }

        private void DrawLevelSection(LevelData level)
        {
            EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);
            if (file.levels.Length > 0)
            {
                var names = new string[file.levels.Length];
                for (int i = 0; i < names.Length; i++) names[i] = $"Level {file.levels[i].index} ({file.levels[i].vehicles.Length} xe)";
                int chosen = EditorGUILayout.Popup(levelCursor, names);
                if (chosen != levelCursor) SelectLevel(chosen);
            }
            if (level != null)
            {
                int number = Mathf.Max(1, EditorGUILayout.DelayedIntField("Số level", level.index));
                if (number != level.index)
                {
                    if (Array.Exists(file.levels, other => other.index == number))
                        EditorUtility.DisplayDialog("Trùng số level", $"Đã có level {number} trong file riêng.", "OK");
                    else
                    {
                        RecordChange("Đổi số level");
                        removedLevelNumbers.Add(level.index);
                        level.index = number;
                        MarkChanged();
                    }
                }
            }
            if (level != null)
            {
                bool hidePassengers = EditorGUILayout.Toggle("Khách ẩn", level.passengersHidden);
                if (hidePassengers != level.passengersHidden)
                {
                    RecordChange("Đổi khách ẩn");
                    level.passengersHidden = hidePassengers;
                    MarkChanged();
                }
                int sites = Mathf.Clamp(EditorGUILayout.IntField("Ô công trường", level.constructionSlots), 0, 3);
                int sitePassengers = Mathf.Max(0, EditorGUILayout.IntField("Khách để mở mỗi ô", level.constructionPassengers));
                if (sites != level.constructionSlots || sitePassengers != level.constructionPassengers)
                {
                    RecordChange("Đổi ô công trường");
                    level.constructionSlots = sites;
                    level.constructionPassengers = sitePassengers;
                    MarkChanged();
                }
                if (sites > 0 && sitePassengers == 0)
                    EditorGUILayout.HelpBox("Ô công trường cần số khách > 0, không thì không có tác dụng.", MessageType.Warning);
            }
            newLevelNumber = Mathf.Max(1, EditorGUILayout.IntField("Số level mới", newLevelNumber));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Thêm level")) AddLevel(newLevelNumber, null);
                using (new EditorGUI.DisabledScope(level == null))
                {
                    if (GUILayout.Button("Nhân bản")) AddLevel(newLevelNumber, level);
                    if (GUILayout.Button("Xoá") && EditorUtility.DisplayDialog("Xoá level", $"Xoá level {level.index}?", "Xoá", "Huỷ"))
                    {
                        RecordChange("Xoá level");
                        removedLevelNumbers.Add(level.index);
                        ArrayUtility.RemoveAt(ref file.levels, levelCursor);
                        SelectLevel(levelCursor);
                        MarkChanged();
                    }
                }
            }
            EditorGUILayout.Space();
        }

        private void DrawGeneratorSection(LevelData level)
        {
            EnsureGeneratorConfig();
            LevelGeneratorProfile[] profiles = generatorConfig.profiles;
            EditorGUILayout.LabelField("Sinh tự động", EditorStyles.boldLabel);
            if (profiles == null || profiles.Length == 0)
            {
                EditorGUILayout.HelpBox($"{LevelGeneratorConfig.AssetPath} chưa có mức khó nào: xoá asset đó để Level Builder tạo lại mặc định.", MessageType.Error);
                return;
            }

            var names = new string[profiles.Length];
            for (int i = 0; i < names.Length; i++) names[i] = profiles[i].displayName;
            generatorProfileIndex = EditorGUILayout.Popup("Mức khó", Mathf.Clamp(generatorProfileIndex, 0, names.Length - 1), names);
            LevelGeneratorProfile profile = profiles[generatorProfileIndex];
            using (new EditorGUILayout.HorizontalScope())
            {
                generatorSeed = EditorGUILayout.IntField("Hạt giống", generatorSeed);
                if (GUILayout.Button("Đổi", GUILayout.Width(44f))) generatorSeed = UnityEngine.Random.Range(1, int.MaxValue);
            }
            generateHiddenCars = EditorGUILayout.ToggleLeft($"Có xe ẩn màu ({profile.hiddenCarChance:P0} xe bị chặn)", generateHiddenCars);
            generateHiddenPassengers = EditorGUILayout.ToggleLeft("Có khách ẩn", generateHiddenPassengers);
            generateLocks = EditorGUILayout.ToggleLeft($"Có khoá và chìa ({profile.lockPairCount} cặp)", generateLocks);
            generateConstruction = EditorGUILayout.ToggleLeft(
                $"Có ô công trường ({profile.constructionSlotCount} ô, {profile.constructionPassengers} khách / ô)", generateConstruction);
            generateGarages = EditorGUILayout.ToggleLeft(
                $"Có bãi chờ ({profile.garageCount} bãi, {profile.garageQueueMin}–{profile.garageQueueMax} xe chờ)", generateGarages);
            using (new EditorGUI.DisabledScope(level == null))
                if (GUILayout.Button("Sinh vào level đang mở")) GenerateIntoLevel(level, profile);

            batchCount = Mathf.Clamp(EditorGUILayout.IntField("Số level hàng loạt", batchCount), 1, 200);
            if (GUILayout.Button($"Sinh {batchCount} level mới từ số {newLevelNumber}")) GenerateBatch(profile, batchCount);
            if (!string.IsNullOrEmpty(generatorInfo)) EditorGUILayout.HelpBox(generatorInfo, MessageType.None);
            EditorGUILayout.Space();
        }

        /// Tạo lúc vẽ cửa sổ, không tạo trong OnEnable: lúc khôi phục cửa sổ sau biên dịch Unity có thể đang import, cấm CreateAsset.
        private void EnsureGeneratorConfig()
        {
            if (generatorConfig != null) return;
            generatorConfig = AssetDatabase.LoadAssetAtPath<LevelGeneratorConfig>(LevelGeneratorConfig.AssetPath);
            if (generatorConfig != null) return;
            generatorConfig = CreateInstance<LevelGeneratorConfig>();
            AssetDatabase.CreateAsset(generatorConfig, LevelGeneratorConfig.AssetPath);
            AssetDatabase.SaveAssets();
        }

        private GeneratedLevel RunGenerator(LevelGeneratorProfile profile, int seed) =>
            LevelGenerator.Generate(profile, shapes, config.levelDataMin, config.levelDataMax, config.blockerRayInset,
                config.carColors?.Count ?? 0, seed,
                new GeneratorOptions { HideCarColors = generateHiddenCars, HidePassengers = generateHiddenPassengers, AddLocks = generateLocks,
                    AddConstruction = generateConstruction, AddGarages = generateGarages,
                    GarageBuildingOffset = config.garageBuildingOffset * config.carScale,
                    GarageBuildingLength = config.garageBuildingLength * config.carScale,
                    GarageBuildingWidth = config.garageBuildingWidth * config.carScale });

        private void GenerateIntoLevel(LevelData level, LevelGeneratorProfile profile)
        {
            GeneratedLevel result = RunGenerator(profile, generatorSeed);
            RecordChange("Sinh level");
            level.seed = result.Level.seed;
            level.difficulty = result.Level.difficulty;
            level.vehicles = result.Level.vehicles;
            level.mix = result.Level.mix;
            level.hardPoints = result.Level.hardPoints;
            level.passengersHidden = result.Level.passengersHidden;
            level.constructionSlots = result.Level.constructionSlots;
            level.constructionPassengers = result.Level.constructionPassengers;
            selection.Clear();
            generatorInfo = $"{profile.displayName}, hạt giống {result.Seed}: {level.vehicles.Length} xe, {result.FreeAtStart} xe ra ngay, " +
                            $"chuỗi chặn {result.MaxChainDepth}." + (result.MetTargets ? "" : " Chưa đạt hết mục tiêu của mức này.");
            // Bấm tiếp là ra bố cục khác; muốn lấy lại đúng level này thì nhập hạt giống ghi ở trên.
            generatorSeed = unchecked(generatorSeed + 1);
            MarkChanged();
        }

        private void GenerateBatch(LevelGeneratorProfile profile, int count)
        {
            RecordChange("Sinh hàng loạt");
            int created = 0, missedTargets = 0, firstNumber = -1;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Level Builder", $"Đang sinh level {i + 1}/{count}", i / (float)count)) break;
                    while (Array.Exists(file.levels, level => level.index == newLevelNumber)) newLevelNumber++;
                    GeneratedLevel result = RunGenerator(profile, generatorSeed);
                    result.Level.index = newLevelNumber;
                    ArrayUtility.Add(ref file.levels, result.Level);
                    if (firstNumber < 0) firstNumber = newLevelNumber;
                    if (!result.MetTargets) missedTargets++;
                    newLevelNumber++;
                    generatorSeed = unchecked(generatorSeed + 1);
                    created++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            if (created == 0) return;
            levelCursor = file.levels.Length - 1;
            selection.Clear();
            generatorInfo = $"{profile.displayName}: đã sinh {created} level, số {firstNumber}–{newLevelNumber - 1}." +
                            (missedTargets > 0 ? $" {missedTargets} level chưa đạt hết mục tiêu." : "");
            MarkChanged();
        }

        private void DrawPaletteSection(LevelData level)
        {
            EditorGUILayout.LabelField("Xe (đang chọn " + selection.Count + ")", EditorStyles.boldLabel);
            int type = GUILayout.Toolbar((int)placeType, TypeLabels);
            if (type != (int)placeType)
            {
                placeType = PlaceableTypes[type];
                ApplyToSelected(level, "Đổi loại xe", car => car.type = type);
            }

            List<Color> colors = config.carColors;
            int colorCount = colors?.Count ?? 0;
            for (int row = 0; row < colorCount; row += 4)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    Color previous = GUI.backgroundColor;
                    for (int i = row; i < Mathf.Min(row + 4, colorCount); i++)
                    {
                        GUI.backgroundColor = colors[i];
                        if (!GUILayout.Button(i == placeColor ? "●" : " ", GUILayout.Height(22f))) continue;
                        placeColor = i;
                        int chosenColor = i;
                        ApplyToSelected(level, "Đổi màu xe", car => car.color = chosenColor);
                    }
                    GUI.backgroundColor = previous;
                }
            }

            float angle = EditorGUILayout.DelayedFloatField("Góc", placeAngle);
            if (!Mathf.Approximately(angle, placeAngle))
            {
                placeAngle = Mathf.Repeat(angle, 360f);
                float chosenAngle = placeAngle;
                ApplyToSelected(level, "Đặt góc xe", car => car.rotY = chosenAngle);
            }
            using (new EditorGUI.DisabledScope(selection.Count == 0))
            {
                bool allHidden = selection.Count > 0, anyHidden = false;
                foreach (int index in selection)
                {
                    allHidden &= level.vehicles[index].colorHidden;
                    anyHidden |= level.vehicles[index].colorHidden;
                }
                EditorGUI.showMixedValue = anyHidden && !allHidden;
                EditorGUI.BeginChangeCheck();
                bool hide = EditorGUILayout.Toggle("Ẩn màu (xe đang chọn)", allHidden);
                EditorGUI.showMixedValue = false;
                if (EditorGUI.EndChangeCheck()) ApplyToSelected(level, "Đổi ẩn màu", car => car.colorHidden = hide);
            }
            DrawSelectionNumber(level, "Bị khoá bởi chìa số", car => car.lockId, (car, value) => car.lockId = value, "Đổi khoá");
            DrawSelectionNumber(level, "Mang chìa số", car => car.keyId, (car, value) => car.keyId = value, "Đổi chìa");
            DrawGarageSection(level);
            rotateStep = Mathf.Clamp(EditorGUILayout.FloatField("Bước xoay R", rotateStep), 1f, 180f);
            using (new EditorGUILayout.HorizontalScope())
            {
                snapEnabled = EditorGUILayout.ToggleLeft("Bắt lưới", snapEnabled, GUILayout.Width(80f));
                using (new EditorGUI.DisabledScope(!snapEnabled))
                    snapStep = Mathf.Max(0.01f, EditorGUILayout.FloatField(snapStep));
            }
            EditorGUILayout.Space();
        }

        /// Bãi chờ: chọn đúng 1 xe → biến nó thành xe đứng trước cổng, rồi thêm / sửa / bỏ các xe chờ bên trong.
        private void DrawGarageSection(LevelData level)
        {
            if (selection.Count != 1) return;
            int index = -1;
            foreach (int i in selection) index = i;
            VehicleData front = level.vehicles[index];
            if (front.garageId <= 0)
            {
                if (GUILayout.Button("Biến xe này thành cửa bãi chờ"))
                {
                    RecordChange("Tạo bãi chờ");
                    int nextId = 1;
                    foreach (VehicleData car in level.vehicles) nextId = Mathf.Max(nextId, car.garageId + 1);
                    front.garageId = nextId;
                    front.garageOrder = 0;
                    MarkChanged();
                }
                return;
            }

            var queued = new List<VehicleData>();
            foreach (VehicleData car in level.vehicles)
                if (car != front && car.garageId == front.garageId && car.garageOrder > front.garageOrder) queued.Add(car);
            queued.Sort((a, b) => a.garageOrder.CompareTo(b.garageOrder));
            EditorGUILayout.LabelField($"Bãi chờ G{front.garageId}: {queued.Count} xe chờ (ra từ trên xuống)", EditorStyles.miniBoldLabel);
            for (int k = 0; k < queued.Count; k++)
            {
                VehicleData car = queued[k];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    int type = EditorGUILayout.Popup(car.type, TypeLabels, GUILayout.Width(70f));
                    int color = Mathf.Clamp(EditorGUILayout.IntField(car.color, GUILayout.Width(30f)), 0, Mathf.Max(0, (config.carColors?.Count ?? 1) - 1));
                    bool hidden = EditorGUILayout.ToggleLeft("?", car.colorHidden, GUILayout.Width(30f));
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = ColorOf(color);
                    GUILayout.Box(GUIContent.none, GUILayout.Width(18f), GUILayout.Height(16f));
                    GUI.backgroundColor = previous;
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordChange("Sửa xe chờ");
                        car.type = type;
                        car.color = color;
                        car.colorHidden = hidden;
                        MarkChanged();
                    }
                    if (GUILayout.Button("x", GUILayout.Width(22f)))
                    {
                        RecordChange("Bỏ xe chờ");
                        ArrayUtility.Remove(ref level.vehicles, car);
                        selection.Clear();
                        MarkChanged();
                        GUIUtility.ExitGUI();
                    }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Thêm xe chờ (loại / màu đang chọn)"))
                {
                    RecordChange("Thêm xe chờ");
                    int order = front.garageOrder;
                    foreach (VehicleData car in queued) order = Mathf.Max(order, car.garageOrder);
                    // ⚠️ tool phải là -1, không thì game bỏ qua xe. Vị trí lấy theo xe đầu (game cũng dùng vị trí xe đầu).
                    ArrayUtility.Add(ref level.vehicles, new VehicleData
                    {
                        type = (int)placeType, color = placeColor, x = front.x, z = front.z, rotY = front.rotY, tool = -1,
                        garageId = front.garageId, garageOrder = order + 1,
                    });
                    MarkChanged();
                }
                if (GUILayout.Button("Bỏ bãi chờ", GUILayout.Width(80f)))
                {
                    RecordChange("Bỏ bãi chờ");
                    foreach (VehicleData car in queued) ArrayUtility.Remove(ref level.vehicles, car);
                    front.garageId = 0;
                    front.garageOrder = 0;
                    selection.Clear();
                    MarkChanged();
                    GUIUtility.ExitGUI();
                }
            }
        }

        /// Ô số áp cho mọi xe đang chọn (0 = không có). Các xe khác số thì hiện "—".
        private void DrawSelectionNumber(LevelData level, string label, Func<VehicleData, int> get, Action<VehicleData, int> set, string actionName)
        {
            using (new EditorGUI.DisabledScope(selection.Count == 0))
            {
                int first = -1;
                bool mixed = false;
                foreach (int index in selection)
                {
                    int value = get(level.vehicles[index]);
                    if (first < 0) first = value;
                    else if (value != first) mixed = true;
                }
                EditorGUI.showMixedValue = mixed;
                EditorGUI.BeginChangeCheck();
                int chosen = Mathf.Clamp(EditorGUILayout.DelayedIntField(label, Mathf.Max(0, first)), 0, 9);
                EditorGUI.showMixedValue = false;
                if (EditorGUI.EndChangeCheck()) ApplyToSelected(level, actionName, car => set(car, chosen));
            }
        }

        private void DrawReportSection(LevelData level)
        {
            EditorGUILayout.LabelField("Soát", EditorStyles.boldLabel);
            var seats = new int[Mathf.Max(1, config.carColors?.Count ?? 1)];
            int outsidePalette = 0;
            foreach (VehicleData car in level.vehicles)
            {
                if (car.color >= 0 && car.color < seats.Length) seats[car.color] += CarModel.SeatsOf((VehicleType)car.type);
                else outsidePalette++;
            }
            var text = new StringBuilder($"{level.vehicles.Length} xe · ghế theo màu:");
            for (int i = 0; i < seats.Length; i++)
                if (seats[i] > 0) text.Append($" M{i}: {seats[i]}");
            EditorGUILayout.HelpBox(text.ToString(), MessageType.None);

            if (level.vehicles.Length == 0) EditorGUILayout.HelpBox("Chưa có xe: vào ván là thắng ngay.", MessageType.Warning);
            if (report.Deadlocked.Count > 0)
                EditorGUILayout.HelpBox($"{report.Deadlocked.Count} xe kẹt vòng (viền tím): không có thứ tự nào đưa chúng ra.", MessageType.Error);
            if (report.Overlapping.Count > 0)
                EditorGUILayout.HelpBox($"{report.Overlapping.Count} xe chồng nhau (viền đỏ): tia dò chặn có thể bỏ sót.", MessageType.Warning);
            if (report.OutOfFrame.Count > 0)
                EditorGUILayout.HelpBox($"{report.OutOfFrame.Count} xe lố khung bãi (viền cam).", MessageType.Warning);
            if (outsidePalette > 0)
                EditorGUILayout.HelpBox($"{outsidePalette} xe dùng màu ngoài bảng GameplayConfig.", MessageType.Error);
            if (level.vehicles.Length > 0) EditorGUILayout.LabelField($"Chạm là ra ngay: {report.FreeNow.Count} xe");
            int hiddenCars = 0, hiddenFree = 0;
            for (int i = 0; i < level.vehicles.Length; i++)
            {
                if (!level.vehicles[i].colorHidden) continue;
                hiddenCars++;
                if (report.FreeNow.Contains(i)) hiddenFree++;
            }
            if (hiddenCars > 0) EditorGUILayout.LabelField($"Xe ẩn màu (nhãn \"?\"): {hiddenCars}");
            var lockIds = new HashSet<int>();
            var keyIds = new HashSet<int>();
            foreach (VehicleData car in level.vehicles)
            {
                if (car.lockId > 0) lockIds.Add(car.lockId);
                if (car.keyId > 0) keyIds.Add(car.keyId);
            }
            foreach (int id in lockIds)
                if (!keyIds.Contains(id)) EditorGUILayout.HelpBox($"Khoá số {id} không có xe mang chìa: xe đó coi như không khoá.", MessageType.Warning);
            foreach (int id in keyIds)
                if (!lockIds.Contains(id)) EditorGUILayout.HelpBox($"Chìa số {id} không mở khoá nào.", MessageType.Warning);
            if (hiddenFree > 0)
                EditorGUILayout.HelpBox($"{hiddenFree} xe ẩn màu ra được ngay: lộ màu luôn khi vào ván.", MessageType.Warning);
            EditorGUILayout.Space();
        }

        /// Mix và điểm khó quyết định thứ tự khách. Để trống vẫn chơi được: khách ra theo từng xe, dễ hơn.
        private void DrawPacingSection(LevelData level)
        {
            EditorGUILayout.LabelField("Trộn khách: tại %, cỡ 1–3, từ %, đến %", EditorStyles.boldLabel);
            for (int i = 0; i < level.mix.Length; i++)
            {
                MixData row = level.mix[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    float at = EditorGUILayout.FloatField(row.at);
                    int size = EditorGUILayout.IntField(row.size);
                    float from = EditorGUILayout.FloatField(row.from);
                    float to = EditorGUILayout.FloatField(row.to);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordChange("Sửa trộn khách");
                        row.at = Mathf.Clamp(at, 0f, 100f);
                        row.size = Mathf.Clamp(size, 1, 3);
                        row.from = Mathf.Clamp(from, 0f, 100f);
                        row.to = Mathf.Clamp(to, 0f, 100f);
                        MarkChanged();
                    }
                    if (GUILayout.Button("x", GUILayout.Width(22f)))
                    {
                        RecordChange("Xoá dòng trộn khách");
                        ArrayUtility.RemoveAt(ref level.mix, i);
                        MarkChanged();
                        GUIUtility.ExitGUI();
                    }
                }
            }
            if (GUILayout.Button("Thêm dòng trộn"))
            {
                RecordChange("Thêm dòng trộn khách");
                ArrayUtility.Add(ref level.mix, new MixData { size = 1 });
                MarkChanged();
            }

            EditorGUILayout.LabelField("Điểm khó: tại %, thêm xe chặn", EditorStyles.boldLabel);
            for (int i = 0; i < level.hardPoints.Length; i++)
            {
                HardPointData row = level.hardPoints[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    float at = EditorGUILayout.FloatField(row.at);
                    int bonus = EditorGUILayout.IntField(row.bonus);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordChange("Sửa điểm khó");
                        row.at = Mathf.Clamp(at, 0f, 100f);
                        row.bonus = Mathf.Max(0, bonus);
                        MarkChanged();
                    }
                    if (GUILayout.Button("x", GUILayout.Width(22f)))
                    {
                        RecordChange("Xoá điểm khó");
                        ArrayUtility.RemoveAt(ref level.hardPoints, i);
                        MarkChanged();
                        GUIUtility.ExitGUI();
                    }
                }
            }
            if (GUILayout.Button("Thêm điểm khó"))
            {
                RecordChange("Thêm điểm khó");
                ArrayUtility.Add(ref level.hardPoints, new HardPointData());
                MarkChanged();
            }
        }

        #endregion
    }
}
