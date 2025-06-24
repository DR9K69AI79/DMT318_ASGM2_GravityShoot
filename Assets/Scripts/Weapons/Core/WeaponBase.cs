using UnityEngine;
using System;
using Photon.Pun;

namespace DWHITE.Weapons
{    /// <summary>
    /// 武器系统抽象基类
    /// 定义所有武器的通用接口和行为
    /// </summary>
    public abstract class WeaponBase : MonoBehaviourPun // TODO: 添加 , IWeapon 当Unity识别到IWeapon接口后
    {
        #region 事件定义
        
        /// <summary>
        /// 武器开火事件
        /// </summary>
        public static event Action<WeaponBase> OnWeaponFired;
        
        /// <summary>
        /// 武器装备事件
        /// </summary>
        public static event Action<WeaponBase> OnWeaponEquipped;
        
        /// <summary>
        /// 武器卸载事件
        /// </summary>
        public static event Action<WeaponBase> OnWeaponUnequipped;
        
        /// <summary>
        /// 弹药变化事件
        /// </summary>
        public static event Action<WeaponBase, int, int> OnAmmoChanged; // weapon, current, max
        
        /// <summary>
        /// 开始装弹事件
        /// </summary>
        public static event Action<WeaponBase> OnReloadStarted;
        
        /// <summary>
        /// 装弹完成事件
        /// </summary>
        public static event Action<WeaponBase> OnReloadCompleted;
        
        #endregion
        
        #region 配置与引用
        
        [Header("武器配置")]
        [SerializeField] protected WeaponData _weaponData;

        [Header("发射点")]
        [SerializeField] protected Transform _muzzlePoint;
        [SerializeField] protected Transform _muzzleFxPoint; // 用于特效或音效的发射点
        
        [Header("调试")]
        [SerializeField] protected bool _showDebugInfo = false;
        
        #endregion
        
        #region 状态变量
        
        protected float _lastFireTime;
        protected int _currentAmmo;
        protected bool _isReloading;
        protected bool _isEquipped;
        protected float _reloadStartTime;
        
        // 后坐力状态
        protected Vector2 _currentRecoil;
        protected float _recoilRecoveryTimer;
        
        #endregion
        
        #region 属性访问
        public WeaponData WeaponData => _weaponData;
        public bool IsEquipped => _isEquipped;
        public bool IsReloading => _isReloading;
        public int CurrentAmmo => _currentAmmo;
        public int MaxAmmo => _weaponData ? _weaponData.MagazineSize : 0;
        public bool HasAmmo => _weaponData && (_weaponData.HasInfiniteAmmo || _currentAmmo > 0);
        public bool CanFire => IsEquipped && !IsReloading && HasAmmo && !IsOnCooldown;
        public bool IsOnCooldown => Time.time - _lastFireTime < (_weaponData ? _weaponData.FireInterval : 0f);
        public Transform MuzzlePoint => _muzzlePoint;
        public Transform MuzzleFxPoint => _muzzleFxPoint;
        public Vector2 CurrentRecoil => _currentRecoil;
        
        /// <summary>
        /// 装弹进度 (0.0 - 1.0)
        /// </summary>
        public float ReloadProgress
        {
            get
            {
                if (!_isReloading || _weaponData == null) return 0f;
                
                float elapsed = Time.time - _reloadStartTime;
                return Mathf.Clamp01(elapsed / _weaponData.ReloadTime);
            }
        }
        
        #endregion
        
        #region Unity 生命周期
        
        protected virtual void Awake()
        {
            ValidateConfiguration();
            InitializeWeapon();
        }
        
        protected virtual void Start()
        {
            if (_weaponData != null)
            {
                _currentAmmo = _weaponData.MagazineSize;
            }
        }
        
        protected virtual void Update()
        {
            UpdateWeapon();
            UpdateRecoilRecovery();
            CheckReloadProgress();
        }
        
        #endregion
        
        #region 武器生命周期
        
