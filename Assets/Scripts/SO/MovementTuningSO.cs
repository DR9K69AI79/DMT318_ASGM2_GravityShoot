using UnityEngine;

namespace DWHITE {	
	/// <summary>
	/// 奔跑模式枚举
	/// </summary>
	public enum SprintMode
	{
		/// <summary>按住奔跑键才能奔跑</summary>
		Hold,
		/// <summary>切换奔跑状态</summary>
		Toggle
	}
	
	/// <summary>
	/// 移动调参配置 - 数据驱动的"手感"设置
	/// 存放所有移动相关的曲线和常量，让设计师可以在Inspector中直接调整
	/// </summary>
	[CreateAssetMenu(fileName = "MovementTuning", menuName = "GravityShoot/Movement Tuning", order = 1)]
	public class MovementTuningSO : ScriptableObject
	{    [Header("地面移动")]
    [Tooltip("地面加速度曲线 (X: 当前速度/最大速度比例 0-1, Y: 加速度 m/s²)")]
    public AnimationCurve groundAcceleration = AnimationCurve.EaseInOut(0f, 30f, 1f, 5f);
    
    [Tooltip("地面最大加速度 (m/s²)")]
    public float maxGroundAcceleration = 30f;
    
    [Tooltip("地面最大移动速度 (m/s)")]
    public float maxGroundSpeed = 8f;
    
    [Header("奔跑")]
    [Tooltip("奔跑速度倍率 - 相对于正常移动速度")]
    [Range(1.2f, 3f)]
    public float sprintSpeedMultiplier = 1.8f;
    
    [Tooltip("奔跑加速度倍率 - 相对于正常加速度")]
    [Range(1f, 2f)]
    public float sprintAccelerationMultiplier = 1.2f;
    
    [Tooltip("奔跑模式 - Toggle切换 或 Hold按住")]
    public SprintMode sprintMode = SprintMode.Hold;
    
    [Tooltip("奔跑转换速度 - 从正常速度切换到奔跑速度的平滑度")]
    [Range(1f, 10f)]
    public float sprintTransitionSpeed = 5f;
	    
	    [Header("空中移动")]
	    [Tooltip("空中加速度曲线 (X: 当前速度/最大速度比例 0-1, Y: 加速度 m/s²)")]
	    public AnimationCurve airAcceleration = AnimationCurve.EaseInOut(0f, 15f, 1f, 2f);
	    
	    [Tooltip("空中最大加速度 (m/s²)")]
	    public float maxAirAcceleration = 15f;
	    
	    [Tooltip("空中最大移动速度 (m/s)")]
	    public float maxAirSpeed = 6f;
	    
	    [Header("跳跃设置")]
	    [Tooltip("跳跃高度 (m) - 用于计算跳跃速度")]
	    public float jumpHeight = 2f;
	    
	    [Tooltip("跳跃初始速度 (m/s)")]
	    public float jumpSpeed = 8f;
	    
	    [Tooltip("土狼时间 - 离开地面后仍可跳跃的帧数")]
	    [Range(0, 10)]
	    public int coyoteFrames = 3;
	    
	    [Tooltip("跳跃缓冲 - 提前按下跳跃键的缓冲帧数")]
	    [Range(0, 10)]
	    public int jumpBufferFrames = 5;
	    
	    [Header("地面贴合")]
	    [Tooltip("最小地面角度点积 - 用于判断表面是否可行走")]
	    [Range(0f, 1f)]
	    public float minGroundDotProduct = 0.9f;
	    
	    [Tooltip("贴地射线检测距离 (m)")]
	    public float snapProbeDistance = 1f;
	    
	    [Tooltip("贴地最大下落速度 - 超过此速度时不进行贴地 (m/s)")]
	    public float maxSnapSpeed = 10f;
	    
	    [Tooltip("可行走的最大坡度角度 (°)")]
	    [Range(0f, 90f)]
	    public float maxGroundAngle = 45f;
	    
	    [Header("阶梯处理")]
	    [Tooltip("最大可自动跨越的阶梯高度 (m)")]
	    public float maxStepHeight = 0.3f;
	    
	    [Tooltip("阶梯检测距离 (m)")]
	    public float stepProbeDistance = 0.5f;
	    
	    [Header("重力增强")]
	    [Tooltip("重力倍率 - 可用于调整下落感觉")]
	    public float gravityMultiplier = 1f;
	    
	    [Tooltip("最大下落速度 (m/s) - 防止过快下落")]
	    public float maxFallSpeed = 50f;
	      [Header("响应性")]
    [Tooltip("转向响应速度 - 改变移动方向的灵敏度")]
    public float turnResponsiveness = 8f;
    
    [Tooltip("刹车效果 - 停止移动时的减速倍率")]
    public float brakeMultiplier = 2f;
    
    [Header("姿态对齐 (Pose Alignment)")]
    [Tooltip("在地面上时，角色身体对齐地面的速度。")]
    public float groundedTurnSpeed = 15f;

    [Tooltip("在空中时，角色身体对齐重力方向的速度。")]
    public float airborneTurnSpeed = 10f;
	      private void OnValidate()
    {
        // 确保所有数值都在合理范围内
        maxGroundAcceleration = Mathf.Max(0.1f, maxGroundAcceleration);
        maxAirAcceleration = Mathf.Max(0.1f, maxAirAcceleration);
        maxGroundSpeed = Mathf.Max(0.1f, maxGroundSpeed);
        maxAirSpeed = Mathf.Max(0.1f, maxAirSpeed);
        sprintSpeedMultiplier = Mathf.Clamp(sprintSpeedMultiplier, 1.2f, 3f);
        sprintAccelerationMultiplier = Mathf.Clamp(sprintAccelerationMultiplier, 1f, 2f);
        sprintTransitionSpeed = Mathf.Clamp(sprintTransitionSpeed, 1f, 10f);
        jumpHeight = Mathf.Max(0.1f, jumpHeight);
        jumpSpeed = Mathf.Max(0.1f, jumpSpeed);
        minGroundDotProduct = Mathf.Clamp01(minGroundDotProduct);
        snapProbeDistance = Mathf.Max(0.01f, snapProbeDistance);
        maxSnapSpeed = Mathf.Max(0.1f, maxSnapSpeed);
        maxStepHeight = Mathf.Max(0f, maxStepHeight);
        stepProbeDistance = Mathf.Max(0.01f, stepProbeDistance);
        gravityMultiplier = Mathf.Max(0.1f, gravityMultiplier);
        maxFallSpeed = Mathf.Max(1f, maxFallSpeed);
        turnResponsiveness = Mathf.Max(0.1f, turnResponsiveness);
        brakeMultiplier = Mathf.Max(0.1f, brakeMultiplier);
        groundedTurnSpeed = Mathf.Max(0.1f, groundedTurnSpeed);
        airborneTurnSpeed = Mathf.Max(0.1f, airborneTurnSpeed);
    }
	}
}
