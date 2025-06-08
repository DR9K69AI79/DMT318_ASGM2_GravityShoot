using UnityEngine;

namespace DWHITE
{
    /// <summary>
    /// 盒形重力体，基于CatLikeCoding的参考实现
    /// 支持内外距离控制和衰减因子的复杂重力计算
    /// </summary>
    public class GravityBox : GravitySource
    {
        [Header("基础重力设置")]
        [SerializeField] private float gravity = 9.81f;

        [Header("盒体边界")]
        [SerializeField] private Vector3 boundaryDistance = Vector3.one;

        [Header("内部距离设置")]
        [SerializeField, Min(0f)] private float innerDistance = 0f;
        [SerializeField, Min(0f)] private float innerFalloffDistance = 0f;

        [Header("外部距离设置")]
        [SerializeField, Min(0f)] private float outerDistance = 0f;
        [SerializeField, Min(0f)] private float outerFalloffDistance = 0f;

        // 计算得出的衰减因子
        private float innerFalloffFactor, outerFalloffFactor;

        /// <summary>
        /// 重力强度属性
        /// </summary>
        public float Gravity
        {
            get => gravity;
            set => gravity = value;
        }

        /// <summary>
        /// 边界距离属性
        /// </summary>
        public Vector3 BoundaryDistance
        {
            get => boundaryDistance;
            set => boundaryDistance = Vector3.Max(value, Vector3.zero);
        }

        public override Vector3 GetGravity(Vector3 position)
        {
            // 将位置转换到本地空间
            position = transform.InverseTransformDirection(position - transform.position);

            Vector3 vector = Vector3.zero;
            int outside = 0;

            // 检查X轴边界
            if (position.x > boundaryDistance.x)
            {
                vector.x = boundaryDistance.x - position.x;
                outside = 1;
            }
            else if (position.x < -boundaryDistance.x)
            {
                vector.x = -boundaryDistance.x - position.x;
                outside = 1;
            }

            // 检查Y轴边界
            if (position.y > boundaryDistance.y)
            {
                vector.y = boundaryDistance.y - position.y;
                outside += 1;
            }
            else if (position.y < -boundaryDistance.y)
            {
                vector.y = -boundaryDistance.y - position.y;
                outside += 1;
            }

            // 检查Z轴边界
            if (position.z > boundaryDistance.z)
            {
                vector.z = boundaryDistance.z - position.z;
                outside += 1;
            }
            else if (position.z < -boundaryDistance.z)
            {
                vector.z = -boundaryDistance.z - position.z;
                outside += 1;
            }

            // 如果在边界外部
            if (outside > 0)
            {
                float distance = outside == 1 ?
                    Mathf.Abs(vector.x + vector.y + vector.z) : vector.magnitude;
                
                if (distance > outerFalloffDistance)
                {
                    return Vector3.zero;
                }
                
                float g = gravity / distance;
                if (distance > outerDistance)
                {
                    g *= 1f - (distance - outerDistance) * outerFalloffFactor;
                }
                
                return transform.TransformDirection(g * vector);
            }

            // 在边界内部 - 计算到最近面的距离
            Vector3 distances;
            distances.x = boundaryDistance.x - Mathf.Abs(position.x);
            distances.y = boundaryDistance.y - Mathf.Abs(position.y);
            distances.z = boundaryDistance.z - Mathf.Abs(position.z);

            // 找到最近的面并计算重力分量
            if (distances.x < distances.y)
            {
                if (distances.x < distances.z)
                {
                    vector.x = GetGravityComponent(position.x, distances.x);
                }
                else
                {
                    vector.z = GetGravityComponent(position.z, distances.z);
                }
            }
            else if (distances.y < distances.z)
            {
                vector.y = GetGravityComponent(position.y, distances.y);
            }
            else
            {
                vector.z = GetGravityComponent(position.z, distances.z);
            }

            return transform.TransformDirection(vector);
        }

        /// <summary>
        /// 计算单个轴向的重力分量
        /// </summary>
        private float GetGravityComponent(float coordinate, float distance)
        {
            if (distance > innerFalloffDistance)
            {
                return 0f;
            }
            
            float g = gravity;
            if (distance > innerDistance)
            {
                g *= 1f - (distance - innerDistance) * innerFalloffFactor;
            }
            
            return coordinate > 0f ? -g : g;
        }

        private void Awake()
        {
            OnValidate();
        }

