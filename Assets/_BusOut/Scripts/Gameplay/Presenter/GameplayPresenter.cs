using System;
using System.Collections;
using System.Collections.Generic;
using BusOut.Core;
using BusOut.DesignPattern;
using UnityEngine;
using UnityEngine.Serialization;

namespace BusOut.Gameplay
{
    /// Vòng đời một ván: khởi tạo, dựng level, thắng / thua, hồi sinh, chơi lại.
    /// MonoBehaviour duy nhất của tầng Presenter; giữ tham chiếu scene và tạo các presenter con:
    /// LevelSpawner (xe trong level), BusStopPresenter (ô đỗ), CarFlowPresenter (điều khiển xe),
    /// PassengerPresenter (hàng khách), BoardingPresenter (khách lên xe, xe đầy rời bến).
    /// Khởi tạo + tạo sẵn pool chạy trong màn Loading (IPreloadStep, GameContext còn tắt) để lần bấm Play đầu không giật;
    /// không có màn Loading (chạy thẳng GameScene) thì Start làm lúc GameContext bật lần đầu.
    public class GameplayPresenter : MonoBehaviour, IPreloadStep
    {
        [SerializeField] private GameplayConfig config;
        [Tooltip("Assets/_BusOut/Resources/UIConfig.asset: số hoạt ảnh popup (dùng chung Menu + màn chơi).")]
        [SerializeField] private UIConfig uiConfig;
        [SerializeField] private ParkingLotView parkingLot;
        [SerializeField] private BusStopView busStopView;
        [FormerlySerializedAs("carInput")]
        [SerializeField] private GameplayInput gameplayInput;

        [Tooltip("CameraFitView trên GameContext/Main Camera: chỉnh FOV theo tỉ lệ màn hình.")]
        [SerializeField] private CameraFitView cameraFit;

        [Header("Level")]
        [Tooltip("File level đã giải mã (Assets/_BusOut/Data/Levels/curve_*.json).")]
        [SerializeField] private TextAsset levelCurve;

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

        [Header("Giao diện trong ván")]
        [Tooltip("TopBarView trên Canvas/TopUI.")]
        [SerializeField] private TopBarView topBar;
        [Tooltip("BoosterBarView trên Canvas/BottomUI/Boosters.")]
        [SerializeField] private BoosterBarView boosterBar;
        [Tooltip("LevelResultView trên bảng kết quả (cha của panel Thắng / Thua).")]
        [SerializeField] private LevelResultView levelResult;
        [Tooltip("TouchFxCanvas/CoinFlyLayer (CoinFlyView): coin thưởng bay về thanh trên. Trống = nhận thưởng về Menu ngay (báo lỗi).")]
        [SerializeField] private CoinFlyView coinFly;
        [Tooltip("NoSlotMessageView trên chữ \"Hết chỗ!\".")]
        [SerializeField] private NoSlotMessageView noSlotMessage;
        [Tooltip("SharedPopupsPresenter trên SharedUI: popup dùng chung (Cài đặt, Leave game) — nút bánh răng ở thanh trên mở.")]
        [SerializeField] private SharedPopupsPresenter sharedPopups;

        [Header("Hướng dẫn đầu game")]
        [Tooltip("TutorialHandView trong Canvas gameplay: bàn tay chỉ xe ở level hướng dẫn.")]
        [SerializeField] private TutorialHandView tutorialHand;
        [Tooltip("BoosterIntroView trên Tutorial/BoosterTut: màn giới thiệu booster vừa mở khoá.")]
        [SerializeField] private BoosterIntroView boosterIntro;

