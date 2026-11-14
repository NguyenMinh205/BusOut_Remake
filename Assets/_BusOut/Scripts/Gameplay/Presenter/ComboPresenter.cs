using BusOut.Core;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Chữ khen combo: xe đủ khách liên tiếp (mỗi xe cách xe trước không quá FxConfig.comboWindow giây) thì combo tăng,
    /// đạt mốc thì hiện chữ khen + phát tiếng của mốc. Chỉ là phản hồi: không đổi luật, không thưởng gì.
    /// GameplayPresenter tạo, gọi ResetForLevel khi vào level.
    public sealed class ComboPresenter
    {
        private readonly FxConfig fx;
        private readonly ComboPraiseView view;

        private int combo;
        private float lastFillTime;

        public ComboPresenter(FxConfig fx, ComboPraiseView view, BoardingPresenter boarding)
        {
            this.fx = fx;
            this.view = view;
            boarding.CarFilled += HandleCarFilled;
            ValidateTiers();
        }

        public void ResetForLevel()
        {
            combo = 0;
            view.Hide();
        }

        private void HandleCarFilled()
        {
            // Time.time (không phải realtime): popup / quảng cáo dừng thời gian thì không làm đứt combo oan.
            float now = Time.time;
            combo = combo > 0 && now - lastFillTime <= fx.comboWindow ? combo + 1 : 1;
            lastFillTime = now;

            ComboTier tier = FindTier(combo);
            if (tier == null) return;
            if (tier.sprite != null) view.Show(tier.sprite, fx);
            AudioService.Play(tier.sound);
        }

        /// Mốc lớn nhất mà combo đã đạt; null khi chưa tới mốc đầu.
        private ComboTier FindTier(int count)
        {
            ComboTier best = null;
            if (fx.comboTiers == null) return null;
            foreach (ComboTier tier in fx.comboTiers)
                if (tier != null && count >= tier.minCombo && (best == null || tier.minCombo > best.minCombo)) best = tier;
            return best;
        }

        private void ValidateTiers()
        {
            if (fx.comboTiers == null || fx.comboTiers.Length == 0)
            {
                Debug.LogError("[BusOut] FxConfig chưa có Combo Tiers: không có chữ khen combo. Thêm mốc (Min Combo, Sprite, Sound) " +
                               "vào Resources/FxConfig.", fx);
                return;
            }
            foreach (ComboTier tier in fx.comboTiers)
                if (tier != null && tier.sprite == null)
                    Debug.LogError($"[BusOut] FxConfig › Combo Tiers: mốc {tier.minCombo} thiếu Sprite, mốc đó chỉ phát tiếng. " +
                                   "Kéo sprite trong Art/BusOut/UI/Combo/ vào.", fx);
        }
    }
}
