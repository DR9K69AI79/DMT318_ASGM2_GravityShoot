using UnityEngine;
using DWHITE.Weapons;

namespace DWHITE
{
    /// <summary>
    /// 玩家动画控制器 - 基于事件驱动的动画系统
    /// 订阅PlayerStatusManager的状态变化，独立管理角色动画
    /// 基于最小可行原则，专注核心动画功能
    /// </summary>    
    public class PlayerAnimationController : MonoBehaviour
    {
        #region Dependencies
        [SerializeField] private GameObject _playerRoot;
        
        private Animator _animator;
        private PlayerStatusManager _statusManager;
        private NetworkPlayerController _networkPlayerController;
        private PlayerWeaponController _weaponController;
        private bool _isNetworkPlayer;

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

        [Header("武器动画层配置")]
        [Tooltip("武器动画层索引")]
        [SerializeField] private int _weaponAnimationLayer = 1;
        [Tooltip("武器动画触发模式")]
        [SerializeField] private bool _useDirectAnimationNames = true;

        [Tooltip("武器动画层参数名")]
        [SerializeField] private string _hasWeaponParam = "hasWeapon";

        #endregion

        #region Animation State
        // 武器动画层权重
        private float _currentWeaponAnimationLayerWeight;

        // 当前动画参数值（用于平滑过渡）
        private float _currentVelocity;
        private float _currentVelocityForward;
        private float _currentVelocityStrafe;
        private bool _currentIsGrounded;
        private bool _currentIsSprinting;
        private bool _currentIsInAir;
        private bool _currentHasWeapon;

        // 目标动画参数值
        private float _targetVelocity;
        private float _targetVelocityForward;
        private float _targetVelocityStrafe;
        private bool _wasGroundedLastFrame;

        #endregion

        #region Unity Lifecycle        
        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _statusManager = _playerRoot.GetComponent<PlayerStatusManager>();
            _networkPlayerController = _playerRoot.GetComponent<NetworkPlayerController>();
            _weaponController = _playerRoot.GetComponent<PlayerWeaponController>();
            _isNetworkPlayer = _networkPlayerController != null;
        }        private void OnEnable()
        {
            // 事件订阅将在Start中进行，确保所有组件都已初始化
        }        private void OnDisable()
        {
            // 取消订阅当前玩家的状态变化事件
            UnsubscribeFromEvents();
        }

        private void Start()
        {
            // 初始化动画状态
            InitializeAnimationState();
            
            // 订阅事件（在Start中确保所有组件都已初始化）
            SubscribeToEvents();
        }

        private void Update()
        {
            // 对于远程网络玩家，直接从NetworkPlayerController获取状态
            if (_isNetworkPlayer && _networkPlayerController != null && !_networkPlayerController.IsLocalPlayer)
            {
                UpdateRemotePlayerAnimation();
            }
            
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
        }        /// <summary>
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

        /// <summary>
        /// 处理武器动画触发
        /// </summary>
        private void HandleWeaponAnimationTriggered(string animationName)
        {
            if (_animator == null || string.IsNullOrEmpty(animationName)) return;

            if (_useDirectAnimationNames)
            {
                // 直接播放指定名称的动画
                PlayWeaponAnimation(animationName);
            }
            else
            {
                // 通过触发器参数播放动画
                _animator.SetTrigger(animationName);
            }            if (_showDebugInfo)
                Debug.Log($"[动画控制器] 触发武器动画: {animationName}");
        }

        #endregion

        #region Weapon Animation

        /// <summary>
        /// 播放武器动画
        /// </summary>
        private void PlayWeaponAnimation(string animationName)
        {
            if (_animator == null || string.IsNullOrEmpty(animationName)) return;

            try
            {
                // 在指定的武器动画层播放动画
                _animator.Play(animationName, _weaponAnimationLayer);

                if (_showDebugInfo)
                    Debug.Log($"[动画控制器] 播放武器动画: {animationName} (Layer: {_weaponAnimationLayer})");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[动画控制器] 播放武器动画失败: {animationName} - {e.Message}");
            }
        }

        /// <summary>
        /// 停止当前武器动画
        /// </summary>
        public void StopWeaponAnimation()
        {
            if (_animator == null) return;

            // 播放待机动画或停止当前动画
            var currentWeapon = _statusManager?.CurrentWeapon;
            if (currentWeapon?.WeaponData != null && !string.IsNullOrEmpty(currentWeapon.WeaponData.IdleAnimationName))
            {
                PlayWeaponAnimation(currentWeapon.WeaponData.IdleAnimationName);
            }
        }

        /// <summary>
        /// 获取当前武器动画状态
        /// </summary>
        public AnimatorStateInfo GetCurrentWeaponAnimationState()
        {
            return _animator != null ? _animator.GetCurrentAnimatorStateInfo(_weaponAnimationLayer) : default;
        }