        private LevelCurveData curveData;
        private LevelCurveData ownLevels; // null khi chưa dựng level riêng nào
        private LevelSpawner spawner;
        private BusStopPresenter busStop;
        private CarFlowPresenter carFlow;
        private PassengerPresenter passengers; // null khi thiếu tham chiếu hành khách: xe vẫn chơi được
        private TutorialPresenter tutorial; // null khi thiếu bàn tay: game vẫn chơi được, chỉ mất hướng dẫn
        private BoardingPresenter boarding;    // null cùng lúc với passengers
        private HudPresenter hud;
        private BoosterPresenter booster; // null khi thiếu hàng khách: booster nào cũng sắp lại hàng
        private LevelResultPresenter result; // null khi thiếu bảng kết quả: thắng / thua chỉ còn log
        private PurchasePresenter shop;   // null khi thiếu popup mua: hết lượt thì nút booster chỉ rung
        private bool hasSharedPopups; // false khi thiếu SharedUI: nút bánh răng không làm gì
        private SecureGameData gameData;
        private bool isInitialized; // InitializeCore đã chạy (thành công hay không) — chỉ chạy một lần
        private bool isReady;
        // true khi không có ván nào đang chơi (đã thắng / thua, hoặc level không tải được): ngừng kiểm tra thắng thua,
        // khoá chạm xe và chạm ô.
        private bool levelFinished = true;
        private int currentLevel;
        // Còn bao lâu nữa mới cho chạm (xe đang "bung" ra lúc vào level). Chạm xe khi xe còn cỡ 0 là chạm vào thứ chưa thấy.
        private float interactionLockRemaining;
        // Level đã nạp dưới màn chuyển cảnh, chờ ScreenRevealedEvent mới cho xe "bung".
        private bool isIntroPending;

        #region Khởi tạo, tạo sẵn

        /// Không qua màn Loading: khởi tạo + tạo sẵn ngay lúc GameContext bật lần đầu (lần bấm Play đầu), rồi nạp level.
        /// Qua màn Loading thì Preload đã làm hết: lần bấm Play đầu đi qua GameplayShownEvent như các lần sau.
        private void Start()
        {
            if (isInitialized) return;
            if (!InitializeCore()) return;
            PrewarmCars();
            PrewarmPassengers();
            isReady = true;
            LoadLevel(gameData.currentLevel);
        }

        /// Màn Loading (GameFlowController) gọi khi GameContext còn tắt. Gọi method trên script đang tắt thì được; Awake /
        /// Start của nó chưa chạy — các view được gọi Init ở đây không dựa vào Awake (soát 1/11).
        public IEnumerator Preload(Action<float> reportProgress)
        {
            if (!isInitialized && InitializeCore())
            {
                reportProgress(0.4f);
                yield return null;
                PrewarmCars();
                reportProgress(0.7f);
                yield return null;
                PrewarmPassengers();
                isReady = true;
            }
            reportProgress(1f);
        }

