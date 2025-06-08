using UnityEngine;

namespace DWHITE {	
	/// <summary>
	/// 基于重力的第一人称相机控制器
	/// 在重力环境变化时保持稳定的视角控制
	/// 偏航角控制角色身体旋转，俯仰角控制相机相对角色的倾斜
	/// </summary>
	public class FPSGravityCamera : MonoBehaviour
	{
	    [Header("相机设置")]
	    [SerializeField] private Transform _cameraRoot;
	    [SerializeField] private Camera _playerCamera;
	    [SerializeField] private Vector3 _cameraOffset = new Vector3(0, 1.7f, 0);
	    
	    [Header("视角控制")]
	    [SerializeField] private float _lookSensitivity = 2f;
	    [SerializeField] private float _verticalLookLimit = 360f;
	    [SerializeField] private bool _invertY = false;
	    
	    [Header("高级设置")]
	    [SerializeField] private bool _syncCharacterRotation = true;
	    [SerializeField] private float _characterSyncSpeed = 10f;
	    
	    [Header("调试")]
	    [SerializeField] private bool _showDebugInfo = false;
	    [SerializeField] private bool _showDebugGizmos = false;
	    
	    // 组件引用
	    private PlayerInput _playerInput;
	    private RBPlayerMotor _motor;
	    
	    // 旋转状态
	    private float _yaw = 0f;
	    private float _pitch = 0f;
	    
	    // 属性
	    public Camera PlayerCamera => _playerCamera;
	    public Transform CameraRoot => _cameraRoot;
	    public float Yaw => _yaw;
	    public float Pitch => _pitch;
	    public Vector3 CameraForward => _cameraRoot.forward;
	    public Vector3 CameraRight => _cameraRoot.right;
	    
	    private void Awake()
	    {
	        _playerInput = GetComponent<PlayerInput>();
	        _motor = GetComponent<RBPlayerMotor>();
	        
	        // 设置相机根节点
	        SetupCameraRoot();
	        
	        // 查找或创建相机
	        SetupCamera();
	    }
	    
	    private void Start()
	    {
	        // 初始化旋转状态
	        InitializeRotation();
	        
	        // 锁定光标
	        _playerInput.SetCursorLock(true);
	    }
	    
	    private void LateUpdate()
	    {
	        UpdateInput();
	        ApplyCameraTransform();
	        
	        if (_syncCharacterRotation)
	        {
	            SyncCharacterRotation();
	        }
	        
	        if (_showDebugInfo)
	        {
	            UpdateDebugInfo();
	        }
	    }
	    
	    /// <summary>
	    /// 设置相机根节点
	    /// </summary>
	    private void SetupCameraRoot()
	    {
	        if (_cameraRoot == null)
	        {
	            GameObject rootGO = new GameObject("CameraRoot");
	            rootGO.transform.SetParent(transform, false);
	            rootGO.transform.localPosition = _cameraOffset;
	            _cameraRoot = rootGO.transform;
	        }
	        else
	        {
	            // 确保相机根节点位置正确
	            _cameraRoot.localPosition = _cameraOffset;
	        }
	    }
	    
	    /// <summary>
	    /// 设置相机
	    /// </summary>
	    private void SetupCamera()
	    {
	        if (_playerCamera == null)
	        {
	            _playerCamera = _cameraRoot.GetComponentInChildren<Camera>();
	            
	            if (_playerCamera == null)
	            {
	                GameObject cameraGO = new GameObject("PlayerCamera");
	                cameraGO.transform.SetParent(_cameraRoot, false);
	                cameraGO.transform.localPosition = Vector3.zero;
	                _playerCamera = cameraGO.AddComponent<Camera>();
	                
	                // 设置为主相机
	                _playerCamera.tag = "MainCamera";
	            }
	        }
	    }
	    
