using System.Collections.Generic;
using BusOut.Core;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Hướng dẫn trong ván, chạy theo danh sách bước: màn giới thiệu, hoặc bàn tay chỉ một xe / một nút booster, chỉ thứ đó nhận chạm.
    /// - Level hướng dẫn (level 1): chỉ vài xe để biết cách chơi.
    /// - Level mở khoá booster: tặng 1 lượt, giới thiệu, rồi bắt dùng thử. Mỗi booster một kịch bản vì cần tình huống khác nhau mới dùng được.
    /// - Level đầu tiên có cơ chế chưa gặp (bãi chờ, xe ẩn, khoá...): chỉ màn giới thiệu, không bắt thao tác.
    /// ⚠️ Bất kỳ bước nào không làm được (không có xe phù hợp, booster từ chối) đều phải kết thúc hướng dẫn ngay: mọi thao tác
    /// khác đang bị khoá, đứng chờ ở đó là người chơi kẹt vĩnh viễn.
    public sealed class TutorialPresenter
    {
        private enum Phase { Off, Waiting, Pointing }
        private enum StepKind { Intro, TapCar, TapBooster }
        private enum CarRule { ServesQueue, NotAtQueueHead, Blocked }

        private readonly struct Step
        {
            public readonly StepKind Kind;
            public readonly CarRule Rule;
            public readonly MechanicIntroDefinition Mechanic; // bước Intro: null = giới thiệu booster của bài học

            public Step(StepKind kind, CarRule rule = default, MechanicIntroDefinition mechanic = null)
            {
                Kind = kind;
                Rule = rule;
                Mechanic = mechanic;
            }
        }

        private readonly GameplayConfig config;
        private readonly SecureGameData gameData;
        private readonly LevelSpawner spawner;
        private readonly PassengerPresenter passengers;
        private readonly HudPresenter hud;
        private readonly TutorialHandView hand;
        private readonly BoosterIntroView intro; // null khi thiếu: bài học bỏ qua màn giới thiệu
        private readonly List<int> queueColors = new List<int>();
        private readonly List<Step> steps = new List<Step>();

        private Phase phase;
        private int stepIndex;
        private float waitRemaining;
        private CarView target;
        private BoosterDefinition lesson; // null khi level này không phải bài học booster

        public TutorialPresenter(GameplayConfig config, SecureGameData gameData, LevelSpawner spawner, PassengerPresenter passengers,
            HudPresenter hud, TutorialHandView hand, BoosterIntroView intro)
        {
            this.config = config;
            this.gameData = gameData;
            this.spawner = spawner;
            this.passengers = passengers;
            this.hud = hud;
            this.hand = hand;
            this.intro = intro;
        }

        /// Đang hướng dẫn: mọi thao tác ngoài bước hiện tại tạm khoá.
        public bool IsActive => phase != Phase.Off;

        /// <paramref name="level"/>: null khi không tải được level (chỉ dọn hướng dẫn cũ).
        public void ResetForLevel(int levelNumber, LevelData level)
        {
            hand.Hide();
            intro?.Hide();
            target = null;
            stepIndex = 0;
            waitRemaining = 0f;
            lesson = null;
            steps.Clear();

            if (levelNumber == config.tutorialLevel)
            {
                for (int i = 0; i < config.tutorialGuidedTaps; i++) steps.Add(new Step(StepKind.TapCar, CarRule.ServesQueue));
            }
            else BuildBoosterLesson(levelNumber);
            if (level != null) AddMechanicIntros(level);
            phase = steps.Count > 0 ? Phase.Waiting : Phase.Off;
        }

        /// <paramref name="canGuide"/>: xe đã bung xong và ván chưa kết thúc.
        public void Tick(float deltaTime, bool canGuide)
        {
            if (phase != Phase.Waiting || !canGuide) return;
            waitRemaining -= deltaTime;
            if (waitRemaining > 0f) return;

            // Tặng lúc xe đã bung xong để số lượt trên nút tăng ngay trước mắt. Chơi lại level không tặng thêm (save có cờ).
            if (lesson != null && stepIndex == 0)
            {
                gameData.GrantBoosterLessonGift(lesson.type);
                gameData.Save();
            }
            ShowCurrentStep();
        }

        public bool AllowsCar(CarView car) =>
            phase == Phase.Off || (phase == Phase.Pointing && steps[stepIndex].Kind == StepKind.TapCar && car == target);

        public bool AllowsBooster(BoosterType type) =>
            phase == Phase.Off || (phase == Phase.Pointing && steps[stepIndex].Kind == StepKind.TapBooster && lesson != null && type == lesson.type);

        /// Gọi sau lượt chạm xe. Xe chưa rời chỗ (đang bận hoạt ảnh) thì giữ nguyên bàn tay để người chơi chạm lại.
        public void OnCarTapHandled()
        {
            if (phase != Phase.Pointing || steps[stepIndex].Kind != StepKind.TapCar) return;
            if (spawner.TryGetCar(target, out CarModel car) && car.State == CarState.Parked) return;
            Advance(config.tutorialStepDelay);
        }

        /// Gọi sau lượt bấm booster, kể cả khi booster từ chối.
        public void OnBoosterPressed(bool used)
        {
            if (phase != Phase.Pointing || steps[stepIndex].Kind != StepKind.TapBooster) return;
            if (used) Advance(config.tutorialStepDelay);
            else Stop();
        }

        private void BuildBoosterLesson(int levelNumber)
        {
            BoosterDefinition booster = FindBoosterUnlockedAt(levelNumber);
            if (booster == null || gameData.IsBoosterLessonDone(booster.type)) return;

            if (intro != null) steps.Add(new Step(StepKind.Intro));
            switch (booster.type)
            {
                case BoosterType.Shuffle:
                    steps.Add(new Step(StepKind.TapBooster));
                    break;
                case BoosterType.Sort:
                case BoosterType.Undo:
                    // Cần sẵn một xe ở bến mà khách đầu hàng không lên được: Sort mới có khách để kéo lên, Undo mới có xe chưa ai nhận ghế.
                    steps.Add(new Step(StepKind.TapCar, CarRule.NotAtQueueHead));
                    steps.Add(new Step(StepKind.TapBooster));
                    break;
                case BoosterType.Crane:
                    steps.Add(new Step(StepKind.TapBooster));
                    steps.Add(new Step(StepKind.TapCar, CarRule.Blocked));
                    break;
                default:
                    Debug.LogError($"[BusOut] Booster {booster.type} chưa có kịch bản hướng dẫn: thêm vào TutorialPresenter.BuildBoosterLesson.");
                    steps.Clear();
                    return;
            }
            lesson = booster;
        }

        /// Đọc chính file level thay vì đặt số level trong config: đổi lịch cơ chế trong bộ level thì màn giới thiệu tự theo.
        /// Nhảy cóc level (công cụ thử) gặp nhiều cơ chế mới cùng lúc thì giới thiệu lần lượt.
        private void AddMechanicIntros(LevelData level)
        {
            if (intro == null || config.mechanicIntros == null) return;
            foreach (MechanicIntroDefinition mechanic in config.mechanicIntros)
            {
                if (mechanic == null || gameData.IsMechanicIntroSeen((int)mechanic.mechanic)) continue;
                if (HasMechanic(level, mechanic.mechanic)) steps.Add(new Step(StepKind.Intro, mechanic: mechanic));
            }
        }

        private static bool HasMechanic(LevelData level, LevelMechanic mechanic)
        {
            switch (mechanic)
            {
                case LevelMechanic.HiddenPassenger: return level.passengersHidden;
                case LevelMechanic.Construction: return level.constructionSlots > 0;
            }
            foreach (VehicleData vehicle in level.vehicles)
            {
                switch (mechanic)
                {
                    case LevelMechanic.Garage: if (vehicle.garageId > 0) return true; break;
                    case LevelMechanic.HiddenCar: if (vehicle.colorHidden) return true; break;
                    case LevelMechanic.LockKey: if (vehicle.lockId > 0) return true; break;
                }
            }
            return false;
        }

        private BoosterDefinition FindBoosterUnlockedAt(int levelNumber)
        {
            foreach (BoosterDefinition booster in config.boosters)
                if (booster.unlockLevel == levelNumber) return booster;
            return null;
        }

        private void ShowCurrentStep()
        {
            Step step = steps[stepIndex];
            switch (step.Kind)
            {
                case StepKind.Intro:
                    if (step.Mechanic != null) intro.Show(step.Mechanic, () => Advance(0f));
                    else intro.Show(lesson, () => Advance(0f));
                    break;
                case StepKind.TapBooster:
                    RectTransform button = hud?.GetBoosterRect(lesson.type);
                    if (button == null)
                    {
                        Stop();
                        return;
                    }
                    hand.ShowAtButton(button);
                    break;
                default:
                    target = FindCar(step.Rule);
                    if (target == null)
                    {
                        Stop();
                        return;
                    }
                    hand.ShowAt(target.transform.position);
                    break;
            }
            phase = Phase.Pointing;
        }

        private void Advance(float delayBeforeNextStep)
        {
            // Đánh dấu ngay khi đóng màn, không chờ hết bài: thoát giữa chừng thì lần sau không bắt đọc lại.
            MechanicIntroDefinition mechanic = steps[stepIndex].Mechanic;
            if (mechanic != null)
            {
                gameData.MarkMechanicIntroSeen((int)mechanic.mechanic);
                gameData.Save();
            }
            hand.Hide();
            target = null;
            stepIndex++;
            if (stepIndex < steps.Count)
            {
                phase = Phase.Waiting;
                waitRemaining = delayBeforeNextStep;
                return;
            }
            phase = Phase.Off;
            if (lesson == null) return;
            gameData.MarkBoosterLessonDone(lesson.type);
            gameData.Save();
        }

        /// Dừng giữa chừng: chưa đánh dấu đã học, chơi lại level sẽ hướng dẫn tiếp (lượt tặng vẫn giữ, không tặng lại).
        private void Stop()
        {
            hand.Hide();
            intro?.Hide();
            target = null;
            phase = Phase.Off;
        }

        #region Chọn xe

        private CarView FindCar(CarRule rule)
        {
            passengers.CopyQueueColors(queueColors);
            CarModel car = null;
            switch (rule)
            {
                case CarRule.ServesQueue:
                    car = FindServingCar();
                    break;
                case CarRule.NotAtQueueHead:
                    if (queueColors.Count > 0) car = FindFreeCarBehindQueueHead(queueColors[0]);
                    break;
                case CarRule.Blocked:
                    car = FindParkedCar(true) ?? FindParkedCar(false);
                    break;
            }
            return car != null ? spawner.GetView(car.Id) : null;
        }

        /// Xe ra được ngay, cùng màu khách sớm nhất trong hàng mà chưa có xe nào đang đón: chạm đúng xe này là thấy khách lên xe.
        private CarModel FindServingCar()
        {
            foreach (int color in queueColors)
            {
                if (HasCarServing(color)) continue;
                foreach (CarModel car in spawner.Cars)
                    if (car.State == CarState.Parked && IsShown(car) && car.ColorIndex == color && IsPathClear(car)) return car;
                return null;
            }
            return null;
        }

        /// Ưu tiên màu đã có trong hàng: màu chưa lên kế hoạch thì Sort không có khách nào để kéo lên và sẽ từ chối.
        private CarModel FindFreeCarBehindQueueHead(int headColor)
        {
            foreach (int color in queueColors)
            {
                if (color == headColor) continue;
                foreach (CarModel car in spawner.Cars)
                    if (car.State == CarState.Parked && IsShown(car) && car.ColorIndex == color && IsPathClear(car)) return car;
            }
            foreach (CarModel car in spawner.Cars)
                if (car.State == CarState.Parked && IsShown(car) && car.ColorIndex != headColor && IsPathClear(car)) return car;
            return null;
        }

        /// Xe có khách đứng sớm nhất trong hàng, để nhấc lên là thấy khách lên xe ngay.
        private CarModel FindParkedCar(bool blockedOnly)
        {
            foreach (int color in queueColors)
                foreach (CarModel car in spawner.Cars)
                    if (car.State == CarState.Parked && IsShown(car) && car.ColorIndex == color && (!blockedOnly || !IsPathClear(car))) return car;
            return null;
        }

        // Xe còn trong cổng bãi chờ (đang tắt) không chạm được: bàn tay chỉ vào đó là người chơi kẹt.
        private bool IsShown(CarModel car)
        {
            CarView view = spawner.GetView(car.Id);
            return view != null && view.gameObject.activeSelf;
        }

        private bool HasCarServing(int color)
        {
            foreach (CarModel car in spawner.Cars)
                if (car.ColorIndex == color && car.HasFreeSeat && (car.State == CarState.MovingToSlot || car.State == CarState.InSlot))
                    return true;
            return false;
        }

        private bool IsPathClear(CarModel car)
        {
            IReadOnlyList<CarModel> cars = spawner.Cars;
            foreach (int blockerId in spawner.Graph.AheadOf(car.Id))
                if (cars[blockerId].State == CarState.Parked) return false;
            return true;
        }

        #endregion
    }
}
