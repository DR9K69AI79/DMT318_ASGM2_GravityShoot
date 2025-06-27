using UnityEngine;
using System;
using Photon.Pun;
using DWHITE.Weapons;
using DWHITE.Weapons.Network;

namespace DWHITE
{
    /// <summary>
    /// 玩家状态管理器 - 统一的状态管理和网络同步中心
    /// 集成运动状态、武器状态、生命值状态的管理与同步
    /// 基于事件驱动设计，提供集中的状态访问点
    /// 
    /// 重要说明：
    /// - 本地玩家：从启用的PlayerMotor和PlayerInput组件读取状态
    /// - 远程玩家：从NetworkPlayerController的网络同步数据读取状态，避免访问被禁用的组件
    /// - 健康和武器状态始终从本地管理，确保数据一致性
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerStatusManager : NetworkSyncBase, IDamageable
    {
        #region Events

        /// <summary>
        /// 状态发生任何变化时触发 (实例事件)
        /// </summary>
        public event Action<PlayerStateChangedEventArgs> OnStateChanged;

        /// <summary>
        /// 运动状态变化时触发 (实例事件)
        /// </summary>
        public event Action<PlayerStateChangedEventArgs> OnMovementChanged;

        /// <summary>
        /// 地面状态变化时触发 (实例事件)
        /// </summary>
        public event Action<PlayerStateChangedEventArgs> OnGroundStateChanged;

        /// <summary>
        /// 跳跃状态变化时触发 (实例事件)
        /// </summary>
        public event Action<PlayerStateChangedEventArgs> OnJumpStateChanged;

        /// <summary>
        /// 冲刺状态变化时触发 (实例事件)
        /// </summary>
        public event Action<PlayerStateChangedEventArgs> OnSprintStateChanged;

        /// <summary>
        /// 生命值变化时触发 (实例事件)
        /// </summary>
        public event Action<float, float> OnHealthChanged; // currentHealth, maxHealth

        /// <summary>
        /// 武器切换时触发 (实例事件)
        /// </summary>
        public event Action<int> OnWeaponChanged; // weaponIndex

        /// <summary>
        /// 弹药变化时触发 (实例事件)
        /// </summary>
        public event Action<int, int> OnAmmoChanged; // currentAmmo, maxAmmo

        /// <summary>
        /// 装弹状态变化时触发 (实例事件)
        /// </summary>
        public event Action<bool> OnReloadStateChanged; // isReloading

        /// <summary>
        /// 武器动画播放事件 (实例事件)
        /// </summary>
        public event Action<string> OnWeaponAnimationTriggered; // animationName

        /// <summary>
        /// 玩家死亡时触发 (实例事件)
        /// </summary>
        public event Action<PlayerStatusManager> OnPlayerDeath;

        #endregion

        #region Configuration

        [Header("状态管理配置")]
        [Tooltip("状态更新的频率（每秒帧数）")]
        [SerializeField] private int _updateRate = 30;
        [Tooltip("是否在Inspector中显示当前状态")]
        [SerializeField] private bool _showDebugInfo = false;

        [Header("生命值配置")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private bool _enableHealthRegeneration = false;
        [SerializeField] private float _healthRegenRate = 1f; // 每秒恢复量

        [Header("防作弊设置")]
        [SerializeField] private bool _enableAntiCheat = true;
        [SerializeField] private float _maxDamagePerSecond = 1000f;
        [SerializeField] private float _validationTimeWindow = 5f;
        [SerializeField] private float _damageValidationTolerance = 0.1f;

        [Header("调试与监控配置")]
        [SerializeField] private bool _enableSyncDebugging = false;
        [SerializeField] private bool _enablePerformanceMonitoring = false;
        [SerializeField] private bool _enableStateValidation = false;
        [SerializeField] private float _syncWarningThreshold = 0.1f;
        [SerializeField] private int _maxDebugLogEntries = 100;

        #endregion

        #region Dependencies

        private PlayerMotor _playerMotor;
        private PlayerInput _playerInput;
        private PlayerWeaponController _weaponController;

        #endregion

        #region State Management

        /// <summary>
        /// 当前状态的只读访问
        /// </summary>
        public PlayerStateData CurrentState { get; private set; }

        private PlayerStateData _previousState;
        private float _lastUpdateTime;
        private float _updateInterval;        // 内部状态字段
        private float _currentHealth;
        private bool _isAlive = true;

        // 防作弊统计
        private float _totalDamageThisSecond = 0f;
        private float _lastDamageResetTime = 0f;
        private float _lastValidationTime = 0f;

        // 同步调试统计
        private float _lastNetworkUpdateTime;
        private int _networkUpdateCount;
        private float _averageNetworkInterval;
        private float _lastStateCollectionTime;
        private int _stateCollectionErrors;
        private int _successfulStateUpdates;

        // 性能监控
        private float _updateStateExecutionTime;
        private float _maxUpdateStateTime;
        private float _avgUpdateStateTime;
        private int _updateStateCallCount;

        // 数据源追踪
        private bool _lastMotorEnabled;
        private bool _lastInputEnabled;
        private Vector3 _lastLocalPosition;
        private Vector3 _lastNetworkPosition;
        private float _positionDiscrepancy;

        // 同步冲突检测
        private float _lastRpcHealthValue = -1f;
        private float _lastNetworkHealthValue = -1f;
        private int _healthSyncConflictCount;

        // 调试日志缓存
        private System.Collections.Generic.Queue<string> _debugLogCache = new System.Collections.Generic.Queue<string>();

        #endregion

        #region Properties

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsAlive => _isAlive;
        public int CurrentWeaponIndex => _weaponController?.CurrentWeaponIndex ?? -1;
        public WeaponBase CurrentWeapon => _weaponController?.CurrentWeapon;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // 获取组件引用
            _playerMotor = GetComponent<PlayerMotor>();
            _playerInput = GetComponent<PlayerInput>();
            _weaponController = GetComponent<PlayerWeaponController>();

            // 计算更新间隔
            _updateInterval = 1f / _updateRate;

            // 初始化状态
            _currentHealth = _maxHealth;
            CurrentState = PlayerStateData.Empty;
            _previousState = CurrentState;

            // 初始化防作弊统计
            _lastDamageResetTime = Time.time;
            _lastValidationTime = Time.time;
        }

        private void Start()
        {
            // 订阅武器事件
            if (_weaponController != null && photonView.IsMine)
            {
                SubscribeToWeaponEvents();
            }

            // 初始化调试统计
            InitializeDebugStatistics();

            // 添加调试信息
            if (_showDebugInfo)
            {
                Debug.Log($"[PlayerStatusManager] 初始化完成:");
                Debug.Log($"  - 是否为本地玩家: {photonView.IsMine}");
                Debug.Log($"  - PlayerMotor启用状态: {_playerMotor?.enabled}");
                Debug.Log($"  - PlayerInput启用状态: {_playerInput?.enabled}");
                Debug.Log($"  - 武器控制器: {(_weaponController != null ? "已找到" : "未找到")}");
            }

            // 进行首次状态更新
            UpdateState();
        }

        private void Update()
        {
            // 本地玩家：按照设定的频率更新状态
            // 远程玩家：降低更新频率，主要依赖网络同步
            float updateFrequency = photonView.IsMine ? _updateInterval : _updateInterval * 2f;
            
            if (Time.time - _lastUpdateTime >= updateFrequency)
            {
                UpdateState();
                _lastUpdateTime = Time.time;
            }

            // 只有本地玩家才处理生命值回复
            if (photonView.IsMine && _enableHealthRegeneration && _isAlive && _currentHealth < _maxHealth)
            {
                float regenAmount = _healthRegenRate * Time.deltaTime;
                _currentHealth = Mathf.Min(_maxHealth, _currentHealth + regenAmount);
            }

            // 只有本地玩家才需要防作弊统计
            if (photonView.IsMine && _enableAntiCheat)
            {
                UpdateDamageStatistics();
                ValidateDamageRate();
            }

            // 执行调试与监控
            if (_enableSyncDebugging || _enablePerformanceMonitoring)
            {
                UpdateDebugStatistics();
            }
        }

        private void OnDestroy()
        {
            // 取消事件订阅
            if (_weaponController != null && photonView.IsMine)
            {
                UnsubscribeFromWeaponEvents();
            }
        }

        #endregion

        #region Event Subscription

        private void SubscribeToWeaponEvents()
        {
            PlayerWeaponController.OnWeaponSwitched += HandleWeaponSwitched;
            WeaponBase.OnWeaponFired += HandleWeaponFired;
            WeaponBase.OnAmmoChanged += HandleAmmoChanged;
            WeaponBase.OnReloadStarted += HandleReloadStarted;
            WeaponBase.OnReloadCompleted += HandleReloadCompleted;
        }

        private void UnsubscribeFromWeaponEvents()
        {
            PlayerWeaponController.OnWeaponSwitched -= HandleWeaponSwitched;
            WeaponBase.OnWeaponFired -= HandleWeaponFired;
            WeaponBase.OnAmmoChanged -= HandleAmmoChanged;
            WeaponBase.OnReloadStarted -= HandleReloadStarted;
            WeaponBase.OnReloadCompleted -= HandleReloadCompleted;
        }

        #endregion

        #region Weapon Event Handlers        
        private void HandleWeaponSwitched(PlayerWeaponController controller, WeaponBase weapon)
        {
            if (controller != _weaponController || !photonView.IsMine) return;

            // 触发本地事件
            OnWeaponChanged?.Invoke(controller.CurrentWeaponIndex);

            // 触发武器收起动画
            if (weapon?.WeaponData != null && !string.IsNullOrEmpty(weapon.WeaponData.UnequipAnimationName))
            {
                OnWeaponAnimationTriggered?.Invoke(weapon.WeaponData.UnequipAnimationName);

                // 同步武器动画到其他玩家
                photonView.RPC("RPC_WeaponAnimation", RpcTarget.Others, weapon.WeaponData.UnequipAnimationName);
            }

            // 通过RPC同步武器切换
            photonView.RPC("RPC_WeaponSwitched", RpcTarget.Others, controller.CurrentWeaponIndex);

            // 触发武器装备动画
            if (weapon?.WeaponData != null && !string.IsNullOrEmpty(weapon.WeaponData.EquipAnimationName))
            {
                OnWeaponAnimationTriggered?.Invoke(weapon.WeaponData.EquipAnimationName);

                // 同步武器动画到其他玩家
                photonView.RPC("RPC_WeaponAnimation", RpcTarget.Others, weapon.WeaponData.EquipAnimationName);
            }

            // 同步弹药信息
            if (weapon != null)
            {
                OnAmmoChanged?.Invoke(weapon.CurrentAmmo, weapon.MaxAmmo);
            }


        }
        private void HandleWeaponFired(WeaponBase weapon)
        {
            if (weapon.transform.root != transform || !photonView.IsMine) return;

            // 触发射击动画
            if (weapon?.WeaponData != null && !string.IsNullOrEmpty(weapon.WeaponData.FireAnimationName))
            {
                OnWeaponAnimationTriggered?.Invoke(weapon.WeaponData.FireAnimationName);

                // 同步武器动画到其他玩家
                photonView.RPC("RPC_WeaponAnimation", RpcTarget.Others, weapon.WeaponData.FireAnimationName);
            }

            // 通过RPC同步射击效果
            Vector3 muzzlePos = weapon.MuzzlePoint ? weapon.MuzzlePoint.position : weapon.transform.position;
            Vector3 aimDir = _weaponController.CurrentAimDirection;

            photonView.RPC("RPC_WeaponFired", RpcTarget.Others,
                muzzlePos.x, muzzlePos.y, muzzlePos.z,
                aimDir.x, aimDir.y, aimDir.z);
        }

        private void HandleAmmoChanged(WeaponBase weapon, int currentAmmo, int maxAmmo)
        {
            if (weapon.transform.root != transform) return;

            // 触发本地事件
            OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
        }
        private void HandleReloadStarted(WeaponBase weapon)
        {
            if (weapon.transform.root != transform || !photonView.IsMine) return;

            // 触发装弹动画
            if (weapon?.WeaponData != null && !string.IsNullOrEmpty(weapon.WeaponData.ReloadAnimationName))
            {
                OnWeaponAnimationTriggered?.Invoke(weapon.WeaponData.ReloadAnimationName);

                // 同步武器动画到其他玩家
                photonView.RPC("RPC_WeaponAnimation", RpcTarget.Others, weapon.WeaponData.ReloadAnimationName);
            }

            OnReloadStateChanged?.Invoke(true);
            photonView.RPC("RPC_ReloadStateChanged", RpcTarget.Others, true);
        }
        private void HandleReloadCompleted(WeaponBase weapon)
        {
            if (weapon.transform.root != transform || !photonView.IsMine) return;

            OnReloadStateChanged?.Invoke(false);
            photonView.RPC("RPC_ReloadStateChanged", RpcTarget.Others, false);

            // 换弹完成后触发弹药更新事件
            OnAmmoChanged?.Invoke(weapon.CurrentAmmo, weapon.MaxAmmo);
        }

        #endregion

        #region State Collection & Distribution

        /// <summary>
        /// 从各个组件收集状态并分发事件
        /// </summary>
        private void UpdateState()
        {
            float startTime = Time.realtimeSinceStartup;
            
            try
            {
                // 状态收集调试
                if (_enableSyncDebugging)
                {
                    ValidateComponentStates();
                }

                // 保存前一帧状态
                _previousState = CurrentState;

                // 收集新状态
                CurrentState = CollectCurrentState();

                // 状态验证
                if (_enableStateValidation)
                {
                    ValidateStateConsistency();
                }

                // 检查并分发状态变化事件
                if (!CurrentState.Equals(_previousState))
                {
                    DistributeStateEvents();
                }

                // 更新成功统计
                _successfulStateUpdates++;
                _lastStateCollectionTime = Time.time;
            }
            catch (System.Exception ex)
            {
                _stateCollectionErrors++;
                AddDebugLog($"[ERROR] UpdateState异常: {ex.Message}");
                
                if (_showDebugInfo)
                {
                    Debug.LogError($"[PlayerStatusManager] UpdateState异常: {ex.Message}");
                    Debug.LogError($"  - 是否为本地玩家: {photonView.IsMine}");
                    Debug.LogError($"  - PlayerMotor状态: {(_playerMotor != null ? $"存在,启用:{_playerMotor.enabled}" : "null")}");
                    Debug.LogError($"  - PlayerInput状态: {(_playerInput != null ? $"存在,启用:{_playerInput.enabled}" : "null")}");
                }
                
                // 发生异常时使用安全的默认状态
                CurrentState = CreateSafeDefaultState();
            }
            finally
            {
                // 性能监控
                if (_enablePerformanceMonitoring)
                {
                    float executionTime = Time.realtimeSinceStartup - startTime;
                    UpdatePerformanceStatistics(executionTime);
                }
            }
        }

        /// <summary>
        /// 创建安全的默认状态
        /// </summary>
        private PlayerStateData CreateSafeDefaultState()
        {
            return new PlayerStateData
            {
                isGrounded = false,
                isOnSteep = false,
                isSprinting = false,
                velocity = Vector3.zero,
                speed = 0f,
                moveInput = Vector2.zero,
                currentSpeedMultiplier = 1f,
                isJumping = false,
                jumpPhase = 0,
                canJump = false,
                gravityDirection = Vector3.down,
                upAxis = Vector3.up,
                forwardAxis = transform.forward,
                rightAxis = transform.right,
                contactNormal = Vector3.zero,
                lookInput = Vector2.zero,
                firePressed = false,
                jumpPressed = false,
                sprintPressed = false,
                currentHealth = _currentHealth,
                maxHealth = _maxHealth,
                isAlive = _isAlive,
                hasWeapon = CurrentWeapon != null,
                currentWeaponIndex = CurrentWeaponIndex,
                currentAmmo = CurrentWeapon?.CurrentAmmo ?? 0,
                maxAmmo = CurrentWeapon?.MaxAmmo ?? 0,
                isReloading = CurrentWeapon?.IsReloading ?? false,
                weaponName = CurrentWeapon?.WeaponData?.WeaponName ?? ""
            };
        }

        /// <summary>
        /// 从PlayerMotor、PlayerInput和武器系统收集当前状态
        /// </summary>
        private PlayerStateData CollectCurrentState()
        {
            // 对于本地玩家，从组件直接收集状态
            if (photonView.IsMine)
            {
                return CollectLocalPlayerState();
            }
            // 对于远程玩家，从网络数据或缓存获取状态
            else
            {
                return CollectRemotePlayerState();
            }
        }

        /// <summary>
        /// 收集本地玩家状态（从实际组件读取）
        /// </summary>
        private PlayerStateData CollectLocalPlayerState()
        {
            var stateData = new PlayerStateData
            {
                // Movement State - 从启用的组件读取
                isGrounded = _playerMotor?.enabled == true ? _playerMotor.IsGrounded : false,
                isOnSteep = _playerMotor?.enabled == true ? _playerMotor.OnSteep : false,
                isSprinting = _playerMotor?.enabled == true ? _playerMotor.IsSprinting : false,
                velocity = _playerMotor?.enabled == true ? _playerMotor.Velocity : Vector3.zero,
                speed = _playerMotor?.enabled == true ? _playerMotor.Velocity.magnitude : 0f,
                moveInput = _playerInput?.enabled == true ? _playerInput.MoveInput : Vector2.zero,
                currentSpeedMultiplier = _playerMotor?.enabled == true ? _playerMotor.CurrentSpeedMultiplier : 1f,

                // Jump State
                isJumping = _playerMotor?.enabled == true && !_playerMotor.IsGrounded && Vector3.Dot(_playerMotor.Velocity, _playerMotor.UpAxis) > 0.1f,
                jumpPhase = 0,
                canJump = _playerMotor?.enabled == true ? _playerMotor.IsGrounded : false,

                // Environment State
                gravityDirection = _playerMotor?.enabled == true ? -_playerMotor.UpAxis : Vector3.down,
                upAxis = _playerMotor?.enabled == true ? _playerMotor.UpAxis : Vector3.up,
                forwardAxis = _playerMotor?.enabled == true ? _playerMotor.ForwardAxis : transform.forward,
                rightAxis = _playerMotor?.enabled == true ? _playerMotor.RightAxis : transform.right,
                contactNormal = Vector3.zero,

                // Input State
                lookInput = _playerInput?.enabled == true ? _playerInput.LookInput : Vector2.zero,
                firePressed = _playerInput?.enabled == true ? _playerInput.FirePressed : false,
                jumpPressed = _playerInput?.enabled == true ? _playerInput.JumpPressed : false,
                sprintPressed = _playerInput?.enabled == true ? _playerInput.SprintPressed : false,

                // Health State
                currentHealth = _currentHealth,
                maxHealth = _maxHealth,
                isAlive = _isAlive,

                // Weapon State
                hasWeapon = CurrentWeapon != null,
                currentWeaponIndex = CurrentWeaponIndex,
                currentAmmo = CurrentWeapon?.CurrentAmmo ?? 0,
                maxAmmo = CurrentWeapon?.MaxAmmo ?? 0,
                isReloading = CurrentWeapon?.IsReloading ?? false,
                weaponName = CurrentWeapon?.WeaponData?.WeaponName ?? ""
            };

            return stateData;
        }

        /// <summary>
        /// 收集远程玩家状态（从网络同步数据获取）
        /// </summary>
        private PlayerStateData CollectRemotePlayerState()
        {
            // 尝试从NetworkPlayerController获取远程玩家状态
            var networkController = GetComponent<NetworkPlayerController>();
            if (networkController != null && networkController.HasRemotePlayerState())
            {
                var remoteState = networkController.GetRemotePlayerState();
                
                // 合并网络状态和本地健康/武器状态
                var stateData = remoteState;
                stateData.currentHealth = _currentHealth;
                stateData.maxHealth = _maxHealth;
                stateData.isAlive = _isAlive;
                stateData.hasWeapon = CurrentWeapon != null;
                stateData.currentWeaponIndex = CurrentWeaponIndex;
                stateData.currentAmmo = CurrentWeapon?.CurrentAmmo ?? 0;
                stateData.maxAmmo = CurrentWeapon?.MaxAmmo ?? 0;
                stateData.isReloading = CurrentWeapon?.IsReloading ?? false;
                stateData.weaponName = CurrentWeapon?.WeaponData?.WeaponName ?? "";

                return stateData;
            }
            
            // 如果无法获取网络状态，返回最小化的状态数据
            return new PlayerStateData
            {
                // 基础状态
                isGrounded = false,
                isOnSteep = false,
                isSprinting = false,
                velocity = Vector3.zero,
                speed = 0f,
                moveInput = Vector2.zero,
                currentSpeedMultiplier = 1f,
                isJumping = false,
                jumpPhase = 0,
                canJump = false,
                gravityDirection = Vector3.down,
                upAxis = Vector3.up,
                forwardAxis = transform.forward,
                rightAxis = transform.right,
                contactNormal = Vector3.zero,
                lookInput = Vector2.zero,
                firePressed = false,
                jumpPressed = false,
                sprintPressed = false,

                // 健康和武器状态（这些是本地管理的）
                currentHealth = _currentHealth,
                maxHealth = _maxHealth,
                isAlive = _isAlive,
                hasWeapon = CurrentWeapon != null,
                currentWeaponIndex = CurrentWeaponIndex,
                currentAmmo = CurrentWeapon?.CurrentAmmo ?? 0,
                maxAmmo = CurrentWeapon?.MaxAmmo ?? 0,
                isReloading = CurrentWeapon?.IsReloading ?? false,
                weaponName = CurrentWeapon?.WeaponData?.WeaponName ?? ""
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

        #region Damage Validation & Effects

        /// <summary>
        /// 验证伤害请求
        /// </summary>
        private bool ValidateDamageRequest(float damage)
        {
            // 检查伤害值是否合理
            if (damage <= 0 || damage > 10000f)
            {
                Debug.LogWarning($"[PlayerStatusManager] 伤害值异常: {damage}");
                return false;
            }

            // 检查伤害速率
            float currentTime = Time.time;
            if (currentTime - _lastDamageResetTime >= 1f)
            {
                _totalDamageThisSecond = 0f;
                _lastDamageResetTime = currentTime;
            }

            if (_totalDamageThisSecond + damage > _maxDamagePerSecond)
            {
                Debug.LogWarning($"[PlayerStatusManager] 伤害速率过高: {_totalDamageThisSecond + damage}/s");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 播放伤害特效
        /// </summary>
        private void PlayDamageEffects(Vector3 hitPoint, Vector3 hitDirection, float damage)
        {
            // 这里可以播放命中特效、伤害数字等
            // 暂时使用日志输出
            if (_showDebugInfo)
            {
                Debug.Log($"[PlayerStatusManager] 受到伤害: {damage} at {hitPoint}");
            }
        }

        /// <summary>
        /// 更新伤害统计
        /// </summary>
        private void UpdateDamageStatistics()
        {
            float currentTime = Time.time;

            // 重置每秒伤害统计
            if (currentTime - _lastDamageResetTime >= 1f)
            {
                _totalDamageThisSecond = 0f;
                _lastDamageResetTime = currentTime;
            }
        }

        /// <summary>
        /// 验证伤害速率
        /// </summary>
        private void ValidateDamageRate()
        {
            if (_totalDamageThisSecond > _maxDamagePerSecond * 1.5f)
            {
                Debug.LogWarning($"[PlayerStatusManager] 异常高伤害速率检测: {_totalDamageThisSecond}/s");
                // 可以在这里采取反作弊措施
            }
        }

        #endregion

        #region IDamageable Implementation

        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[玩家状态管理] TakeDamage调用:");
                Debug.Log($"  - 伤害值: {damage}");
                Debug.Log($"  - 命中点: {hitPoint}");
                Debug.Log($"  - 命中方向: {hitDirection}");
                Debug.Log($"  - 是否为本地玩家: {photonView.IsMine}");
                Debug.Log($"  - 当前生命值: {_currentHealth}/{_maxHealth}");
                Debug.Log($"  - 是否存活: {_isAlive}");
            }

            // 基本验证：伤害值和存活状态
            if (!_isAlive || damage <= 0)
            {
                if (_showDebugInfo)
                {
                    if (!_isAlive) Debug.Log($"[玩家状态管理] 跳过伤害：玩家已死亡");
                    if (damage <= 0) Debug.Log($"[玩家状态管理] 跳过伤害：伤害值无效 ({damage})");
                }
                return;
            }

            // 重要：任何客户端都可以计算伤害，但只有玩家的拥有者才能修改血量并同步
            if (!photonView.IsMine)
            {
                if (_showDebugInfo)
                {
                    Debug.Log($"[玩家状态管理] 远程玩家受到伤害计算，但不处理血量变化");
                    Debug.Log($"  - 伤害值: {damage}");
                    Debug.Log($"  - 血量修改将由玩家拥有者处理");
                }
                
                // 对于远程玩家，只播放伤害特效，不修改血量
                // 血量变化将通过RPC从玩家拥有者同步过来
                PlayDamageEffects(hitPoint, hitDirection, damage);
                return;
            }

            // 以下逻辑只有本地玩家（拥有者）才执行
            if (_showDebugInfo)
                Debug.Log($"[玩家状态管理] 本地玩家处理伤害和血量变化");

            // 防作弊检查（只在拥有者端进行）
            if (_enableAntiCheat && !ValidateDamageRequest(damage))
            {
                Debug.LogWarning($"[玩家状态管理] 伤害请求被拒绝 - 伤害: {damage}");
                return;
            }

            float previousHealth = _currentHealth;
            _currentHealth = Mathf.Max(0, _currentHealth - damage);

            if (_showDebugInfo)
            {
                Debug.Log($"[玩家状态管理] 伤害应用结果:");
                Debug.Log($"  - 生命值变化: {previousHealth:F1} → {_currentHealth:F1}");
                Debug.Log($"  - 实际扣除: {(previousHealth - _currentHealth):F1}");
            }

            // 更新伤害统计
            _totalDamageThisSecond += damage;

            // 触发健康值变化事件（本地）
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (_showDebugInfo)
                Debug.Log($"[玩家状态管理] 本地健康值变化事件已触发");

            // 通过RPC同步健康值变化到其他玩家
            photonView.RPC("RPC_HealthChanged", RpcTarget.Others, _currentHealth);

            if (_showDebugInfo)
                Debug.Log($"[玩家状态管理] 健康值变化RPC已发送到其他玩家，新生命值: {_currentHealth:F1}");

            // 播放伤害特效（本地）
            PlayDamageEffects(hitPoint, hitDirection, damage);

            // 检查死亡
            if (_currentHealth <= 0 && _isAlive)
            {
                if (_showDebugInfo)
                    Debug.Log($"[玩家状态管理] 玩家死亡，调用HandleDeath");
                HandleDeath();
            }

            if (_showDebugInfo)
                Debug.Log($"[玩家状态管理] TakeDamage处理完成");
        }

        public float GetCurrentHealth()
        {
            return _currentHealth;
        }

        public float GetMaxHealth()
        {
            return _maxHealth;
        }

        #endregion

        #region Death Handling

        private void HandleDeath()
        {
            _isAlive = false;
            OnPlayerDeath?.Invoke(this);

            // 通过RPC同步死亡状态
            photonView.RPC("RPC_PlayerDeath", RpcTarget.Others);
        }

        #endregion

        #region Photon RPC Methods

        [PunRPC]
        private void RPC_WeaponSwitched(int weaponIndex)
        {
            if (photonView.IsMine) return;

            // 远端玩家武器切换
            if (_weaponController != null)
            {
                _weaponController.SwitchToWeapon(weaponIndex);
            }
        }

        [PunRPC]
        private void RPC_WeaponFired(float posX, float posY, float posZ, float dirX, float dirY, float dirZ)
        {
            if (photonView.IsMine) return;

            Vector3 muzzlePosition = new Vector3(posX, posY, posZ);
            Vector3 aimDirection = new Vector3(dirX, dirY, dirZ);

            // 播放远端射击效果
            if (CurrentWeapon != null)
            {
                CurrentWeapon.NetworkFire(aimDirection, Time.time);
            }
        }

        [PunRPC]
        private void RPC_ReloadStateChanged(bool isReloading)
        {
            if (photonView.IsMine) return;

            OnReloadStateChanged?.Invoke(isReloading);
        }

        [PunRPC]
        private void RPC_HealthChanged(float newHealth)
        {
            if (photonView.IsMine) return;

            float previousHealth = _currentHealth;
            _currentHealth = newHealth;
            
            // 记录健康值RPC数据用于同步冲突检测
            _lastRpcHealthValue = newHealth;
            _lastNetworkUpdateTime = Time.time;
            _networkUpdateCount++;
            
            if (_showDebugInfo)
            {
                Debug.Log($"[PlayerStatusManager] RPC_HealthChanged接收:");
                Debug.Log($"  - 远程玩家ViewID: {photonView.ViewID}");
                Debug.Log($"  - 生命值变化: {previousHealth:F1} → {_currentHealth:F1}");
                Debug.Log($"  - 实际变化: {(previousHealth - _currentHealth):F1}");
                Debug.Log($"  - 网络延迟: {Time.time - (float)PhotonNetwork.Time:F3}s");
            }
            
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        [PunRPC]
        private void RPC_PlayerDeath()
        {
            if (photonView.IsMine) return;

            if (_showDebugInfo)
            {
                Debug.Log($"[PlayerStatusManager] RPC_PlayerDeath接收:");
                Debug.Log($"  - 远程玩家ViewID: {photonView.ViewID}");
                Debug.Log($"  - 当前生命值: {_currentHealth:F1}");
            }

            _isAlive = false;
            _currentHealth = 0;
            OnPlayerDeath?.Invoke(this);
        }

        [PunRPC]
        private void RPC_WeaponAnimation(string animationName)
        {
            if (photonView.IsMine) return;

            // 在远程玩家上触发武器动画
            OnWeaponAnimationTriggered?.Invoke(animationName);
        }

        #endregion

        #region NetworkSyncBase Implementation

        protected override void WriteData(PhotonStream stream)
        {
            // 发送关键状态数据 (不再同步health和alive，这些通过RPC处理)
            stream.SendNext(CurrentWeaponIndex);
            stream.SendNext(CurrentWeapon?.CurrentAmmo ?? 0);
            stream.SendNext(CurrentWeapon?.IsReloading ?? false);
            
            // 发送健康状态仅作为备份验证（不触发事件）
            stream.SendNext(_currentHealth);
            stream.SendNext(_isAlive);
        }

        protected override void ReadData(PhotonStream stream, PhotonMessageInfo info)
        {
            // 接收远端状态数据
            int networkWeaponIndex = (int)stream.ReceiveNext();
            int networkAmmo = (int)stream.ReceiveNext();
            bool networkReloading = (bool)stream.ReceiveNext();
            
            // 接收健康状态用于验证（但不触发事件，避免与RPC重复）
            float networkHealth = (float)stream.ReceiveNext();
            bool networkAlive = (bool)stream.ReceiveNext();

            // 记录网络同步数据用于冲突检测
            _lastNetworkHealthValue = networkHealth;
            _lastNetworkUpdateTime = Time.time;
            _networkUpdateCount++;
            
            // 计算平均网络更新间隔
            if (_networkUpdateCount > 1)
            {
                _averageNetworkInterval = (_averageNetworkInterval * (_networkUpdateCount - 1) + 
                    (Time.time - _lastNetworkUpdateTime)) / _networkUpdateCount;
            }

            // 健康状态验证：检测RPC和NetworkSync的差异
            if (_enableSyncDebugging && _lastRpcHealthValue >= 0f)
            {
                float healthDifference = Mathf.Abs(_lastRpcHealthValue - networkHealth);
                if (healthDifference > 5f)
                {
                    _healthSyncConflictCount++;
                    AddDebugLog($"健康值同步差异检测: RPC值={_lastRpcHealthValue:F1}, NetworkSync值={networkHealth:F1}, 差异={healthDifference:F1}");
                }
            }

            // 存活状态验证
            if (_enableSyncDebugging && _isAlive != networkAlive)
            {
                AddDebugLog($"存活状态同步差异检测: 本地RPC值={_isAlive}, NetworkSync值={networkAlive}");
            }

            if (_showDebugInfo)
            {
                Debug.Log($"[PlayerStatusManager] NetworkSync数据接收:");
                Debug.Log($"  - 武器索引: {networkWeaponIndex}");
                Debug.Log($"  - 弹药: {networkAmmo}");
                Debug.Log($"  - 装弹中: {networkReloading}");
                Debug.Log($"  - 健康值(验证): {networkHealth:F1}");
                Debug.Log($"  - 存活状态(验证): {networkAlive}");
                Debug.Log($"  - 网络延迟: {(Time.time - (float)info.SentServerTime):F3}s");
            }

            // 武器状态在RPC中处理，这里只更新显示
            // 健康状态完全由RPC处理，此处不再重复触发事件
        }

        #endregion

        #region Public API

        /// <summary>
        /// 立即强制更新状态
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

        /// <summary>
        /// 设置最大生命值
        /// </summary>
        public void SetMaxHealth(float maxHealth)
        {
            _maxHealth = maxHealth;
            if (_currentHealth > _maxHealth)
            {
                _currentHealth = _maxHealth;
                OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            }
        }

        /// <summary>
        /// 恢复生命值
        /// </summary>
        public void Heal(float amount)
        {
            if (!_isAlive || amount <= 0) return;

            float previousHealth = _currentHealth;
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);

            if (_currentHealth != previousHealth)
            {
                OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

                if (photonView.IsMine)
                {
                    photonView.RPC("RPC_HealthChanged", RpcTarget.Others, _currentHealth);
                }
            }
        }

        /// <summary>
        /// 重生玩家
        /// </summary>
        public void Respawn()
        {
            _currentHealth = _maxHealth;
            _isAlive = true;
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (photonView.IsMine)
            {
                photonView.RPC("RPC_HealthChanged", RpcTarget.Others, _currentHealth);
            }
        }

        /// <summary>
        /// 设置防作弊参数
        /// </summary>
        public void SetAntiCheatOptions(bool enabled, float maxDpsPerSecond = 1000f, float validationWindow = 5f)
        {
            _enableAntiCheat = enabled;
            _maxDamagePerSecond = maxDpsPerSecond;
            _validationTimeWindow = validationWindow;
        }

        /// <summary>
        /// 获取当前伤害统计
        /// </summary>
        public float GetCurrentDPS()
        {
            return _totalDamageThisSecond;
        }

        /// <summary>
        /// 重置伤害统计
        /// </summary>
        public void ResetDamageStatistics()
        {
            _totalDamageThisSecond = 0f;
            _lastDamageResetTime = Time.time;
            _lastValidationTime = Time.time;
        }

        #endregion

        #region Debug

        private void OnGUI()
        {
            if (!_showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 200, 350, 500));
            GUILayout.Label("=== Player Status Manager ===");
            GUILayout.Label($"Health: {_currentHealth:F1}/{_maxHealth:F1}");
            GUILayout.Label($"Alive: {_isAlive}");
            GUILayout.Label($"Weapon: {CurrentWeaponIndex} ({CurrentWeapon?.WeaponData?.WeaponName ?? "None"})");
            GUILayout.Label($"Ammo: {CurrentWeapon?.CurrentAmmo ?? 0}/{CurrentWeapon?.MaxAmmo ?? 0}");
            GUILayout.Label($"Reloading: {CurrentWeapon?.IsReloading ?? false}");
            GUILayout.Label($"--- Movement ---");
            GUILayout.Label($"Grounded: {CurrentState.isGrounded}");
            GUILayout.Label($"Sprinting: {CurrentState.isSprinting}");
            GUILayout.Label($"Jumping: {CurrentState.isJumping}");
            GUILayout.Label($"Speed: {CurrentState.speed:F2}");
            GUILayout.Label($"Move Input: {CurrentState.moveInput}");
            GUILayout.Label($"Up Axis: {CurrentState.upAxis}");
            GUILayout.EndArea();
        }

        #endregion

        #region Debug & Monitoring Implementation

        /// <summary>
        /// 初始化调试统计
        /// </summary>
        private void InitializeDebugStatistics()
        {
            _lastNetworkUpdateTime = Time.time;
            _networkUpdateCount = 0;
            _averageNetworkInterval = 0f;
            _lastStateCollectionTime = Time.time;
            _stateCollectionErrors = 0;
            _successfulStateUpdates = 0;
            _updateStateExecutionTime = 0f;
            _maxUpdateStateTime = 0f;
            _avgUpdateStateTime = 0f;
            _updateStateCallCount = 0;
            _lastMotorEnabled = _playerMotor?.enabled ?? false;
            _lastInputEnabled = _playerInput?.enabled ?? false;
            _lastLocalPosition = transform.position;
            _lastNetworkPosition = transform.position;
            _positionDiscrepancy = 0f;
            _healthSyncConflictCount = 0;
            _debugLogCache.Clear();

            AddDebugLog("[INIT] PlayerStatusManager调试系统初始化完成");
        }
        
        /// <summary>
        /// 获取状态更新错误率
        /// </summary>
        private float GetErrorRate()
        {
            int total = _stateCollectionErrors + _successfulStateUpdates;
            if (total == 0) return 0f;
            return (_stateCollectionErrors * 100.0f) / total;
        }

        /// <summary>
        /// 更新调试统计
        /// </summary>
        private void UpdateDebugStatistics()
        {
            // 检测组件状态变化
            bool currentMotorEnabled = _playerMotor?.enabled ?? false;
            bool currentInputEnabled = _playerInput?.enabled ?? false;

            if (currentMotorEnabled != _lastMotorEnabled)
            {
                AddDebugLog($"[COMPONENT] PlayerMotor启用状态变化: {_lastMotorEnabled} → {currentMotorEnabled}");
                _lastMotorEnabled = currentMotorEnabled;
            }

            if (currentInputEnabled != _lastInputEnabled)
            {
                AddDebugLog($"[COMPONENT] PlayerInput启用状态变化: {_lastInputEnabled} → {currentInputEnabled}");
                _lastInputEnabled = currentInputEnabled;
            }

            // 位置同步监控
            if (!photonView.IsMine)
            {
                Vector3 currentPosition = transform.position;
                _positionDiscrepancy = Vector3.Distance(_lastLocalPosition, currentPosition);
                
                if (_positionDiscrepancy > _syncWarningThreshold)
                {
                    AddDebugLog($"[SYNC] 位置变化过大: {_positionDiscrepancy:F3}m");
                }
                
                _lastLocalPosition = currentPosition;
            }

            // 网络更新频率监控
            if (!photonView.IsMine && Time.time - _lastNetworkUpdateTime > 0.5f)
            {
                AddDebugLog($"[NETWORK] 网络更新延迟: {Time.time - _lastNetworkUpdateTime:F2}s");
            }
        }

        /// <summary>
        /// 验证组件状态
        /// </summary>
        private void ValidateComponentStates()
        {
            if (photonView.IsMine)
            {
                // 本地玩家：验证组件可用性
                if (_playerMotor == null)
                {
                    AddDebugLog("[WARNING] PlayerMotor组件缺失");
                }
                else if (!_playerMotor.enabled)
                {
                    AddDebugLog("[WARNING] PlayerMotor组件已禁用");
                }

                if (_playerInput == null)
                {
                    AddDebugLog("[WARNING] PlayerInput组件缺失");
                }
                else if (!_playerInput.enabled)
                {
                    AddDebugLog("[WARNING] PlayerInput组件已禁用");
                }
            }
            else
            {
                // 远程玩家：验证网络同步组件
                var networkController = GetComponent<NetworkPlayerController>();
                if (networkController == null)
                {
                    AddDebugLog("[WARNING] NetworkPlayerController组件缺失");
                }
                else if (!networkController.HasRemotePlayerState())
                {
                    AddDebugLog("[WARNING] NetworkPlayerController未提供远程状态数据");
                }
            }
        }

        /// <summary>
        /// 验证状态一致性
        /// </summary>
        private void ValidateStateConsistency()
        {
            // 验证基础状态合理性
            if (CurrentState.speed < 0f)
            {
                AddDebugLog($"[VALIDATION] 速度值异常: {CurrentState.speed}");
            }

            if (CurrentState.currentHealth < 0f || CurrentState.currentHealth > CurrentState.maxHealth)
            {
                AddDebugLog($"[VALIDATION] 生命值异常: {CurrentState.currentHealth}/{CurrentState.maxHealth}");
            }

            // 检查逻辑一致性
            if (CurrentState.isJumping && CurrentState.isGrounded)
            {
                AddDebugLog("[VALIDATION] 逻辑冲突: 同时处于跳跃和接地状态");
            }

            if (CurrentState.speed > 0.1f && CurrentState.moveInput.magnitude < 0.01f && photonView.IsMine)
            {
                AddDebugLog("[VALIDATION] 运动状态不一致: 有速度但无输入");
            }
        }

        /// <summary>
        /// 更新性能统计
        /// </summary>
        private void UpdatePerformanceStatistics(float executionTime)
        {
            _updateStateCallCount++;
            _updateStateExecutionTime += executionTime;
            _avgUpdateStateTime = _updateStateExecutionTime / _updateStateCallCount;

            if (executionTime > _maxUpdateStateTime)
            {
                _maxUpdateStateTime = executionTime;
                if (executionTime > 0.01f) // 10ms警告阈值
                {
                    AddDebugLog($"[PERFORMANCE] UpdateState执行时间过长: {executionTime * 1000:F2}ms");
                }
            }
        }

        /// <summary>
        /// 添加调试日志到缓存
        /// </summary>
        private void AddDebugLog(string message)
        {
            string timestampedMessage = $"[{Time.time:F2}] {message}";
            
            _debugLogCache.Enqueue(timestampedMessage);
            
            // 限制缓存大小
            while (_debugLogCache.Count > _maxDebugLogEntries)
            {
                _debugLogCache.Dequeue();
            }

            // 如果启用调试输出，也输出到控制台
            if (_showDebugInfo)
            {
                Debug.Log($"[PlayerStatusManager] {timestampedMessage}");
            }
        }

        #endregion
    }
}