        /// Dựng mọi presenter con + view (không tạo pool). False khi thiếu tham chiếu bắt buộc (đã báo lỗi) — game không chơi được.
        private bool InitializeCore()
        {
            isInitialized = true;
            if (config == null) config = GameplayConfig.CreateDefault();
            if (uiConfig == null)
            {
                Debug.LogError("[BusOut] GameplayPresenter thiếu UI Config: popup chạy số mặc định trong code. " +
                               "Kéo Assets/_BusOut/Resources/UIConfig.asset vào.", this);
                uiConfig = UIConfig.CreateDefault();
            }
            if (!ValidateReferences()) return false;
            if (!parkingLot.Init(config.levelDataMin, config.levelDataMax)) return false;
            if (!busStopView.Init()) return false;
            if (!gameplayInput.Init(config.tapRayDistance)) return false;
            // Thiếu chỉ mất phần chỉnh camera theo màn hình, game vẫn chơi được.
            if (cameraFit == null)
                Debug.LogError("[BusOut] GameplayPresenter thiếu Camera Fit: màn dài sẽ bị cắt hai mép bãi. " +
                               "Thêm CameraFitView vào GameContext/Main Camera rồi kéo vào.", this);
            else
            {
                cameraFit.Init(config.cameraReferenceAspect);
                // Rung camera nghe sự kiện, không ai kéo vào đâu → thiếu thì im lặng mất rung: báo ở đây.
                if (cameraFit.GetComponent<CameraShakeView>() == null)
                    Debug.LogError("[BusOut] GameContext/Main Camera thiếu CameraShakeView: xe đâm xe không rung camera. " +
                                   "Add Component → CameraShakeView lên Main Camera (cùng CameraFitView).", cameraFit);
            }

            // Thiếu chỉ mất coin bay: nhận thưởng thắng về Menu ngay.
            if (coinFly == null)
                Debug.LogError("[BusOut] GameplayPresenter thiếu Coin Fly: nhận thưởng thắng không có coin bay về thanh trên. " +
                               "Add Component CoinFlyView lên TouchFxCanvas/CoinFlyLayer rồi kéo vào.", this);
            ownLevels = LoadOwnLevels();
            // Bộ level riêng có từ level 1 trở đi (E5, bạn chốt 11/11: bỏ curve) thì dùng hẳn bộ này, quay vòng trong chính nó.
            // File curve chỉ còn là dự phòng khi Resources/Levels chưa có level 1 (vd. chỉ có level thử số 1000).
            curveData = ownLevels != null && ownLevels.LastLevelNumber > 0 ? ownLevels : LevelCurveData.Parse(levelCurve.text);
            spawner = new LevelSpawner(config, parkingLot, fourSeatPrefab, sixSeatPrefab, tenSeatPrefab);
            busStop = new BusStopPresenter(busStopView, config);
            carFlow = new CarFlowPresenter(config, parkingLot, spawner, busStop);
            passengers = CreatePassengerPresenter();
            if (passengers != null) boarding = new BoardingPresenter(config, spawner, busStop, passengers, carFlow,
                cameraFit != null ? cameraFit.transform : null);
            // Thiếu biển đếm chỉ mất con số trên biển, game vẫn chơi được.
            if (passengerCount == null)
                Debug.LogError("[BusOut] GameplayPresenter thiếu Passenger Count: biển đếm khách không cập nhật. " +
                               "Kéo CountSign (có PassengerCountView) vào mục Hành khách.", this);
            else if (!passengerCount.Init()) passengerCount = null;
            gameData = DataManager.Instance.GameData;
            if (passengers != null)
                booster = new BoosterPresenter(config, gameData, spawner, busStop, passengers, carFlow, CanUseBooster);
            hud = CreateHudPresenter();
            if (booster != null) booster.ArmedChanged += hud.SetBoosterArmed;
            tutorial = CreateTutorialPresenter();
            result = CreateResultPresenter();
            hasSharedPopups = InitSharedPopups();
            SendLoadingTips();
            shop = CreateShopPresenter();
            CreateNoSlotMessage();
            // Mỗi lần GameFlowController bật xong GameContext (bấm Play). Lần bấm Play đầu mà KHÔNG qua màn Loading thì sự
            // kiện đã phát trước Start (lúc chưa nghe) — Start tự nạp level.
            EventBus.Subscribe<GameplayShownEvent>(HandleGameplayShown);
            EventBus.Subscribe<ScreenRevealedEvent>(HandleScreenRevealed);
            return true;
        }

        /// Pool xe theo level đông nhất của file curve (LevelSpawner.Prewarm).
        private void PrewarmCars()
        {
            spawner.Prewarm(curveData, ownLevels);
            // Hiệu ứng va chạm: tạo sẵn cùng xe — Instantiate lúc cú đâm đầu tiên là giật đúng lúc người chơi đang nhìn.
            FxConfig fx = FxConfig.Instance;
            PoolingManager.Prewarm(fx.rammedEffectPrefab, fx.rammedEffectPrewarmCount);
            busStop.PrewarmEffects();
            // Cần cẩu: một bóng (thường chỉ một xe bay một lúc); bụi dùng chung pool hiệu ứng va chạm nếu cùng prefab.
            if (fx.craneShadowPrefab != null) PoolingManager.Prewarm(fx.craneShadowPrefab, 1);
            if (fx.coinFlyPrefab != null) PoolingManager.Prewarm(fx.coinFlyPrefab, fx.coinFlyCount);
            // Hiệu ứng xe đủ khách: mỗi prefab vài bản (chọn ngẫu nhiên lúc chơi).
            if (fx.carFullEffectPrefabs != null)
                foreach (GameObject prefab in fx.carFullEffectPrefabs)
                    PoolingManager.Prewarm(prefab, fx.carFullEffectPrewarmCount);
        }

