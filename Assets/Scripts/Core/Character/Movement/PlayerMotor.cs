using UnityEngine;

namespace DWHITE
{
    /// <summary>
    /// 基于Rigidbody的角色运动控制器，为自定义重力环境设计。
    /// 通过直接控制速度而非施加力，提供精确且可预测的移动手感。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerMotor : MonoBehaviour
    {
        #region Configuration & Dependencies
        
        [Header("核心配置")]
        [Tooltip("包含所有移动调谐参数的ScriptableObject。")]
        [SerializeField] private MovementTuningSO _tuning;
        [Tooltip("（可选）用于视觉反馈的角色Animator。")]
        [SerializeField] private Animator _animator;
        [Tooltip("角色视觉模型的根Transform，用于旋转。")]
        [SerializeField] private GameObject _characterRig;

        [Header("地面检测")]
        [Tooltip("定义哪些层被视作“地面”。")]
        [SerializeField] private LayerMask _groundLayer = -1;
        [Tooltip("从角色脚底向下发出的主要地面检测射线距离。")]
        [SerializeField] private float _probeDistance = 0.1f;
        [Tooltip("允许角色“吸附”到地面的最大距离，用于平滑地走下斜坡和台阶。")]
        [SerializeField] private float _snapProbeDistance = 0.5f;
        [Tooltip("地面检测射线的起始点相对于角色根位置的垂直偏移。")]
        [SerializeField] private float _groundCheckOffset = 0.05f;

        [Header("调试")]
        [Tooltip("在屏幕上显示实时的调试信息（OnGUI）。")]
        [SerializeField] private bool _showDebugInfo = false;
        [Tooltip("在场景视图中绘制辅助线。")]
        [SerializeField] private bool _showDebugGizmos = false;

        // 组件引用
        private Rigidbody _rb;
        private PlayerInput _playerInput;
        private FPSGravityCamera _cameraController;
        private Transform _camOrientation;
        
        #endregion

        #region State Variables

        // 运动状态
        private Vector3 _velocity;
        private Vector3 _desiredVelocity;
        private Vector3 _gravity;

        // 坐标轴状态
        private Vector3 _upAxis = Vector3.up;
        private Vector3 _rightAxis = Vector3.right;
        private Vector3 _forwardAxis = Vector3.forward;

        // 地面检测状态
        private bool _onGround;
        private bool _onSteep;
        private Vector3 _contactNormal;
        private Vector3 _groundNormal;
        private float _minGroundDotProduct;
        private int _groundContactCount;
        private int _stepsSinceLastGrounded;
        private int _stepsSinceLastJump;

        // 跳跃状态
        private bool _desiredJump;
        private int _jumpPhase;
        private int _coyoteCounter;
        private int _jumpBufferCounter;

        // 奔跑状态
        private bool _isSprinting;
        private bool _sprintToggled;
        private float _currentSpeedMultiplier = 1f;
        
        #endregion

        #region Public Properties

        /// <summary>
        /// 角色当前的物理速度。
        /// </summary>
        public Vector3 Velocity => _velocity;
        
        /// <summary>
        /// 角色当前的“上”方向，由重力系统决定。
        /// </summary>
        public Vector3 UpAxis => _upAxis;
        
        /// <summary>
        /// 角色当前是否在地面上。
        /// </summary>
        public bool IsGrounded => _onGround;

        /// <summary>
        /// 角色当前是否在过于陡峭的斜坡上。
        /// </summary>
        public bool OnSteep => _onSteep;

        /// <summary>
        /// 角色在地面上的基础移动速度。
        /// </summary>
        public float MoveSpeed => _tuning != null ? _tuning.maxGroundSpeed : 10f;
        
        /// <summary>
        /// 当前使用的移动调参配置。
        /// </summary>
        public MovementTuningSO Tuning => _tuning;
        
        /// <summary>
        /// 角色当前是否在奔跑。
        /// </summary>
        public bool IsSprinting => _isSprinting;
        
        /// <summary>
        /// 当前的速度倍率，用于平滑过渡奔跑状态。
        /// </summary>
        public float CurrentSpeedMultiplier => _currentSpeedMultiplier;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // 初始化组件引用
            _rb = GetComponent<Rigidbody>();
            _playerInput = GetComponent<PlayerInput>();
            _cameraController = GetComponent<FPSGravityCamera>();
            if (transform.childCount > 0)
            {
                _camOrientation = transform.GetChild(0);
            }

            // 配置Rigidbody
            _rb.useGravity = false;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            
            // 加载并验证配置
            LoadAndValidateTuning();
        }
        
