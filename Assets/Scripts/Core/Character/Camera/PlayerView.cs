using UnityEngine;

namespace DWHITE 
{		/// <summary>
	/// 基于重力变换矩阵的第一人称视角控制器
	/// 职责：
	/// 1. 维护重力变换矩阵，实时同步重力变化
	/// 2. 通过变换矩阵处理所有方向向量，确保一致性
	/// 3. 实现头部独立转动和身体跟随逻辑
	/// 4. 基于变换后的向量提供移动方向
	/// </summary>
	public class PlayerView : MonoBehaviour
	{
	    [Header("核心引用")]
	    [SerializeField] private PlayerInput _playerInput;
	    [SerializeField] private PlayerMotor _motor;
	    [SerializeField] private Transform _aimTarget; // 拖入 AimOrientation/target 物体
	    [SerializeField] private Transform _playerBody; // 拖入 Player 根物体	    [Header("视角控制")]
	    [SerializeField] private float _lookSensitivity = 1.0f;
	    [SerializeField] [Range(0f, 90f)] private float _maxPitchUp = 88f; // 向上看的最大角度
	    [SerializeField] [Range(0f, 90f)] private float _maxPitchDown = 88f; // 向下看的最大角度
	    [SerializeField] private bool _invertY = false;

		[Header("头部/身体 旋转逻辑")]
	    [Tooltip("头部相对于身体可以独立旋转的最大角度")]
	    [SerializeField] private float _headYawLimit = 60f;
	    [Tooltip("当头部转动超过限制时，身体跟上旋转的速度")]
	    [SerializeField] private float _bodyRotationSpeed = 8f;
	    [Tooltip("身体向头部对齐的速度（当角度差较小时）")]
	    [SerializeField] private float _bodyAlignmentSpeed = 0.3f;
	    [Tooltip("开始身体对齐的角度差阈值（相对于头部转动限制的比例）")]
	    [SerializeField] [Range(0.1f, 0.9f)] private float _alignmentThreshold = 0.5f;
	    
	    [Header("AimTarget 设置")]
	    [Tooltip("AimTarget 距离玩家的距离")]
	    [SerializeField] private float _aimDistance = 10f;
	    [SerializeField] private Vector3 _headOffset = new Vector3(0, 1.7f, 0);

	    [Header("调试")]
	    [SerializeField] private bool _showDebugInfo = false;
	    [SerializeField] private bool _showDebugGizmos = false;
		// --- 重力变换系统 ---
		private Vector3 _referenceGravityUp = Vector3.up; // 参考重力方向（世界坐标系的"上"）
		private Quaternion _gravityTransform = Quaternion.identity; // 从参考重力到当前重力的变换
		private Quaternion _inverseGravityTransform = Quaternion.identity; // 从当前重力到参考重力的逆变换
		
		// --- 基于参考坐标系的内部状态（在参考重力系统下的"意图"） ---
		private Vector3 _referenceAimDirection = Vector3.forward; // 参考坐标系下的瞄准方向
		private Vector3 _referenceBodyDirection = Vector3.forward; // 参考坐标系下的身体朝向
		private float _currentPitch = 0f; // 俯仰角度（辅助计算）

	    // --- 公共属性 ---
	    /// <summary>
	    /// 水平前进方向 - 基于视角朝向的移动方向
	    /// </summary>
	    public Vector3 HorizontalForwardDirection { get; private set; }
	    
	    /// <summary>
	    /// 水平右侧方向 - 基于视角朝向的移动方向
	    /// </summary>
	    public Vector3 HorizontalRightDirection { get; private set; }	    /// <summary>
	    /// 当前瞄准方向（世界坐标系）
	    /// </summary>
	    public Vector3 CurrentAimDirection => _gravityTransform * _referenceAimDirection;
	    
	    /// <summary>
	    /// 当前身体朝向（世界坐标系）
	    /// </summary>
	    public Vector3 CurrentBodyDirection => _gravityTransform * _referenceBodyDirection;