        /// <summary>
        /// 装备武器
        /// </summary>
        public virtual void Equip()
        {
            if (_isEquipped) return;
            
            _isEquipped = true;
            gameObject.SetActive(true);
            OnEquip();
            OnWeaponEquipped?.Invoke(this);
            
            if (_showDebugInfo)
                Debug.Log($"[武器系统] {_weaponData?.WeaponName} 已装备");
        }
        
        /// <summary>
        /// 卸载武器
        /// </summary>
        public virtual void Unequip()
        {
            if (!_isEquipped) return;
            
            _isEquipped = false;
            _isReloading = false;
            OnUnequip();
            gameObject.SetActive(false);
            OnWeaponUnequipped?.Invoke(this);
            
            if (_showDebugInfo)
                Debug.Log($"[武器系统] {_weaponData?.WeaponName} 已卸载");
        }
        
        /// <summary>
        /// 子类重写的装备逻辑
        /// </summary>
        protected virtual void OnEquip() { }
        
        /// <summary>
        /// 子类重写的卸载逻辑
        /// </summary>
        protected virtual void OnUnequip() { }

        #endregion

        #region 射击系统          
        /// <summary>
        /// 尝试开火
        /// </summary>
        /// <param name="targetDirection">射击方向</param>
        /// <returns>是否成功开火</returns>
        public virtual bool TryFire(Vector3 targetDirection)
        {
            if (_showDebugInfo)
            {
                Debug.Log("[武器基类] TryFire 方法开始执行");
                Debug.Log($"[武器基类] CanFire: {CanFire}");
                Debug.Log($"[武器基类] HasAmmo: {HasAmmo}");
                Debug.Log($"[武器基类] IsReloading: {_isReloading}");
                Debug.Log($"[武器基类] IsEquipped: {_isEquipped}");
                Debug.Log($"[武器基类] IsOnCooldown: {IsOnCooldown}");
                Debug.Log($"[武器基类] 射击间隔: {Time.time - _lastFireTime} / {_weaponData?.FireInterval}");
            }
            if (!CanFire)
            {
                if (_showDebugInfo)
                    Debug.Log("[武器基类] 无法开火");

                if (!HasAmmo && !_isReloading)
                {
                    if (_showDebugInfo)
                        Debug.Log("[武器基类] 尝试自动换弹");

                    // 播放空弹音效
                    PlayEmptySound();
                    TryReload();
                }
                return false;
            }

            if (_showDebugInfo)
                Debug.Log("[武器基类] 开火条件满足，调用FireImplementation方法");

            // 调用子类实现的具体开火逻辑
            bool fireResult = FireImplementation(targetDirection);

            if (fireResult)
            {
                // 统一处理开火后的逻辑
                OnFireComplete(targetDirection);
            }

            if (_showDebugInfo)
                Debug.Log($"[武器基类] FireImplementation方法执行完成，结果: {fireResult}");

            return fireResult;
        }
        
        /// <summary>
        /// 具体的开火实现 (抽象方法，子类必须实现)
        /// </summary>
        /// <param name="direction">射击方向</param>
        /// <returns>是否成功开火</returns>
        protected abstract bool FireImplementation(Vector3 direction);

        /// <summary>
        /// 射击后处理 - 由TryFire统一调用
        /// </summary>
        protected virtual void OnFireComplete(Vector3 direction)
        {
            _lastFireTime = Time.time;

            // 消耗弹药
            if (_weaponData != null && !_weaponData.HasInfiniteAmmo)
            {
                _currentAmmo = Mathf.Max(0, _currentAmmo - 1);
                OnAmmoChanged?.Invoke(this, _currentAmmo, MaxAmmo);
            }

            // 添加后坐力
            AddRecoil();

            // 播放音效
            PlayFireSound();

            // 触发事件
            OnWeaponFired?.Invoke(this);

            if (_showDebugInfo)
                Debug.Log($"[武器系统] {_weaponData?.WeaponName} 开火，剩余弹药: {_currentAmmo}");
        }
        
        #endregion
          #region 装弹系统
        