        private void Start()
        {
            _playerInput.SetCursorLock(true);
            ResetState();
        }

        private void Update()
        {
            HandleInput();
        }

        private void FixedUpdate()
        {
            if (_tuning == null) return;

            // 更新重力与状态
            _gravity = CustomGravity.GetGravity(_rb.position, out _upAxis);
            UpdateState();
            
            // 计算并应用速度
            AdjustVelocity();
            
            // 执行跳跃
            if (_desiredJump)
            {
                _desiredJump = false;
                Jump();
            }

            // 应用重力并更新Rigidbody
            _velocity += _gravity * Time.fixedDeltaTime;
            _rb.velocity = _velocity;

            // 清理本帧状态并对齐旋转
            ClearState();
            AlignRotationToGravity();

            // 更新动画
            UpdateAnimator();
        }

        private void OnValidate()
        {
            if (_tuning != null)
            {
                _minGroundDotProduct = Mathf.Cos(_tuning.maxGroundAngle * Mathf.Deg2Rad);
            }
        }
        
        #endregion

        #region Input Handling

        /// <summary>
        /// 在Update中处理所有玩家输入。
        /// </summary>
        private void HandleInput()
        {
            HandleSprintInput();
            HandleJumpInput();
            
            UpdateMovementAxes();
            UpdateDesiredVelocity();
        }
        
        /// <summary>
        /// 处理奔跑相关的输入和状态切换。
        /// </summary>
        private void HandleSprintInput()
        {
            if (_tuning == null) return;

            switch (_tuning.sprintMode)
            {
                case SprintMode.Hold:
                    _isSprinting = _playerInput.SprintHeld && _onGround && _playerInput.MoveInput.magnitude > 0.1f;
                    break;
                case SprintMode.Toggle:
                    if (_playerInput.SprintPressed)
                    {
                        _sprintToggled = !_sprintToggled;
                    }
                    _isSprinting = _sprintToggled && _onGround && _playerInput.MoveInput.magnitude > 0.1f;
                    break;
            }

            float targetMultiplier = _isSprinting ? _tuning.sprintSpeedMultiplier : 1f;
            _currentSpeedMultiplier = Mathf.Lerp(_currentSpeedMultiplier, targetMultiplier, _tuning.sprintTransitionSpeed * Time.deltaTime);
        }

        /// <summary>
        /// 处理跳跃相关的输入，包括跳跃缓冲。
        /// </summary>
        private void HandleJumpInput()
        {
            if (_playerInput.JumpPressed)
            {
                _jumpBufferCounter = _tuning.jumpBufferFrames;
            }
            else if (_jumpBufferCounter > 0)
            {
                _jumpBufferCounter--;
            }

            if (_jumpBufferCounter > 0 && CanJump())
            {
                _desiredJump = true;
                _jumpBufferCounter = 0;
            }
        }

        /// <summary>
        /// 从相机更新移动坐标轴。
        /// </summary>
        private void UpdateMovementAxes()
        {
            if (_cameraController == null) return;
            _forwardAxis = _cameraController.HorizontalForwardDirection;
            _rightAxis = _cameraController.HorizontalRightDirection;
        }

        /// <summary>
        /// 根据输入和当前最大速度计算期望速度。
        /// </summary>
        private void UpdateDesiredVelocity()
        {
            float currentMaxSpeed = GetCurrentMaxSpeed();
            Vector2 moveInput = _playerInput.MoveInput;
            
            _desiredVelocity = (_rightAxis * moveInput.x + _forwardAxis * moveInput.y) * currentMaxSpeed;
            _desiredVelocity = Vector3.ClampMagnitude(_desiredVelocity, currentMaxSpeed);
        }

