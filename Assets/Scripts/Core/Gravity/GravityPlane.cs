using UnityEngine;

/// <summary>
/// 平面重力体，提供恒定方向的重力
/// </summary>
public class GravityPlane : GravitySource
{
    [Header("重力设置")]
    [SerializeField] private float _gravity = 9.8f;
    [SerializeField] private float _range = 10f;
    
    [Header("调试")]
    [SerializeField] private Color _gizmoColor = Color.green;

    public float Gravity
    {
        get => _gravity;
        set => _gravity = value;
    }

    public float Range
    {
        get => _range;
        set => _range = Mathf.Max(0f, value);
    }

    public override Vector3 GetGravity(Vector3 position)
    {
        Vector3 up = transform.up;
        Vector3 toPosition = position - transform.position;
        
        // 计算到平面的距离
        float distance = Vector3.Dot(toPosition, up);
        
        // 如果超出范围，不受影响
        if (Mathf.Abs(distance) > _range)
            return Vector3.zero;

        // 返回恒定方向的重力
        return -up * _gravity;
    }

    public override Vector3 GetUpAxis(Vector3 position)
    {
        return transform.up;
    }

    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = _gizmoColor;
        
        // 绘制平面范围
        Vector3 up = transform.up;
        Vector3 right = transform.right;
        Vector3 forward = transform.forward;
        
        float size = 5f;
        Vector3[] corners = new Vector3[4];
        corners[0] = transform.position + (right - forward) * size;
        corners[1] = transform.position + (right + forward) * size;
        corners[2] = transform.position + (-right + forward) * size;
        corners[3] = transform.position + (-right - forward) * size;
        
        // 绘制平面
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
        }
        
        // 绘制范围指示
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + up * _range);
        Gizmos.DrawLine(transform.position, transform.position - up * _range);
        
        // 绘制重力方向
        Gizmos.color = Color.red;
        for (int i = 0; i < 4; i++)
        {
            Vector3 gravity = GetGravity(corners[i]);
            if (gravity != Vector3.zero)
            {
                Gizmos.DrawLine(corners[i], corners[i] + gravity.normalized * 2f);
            }
        }
    }

    private void OnValidate()
    {
        _range = Mathf.Max(0f, _range);
        _gravity = Mathf.Max(0f, _gravity);
    }
}
