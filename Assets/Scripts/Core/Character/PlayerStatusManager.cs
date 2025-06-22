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
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    [RequireComponent(typeof(PlayerInput))]    public class PlayerStatusManager : NetworkSyncBase, IDamageable
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

            // 处理生命值回复
            if (_enableHealthRegeneration && _isAlive && _currentHealth < _maxHealth)
            {
                float regenAmount = _healthRegenRate * Time.deltaTime;
                _currentHealth = Mathf.Min(_maxHealth, _currentHealth + regenAmount);
            }

            // 更新防作弊统计
            if (_enableAntiCheat)
            {
                UpdateDamageStatistics();
                ValidateDamageRate();
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
            WeaponBase.OnReloadCompleted -= HandleReloadCompleted;        }

        #endregion

        #region Weapon Event Handlers        
        private void HandleWeaponSwitched(PlayerWeaponController controller, WeaponBase weapon)
        {
            if (controller != _weaponController || !photonView.IsMine) return;

            // 触发本地事件
            OnWeaponChanged?.Invoke(controller.CurrentWeaponIndex);

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

            // 通过RPC同步武器切换
            photonView.RPC("RPC_WeaponSwitched", RpcTarget.Others, controller.CurrentWeaponIndex);
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
        }        private void HandleReloadStarted(WeaponBase weapon)
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
        /// 从PlayerMotor、PlayerInput和武器系统收集当前状态
        /// </summary>
        private PlayerStateData CollectCurrentState()
        {
            var stateData = new PlayerStateData
            {
                // Movement State
                isGrounded = _playerMotor.IsGrounded,
                isOnSteep = _playerMotor.OnSteep,
                isSprinting = _playerMotor.IsSprinting,
                velocity = _playerMotor.Velocity,
                speed = _playerMotor.Velocity.magnitude,
                moveInput = _playerInput.MoveInput,
                currentSpeedMultiplier = _playerMotor.CurrentSpeedMultiplier,
                
                // Jump State
                isJumping = !_playerMotor.IsGrounded && Vector3.Dot(_playerMotor.Velocity, _playerMotor.UpAxis) > 0.1f,
                jumpPhase = 0,
                canJump = _playerMotor.IsGrounded,
                
                // Environment State
                gravityDirection = -_playerMotor.UpAxis,
                upAxis = _playerMotor.UpAxis,
                forwardAxis = _playerMotor.ForwardAxis,
                rightAxis = _playerMotor.RightAxis,
                contactNormal = Vector3.zero,
                
                // Input State
                lookInput = _playerInput.LookInput,
                firePressed = _playerInput.FirePressed,
                jumpPressed = _playerInput.JumpPressed,
                sprintPressed = _playerInput.SprintPressed,
                
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
        {            return _previousState.isSprinting != CurrentState.isSprinting ||
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
            if (!photonView.IsMine || !_isAlive || damage <= 0) return;
            
            // 防作弊检查
            if (_enableAntiCheat && !ValidateDamageRequest(damage))
            {
                Debug.LogWarning($"[PlayerStatusManager] 伤害请求被拒绝 - 伤害: {damage}");
                return;
            }
            
            float previousHealth = _currentHealth;
            _currentHealth = Mathf.Max(0, _currentHealth - damage);
            
            // 更新伤害统计
            _totalDamageThisSecond += damage;
            
            // 触发健康值变化事件
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            
            // 通过RPC同步健康值变化
            photonView.RPC("RPC_HealthChanged", RpcTarget.Others, _currentHealth);
            
            // 播放伤害特效（本地）
            PlayDamageEffects(hitPoint, hitDirection, damage);
            
            // 检查死亡
            if (_currentHealth <= 0 && _isAlive)
            {
                HandleDeath();
            }
        }

        public float GetCurrentHealth()
        {
            return _currentHealth;
        }

        public float GetMaxHealth()
        {
            return _maxHealth;        }

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
            
            _currentHealth = newHealth;
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }        [PunRPC]
        private void RPC_PlayerDeath()
        {
            if (photonView.IsMine) return;
            
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
            // 发送关键状态数据
            stream.SendNext(_currentHealth);
            stream.SendNext(_isAlive);
            stream.SendNext(CurrentWeaponIndex);
            stream.SendNext(CurrentWeapon?.CurrentAmmo ?? 0);
            stream.SendNext(CurrentWeapon?.IsReloading ?? false);
        }

        protected override void ReadData(PhotonStream stream, PhotonMessageInfo info)
        {
            // 接收远端状态数据
            float networkHealth = (float)stream.ReceiveNext();
            bool networkAlive = (bool)stream.ReceiveNext();
            int networkWeaponIndex = (int)stream.ReceiveNext();
            int networkAmmo = (int)stream.ReceiveNext();
            bool networkReloading = (bool)stream.ReceiveNext();
            
            // 应用远端状态
            if (_currentHealth != networkHealth)
            {
                _currentHealth = networkHealth;
                OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            }
            
            if (_isAlive != networkAlive)
            {
                _isAlive = networkAlive;
                if (!_isAlive)
                {
                    OnPlayerDeath?.Invoke(this);
                }
            }
            
            // 武器状态在RPC中处理，这里只更新显示
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
        }        /// <summary>
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
    }
}
