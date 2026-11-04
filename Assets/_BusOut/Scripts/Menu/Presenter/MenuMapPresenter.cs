using BusOut.Core;
using DG.Tweening;
using UnityEngine;

namespace BusOut.Menu
{
    /// Bản đồ level ở Menu: level nào ở ô nào, module / chủ đề nào, bệ nào đã qua, xe đứng ở level hiện tại, trời theo
    /// chủ đề. Vuốt dọc để xem level đã qua / sắp tới (chỉ xem — như gốc, Play luôn chơi level hiện tại): kéo theo tay,
    /// thả thì trôi theo đà rồi dừng đúng một level; trời chuyển dần khi lăn qua ranh giới hai chủ đề.
    /// Thắng về Menu: bệ vừa qua sáng "đã qua" + pháo sáng, xe CHẠY THẬT dọc mặt đường sang bệ level mới (có khói, phóng
    /// to), bản đồ lăn theo sau để bệ mới về giữa khung nhìn (bạn chốt 3/11/2026 — gốc: xe đứng yên, đường lăn bên dưới).
    /// MenuPresenter tạo (null khi config chủ đề sai — Menu vẫn chạy, chỉ mất bản đồ).
    public sealed class MenuMapPresenter
    {
        // Tốc độ kéo làm mượt theo từng lần di chuyển ngón tay: một khung lẻ giật không làm bản đồ văng xa khi thả.
        private const float DragVelocitySmoothing = 0.5f;
        // Lệch dưới mức này (level) coi như đang đứng đúng level hiện tại — chỉ để nuốt sai số số thực, không phải số chỉnh.
        private const float ScrolledAwayEpsilon = 0.01f;

        private readonly MenuConfig config;
        private readonly FxConfig fx;
        private readonly MenuMapView view;
        private readonly MapReturnButtonView returnButton; // null khi thiếu nút: chỉ mất nút, vẫn vuốt được
        private readonly SecureGameData gameData;

        private int currentLevel = 1;
        private bool isReturnButtonVisible;
        // Vị trí trống = level đang xem − 1; số thực khi đang lăn giữa hai level.
        private float position;
        private int windowCenter = int.MinValue;
        private bool isDragging;
        private float dragVelocity; // level / giây
        private float lastDragTime;
        private Tween moveTween; // hít / về level hiện tại / xe chạy sang bệ mới: luôn chỉ một
        // Đang chạy sang bệ mới (cả lúc đứng chờ trước khi chạy): khoá vuốt + nút về, không đặt xe lên bệ.
        private bool isAdvancing;
        // Level xe sẽ chạy tới khi màn chuyển cảnh mờ đi (0 = không có).
        private int pendingAdvanceLevel;
        // Quãng xe đã chạy dọc mặt đường (level, 0..1) trong lúc sang bệ mới.
        private float carDriven;
        private readonly DG.Tweening.Core.DOGetter<float> getCarDriven;
        private readonly DG.Tweening.Core.DOSetter<float> setCarDriven;
        // Tiến độ đoạn chạy (0..1) và vị trí bản đồ lúc bắt đầu chạy.
        private float advanceProgress;
        private float advanceFromPosition;
        private readonly DG.Tweening.Core.DOGetter<float> getAdvanceProgress;
        private readonly DG.Tweening.Core.DOSetter<float> setAdvanceProgress;

        private MenuMapPresenter(MenuConfig config, MenuMapView view, MapReturnButtonView returnButton, SecureGameData gameData)
        {
            this.config = config;
            fx = FxConfig.Instance;
            this.view = view;
            this.returnButton = returnButton;
            this.gameData = gameData;
            getCarDriven = () => carDriven;
            setCarDriven = SetCarDriven;
            getAdvanceProgress = () => advanceProgress;
            setAdvanceProgress = SetAdvanceProgress;
            if (returnButton != null) returnButton.Clicked += ReturnToCurrentLevel;
        }