        /// <summary>
        /// 尝试装弹 - 模板方法模式
        /// </summary>
        public virtual bool TryReload()
        {
            if (_showDebugInfo)
            {
                Debug.Log("[武器基类] TryReload 方法开始执行");
                Debug.Log($"[武器基类] IsReloading: {_isReloading}");
                Debug.Log($"[武器基类] IsEquipped: {_isEquipped}");
                Debug.Log($"[武器基类] HasInfiniteAmmo: {_weaponData?.HasInfiniteAmmo}");
                Debug.Log($"[武器基类] CurrentAmmo: {_currentAmmo} / {_weaponData?.MagazineSize}");
            }

            // 通用检查
            if (_isReloading || !_isEquipped || _weaponData == null || _weaponData.HasInfiniteAmmo)
            {
                if (_showDebugInfo)
                    Debug.Log("[武器基类] 装弹检查失败：状态不允许");
                return false;
            }
            
            if (_currentAmmo >= _weaponData.MagazineSize)
            {
                if (_showDebugInfo)
                    Debug.Log("[武器基类] 装弹检查失败：弹药已满");
                return false;
            }
            
            // 调用子类的特殊装弹检查
            if (!CanReloadImplementation())
            {
                if (_showDebugInfo)
                    Debug.Log("[武器基类] 装弹检查失败：子类检查不通过");
                return false;
            }
            
            if (_showDebugInfo)
                Debug.Log("[武器基类] 装弹检查通过，开始装弹");
            
            StartReload();
            return true;
        }
        
        /// <summary>
        /// 子类可重写的装弹检查 (抽象方法)
        /// </summary>
        /// <returns>是否可以装弹</returns>
        protected virtual bool CanReloadImplementation()
        {
            // 默认实现：总是允许装弹
            return true;
        }
        
        /// <summary>
        /// 开始装弹 - 统一处理
        /// </summary>
        protected virtual void StartReload()
        {
            _isReloading = true;
            _reloadStartTime = Time.time;
            
            // 调用子类的装弹实现
            OnReloadStartImplementation();
            
            // 统一处理
            PlayReloadSound();
            OnReloadStarted?.Invoke(this);
            
            if (_showDebugInfo)
                Debug.Log($"[武器系统] {_weaponData?.WeaponName} 开始装弹");
        }
        
        /// <summary>
        /// 子类可重写的装弹开始逻辑
        /// </summary>
        protected virtual void OnReloadStartImplementation()
        {
            // 默认实现：无额外逻辑
        }
        
        /// <summary>
        /// 检查装弹进度
        /// </summary>
        protected virtual void CheckReloadProgress()
        {
            if (!_isReloading || _weaponData == null) return;
            
            if (Time.time - _reloadStartTime >= _weaponData.ReloadTime)
            {
                CompleteReload();
            }
        }
          /// <summary>
        /// 完成装弹 - 统一处理
        /// </summary>
        protected virtual void CompleteReload()
        {
            _isReloading = false;
            _currentAmmo = _weaponData.MagazineSize;
            
            // 调用子类的装弹完成实现
            OnReloadCompleteImplementation();
            
            // 统一处理
            OnAmmoChanged?.Invoke(this, _currentAmmo, MaxAmmo);
            OnReloadCompleted?.Invoke(this);
            
            if (_showDebugInfo)
                Debug.Log($"[武器系统] {_weaponData?.WeaponName} 装弹完成");
        }
        
        /// <summary>
        /// 子类可重写的装弹完成逻辑
        /// </summary>
        protected virtual void OnReloadCompleteImplementation()
        {
            // 默认实现：无额外逻辑
        }
        
        #endregion
        
        #region 后坐力系统
        
        /// <summary>
        /// 添加后坐力
        /// </summary>
        protected virtual void AddRecoil()
        {
            if (_weaponData == null) return;
            
            Vector2 recoil = _weaponData.RecoilPattern;
            // 添加随机性
            recoil.x += UnityEngine.Random.Range(-recoil.x * 0.3f, recoil.x * 0.3f);
            recoil.y += UnityEngine.Random.Range(-recoil.y * 0.1f, recoil.y * 0.1f);
            
            _currentRecoil += recoil;
            _recoilRecoveryTimer = 0f;
        }
        