	    private void Awake()
	    {
	        // 确保引用已设置
	        if (_playerInput == null) _playerInput = GetComponent<PlayerInput>();
	        if (_motor == null) _motor = GetComponent<PlayerMotor>();
	    }	    private void Start()
	    {
	        _playerInput.SetCursorLock(true);

	        // 初始化重力变换系统
	        InitializeGravityTransform();
	        
	        // 初始化向量状态
	        InitializeDirections();
	    }	    private void LateUpdate()
	    {
	        // 0. 更新重力变换矩阵
	        UpdateGravityTransform();

	        // 1. 处理输入并更新瞄准方向
	        HandleInputWithVectors();

			// 2. 更新AimTarget位置
			UpdateAimTargetPosition();

			// 3. 实现头部/身体分离逻辑
			UpdateBodyRotationWithVectors();

			// 4. 更新移动方向
			UpdateHorizontalDirections();
			
			if (_showDebugInfo)
			{
				UpdateDebugInfo();
			}
		}

		/// <summary>
		/// 初始化重力变换系统
		/// </summary>
		private void InitializeGravityTransform()
		{
			if (_motor == null) return;
			
			// 计算从参考重力到当前重力的变换
			Vector3 currentGravityUp = _motor.UpAxis;
			_gravityTransform = Quaternion.FromToRotation(_referenceGravityUp, currentGravityUp);
			_inverseGravityTransform = Quaternion.Inverse(_gravityTransform);
			
			if (_showDebugInfo)
			{
				Debug.Log($"[GravityTransform] Initialized - Reference: {_referenceGravityUp:F2}, " +
						  $"Current: {currentGravityUp:F2}, Transform: {_gravityTransform.eulerAngles:F1}°");
			}
		}
		
		/// <summary>
		/// 更新重力变换矩阵（每帧调用）
		/// </summary>
		private void UpdateGravityTransform()
		{
			if (_motor == null) return;
			
			Vector3 currentGravityUp = _motor.UpAxis;
			Quaternion newGravityTransform = Quaternion.FromToRotation(_referenceGravityUp, currentGravityUp);
			
			// 检查变换是否发生显著变化
			float transformDifference = Quaternion.Angle(_gravityTransform, newGravityTransform);
			
			if (transformDifference > 0.1f) // 变换角度超过阈值
			{
				if (_showDebugInfo)
				{
					Debug.Log($"[GravityTransform] Updated - Angle difference: {transformDifference:F2}°, " +
							  $"New transform: {newGravityTransform.eulerAngles:F1}°");
				}
				
				_gravityTransform = newGravityTransform;
				_inverseGravityTransform = Quaternion.Inverse(_gravityTransform);
			}
		}

		/// <summary>
		/// 初始化方向向量（在参考坐标系中）
		/// </summary>
		private void InitializeDirections()
		{
			if (_motor == null) return;

			// 获取当前身体朝向并转换到参考坐标系
			Vector3 worldBodyForward = _playerBody.forward;
			_referenceBodyDirection = (_inverseGravityTransform * worldBodyForward).normalized;
			
			// 如果转换结果无效，使用默认值
			if (_referenceBodyDirection.magnitude < 0.001f)
			{
				_referenceBodyDirection = Vector3.forward;
			}

			// 初始化瞄准方向与身体朝向一致
			_referenceAimDirection = _referenceBodyDirection;
			_currentPitch = CalculateCurrentPitch(CurrentAimDirection);

			// 初始化移动方向
			UpdateHorizontalDirections();
			
			if (_showDebugInfo)
			{
				Debug.Log($"[PlayerView] Initialized - Reference Body: {_referenceBodyDirection:F2}, " +
						  $"Reference Aim: {_referenceAimDirection:F2}, World Aim: {CurrentAimDirection:F2}");
			}
		}
				/// <summary>
	    /// 基于向量运算处理输入
	    /// </summary>
	    private void HandleInputWithVectors()
	    {
	        if (_playerInput == null) return;
	        Vector2 lookInput = _playerInput.LookInput * _lookSensitivity * Time.deltaTime;

	        // 处理Yaw（绕参考重力Up轴旋转）
	        if (Mathf.Abs(lookInput.x) > 0.001f)
	        {
	            Vector3 refUp = _referenceGravityUp;
	            _referenceAimDirection = RotateVectorAroundAxis(_referenceAimDirection, refUp, lookInput.x);
	        }
	        // 处理Pitch（绕参考右轴旋转）
	        if (Mathf.Abs(lookInput.y) > 0.001f)
	        {
	            float pitchInput = _invertY ? lookInput.y : -lookInput.y;
	            Vector3 refRight = Vector3.Cross(_referenceGravityUp, _referenceAimDirection).normalized;
	            Vector3 potentialNewDir = RotateVectorAroundAxis(_referenceAimDirection, refRight, pitchInput);
	            if (IsWithinPitchLimits(_gravityTransform * potentialNewDir))
	            {
	                _referenceAimDirection = potentialNewDir;
	                _currentPitch = CalculateCurrentPitch(_gravityTransform * _referenceAimDirection);
	            }
	        }
	        _referenceAimDirection = _referenceAimDirection.normalized;
	    }

