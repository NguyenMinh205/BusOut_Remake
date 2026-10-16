using System;
using System.Collections.Generic;
using BusOut.DesignPattern;
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

        public int PassengerId { get; private set; } = -1;
        public bool IsWalking => isWalking;

        public bool Init(int passengerId, Color color, bool isVip)
        {
            PassengerId = passengerId;
            if (bodyRenderer == null)
            {
                Debug.LogError($"[BusOut] Prefab {name}: PassengerView thiếu bodyRenderer. Kéo MeshRenderer của thân khách vào.", this);
                return false;
            }
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorPropertyId, color);
            propertyBlock.SetFloat(VipPropertyId, isVip ? 1f : 0f);
            bodyRenderer.SetPropertyBlock(propertyBlock);
            return true;
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
            // Chưa gắn Animator / chưa có Controller thì bỏ qua: gọi SetBool lúc đó Unity in cảnh báo mỗi lần.
            if (animator != null && animator.runtimeAnimatorController != null) animator.SetBool(IsMovingHash, moving);
        }

        public void OnSpawned() { }

        /// Trả về pool: dừng đi, bỏ callback cũ (không thì khách của lượt trước gọi nhầm vào logic lượt sau).
        public void OnDespawned()
        {
            isWalking = false;
            onArrived = null;
            walkPath.Clear();
            PassengerId = -1;
            SetMovingAnimation(false);
        }
    }
}