        /// Khách: cả hàng + mọi ô đỗ đều là bus đầy khách (trường hợp đông nhất có thể).
        private void PrewarmPassengers() =>
            passengers?.Prewarm(config.passengerQueueCount + busStopView.SlotCount * CarModel.SeatsOf(VehicleType.TenSeat));

        private void OnDestroy()
        {
            hud?.Dispose();
            EventBus.Unsubscribe<GameplayShownEvent>(HandleGameplayShown);
            EventBus.Unsubscribe<ScreenRevealedEvent>(HandleScreenRevealed);
        }

        #endregion

        #region Sự kiện màn hình và nút bấm

        /// Menu bấm Play = vào ván mới với level đang lưu. Nạp dưới màn chuyển cảnh; xe "bung" khi màn che mờ đi
        /// (HandleScreenRevealed) — bung ngay thì xong trước khi người chơi thấy.
        /// ⚠️ Không nạp trong OnEnable: lúc đó GameContext còn đang bật dở, Unity cấm SetParent (trả xe / khách cũ về pool,
        /// lấy xe mới) — lệnh bị bỏ qua, ván mới lẫn xe cũ.
        private void HandleGameplayShown(GameplayShownEvent _)
        {
            if (isReady) LoadLevel(gameData.currentLevel, true);
        }

        private void HandleScreenRevealed(ScreenRevealedEvent _)
        {
            if (!isIntroPending) return;
            isIntroPending = false;
            PlayIntro();
        }

        private void PlayIntro() => interactionLockRemaining = spawner.PlayIntro(config.introCarDuration, config.introSpread);

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
            if (isReady) LoadLevel(gameData.currentLevel);
        }

        private bool HandleBoosterRequested(BoosterType type)
        {
            if (booster == null || (tutorial != null && !tutorial.AllowsBooster(type))) return false;
            bool used = booster.TryUse(type);
            tutorial?.OnBoosterPressed(used);
            return used;
        }

        private bool HandleShopRequested(BoosterType type) => shop != null && shop.Open(type);

        /// ⚠️ Không mở trong lúc chờ popup thắng / thua hiện (1.4s / 1.25s; hiện rồi thì Dim của popup chặn nút): mở được
        /// thì Home → Leave game → về Menu, thắng là mất coin thưởng (coin chỉ cộng khi bấm nhận ở WinPanel).
        private void HandleSettingsRequested()
        {
            if (result != null && result.IsPending) return;
            if (hasSharedPopups) sharedPopups.OpenSettings(PopupContext.InGame);
        }

        /// Chỉ dùng booster khi đang chơi thật sự: không trong lúc xe đang "bung" ra, không khi ván đã kết thúc.
        private bool CanUseBooster() => isReady && !levelFinished && interactionLockRemaining <= 0f && !carFlow.IsRewinding;

        // Popup mua bị khoá riêng: bài hướng dẫn booster vẫn phải dùng được booster qua CanUseBooster.
        private bool CanOpenPurchase() => CanUseBooster() && (tutorial == null || !tutorial.IsActive);

        /// Level riêng trong Resources (mỗi level một file); null khi chưa có level nào.
        private static LevelCurveData LoadOwnLevels()
        {
            TextAsset[] files = Resources.LoadAll<TextAsset>(LevelCurveData.OwnLevelsFolder);
            if (files.Length == 0) return null;
            var levels = new LevelData[files.Length];
            for (int i = 0; i < files.Length; i++) levels[i] = JsonUtility.FromJson<LevelData>(files[i].text);
            return new LevelCurveData { curve = LevelCurveData.OwnLevelsFolder, levels = levels };
        }

