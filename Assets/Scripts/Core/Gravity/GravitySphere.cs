using UnityEngine;

/// <summary>
/// 球形重力体，模拟行星引力
/// </summary>
public class GravitySphere : GravitySource
{
    [Header("重力设置")]
    [SerializeField] private float _gravity = 9.8f;
    [SerializeField] private float _radius = 10f;
    [SerializeField] private bool _useInverseSquare = true;
    
    [Header("调试")]
    [SerializeField] private Color _gizmoColor = Color.yellow;

    public float Gravity
    {
        get => _gravity;
        set => _gravity = value;
    }

    public float Radius
    {
        get => _radius;
        set => _radius = Mathf.Max(0f, value);
    }

    public override Vector3 GetGravity(Vector3 position)
    {
        Vector3 vector = transform.position - position;
        float distance = vector.magnitude;
        
        // 如果在重力半径之外，不受影响
        if (distance > _radius || distance == 0f)
            return Vector3.zero;

        float gravityMagnitude = _gravity;
        
        // 应用平方反比定律（如果启用）
        if (_useInverseSquare)
        {
            float normalizedDistance = distance / _radius;
            gravityMagnitude *= 1f / (normalizedDistance * normalizedDistance);
        }

        return vector.normalized * gravityMagnitude;
    }

    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = _gizmoColor;
        Gizmos.DrawWireSphere(transform.position, _radius);
        
        // 绘制重力方向指示
        Gizmos.color = Color.red;
        Vector3[] directions = {
            Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back
        };
        
        foreach (Vector3 dir in directions)
        {
            Vector3 point = transform.position + dir * _radius * 0.8f;
            Vector3 gravity = GetGravity(point);
            if (gravity != Vector3.zero)
            {
                Gizmos.DrawLine(point, point + gravity.normalized * 2f);
            }
        }
    }

    private void OnValidate()
    {
        _radius = Mathf.Max(0f, _radius);
        _gravity = Mathf.Max(0f, _gravity);
    }
}
