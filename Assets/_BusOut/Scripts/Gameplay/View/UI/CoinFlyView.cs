using System;
using System.Collections.Generic;
using BusOut.Core;
using BusOut.DesignPattern;
using DG.Tweening;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Coin bay từ phần thưởng (popup thắng) về số coin trên thanh trên, theo đường cong, lệch nhịp nhau.
    /// Gắn trên TouchFxCanvas/CoinFlyLayer (RectTransform kéo giãn toàn màn): canvas đó nằm TRÊN popup và không có Graphic
    /// Raycaster nên coin bay qua popup mà không chặn chạm. Prefab + số trong FxConfig (nhóm UI: coin bay).
    public class CoinFlyView : MonoBehaviour
    {
        private readonly List<GameObject> flyingCoins = new List<GameObject>(16);
        private readonly List<Tween> tweens = new List<Tween>(16);

        /// Bay <c>FxConfig.coinFlyCount</c> coin từ <paramref name="from"/> tới <paramref name="to"/> (vị trí thế giới của
        /// RectTransform — Canvas Overlay: toạ độ màn hình). Mỗi coin tới nơi gọi <paramref name="onCoinArrived"/>(đã tới,
        /// tổng); coin cuối tới + chờ một chút thì gọi <paramref name="onComplete"/>.
        /// Trả false (không gọi gì) khi không bay được — nơi gọi tự đi tiếp.
        public bool Play(Vector3 from, Vector3 to, Action<int, int> onCoinArrived, Action onComplete)
        {
            FxConfig fx = FxConfig.Instance;
            if (fx.coinFlyPrefab == null || fx.coinFlyCount <= 0) return false;
            Stop();
            // Số trong config tính theo đơn vị UI (Reference Resolution 1170×2532); đổi ra toạ độ màn hình theo cỡ canvas.
            float uiToWorld = transform.lossyScale.x;
            int count = fx.coinFlyCount;
            int arrived = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 start = from + (Vector3)(UnityEngine.Random.insideUnitCircle * (fx.coinFlySpread * uiToWorld));
                // Điểm uốn: giữa đường, lệch ngang ngẫu nhiên hai phía → các coin bay toả ra rồi gom về một chỗ.
                Vector3 direction = to - start;
                Vector3 side = new Vector3(-direction.y, direction.x, 0f).normalized;
                Vector3 control = (start + to) * 0.5f + side * (UnityEngine.Random.Range(-1f, 1f) * fx.coinFlyCurve * uiToWorld);

                GameObject coin = PoolingManager.Spawn(fx.coinFlyPrefab, start, Quaternion.identity, transform);
                if (coin == null) continue;
                Transform coinTransform = coin.transform;
                coinTransform.localScale = Vector3.one;
                flyingCoins.Add(coin);
                float endScale = fx.coinFlyEndScale;
                Tween tween = DOVirtual.Float(0f, 1f, fx.coinFlyDuration, t =>
                    {
                        // Bézier bậc 2: start → control → to.
                        float u = 1f - t;
                        coinTransform.position = u * u * start + 2f * u * t * control + t * t * to;
                        coinTransform.localScale = Vector3.one * Mathf.Lerp(1f, endScale, t);
                    })
                    .SetDelay(i * fx.coinFlyInterval)
                    .SetEase(Ease.InOutSine)
                    .SetLink(coin)
                    .OnComplete(() =>
                    {
                        flyingCoins.Remove(coin);
                        PoolingManager.Despawn(coin);
                        arrived++;
                        onCoinArrived?.Invoke(arrived, count);
                        if (arrived == count)
                            tweens.Add(DOVirtual.DelayedCall(fx.coinFlyEndDelay, () => onComplete?.Invoke()).SetLink(gameObject));
                    });
                tweens.Add(tween);
            }
            return true;
        }

        // Tắt GameContext giữa lúc bay (không xảy ra trong luồng thường — luồng chờ coin bay xong mới về Menu): dừng hết,
        // trả coin về pool, không để coin nằm lại trên màn.
        private void OnDisable() => Stop();

        private void Stop()
        {
            for (int i = 0; i < tweens.Count; i++)
                if (tweens[i] != null && tweens[i].IsActive()) tweens[i].Kill();
            tweens.Clear();
            for (int i = 0; i < flyingCoins.Count; i++)
                if (flyingCoins[i] != null) PoolingManager.Despawn(flyingCoins[i]);
            flyingCoins.Clear();
        }
    }
}