        private TutorialPresenter CreateTutorialPresenter()
        {
            if (passengers == null) return null;
            if (tutorialHand == null)
            {
                Debug.LogError("[BusOut] GameplayPresenter thiếu Tutorial Hand: level đầu không có hướng dẫn. " +
                               "Kéo object có TutorialHandView vào mục Hướng dẫn đầu game.", this);
                return null;
            }
            if (!tutorialHand.Init()) return null;
            if (boosterIntro == null)
                Debug.LogError("[BusOut] GameplayPresenter thiếu Booster Intro: bài hướng dẫn booster không có màn giới thiệu. " +
                               "Kéo Tutorial/BoosterTut (có BoosterIntroView) vào mục Hướng dẫn đầu game.", this);
            else if (!boosterIntro.Init(PanelAnimation())) boosterIntro = null;
            return new TutorialPresenter(config, gameData, spawner, passengers, hud, tutorialHand, boosterIntro);
        }

        /// Nút chơi lại trên thanh trên.
        /// ⚠️ Ván đã kết thúc + có popup kết quả → bỏ qua: thắng thì save đã sang level sau, bấm trong lúc chờ WinPanel
        /// (1.4s) là sang level mới mà MẤT coin thưởng (coin chỉ cộng khi bấm nhận ở WinPanel). Không có popup thì vẫn cho
        /// bấm — đó là đường duy nhất để chơi tiếp.
        private void HandleTopBarReplay()
        {
            if (levelFinished && result != null) return;
            ReloadLevel();
        }

        #endregion

        #region Nạp level

        /// ⚠️ Thứ tự có nghĩa: xoá xe cũ (kèm tween/đường chạy) TRƯỚC khi đặt lại ô, không thì xe cũ tới nơi
        /// sau khi reset sẽ đánh dấu ô của level mới là có xe.
        public void LoadLevel(int number) => LoadLevel(number, false);

        /// <paramref name="deferIntro"/>: true = chưa cho xe "bung", chờ ScreenRevealedEvent (đang dưới màn chuyển cảnh).
        private void LoadLevel(int number, bool deferIntro)
        {
            isIntroPending = false;
            result?.Hide();
            shop?.Hide();
            if (hasSharedPopups) sharedPopups.HideAll();
            booster?.Disarm();
            noSlotMessage?.Hide();
            tutorial?.ResetForLevel(number);
            passengers?.DespawnAll();
            spawner.DespawnAll();
            carFlow.ResetForLevel();
            busStop.ResetForLevel(config.openSlotCount);

            // Số level hiển thị tăng mãi; quá level cuối của file thì quay vòng layout từ loopStartLevel.
            int layoutNumber = curveData.ResolveLayoutNumber(number, config.loopStartLevel);
            // Level riêng khớp theo số HIỂN THỊ, không theo layout: level thử số 1000 phải chơi đúng nó, không bị quay vòng về 21.
            LevelData level = ownLevels?.FindLevel(number) ?? curveData.FindLevel(layoutNumber);
            if (level == null || level.vehicles == null)
            {
                Debug.LogError($"[BusOut] Không có level {layoutNumber} (level hiển thị {number}). Level lấy từ save (SecureGameData.currentLevel): xoá save hoặc sửa file curve.", this);
                // ⚠️ Dọn hết trạng thái ván cũ: giữ lại thì bộ lên kế hoạch cũ chạy trên danh sách xe đã xoá (lỗi mỗi frame),
                // và "không xe + không khách" bị tính là thắng.
                passengers?.ResetForLevel(null, false);
                boarding?.ResetForLevel();
                levelFinished = true;
                interactionLockRemaining = 0f;
                return;
            }
            spawner.Spawn(level);
            busStop.SetupConstruction(level.constructionSlots, level.constructionPassengers);
            currentLevel = number;
            levelFinished = false;
            if (passengers != null)
            {
                // Hạt giống = số level HIỂN THỊ: cùng level luôn ra cùng thứ tự màu (dễ thử lại), còn layout quay vòng
                // (121 = layout 21) vẫn ra thứ tự khách khác lần trước.
                var planSettings = new PassengerPlanSettings
                {
                    GroupMin = config.passengerGroupMin,
                    GroupMax = config.passengerGroupMax,
                    HardPointBaseBlockers = config.hardPointBaseBlockers,
                    // Ô công trường lúc đầu không dùng được: điểm khó tính theo số ô thật sự dùng được, không thì kế hoạch vượt sức bến.
                    HardPointSlotLimit = config.openSlotCount - busStop.ConstructionSlotCount - config.hardPointSlotReserve,
                };
                var planner = new PassengerColorPlanner(spawner.Cars, spawner.BuildBlockingGraph(), new System.Random(number),
                    level, planSettings);
                passengers.ResetForLevel(planner, level.passengersHidden);
            }
            boarding?.ResetForLevel();
            // ⚠️ Sau khi dựng đồ thị chặn (trong planner ở trên): hoạt ảnh đặt xe về cỡ 0, dựng đồ thị lúc đó là sai.
            if (deferIntro)
            {
                // Khoá chạm xe / booster tới khi xe bung xong (PlayIntro đặt lại thời gian khoá thật).
                isIntroPending = true;
                interactionLockRemaining = float.PositiveInfinity;
            }
            else PlayIntro();
            EventBus.Publish(new LevelStartedEvent { LevelIndex = number, LevelName = level.index.ToString() });
        }