        public static MenuMapPresenter Create(MenuConfig config, MenuMapView view, MapReturnButtonView returnButton,
            SecureGameData gameData)
        {
            if (!IsThemeListValid(config, view)) return null;
            FxConfig fx = FxConfig.Instance;
            if (fx.levelPassEffectPrefab == null)
                Debug.LogError("[BusOut] FxConfig thiếu Level Pass Effect Prefab: thắng về Menu không có pháo sáng ở bệ. Kéo " +
                               "Prefab/VFX/LevelPassEffect vào Resources/FxConfig.", fx);
            if (fx.carIdleRingsPrefab == null)
                Debug.LogError("[BusOut] FxConfig thiếu Car Idle Rings Prefab: xe Menu đứng yên không có vòng tỏa dưới gầm. Kéo " +
                               "Prefab/VFX/CarIdleRings vào Resources/FxConfig.", fx);
            if (!view.Init(MenuMapLayout.SlotCount, MenuMapLayout.DegreesPerLevel)) return null;
            view.InitCarEffects(fx.carIdleRingsPrefab);
            if (returnButton != null && !returnButton.Init()) returnButton = null;
            return new MenuMapPresenter(config, view, returnButton, gameData);
        }

        /// Màn Loading: tạo sẵn mỗi prefab module một bản trong pool, mỗi khung tối đa preloadModulesPerFrame bản.
        /// <paramref name="reportProgress"/>: 0..1.
        public System.Collections.IEnumerator PrewarmModules(System.Action<float> reportProgress)
        {
            int total = 0;
            foreach (MenuTheme theme in config.themes) total += theme.modules.Length;
            int done = 0;
            int perFrame = Mathf.Max(1, config.preloadModulesPerFrame);
            foreach (MenuTheme theme in config.themes)
            {
                foreach (GameObject module in theme.modules)
                {
                    view.PrewarmModule(module);
                    done++;
                    if (done % perFrame != 0) continue;
                    reportProgress(done / (float)total);
                    yield return null;
                }
            }
            // Pháo sáng: lần thắng đầu tiên không phải Instantiate đúng lúc xe bắt đầu chạy.
            if (fx.levelPassEffectPrefab != null) view.PrewarmModule(fx.levelPassEffectPrefab);
            reportProgress(1f);
        }

        /// Về đúng level đang lưu (không hoạt ảnh): xe ở bệ level đó, trời theo chủ đề của nó.
        public void ShowCurrentLevel() => JumpTo(Mathf.Max(1, gameData.currentLevel));

        /// Mở lại Menu (về từ ván chơi, dưới màn chuyển cảnh): vừa thắng (level lưu = level đang hiện + 1) → đứng ở bệ vừa
        /// thắng, chờ PlayPendingAdvance (màn che mờ đi) mới chạy sang bệ mới; còn lại (thua, rời ván) → đứng ở level lưu.
        public void Refresh()
        {
            int savedLevel = Mathf.Max(1, gameData.currentLevel);
            if (savedLevel == currentLevel + 1 && !isAdvancing)
            {
                JumpTo(currentLevel);
                pendingAdvanceLevel = savedLevel;
            }
            else
            {
                pendingAdvanceLevel = 0;
                ShowCurrentLevel();
            }
        }

        /// Màn chuyển cảnh bắt đầu mờ (ScreenRevealedEvent): chạy sang bệ mới nếu Refresh vừa thấy thắng. Chạy ngay lúc
        /// Refresh thì xe đi xong (hoặc đi dở) dưới màn che, người chơi không thấy.
        public void PlayPendingAdvance()
        {
            if (pendingAdvanceLevel <= 0) return;
            int toLevel = pendingAdvanceLevel;
            pendingAdvanceLevel = 0;
            PlayAdvance(toLevel - 1, toLevel);
        }

        private void JumpTo(int level)
        {
            KillMove();
            // Đang chạy dở bị cắt ngang (vd. bấm Play giữa lúc chạy): tắt khói, bỏ phóng to — FillWindow đặt lại xe lên bệ.
            view.EndCarDrive();
            isDragging = false;
            isAdvancing = false;
            currentLevel = level;
            // Dựng lại cả cửa sổ dù vị trí không đổi: level hiện tại có thể vừa đổi (bệ "đã qua", chỗ đặt xe) và xe có thể
            // đang ở carPivot (chạy dở) — FillWindow đặt lại xe lên bệ.
            windowCenter = int.MinValue;
            SetPosition(level - 1);
        }

