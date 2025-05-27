using UnityEngine;

/// <summary>
/// 基于 Rigidbody 的玩家运动控制器
/// 完全整合 PhysX，舍弃 CharacterController
/// 通过直接写入速度并叠加自定义力来驱动
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
    [SerializeField] private float _groundCheckRadius = 0.3f;
    [SerializeField] private float _groundCheckDistance = 0.1f;
    [SerializeField] private float _groundSnapOffset = 0.02f; // 微小的离地偏移，防止穿模

    [Header("调试")]
    [SerializeField] private bool _showDebugInfo = false;
    [SerializeField] private bool _showDebugGizmos = false;

    // 组件引用
    private Rigidbody _rb;
    private PlayerInput _playerInput;

    // 运动状态
    private Vector3 _upAxis = Vector3.up;
    private Vector3 _rightAxis = Vector3.right;
    private Vector3 _forwardAxis = Vector3.forward;
    private Vector3 _gravityDirection = Vector3.down;
    float _gravityMagnitude = 9.81f;

    private bool _onGround;
    private bool _onSteep;
    private Vector3 _groundNormal = Vector3.up;
    private float _verticalComponent;

    // 跳跃状态
    private bool _jumped;
    private bool _performJump;
    private int _coyoteCounter;
    private int _jumpBufferCounter;    // 属性
    public Vector3 Velocity => _rb.velocity;
    public Vector3 UpAxis => _upAxis;
    public bool IsGrounded => _onGround;
    public bool OnSteep => _onSteep;
    public float MoveSpeed => _tuning.maxGroundSpeed;
    public MovementTuningSO Tuning => _tuning;

#region Unity Callbacks
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _playerInput = GetComponent<PlayerInput>();

        // 配置 Rigidbody
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
        UpdateJump();
    }

    private void FixedUpdate()
    {
        if (_tuning == null) return;
        if (_performJump) PerformJump();
        UpdateGravity();
        UpdateGravityAlignment();
        UpdateGroundCheck();
        UpdateMovement();
        ApplyGravity();
        LimitFallSpeed();
        
        // 对齐角色旋转到重力方向
        AlignRotation();
    }