        private void OnValidate()
        {
            boundaryDistance = Vector3.Max(boundaryDistance, Vector3.zero);
            
            float maxInner = Mathf.Min(
                Mathf.Min(boundaryDistance.x, boundaryDistance.y), boundaryDistance.z
            );
            
            innerDistance = Mathf.Min(innerDistance, maxInner);
            innerFalloffDistance = Mathf.Max(Mathf.Min(innerFalloffDistance, maxInner), innerDistance);
            outerFalloffDistance = Mathf.Max(outerFalloffDistance, outerDistance);

            // 计算衰减因子，避免除零
            innerFalloffFactor = innerFalloffDistance > innerDistance ? 
                1f / (innerFalloffDistance - innerDistance) : 0f;
            outerFalloffFactor = outerFalloffDistance > outerDistance ? 
                1f / (outerFalloffDistance - outerDistance) : 0f;
        }

        protected override void OnDrawGizmosSelected()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            
            Vector3 size;
            
            // 绘制内部衰减区域
            if (innerFalloffDistance > innerDistance)
            {
                Gizmos.color = Color.cyan;
                size.x = 2f * (boundaryDistance.x - innerFalloffDistance);
                size.y = 2f * (boundaryDistance.y - innerFalloffDistance);
                size.z = 2f * (boundaryDistance.z - innerFalloffDistance);
                Gizmos.DrawWireCube(Vector3.zero, size);
            }
            
            // 绘制内部距离区域
            if (innerDistance > 0f)
            {
                Gizmos.color = Color.yellow;
                size.x = 2f * (boundaryDistance.x - innerDistance);
                size.y = 2f * (boundaryDistance.y - innerDistance);
                size.z = 2f * (boundaryDistance.z - innerDistance);
                Gizmos.DrawWireCube(Vector3.zero, size);
            }
            
            // 绘制主边界
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(Vector3.zero, 2f * boundaryDistance);
            
            // 绘制外部距离区域
            if (outerDistance > 0f)
            {
                Gizmos.color = Color.yellow;
                DrawGizmosOuterCube(outerDistance);
            }
            
            // 绘制外部衰减区域
            if (outerFalloffDistance > outerDistance)
            {
                Gizmos.color = Color.cyan;
                DrawGizmosOuterCube(outerFalloffDistance);
            }
        }

        /// <summary>
        /// 绘制外部立方体的Gizmos
        /// </summary>
        private void DrawGizmosOuterCube(float distance)
        {
            Vector3 a, b, c, d;
            
            // 绘制X轴正面和负面
            a.y = b.y = boundaryDistance.y;
            d.y = c.y = -boundaryDistance.y;
            b.z = c.z = boundaryDistance.z;
            d.z = a.z = -boundaryDistance.z;
            a.x = b.x = c.x = d.x = boundaryDistance.x + distance;
            DrawGizmosRect(a, b, c, d);
            a.x = b.x = c.x = d.x = -a.x;
            DrawGizmosRect(a, b, c, d);

            // 绘制Y轴正面和负面
            a.x = d.x = boundaryDistance.x;
            b.x = c.x = -boundaryDistance.x;
            a.z = b.z = boundaryDistance.z;
            c.z = d.z = -boundaryDistance.z;
            a.y = b.y = c.y = d.y = boundaryDistance.y + distance;
            DrawGizmosRect(a, b, c, d);
            a.y = b.y = c.y = d.y = -a.y;
            DrawGizmosRect(a, b, c, d);

            // 绘制Z轴正面和负面
            a.x = d.x = boundaryDistance.x;
            b.x = c.x = -boundaryDistance.x;
            a.y = b.y = boundaryDistance.y;
            c.y = d.y = -boundaryDistance.y;
            a.z = b.z = c.z = d.z = boundaryDistance.z + distance;
            DrawGizmosRect(a, b, c, d);
            a.z = b.z = c.z = d.z = -a.z;
            DrawGizmosRect(a, b, c, d);
            
            // 绘制外围立方体轮廓
            distance *= 0.5773502692f; // 1/sqrt(3)的近似值
            Vector3 size = boundaryDistance;
            size.x = 2f * (size.x + distance);
            size.y = 2f * (size.y + distance);
            size.z = 2f * (size.z + distance);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }

        /// <summary>
        /// 绘制矩形的Gizmos辅助方法
        /// </summary>
        private void DrawGizmosRect(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
    }
}