	    /// <summary>
	    /// 获取在当前重力平面上的参考"正前方"方向
	    /// </summary>
	    private Vector3 GetReferenceForwardOnPlane()
	    {
	        if (_motor == null || _motor.UpAxis == Vector3.zero)
	        {
	            Debug.LogWarning("[FPSGravityCamera] Motor not ready or UpAxis is zero in GetReferenceForwardOnPlane. Returning world forward.");
	            return Vector3.forward; 
	        }
	
	        Vector3 referenceForward = Vector3.ProjectOnPlane(Vector3.forward, _motor.UpAxis).normalized;
	        if (referenceForward == Vector3.zero)
	        {
	            // 如果世界正前方平行于 UpAxis，则尝试使用世界右方向
	            referenceForward = Vector3.ProjectOnPlane(Vector3.right, _motor.UpAxis).normalized;
	            if (referenceForward == Vector3.zero)
	            {
	                // 极少数情况：通过旋转构造一个正交向量
	                Quaternion toGravityPlane = Quaternion.FromToRotation(Vector3.up, _motor.UpAxis);
	                referenceForward = (toGravityPlane * Vector3.forward).normalized;
	                if (referenceForward == Vector3.zero) referenceForward = Vector3.one.normalized;
	            }
	        }
	        return referenceForward;
	    }
	    
	    /// <summary>
	    /// 初始化旋转状态
	    /// </summary>
	    private void InitializeRotation()
	    {
	        if (_motor == null) 
	        {
	            Debug.LogError("[FPSGravityCamera] RBPlayerMotor not found during InitializeRotation!");
	            _yaw = 0f;
	            _pitch = 0f;
	            return;
	        }
	
	        Vector3 referenceForward = GetReferenceForwardOnPlane();
	        Vector3 characterForwardOnPlane = Vector3.ProjectOnPlane(transform.forward, _motor.UpAxis).normalized;
	
	        if (characterForwardOnPlane == Vector3.zero) 
	        {
	            // 如果角色初始朝向平行于其UpAxis，则认为其在平面上的朝向与参考方向一致
	            characterForwardOnPlane = referenceForward; 
	        }
	        
	        if (referenceForward != Vector3.zero && characterForwardOnPlane != Vector3.zero)
	        {
	            _yaw = Vector3.SignedAngle(referenceForward, characterForwardOnPlane, _motor.UpAxis);
	        }
	        else
	        {
	            _yaw = 0f;
	        }
	        _pitch = 0f;
	    }
	
	    /// <summary>
	    /// 更新输入
	    /// </summary>
	    private void UpdateInput()
	    {
	        if (_playerInput == null) return;
	        
	        Vector2 lookInput = _playerInput.LookInput;
	        
	        // 应用灵敏度
	        lookInput *= _lookSensitivity;
	        
	        // lookInput.x 更新角色身体的目标偏航角 _yaw
	        _yaw += lookInput.x;
	        
	        // 更新pitch（垂直旋转）
	        float pitchDelta = _invertY ? -lookInput.y : lookInput.y;
	        _pitch += pitchDelta;
	        _pitch = Mathf.Clamp(_pitch, -_verticalLookLimit, _verticalLookLimit);
	    }
	    
	    /// <summary>
	    /// 应用相机变换
	    /// </summary>
	    private void ApplyCameraTransform()
	    {
	        // 相机的旋转 = 角色当前的实际身体旋转 * 相机的局部俯仰旋转
	        // transform.rotation 是角色身体的当前实际朝向（已由SyncCharacterRotation处理重力和目标yaw）
	        _cameraRoot.rotation = transform.rotation * Quaternion.AngleAxis(_pitch, Vector3.right);
	        
	        // 相机位置的计算保持不变
	        _cameraRoot.position = transform.position + (transform.rotation * _cameraOffset);
	    }
	    