        /// Đứng ở bệ vừa thắng như lúc rời Menu (mapAdvanceDelay), lùi lấy đà (mapCarBackUp*), rồi xe chạy dọc mặt đường
        /// sang bệ mới và bản đồ lăn CÙNG LÚC trong mapAdvanceDuration (như gốc 1.4s): giữa đường xe dẫn trước khung nhìn tối
        /// đa mapCarLeadLevels (thấy xe lăn bánh trên đường, không trượt), thân xe ngửa khi tăng tốc / chúi khi phanh và nhún
        /// đều; gần tới thì tắt khói + thu cỡ; đỗ thì nảy một cái (bạn duyệt 4/11).
        private void PlayAdvance(int fromLevel, int toLevel)
        {
            JumpTo(fromLevel);
            isAdvancing = true;
            float startTime = Mathf.Max(0f, config.mapAdvanceDelay);
            float backUpDuration = Mathf.Max(0f, config.mapCarBackUpDuration);
            float driveStart = startTime + backUpDuration;
            float driveDuration = Mathf.Max(0.01f, config.mapAdvanceDuration);
            float scaleBack = Mathf.Clamp(fx.mapCarDriveScaleDuration, 0f, driveDuration);
            // ⚠️ Đặt về 0 NGAY ĐÂY, không trong StartAdvance: tween đọc giá trị đầu lúc nó bắt đầu, cùng thời điểm với callback
            // StartAdvance — đọc trước callback là đọc số của lần thắng trước → xe đứng im / nhảy cóc.
            carDriven = 0f;
            advanceProgress = 0f;
            advanceFromPosition = fromLevel - 1;
            moveTween = DOTween.Sequence()
                .InsertCallback(startTime, () => StartAdvance(fromLevel, toLevel))
                .Insert(startTime, DOTween.To(getCarDriven, setCarDriven, -config.mapCarBackUpLevels, backUpDuration)
                    .SetEase(Ease.OutQuad))
                // Tiến độ chạy tuyến tính 0 → 1; dáng chạy (ease, dẫn trước, chúi, nhún) tính trong SetAdvanceProgress.
                .Insert(driveStart, DOTween.To(getAdvanceProgress, setAdvanceProgress, 1f, driveDuration).SetEase(Ease.Linear))
                .InsertCallback(driveStart + driveDuration - scaleBack, EndCarMove)
                .AppendCallback(FinishAdvance)
                .SetLink(view.gameObject)
                .SetTarget(this);
        }

        /// <paramref name="t"/> 0..1 của đoạn chạy. Xe: InOutSine từ chỗ lùi lấy đà tới bệ mới. Bản đồ: cùng nhịp nhưng chậm
        /// hơn xe một đoạn lead·sin(πt) (0 ở hai đầu → xe bắt đầu và dừng ở giữa màn). Gia tốc của InOutSine ∝ cos(πt) nên
        /// dáng thân xe theo sin(2πt): dương nửa đầu (tăng tốc → ngửa đầu), âm nửa sau (phanh → chúi đầu), 0 ở hai đầu.
        private void SetAdvanceProgress(float t)
        {
            advanceProgress = t;
            float eased = 0.5f * (1f - Mathf.Cos(Mathf.PI * t));
            SetCarDriven(Mathf.Lerp(-config.mapCarBackUpLevels, 1f, eased));
            float road = Mathf.Clamp01(eased - config.mapCarLeadLevels * Mathf.Sin(Mathf.PI * t));
            SetPosition(advanceFromPosition + road);
            float elapsed = t * config.mapAdvanceDuration;
            float tilt = fx.mapCarTiltDegrees * Mathf.Sin(2f * Mathf.PI * t);
            float squash = fx.mapCarBounceAmount * Mathf.Abs(Mathf.Sin(Mathf.PI * fx.mapCarBounceFrequency * elapsed));
            view.SetCarBody(tilt, squash);
        }

        private void EndCarMove() => view.EndCarMove(fx.mapCarDriveScaleDuration);

