using UnityEngine;

namespace DWHITE
{
    /// <summary>
    /// 玩家动画控制器 - 基于事件驱动的动画系统
    /// 订阅PlayerStateManager的状态变化，独立管理角色动画
    /// 基于最小可行原则，专注核心动画功能
    /// </summary>
    [RequireComponent(typeof(PlayerStateManager))]
    public class PlayerAnimationController : MonoBehaviour
    {
        
        #region Dependencies

        [SerializeField] private Animator _animator;
        [SerializeField] private PlayerStateManager _stateManager;

        #endregion

        #region Configuration

        [Header("动画配置")]
        [Tooltip("动画参数的平滑过渡速度")]
        [SerializeField] private float _animationSmoothing = 10f;
        [Tooltip("是否启用调试信息显示")]
        [SerializeField] private bool _showDebugInfo = false;

        [Header("动画参数名称")]
        [Tooltip("速度参数名")]
        [SerializeField] private string _velocityParam = "velocity";
        [Tooltip("前后移动速度参数名")]
        [SerializeField] private string _velocityForwardParam = "velocityForward";
        [Tooltip("左右移动速度参数名")]
        [SerializeField] private string _velocityStrafeParam = "velocityStrafe";
        [Tooltip("冲刺状态参数名")]
        [SerializeField] private string _isSprintingParam = "isSprinting";
        [Tooltip("接地状态参数名")]
        [SerializeField] private string _isGroundedParam = "isGrounded";
        [Tooltip("空中状态参数名")]
        [SerializeField] private string _isInAirParam = "isInAir";
        [Tooltip("跳跃触发器参数名")]
        [SerializeField] private string _triggerJumpParam = "triggerJump";
        [Tooltip("着地触发器参数名")]
        [SerializeField] private string _triggerLandParam = "triggerLand";

        #endregion

        #region Animation State

        // 当前动画参数值（用于平滑过渡）
        private float _currentVelocity;
        private float _currentVelocityForward;
        private float _currentVelocityStrafe;
        private bool _currentIsGrounded;
        private bool _currentIsSprinting;
        private bool _currentIsInAir;

        // 目标动画参数值
        private float _targetVelocity;
        private float _targetVelocityForward;
        private float _targetVelocityStrafe;

