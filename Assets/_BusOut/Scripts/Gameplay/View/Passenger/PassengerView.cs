using System;
using System.Collections.Generic;
using BusOut.DesignPattern;
using DG.Tweening;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Hình và chuyển động của một hành khách. Gắn trên root prefab Passenger. Được pool (PoolingManager).
    public class PassengerView : MonoBehaviour, IPoolable
    {
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int VipPropertyId = Shader.PropertyToID("_VipPerson");
        // Tham số bool trong Animator Controller của khách: true khi đang đi, false khi đứng.
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        [Tooltip("Renderer thân khách (mesh Character, shader BusOut/Character) — nhận màu qua MaterialPropertyBlock.")]
        [SerializeField] private Renderer bodyRenderer;

        [Tooltip("Không bắt buộc. Khi có clip đi bộ: Animator Controller cần tham số bool \"IsMoving\" (đi = true, đứng = false).")]
        [SerializeField] private Animator animator;

        private readonly List<Vector3> walkPath = new List<Vector3>(8);
        private int walkIndex;
        private float walkSpeed;
        private float walkTurnSharpness;
        private bool isWalking;
        private Quaternion arriveRotation;
        private Action<PassengerView> onArrived;
        private MaterialPropertyBlock propertyBlock;

        private Vector3 defaultLocalScale;
        private bool hasDefaultScale;
        // Đổi màu chờ cột sáng quét tới (Sort / Shuffle): màu + độ nảy giữ tới lúc áp.
        private Tween recolorDelay;
        private Color pendingColor;
        private float pendingPunch;
        private float pendingDuration;
        private TweenCallback applyPendingRecolor;

        // Khách ẩn: màu thật giữ ở đây, trên người đang tô hiddenColor. Booster đổi màu lúc khách còn ẩn thì chỉ đổi màu thật.
        private Color trueColor;
        private Color hiddenColor;

        public int PassengerId { get; private set; } = -1;
        public bool IsWalking => isWalking;
        public bool IsColorHidden { get; private set; }

        public bool Init(int passengerId, Color color, bool isVip)
        {
            PassengerId = passengerId;
            if (bodyRenderer == null)
            {
                Debug.LogError($"[BusOut] Prefab {name}: PassengerView thiếu bodyRenderer. Kéo MeshRenderer của thân khách vào.", this);
                return false;
            }
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            trueColor = color;
            IsColorHidden = false;
            bodyRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorPropertyId, color);
            propertyBlock.SetFloat(VipPropertyId, isVip ? 1f : 0f);
            bodyRenderer.SetPropertyBlock(propertyBlock);
            return true;
        }

        /// Khách ẩn: tô màu che, không nảy (khách mới vào cuối hàng).
        public void HideColor(Color color)
        {
            if (IsColorHidden) return;
            IsColorHidden = true;
            hiddenColor = color;
            SetBodyColor(color);
        }

        /// Lộ màu thật + nảy nhẹ.
        public void RevealColor(float punch, float duration)
        {
            if (!IsColorHidden) return;
            IsColorHidden = false;
            Recolor(trueColor, punch, duration);
        }

        private void SetBodyColor(Color color)
        {
            if (propertyBlock == null || bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorPropertyId, color);
            bodyRenderer.SetPropertyBlock(propertyBlock);
        }

        /// Booster (Sort / Shuffle): đổi màu sau <paramref name="delay"/> giây — đúng lúc cột sáng quét qua chỗ khách đứng.
        /// Gọi lại khi đang chờ thì bỏ lần chờ cũ (màu mới nhất mới đúng với Model).
        public void Recolor(Color color, float punch, float duration, float delay)
        {
            KillRecolorDelay();
            if (delay <= 0f)
            {
                Recolor(color, punch, duration);
                return;
            }
            pendingColor = color;
            pendingPunch = punch;
            pendingDuration = duration;
            if (applyPendingRecolor == null) applyPendingRecolor = ApplyPendingRecolor;
            // SetTarget(transform): OnDespawned (DOKill) huỷ luôn — không thì khách đã về pool, lấy ra lượt sau bị tô màu cũ.
            recolorDelay = DOVirtual.DelayedCall(delay, applyPendingRecolor).SetLink(gameObject).SetTarget(transform);
        }

        /// Rời hàng (lên xe) khi màu còn đang chờ: áp ngay — không thì khách màu cũ đi vào xe màu mới.
        public void FlushPendingRecolor()
        {
            if (recolorDelay == null || !recolorDelay.IsActive()) return;
            KillRecolorDelay();
            ApplyPendingRecolor();
        }

        private void ApplyPendingRecolor()
        {
            recolorDelay = null;
            Recolor(pendingColor, pendingPunch, pendingDuration);
        }

        private void KillRecolorDelay()
        {
            if (recolorDelay != null && recolorDelay.IsActive()) recolorDelay.Kill();
            recolorDelay = null;
        }

        /// Booster (Sort / Shuffle) đổi màu khách đang đứng: tô màu mới + nảy nhẹ cho người chơi thấy chỗ đổi.
        public void Recolor(Color color, float punch, float duration)
        {
            if (propertyBlock == null || bodyRenderer == null) return;
            trueColor = color;
            // ⚠️ Khách còn ẩn: vẫn nảy (người chơi thấy hàng được sắp lại) nhưng không để lộ màu mới.
            SetBodyColor(IsColorHidden ? hiddenColor : color);
            // Nảy theo cỡ gốc: Complete cú nảy cũ trước để không cộng dồn lệch cỡ.
            transform.DOComplete();
            Vector3 baseScale = hasDefaultScale ? defaultLocalScale : transform.localScale;
            transform.DOPunchScale(baseScale * punch, duration, 6, 0.5f)
                .SetLink(gameObject)
                .SetTarget(transform);
        }

        /// Đi lần lượt qua các điểm với tốc độ đều, mặt quay theo hướng đi; tới nơi quay về <paramref name="endRotation"/>.
        /// Gọi khi đang đi dở thì bỏ đường cũ, đi đường mới từ vị trí hiện tại.
        public void WalkTo(List<Vector3> points, float speed, float turnSharpness, Quaternion endRotation,
            Action<PassengerView> onFinished = null)
        {
            walkPath.Clear();
            walkPath.AddRange(points);
            walkIndex = 0;
            walkSpeed = speed;
            walkTurnSharpness = turnSharpness;
            arriveRotation = endRotation;
            onArrived = onFinished;
            isWalking = walkPath.Count > 0;
            SetMovingAnimation(isWalking);
            if (!isWalking) FinishWalk();
        }

        private void Update()
        {
            if (!isWalking) return;

            float deltaTime = Time.deltaTime;
            float remaining = walkSpeed * deltaTime;
            Vector3 position = transform.position;
            // Dồn quãng đường dư sang điểm sau để tốc độ đều qua các góc (như CarView).
            while (remaining > 0f && walkIndex < walkPath.Count)
            {
                Vector3 toTarget = walkPath[walkIndex] - position;
                float distance = toTarget.magnitude;
                if (distance <= remaining)
                {
                    position = walkPath[walkIndex];
                    remaining -= distance;
                    walkIndex++;
                }
                else
                {
                    position += toTarget / distance * remaining;
                    remaining = 0f;
                }
            }

            if (walkIndex < walkPath.Count)
            {
                Vector3 look = walkPath[walkIndex] - position;
                look.y = 0f;
                if (look.sqrMagnitude > 1e-4f)
                {
                    float t = 1f - Mathf.Exp(-walkTurnSharpness * deltaTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), t);
                }
            }
            transform.position = position;

            if (walkIndex >= walkPath.Count) FinishWalk();
        }

        private void FinishWalk()
        {
            isWalking = false;
            transform.rotation = arriveRotation;
            SetMovingAnimation(false);
            Action<PassengerView> callback = onArrived;
            onArrived = null;
            callback?.Invoke(this);
        }

        private void SetMovingAnimation(bool moving)
        {
            // Chưa gắn Animator / chưa có Controller / đang tắt thì bỏ qua: gọi SetBool lúc đó Unity in cảnh báo mỗi lần.
            // ⚠️ Khách ngồi trên xe nằm dưới ghế, ghế tắt khi đóng nóc → lúc xe bị xoá, trả khách về pool gọi vào đây với
            // Animator đang tắt (10 khách / bus). Bật lại từ pool thì Animator tự về tham số mặc định (IsMoving = false).
            if (animator != null && animator.runtimeAnimatorController != null && animator.isActiveAndEnabled)
                animator.SetBool(IsMovingHash, moving);
        }

        /// Đã ngồi lên ghế: chốt tư thế đứng (khung đầu của state mặc định = Idle) rồi tắt Animator.
        /// ⚠️ Mỗi khách là Animator + SkinnedMesh; xe ở bến mở nóc thì tới ~40 khách ngồi cùng lúc vẫn chạy Idle cộng với
        /// 25 người trong hàng. Khách ngồi không cần chuyển động. Rebind trước khi Update(0) để không đứng hình giữa bước
        /// chân (vừa chuyển Walk → Idle chưa xong).
        public void FreezeSeatedPose()
        {
            if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled) return;
            animator.Rebind();
            animator.Update(0f);
            animator.enabled = false;
        }

        /// Lấy lại scale của prefab: lần dùng trước khách có thể đã bị thu nhỏ khi ngồi trên xe.
        public void OnSpawned()
        {
            if (!hasDefaultScale)
            {
                defaultLocalScale = transform.localScale;
                hasDefaultScale = true;
            }
            transform.localScale = defaultLocalScale;
            // Lần dùng trước đã ngồi trên xe (FreezeSeatedPose tắt Animator): bật lại, Animator tự về state mặc định.
            if (animator != null) animator.enabled = true;
        }

        /// Trả về pool: dừng đi, bỏ callback cũ (không thì khách của lượt trước gọi nhầm vào logic lượt sau).
        public void OnDespawned()
        {
            isWalking = false;
            onArrived = null;
            walkPath.Clear();
            PassengerId = -1;
            SetMovingAnimation(false);
            // ⚠️ Cú nảy đổi màu đang chạy dở mà trả về pool → lần lấy ra sau cỡ bị lệch. Dừng và trả cỡ gốc (huỷ luôn
            // lần đổi màu đang chờ cột sáng).
            transform.DOKill();
            recolorDelay = null;
            if (hasDefaultScale) transform.localScale = defaultLocalScale;
        }
    }
}