        #endregion

        #region Mỗi khung: thắng, thua, hồi sinh

        private void Update()
        {
            if (!isReady) return;
            float deltaTime = Time.deltaTime;
            if (interactionLockRemaining > 0f) interactionLockRemaining -= deltaTime;
            passengers?.Tick(deltaTime);
            boarding?.Tick(deltaTime);
            // Không lộ màu trong lúc xe đang bung vào level: cú nảy lộ màu đè lên tween phóng cỡ của intro.
            if (interactionLockRemaining <= 0f) spawner.RevealFreedCars();
            spawner.UpdateLockMarks();
            spawner.UpdateGarages(deltaTime);
            result?.Tick(deltaTime);
            tutorial?.Tick(deltaTime, !levelFinished && interactionLockRemaining <= 0f);
            if (passengers != null && passengerCount != null) passengerCount.SetCount(passengers.RemainingCount);
            CheckLevelCompleted();
            CheckLevelFailed();
        }

        /// Thắng: mọi xe đã rời đi và không còn khách nào phải chở (tổng khách = tổng ghế nên hai điều kiện đi cùng nhau).
        private void CheckLevelCompleted()
        {
            if (levelFinished || passengers == null) return;
            if (!spawner.AllCarsGone || passengers.RemainingCount > 0) return;
            levelFinished = true;
            GameLog.Info($"[BusOut] Thắng level {currentLevel}.", this);
            // Lưu ngay khi thắng (không chờ app vào nền): tắt app ngang lúc này vẫn giữ level mới.
            gameData.CompleteLevel();
            // Coin thưởng cộng khi người chơi bấm nhận ở WinPanel (thường / quảng cáo). Không có popup thì cộng luôn.
            if (result == null) gameData.AddCoins(config.winCoinReward);
            gameData.Save();
            EventBus.Publish(new LevelCompletedEvent { LevelIndex = currentLevel });
            shop?.Hide();
            if (hasSharedPopups) sharedPopups.HideAll();
            booster?.Disarm();
            result?.ShowWinLater(currentLevel);
        }

        /// Thua: hết ô và không còn nước nào gỡ được (xem BoardingPresenter.IsStuck). Báo đúng một lần mỗi ván.
        private void CheckLevelFailed()
        {
            if (levelFinished || boarding == null || !boarding.IsStuck()) return;
            levelFinished = true;
            GameLog.Info($"[BusOut] Thua level {currentLevel}: hết ô đỗ, không xe nào ở bến cùng màu người đầu hàng.", this);
            EventBus.Publish(new LevelFailedEvent { LevelIndex = currentLevel, Reason = "Out of slots" });
            shop?.Hide();
            if (hasSharedPopups) sharedPopups.HideAll();
            booster?.Disarm();
            result?.ShowLoseLater(currentLevel, busStop.HasLockedSlot);
            // Khoảng đứng yên trước popup Continue: báo lý do thua. Mờ xong đúng lúc popup hiện, không đè lên popup.
            noSlotMessage?.Show(Mathf.Max(0f, config.losePopupDelay - config.noSlotMessageFade), config.noSlotMessageFade);
        }