        /// <summary>
        /// 检查武器动画是否正在播放
        /// </summary>
        public bool IsWeaponAnimationPlaying(string animationName)
        {
            if (_animator == null || string.IsNullOrEmpty(animationName)) return false;

            var stateInfo = _animator.GetCurrentAnimatorStateInfo(_weaponAnimationLayer);
            return stateInfo.IsName(animationName) && stateInfo.normalizedTime < 1.0f;
        }

        #endregion

        #region Animation Updates

        /// <summary>
        /// 初始化动画状态
        /// </summary>
        private void InitializeAnimationState()
        {
            if (_statusManager != null)
            {
                var currentState = _statusManager.GetStateSnapshot();

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
                _currentHasWeapon = currentState.hasWeapon;

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
                    _animator.SetBool(_hasWeaponParam, _currentHasWeapon);
                    _animator.SetLayerWeight(_weaponAnimationLayer, _currentHasWeapon ? 1f : 0f);
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

            _animator.SetLayerWeight(_weaponAnimationLayer, _currentHasWeapon ? 1f : 0f);
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
        }        /// <summary>
        /// 获取当前动画状态信息
        /// </summary>
        public AnimatorStateInfo GetCurrentAnimationState(int layerIndex = 0)
        {
            return _animator != null ? _animator.GetCurrentAnimatorStateInfo(layerIndex) : default;
        }

        #endregion

        #region Debug

        private void OnGUI()
        {
            if (!_showDebugInfo || _animator == null) return;

            GUILayout.BeginArea(new Rect(320, 200, 300, 400));
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
                
                // 显示武器动画层状态
                GUILayout.Label("--- Weapon Animation Layer ---");
                var weaponStateInfo = _animator.GetCurrentAnimatorStateInfo(_weaponAnimationLayer);
                GUILayout.Label($"Weapon State: {weaponStateInfo.shortNameHash}");
                GUILayout.Label($"Weapon State Time: {weaponStateInfo.normalizedTime:F2}");
                GUILayout.Label($"Weapon Layer: {_weaponAnimationLayer}");
                
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

        #region Remote Player Animation

        /// <summary>
        /// 更新远程玩家动画（直接从网络状态）
        /// </summary>
        private void UpdateRemotePlayerAnimation()
        {
            if (_networkPlayerController == null || !_networkPlayerController.HasRemotePlayerState()) return;
            
            var remoteState = _networkPlayerController.GetRemotePlayerState();
            
            // 直接更新目标动画参数
            _targetVelocity = remoteState.speed;
            _targetVelocityForward = Vector3.Dot(remoteState.velocity, remoteState.forwardAxis);
            _targetVelocityStrafe = Vector3.Dot(remoteState.velocity, remoteState.rightAxis);
            
            // 立即更新状态参数（不需要平滑过渡）
            if (_animator != null)
            {
                bool wasGrounded = _currentIsGrounded;
                _currentIsGrounded = remoteState.isGrounded;
                _currentIsInAir = !_currentIsGrounded;
                _currentIsSprinting = remoteState.isSprinting;
                
                _animator.SetBool(_isGroundedParam, _currentIsGrounded);
                _animator.SetBool(_isInAirParam, _currentIsInAir);
                _animator.SetBool(_isSprintingParam, _currentIsSprinting);
                
                // 检测跳跃和着地状态变化，触发相应的触发器
                if (!wasGrounded && _currentIsGrounded)
                {
                    // 从空中到地面 = 着地
                    _animator.SetTrigger(_triggerLandParam);
                }
                else if (wasGrounded && !_currentIsGrounded)
                {
                    // 从地面到空中 = 跳跃（或掉落）
                    if (remoteState.isJumping)
                    {
                        _animator.SetTrigger(_triggerJumpParam);
                    }
                }
            }
        }

        #endregion

        #region Event Subscription

        /// <summary>
        /// 订阅状态管理器事件
        /// </summary>
        private void SubscribeToEvents()
        {
            if (_statusManager != null)
            {
                _statusManager.OnMovementChanged += HandleMovementChanged;
                _statusManager.OnGroundStateChanged += HandleGroundStateChanged;
                _statusManager.OnSprintStateChanged += HandleSprintStateChanged;
                _statusManager.OnWeaponAnimationTriggered += HandleWeaponAnimationTriggered;
            }
        }
        
        /// <summary>
        /// 取消订阅状态管理器事件
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (_statusManager != null)
            {
                _statusManager.OnMovementChanged -= HandleMovementChanged;
                _statusManager.OnGroundStateChanged -= HandleGroundStateChanged;
                _statusManager.OnSprintStateChanged -= HandleSprintStateChanged;
                _statusManager.OnWeaponAnimationTriggered -= HandleWeaponAnimationTriggered;
            }
        }

        #endregion
    }
}
