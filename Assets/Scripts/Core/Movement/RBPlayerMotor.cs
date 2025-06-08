using UnityEngine;

namespace DWHITE 
{	
    /// <summary>
    /// 基于 Rigidbody 的玩家运动控制器 - 重构版本
    /// 参考 MovingSphere.cs 的设计模式
    /// 直接控制速度而不是使用力，提供更精确和可预测的移动手感
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInput))]
    public class RBPlayerMotor : MonoBehaviour
    {
        [Header("调参配置")]
        [SerializeField] private MovementTuningSO _tuning;
        [SerializeField] private Animator _animator; // Debug临时实现    

        [Header("地面检测")]
        [SerializeField] private LayerMask _groundLayer = -1;
        [SerializeField] private float _probeDistance = 1f;
        [SerializeField] private float _snapProbeDistance = 1f;

        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = false;
        [SerializeField] private bool _showDebugGizmos = false;

        // 组件引用
        private Rigidbody _rb;
        private PlayerInput _playerInput;

        // 运动状态 - 参考 MovingSphere 的设计
        private Vector3 _upAxis = Vector3.up;
        private Vector3 _rightAxis = Vector3.right;
        private Vector3 _forwardAxis = Vector3.forward;
        private Vector3 _velocity;
        private Vector3 _desiredVelocity;
        private Vector3 _gravity;

        // 地面检测状态
        private bool _onGround;
        private bool _onSteep;
        private Vector3 _contactNormal = Vector3.up;
        private Vector3 _groundNormal = Vector3.up;
        private float _minGroundDotProduct;
        private int _groundContactCount;
        private int _stepsSinceLastGrounded;
        private int _stepsSinceLastJump;

        // 跳跃状态
        private bool _desiredJump;
        private int _jumpPhase;
        private int _coyoteCounter;
        private int _jumpBufferCounter;

        // 属性
        public Vector3 Velocity => _velocity;
        public Vector3 UpAxis => _upAxis;
        public bool IsGrounded => _onGround;
        public bool OnSteep => _onSteep;
        public float MoveSpeed => _tuning != null ? _tuning.maxGroundSpeed : 10f;
        public MovementTuningSO Tuning => _tuning;

#region Unity Callbacks
        private void OnValidate()
        {
            // 计算地面检测的最小点积值（参考 MovingSphere）
            if (_tuning != null)
            {
                _minGroundDotProduct = Mathf.Cos(_tuning.maxGroundAngle * Mathf.Deg2Rad);
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _playerInput = GetComponent<PlayerInput>();

            // 配置 Rigidbody - 参考 MovingSphere
            _rb.useGravity = false;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            // 如果没有指定调参配置，尝试加载默认的
            if (_tuning == null)
            {
                _tuning = Resources.Load<MovementTuningSO>("MovementTuning");
                if (_tuning == null)
                {
                    Debug.LogWarning("[RBPlayerMotor] 未找到 MovementTuningSO 配置，请在 Inspector 中指定");
                }
            }

            OnValidate();
        }

        private void Start()
        {
            // 锁定鼠标光标
            _playerInput.SetCursorLock(true);
            // 初始化状态
            ResetState();
        }

        private void Update()
        {
            // 处理输入
            HandleInput();
        }

        private void FixedUpdate()
        {
            if (_tuning == null) return;

            // 参考 MovingSphere 的 FixedUpdate 结构
            _gravity = CustomGravity.GetGravity(_rb.position, out _upAxis);
            UpdateState();
            AdjustVelocity();
            
            if (_desiredJump)
            {
                _desiredJump = false;
                Jump();
            }

            // 应用重力
            _velocity += _gravity * Time.fixedDeltaTime;
            
            // 直接设置速度 - 关键差异点1：不使用 AddForce
            _rb.velocity = _velocity;
            
            ClearState();
            AlignRotation();
        }
#endregion

        /// <summary>
        /// 处理输入 - 参考 MovingSphere 的输入处理
        /// </summary>
        private void HandleInput()
        {
            Vector2 moveInput = _playerInput.MoveInput;
            
            // 计算期望速度
            _desiredVelocity = (_rightAxis * moveInput.x + _forwardAxis * moveInput.y) * MoveSpeed;
            _desiredVelocity = Vector3.ClampMagnitude(_desiredVelocity, MoveSpeed);

            // 更新轴向 - 基于相机方向但投影到重力垂直平面
            UpdateMovementAxes();

            // 跳跃输入处理
            if (_playerInput.JumpPressed)
            {
                _jumpBufferCounter = _tuning.jumpBufferFrames;
            }
            else if (_jumpBufferCounter > 0)
            {
                _jumpBufferCounter--;
            }

            // 执行跳跃
            if (_jumpBufferCounter > 0 && CanJump())
            {
                _desiredJump = true;
                _jumpBufferCounter = 0;
            }
        }

        /// <summary>
        /// 更新运动轴向 - 参考 MovingSphere 的轴向更新
        /// </summary>
        private void UpdateMovementAxes()
        {
            Camera mainCamera = Camera.main;
            Vector3 cameraForward;
            
            if (mainCamera != null)
            {
                cameraForward = ProjectDirectionOnPlane(mainCamera.transform.forward, _upAxis);
            }
            else
            {
                cameraForward = ProjectDirectionOnPlane(Vector3.forward, _upAxis);
            }
            
            _rightAxis = Vector3.Cross(_upAxis, cameraForward).normalized;
            _forwardAxis = Vector3.Cross(_rightAxis, _upAxis).normalized;
        }

        /// <summary>
        /// 更新状态 - 参考 MovingSphere 的 UpdateState
        /// </summary>
        private void UpdateState()
        {
            _stepsSinceLastGrounded += 1;
            _stepsSinceLastJump += 1;
            _velocity = _rb.velocity;

            // 地面检测
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

            // 土狼时间处理
            UpdateCoyoteTime();
        }

        /// <summary>
        /// 地面检测 - 关键差异点2：使用 Raycast 而不是 SphereCast
        /// </summary>
        private bool CheckGround()
        {
            bool wasOnGround = _onGround;
            _onGround = false;
            _groundContactCount = 0;
            _contactNormal = Vector3.zero;

            // 使用 Raycast 进行精确地面检测
            if (Physics.Raycast(
                _rb.position, -_upAxis, out RaycastHit hit,
                _probeDistance, _groundLayer, QueryTriggerInteraction.Ignore
            ))
            {
                float upDot = Vector3.Dot(_upAxis, hit.normal);
                if (upDot >= _minGroundDotProduct)
                {
                    _onGround = true;
                    _groundContactCount = 1;
                    _contactNormal = hit.normal;
                    _groundNormal = hit.normal;

                    // 检查坡度
                    float angle = Vector3.Angle(_upAxis, _groundNormal);
                    _onSteep = angle > _tuning.maxGroundAngle;

                    // 如果在可行走的地面上且向下移动，停止垂直运动
                    if (!_onSteep && Vector3.Dot(_velocity, _upAxis) <= 0f)
                    {
                        float verticalComponent = Vector3.Dot(_velocity, _upAxis);
                        _velocity -= _upAxis * verticalComponent;
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
        /// 贴地检测 - 参考 MovingSphere 的 SnapToGround
        /// </summary>
        private bool SnapToGround()
        {
            if (_stepsSinceLastGrounded > 1 || _stepsSinceLastJump <= 2)
            {
                return false;
            }

            float speed = _velocity.magnitude;
            if (speed > _tuning.maxSnapSpeed)
            {
                return false;
            }

            if (!Physics.Raycast(
                _rb.position, -_upAxis, out RaycastHit hit,
                _snapProbeDistance, _groundLayer, QueryTriggerInteraction.Ignore
            ))
            {
                return false;
            }

            float upDot = Vector3.Dot(_upAxis, hit.normal);
            if (upDot < _minGroundDotProduct)
            {
                return false;
            }

            _onGround = true;
            _groundContactCount = 1;
            _contactNormal = hit.normal;
            _groundNormal = hit.normal;

            // 重新投影速度到地面
            float dot = Vector3.Dot(_velocity, hit.normal);
            if (dot > 0f)
            {
                _velocity = (_velocity - hit.normal * dot).normalized * speed;
            }

            return true;
        }

        /// <summary>
        /// 土狼时间更新
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
        /// 调整速度 - 关键差异点3：直接调整速度而不是使用力
        /// 参考 MovingSphere 的 AdjustVelocity
        /// </summary>
        private void AdjustVelocity()
        {
            Vector2 moveInput = _playerInput.MoveInput;
            
            // 选择加速度和最大速度
            float acceleration = _onGround ? _tuning.maxGroundAcceleration : _tuning.maxAirAcceleration;
            float speed = _onGround ? _tuning.maxGroundSpeed : _tuning.maxAirSpeed;

            // 将轴向投影到接触平面
            Vector3 xAxis = ProjectDirectionOnPlane(_rightAxis, _contactNormal);
            Vector3 zAxis = ProjectDirectionOnPlane(_forwardAxis, _contactNormal);

            // 计算当前速度在各轴向的分量
            float currentX = Vector3.Dot(_velocity, xAxis);
            float currentZ = Vector3.Dot(_velocity, zAxis);

            // 计算期望速度
            float desiredX = moveInput.x * speed;
            float desiredZ = moveInput.y * speed;

            // 应用刹车
            if (moveInput.magnitude < 0.01f)
            {
                acceleration *= _tuning.brakeMultiplier;
            }

            // 计算本帧最大速度变化
            float maxSpeedChange = acceleration * Time.fixedDeltaTime;

            // 调整速度
            float newX = Mathf.MoveTowards(currentX, desiredX, maxSpeedChange);
            float newZ = Mathf.MoveTowards(currentZ, desiredZ, maxSpeedChange);

            _velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);

            // 动画更新
            if (_showDebugInfo && _animator != null)
            {
                float velocityForward = Vector3.Dot(_velocity, _forwardAxis);
                float velocityStrafe = -Vector3.Dot(_velocity, _rightAxis);
                _animator.SetFloat("velocityForward", velocityForward);
                _animator.SetFloat("velocityStrafe", velocityStrafe);
            }
        }

        /// <summary>
        /// 检查是否可以跳跃
        /// </summary>
        private bool CanJump()
        {
            return (_onGround && !_onSteep) || _coyoteCounter > 0;
        }

        /// <summary>
        /// 执行跳跃 - 关键差异点4：向重力反方向跳跃
        /// 参考 MovingSphere 的 Jump 方法
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
            
            // 计算跳跃速度 - 基于重力计算
            float jumpSpeed = Mathf.Sqrt(2f * _gravity.magnitude * _tuning.jumpHeight);
            
            // 标准化跳跃方向（向上 + 地面法线的组合）
            jumpDirection = (jumpDirection + _upAxis).normalized;
            
            // 检查当前在跳跃方向的速度分量
            float alignedSpeed = Vector3.Dot(_velocity, jumpDirection);
            if (alignedSpeed > 0f)
            {
                jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
            }
            
            _velocity += jumpDirection * jumpSpeed;
            _onGround = false;
            _coyoteCounter = 0;

            if (_showDebugInfo)
                Debug.Log("[RBPlayerMotor] Player jumped");
        }

        /// <summary>
        /// 清除状态 - 参考 MovingSphere
        /// </summary>
        private void ClearState()
        {
            _groundContactCount = 0;
            _contactNormal = Vector3.zero;
        }

        /// <summary>
        /// 对齐角色旋转到重力方向
        /// 关键差异点5：改进的旋转逻辑
        /// </summary>
        private void AlignRotation()
        {
            Vector3 characterForward = ProjectDirectionOnPlane(transform.forward, _upAxis);
            if (characterForward == Vector3.zero)
                characterForward = _forwardAxis;

            Quaternion targetRotation = Quaternion.LookRotation(characterForward, _upAxis);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                _tuning.turnResponsiveness * Time.fixedDeltaTime
            );
        }

        /// <summary>
        /// 将方向投影到平面上 - 参考 MovingSphere
        /// </summary>
        private Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal)
        {
            return (direction - normal * Vector3.Dot(direction, normal)).normalized;
        }

        /// <summary>
        /// 添加冲量
        /// </summary>
        public void AddImpulse(Vector3 impulse)
        {
            _velocity += impulse;
        }

        /// <summary>
        /// 设置调参配置
        /// </summary>
        public void SetTuning(MovementTuningSO tuning)
        {
            _tuning = tuning;
            OnValidate();
        }

        /// <summary>
        /// 重置玩家状态
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
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showDebugGizmos) return;

            // 绘制轴向
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + _upAxis * 2f);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + _rightAxis * 2f);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + _forwardAxis * 2f);

            // 绘制地面法线 (当在地面上时)
            if (_onGround && _groundNormal != Vector3.zero)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(transform.position, transform.position + _groundNormal * 1.8f);
            }

            // 绘制速度
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + _velocity);

            // 绘制地面检测
            Gizmos.color = _onGround ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, transform.position - _upAxis * _probeDistance);

            // 绘制贴地检测
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position - _upAxis * _snapProbeDistance);

            // 绘制重力
            if (_gravity != Vector3.zero)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, transform.position + _gravity.normalized * 1.5f);
            }
        }

        /// <summary>
        /// 在编辑器中显示调试信息
        /// </summary>
        private void OnGUI()
        {
            if (!_showDebugInfo || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 600));
            GUILayout.BeginVertical("box");
            
            // Title
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUILayout.Label("Player Control Debug (Refactored)", titleStyle);
            GUILayout.Space(5);

            // Position and Movement
            GUILayout.Label("● Position & Movement", titleStyle);
            GUILayout.Label($"Position: {transform.position:F2}");
            GUILayout.Label($"Velocity: {_velocity:F2}");
            GUILayout.Label($"Speed: {_velocity.magnitude:F2} m/s");
            GUILayout.Space(5);
              
            // Ground State
            GUILayout.Label("● Ground State", titleStyle);
            GUILayout.Label($"On Ground: {_onGround}");
            GUILayout.Label($"On Steep: {_onSteep}");
            GUILayout.Label($"Contact Normal: {_contactNormal:F2}");
            GUILayout.Label($"Ground Contacts: {_groundContactCount}");
            GUILayout.Space(5);
            
            // Jump State
            GUILayout.Label("● Jump State", titleStyle);
            GUILayout.Label($"Jump Phase: {_jumpPhase}");
            GUILayout.Label($"Coyote Counter: {_coyoteCounter}");
            GUILayout.Label($"Jump Buffer: {_jumpBufferCounter}");
            GUILayout.Label($"Steps Since Ground: {_stepsSinceLastGrounded}");
            GUILayout.Label($"Steps Since Jump: {_stepsSinceLastJump}");
            GUILayout.Space(5);
            
            // Orientation
            GUILayout.Label("● Orientation", titleStyle);
            GUILayout.Label($"Up Axis: {_upAxis:F2}");
            GUILayout.Label($"Forward Axis: {_forwardAxis:F2}");
            GUILayout.Label($"Right Axis: {_rightAxis:F2}");
            GUILayout.Space(5);
            
            // Gravity
            GUILayout.Label("● Gravity", titleStyle);
            GUILayout.Label($"Direction: {_gravity.normalized:F2}");
            GUILayout.Label($"Magnitude: {_gravity.magnitude:F2} m/s²");

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
