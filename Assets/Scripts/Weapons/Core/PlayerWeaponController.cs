using UnityEngine;
using System.Collections.Generic;
using System;
using Photon.Pun;
using DWHITE;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 玩家武器控制器
    /// 管理武器切换、输入处理和网络同步
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerWeaponController : MonoBehaviourPun
    {
        #region 事件定义
        
        /// <summary>
        /// 武器切换事件
        /// </summary>
        public static event Action<PlayerWeaponController, WeaponBase> OnWeaponSwitched;
        
        /// <summary>
        /// 尝试射击事件（用于UI反馈等）
        /// </summary>
        public static event Action<PlayerWeaponController, bool> OnFireAttempt; // success
        
        #endregion
        
        #region 配置
        
        [Header("武器配置")]
        [SerializeField] private List<WeaponBase> _availableWeapons = new List<WeaponBase>();
        [SerializeField] private int _startingWeaponIndex = 0;
        [SerializeField] private bool _allowWeaponSwitch = true;
        [SerializeField] private Transform _weaponContainer;
        
        [Header("输入设置")]
        [SerializeField] private bool _autoFire = false; // 自动射击模式
        [SerializeField] private float _fireInputBufferTime = 0.1f; // 射击输入缓冲时间
        
        [Header("瞄准设置")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private float _aimRange = 100f;
        [SerializeField] private LayerMask _aimLayers = -1;
        [SerializeField] private bool _usePhysicsAiming = true; // 使用物理瞄准还是屏幕中心
        
        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = false;
        [SerializeField] private bool _showAimDebug = false;
        
        #endregion
        
        #region 组件引用
        
        private PlayerInput _playerInput;
        private PlayerView _playerView;
        
        #endregion
        
        #region 状态变量
        
        private WeaponBase _currentWeapon;
        private int _currentWeaponIndex = -1;
        private bool _isLocalPlayer;
        
        // 射击状态
        private bool _fireInputPressed;
        private bool _fireInputHeld;
        private float _lastFireInputTime;
        private bool _pendingFireInput;
        
        // 瞄准状态
        private Vector3 _currentAimDirection = Vector3.forward;
        private Vector3 _lastValidAimPoint;
        
        #endregion
        
        #region 属性访问
          public WeaponBase CurrentWeapon => _currentWeapon;
        public int CurrentWeaponIndex => _currentWeaponIndex;
        public int WeaponCount => _availableWeapons.Count;
        public bool HasWeapon => _currentWeapon != null;
        public bool CanSwitchWeapon => _allowWeaponSwitch && _availableWeapons.Count > 1;
        public Vector3 CurrentAimDirection => _currentAimDirection;
        public bool IsLocalPlayer => _isLocalPlayer;
        
        /// <summary>
        /// 获取指定索引的武器
        /// </summary>
        public WeaponBase GetWeaponAtIndex(int index)
        {
            if (index >= 0 && index < _availableWeapons.Count)
            {
                return _availableWeapons[index];
            }
            return null;
        }
        
        /// <summary>
        /// 获取所有可用武器
        /// </summary>
        public WeaponBase[] GetAllWeapons()
        {
            return _availableWeapons.ToArray();
        }
        
        #endregion
        
        #region Unity 生命周期
        
        private void Awake()
        {
            // 获取组件引用
            _playerInput = GetComponent<PlayerInput>();
            _playerView = GetComponent<PlayerView>();
            
            // 确定是否为本地玩家
            _isLocalPlayer = photonView == null || photonView.IsMine;
            
            // 验证配置
            ValidateConfiguration();
        }
        
        private void Start()
        {
            // 只有本地玩家需要处理输入
            if (_isLocalPlayer)
            {
                SetupInputHandlers();
            }
            
            // 初始化武器
            InitializeWeapons();
            
            // 装备起始武器
            if (_availableWeapons.Count > 0)
            {
                int weaponIndex = Mathf.Clamp(_startingWeaponIndex, 0, _availableWeapons.Count - 1);
                SwitchToWeapon(weaponIndex);
            }
        }
        
        private void Update()
        {
            if (_isLocalPlayer)
            {
                UpdateAiming();
                UpdateFireInput();
                UpdateWeaponSwitching();
            }
        }
        
        private void OnDestroy()
        {
            if (_isLocalPlayer)
            {
                RemoveInputHandlers();
            }
        }
        
        #endregion
        
        #region 输入处理        /// <summary>
        /// 设置输入事件处理器
        /// </summary>
        private void SetupInputHandlers()
        {
            if (_playerInput == null) return;
            
            // 通过PlayerInput组件订阅输入事件
            // PlayerInput会从InputManager获取输入并转发给武器控制器
        }
        
        /// <summary>
        /// 移除输入事件处理器
        /// </summary>
        private void RemoveInputHandlers()        {
            // 清理输入订阅
        }
        
        /// <summary>
        /// 更新射击输入处理
        /// </summary>
        private void UpdateFireInput()
        {
            if (_playerInput == null) return;

            // 通过PlayerInput主动获取射击输入状态
            bool firePressed = _playerInput.FirePressed;
            bool fireHeld = _playerInput.FireHeld;

            // 处理单次射击
            if (firePressed)
            {
                _fireInputPressed = true;
                _fireInputHeld = true;
                _lastFireInputTime = Time.time;
                _pendingFireInput = true;
                
                TryFire();
                
                if (_showDebugInfo)
                    Debug.Log("[武器控制器] 射击输入按下");
            }

            // 更新射击持续状态
            if (!fireHeld && _fireInputHeld)
            {
                _fireInputHeld = false;
                
                if (_showDebugInfo)
                    Debug.Log("[武器控制器] 射击输入释放");
            }
            
            // 处理自动射击
            if (_autoFire && fireHeld && _currentWeapon != null && _currentWeapon.WeaponData.Automatic)
            {
                TryFire();
            }
            
            // 处理输入缓冲
            if (_pendingFireInput && Time.time - _lastFireInputTime <= _fireInputBufferTime)
            {
                if (TryFire())
                {
                    _pendingFireInput = false;
                }
            }
            else if (_pendingFireInput)
            {
                _pendingFireInput = false;
            }
            
            // 重置单帧输入状态
            if (firePressed)
            {
                _fireInputPressed = false;
            }
        }
        
        /// <summary>
        /// 更新武器切换输入
        /// </summary>
        private void UpdateWeaponSwitching()
        {
            if (!CanSwitchWeapon || _playerInput == null) return;
            
            // 通过PlayerInput组件检查武器切换输入
            float switchInput = _playerInput.WeaponSwitchInput;
            if (Mathf.Abs(switchInput) > 0.1f)
            {
                int direction = switchInput > 0 ? 1 : -1;
                SwitchToNextWeapon(direction);
                
                if (_showDebugInfo)
                    Debug.Log($"[武器控制器] 武器切换 - 方向: {direction}");
            }
            
            // 检查装弹输入
            if (_playerInput.ReloadPressed)
            {
                bool success = ReloadCurrentWeapon();
                
                if (_showDebugInfo)
                    Debug.Log($"[武器控制器] 装弹输入 - 结果: {success}");
            }
        }
        
        #endregion
        
        #region 瞄准系统
        
        /// <summary>
        /// 更新瞄准
        /// </summary>
        private void UpdateAiming()
        {
            if (_playerCamera == null) return;
            
            Vector3 aimDirection;
            
            if (_usePhysicsAiming)
            {
                aimDirection = CalculatePhysicsAimDirection();
            }
            else
            {
                aimDirection = _playerCamera.transform.forward;
            }
            
            _currentAimDirection = aimDirection;
            
            // 调试绘制
            if (_showAimDebug)
            {
                Debug.DrawRay(_playerCamera.transform.position, aimDirection * _aimRange, Color.red);
            }
        }
        
        /// <summary>
        /// 计算物理瞄准方向
        /// </summary>
        private Vector3 CalculatePhysicsAimDirection()
        {
            // 从相机中心发射射线
            Ray aimRay = _playerCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));
            
            Vector3 targetPoint;
            
            // 尝试射线检测
            if (Physics.Raycast(aimRay, out RaycastHit hit, _aimRange, _aimLayers))
            {
                targetPoint = hit.point;
                _lastValidAimPoint = targetPoint;
            }
            else
            {
                // 没有命中，使用最大距离点
                targetPoint = aimRay.origin + aimRay.direction * _aimRange;
            }
            
            // 计算从武器到目标点的方向
            Vector3 weaponPosition = _currentWeapon != null ? _currentWeapon.GetMuzzlePosition() : transform.position;
            Vector3 aimDirection = (targetPoint - weaponPosition).normalized;
            
            return aimDirection;
        }
        
        /// <summary>
        /// 获取瞄准射线
        /// </summary>
        public Ray GetAimRay()
        {
            Vector3 origin = _currentWeapon != null ? _currentWeapon.GetMuzzlePosition() : transform.position;
            return new Ray(origin, _currentAimDirection);
        }
        
        #endregion
        
        #region 射击系统
          /// <summary>
        /// 尝试射击
        /// </summary>
        public bool TryFire()
        {
            Debug.Log("[武器控制器] TryFire 方法开始执行");
            Debug.Log($"[武器控制器] 当前武器存在: {_currentWeapon != null}");
            
            if (_currentWeapon == null)
            {
                Debug.Log("[武器控制器] 没有当前武器，射击失败");
                OnFireAttempt?.Invoke(this, false);
                return false;
            }
            
            Debug.Log($"[武器控制器] 当前武器: {_currentWeapon.gameObject.name}");
            Debug.Log($"[武器控制器] 武器类型: {_currentWeapon.GetType().Name}");
            Debug.Log($"[武器控制器] 瞄准方向: {_currentAimDirection}");            
            bool success = _currentWeapon.TryFire(_currentAimDirection);
            
            Debug.Log($"[武器控制器] 武器TryFire结果: {success}");
            
            // 网络同步现在由 PlayerStatusManager 处理
            
            OnFireAttempt?.Invoke(this, success);
            
            if (_showDebugInfo && success)
                Debug.Log($"[武器控制器] 成功射击 {_currentWeapon.WeaponData.WeaponName}");
            
            return success;
        }
        
        /// <summary>
        /// 强制射击（无冷却检查）
        /// </summary>
        public void ForceFire(Vector3 direction)
        {
            if (_currentWeapon == null) return;
            
            // 直接调用武器的 Fire 方法
            var fireMethod = typeof(WeaponBase).GetMethod("Fire", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fireMethod?.Invoke(_currentWeapon, new object[] { direction });
        }
        
        #endregion
        
        #region 武器管理
        
        /// <summary>
        /// 初始化武器
        /// </summary>
        private void InitializeWeapons()
        {
            // 找到所有子武器
            if (_availableWeapons.Count == 0)
            {
                WeaponBase[] childWeapons = GetComponentsInChildren<WeaponBase>(true);
                _availableWeapons.AddRange(childWeapons);
            }
            
            // 初始化所有武器为未装备状态
            foreach (var weapon in _availableWeapons)
            {
                if (weapon != null)
                {
                    weapon.Unequip();
                }
            }
            
            if (_showDebugInfo)
                Debug.Log($"[武器控制器] 初始化了 {_availableWeapons.Count} 个武器");
        }
        
        /// <summary>
        /// 切换到指定武器
        /// </summary>
        public bool SwitchToWeapon(int weaponIndex)
        {
            if (weaponIndex < 0 || weaponIndex >= _availableWeapons.Count)
                return false;
            
            if (weaponIndex == _currentWeaponIndex)
                return true;
            
            WeaponBase newWeapon = _availableWeapons[weaponIndex];
            if (newWeapon == null)
                return false;
            
            // 卸载当前武器
            if (_currentWeapon != null)
            {
                _currentWeapon.Unequip();
            }
            
            // 装备新武器
            _currentWeapon = newWeapon;
            _currentWeaponIndex = weaponIndex;
            _currentWeapon.Equip();
            
            // 触发事件
            OnWeaponSwitched?.Invoke(this, _currentWeapon);
            
            // 网络同步现在由 PlayerStatusManager 处理
            
            if (_showDebugInfo)
                Debug.Log($"[武器控制器] 切换到武器: {_currentWeapon.WeaponData.WeaponName}");
            
            return true;
        }
        
        /// <summary>
        /// 切换到下一个武器
        /// </summary>
        public bool SwitchToNextWeapon(int direction = 1)
        {
            if (_availableWeapons.Count <= 1) return false;
            
            int nextIndex = (_currentWeaponIndex + direction) % _availableWeapons.Count;
            if (nextIndex < 0) nextIndex = _availableWeapons.Count - 1;
            
            return SwitchToWeapon(nextIndex);
        }
        
        /// <summary>
        /// 添加武器
        /// </summary>
        public void AddWeapon(WeaponBase weapon)
        {
            if (weapon == null || _availableWeapons.Contains(weapon)) return;
            
            _availableWeapons.Add(weapon);
            weapon.transform.SetParent(_weaponContainer);
            weapon.Unequip();
            
            if (_showDebugInfo)
                Debug.Log($"[武器控制器] 添加武器: {weapon.WeaponData?.WeaponName}");
        }
        
        /// <summary>
        /// 移除武器
        /// </summary>
        public bool RemoveWeapon(WeaponBase weapon)
        {
            if (weapon == null || !_availableWeapons.Contains(weapon)) return false;
            
            // 如果是当前武器，先切换
            if (_currentWeapon == weapon)
            {
                if (_availableWeapons.Count > 1)
                {
                    int nextIndex = (_currentWeaponIndex + 1) % _availableWeapons.Count;
                    if (_availableWeapons[nextIndex] == weapon)
                        nextIndex = (nextIndex + 1) % _availableWeapons.Count;
                    
                    SwitchToWeapon(nextIndex);
                }
                else
                {
                    weapon.Unequip();
                    _currentWeapon = null;
                    _currentWeaponIndex = -1;
                }
            }
            
            _availableWeapons.Remove(weapon);
            
            if (_showDebugInfo)
                Debug.Log($"[武器控制器] 移除武器: {weapon.WeaponData?.WeaponName}");
            
            return true;
        }
        
        #endregion
          #region 网络同步 [已废弃 - 由PlayerStatusManager处理]
        
        /// <summary>
        /// [已废弃] 远程射击同步 - 现在由PlayerStatusManager处理
        /// </summary>
        [System.Obsolete("网络同步功能已迁移到PlayerStatusManager")]
        [PunRPC]
        private void OnRemoteFire(float dirX, float dirY, float dirZ, double timestamp)
        {
            Debug.LogWarning("[PlayerWeaponController] OnRemoteFire已废弃，请使用PlayerStatusManager");
            // 为了兼容性保留，但功能已移到PlayerStatusManager
        }
        
        /// <summary>
        /// [已废弃] 远程武器切换同步 - 现在由PlayerStatusManager处理
        /// </summary>
        [System.Obsolete("网络同步功能已迁移到PlayerStatusManager")]
        [PunRPC]
        private void OnRemoteWeaponSwitch(int weaponIndex)
        {
            Debug.LogWarning("[PlayerWeaponController] OnRemoteWeaponSwitch已废弃，请使用PlayerStatusManager");
            // 为了兼容性，仍然执行切换，但警告用户
            SwitchToWeapon(weaponIndex);
        }
        
        #endregion
        
        #region 公共API
        
        /// <summary>
        /// 获取指定索引的武器
        /// </summary>
        public WeaponBase GetWeapon(int index)
        {
            if (index < 0 || index >= _availableWeapons.Count)
                return null;
            return _availableWeapons[index];
        }
        
        /// <summary>
        /// 获取武器索引
        /// </summary>
        public int GetWeaponIndex(WeaponBase weapon)
        {
            return _availableWeapons.IndexOf(weapon);
        }
        
        /// <summary>
        /// 装弹当前武器
        /// </summary>
        public bool ReloadCurrentWeapon()
        {
            return _currentWeapon?.TryReload() ?? false;
        }
        
        /// <summary>
        /// 设置自动射击模式
        /// </summary>
        public void SetAutoFire(bool autoFire)
        {
            _autoFire = autoFire;
        }
        
        #endregion
        
        #region 验证和调试
        
        /// <summary>
        /// 验证配置
        /// </summary>
        private void ValidateConfiguration()
        {
            if (_weaponContainer == null)
            {
                _weaponContainer = transform;
                Debug.LogWarning("[武器控制器] 未设置武器容器，使用自身 Transform");
            }
            
            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
                if (_playerCamera == null)
                {
                    Debug.LogError("[武器控制器] 找不到玩家相机");
                }
            }
            
            // 验证起始武器索引
            if (_startingWeaponIndex >= _availableWeapons.Count)
            {
                _startingWeaponIndex = 0;
            }
        }
        
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_showAimDebug && _playerCamera != null)
            {
                // 绘制瞄准方向
                Gizmos.color = Color.green;
                Vector3 startPos = _currentWeapon != null ? _currentWeapon.GetMuzzlePosition() : transform.position;
                Gizmos.DrawRay(startPos, _currentAimDirection * _aimRange);
                
                // 绘制最后有效瞄准点
                if (_lastValidAimPoint != Vector3.zero)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(_lastValidAimPoint, 0.2f);
                }
            }
        }
#endif
        
        #endregion
    }
}
