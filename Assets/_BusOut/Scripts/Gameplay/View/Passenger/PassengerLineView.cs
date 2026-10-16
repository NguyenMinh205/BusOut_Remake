using UnityEngine;

namespace BusOut.Gameplay
{
    /// Các điểm của hàng khách trong scene. Gắn trên GAMEOBJECT/ObjectPoint/PassengerPoint.
    public class PassengerLineView : MonoBehaviour
    {
        [Tooltip("Khách mới xuất hiện ở đây rồi đi vào hàng (SpawnPoint).")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("Các điểm của đường xếp hàng theo thứ tự ĐẦU hàng → CUỐI hàng: ThirdTargetPoint, SecondTargetPoint, FirstTargetPoint.")]
        [SerializeField] private Transform[] queuePoints;

        [Tooltip("Khách rời hàng đi qua điểm này rồi mới tới xe (MoveToBusPoint).")]
        [SerializeField] private Transform moveToBusPoint;

        [Tooltip("Cha của các khách sinh ra. Bỏ trống = chính GameObject này.")]
        [SerializeField] private Transform passengersRoot;

        private Vector3[] queuePositions;

        public Vector3 SpawnPoint => spawnPoint.position;
        public Vector3 MoveToBusPoint => moveToBusPoint.position;
        public Transform PassengersRoot => passengersRoot != null ? passengersRoot : transform;
        public Vector3[] QueuePositions => queuePositions;

        public bool Init()
        {
            bool pointsOk = queuePoints != null && queuePoints.Length >= 2;
            if (pointsOk)
                foreach (Transform point in queuePoints)
                    if (point == null) pointsOk = false;
            if (spawnPoint == null || moveToBusPoint == null || !pointsOk)
            {
                Debug.LogError("[BusOut] PassengerLineView thiếu tham chiếu: kéo SpawnPoint, MoveToBusPoint và ít nhất 2 điểm hàng " +
                               "(ThirdTargetPoint, SecondTargetPoint, FirstTargetPoint — theo thứ tự đầu → cuối hàng).", this);
                return false;
            }
            queuePositions = new Vector3[queuePoints.Length];
            for (int i = 0; i < queuePoints.Length; i++) queuePositions[i] = queuePoints[i].position;
            return true;
        }

        private void OnDrawGizmos()
        {
            if (queuePoints == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < queuePoints.Length - 1; i++)
                if (queuePoints[i] != null && queuePoints[i + 1] != null)
                    Gizmos.DrawLine(queuePoints[i].position, queuePoints[i + 1].position);
            if (queuePoints.Length > 0 && queuePoints[0] != null && moveToBusPoint != null)
                Gizmos.DrawLine(queuePoints[0].position, moveToBusPoint.position);
        }
    }
}
