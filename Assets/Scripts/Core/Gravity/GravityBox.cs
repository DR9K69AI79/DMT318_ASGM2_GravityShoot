using UnityEngine;

namespace DWHITE
{

    /// <summary>
    /// 盒形重力体，在指定盒形的周围提供与面法线方向一致的重力
    /// </summary>
    public class GravityBox : GravitySource
    {
        [Header("重力设置")]
        [SerializeField] private float _gravityValue = -9.81f;
        [SerializeField] private Vector3 _size = Vector3.one * 10f;

        [Header("调试")]
        [SerializeField] private Color _gizmoColor = Color.blue;

        private Vector3 _gravity;

        public Vector3 Gravity
        {
            get => _gravity;
            set => _gravity = value;
        }

        public Vector3 Size
        {
            get => _size;
            set => _size = Vector3.Max(Vector3.zero, value);
        }

        public override Vector3 GetGravity(Vector3 position)
        {
            // 将位置转换到本地空间
            Vector3 localPosition = transform.InverseTransformPoint(position);

            // 检查是否在盒体内
            Vector3 halfSize = _size * 0.5f;
            if (Mathf.Abs(localPosition.x) <= halfSize.x &&
                Mathf.Abs(localPosition.y) <= halfSize.y &&
                Mathf.Abs(localPosition.z) <= halfSize.z)
            {
                // 将重力方向转换到世界空间
                return transform.TransformDirection(_gravity);
            }

            return Vector3.zero;
        }

        public override Vector3 GetUpAxis(Vector3 position)
        {
            Vector3 gravity = GetGravity(position);
            if (gravity == Vector3.zero)
                return Vector3.up;

            return -gravity.normalized;
        }

        protected override void OnDrawGizmosSelected()
        {
            Gizmos.color = _gizmoColor;

            // 绘制盒体轮廓
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, _size);

            // 绘制重力方向
            if (_gravity != Vector3.zero)
            {
                Gizmos.color = Color.red;
                Vector3 center = Vector3.zero;
                Vector3 gravityDir = _gravity.normalized;
                float arrowLength = Mathf.Min(_size.x, _size.y, _size.z) * 0.3f;

                Gizmos.DrawLine(center, center + gravityDir * arrowLength);

                // 绘制箭头头部
                Vector3 arrowHead1 = center + gravityDir * arrowLength + Vector3.Cross(gravityDir, Vector3.up).normalized * 0.5f;
                Vector3 arrowHead2 = center + gravityDir * arrowLength + Vector3.Cross(gravityDir, Vector3.right).normalized * 0.5f;
                Gizmos.DrawLine(center + gravityDir * arrowLength, arrowHead1);
                Gizmos.DrawLine(center + gravityDir * arrowLength, arrowHead2);
            }

            Gizmos.matrix = oldMatrix;
        }

        private void OnValidate()
        {
            _size = Vector3.Max(Vector3.zero, _size);
        }
    }
}