        /// Mua thêm ô (popup mở ô): mở ô khoá gần nhất cho level đang chơi. False khi không còn ô khoá / ván đã kết thúc.
        private bool UnlockExtraSlot() => isReady && !levelFinished && busStop.UnlockNearest() >= 0;

        /// Hồi sinh: +1 ô (ô khoá gần nhất) rồi chơi tiếp đúng thế cờ đang có. Ô mới trống nên IsStuck tự hết đúng.
        /// False khi không hồi sinh được (không trong ván đã thua, hoặc hết ô khoá) — nơi gọi không được trừ coin.
        private bool TryRevive()
        {
            if (!isReady || !levelFinished || busStop.UnlockNearest() < 0) return false;
            result?.Hide();
            levelFinished = false;
            GameLog.Info($"[BusOut] Hồi sinh level {currentLevel}: mở thêm 1 ô.", this);
            return true;
        }

        #endregion

        #region Dựng presenter con, tiện ích

        /// Thiếu thanh nào thì chỉ mất thanh đó, game vẫn chơi được.
        private HudPresenter CreateHudPresenter()
        {
            if (topBar == null)
                Debug.LogError("[BusOut] GameplayPresenter thiếu Top Bar: coin / level không hiện. Kéo Canvas/TopUI (có TopBarView) vào.", this);
            else if (!topBar.Init()) topBar = null;
            if (boosterBar == null)
                Debug.LogError("[BusOut] GameplayPresenter thiếu Booster Bar: nút booster không cập nhật. Kéo Canvas/BottomUI/Boosters (có BoosterBarView) vào.", this);
            else if (!boosterBar.Init()) boosterBar = null;
            return new HudPresenter(config, gameData, topBar, boosterBar, HandleTopBarReplay, HandleBoosterRequested,
                HandleShopRequested, HandleSettingsRequested);
        }

        private LevelResultPresenter CreateResultPresenter()
        {
            if (levelResult == null)
            {
                Debug.LogError("[BusOut] GameplayPresenter thiếu Level Result: thắng / thua không hiện bảng. Kéo bảng kết quả (có LevelResultView) vào.", this);
                return null;
            }
            // Nhận thưởng thắng → về Menu (save đã sang level sau, Menu cho xe chạy sang bệ mới); Replay → nạp lại level.
            return levelResult.Init(PanelAnimation())
                ? new LevelResultPresenter(config, levelResult, gameData, TryRevive, ReloadLevel, GoHome, PlayCoinReward)
                : null;
        }

        private PurchasePresenter CreateShopPresenter()
        {
            // Popup mua nằm ở SharedUI (dùng chung với Shop ở Menu sau này), SharedPopupsPresenter khởi tạo sẵn.
            PurchasePopupView purchasePopup = hasSharedPopups ? sharedPopups.PurchasePopup : null;
            if (purchasePopup == null)
            {
                Debug.LogError("[BusOut] GameplayPresenter không có popup mua (SharedUI thiếu hoặc PurchasePanel chưa kéo vào " +
                               "SharedPopupsPresenter): không mua thêm lượt booster / ô đỗ được.", this);
                return null;
            }
            return new PurchasePresenter(config, gameData, purchasePopup, CanOpenPurchase, UnlockExtraSlot);
        }

        /// Về Menu (GameFlowController tắt GameContext, bật HomeContext). Ván đang dở để nguyên: lần Play sau LoadLevel dọn hết.
        /// Coin thưởng bay từ popup về số coin trên thanh trên, số đếm lên theo coin tới nơi, xong mới gọi
        /// <paramref name="onDone"/> (về Menu). Thiếu view / thanh trên → gọi luôn.
        private void PlayCoinReward(int coinsBefore, int coinsAfter, Vector3 from, Action onDone)
        {
            if (coinFly == null || topBar == null || hud == null)
            {
                onDone();
                return;
            }
            hud.BeginCoinCountUp(coinsBefore);
            bool started = coinFly.Play(from, topBar.CoinTargetPosition,
                (arrived, total) => hud.ShowCountingCoins(coinsBefore + (coinsAfter - coinsBefore) * arrived / total),
                () =>
                {
                    hud.EndCoinCountUp();
                    onDone();
                });
            if (started) return;
            hud.EndCoinCountUp();
            onDone();
        }