        #endregion

        #region Movement & Physics

        /// <summary>
        /// 在FixedUpdate中根据输入调整实际速度。
        /// </summary>
        private void AdjustVelocity()
        {
            Vector2 moveInput = _playerInput.MoveInput;
            
            float baseAcceleration = _onGround ? _tuning.maxGroundAcceleration : _tuning.maxAirAcceleration;
            float acceleration = _isSprinting && _onGround ? baseAcceleration * _tuning.sprintAccelerationMultiplier : baseAcceleration;
            
            // 将移动轴向投影到地面/接触平面
            Vector3 xAxis = ProjectDirectionOnPlane(_rightAxis, _contactNormal);
            Vector3 zAxis = ProjectDirectionOnPlane(_forwardAxis, _contactNormal);

            // 计算当前在移动平面上的速度
            float currentX = Vector3.Dot(_velocity, xAxis);
            float currentZ = Vector3.Dot(_velocity, zAxis);

            // 计算期望在移动平面上的速度
            float desiredX = Vector3.Dot(_desiredVelocity, xAxis);
            float desiredZ = Vector3.Dot(_desiredVelocity, zAxis);

            // 如果没有输入，应用刹车
            if (moveInput.magnitude < 0.01f)
            {
                acceleration *= _tuning.brakeMultiplier;
            }

            float maxSpeedChange = acceleration * Time.fixedDeltaTime;

            float newX = Mathf.MoveTowards(currentX, desiredX, maxSpeedChange);
            float newZ = Mathf.MoveTowards(currentZ, desiredZ, maxSpeedChange);

            // 更新速度
            _velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
        }

        /// <summary>
        /// 执行跳跃。
        /// </summary>
        private void Jump()
        {
            Vector3 jumpDirection;
            if (_onGround && !_onSteep)
            {
                jumpDirection = _contactNormal;
            }
            else if (_coyoteCounter > 0)
            {
                jumpDirection = _upAxis;
            }
            else
            {
                return;
            }

            _stepsSinceLastJump = 0;
            _jumpPhase += 1;
            
            float jumpSpeed = Mathf.Sqrt(2f * _gravity.magnitude * _tuning.jumpHeight);
            
            // 混合地面法线和重力上方向，获得更直观的跳跃方向
            jumpDirection = (jumpDirection + _upAxis).normalized;
            
            // 如果已经在向上移动，减去当前速度以确保跳跃高度一致
            float alignedSpeed = Vector3.Dot(_velocity, jumpDirection);
            if (alignedSpeed > 0f)
            {
                jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
            }
            
            _velocity += jumpDirection * jumpSpeed;
            _onGround = false;
            _coyoteCounter = 0;

            if (_showDebugInfo) Debug.Log("[RBPlayerMotor] Player jumped");
        }
        
