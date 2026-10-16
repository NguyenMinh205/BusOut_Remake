using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace BusOut.Gameplay
{
    /// Hình và chuyển động của một xe. Gắn trên root prefab Car / Van / Bus.
    /// Không giữ luật: Presenter quyết định xe đi đâu, View chỉ diễn.
    public class CarView : MonoBehaviour
    {
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        // Dùng chung cho mọi xe: kiểm tra chặn chỉ chạy khi chạm, không chạy song song.
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

        [Tooltip("Collider thân xe (trên root). Dùng để xe khác kiểm tra bị chặn — KHÔNG phải SelectionBox.")]
        [SerializeField] private BoxCollider bodyCollider;

        [Tooltip("Collider vùng chạm (SelectionBox), to hơn thân để dễ chạm trúng.")]
        [SerializeField] private Collider selectionCollider;

        [Tooltip("Renderer của Body — nhận màu xe qua MaterialPropertyBlock.")]
        [SerializeField] private Renderer bodyRenderer;

        [Tooltip("Mũi tên chỉ hướng đi trên nóc (object Mark). Ẩn khi xe đã ra tới làn trên.")]
        [SerializeField] private GameObject markObject;

        private readonly List<Vector3> drivePath = new List<Vector3>(12);
        private int drivePathIndex;
        private int driveReverseCount;
        private int driveMilestoneIndex;
        private Action<CarView> onDriveMilestone;
        private float driveSpeed;
        private float driveTurnSharpness;
        private bool isDriving;
        private bool hasArriveRotation;
        private Quaternion arriveRotation;
        private Action<CarView> onDriveFinished;

        private Sequence ramSequence;
        private Tween tiltTween;
        private Tween shakeTween;
        private Quaternion restRotation;

        public int CarId { get; private set; }

        /// Đang lao tới / lùi về / rung — chạm thêm thì bỏ qua để xe không bị dịch khỏi chỗ đỗ.
        public bool IsBusy => (ramSequence != null && ramSequence.IsActive()) || (shakeTween != null && shakeTween.IsActive());

        /// Nửa chiều dài thân xe theo hướng đầu xe (thế giới), tính từ tâm collider thân.
        public float HalfLength => bodyCollider.size.z * 0.5f * transform.lossyScale.z;

        public bool Init(int carId, Color color)
        {
            CarId = carId;
            if (bodyCollider == null || selectionCollider == null || bodyRenderer == null || markObject == null)
            {
                Debug.LogError($"[BusOut] Prefab {name}: CarView thiếu tham chiếu (bodyCollider / selectionCollider / bodyRenderer / markObject). " +
                               "Mở prefab trong Assets/_BusOut/Prefab, kéo BoxCollider ở root, BoxCollider của SelectionBox, MeshRenderer của Body và object Mark vào.", this);
                return false;
            }

            var block = new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor(ColorPropertyId, color);
            bodyRenderer.SetPropertyBlock(block);

            restRotation = transform.rotation;
            return true;
        }

        public void SetCollidersEnabled(bool body, bool selection)
        {
            bodyCollider.enabled = body;
            selectionCollider.enabled = selection;
        }

        public void SetMarkVisible(bool visible) => markObject.SetActive(visible);

        /// Collider này có phải thân của xe này không (để phân biệt với SelectionBox khi tia trúng).
        public bool IsBody(Collider other) => other == bodyCollider;

        /// Bắn 3 tia (trái / giữa / phải thân xe) theo hướng đầu xe, như game gốc.
        /// Tia xuất phát từ TÂM thân: Physics bỏ qua collider chứa điểm gốc tia, nên tia không trúng chính xe này,
        /// và xe chặn đỗ sát đầu xe (không có khe hở) vẫn bị phát hiện.
        /// <paramref name="hitDistance"/> tính từ tâm thân xe.
        public bool FindBlocker(float maxDistance, float sideInset, out CarView blocker, out float hitDistance)
        {
            blocker = null;
            hitDistance = float.MaxValue;

            Transform bodyTransform = bodyCollider.transform;
            Vector3 center = bodyTransform.TransformPoint(bodyCollider.center);
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float halfWidth = bodyCollider.size.x * 0.5f * bodyTransform.lossyScale.x - sideInset;
            if (halfWidth < 0f) halfWidth = 0f;

            for (int side = -1; side <= 1; side++)
            {
                Vector3 origin = center + right * (halfWidth * side);
                int count = Physics.RaycastNonAlloc(origin, forward, HitBuffer, maxDistance, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = HitBuffer[i];
                    if (hit.distance >= hitDistance) continue;
                    // ⚠️ Chỉ tính thân xe: SelectionBox to hơn thân, tính cả nó thì xe đỗ cạnh nhau bị coi là chặn oan.
                    var other = hit.collider.GetComponentInParent<CarView>();
                    if (other == null || other == this || !other.IsBody(hit.collider)) continue;
                    hitDistance = hit.distance;
                    blocker = other;
                }
            }
            return blocker != null;
        }

        /// Mọi xe nằm trên đường ra (3 tia như FindBlocker, nhưng lấy hết chứ không chỉ xe gần nhất), thêm vào
        /// <paramref name="result"/> (không trùng). Dùng dựng đồ thị chặn lúc vào level.
        public void FindAllAhead(float maxDistance, float sideInset, List<CarView> result)
        {
            Transform bodyTransform = bodyCollider.transform;
            Vector3 center = bodyTransform.TransformPoint(bodyCollider.center);
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float halfWidth = Mathf.Max(0f, bodyCollider.size.x * 0.5f * bodyTransform.lossyScale.x - sideInset);

            for (int side = -1; side <= 1; side++)
            {
                Vector3 origin = center + right * (halfWidth * side);
                int count = Physics.RaycastNonAlloc(origin, forward, HitBuffer, maxDistance, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var other = HitBuffer[i].collider.GetComponentInParent<CarView>();
                    if (other == null || other == this || !other.IsBody(HitBuffer[i].collider) || result.Contains(other)) continue;
                    result.Add(other);
                }
            }
        }

        /// Bị chặn: lao tới sát xe chặn, gọi <paramref name="onImpact"/> lúc chạm, rồi lùi về chỗ cũ.
        public void PlayRam(float travel, GameplayConfig config, Action onImpact)
        {
            KillRam();
            Vector3 start = transform.position;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            Vector3 target = start + forward.normalized * travel;
            float duration = Mathf.Max(config.rammedMinDuration, travel / Mathf.Max(0.01f, config.rammedMoveSpeed));

            ramSequence = DOTween.Sequence()
                .Append(transform.DOMove(target, duration).SetEase(Ease.InQuad))
                .AppendCallback(() => onImpact?.Invoke())
                .Append(transform.DOMove(start, config.rammedReturnDuration).SetEase(Ease.OutQuad))
                .SetLink(gameObject)
                .SetTarget(transform);
        }

        /// Bị xe khác đâm: lắc nhẹ rồi về tư thế đỗ.
        public void PlayRammed(Vector3 hitDirection, GameplayConfig config)
        {
            if (isDriving) return;
            KillTilt();

            hitDirection.y = 0f;
            hitDirection.Normalize();
            float alongForward = Vector3.Dot(hitDirection, transform.forward);
            bool parallel = Mathf.Abs(alongForward) > config.rammedParallelThreshold;
            // Đâm dọc: chúi đầu theo hướng bị đẩy (quay quanh trục ngang). Đâm ngang: nghiêng về phía bị đẩy (quay quanh trục dọc).
            Vector3 localAxis = parallel ? Vector3.right : Vector3.forward;
            float sign = parallel ? Mathf.Sign(alongForward) : -Mathf.Sign(Vector3.Dot(hitDirection, transform.right));
            Quaternion tilted = restRotation * Quaternion.AngleAxis(config.rammedTiltDegrees * sign, localAxis);

            tiltTween = transform.DORotateQuaternion(tilted, config.rammedTiltDuration * 0.5f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo)
                .SetLink(gameObject)
                .SetTarget(transform)
                .OnKill(RestoreRestRotation);
        }

        /// Hết ô đỗ: rung ngang tại chỗ.
        public void PlayRejectShake(GameplayConfig config)
        {
            if (shakeTween != null && shakeTween.IsActive()) return;
            Vector3 punch = transform.right * config.noSlotShakeDistance;
            shakeTween = transform.DOPunchPosition(punch, config.noSlotShakeDuration, 10, 1f)
                .SetLink(gameObject)
                .SetTarget(transform);
        }

        /// Chạy qua lần lượt các điểm với tốc độ đều, đầu xe xoay mượt theo hướng chạy.
        /// <paramref name="endRotation"/> (nếu có) được đặt đúng lúc tới nơi để xe đỗ thẳng ô.
        /// <paramref name="reverseCount"/> điểm đầu tiên được chạy LÙI: đầu xe quay ngược hướng chạy (rời ô đỗ).
        /// <paramref name="onMilestone"/> được gọi một lần khi xe tới điểm thứ <paramref name="milestoneIndex"/> (vd. tới làn trên).
        public void Drive(List<Vector3> points, float speed, float turnSharpness, Quaternion? endRotation,
            Action<CarView> onFinished, int reverseCount = 0, int milestoneIndex = -1, Action<CarView> onMilestone = null)
        {
            KillRam();
            KillTilt();
            drivePath.Clear();
            drivePath.AddRange(points);
            drivePathIndex = 0;
            driveReverseCount = reverseCount;
            driveMilestoneIndex = milestoneIndex;
            onDriveMilestone = onMilestone;
            driveSpeed = speed;
            driveTurnSharpness = turnSharpness;
            hasArriveRotation = endRotation.HasValue;
            arriveRotation = endRotation ?? Quaternion.identity;
            onDriveFinished = onFinished;
            isDriving = drivePath.Count > 0;
            if (!isDriving) FinishDrive();
        }

        private void Update()
        {
            if (!isDriving) return;

            float deltaTime = Time.deltaTime;
            float remaining = driveSpeed * deltaTime;
            Vector3 position = transform.position;
            // Dồn phần quãng đường dư sang điểm kế tiếp để tốc độ đều, không khựng ở mỗi góc.
            while (remaining > 0f && drivePathIndex < drivePath.Count)
            {
                Vector3 toTarget = drivePath[drivePathIndex] - position;
                float distance = toTarget.magnitude;
                if (distance <= remaining)
                {
                    position = drivePath[drivePathIndex];
                    remaining -= distance;
                    if (drivePathIndex == driveMilestoneIndex) FireMilestone();
                    drivePathIndex++;
                }
                else
                {
                    position += toTarget / distance * remaining;
                    remaining = 0f;
                }
            }

            if (drivePathIndex < drivePath.Count)
            {
                Vector3 look = drivePath[drivePathIndex] - position;
                if (drivePathIndex < driveReverseCount) look = -look;
                look.y = 0f;
                if (look.sqrMagnitude > 1e-4f)
                {
                    float t = 1f - Mathf.Exp(-driveTurnSharpness * deltaTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), t);
                }
            }
            transform.position = position;

            if (drivePathIndex >= drivePath.Count) FinishDrive();
        }

        private void FireMilestone()
        {
            Action<CarView> callback = onDriveMilestone;
            onDriveMilestone = null;
            callback?.Invoke(this);
        }

        private void FinishDrive()
        {
            isDriving = false;
            if (hasArriveRotation) transform.rotation = arriveRotation;
            restRotation = transform.rotation;
            Action<CarView> callback = onDriveFinished;
            onDriveFinished = null;
            callback?.Invoke(this);
        }

        public void StopAll()
        {
            isDriving = false;
            onDriveFinished = null;
            onDriveMilestone = null;
            transform.DOKill();
        }

        private void KillRam()
        {
            if (ramSequence != null && ramSequence.IsActive()) ramSequence.Kill();
            ramSequence = null;
        }

        private void KillTilt()
        {
            if (tiltTween != null && tiltTween.IsActive()) tiltTween.Kill();
            tiltTween = null;
        }

        private void RestoreRestRotation()
        {
            if (!isDriving) transform.rotation = restRotation;
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