        private void StartAdvance(int fromLevel, int toLevel)
        {
            // Xe sang carPivot TRƯỚC khi dựng lại cửa sổ: bệ cũ có thể bị trả về pool, xe còn là con của nó thì bị ẩn theo.
            view.BeginCarDrive(fx.mapCarDriveScale, fx.mapCarDriveScaleDuration);
            view.PlayLevelPassEffect(MenuMapLayout.SlotOf(fromLevel), fx.levelPassEffectPrefab);
            AudioService.Play(SoundId.CarMoveToBusStop);
            currentLevel = toLevel;
            // Dựng lại để bệ vừa qua bật lớp "đã qua" (level < currentLevel).
            windowCenter = int.MinValue;
            SetPosition(position);
        }

        private void SetCarDriven(float value)
        {
            carDriven = value;
            view.SetCarDrive(value);
        }

        private void FinishAdvance()
        {
            isAdvancing = false;
            view.EndCarDrive();
            view.PlaceCar(MenuMapLayout.SlotOf(currentLevel));
            view.PlayCarArrive(fx.mapCarArrivePunch, fx.mapCarArrivePunchDuration);
            UpdateReturnButton();
        }

        public void BeginDrag()
        {
            if (isAdvancing) return;
            KillMove();
            isDragging = true;
            dragVelocity = 0f;
            lastDragTime = Time.unscaledTime;
        }

        /// <paramref name="deltaPixels"/>: độ dời dọc của ngón tay trong lần di chuyển này (pixel, dương = lên).
        public void Drag(float deltaPixels)
        {
            if (!isDragging) return;
            // Kéo xuống = kéo mặt đường về phía mình → level phía trước lăn tới chỗ xe → vị trí tăng.
            float deltaLevels = -deltaPixels / Screen.height * config.mapLevelsPerScreenHeight;
            float now = Time.unscaledTime;
            float elapsed = now - lastDragTime;
            if (elapsed > 0f) dragVelocity = Mathf.Lerp(dragVelocity, deltaLevels / elapsed, DragVelocitySmoothing);
            lastDragTime = now;
            SetPosition(position + deltaLevels);
        }

        /// Thả tay: trôi thêm theo đà rồi dừng ở level gần nhất trong vùng được xem.
        public void EndDrag()
        {
            if (!isDragging) return;
            isDragging = false;
            // Ngón tay dừng rồi mới thả: tốc độ đo lúc còn di chuyển đã cũ, trôi theo nó là bản đồ tự chạy sai ý.
            if (Time.unscaledTime - lastDragTime > config.mapReleaseIdleTime) dragVelocity = 0f;
            SnapTo(Mathf.Clamp(Mathf.Round(position + dragVelocity * config.mapFlingTime), 0f, MaxPosition));
        }

        /// Nút "về level hiện tại": lăn bản đồ về bệ có xe (trời chuyển theo nếu đi qua chủ đề khác).
        public void ReturnToCurrentLevel()
        {
            if (isAdvancing) return;
            isDragging = false;
            SnapTo(currentLevel - 1);
        }

        /// Lăn mượt tới <paramref name="target"/>: đi càng xa càng lâu, nhưng có trần (nút về từ xa không chờ quá lâu).
        private void SnapTo(float target)
        {
            KillMove();
            float duration = Mathf.Min(config.mapSnapDuration + Mathf.Abs(target - position) * config.mapSnapDurationPerLevel,
                config.mapSnapMaxDuration);
            moveTween = DOTween.To(() => position, SetPosition, target, duration)
                .SetEase(Ease.OutCubic)
                .SetLink(view.gameObject)
                .SetTarget(this);
        }

        /// Xa nhất xem được: level hiện tại + mapViewAheadLevels. Lùi thì tới level 1 (vị trí 0).
        private float MaxPosition => currentLevel - 1 + Mathf.Max(0, config.mapViewAheadLevels);

        private void SetPosition(float value)
        {
            position = Mathf.Clamp(value, 0f, MaxPosition);
            view.SetRoadPosition(position);
            int center = Mathf.RoundToInt(position) + 1;
            if (center != windowCenter)
            {
                windowCenter = center;
                FillWindow(center);
            }
            UpdateSky();
            UpdateReturnButton();
        }