        /// <summary>
        /// 平滑地将角色自身的Transform旋转到与当前重力方向对齐。
        /// </summary>
        private void AlignRotationToGravity()
        {
            Vector3 characterForward = ProjectDirectionOnPlane(transform.forward, _upAxis);
            if (characterForward == Vector3.zero)
            {
                // 如果角色完全垂直，使用相机的水平朝向作为参考
                characterForward = ProjectDirectionOnPlane(_cameraController.transform.forward, _upAxis);
            }

            Quaternion targetRotation = Quaternion.LookRotation(characterForward, _upAxis);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                _tuning.turnResponsiveness * Time.fixedDeltaTime
            );
        }

        #endregion

        #region State Management & Ground Check
        
        /// <summary>
        /// 在FixedUpdate的开始阶段更新所有运动状态。
        /// </summary>
        private void UpdateState()
        {
            _stepsSinceLastGrounded++;
            _stepsSinceLastJump++;
            _velocity = _rb.velocity;

            if (CheckGround() || SnapToGround())
            {
                _stepsSinceLastGrounded = 0;
                if (_stepsSinceLastJump > 1)
                {
                    _jumpPhase = 0;
                }
                if (_groundContactCount > 1)
                {
                    _contactNormal.Normalize();
                }
            }
            else
            {
                _contactNormal = _upAxis;
            }

            UpdateCoyoteTime();
        }

        /// <summary>
        /// 使用Raycast进行主要的地面检测。
        /// </summary>
        /// <returns>是否检测到有效的地面。</returns>
        private bool CheckGround()
        {
            _onGround = false;
            _contactNormal = Vector3.zero;

            if (Physics.Raycast(
                _rb.position + _upAxis * _groundCheckOffset,
                -_upAxis,
                out RaycastHit hit,
                _probeDistance,
                _groundLayer,
                QueryTriggerInteraction.Ignore
            ))
            {
                if (Vector3.Dot(_upAxis, hit.normal) >= _minGroundDotProduct)
                {
                    _onGround = true;
                    _contactNormal = hit.normal;
                    _groundNormal = hit.normal;
                    _onSteep = Vector3.Angle(_upAxis, _groundNormal) > _tuning.maxGroundAngle;

                    // 如果在可行走的地面上，抵消向下的速度以避免弹跳
                    if (!_onSteep && Vector3.Dot(_velocity, _upAxis) < 0f)
                    {
                        _velocity -= _upAxis * Vector3.Dot(_velocity, _upAxis);
                    }
                }
            }

            if (!_onGround)
            {
                _groundNormal = _upAxis;
                _onSteep = false;
            }

            return _onGround;
        }

        /// <summary>
        /// 尝试将角色吸附到地面，以处理斜坡和台阶。
        /// </summary>
        /// <returns>是否成功吸附到地面。</returns>
        private bool SnapToGround()
        {
            if (_stepsSinceLastGrounded > 1 || _stepsSinceLastJump <= 2 || _velocity.magnitude > _tuning.maxSnapSpeed)
            {
                return false;
            }

            if (!Physics.Raycast(_rb.position, -_upAxis, out RaycastHit hit, _snapProbeDistance, _groundLayer, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            
            if (Vector3.Dot(_upAxis, hit.normal) < _minGroundDotProduct)
            {
                return false;
            }

            _onGround = true;
            _contactNormal = hit.normal;
            _groundNormal = hit.normal;

            // 将速度投影到地面平面
            float speed = _velocity.magnitude;
            float dot = Vector3.Dot(_velocity, hit.normal);
            if (dot > 0f)
            {
                _velocity = (_velocity - hit.normal * dot).normalized * speed;
            }

            return true;
        }
        
        /// <summary>
        /// 更新土狼时间计数器。
        /// </summary>
        private void UpdateCoyoteTime()
        {
            if (!_onGround && _coyoteCounter > 0)
            {
                _coyoteCounter--;
            }
            else if (_onGround)
            {
                _coyoteCounter = _tuning.coyoteFrames;
            }
        }
        
        /// <summary>
        /// 在FixedUpdate结束时清除瞬时状态。
        /// </summary>
        private void ClearState()
        {
            _groundContactCount = 0;
            _contactNormal = Vector3.zero;
        }

        /// <summary>
        /// 重置所有状态变量到初始值。
        /// </summary>
        public void ResetState()
        {
            _velocity = Vector3.zero;
            _rb.velocity = Vector3.zero;
            _jumpPhase = 0;
            _coyoteCounter = 0;
            _jumpBufferCounter = 0;
            _stepsSinceLastGrounded = 0;
            _stepsSinceLastJump = 0;
            _isSprinting = false;
            _sprintToggled = false;
            _currentSpeedMultiplier = 1f;
        }

        #endregion

        #region Helper & Utility Methods

        /// <summary>
        /// 加载并验证移动调参配置。
        /// </summary>
        private void LoadAndValidateTuning()
        {
            if (_tuning == null)
            {
                _tuning = Resources.Load<MovementTuningSO>("MovementTuning");
                if (_tuning == null)
                {
                    Debug.LogError("[PlayerMotor] 未找到 MovementTuningSO 配置文件 'MovementTuning'，请创建并放置在Resources文件夹中，或在检视面板中手动指定。");
                    enabled = false;
                    return;
                }
            }
            OnValidate();
        }

        /// <summary>
        /// 检查是否满足跳跃条件。
        /// </summary>
        private bool CanJump()
        {
            return (_onGround && !_onSteep) || _coyoteCounter > 0;
        }

        /// <summary>
        /// 计算并返回当前的最大移动速度。
        /// </summary>
        private float GetCurrentMaxSpeed()
        {
            if (_tuning == null) return 10f;
            return _onGround ? _tuning.maxGroundSpeed * _currentSpeedMultiplier : _tuning.maxAirSpeed;
        }
        
        /// <summary>
        /// 将给定的方向向量投影到一个法线定义的平面上。
        /// </summary>
        private Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal)
        {
            return (direction - normal * Vector3.Dot(direction, normal)).normalized;
        }
        
        /// <summary>
        /// 更新Animator中的参数。
        /// </summary>
        private void UpdateAnimator()
        {
            if (_animator != null)
            {
                float velocityForward = Vector3.Dot(_velocity, _forwardAxis);
                float velocityStrafe = -Vector3.Dot(_velocity, _rightAxis); // 取反以匹配常见动画BlendTree设置
                _animator.SetFloat("velocityForward", velocityForward);
                _animator.SetFloat("velocityStrafe", velocityStrafe);
                _animator.SetBool("isSprinting", _isSprinting);
            }
        }
        
        #endregion

        #region Debugging

        private void OnDrawGizmosSelected()
        {
            if (!_showDebugGizmos || !Application.isPlaying) return;

            // 绘制基础坐标轴
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + _upAxis * 2f);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + _rightAxis * 2f);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + _forwardAxis * 2f);

            // 绘制速度
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + _velocity);

            // 绘制地面检测
            Vector3 groundCheckStart = _rb.position + _upAxis * _groundCheckOffset;
            Gizmos.color = _onGround ? Color.green : Color.red;
            Gizmos.DrawLine(groundCheckStart, groundCheckStart - _upAxis * _probeDistance);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(_rb.position, _rb.position - _upAxis * _snapProbeDistance);

            // 绘制重力方向
            if (_gravity != Vector3.zero)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, transform.position + _gravity.normalized * 1.5f);
            }
        }

        private void OnGUI()
        {
            if (!_showDebugInfo || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 650));
            GUILayout.BeginVertical("box");
            
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            
            GUILayout.Label("Player Motor Debug", titleStyle);
            GUILayout.Space(5);

            GUILayout.Label("● Movement", titleStyle);
            GUILayout.Label($"Velocity: {_velocity:F2} (Magnitude: {_velocity.magnitude:F2})");
            GUILayout.Label($"Desired Velocity: {_desiredVelocity:F2}");
            GUILayout.Space(5);
              
            GUILayout.Label("● Ground State", titleStyle);
            GUILayout.Label($"On Ground: {_onGround}");
            GUILayout.Label($"On Steep: {_onSteep}");
            GUILayout.Label($"Contact Normal: {_contactNormal:F2}");
            GUILayout.Label($"Ground Normal: {_groundNormal:F2}");
            GUILayout.Space(5);
            
            GUILayout.Label("● Jump State", titleStyle);
            GUILayout.Label($"Jump Phase: {_jumpPhase}");
            GUILayout.Label($"Coyote Counter: {_coyoteCounter}");
            GUILayout.Label($"Jump Buffer: {_jumpBufferCounter}");
            GUILayout.Space(5);
            
            GUILayout.Label("● Sprint State", titleStyle);
            GUILayout.Label($"Is Sprinting: {_isSprinting}");
            GUILayout.Label($"Speed Multiplier: {_currentSpeedMultiplier:F2}");
            GUILayout.Space(5);

            GUILayout.Label("● Orientation", titleStyle);
            GUILayout.Label($"Up Axis: {_upAxis:F2}");
            GUILayout.Label($"Forward Axis: {_forwardAxis:F2}");
            GUILayout.Label($"Right Axis: {_rightAxis:F2}");
            GUILayout.Space(5);
            
            GUILayout.Label("● Gravity", titleStyle);
            GUILayout.Label($"Direction: {_gravity.normalized:F2}");
            GUILayout.Label($"Magnitude: {_gravity.magnitude:F2} m/s²");

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        #endregion
    }
}