#endregion

    /// <summary>
    /// 更新重力
    /// </summary>
    private void UpdateGravity()
    {
        // 获取自定义重力方向和大小
        Vector3 gravity = CustomGravity.GetGravity(transform.position, out Vector3 newUpAxisFromCustomGravity);

        // 更新重力方向和上轴
        _gravityDirection = gravity.normalized;
        _gravityMagnitude = gravity.magnitude;

        Vector3 previousUpAxis = _upAxis; // 记录之前的上轴方向
        Vector3 targetUpAxis = newUpAxisFromCustomGravity;

        // 当在地面且非陡坡时，优先使用实际的地面法线作为目标"上"方向
        if (_onGround && !_onSteep && _groundNormal != Vector3.zero)
        {
            targetUpAxis = _groundNormal;
        }

        // 平滑更新轴向
        if (_tuning != null && _tuning.turnResponsiveness > 0f)
        {
            _upAxis = Vector3.Slerp(_upAxis, targetUpAxis, _tuning.turnResponsiveness * Time.fixedDeltaTime).normalized;
        }
        else if (_tuning != null)
        {
            _upAxis = targetUpAxis; // 如果 responsiveness 不是正数，则立即吸附到 targetUpAxis
        }
        // 如果 _tuning 为 null，_upAxis 保持不变

        // 检查重力方向是否发生显著变化
        float upAxisChange = Vector3.Dot(previousUpAxis, _upAxis);
        if (upAxisChange < 1f) // 如果上轴发生变化
        {
            Debug.Log("[RBPlayerMotor] 重力方向发生变化，重新对齐运动轴向");
            // 重新投影现有速度到新的重力垂直平面
            Vector3 currentVelocity = _rb.velocity;
            Vector3 newHorizontalVelocity = Vector3.ProjectOnPlane(currentVelocity, _upAxis);
            float newVerticalComponent = Vector3.Dot(currentVelocity, _upAxis);

            // 更新速度以保持在新的重力垂直平面内
            _rb.velocity = newHorizontalVelocity + _upAxis * newVerticalComponent;
        }

        // 计算垂直分量（相对于重力方向）
        _verticalComponent = Vector3.Dot(_rb.velocity, _upAxis);

        // 调试绘制
        if (_showDebugGizmos)
        {
            Debug.DrawLine(transform.position, transform.position + _gravityDirection * _gravityMagnitude, Color.red);
        }
    }

    /// <summary>
    /// 更新重力对齐
    /// </summary>
    private void UpdateGravityAlignment()
    {
        // 重新计算右轴和前轴，保持相机的前向作为参考
        Camera mainCamera = Camera.main;
        Vector3 cameraForward;
        
        if (mainCamera != null)
        {
            cameraForward = Vector3.ProjectOnPlane(mainCamera.transform.forward, _upAxis).normalized;
        }
        else
        {
            cameraForward = Vector3.zero;
        }
        
        if (cameraForward == Vector3.zero)
            cameraForward = Vector3.ProjectOnPlane(Vector3.forward, _upAxis).normalized;

        _rightAxis = Vector3.Cross(_upAxis, cameraForward).normalized;
        _forwardAxis = Vector3.Cross(_rightAxis, _upAxis).normalized;
    }

    /// <summary>
    /// 地面检测
    /// </summary>
    private void UpdateGroundCheck()
    {
        Vector3 position = transform.position;
        float checkDistance = _groundCheckDistance + _groundCheckRadius;

        // 球形射线检测
        bool wasOnGround = _onGround;
        _onGround = Physics.SphereCast(
            position + _upAxis * _groundCheckRadius,
            _groundCheckRadius,
            -_upAxis,
            out RaycastHit hit,
            checkDistance,
            _groundLayer
        );

        if (_onGround)
        {
            _groundNormal = hit.normal;

            // 检查坡度
            float angle = Vector3.Angle(_upAxis, _groundNormal);
            _onSteep = angle > _tuning.maxGroundAngle;            // 如果在可行走的地面上且向下移动，停止垂直运动
            if (!_onSteep && Vector3.Dot(_rb.velocity, _upAxis) <= 0f)
            {
                _verticalComponent = 0f;
                _jumped = false;
                
                // 立即更新速度以停止下沉
                Vector3 horizontalVel = Vector3.ProjectOnPlane(_rb.velocity, _upAxis);
                _rb.velocity = horizontalVel;
            }
        }
        else
        {
            _groundNormal = _upAxis;
            _onSteep = false;
        }

        // 土狼时间处理
        if (wasOnGround && !_onGround)
        {
            _coyoteCounter = _tuning.coyoteFrames;
        }
        else if (_onGround)
        {
            _coyoteCounter = 0;
        }
        else if (_coyoteCounter > 0)
        {
            _coyoteCounter--;
        }
    }    /// <summary>
    /// 贴地检测
    /// </summary>
    private void SnapToGround()
    {
        if (_jumped || _onGround) return;

        // 检查下落速度是否过快
        if (_tuning == null || -Vector3.Dot(_rb.velocity, _upAxis) > _tuning.maxSnapSpeed) return;

        Vector3 position = transform.position;
        if (Physics.Raycast(position, -_upAxis, out RaycastHit hit, _tuning.snapProbeDistance, _groundLayer))
        {
            float angle = Vector3.Angle(_upAxis, hit.normal);
            if (angle <= _tuning.maxGroundAngle)
            {
                // 贴地: 将角色脚底移动到碰撞点上方一个微小偏移处
                transform.position = hit.point + _upAxis * _groundSnapOffset;
                _onGround = true;
                _groundNormal = hit.normal;
                _verticalComponent = 0f;
                
                // 立即更新 Rigidbody 速度以反映这一点
                Vector3 horizontalVel = Vector3.ProjectOnPlane(_rb.velocity, _upAxis);
                _rb.velocity = horizontalVel; // 停止任何下沉速度
            }
        }
    }    /// <summary>
    /// 更新移动
    /// </summary>
    private void UpdateMovement()
    {
        Vector2 moveInput = _playerInput.MoveInput;

        // 计算期望移动方向
        Vector3 wishDirection = (_rightAxis * moveInput.x + _forwardAxis * moveInput.y).normalized;

        // 选择最大速度和加速度曲线
        float maxSpeed = _onGround && !_onSteep ? _tuning.maxGroundSpeed : _tuning.maxAirSpeed;
        AnimationCurve accelCurve = _onGround && !_onSteep ? _tuning.groundAcceleration : _tuning.airAcceleration;

        // 计算期望速度
        Vector3 wishVelocity = wishDirection * maxSpeed;

        // 获取当前水平面速度 - 确保始终垂直于当前 _upAxis
        Vector3 currentVelocity = _rb.velocity;
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(currentVelocity, _upAxis);

        // 计算加速度
        float currentSpeedRatio = horizontalVelocity.magnitude / maxSpeed;
        float acceleration = accelCurve.Evaluate(Mathf.Clamp01(currentSpeedRatio));

        // 如果没有输入，应用刹车
        if (moveInput.magnitude < 0.01f)
        {
            acceleration *= _tuning.brakeMultiplier;
        }

        // 计算新的水平速度
        Vector3 newHorizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            wishVelocity,
            acceleration * Time.fixedDeltaTime
        );

        // 确保新的水平速度严格垂直于当前 _upAxis（重力垂直约束）
        newHorizontalVelocity = Vector3.ProjectOnPlane(newHorizontalVelocity, _upAxis);

        // 临时放动画
        if (_showDebugInfo)
        {
            float velocityForward = Vector3.Dot(newHorizontalVelocity, _forwardAxis);
            float velocityStrafe = -Vector3.Dot(newHorizontalVelocity, _rightAxis);
            _animator.SetFloat("velocityForward", velocityForward);
            _animator.SetFloat("velocityStrafe", velocityStrafe);
        }

        // 应用新速度，保持垂直分量，确保运动始终在重力垂直平面内
        _rb.velocity = newHorizontalVelocity + _upAxis * _verticalComponent;

        // 尝试贴地
        if (!_jumped)
        {
            SnapToGround();
        }
    }

    /// <summary>
    /// 更新跳跃
    /// </summary>
    private void UpdateJump()
    {
        // 跳跃缓冲处理
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
            PerformJump();
            _jumpBufferCounter = 0;
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
    /// 执行跳跃
    /// </summary>
    private void PerformJump()
    {
        if (_showDebugInfo)
            Debug.Log("[RBPlayerMotor] Player jumped");

        _performJump = false;

        _verticalComponent = _tuning.jumpSpeed;
        _jumped = true;
        _onGround = false;
        _coyoteCounter = 0;

        // 立即应用跳跃速度
        Vector3 horizontalVel = Vector3.ProjectOnPlane(_rb.velocity, _upAxis);
        _rb.velocity = horizontalVel + _upAxis * _verticalComponent;
    }

    /// <summary>
    /// 应用重力
    /// </summary>
    private void ApplyGravity()
    {
        // 如果在可行走的地面上，不应用重力
        if (_onGround && !_onSteep) return;

        Vector3 gravity = CustomGravity.GetGravity(transform.position, out Vector3 newUpAxis);

        // 应用重力倍率
        gravity *= _tuning.gravityMultiplier;

        // 计算重力加速度（相对于当前上轴方向）
        float gravityAccel = Vector3.Dot(gravity, _upAxis);
        _verticalComponent += gravityAccel * Time.fixedDeltaTime;

        // 更新 Rigidbody 速度
        Vector3 horizontalVel = Vector3.ProjectOnPlane(_rb.velocity, _upAxis);
        _rb.velocity = horizontalVel + _upAxis * _verticalComponent;
    }

    /// <summary>
    /// 限制最大下落速度
    /// </summary>
    private void LimitFallSpeed()
    {
        if (_verticalComponent < -_tuning.maxFallSpeed)
        {
            _verticalComponent = -_tuning.maxFallSpeed;
            Vector3 horizontalVel = Vector3.ProjectOnPlane(_rb.velocity, _upAxis);
            _rb.velocity = horizontalVel + _upAxis * _verticalComponent;
        }
    }

    /// <summary>
    /// 对齐角色旋转到重力方向
    /// </summary>
    private void AlignRotation()
    {
        Vector3 characterForward = Vector3.ProjectOnPlane(transform.forward, _upAxis).normalized;
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
    /// 添加冲量
    /// </summary>
    public void AddImpulse(Vector3 impulse)
    {
        _rb.AddForce(impulse, ForceMode.VelocityChange);
    }

    /// <summary>
    /// 设置调参配置
    /// </summary>
    public void SetTuning(MovementTuningSO tuning)
    {
        _tuning = tuning;
    }

    /// <summary>
    /// 重置玩家状态
    /// </summary>
    public void ResetState()
    {
        _rb.velocity = Vector3.zero;
        _verticalComponent = 0f;
        _jumped = false;
        _coyoteCounter = 0;
        _jumpBufferCounter = 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (!_showDebugGizmos) return;        // 绘制轴向
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
        if (_rb != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + _rb.velocity);
        }

        // 绘制地面检测
        Gizmos.color = _onGround ? Color.green : Color.red;
        Vector3 checkPos = transform.position + _upAxis * _groundCheckRadius;
        Gizmos.DrawWireSphere(checkPos, _groundCheckRadius);
        Gizmos.DrawLine(checkPos, checkPos - _upAxis * (_groundCheckDistance + _groundCheckRadius));

        // 绘制贴地检测
        if (_tuning != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position - _upAxis * _tuning.snapProbeDistance);
        }
    }    private void OnValidate()
    {
        _groundCheckRadius = Mathf.Max(0.01f, _groundCheckRadius);
        _groundSnapOffset = Mathf.Max(0.001f, _groundSnapOffset);
        _groundCheckDistance = Mathf.Max(0.01f, _groundCheckDistance);
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
        GUILayout.Label("Player Control Debug", titleStyle);
        GUILayout.Space(5);

        // Position and Movement
        GUILayout.Label("● Position & Movement", titleStyle);
        GUILayout.Label($"Position: {transform.position:F2}");
        GUILayout.Label($"Velocity: {_rb.velocity:F2}");
        GUILayout.Label($"Speed: {_rb.velocity.magnitude:F2} m/s");
        GUILayout.Space(5);
          // Ground State
        GUILayout.Label("● Ground State", titleStyle);
        GUILayout.Label($"On Ground: {_onGround}");
        GUILayout.Label($"On Steep: {_onSteep}");
        GUILayout.Label($"Ground Normal: {_groundNormal:F2}");
        GUILayout.Label($"Vertical Component: {_verticalComponent:F2}");
        GUILayout.Space(5);
        
        // Jump State
        GUILayout.Label("● Jump State", titleStyle);
        GUILayout.Label($"Jumped: {_jumped}");
        GUILayout.Label($"Coyote Counter: {_coyoteCounter}");
        GUILayout.Label($"Jump Buffer Counter: {_jumpBufferCounter}");
        GUILayout.Space(5);
        
        // Orientation
        GUILayout.Label("● Orientation", titleStyle);
        GUILayout.Label($"Up Axis: {_upAxis:F2}");
        GUILayout.Label($"Forward Axis: {_forwardAxis:F2}");
        GUILayout.Label($"Right Axis: {_rightAxis:F2}");
        GUILayout.Space(5);
        
        // Gravity
        GUILayout.Label("● Gravity", titleStyle);
        GUILayout.Label($"Direction: {_gravityDirection:F2}");
        GUILayout.Label($"Magnitude: {_gravityMagnitude:F2} m/s²");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
