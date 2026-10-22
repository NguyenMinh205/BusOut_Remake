using System;

namespace BusOut.Gameplay
{
    /// Bảng kết quả cuối ván: chờ một chút sau khi thắng / thua (gốc: 1.4s / 1.25s) rồi hiện, chuyển nút bấm về
    /// GameplayPresenter. GameplayPresenter tạo, gọi Tick mỗi frame và Hide khi vào level.
    public sealed class LevelResultPresenter
    {
        private enum Pending { None, Win, Lose }

        private readonly GameplayConfig config;
        private readonly LevelResultView view;

        private Pending pending;
        private float timer;
        private int coinReward;
        private bool canRevive;

        public LevelResultPresenter(GameplayConfig config, LevelResultView view, Action onNext, Action onRevive, Action onReplay)
        {
            this.config = config;
            this.view = view;
            view.NextClicked += onNext;
            view.ReviveClicked += onRevive;
            view.ReplayClicked += onReplay;
        }

        public void ShowWinLater(int reward)
        {
            pending = Pending.Win;
            timer = config.winPopupDelay;
            coinReward = reward;
        }

        public void ShowLoseLater(bool revivable)
        {
            pending = Pending.Lose;
            timer = config.losePopupDelay;
            canRevive = revivable;
        }

        /// Huỷ cả bảng đang chờ hiện: chơi lại ngay trong lúc chờ thì bảng của ván cũ không được bật lên.
        public void Hide()
        {
            pending = Pending.None;
            view.Hide();
        }

        public void Tick(float deltaTime)
        {
            if (pending == Pending.None) return;
            timer -= deltaTime;
            if (timer > 0f) return;
            if (pending == Pending.Win) view.ShowWin(coinReward);
            else view.ShowLose(canRevive);
            pending = Pending.None;
        }
    }
}
