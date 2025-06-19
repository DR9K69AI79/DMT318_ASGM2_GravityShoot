using UnityEngine;
using System;

namespace DWHITE
{
    /// <summary>
    /// 玩家状态管理器 - 事件驱动的状态中心
    /// 收集PlayerMotor和其他组件的状态变化，统一分发给订阅者
    /// 基于最小可行原则设计，专注核心功能
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerStateManager : MonoBehaviour
    {
        #region Events
        
        /// <summary>
        /// 状态发生任何变化时触发
        /// </summary>
        public static event Action<PlayerStateChangedEventArgs> OnStateChanged;
        
        /// <summary>
        /// 运动状态变化时触发
        /// </summary>
        public static event Action<PlayerStateChangedEventArgs> OnMovementChanged;
        
        /// <summary>
        /// 地面状态变化时触发
        /// </summary>
        public static event Action<PlayerStateChangedEventArgs> OnGroundStateChanged;
        
        /// <summary>
        /// 跳跃状态变化时触发
        /// </summary>
        public static event Action<PlayerStateChangedEventArgs> OnJumpStateChanged;
        
        /// <summary>
        /// 冲刺状态变化时触发
        /// </summary>
        public static event Action<PlayerStateChangedEventArgs> OnSprintStateChanged;

        #endregion

        #region Configuration

        [Header("状态管理配置")]
        [Tooltip("状态更新的频率（每秒帧数）")]
        [SerializeField] private int _updateRate = 30;
        [Tooltip("是否在Inspector中显示当前状态")]
        [SerializeField] private bool _showDebugInfo = false;

        #endregion

        #region Dependencies

        private PlayerMotor _playerMotor;
        private PlayerInput _playerInput;

        #endregion

        #region State Management

        /// <summary>
        /// 当前状态的只读访问
        /// </summary>
        public PlayerStateData CurrentState { get; private set; }

        private PlayerStateData _previousState;
        private float _lastUpdateTime;
        private float _updateInterval;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // 获取组件引用
            _playerMotor = GetComponent<PlayerMotor>();
            _playerInput = GetComponent<PlayerInput>();
            
            // 计算更新间隔
            _updateInterval = 1f / _updateRate;
            
            // 初始化状态
            CurrentState = PlayerStateData.Empty;
            _previousState = CurrentState;
        }

        private void Start()
        {
            // 进行首次状态更新
            UpdateState();
        }

        private void Update()
        {
            // 按照设定的频率更新状态
            if (Time.time - _lastUpdateTime >= _updateInterval)
            {
                UpdateState();
                _lastUpdateTime = Time.time;
            }
        }

        #endregion

        #region State Collection & Distribution

        /// <summary>
        /// 从各个组件收集状态并分发事件
        /// </summary>
        private void UpdateState()
        {
            // 保存前一帧状态
            _previousState = CurrentState;
            
            // 收集新状态
            CurrentState = CollectCurrentState();
            
            // 检查并分发状态变化事件
            if (!CurrentState.Equals(_previousState))
            {
                DistributeStateEvents();
            }
        }

        /// <summary>
        /// 从PlayerMotor和PlayerInput收集当前状态
        /// </summary>
        private PlayerStateData CollectCurrentState()
        {
            return new PlayerStateData
            {
                // Movement State
                isGrounded = _playerMotor.IsGrounded,
                isOnSteep = _playerMotor.OnSteep,
                isSprinting = _playerMotor.IsSprinting,
                velocity = _playerMotor.Velocity,
                speed = _playerMotor.Velocity.magnitude,
                moveInput = _playerInput.MoveInput,
                currentSpeedMultiplier = _playerMotor.CurrentSpeedMultiplier,
                
                // Jump State (基于velocity推断跳跃状态)
                isJumping = !_playerMotor.IsGrounded && Vector3.Dot(_playerMotor.Velocity, _playerMotor.UpAxis) > 0.1f,
                jumpPhase = 0, // 暂时简化，后续可以从PlayerMotor获取
                canJump = _playerMotor.IsGrounded,
                
                // Environment State
                gravityDirection = -_playerMotor.UpAxis,
                upAxis = _playerMotor.UpAxis,
                forwardAxis = _playerMotor.ForwardAxis,
                rightAxis = _playerMotor.RightAxis,
                contactNormal = Vector3.zero, // 暂时简化，后续可以从PlayerMotor获取
                
                // Input State
                lookInput = _playerInput.LookInput,
                firePressed = _playerInput.FirePressed,
                jumpPressed = _playerInput.JumpPressed,
                sprintPressed = _playerInput.SprintPressed
            };
        }

        /// <summary>
        /// 分发状态变化事件
        /// </summary>
        private void DistributeStateEvents()
        {
            var eventArgs = new PlayerStateChangedEventArgs(_previousState, CurrentState, Time.deltaTime);
            
            // 分发通用状态变化事件
            OnStateChanged?.Invoke(eventArgs);
            
            // 检查并分发特定类型的状态变化事件
            if (HasMovementChanged())
            {
                OnMovementChanged?.Invoke(eventArgs);
            }
            
            if (HasGroundStateChanged())
            {
                OnGroundStateChanged?.Invoke(eventArgs);
            }
            
            if (HasJumpStateChanged())
            {
                OnJumpStateChanged?.Invoke(eventArgs);
            }
            
            if (HasSprintStateChanged())
            {
                OnSprintStateChanged?.Invoke(eventArgs);
            }
        }

        #endregion

        #region State Change Detection

        private bool HasMovementChanged()
        {
            return !Vector3.Equals(_previousState.velocity, CurrentState.velocity) ||
                   !Vector2.Equals(_previousState.moveInput, CurrentState.moveInput) ||
                   _previousState.speed != CurrentState.speed;
        }

        private bool HasGroundStateChanged()
        {
            return _previousState.isGrounded != CurrentState.isGrounded ||
                   _previousState.isOnSteep != CurrentState.isOnSteep;
        }

        private bool HasJumpStateChanged()
        {
            return _previousState.isJumping != CurrentState.isJumping ||
                   _previousState.jumpPhase != CurrentState.jumpPhase ||
                   _previousState.canJump != CurrentState.canJump;
        }

        private bool HasSprintStateChanged()
        {
            return _previousState.isSprinting != CurrentState.isSprinting ||
                   Mathf.Abs(_previousState.currentSpeedMultiplier - CurrentState.currentSpeedMultiplier) > 0.01f;
        }

        #endregion

        #region Public API

        /// <summary>
        /// 立即强制更新状态（用于外部系统需要最新状态时）
        /// </summary>
        public void ForceUpdateState()
        {
            UpdateState();
        }

        /// <summary>
        /// 获取状态的只读副本
        /// </summary>
        public PlayerStateData GetStateSnapshot()
        {
            return CurrentState;
        }

        #endregion

        #region Debug

        private void OnGUI()
        {
            if (!_showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 200, 300, 400));
            GUILayout.Label("=== Player State Manager ===");
            GUILayout.Label($"Grounded: {CurrentState.isGrounded}");
            GUILayout.Label($"Sprinting: {CurrentState.isSprinting}");
            GUILayout.Label($"Jumping: {CurrentState.isJumping}");
            GUILayout.Label($"Speed: {CurrentState.speed:F2}");
            GUILayout.Label($"Move Input: {CurrentState.moveInput}");
            GUILayout.Label($"Up Axis: {CurrentState.upAxis}");
            GUILayout.EndArea();
        }

        #endregion
    }
}