	    /// <summary>
	    /// 同步角色旋转
	    /// </summary>
	    private void SyncCharacterRotation()
	    {
	        // 如果不启用角色旋转同步，或者 motor 未初始化，则不执行
	        if (!_syncCharacterRotation || _motor == null) return;
	
	        Vector3 referenceForwardOnPlane = GetReferenceForwardOnPlane();
	        if (referenceForwardOnPlane == Vector3.zero)
	        {
	            Debug.LogWarning("[FPSGravityCamera] Could not get a valid reference forward for SyncCharacterRotation.");
	            return; 
	        }
	
	        // 角色身体的目标"正前方"方向，通过将参考方向按 _yaw 角度围绕 _motor.UpAxis 旋转得到
	        Vector3 targetCharacterForward = Quaternion.AngleAxis(_yaw, _motor.UpAxis) * referenceForwardOnPlane;
	        
	        // 确保目标前方向量有效
	        if (targetCharacterForward.sqrMagnitude > 0.001f) 
	        {
	            Quaternion targetRotation = Quaternion.LookRotation(targetCharacterForward, _motor.UpAxis);
	            transform.rotation = Quaternion.Slerp(
	                transform.rotation, 
	                targetRotation, 
	                _characterSyncSpeed * Time.deltaTime
	            );
	        }
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
	    /// 设置垂直视角限制
	    /// </summary>
	    public void SetVerticalLimit(float limit)
	    {
	        _verticalLookLimit = Mathf.Clamp(limit, 0f, 90f);
	    }
	    
	    /// <summary>
	    /// 重置视角
	    /// </summary>
	    public void ResetView()
	    {
	        _pitch = 0f;
	        // _yaw 现在代表相对于参考方向的角度，重置时保持当前角色朝向
	        if (_motor != null)
	        {
	            Vector3 referenceForward = GetReferenceForwardOnPlane();
	            Vector3 characterForwardOnPlane = Vector3.ProjectOnPlane(transform.forward, _motor.UpAxis).normalized;
	            if (referenceForward != Vector3.zero && characterForwardOnPlane != Vector3.zero)
	            {
	                _yaw = Vector3.SignedAngle(referenceForward, characterForwardOnPlane, _motor.UpAxis);
	            }
	        }
	    }
	    
	    /// <summary>
	    /// 添加视角偏移（用于后坐力等效果）
	    /// </summary>
	    public void AddViewKick(Vector2 kickAmount)
	    {
	        _yaw += kickAmount.x;
	        _pitch = Mathf.Clamp(_pitch + kickAmount.y, -_verticalLookLimit, _verticalLookLimit);
	    }
	    
	    /// <summary>
	    /// 获取世界坐标中的视角射线
	    /// </summary>
	    public Ray GetViewRay()
	    {
	        return new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);
	    }
	    
	    /// <summary>
	    /// 检查目标是否在视野内
	    /// </summary>
	    public bool IsInView(Vector3 worldPosition, float maxAngle = 60f)
	    {
	        Vector3 toTarget = (worldPosition - _playerCamera.transform.position).normalized;
	        float angle = Vector3.Angle(_playerCamera.transform.forward, toTarget);
	        return angle <= maxAngle;
	    }
	    
	    /// <summary>
	    /// 更新调试信息
	    /// </summary>
	    private void UpdateDebugInfo()
	    {
	        if (Time.frameCount % 30 == 0) // 每30帧更新一次
	        {
	            string info = $"[FPSGravityCamera] Yaw: {_yaw:F1}°, Pitch: {_pitch:F1}°";
	            info += $"\\nPlayer Up Axis (_motor.UpAxis): {_motor.UpAxis:F2}";
	            info += $"\\nPlayer Body Rotation: {transform.rotation.eulerAngles:F1}";
	            info += $"\\nReference Forward: {GetReferenceForwardOnPlane():F2}";
	            Debug.Log(info);
	        }
	    }
	    
	    private void OnDrawGizmosSelected()
	    {
	        if (!_showDebugGizmos) return;
	        
	        // 绘制相机根位置
	        if (_cameraRoot != null)
	        {
	            Gizmos.color = Color.yellow;
	            Gizmos.DrawWireSphere(_cameraRoot.position, 0.1f);
	            
	            // 绘制相机朝向
	            Gizmos.color = Color.red;
	            Gizmos.DrawLine(_cameraRoot.position, _cameraRoot.position + _cameraRoot.forward * 2f);
	            
	            Gizmos.color = Color.green;
	            Gizmos.DrawLine(_cameraRoot.position, _cameraRoot.position + _cameraRoot.up * 2f);
	            
	            Gizmos.color = Color.blue;
	            Gizmos.DrawLine(_cameraRoot.position, _cameraRoot.position + _cameraRoot.right * 2f);
	        }
	        
	        // 绘制玩家身体的向上轴以供比较
	        if (transform != null)
	        {
	            Gizmos.color = Color.magenta;
	            Gizmos.DrawLine(transform.position, transform.position + transform.up * 1.5f);
	            
	            // 绘制玩家身体的前方向
	            Gizmos.color = Color.cyan;
	            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
	        }
	    }
	    
	    private void OnValidate()
	    {
	        _lookSensitivity = Mathf.Max(0.1f, _lookSensitivity);
	        _verticalLookLimit = Mathf.Clamp(_verticalLookLimit, 0f, 360f);
	        _characterSyncSpeed = Mathf.Max(0.1f, _characterSyncSpeed);
	    }
	}
}