        /// Nút hiện khi đã lăn khỏi level hiện tại VÀ xe (ở bệ level đó) đã khuất khỏi màn hình. Điều kiện đầu chặn trường
        /// hợp xe đứng sát mép màn ngay cả khi chưa lăn (tuỳ góc Map / camera) — không thì vào Menu đã thấy nút.
        /// Chỉ gọi view khi trạng thái đổi (hàm này chạy mỗi khung lúc đang lăn).
        private void UpdateReturnButton()
        {
            if (returnButton == null) return;
            bool hasScrolledAway = Mathf.Abs(currentLevel - 1 - position) > ScrolledAwayEpsilon;
            bool isVisible = hasScrolledAway &&
                             !view.IsCarOnScreen(config.mapCarVisibleViewportBottom, config.mapCarVisibleViewportTop);
            if (isVisible == isReturnButtonVisible) return;
            isReturnButtonVisible = isVisible;
            returnButton.SetVisible(isVisible);
        }

        /// Gắn module + số level cho 8 level liên tiếp quanh <paramref name="centerLevel"/>; xe về bệ level hiện tại nếu
        /// level đó còn trong cửa sổ (ra ngoài thì module của nó đã trả về pool, xe tự ẩn — xem MenuMapView.ReleaseModule).
        private void FillWindow(int centerLevel)
        {
            int first = MenuMapLayout.FirstLevelInWindow(centerLevel);
            int end = first + MenuMapLayout.SlotCount;
            for (int level = first; level < end; level++)
            {
                int slot = MenuMapLayout.SlotOf(level);
                view.SetModule(slot, level >= 1 ? ModuleOf(level) : null);
                view.SetPad(slot, level, level < currentLevel);
            }
            // Đang chạy sang bệ mới thì xe ở carPivot, FinishAdvance mới đặt lên bệ.
            if (!isAdvancing && currentLevel >= first && currentLevel < end) view.PlaceCar(MenuMapLayout.SlotOf(currentLevel));
        }

        /// Đang lăn từ level L sang L + 1 khác chủ đề: trời trộn theo phần đã lăn. Cùng chủ đề: trời của chủ đề đó.
        private void UpdateSky()
        {
            int level = Mathf.FloorToInt(position) + 1;
            float progress = position - (level - 1);
            MenuTheme from = ThemeOf(level);
            MenuTheme to = ThemeOf(level + 1);
            if (from == to || progress <= 0f) view.SetSky(from.sky, from.cloud, from.sky, from.cloud, 0f);
            else view.SetSky(from.sky, from.cloud, to.sky, to.cloud, progress);
        }

        private void KillMove()
        {
            if (moveTween != null && moveTween.IsActive()) moveTween.Kill();
            moveTween = null;
        }

        private MenuTheme ThemeOf(int level) =>
            config.themes[MenuMapLayout.ThemeOf(level, config.levelsPerTheme, config.themes.Count)];

        private GameObject ModuleOf(int level) =>
            ThemeOf(level).modules[MenuMapLayout.PieceOf(level, config.levelsPerTheme)];

        /// Kiểm một lần lúc tạo để lúc chạy không phải kiểm từng level: thiếu một module là bản đồ hở một đoạn.
        private static bool IsThemeListValid(MenuConfig config, Object context)
        {
            string problems = "";
            if (config.levelsPerTheme < 1) problems += $" levelsPerTheme = {config.levelsPerTheme} (phải ≥ 1).";
            if (config.themes == null || config.themes.Count == 0) problems += " Chưa có chủ đề nào.";
            else
            {
                for (int i = 0; i < config.themes.Count; i++)
                {
                    MenuTheme theme = config.themes[i];
                    string label = $" Chủ đề {i} ({theme?.name}):";
                    if (theme == null) { problems += $"{label} trống."; continue; }
                    if (theme.modules == null || theme.modules.Length != config.levelsPerTheme)
                        problems += $"{label} cần đúng {config.levelsPerTheme} module, đang có {theme.modules?.Length ?? 0}.";
                    else if (System.Array.IndexOf(theme.modules, null) >= 0) problems += $"{label} có ô module trống.";
                    if (theme.sky == null || theme.cloud == null) problems += $"{label} thiếu trời hoặc mây.";
                }
            }
            if (problems.Length == 0) return true;
            Debug.LogError($"[BusOut] MenuConfig: danh sách chủ đề sai, bản đồ level không hiện.{problems} " +
                           "Sửa trong Assets/_BusOut/Resources/MenuConfig.asset.", context);
            return false;
        }
    }
}
