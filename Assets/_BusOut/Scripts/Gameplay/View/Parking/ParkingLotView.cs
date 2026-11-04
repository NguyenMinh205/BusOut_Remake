using BusOut.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace BusOut.Gameplay
{
    /// Hình học bãi đỗ trong scene: gốc toạ độ level, làn xe chạy quanh bãi, 2 điểm rời bến. (Ô đỗ: BusStopView.)
    /// Đặt GameObject này sao cho toạ độ level gốc khớp với bãi (scene hiện tại: (0, 0, -5) — Ground ở z = 0; không xoay, scale 1).
    public class ParkingLotView : MonoBehaviour
    {
        [Header("Bãi đỗ (toạ độ local) — Gizmo trắng")]
        [Tooltip("Góc dưới-trái bãi đỗ (x, z). Xe trong level phải nằm trong khung này.")]
        [SerializeField] private Vector2 lotMin = new Vector2(-10.5f, -12.5f);
        [Tooltip("Góc trên-phải bãi đỗ (x, z).")]
        [SerializeField] private Vector2 lotMax = new Vector2(10.5f, 9f);

        [Header("Làn xe chạy, tính từ mép bãi ra ngoài (đơn vị) — Gizmo vàng")]
        [Tooltip("Làn trái và phải cách mép bãi bao nhiêu. Làn bám sát mép bãi (không ra đường Road) vì camera không nhìn hết hai bên đường.")]
        [SerializeField] private float sideLaneMargin = 2.2f;
        [Tooltip("Làn dưới cách mép dưới bãi bao nhiêu.")]
        [SerializeField] private float bottomLaneMargin = 1.5f;
        [Tooltip("Làn trên cách mép trên bãi bao nhiêu. Xe chạy qua trước dãy ô đỗ và rời bến theo làn này.")]
        [SerializeField] private float topLaneMargin = 2f;

        [Header("Tham chiếu trong scene")]
        [Tooltip("Điểm rời bến bên trái (LeavingPointLeft): xe ở nửa trái bến chạy tới đây rồi biến mất.")]
        [SerializeField] private Transform leftLeavingPoint;
        [Tooltip("Điểm rời bến bên phải (LeavingPointRight): xe ở nửa phải bến chạy tới đây rồi biến mất.")]
        [FormerlySerializedAs("leavingPoint")]
        [SerializeField] private Transform rightLeavingPoint;
        [Tooltip("Cha của các xe spawn ra. Bỏ trống = chính GameObject này.")]
        [SerializeField] private Transform carsRoot;

        // Căn bố cục level vào khung bãi (Init): toạ độ dữ liệu → toạ độ local của bãi.
        private float layoutScale = 1f;
        private Vector2 dataCenter;
        private Vector2 lotCenter;

        /// Bố cục level co bấy nhiêu lần để vừa khung bãi (1 = khung bãi bằng khung dữ liệu). Xe phải co theo đúng số này.
        public float LayoutScale => layoutScale;
        public Transform CarsRoot => carsRoot != null ? carsRoot : transform;
        public Vector3 LeftLeavingPoint => leftLeavingPoint.position;
        public Vector3 RightLeavingPoint => rightLeavingPoint.position;

        public LaneRing Ring
        {
            get
            {
                Vector3 origin = transform.position;
                return new LaneRing
                {
                    LeftX = origin.x + lotMin.x - sideLaneMargin,
                    RightX = origin.x + lotMax.x + sideLaneMargin,
                    BottomZ = origin.z + lotMin.y - bottomLaneMargin,
                    TopZ = origin.z + lotMax.y + topLaneMargin,
                };
            }
        }

        /// <paramref name="dataMin"/> / <paramref name="dataMax"/>: khung chứa xe của dữ liệu level (GameplayConfig).
        public bool Init(Vector2 dataMin, Vector2 dataMax)
        {
            if (leftLeavingPoint == null || rightLeavingPoint == null)
            {
                Debug.LogError("[BusOut] ParkingLotView thiếu tham chiếu: kéo LeavingPointLeft vào Left Leaving Point " +
                               "và LeavingPointRight vào Right Leaving Point.", this);
                return false;
            }
            // Làn đường chỉ cộng vị trí, không tính xoay/scale: xoay GameObject này là xe chạy lệch khỏi đường.
            if (transform.rotation != Quaternion.identity || transform.lossyScale != Vector3.one)
                Debug.LogError("[BusOut] ParkingLotView phải để rotation (0,0,0) và scale (1,1,1), không thì xe chạy lệch khỏi đường.", this);
            Vector2 dataSize = dataMax - dataMin;
            Vector2 lotSize = lotMax - lotMin;
            if (dataSize.x <= 0f || dataSize.y <= 0f || lotSize.x <= 0f || lotSize.y <= 0f)
            {
                Debug.LogError($"[BusOut] Khung bãi ({lotMin}…{lotMax}) hoặc khung dữ liệu level ({dataMin}…{dataMax}) không hợp lệ " +
                               "(góc trên-phải phải lớn hơn góc dưới-trái): xe đặt 1:1 như dữ liệu. Sửa Lot Min / Lot Max của " +
                               "ParkingLotView hoặc GameplayConfig › Level Data Min / Max.", this);
                layoutScale = 1f;
                dataCenter = lotCenter = Vector2.zero;
                return true;
            }
            // Co ĐỀU hai chiều (lấy chiều chật hơn): co lệch là méo bố cục — xe vốn xếp sát sẽ chồng lên nhau theo một chiều.
            layoutScale = Mathf.Min(lotSize.x / dataSize.x, lotSize.y / dataSize.y);
            dataCenter = (dataMin + dataMax) * 0.5f;
            lotCenter = (lotMin + lotMax) * 0.5f;
            GameLog.Info($"[BusOut] Bố cục level co {layoutScale:0.###} lần cho vừa khung bãi.");
            return true;
        }

        /// Toạ độ trong file level → vị trí thế giới: căn khung dữ liệu vào giữa khung bãi, co theo LayoutScale.
        public Vector3 LevelToWorld(float x, float z)
        {
            Vector2 local = lotCenter + (new Vector2(x, z) - dataCenter) * layoutScale;
            return transform.position + new Vector3(local.x, 0f, local.y);
        }

        private void OnDrawGizmos()
        {
            Vector3 origin = transform.position;
            Gizmos.color = Color.white;
            DrawRect(origin, lotMin.x, lotMax.x, lotMin.y, lotMax.y);
            Gizmos.color = Color.yellow;
            LaneRing ring = Ring;
            DrawRect(new Vector3(0f, origin.y, 0f), ring.LeftX, ring.RightX, ring.BottomZ, ring.TopZ);
        }

        private static void DrawRect(Vector3 origin, float minX, float maxX, float minZ, float maxZ)
        {
            const float Height = 0.05f;
            Vector3 a = origin + new Vector3(minX, Height, minZ);
            Vector3 b = origin + new Vector3(maxX, Height, minZ);
            Vector3 c = origin + new Vector3(maxX, Height, maxZ);
            Vector3 d = origin + new Vector3(minX, Height, maxZ);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
    }
}