        /// <summary>
        /// 更新后坐力恢复
        /// </summary>
        protected virtual void UpdateRecoilRecovery()
        {
            if (_weaponData == null) return;
            
            _recoilRecoveryTimer += Time.deltaTime;
            float recoveryProgress = _recoilRecoveryTimer / _weaponData.RecoilRecoveryTime;
            
            if (recoveryProgress >= 1f)
            {
                _currentRecoil = Vector2.zero;
            }
            else
            {
                _currentRecoil = Vector2.Lerp(_currentRecoil, Vector2.zero, recoveryProgress);
            }
        }
        
        #endregion
          #region 音效系统
        
        protected virtual void PlayFireSound()
        {
            if (_weaponData?.FireSound != null)
            {
                PlaySoundAtPosition(_weaponData.FireSound, GetMuzzlePosition());
                
                if (_showDebugInfo)
                    Debug.Log($"[武器音效] 播放开火音效: {_weaponData.FireSound.name}");
            }
        }
        
        protected virtual void PlayReloadSound()
        {
            if (_weaponData?.ReloadSound != null)
            {
                PlaySoundAtPosition(_weaponData.ReloadSound, transform.position);
                
                if (_showDebugInfo)
                    Debug.Log($"[武器音效] 播放装弹音效: {_weaponData.ReloadSound.name}");
            }
        }
        
        protected virtual void PlayEmptySound()
        {
            if (_weaponData?.EmptySound != null)
            {
                PlaySoundAtPosition(_weaponData.EmptySound, transform.position);
                
                if (_showDebugInfo)
                    Debug.Log($"[武器音效] 播放空弹音效: {_weaponData.EmptySound.name}");
            }
        }
        
        /// <summary>
        /// 在指定位置播放音效的统一方法
        /// </summary>
        protected virtual void PlaySoundAtPosition(AudioClip clip, Vector3 position)
        {
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, position);
            }
        }
        
        #endregion
        
        #region 工具方法
        
        /// <summary>
        /// 初始化武器
        /// </summary>
        protected virtual void InitializeWeapon()
        {
            _isEquipped = false;
            _isReloading = false;
            _lastFireTime = -1f;
            _currentRecoil = Vector2.zero;
            _recoilRecoveryTimer = 0f;
        }
        
        /// <summary>
        /// 更新武器状态
        /// </summary>
        protected virtual void UpdateWeapon()
        {
            // 子类可以重写此方法添加额外的更新逻辑
        }
        
        /// <summary>
        /// 验证配置
        /// </summary>
        protected virtual void ValidateConfiguration()
        {
            if (_weaponData == null)
            {
                Debug.LogError($"[武器系统] {gameObject.name} 缺少 WeaponData 配置");
            }
            
            if (_muzzlePoint == null)
            {
                Debug.LogWarning($"[武器系统] {gameObject.name} 缺少 MuzzlePoint，将使用 transform 作为发射点");
                _muzzlePoint = transform;
            }
        }
        
        /// <summary>
        /// 获取发射点位置
        /// </summary>
        public virtual Vector3 GetMuzzlePosition()
        {
            return _muzzlePoint ? _muzzlePoint.position : transform.position;
        }
        
        /// <summary>
        /// 获取发射方向
        /// </summary>
        public virtual Vector3 GetMuzzleDirection()
        {
            return _muzzlePoint ? _muzzlePoint.forward : transform.forward;
        }
        
        #endregion
        
        #region 网络同步
          /// <summary>
        /// 网络同步开火事件
        /// </summary>
        [PunRPC]
        public virtual void NetworkFire(Vector3 direction, float timestamp)
        {
            // 子类实现具体的网络同步开火逻辑
        }
        
        #endregion
        
        #region 调试
        
#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            if (_muzzlePoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_muzzlePoint.position, 0.1f);
                Gizmos.DrawRay(_muzzlePoint.position, _muzzlePoint.forward * 2f);
            }
        }
#endif
        
        #endregion
    }
}