	    /// <summary>
	    /// 更新AimTarget位置
	    /// </summary>
	    private void UpdateAimTargetPosition()
	    {
	        if (_aimTarget == null) return;
	        
	        // 计算头部位置
	        Vector3 headPosition = _playerBody.position + _motor.GetBodyRotation() * _headOffset;
	        
	        // 设置AimTarget位置
	        _aimTarget.position = headPosition + CurrentAimDirection * _aimDistance;
	    }    /// <summary>
    /// 基于向量的身体跟随逻辑 - 让身体最终与头部朝向一致
    /// </summary>
    private void UpdateBodyRotationWithVectors()
    {
        if (_motor == null) return;
        
        Vector3 currentGravityUp = _motor.UpAxis;
        Vector3 aimDirWorld = CurrentAimDirection;
        Vector3 aimHorizontal = Vector3.ProjectOnPlane(aimDirWorld, currentGravityUp).normalized;
        Vector3 bodyDirWorld = CurrentBodyDirection;
        Vector3 bodyHorizontal = Vector3.ProjectOnPlane(bodyDirWorld, currentGravityUp).normalized;
        if (aimHorizontal.magnitude < 0.001f) return;
        if (bodyHorizontal.magnitude < 0.001f) bodyHorizontal = aimHorizontal;
        float angleDiff = Vector3.SignedAngle(bodyHorizontal, aimHorizontal, currentGravityUp);
        if (Mathf.Abs(angleDiff) > _headYawLimit)
        {
            float targetBodyRotationAngle = angleDiff - Mathf.Sign(angleDiff) * _headYawLimit;
            Vector3 newBodyDirWorld = RotateVectorAroundAxis(bodyHorizontal, currentGravityUp, targetBodyRotationAngle * _bodyRotationSpeed * Time.deltaTime);
            // 回写到参考坐标系
            _referenceBodyDirection = (_inverseGravityTransform * newBodyDirWorld).normalized;
            _motor.SetTargetYawDirection(newBodyDirWorld);
        }
        else if (Mathf.Abs(angleDiff) < _headYawLimit * _alignmentThreshold)
        {
            float alignSpeed = _bodyRotationSpeed * _bodyAlignmentSpeed * Time.deltaTime;
            Vector3 newBodyDirWorld = Vector3.Slerp(bodyHorizontal, aimHorizontal, alignSpeed).normalized;
            _referenceBodyDirection = (_inverseGravityTransform * newBodyDirWorld).normalized;
            _motor.SetTargetYawDirection(newBodyDirWorld);
        }
    }

	    /// <summary>
	    /// 基于向量运算更新水平移动方向
	    /// </summary>
	    private void UpdateHorizontalDirections()
	    {
	        if (_aimTarget == null)
	        {
	            // 后备方案：使用当前瞄准方向
	            HorizontalForwardDirection = Vector3.ProjectOnPlane(CurrentAimDirection, _motor.UpAxis).normalized;
	            HorizontalRightDirection = Vector3.Cross(_motor.UpAxis, HorizontalForwardDirection).normalized;
	        }
	        else
	        {
	            // 使用AimTarget的方向作为视角方向
	            Vector3 aimForward = (_aimTarget.position - _playerBody.position).normalized;
	            Vector3 aimRight = Vector3.Cross(_motor.UpAxis, aimForward).normalized;

	            HorizontalForwardDirection = Vector3.ProjectOnPlane(aimForward, _motor.UpAxis).normalized;
	            HorizontalRightDirection = Vector3.ProjectOnPlane(aimRight, _motor.UpAxis).normalized;
	        }
	    }

