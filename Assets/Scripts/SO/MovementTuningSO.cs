using UnityEngine;

namespace DWHITE {	
	/// <summary>
	/// 移动调参配置 - 数据驱动的"手感"设置
	/// 存放所有移动相关的曲线和常量，让设计师可以在Inspector中直接调整
	/// </summary>
	[CreateAssetMenu(fileName = "MovementTuning", menuName = "GravityShoot/Movement Tuning", order = 1)]
	public class MovementTuningSO : ScriptableObject
	{
	    [Header("地面移动")]
	    [Tooltip("地面加速度曲线 (X: 当前速度/最大速度比例 0-1, Y: 加速度 m/s²)")]
	    public AnimationCurve groundAcceleration = AnimationCurve.EaseInOut(0f, 30f, 1f, 5f);
	    
	    [Tooltip("地面最大移动速度 (m/s)")]
	    public float maxGroundSpeed = 8f;
	    
	    [Header("空中移动")]
	    [Tooltip("空中加速度曲线 (X: 当前速度/最大速度比例 0-1, Y: 加速度 m/s²)")]
	    public AnimationCurve airAcceleration = AnimationCurve.EaseInOut(0f, 15f, 1f, 2f);
	    
	    [Tooltip("空中最大移动速度 (m/s)")]
	    public float maxAirSpeed = 6f;
	    
	    [Header("跳跃设置")]
	    [Tooltip("跳跃初始速度 (m/s)")]
	    public float jumpSpeed = 8f;
	    
	    [Tooltip("土狼时间 - 离开地面后仍可跳跃的帧数")]
	    [Range(0, 10)]
	    public int coyoteFrames = 3;
	    
	    [Tooltip("跳跃缓冲 - 提前按下跳跃键的缓冲帧数")]
	    [Range(0, 10)]
	    public int jumpBufferFrames = 5;
	    
	    [Header("地面贴合")]
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
	    
	    private void OnValidate()
	    {
	        // 确保所有数值都在合理范围内
	        maxGroundSpeed = Mathf.Max(0.1f, maxGroundSpeed);
	        maxAirSpeed = Mathf.Max(0.1f, maxAirSpeed);
	        jumpSpeed = Mathf.Max(0.1f, jumpSpeed);
	        snapProbeDistance = Mathf.Max(0.01f, snapProbeDistance);
	        maxSnapSpeed = Mathf.Max(0.1f, maxSnapSpeed);
	        maxStepHeight = Mathf.Max(0f, maxStepHeight);
	        stepProbeDistance = Mathf.Max(0.01f, stepProbeDistance);
	        gravityMultiplier = Mathf.Max(0.1f, gravityMultiplier);
	        maxFallSpeed = Mathf.Max(1f, maxFallSpeed);
	        turnResponsiveness = Mathf.Max(0.1f, turnResponsiveness);
	        brakeMultiplier = Mathf.Max(0.1f, brakeMultiplier);
	    }
	}
}