        // 跳跃状态追踪（用于触发器）
        private bool _wasGroundedLastFrame;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // 获取组件引用
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }
            _stateManager = GetComponent<PlayerStateManager>();
        }

        private void OnEnable()
        {
            // 订阅状态变化事件
            PlayerStateManager.OnMovementChanged += HandleMovementChanged;
            PlayerStateManager.OnGroundStateChanged += HandleGroundStateChanged;
            PlayerStateManager.OnSprintStateChanged += HandleSprintStateChanged;
        }

        private void OnDisable()
        {
            // 取消订阅状态变化事件
            PlayerStateManager.OnMovementChanged -= HandleMovementChanged;
            PlayerStateManager.OnGroundStateChanged -= HandleGroundStateChanged;
            PlayerStateManager.OnSprintStateChanged -= HandleSprintStateChanged;
        }

        private void Start()
        {
            // 初始化动画状态
            InitializeAnimationState();
        }

        private void Update()
        {
            // 平滑更新动画参数
            UpdateAnimationParameters();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 处理运动状态变化
        /// </summary>
        private void HandleMovementChanged(PlayerStateChangedEventArgs args)
        {
            var state = args.CurrentState;

            // 更新速度参数
            _targetVelocity = state.velocity.magnitude;
            // 计算相对于角色坐标系的速度分量
            _targetVelocityForward = Vector3.Dot(state.velocity, state.forwardAxis);
            _targetVelocityStrafe = Vector3.Dot(state.velocity, state.rightAxis);
        }

        /// <summary>
        /// 处理地面状态变化
        /// </summary>
        private void HandleGroundStateChanged(PlayerStateChangedEventArgs args)
        {
            bool wasGrounded = _currentIsGrounded;
            _currentIsGrounded = args.CurrentState.isGrounded;
            _currentIsInAir = !_currentIsGrounded;

            // 立即更新地面和空中状态（不需要平滑过渡）
            if (_animator != null)
            {
                _animator.SetBool(_isGroundedParam, _currentIsGrounded);
                _animator.SetBool(_isInAirParam, _currentIsInAir);

                // 检测跳跃和着地状态变化，触发相应的触发器
                if (!wasGrounded && _currentIsGrounded)
                {
                    // 从空中到地面 = 着地
                    _animator.SetTrigger(_triggerLandParam);
                }
                else if (wasGrounded && !_currentIsGrounded)
                {
                    // 从地面到空中 = 跳跃（或掉落）
                    // 检查是否是主动跳跃（向上速度）
                    var state = args.CurrentState;
                    float upwardVelocity = Vector3.Dot(state.velocity, state.upAxis);
                    if (upwardVelocity > 0.1f) // 有向上速度，认为是跳跃
                    {
                        _animator.SetTrigger(_triggerJumpParam);
                    }
                }
            }
        }

        /// <summary>
        /// 处理冲刺状态变化
        /// </summary>
        private void HandleSprintStateChanged(PlayerStateChangedEventArgs args)
        {
            _currentIsSprinting = args.CurrentState.isSprinting;
            
            // 立即更新冲刺状态（不需要平滑过渡）
            if (_animator != null)
            {
                _animator.SetBool(_isSprintingParam, _currentIsSprinting);
            }
        }

        #endregion

        #region Animation Updates        

        /// <summary>
        /// 初始化动画状态
        /// </summary>
        private void InitializeAnimationState()
        {
            if (_stateManager != null)
            {
                var currentState = _stateManager.GetStateSnapshot();

                // 初始化所有动画参数
                _targetVelocity = currentState.velocity.magnitude;
                _targetVelocityForward = Vector3.Dot(currentState.velocity, currentState.forwardAxis);
                _targetVelocityStrafe = Vector3.Dot(currentState.velocity, currentState.rightAxis);
                _currentVelocity = _targetVelocity;
                _currentVelocityForward = _targetVelocityForward;
                _currentVelocityStrafe = _targetVelocityStrafe;
                _currentIsGrounded = currentState.isGrounded;
                _currentIsSprinting = currentState.isSprinting;
                _currentIsInAir = !currentState.isGrounded;

                // 初始化跳跃状态追踪
                _wasGroundedLastFrame = currentState.isGrounded;

                // 立即设置动画参数
                if (_animator != null)
                {
                    _animator.SetFloat(_velocityParam, _currentVelocity);
                    _animator.SetFloat(_velocityForwardParam, _currentVelocityForward);
                    _animator.SetFloat(_velocityStrafeParam, _currentVelocityStrafe);
                    _animator.SetBool(_isGroundedParam, _currentIsGrounded);
                    _animator.SetBool(_isSprintingParam, _currentIsSprinting);
                    _animator.SetBool(_isInAirParam, _currentIsInAir);
                }
            }
        }

        /// <summary>
        /// 平滑更新动画参数
        /// </summary>
        private void UpdateAnimationParameters()
        {
            if (_animator == null) return;

            // 平滑过渡速度参数
            _currentVelocity = Mathf.Lerp(_currentVelocity, _targetVelocity, _animationSmoothing * Time.deltaTime);
            _currentVelocityForward = Mathf.Lerp(_currentVelocityForward, _targetVelocityForward, _animationSmoothing * Time.deltaTime);
            _currentVelocityStrafe = Mathf.Lerp(_currentVelocityStrafe, _targetVelocityStrafe, _animationSmoothing * Time.deltaTime);

            // 更新Animator参数
            _animator.SetFloat(_velocityParam, _currentVelocity);
            _animator.SetFloat(_velocityForwardParam, _currentVelocityForward);
            _animator.SetFloat(_velocityStrafeParam, _currentVelocityStrafe);
        }

        #endregion

        #region Public API

        /// <summary>
        /// 手动设置动画参数（用于特殊情况）
        /// </summary>
        public void SetAnimationParameter(string paramName, float value)
        {
            if (_animator != null)
            {
                _animator.SetFloat(paramName, value);
            }
        }

        /// <summary>
        /// 手动设置动画参数（用于特殊情况）
        /// </summary>
        public void SetAnimationParameter(string paramName, bool value)
        {
            if (_animator != null)
            {
                _animator.SetBool(paramName, value);
            }
        }

        /// <summary>
        /// 触发动画触发器
        /// </summary>
        public void TriggerAnimation(string triggerName)
        {
            if (_animator != null)
            {
                _animator.SetTrigger(triggerName);
            }
        }

        /// <summary>
        /// 手动触发跳跃动画（供PlayerMotor等系统调用）
        /// </summary>
        public void TriggerJump()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(_triggerJumpParam);
            }
        }

        /// <summary>
        /// 手动触发着地动画（供其他系统调用）
        /// </summary>
        public void TriggerLand()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(_triggerLandParam);
            }
        }

        /// <summary>
        /// 手动设置空中状态（用于特殊情况）
        /// </summary>
        public void SetInAirState(bool isInAir)
        {
            _currentIsInAir = isInAir;
            if (_animator != null)
            {
                _animator.SetBool(_isInAirParam, isInAir);
            }
        }

        /// <summary>
        /// 获取当前动画状态信息
        /// </summary>
        public AnimatorStateInfo GetCurrentAnimationState(int layerIndex = 0)        {
            return _animator != null ? _animator.GetCurrentAnimatorStateInfo(layerIndex) : default;
        }

        #endregion

        #region Debug

        private void OnGUI()
        {
            if (!_showDebugInfo || _animator == null) return;

            GUILayout.BeginArea(new Rect(320, 200, 300, 350));
            GUILayout.Label("=== Animation Controller ===");
            GUILayout.Label($"Velocity: {_currentVelocity:F2} → {_targetVelocity:F2}");
            GUILayout.Label($"Velocity Forward: {_currentVelocityForward:F2} → {_targetVelocityForward:F2}");
            GUILayout.Label($"Velocity Strafe: {_currentVelocityStrafe:F2} → {_targetVelocityStrafe:F2}");
            GUILayout.Label($"Is Grounded: {_currentIsGrounded}");
            GUILayout.Label($"Is In Air: {_currentIsInAir}");
            GUILayout.Label($"Is Sprinting: {_currentIsSprinting}");
            
            if (_animator != null)
            {
                var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                GUILayout.Label($"Current State: {stateInfo.shortNameHash}");
                GUILayout.Label($"State Time: {stateInfo.normalizedTime:F2}");
                
                // 显示当前触发器状态（如果需要）
                GUILayout.Label("--- Animator Parameters ---");
                GUILayout.Label($"velocityForward: {_animator.GetFloat(_velocityForwardParam):F2}");
                GUILayout.Label($"velocityStrafe: {_animator.GetFloat(_velocityStrafeParam):F2}");
                GUILayout.Label($"isGrounded: {_animator.GetBool(_isGroundedParam)}");
                GUILayout.Label($"isInAir: {_animator.GetBool(_isInAirParam)}");
                GUILayout.Label($"isSprinting: {_animator.GetBool(_isSprintingParam)}");
            }
            GUILayout.EndArea();
        }

        #endregion
    }
}
