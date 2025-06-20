using UnityEngine;

namespace DWHITE
{
    /// <summary>
    /// 基于重力变换矩阵的第一人称视角控制器。
    /// 职责：
    /// 1. 维护重力变换矩阵，实时同步重力变化，并在变换时保持视觉连续性。
    /// 2. 通过变换矩阵处理所有方向向量，确保在任意重力下的逻辑一致性。
    /// 3. 实现头部独立转动和身体平滑跟随的逻辑。
    /// 4. 基于变换后的向量提供准确的移动方向。
    /// </summary>
   
    public class PlayerView : MonoBehaviour
    {
        #region 依赖与配置 (Dependencies & Configuration)

        [Header("核心引用")]
        [SerializeField] private PlayerInput _playerInput;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private Transform _aimTarget;
        [SerializeField] private Transform _playerBody;

        [Header("视角控制")]
        [SerializeField] private float _cameraFov = 90f;
        [SerializeField] private float _lookSensitivity = 1.0f;
        [SerializeField] private float _maxPitchUp = 88f;
        [SerializeField] private float _maxPitchDown = 88f;
        [SerializeField] private bool _invertY = false;

        [Header("头部/身体 旋转逻辑")]
        [SerializeField] private float _headYawLimit = 60f;
        [SerializeField] private float _bodyRotationSpeed = 8f;
        [SerializeField] private float _bodyAlignmentSpeed = 0.3f;
        [SerializeField] private float _alignmentThreshold = 0.5f;

        [Header("瞄准目标")]
        [SerializeField] private float _aimDistance = 10f;
        [SerializeField] private Vector3 _camOffset = new Vector3(0, 0.4f, 0);
        [SerializeField] private Vector3 _headOffset = new Vector3(0, 1.4f, 0);