        private void GoHome()
        {
            if (!EventBus<GoHomeRequestedEvent>.HasListeners)
                Debug.LogError("[BusOut] Không có ai chuyển về Menu (thiếu GameFlowController): nút về Home không làm gì. " +
                               "Tạo object GameFlow ở gốc scene, gắn GameFlowController, kéo HomeContext + GameContext vào.", this);
            EventBus.Publish(new GoHomeRequestedEvent());
        }

        /// Thiếu SharedUI thì chỉ mất popup cài đặt (nút bánh răng không làm gì), game vẫn chơi được.
        private bool InitSharedPopups()
        {
            if (sharedPopups == null)
            {
                Debug.LogError("[BusOut] GameplayPresenter thiếu Shared Popups: nút cài đặt không mở popup. " +
                               "Kéo SharedUI (có SharedPopupsPresenter) vào.", this);
                return false;
            }
            return true;
        }

        /// Màn Loading chuyển cảnh (Core) hiện tranh + tên + mẹo của từng booster; Core không đọc được GameplayConfig nên
        /// gửi qua LoadingTipsChangedEvent, một lần lúc khởi tạo (trong màn Loading đầu game, trước lần chuyển cảnh đầu tiên).
        private void SendLoadingTips()
        {
            var tips = new List<LoadingTip>(config.boosters.Count);
            foreach (BoosterDefinition booster in config.boosters)
            {
                if (booster.loadingSprite == null)
                {
                    Debug.LogError($"[BusOut] Booster {booster.type} chưa có Loading Sprite: không lên màn Loading. Kéo tranh " +
                                   "Loading_Screen_* vào GameplayConfig › Boosters.", config);
                    continue;
                }
                tips.Add(new LoadingTip(booster.loadingSprite, booster.displayName, booster.loadingDescription));
            }
            if (!EventBus<LoadingTipsChangedEvent>.HasListeners)
                Debug.LogError("[BusOut] Không ai nhận trang Loading (thiếu GameFlowController trong scene): màn chuyển cảnh không " +
                               "có tranh / mẹo booster.", this);
            EventBus.Publish(new LoadingTipsChangedEvent { Tips = tips });
        }

        private PanelAnimationSettings PanelAnimation() => uiConfig.PanelAnimation;

        private void CreateNoSlotMessage()
        {
            if (noSlotMessage == null)
                Debug.LogError("[BusOut] GameplayPresenter thiếu No Slot Message: hết ô chỉ còn xe rung. Kéo chữ \"Hết chỗ!\" (có NoSlotMessageView) vào.", this);
            else if (!noSlotMessage.Init()) noSlotMessage = null;
            if (noSlotMessage != null) carFlow.NoSlotRejected += HandleNoSlotRejected;
        }

        private void HandleNoSlotRejected() => noSlotMessage.Show(config.noSlotMessageHold, config.noSlotMessageFade);

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
            // Xe đang tua ngược (Undo) có collider tắt: chạm xe khác lúc này là xe kia chạy xuyên qua nó.
            if (!isReady || levelFinished || interactionLockRemaining > 0f || carFlow.IsRewinding) return;
            if (tutorial != null && !tutorial.AllowsCar(view)) return;
            // Crane đang giương: cú chạm này là chọn xe để nhấc, không phải chạy xe ra như thường.
            if (booster != null && booster.IsCraneArmed) booster.TryCranePick(view);
            else carFlow.HandleCarTapped(view);
            tutorial?.OnCarTapHandled();
        }

        private void HandleSlotTapped(BusStopSlotView slot)
        {
            // Chạm ô khoá → popup mở thêm ô (coin / quảng cáo); ô mở thì không có gì để làm.
            if (!isReady || levelFinished || interactionLockRemaining > 0f || !busStop.IsLocked(slot.Index)) return;
            if (tutorial != null && tutorial.IsActive) return;
            shop?.OpenSlotUnlock();
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

        #endregion
    }
}