	    /// <summary>
	    /// 使用Rodrigues旋转公式旋转向量
	    /// </summary>
	    private Vector3 RotateVectorAroundAxis(Vector3 vector, Vector3 axis, float angle)
	    {
	        float radians = angle * Mathf.Deg2Rad;
	        float cosAngle = Mathf.Cos(radians);
	        float sinAngle = Mathf.Sin(radians);
	        
	        // Rodrigues旋转公式: v' = v*cos(θ) + (k×v)*sin(θ) + k*(k·v)*(1-cos(θ))
	        Vector3 k = axis.normalized;
	        Vector3 vCrossK = Vector3.Cross(k, vector);
	        float vDotK = Vector3.Dot(vector, k);
	        
	        return vector * cosAngle + vCrossK * sinAngle + k * vDotK * (1 - cosAngle);
	    }

	    /// <summary>
	    /// 设置灵敏度
	    /// </summary>
	    public void SetSensitivity(float sensitivity)
	    {
	        _lookSensitivity = Mathf.Max(0.1f, sensitivity);
	    }
	    
	    /// <summary>
	    /// 设置Y轴反转
	    /// </summary>
	    public void SetInvertY(bool invert)
	    {
	        _invertY = invert;
	    }
	 	    /// <summary>
	    /// 重置视角
	    /// </summary>
	    public void ResetView()
	    {
	        _referenceAimDirection = _referenceBodyDirection;
	        _currentPitch = CalculateCurrentPitch(CurrentAimDirection);
	    }
	    
	    /// <summary>
	    /// 添加视角偏移（用于后坐力等效果）
	    /// </summary>
	    public void AddViewKick(Vector2 kickAmount)
	    {
	        if (Mathf.Abs(kickAmount.x) > 0.001f)
	        {
	            Vector3 refUp = _referenceGravityUp;
	            _referenceAimDirection = RotateVectorAroundAxis(_referenceAimDirection, refUp, kickAmount.x);
	        }
	        if (Mathf.Abs(kickAmount.y) > 0.001f)
	        {
	            Vector3 refRight = Vector3.Cross(_referenceGravityUp, _referenceAimDirection).normalized;
	            Vector3 potentialNewDir = RotateVectorAroundAxis(_referenceAimDirection, refRight, -kickAmount.y);
	            if (IsWithinPitchLimits(_gravityTransform * potentialNewDir))
	            {
	                _referenceAimDirection = potentialNewDir.normalized;
	                _currentPitch = CalculateCurrentPitch(CurrentAimDirection);
	            }
	        }
	    }
	    
	    /// <summary>
	    /// 获取世界坐标中的视角射线
	    /// </summary>
	    public Ray GetViewRay()
	    {
	        Vector3 headPosition = _playerBody.position + _motor.GetBodyRotation() * _headOffset;
	        return new Ray(headPosition, CurrentAimDirection);
	    }

		/// <summary>
		/// 检查目标是否在视野内
		/// </summary>
		public bool IsInView(Vector3 worldPosition, float maxAngle = 60f)
		{
			Vector3 headPosition = _playerBody.position + _motor.GetBodyRotation() * _headOffset;
			Vector3 toTarget = (worldPosition - headPosition).normalized;
			float angle = Vector3.Angle(CurrentAimDirection, toTarget);
			return angle <= maxAngle;
		}
		
		/// <summary>
		/// 强制重新适配重力方向（用于外部系统触发重力变化）
		/// </summary>
		public void ForceGravityUpdate()
		{
			if (_motor != null)
			{
				// 强制重力更新检查
			}
		}
		
		/// <summary>
		/// 获取当前的俯仰角限制信息（用于UI显示等）
		/// </summary>
		public Vector2 GetCurrentPitchLimits()
		{
			return new Vector2(-_maxPitchDown, _maxPitchUp);
		}
				/// <summary>
		/// 获取当前俯仰角相对于限制的百分比 (-1到1，0为水平)
		/// </summary>
		public float GetPitchNormalized()
		{
			if (_currentPitch >= 0)
			{
				return _maxPitchUp > 0 ? _currentPitch / _maxPitchUp : 0f;
			}
			else
			{
				return _maxPitchDown > 0 ? _currentPitch / _maxPitchDown : 0f;
			}
		}
		