        [Header("重力响应")]
        [Tooltip("当重力方向改变时，是否维护头部俯仰与upAxis的相对角度")]
        [SerializeField] private bool _maintainPitchToUpAxis = false;
        [Tooltip("俯仰输入阈值，低于此值时激活角度维护功能")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _pitchInputThreshold = 0.1f;

        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = false;
        [SerializeField] private bool _showDebugGizmos = false;
        [SerializeField] private bool _logGravityChangeVerification = false;
        
        #endregion

        #region 公共属性 (Public Properties)

        /// <summary>
        /// 相对于当前重力平面的前进方向，用于移动。
        /// </summary>
        public Vector3 HorizontalForwardDirection { get; private set; }
        
        /// <summary>
        /// 相对于当前重力平面的右侧方向，用于移动。
        /// </summary>
        public Vector3 HorizontalRightDirection { get; private set; }
        
        /// <summary>
        /// 当前瞄准方向（世界坐标系），已应用重力变换。
        /// </summary>
        public Vector3 CurrentAimDirection => _gravityTransform * _referenceAimDirection;

        /// <summary>
        /// 当前身体朝向（世界坐标系），已应用重力变换。
        /// </summary>
        public Vector3 CurrentBodyDirection => _gravityTransform * _referenceBodyDirection;

        /// <summary>
        /// 当前头部俯仰相对于upAxis的角度（度）。
        /// </summary>
        public float CurrentPitchToUpAxis => _pitchAngleToUpAxis;

        /// <summary>
        /// 是否启用了俯仰角维护功能。
        /// </summary>
        public bool IsPitchMaintenanceEnabled => _maintainPitchToUpAxis;
        
        #endregion

        #region 内部状态 (Internal State)

        // 定义一个微小的阈值，用于浮点数和向量幅度的比较，避免精度问题。
        private const float Epsilon = 1e-3f;

        // 重力变换系统
        private static readonly Vector3 ReferenceGravityUp = Vector3.up; // 绝对参考上方向
        private Quaternion _gravityTransform = Quaternion.identity;      // 从参考重力到当前重力的变换
        private Quaternion _inverseGravityTransform = Quaternion.identity; // 逆变换
        // 基于参考坐标系的内部状态（在标准重力下的"意图"方向）
        private Vector3 _referenceAimDirection = Vector3.forward;
        private Vector3 _referenceBodyDirection = Vector3.forward;
        
        // 当前俯仰角（仅用于计算和调试）
        private float _currentPitch = 0f;        // 重力响应系统 - 新设计
        private Vector3 _lastGravityUp = Vector3.up;
        private float _pitchAngleToUpAxis = 0f;  // 头部俯仰相对于当前upAxis的角度
        private bool _isGravityResponseActive = false; // 标记重力响应是否正在激活

        #endregion
        
        #region Unity 生命周期 (Unity Lifecycle)

        private void Awake()
        {
            // 如果没有在 Inspector 中设置，则自动获取组件
            if (_playerInput == null) _playerInput = GetComponent<PlayerInput>();
            if (_motor == null) _motor = GetComponent<PlayerMotor>();
            if (_playerCamera == null) _playerCamera = GetComponentInChildren<Camera>();
        }

        private void Start()
        {
            _playerInput.SetCursorLock(true);
            InitializeCamera();
            InitializeGravityTransform();
            InitializeDirections();
            InitializeGravityResponse();
        }

        private void LateUpdate()
        {
            UpdateGravityTransform();
            
            HandleInput();
            
            UpdateBodyRotation();
            
            UpdatePitchMaintenance();
            
            UpdateAimTargetPosition();
            
            UpdateHorizontalDirections();
            
            if (_showDebugInfo)
            {
                UpdateDebugInfo();
            }
        }
          private void OnValidate()
        {
            // 在编辑器中修改数值时，确保其在有效范围内
            _lookSensitivity = Mathf.Max(0.1f, _lookSensitivity);
            _bodyRotationSpeed = Mathf.Max(0.1f, _bodyRotationSpeed);
            _bodyAlignmentSpeed = Mathf.Clamp(_bodyAlignmentSpeed, 0.05f, 2f);
            _alignmentThreshold = Mathf.Clamp(_alignmentThreshold, 0.1f, 0.9f);
            _aimDistance = Mathf.Max(1f, _aimDistance);            _headYawLimit = Mathf.Clamp(_headYawLimit, 0f, 180f);
            _maxPitchUp = Mathf.Clamp(_maxPitchUp, 0f, 90f);
            _maxPitchDown = Mathf.Clamp(_maxPitchDown, 0f, 90f);
            
            // 俯仰角维护参数验证
            _pitchInputThreshold = Mathf.Clamp(_pitchInputThreshold, 0.01f, 1f);
        }

        #endregion

        #region 公共 API (Public API)

        /// <summary>
        /// 设置视角灵敏度。
        /// </summary>
        public void SetSensitivity(float sensitivity)
        {
            _lookSensitivity = Mathf.Max(0.1f, sensitivity);
        }
        
        /// <summary>
        /// 设置是否反转Y轴。
        /// </summary>
        public void SetInvertY(bool invert)
        {
            _invertY = invert;
        }
          /// <summary>
        /// 将视角重置为与身体方向一致。
        /// </summary>
        public void ResetView()
        {
            _referenceAimDirection = _referenceBodyDirection;
            _currentPitch = CalculateCurrentPitch(CurrentAimDirection);
            // 重置俯仰角度状态
            UpdatePitchAngleToUpAxis();
        }
        
        /// <summary>
        /// 添加一次性的视角偏移（例如：武器后坐力）。
        /// </summary>
        /// <param name="kickAmount">X为水平偏移，Y为垂直偏移。</param>
        public void AddViewKick(Vector2 kickAmount)
        {
            // 注意：后坐力的垂直偏移通常与鼠标输入相反，因此 pitchDelta 为 -kickAmount.y
            ApplyViewRotation(kickAmount.x, -kickAmount.y);
        }
        
        /// <summary>
        /// 获取从头部发出的视角射线（世界坐标）。
        /// </summary>
        public Ray GetViewRay()
        {
            return new Ray(GetHeadPosition(), CurrentAimDirection);
        }

        /// <summary>
        /// 检查一个世界坐标点是否在视野范围内。
        /// </summary>
        /// <param name="worldPosition">要检查的世界坐标点。</param>
        /// <param name="maxAngle">最大视野角度。</param>
        public bool IsInView(Vector3 worldPosition, float maxAngle = 60f)
        {
            Vector3 toTarget = (worldPosition - GetHeadPosition()).normalized;
            return Vector3.Angle(CurrentAimDirection, toTarget) <= maxAngle;
        }
        
        /// <summary>
        /// 强制触发一次重力变换更新。
        /// </summary>
        public void ForceGravityUpdate()
        {
            // 该方法可用于在非自动检测的情况下强制执行一次完整的重力校准流程。
            UpdateGravityTransform(true);
        }
        
        /// <summary>
        /// 获取当前的俯仰角限制（负值为向下，正值为向上）。
        /// </summary>
        public Vector2 GetCurrentPitchLimits()
        {
            return new Vector2(-_maxPitchDown, _maxPitchUp);
        }
        
        /// <summary>
        /// 获取当前俯仰角相对于限制的归一化值（-1到1，0为水平）。
        /// </summary>
        public float GetPitchNormalized()
        {
            if (_currentPitch >= 0)
            {
                return _maxPitchUp > Epsilon? _currentPitch / _maxPitchUp : 0f;
            }
            return _maxPitchDown > Epsilon? _currentPitch / _maxPitchDown : 0f;
        }
        
        /// <summary>
        /// 强制身体方向立即对齐到视角方向（用于重生、传送等特殊情况）。
        /// </summary>
        public void ForceBodyAlignment()
        {
            Vector3 aimDirectionHorizontal = Vector3.ProjectOnPlane(CurrentAimDirection, _motor.UpAxis).normalized;
            
            if (aimDirectionHorizontal.magnitude > Epsilon)
            {
                _referenceBodyDirection = (_inverseGravityTransform * aimDirectionHorizontal).normalized;
                _motor.SetTargetYawDirection(aimDirectionHorizontal);
            }
        }
        
        /// <summary>
        /// 获取当前头部与身体的水平角度差（度）。
        /// </summary>
        public float GetHeadBodyAngleDifference()
        {
            Vector3 currentUp = _motor.UpAxis;
            Vector3 aimHorizontal = Vector3.ProjectOnPlane(CurrentAimDirection, currentUp).normalized;
            Vector3 bodyHorizontal = Vector3.ProjectOnPlane(CurrentBodyDirection, currentUp).normalized;
            
            if (aimHorizontal.magnitude < Epsilon || bodyHorizontal.magnitude < Epsilon)
                return 0f;
                
            return Vector3.SignedAngle(bodyHorizontal, aimHorizontal, currentUp);
        }        /// <summary>
        /// 设置俯仰角维护功能的开关状态。
        /// </summary>
        /// <param name="enabled">是否启用俯仰角维护。</param>
        public void SetPitchMaintenanceEnabled(bool enabled)
        {
            _maintainPitchToUpAxis = enabled;
            if (!enabled)
            {
                // 禁用时更新当前角度状态
                UpdatePitchAngleToUpAxis();
            }
        }
        
        /// <summary>
        /// 重置俯仰角度到当前状态。
        /// </summary>
        public void ResetPitchAngleToUpAxis()
        {
            UpdatePitchAngleToUpAxis();
        }

        #endregion

        #region 核心逻辑 (Core Logic)

        /// <summary>
        /// 初始化摄像机。
        /// </summary>
        private void InitializeCamera()
        {
            _playerCamera.transform.localPosition = _camOffset;
            _playerCamera.fieldOfView = _cameraFov;
        }

        /// <summary>
        /// 初始化或更新重力变换。
        /// </summary>
        private void InitializeGravityTransform()
        {
            Vector3 currentGravityUp = _motor.UpAxis;
            _gravityTransform = Quaternion.FromToRotation(ReferenceGravityUp, currentGravityUp);
            _inverseGravityTransform = Quaternion.Inverse(_gravityTransform);
        }

        /// <summary>
        /// 每帧检查并更新重力变换。当重力方向发生显著变化时，执行状态保持逻辑以确保视觉连续性。
        /// </summary>
        /// <param name="forceUpdate">如果为 true，则无条件执行更新，忽略角度变化阈值。</param>
        private void UpdateGravityTransform(bool forceUpdate = false)
        {
            Vector3 currentGravityUp = _motor.UpAxis;
            Quaternion newGravityTransform = Quaternion.FromToRotation(ReferenceGravityUp, currentGravityUp);

            // 仅当角度变化超过阈值或被强制时才更新，以节省性能并触发状态保持逻辑
            if (forceUpdate || Quaternion.Angle(_gravityTransform, newGravityTransform) > 0.1f)
            {
                #region 重力切换时的姿态保持核心逻辑 (Gravity Transition Logic)

                // 1. [缓存] 在更新重力变换前，保存当前在世界空间中的精确朝向。
                // 这是"修正前"的状态，也是我们希望在"修正后"保持的状态。
                Vector3 preChangeWorldAim = CurrentAimDirection;
                Vector3 preChangeWorldBody = CurrentBodyDirection;

                // 2. [变换] 更新重力变换和其逆矩阵。
                _gravityTransform = newGravityTransform;
                _inverseGravityTransform = Quaternion.Inverse(_gravityTransform);

                // 3. [重投影] 使用新的逆变换，将之前缓存的世界空间朝向"投影"回参考坐标系，
                //    从而计算出能维持原世界朝向的、新的参考向量。
                _referenceAimDirection = (_inverseGravityTransform * preChangeWorldAim).normalized;
                _referenceBodyDirection = (_inverseGravityTransform * preChangeWorldBody).normalized;
                
                // 4. [验证 - 可选调试]
                if (_logGravityChangeVerification)
                {
                    Vector3 postChangeWorldAim = CurrentAimDirection;
                    Debug.Log($"<b>[PlayerView] Gravity Change Verified.</b>\n" +
                              $"Pre-Change World Aim:  {preChangeWorldAim.ToString("F6")}\n" +
                              $"Post-Change World Aim: {postChangeWorldAim.ToString("F6")}\n" +
                              $"Magnitude Diff: {(preChangeWorldAim - postChangeWorldAim).magnitude:E2}");
                }
            }
        }
        #endregion

        /// <summary>
        /// 基于当前身体朝向，初始化所有方向向量。
        /// </summary>
        private void InitializeDirections()
        {
            // 将当前世界坐标的身体朝向，通过逆变换转换为参考坐标系中的方向
            _referenceBodyDirection = (_inverseGravityTransform * _playerBody.forward).normalized;
            
            if (_referenceBodyDirection.magnitude < Epsilon)
            {
                _referenceBodyDirection = Vector3.forward; // 安全回退
            }            // 初始时，视角与身体朝向一致
            ResetView();
            UpdateHorizontalDirections();
        }        /// <summary>
        /// 初始化重力响应系统。
        /// </summary>
        private void InitializeGravityResponse()
        {
            _lastGravityUp = _motor.UpAxis;
            // 初始化俯仰角度：计算当前瞄准方向相对于upAxis的角度
            UpdatePitchAngleToUpAxis();        }

        /// <summary>
        /// 更新俯仰角相对于upAxis的角度。
        /// </summary>
        private void UpdatePitchAngleToUpAxis()
        {
            Vector3 currentUp = _motor.UpAxis;
            Vector3 aimDirection = CurrentAimDirection;
            
            // 计算瞄准方向在当前重力平面上的投影
            Vector3 horizontalAim = Vector3.ProjectOnPlane(aimDirection, currentUp).normalized;
            
            if (horizontalAim.magnitude > Epsilon)
            {
                // 计算俯仰角：从水平方向到瞄准方向的角度
                Vector3 rightAxis = Vector3.Cross(horizontalAim, currentUp).normalized;
                _pitchAngleToUpAxis = Vector3.SignedAngle(horizontalAim, aimDirection, rightAxis);
            }
            else
            {
                // 如果是垂直瞄准，直接设置为90度或-90度
                _pitchAngleToUpAxis = Vector3.Dot(aimDirection, currentUp) > 0 ? 90f : -90f;
            }
        }

        /// <summary>
        /// 俯仰角维护逻辑：当重力变化且俯仰输入很小时，独立维护俯仰角度。
        /// 核心：不修改任何旋转信息，只计算和维护角度值。
        /// </summary>
        private void UpdatePitchMaintenance()
        {
            Vector3 currentGravityUp = _motor.UpAxis;
            Vector2 lookInput = _playerInput.LookInput;
            float pitchInput = _invertY ? lookInput.y : -lookInput.y;

            // 检测重力变化
            bool gravityChanged = Vector3.Angle(_lastGravityUp, currentGravityUp) > 0.1f;
            bool lowPitchInput = Mathf.Abs(pitchInput) < _pitchInputThreshold;

            if (!_maintainPitchToUpAxis)
            {
                // 俯仰角维护关闭：正常更新俯仰角度
                UpdatePitchAngleToUpAxis();
                _lastGravityUp = currentGravityUp;
                _isGravityResponseActive = false;
                return;
            }

            // 检查是否应该激活俯仰角维护
            if (gravityChanged && lowPitchInput)
            {
                // 重力变化且俯仰输入很小：激活维护模式，保持 _pitchAngleToUpAxis 不变
                _isGravityResponseActive = true;

                if (_logGravityChangeVerification)
                {
                    Debug.Log($"[PlayerView] 俯仰角维护：保持角度 {_pitchAngleToUpAxis:F1}°, " +
                             $"重力变化 {Vector3.Angle(_lastGravityUp, currentGravityUp):F1}°");
                }

                _lastGravityUp = currentGravityUp;
                // 关键：不调用UpdatePitchAngleToUpAxis()，保持角度不变
            }
            else if (Mathf.Abs(pitchInput) > _pitchInputThreshold)
            {
                // 有明显的俯仰输入：退出维护模式，正常更新角度
                _isGravityResponseActive = false;
                UpdatePitchAngleToUpAxis();

                if (gravityChanged)
                {
                    _lastGravityUp = currentGravityUp;
                }
            }
            else if (!_isGravityResponseActive)
            {
                // 维护模式未激活，正常更新角度
                UpdatePitchAngleToUpAxis();

                if (gravityChanged)
                {
                    _lastGravityUp = currentGravityUp;
                }
            }
            // 如果维护模式激活且没有明显输入，保持角度不变
        }

        /// <summary>
        /// 处理玩家的视角输入。
        /// </summary>
        private void HandleInput()
        {
            Vector2 lookInput = _playerInput.LookInput * (_lookSensitivity * Time.deltaTime);
            float pitchDelta = _invertY? lookInput.y : -lookInput.y;
            ApplyViewRotation(lookInput.x, pitchDelta);
        }

        /// <summary>
        /// 根据输入，应用视角旋转。
        /// </summary>
        private void ApplyViewRotation(float yawDelta, float pitchDelta)
        {
            // 水平旋转 (Yaw)
            if (Mathf.Abs(yawDelta) > Epsilon)
            {
                // 在参考系中绕标准上方向旋转
                _referenceAimDirection = Quaternion.AngleAxis(yawDelta, ReferenceGravityUp) * _referenceAimDirection;
            }

            // 垂直旋转 (Pitch)
            if (Mathf.Abs(pitchDelta) > Epsilon)
            {
                // 计算参考系中的右方向轴
                Vector3 refRight = Vector3.Cross(ReferenceGravityUp, _referenceAimDirection).normalized;
                Vector3 potentialNewDir = Quaternion.AngleAxis(pitchDelta, refRight) * _referenceAimDirection;

                // 在应用旋转前，检查是否会超出俯仰角限制。
                // 注意：此检查在世界空间中进行，以确保其在任意重力下都正确。
                if (IsWithinPitchLimits(_gravityTransform * potentialNewDir))
                {
                    _referenceAimDirection = potentialNewDir;
                }
            }

            _referenceAimDirection.Normalize();
            
            // 更新当前俯仰角，但要考虑重力响应状态
            _currentPitch = CalculateCurrentPitch(CurrentAimDirection);

              // 如果有俯仰输入且俯仰角维护开启，更新维护的角度
            if (_maintainPitchToUpAxis && Mathf.Abs(pitchDelta) > Epsilon)
            {
                UpdatePitchAngleToUpAxis();
            }
        }
        
        /// <summary>
        /// 更新身体旋转，使其跟随头部朝向。
        /// </summary>
        private void UpdateBodyRotation()
        {
            Vector3 currentUp = _motor.UpAxis;
            
            // 计算视线和身体在当前重力平面上的投影
            Vector3 aimHorizontal = Vector3.ProjectOnPlane(CurrentAimDirection, currentUp).normalized;
            if (aimHorizontal.magnitude < Epsilon) return; // 无法判断水平朝向（例如直视天顶）

            Vector3 bodyHorizontal = Vector3.ProjectOnPlane(CurrentBodyDirection, currentUp).normalized;
            if (bodyHorizontal.magnitude < Epsilon) bodyHorizontal = aimHorizontal; // 如果身体方向无效，则直接对齐

            float angleDiff = Vector3.SignedAngle(bodyHorizontal, aimHorizontal, currentUp);

            // 当头身角度差超过限制时，身体强制跟上
            if (Mathf.Abs(angleDiff) > _headYawLimit)
            {
                float overflowAngle = angleDiff - Mathf.Sign(angleDiff) * _headYawLimit;
                // 使用更平滑的插值方式来计算旋转量
                float rotationAmount = Mathf.Lerp(0, overflowAngle, _bodyRotationSpeed * Time.deltaTime);
                Vector3 newBodyDirWorld = Quaternion.AngleAxis(rotationAmount, currentUp) * bodyHorizontal;
                
                _referenceBodyDirection = (_inverseGravityTransform * newBodyDirWorld).normalized;
                _motor.SetTargetYawDirection(newBodyDirWorld);
            }
            // 当角度差小于一定阈值时，身体缓慢对齐头部
            else if (Mathf.Abs(angleDiff) > Epsilon && Mathf.Abs(angleDiff) < _headYawLimit * _alignmentThreshold)
            {
                float alignAmount = _bodyRotationSpeed * _bodyAlignmentSpeed * Time.deltaTime;
                Vector3 newBodyDirWorld = Vector3.Slerp(bodyHorizontal, aimHorizontal, alignAmount).normalized;

                _referenceBodyDirection = (_inverseGravityTransform * newBodyDirWorld).normalized;
                _motor.SetTargetYawDirection(newBodyDirWorld);
            }
        }

        /// <summary>
        /// 更新瞄准目标点的位置。
        /// </summary>
        private void UpdateAimTargetPosition()
        {
            if (_aimTarget == null) return;

            Vector3 headPosition = GetHeadPosition();
            Vector3 aimDirection = CurrentAimDirection;

            _aimTarget.position = headPosition + aimDirection * _aimDistance;
            _aimTarget.rotation = Quaternion.LookRotation(aimDirection, _motor.UpAxis);
        }

        /// <summary>
        /// 更新用于角色移动的水平方向向量。
        /// </summary>
        private void UpdateHorizontalDirections()
        {
            Vector3 currentUp = _motor.UpAxis;
            
            // 移动的前进方向，基于瞄准方向在重力平面上的投影
            HorizontalForwardDirection = Vector3.ProjectOnPlane(CurrentAimDirection, currentUp).normalized;
            
            // 如果玩家直视天顶或地心，前进方向可能为零向量，此时提供一个基于身体朝向的回退方案
            if (HorizontalForwardDirection.magnitude < Epsilon)
            {
                HorizontalForwardDirection = Vector3.ProjectOnPlane(CurrentBodyDirection, currentUp).normalized;
            }

            // 右方向垂直于前进方向和上方向
            HorizontalRightDirection = Vector3.Cross(currentUp, HorizontalForwardDirection).normalized;
        }

        #endregion

        #region 辅助方法 (Helper Methods)

        /// <summary>
        /// 获取头部在世界空间中的位置。
        /// </summary>
        private Vector3 GetHeadPosition()
        {
            // 头部位置受身体旋转影响，以模拟真实的头部运动
            return _playerBody.position + _headOffset;
        }

        /// <summary>
        /// 检查给定的方向是否在允许的俯仰范围内（重力自适应）。
        /// </summary>
        private bool IsWithinPitchLimits(Vector3 worldDirection)
        {
            Vector3 gravityUp = _motor.UpAxis;
            Vector3 directionOnPlane = Vector3.ProjectOnPlane(worldDirection, gravityUp);

            // 当视线垂直于重力平面时（直上或直下）
            if (directionOnPlane.magnitude < Epsilon)
            {
                float upDot = Vector3.Dot(worldDirection, gravityUp);
                if (upDot > 0) return _maxPitchUp >= 90f - Epsilon;
                return _maxPitchDown >= 90f - Epsilon;
            }

            // 计算俯仰角
            float pitchAngle = Vector3.SignedAngle(directionOnPlane, worldDirection, Vector3.Cross(directionOnPlane, gravityUp));
            
            return pitchAngle <= _maxPitchUp + Epsilon && pitchAngle >= -_maxPitchDown - Epsilon;
        }

        /// <summary>
        /// 计算给定方向的俯仰角。
        /// </summary>
        private float CalculateCurrentPitch(Vector3 worldDirection)
        {
            Vector3 gravityUp = _motor.UpAxis;
            Vector3 directionOnPlane = Vector3.ProjectOnPlane(worldDirection, gravityUp);

            if (directionOnPlane.magnitude < Epsilon)
            {
                return Vector3.Dot(worldDirection, gravityUp) > 0? 90f : -90f;
            }
            
            return Vector3.SignedAngle(directionOnPlane, worldDirection, Vector3.Cross(directionOnPlane, gravityUp));
        }

        #endregion
        
        #region 调试 (Debugging)
          private void UpdateDebugInfo()
        {
            // 每 30 帧更新一次，避免刷屏
            if (Time.frameCount % 30!= 0) return;            var info = $"<b>[PlayerView-GravityAdaptive]</b>\n" +
                       $"Pitch: {_currentPitch:F1}° (Limits: ↑{_maxPitchUp}° ↓{_maxPitchDown}°)\n" +
                       $"Gravity Up: {_motor.UpAxis:F2}\n" +
                       $"Aim Direction (World): {CurrentAimDirection:F2}\n" +
                       $"Body Direction (World): {CurrentBodyDirection:F2}\n" +
                       $"Head-Body Angle: {GetHeadBodyAngleDifference():F1}°\n" +
                       $"Move Forward: {HorizontalForwardDirection:F2}\n" +
                       $"Move Right: {HorizontalRightDirection:F2}\n" +                       $"Pitch Maintenance: {(_maintainPitchToUpAxis ? "ON" : "OFF")}\n" +
                       $"Pitch Maintenance Active: {(_isGravityResponseActive ? "YES" : "NO")}\n" +
                       $"Pitch to UpAxis: {_pitchAngleToUpAxis:F1}°\n" +
                       $"Pitch Input Threshold: {_pitchInputThreshold:F2}";
            Debug.Log(info);
        }
        
        private void OnDrawGizmosSelected()
        {
            if (!_showDebugGizmos || _playerBody == null) return;

            Vector3 headPos = Application.isPlaying? GetHeadPosition() : _playerBody.position + _headOffset;
            Vector3 bodyPos = _playerBody.position;
            
            // 瞄准方向 (红色)
            Gizmos.color = Color.red;
            Gizmos.DrawLine(headPos, headPos + CurrentAimDirection * 3f);
            
            // 身体朝向 (蓝色)
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(bodyPos, bodyPos + CurrentBodyDirection * 2.5f);
            
            // 水平移动方向 (白色/灰色)
            Gizmos.color = Color.white;
            Gizmos.DrawLine(bodyPos, bodyPos + HorizontalForwardDirection * 2f);
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(bodyPos, bodyPos + HorizontalRightDirection * 2f);
            
            // Aim Target (黄色)
            if (_aimTarget!= null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_aimTarget.position, 0.2f);
                Gizmos.color = new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b, 0.5f);
                Gizmos.DrawLine(headPos, _aimTarget.position);
            }
        }
        
        #endregion
    }
}