		/// <summary>
		/// 强制身体立即对齐到头部方向（用于特殊情况，如重生、传送等）
		/// </summary>
		public void ForceBodyAlignment()
		{
			if (_motor == null) return;
			
			Vector3 currentGravityUp = _motor.UpAxis;
			Vector3 aimDirectionHorizontal = Vector3.ProjectOnPlane(CurrentAimDirection, currentGravityUp).normalized;
			
			if (aimDirectionHorizontal.magnitude > 0.001f)
			{
				_referenceBodyDirection = (_inverseGravityTransform * aimDirectionHorizontal).normalized;
				_motor.SetTargetYawDirection(aimDirectionHorizontal);
				
				if (_showDebugInfo)
				{
					Debug.Log($"[ForceBodyAlignment] Body aligned to head direction: {aimDirectionHorizontal:F2}");
				}
			}
		}
		
		/// <summary>
		/// 获取当前头身角度差（度）
		/// </summary>
		public float GetHeadBodyAngleDifference()
		{
			if (_motor == null) return 0f;
			
			Vector3 currentGravityUp = _motor.UpAxis;
			Vector3 aimDirectionHorizontal = Vector3.ProjectOnPlane(CurrentAimDirection, currentGravityUp).normalized;
			Vector3 bodyDirectionHorizontal = Vector3.ProjectOnPlane(CurrentBodyDirection, currentGravityUp).normalized;
			
			if (aimDirectionHorizontal.magnitude < 0.001f || bodyDirectionHorizontal.magnitude < 0.001f)
				return 0f;
				
			return Vector3.SignedAngle(bodyDirectionHorizontal, aimDirectionHorizontal, currentGravityUp);
		}
		/// <summary>
		/// 更新调试信息
		/// </summary>
		private void UpdateDebugInfo()
		{
			if (Time.frameCount % 30 == 0) // 每30帧更新一次
			{
				Vector3 currentGravityUp = _motor != null ? _motor.UpAxis : Vector3.up;
				float headBodyAngleDiff = GetHeadBodyAngleDifference();
				
				string info = $"[PlayerView-GravityAdaptive] Pitch: {_currentPitch:F1}°";
				info += $"\nGravity Up: {currentGravityUp:F2}";
				info += $"\nAim Direction: {CurrentAimDirection:F2}";
				info += $"\nBody Forward: {CurrentBodyDirection:F2}";
				info += $"\nHead-Body Angle: {headBodyAngleDiff:F1}°";
				info += $"\nHorizontal Forward: {HorizontalForwardDirection:F2}";
				info += $"\nHorizontal Right: {HorizontalRightDirection:F2}";
				info += $"\nPitch Limits: [-{_maxPitchDown}°, +{_maxPitchUp}°]";
				Debug.Log(info);
			}
		}
			
	    private void OnDrawGizmosSelected()
	    {
	        if (!_showDebugGizmos) return;

	        Vector3 headPos = _playerBody != null ? _playerBody.position + _headOffset : transform.position;

	        // 绘制瞄准方向
	        Gizmos.color = Color.red;
	        Gizmos.DrawLine(headPos, headPos + CurrentAimDirection * 3f);

	        // 绘制身体前向
	        Gizmos.color = Color.blue;
	        Gizmos.DrawLine(transform.position, transform.position + CurrentBodyDirection * 2f);

	        // 绘制AIM目标位置
	        if (_aimTarget != null)
	        {
	            Gizmos.color = Color.yellow;
	            Gizmos.DrawWireSphere(_aimTarget.position, 0.2f);
	            
	            // 绘制从头部到AIM目标的连线
	            Gizmos.color = Color.green;
	            Gizmos.DrawLine(headPos, _aimTarget.position);
	        }

	        // 绘制水平移动方向
	        Gizmos.color = Color.white;
	        Gizmos.DrawLine(transform.position, transform.position + HorizontalForwardDirection * 2f);

	        Gizmos.color = Color.gray;
	        Gizmos.DrawLine(transform.position, transform.position + HorizontalRightDirection * 2f);
	    }	    private void OnValidate()
	    {
	        _lookSensitivity = Mathf.Max(0.1f, _lookSensitivity);
	        _bodyRotationSpeed = Mathf.Max(0.1f, _bodyRotationSpeed);
	        _bodyAlignmentSpeed = Mathf.Clamp(_bodyAlignmentSpeed, 0.05f, 2f);
	        _alignmentThreshold = Mathf.Clamp(_alignmentThreshold, 0.1f, 0.9f);
	        _aimDistance = Mathf.Max(1f, _aimDistance);
	        _headYawLimit = Mathf.Clamp(_headYawLimit, 0f, 180f);
	        _maxPitchUp = Mathf.Clamp(_maxPitchUp, 0f, 90f);
	        _maxPitchDown = Mathf.Clamp(_maxPitchDown, 0f, 90f);
	    }

		/// <summary>
		/// 检查给定的方向是否在允许的俯仰范围内（基于重力方向）
		/// 这个方法是重力适配的核心，能在任意重力方向下正确限制视角
		/// </summary>
		private bool IsWithinPitchLimits(Vector3 direction)
		{
			if (_motor == null) return true; // 安全性检查

			// 获取当前重力方向的"上"轴
			Vector3 gravityUpAxis = _motor.UpAxis;

			// 将方向向量投影到重力水平面上
			Vector3 directionOnPlane = Vector3.ProjectOnPlane(direction, gravityUpAxis).normalized;

			// 如果方向完全垂直于重力平面（即完全向上或向下），需要特殊处理
			if (directionOnPlane.magnitude < 0.001f)
			{
				// 检查是否朝向重力上方或下方
				float upDot = Vector3.Dot(direction, gravityUpAxis);
				if (upDot > 0.999f) // 几乎完全向上
					return _maxPitchUp >= 89.9f;
				else if (upDot < -0.999f) // 几乎完全向下
					return _maxPitchDown >= 89.9f;
				else
					return false; // 不应该发生的情况
			}

			// 计算俯仰角（相对于重力水平面）
			// 使用右手法则：右轴 = 重力上轴 × 水平方向
			Vector3 rightAxis = Vector3.Cross(gravityUpAxis, directionOnPlane).normalized;
			float pitchAngle = Vector3.SignedAngle(directionOnPlane, direction, rightAxis);

			// 检查是否在允许范围内
			// 向上看：正角度，向下看：负角度
			bool withinLimits = pitchAngle >= -_maxPitchDown && pitchAngle <= _maxPitchUp;

			if (_showDebugInfo && Time.frameCount % 60 == 0) // 每60帧输出一次调试信息
			{
				Debug.Log($"[PitchLimit] Direction: {direction:F2}, OnPlane: {directionOnPlane:F2}, " +
						  $"Pitch: {pitchAngle:F1}°, Range: [-{_maxPitchDown}°, +{_maxPitchUp}°], " +
						  $"Within: {withinLimits}, GravityUp: {gravityUpAxis:F2}");
			}

			return withinLimits;
		}

		/// <summary>
		/// 计算当前瞄准方向的俯仰角（用于调试显示）
		/// 与IsWithinPitchLimits使用相同的计算方法确保一致性
		/// </summary>
		private float CalculateCurrentPitch(Vector3 direction)
		{
			if (_motor == null) return 0f;
			
			Vector3 gravityUpAxis = _motor.UpAxis;
			Vector3 directionOnPlane = Vector3.ProjectOnPlane(direction, gravityUpAxis).normalized;
			
			// 处理完全垂直的情况
			if (directionOnPlane.magnitude < 0.001f)
			{
				float upDot = Vector3.Dot(direction, gravityUpAxis);
				return upDot > 0 ? 90f : -90f;
			}
			
			// 计算俯仰角
			Vector3 rightAxis = Vector3.Cross(gravityUpAxis, directionOnPlane).normalized;
			return Vector3.SignedAngle(directionOnPlane, direction, rightAxis);
		}
	}
}
