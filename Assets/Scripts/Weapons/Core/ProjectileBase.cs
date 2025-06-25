using UnityEngine;
using System;
using Photon.Pun;
using Debug = UnityEngine.Debug;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 投射物抽象基类
    /// 定义所有投射物的通用行为和接口
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public abstract class ProjectileBase : MonoBehaviourPun
    {
        #region 事件定义
        
        /// <summary>
        /// 投射物命中事件
        /// </summary>
        public static event Action<ProjectileBase, RaycastHit> OnProjectileHit;
        
        /// <summary>
        /// 投射物销毁事件
        /// </summary>
        public static event Action<ProjectileBase> OnProjectileDestroyed;
        
        /// <summary>
        /// 投射物弹跳事件
        /// </summary>
        public static event Action<ProjectileBase, Vector3> OnProjectileBounce;
        
        #endregion
        
        #region 事件触发方法
        
        /// <summary>
        /// 触发投射物命中事件
        /// </summary>
        protected virtual void TriggerProjectileHit(RaycastHit hit)
        {
            OnProjectileHit?.Invoke(this, hit);
        }
        
        /// <summary>
        /// 触发投射物销毁事件
        /// </summary>
        protected virtual void TriggerProjectileDestroyed()
        {
            OnProjectileDestroyed?.Invoke(this);
        }
        
        /// <summary>
        /// 触发投射物弹跳事件
        /// </summary>
        protected virtual void TriggerProjectileBounce(Vector3 bouncePoint)
        {
            OnProjectileBounce?.Invoke(this, bouncePoint);
        }
        
        #endregion        
        #region 运行时配置（通过Configure方法设置）
        
        // 基础参数 - 运行时设置
        protected float _damage = 20f;
        protected float _speed = 20f;
        protected float _lifetime = 10f;
        protected LayerMask _hitLayers = -1;
        
        // 物理参数 - 运行时设置
        protected bool _useCustomGravity = false;
        protected float _gravityScale = 1f;
        protected float _drag = 0f;
          // 弹跳参数 - 运行时设置
        protected int _maxBounces = 0;
        protected float _bounceEnergyLoss = 0.1f;
        protected float _minBounceVelocity = 1f;
        
        // 效果预制体 - 运行时设置
        protected GameObject _impactEffectPrefab;
        protected GameObject _explosionEffectPrefab;
        protected AudioClip _launchSound;
        protected AudioClip _impactSound;
        protected AudioClip _explosionSound;
        protected AudioClip _bounceSound;

        [Header("调试选项")]
        [Tooltip("显示调试信息")]
        [SerializeField] protected bool _showDebugInfo = false;
        [Tooltip("显示轨迹预测线")]
        [SerializeField] protected bool _showTrajectory = false;
        
        #endregion
        
        #region 组件引用
          protected Rigidbody _rigidbody;
        protected Collider _collider;
        
        #endregion
        
        #region 状态变量
        
        protected Vector3 _initialVelocity;
        protected float _spawnTime;
        protected int _currentBounces;
        protected bool _hasHit;
        protected bool _isDestroyed;
        protected WeaponBase _sourceWeapon;
        protected GameObject _sourcePlayer;
        
        // 自定义重力
        protected Vector3 _gravityDirection = Vector3.down;
        
        #endregion
        
        #region 属性访问
        
        public float Damage => _damage;
        public float Speed => _speed;
        public float Lifetime => _lifetime;
        public Vector3 Velocity => _rigidbody ? _rigidbody.velocity : Vector3.zero;
        public bool HasHit => _hasHit;
        public bool IsDestroyed => _isDestroyed;
        public WeaponBase SourceWeapon => _sourceWeapon;
        public GameObject SourcePlayer => _sourcePlayer;
        public int RemainingBounces => Mathf.Max(0, _maxBounces - _currentBounces);
        public bool CanBounce => _maxBounces > 0 && _currentBounces < _maxBounces;
        
        #endregion
        
        #region Unity 生命周期
        
        protected virtual void Awake()
        {
            InitializeComponents();
        }
          protected virtual void Start()
        {
            _spawnTime = Time.time;
            SetupPhysics();
        }
        
        protected virtual void Update()
        {
            UpdateProjectile();
            CheckLifetime();
        }
        
        protected virtual void FixedUpdate()
        {
            if (_useCustomGravity && _rigidbody != null)
            {
                ApplyCustomGravity();
            }
        }
        
        protected virtual void OnCollisionEnter(Collision collision)
        {
            HandleCollision(collision);
        }
        
        protected virtual void OnTriggerEnter(Collider other)
        {
            HandleTrigger(other);
        }
        
        #endregion
          #region 初始化
        
        /// <summary>
        /// 初始化组件引用
        /// </summary>
        protected virtual void InitializeComponents()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            
            if (_rigidbody == null)
            {
                UnityEngine.Debug.LogError($"[投射物] {gameObject.name} 缺少 Rigidbody 组件");
            }
        }
        
        /// <summary>
        /// 设置物理属性
        /// </summary>
        protected virtual void SetupPhysics()
        {
            if (_rigidbody == null) return;
            
            _rigidbody.drag = _drag;
            if (_useCustomGravity)
            {
                _gravityDirection = CustomGravity.GetGravity(transform.position).normalized;
            }            // 设置初始速度
            if (_initialVelocity != Vector3.zero)
            {
                _rigidbody.velocity = _initialVelocity;
            }
        }

        #endregion

        #region 发射系统

        /// <summary>
        /// 发射投射物
        /// </summary>
        /// <param name="direction">发射方向</param>
        /// <param name="speed">发射速度</param>
        /// <param name="sourceWeapon">来源武器</param>
        /// <param name="sourcePlayer">来源玩家</param>
        public virtual void Launch(Vector3 direction, float speed, WeaponBase sourceWeapon = null, GameObject sourcePlayer = null)
        {
            _speed = speed;
            _sourceWeapon = sourceWeapon;
            _sourcePlayer = sourcePlayer;

            // 尝试从源武器获取配置（向后兼容）
            if (sourceWeapon != null && sourceWeapon.WeaponData != null)
            {
                if (sourceWeapon.WeaponData.UseProjectileSettings && sourceWeapon.WeaponData.ProjectileSettings != null)
                {
                    var settings = sourceWeapon.WeaponData.ProjectileSettings;
                    _damage = settings.Damage;
                    _lifetime = settings.Lifetime;
                    _impactEffectPrefab = settings.ImpactEffectPrefab;
                    _explosionEffectPrefab = settings.ExplosionEffectPrefab;
                    _launchSound = settings.LaunchSound;
                    _impactSound = settings.ImpactSound;
                    _explosionSound = settings.ExplosionSound;
                    _bounceSound = settings.BounceSound;

                    if (_showDebugInfo)
                        Debug.Log($"[投射物] Launch方法从武器获取配置: 伤害={_damage}, 特效已设置");
                }
                else
                {
                    // 回退到基本的伤害值
                    _damage = sourceWeapon.WeaponData.Damage;
                }
            }

            // 设置速度
            _initialVelocity = direction.normalized * speed; if (_rigidbody != null)
            {
                _rigidbody.velocity = _initialVelocity;
            }

            // 设置旋转
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            // 播放发射音效
            PlayLaunchSound();

            OnLaunch(direction, speed);

            if (_showDebugInfo)
                Debug.Log($"[投射物] {gameObject.name} 发射，速度: {speed}, 方向: {direction}");
        }
        
        /// <summary>
        /// 子类重写的发射逻辑
        /// </summary>
        protected virtual void OnLaunch(Vector3 direction, float speed) { }
        
        #endregion
        
        #region 碰撞处理
        
        /// <summary>
        /// 处理碰撞
        /// </summary>
        protected virtual void HandleCollision(Collision collision)
        {
            if (_isDestroyed) return;
            
            // 检查是否应该被忽略
            if (ShouldIgnoreCollision(collision.collider)) return;
            
            ContactPoint contact = collision.contacts[0];
            Vector3 hitPoint = contact.point;
            Vector3 hitNormal = contact.normal;
            
            // 创建碰撞信息
            RaycastHit hit = CreateHitInfo(collision.collider, hitPoint, hitNormal);

            // 处理命中
            if (ProcessHit(hit))
            {
                // 如果命中处理返回 true，则销毁投射物
                DestroyProjectile();
                PlayImpactEffect(hitPoint, hitNormal);
                PlayImpactSound();
                
            }
            else if (CanBounce)
            {
                // 尝试弹跳
                TryBounce(collision);
            }
            else
            {
                // 无法弹跳，销毁投射物
                DestroyProjectile();
                PlayImpactEffect(hitPoint, hitNormal);
                PlayImpactSound();
            }
        }
        
        /// <summary>
        /// 处理触发器
        /// </summary>
        protected virtual void HandleTrigger(Collider other)
        {
            if (_isDestroyed) return;
            
            // 检查是否应该被忽略
            if (ShouldIgnoreCollision(other)) return;
            
            // 创建碰撞信息
            RaycastHit hit = CreateHitInfo(other, transform.position, -transform.forward);
            
            // 处理命中
            ProcessHit(hit);
        }
        
        /// <summary>
        /// 检查是否应该忽略碰撞
        /// </summary>
        protected virtual bool ShouldIgnoreCollision(Collider collider)
        {
            // 忽略自己
            if (collider.transform == transform) return true;
            
            // 忽略来源玩家（避免自伤）
            if (_sourcePlayer != null && collider.transform.IsChildOf(_sourcePlayer.transform))
                return true;
            
            // 检查层级
            if ((_hitLayers.value & (1 << collider.gameObject.layer)) == 0)
                return true;
            
            return false;
        }
        
        /// <summary>
        /// 创建命中信息
        /// </summary>
        protected virtual RaycastHit CreateHitInfo(Collider collider, Vector3 point, Vector3 normal)
        {
            RaycastHit hit = new RaycastHit();
            // Unity 的 RaycastHit 是结构体，需要通过反射或其他方式设置
            // 这里简化处理，在子类中可以根据需要扩展
            return hit;
        }
        
        /// <summary>
        /// 处理命中 (抽象方法，子类必须实现)
        /// </summary>
        /// <param name="hit">命中信息</param>
        /// <returns>是否应该销毁投射物</returns>
        protected abstract bool ProcessHit(RaycastHit hit);
        
        #endregion
        
        #region 弹跳系统
        
        /// <summary>
        /// 尝试弹跳
        /// </summary>
        protected virtual void TryBounce(Collision collision)
        {
            if (!CanBounce || _rigidbody == null) return;
            
            ContactPoint contact = collision.contacts[0];
            Vector3 hitNormal = contact.normal;
            Vector3 incomingVelocity = _rigidbody.velocity;
            
            // 计算反射速度
            Vector3 reflectedVelocity = Vector3.Reflect(incomingVelocity, hitNormal);
            
            // 应用能量损失
            float energyRetained = 1f - _bounceEnergyLoss;
            reflectedVelocity *= energyRetained;
            
            // 检查最小弹跳速度
            if (reflectedVelocity.magnitude < _minBounceVelocity)
            {
                DestroyProjectile();
                return;
            }
            
            // 应用反射速度
            _rigidbody.velocity = reflectedVelocity;
            
            // 更新弹跳计数
            _currentBounces++;
            
            // 播放弹跳音效
            PlayBounceSound();
              // 触发弹跳事件
            TriggerProjectileBounce(contact.point);
            
            // 子类弹跳逻辑
            OnBounce(collision, reflectedVelocity);
            
            if (_showDebugInfo)
                Debug.Log($"[投射物] {gameObject.name} 弹跳 {_currentBounces}/{_maxBounces}");
        }
        
        /// <summary>
        /// 子类重写的弹跳逻辑
        /// </summary>
        protected virtual void OnBounce(Collision collision, Vector3 newVelocity) { }
        
        #endregion
        
        #region 自定义重力
        
        /// <summary>
        /// 设置自定义重力
        /// </summary>
        public virtual void SetCustomGravity(Vector3 gravityDirection, float gravityScale = 1f)
        {
            _useCustomGravity = true;
            _gravityDirection = gravityDirection.normalized;
            _gravityScale = gravityScale;
            
            if (_rigidbody != null)
            {
                _rigidbody.useGravity = false;
            }
        }

        /// <summary>
        /// 应用自定义重力
        /// </summary>
        protected virtual void ApplyCustomGravity()
        {
            if (_rigidbody == null) return;

            Vector3 gravityForce = _gravityDirection * (CustomGravity.GetGravity(transform.position).magnitude * _gravityScale);
            _rigidbody.AddForce(gravityForce, ForceMode.Acceleration);
            if (_showDebugInfo)
            {
                Debug.Log($"{gameObject.name} 应用自定义重力: {gravityForce}");
            }
        }
        
        #endregion
        
        #region 生命周期管理
        
        /// <summary>
        /// 更新投射物状态
        /// </summary>
        protected virtual void UpdateProjectile()
        {
            // 子类可以重写此方法添加额外的更新逻辑
        }
        
        /// <summary>
        /// 检查生命周期
        /// </summary>
        protected virtual void CheckLifetime()
        {
            if (_lifetime > 0 && Time.time - _spawnTime >= _lifetime)
            {
                DestroyProjectile();
            }
        }

        /// <summary>
        /// 销毁投射物
        /// </summary>
        public virtual void DestroyProjectile()
        {
            if (_isDestroyed) return;

            _isDestroyed = true;

            // 触发销毁事件
            TriggerProjectileDestroyed();

            // 子类销毁逻辑
            OnDestroy();

            // 网络销毁
            if (photonView != null && photonView.IsMine)
            {
                // 安全销毁：先检查PhotonView是否仍然有效
                if (photonView.ViewID != 0)
                {
                    try
                    {
                        // 添加额外检查，确保对象仍在PhotonNetwork的管理中
                        if (PhotonNetwork.GetPhotonView(photonView.ViewID) != null)
                        {
                            PhotonNetwork.Destroy(gameObject);
                        }
                        else
                        {
                            Debug.LogWarning($"[投射物] PhotonView {photonView.ViewID} 不在网络列表中，执行本地销毁");
                            Destroy(gameObject);
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[投射物] 网络销毁失败，改为本地销毁: {e.Message}");
                        Destroy(gameObject);
                    }
                }
                else
                {
                    // PhotonView已经无效，直接本地销毁
                    Destroy(gameObject);
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// 子类重写的销毁逻辑
        /// </summary>
        protected virtual void OnDestroy() { }

        #endregion

        #region 效果系统

        /// <summary>
        /// 播放撞击效果
        /// </summary>
        protected virtual void PlayImpactEffect(Vector3 position, Vector3 normal)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[投射物] PlayImpactEffect 被调用: position={position}, normal={normal}");
                Debug.Log($"[投射物] _impactEffectPrefab = {(_impactEffectPrefab != null ? _impactEffectPrefab.name : "NULL")}");
            }

            if (_impactEffectPrefab != null)
            {
                GameObject effect = Instantiate(_impactEffectPrefab, position, Quaternion.LookRotation(normal));
                Destroy(effect, 5f); // 5秒后自动销毁特效

                if (_showDebugInfo)
                    Debug.Log($"[投射物] 成功创建撞击特效: {effect.name}");
            }
            else if (_showDebugInfo)
            {
                Debug.LogWarning("[投射物] 无法创建撞击特效: _impactEffectPrefab 为空");
            }
        }
          /// <summary>
        /// 播放撞击音效
        /// </summary>
        protected virtual void PlayImpactSound()
        {
            if (_impactSound != null)
            {
                AudioSource.PlayClipAtPoint(_impactSound, transform.position);
            }
        }
        
        /// <summary>
        /// 播放弹跳音效
        /// </summary>
        protected virtual void PlayBounceSound()
        {
            if (_bounceSound != null)
            {
                AudioSource.PlayClipAtPoint(_bounceSound, transform.position);
            }
        }
        
        /// <summary>
        /// 播放发射音效
        /// </summary>
        protected virtual void PlayLaunchSound()
        {
            if (_launchSound != null)
            {
                AudioSource.PlayClipAtPoint(_launchSound, transform.position);
            }
        }
        
        /// <summary>
        /// 播放爆炸特效
        /// </summary>
        protected virtual void PlayExplosionEffect(Vector3 position, Vector3 normal = default)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[投射物] PlayExplosionEffect 被调用: position={position}");
                Debug.Log($"[投射物] _explosionEffectPrefab = {(_explosionEffectPrefab != null ? _explosionEffectPrefab.name : "NULL")}");
            }
            
            if (_explosionEffectPrefab != null)
            {
                Quaternion rotation = normal != default(Vector3) ? Quaternion.LookRotation(normal) : Quaternion.identity;
                GameObject effect = Instantiate(_explosionEffectPrefab, position, rotation);
                Destroy(effect, 10f); // 10秒后自动销毁爆炸特效（通常比撞击特效持续时间长）
                
                if (_showDebugInfo)
                    Debug.Log($"[投射物] 成功创建爆炸特效: {effect.name}");
            }
            else if (_showDebugInfo)
            {
                Debug.LogWarning("[投射物] 无法创建爆炸特效: _explosionEffectPrefab 为空");
            }
        }
        
        /// <summary>
        /// 播放爆炸音效
        /// </summary>
        protected virtual void PlayExplosionSound()
        {
            if (_explosionSound != null)
            {
                AudioSource.PlayClipAtPoint(_explosionSound, transform.position);
            }
        }
          #endregion
        
        #region 网络支持方法
          /// <summary>
        /// 网络销毁回调
        /// </summary>
        public virtual void NetworkDestroy()
        {
            if (_isDestroyed) return;
            
            _isDestroyed = true;
            
            // 触发销毁事件
            TriggerProjectileDestroyed();
            
            // 子类销毁逻辑
            OnDestroy();
            
            // 直接本地销毁，不再通过网络
            Destroy(gameObject);
        }
        
        /// <summary>
        /// 网络弹跳事件回调
        /// </summary>
        public virtual void OnNetworkBounce(Vector3 bouncePoint, Vector3 bounceNormal)
        {
            PlayBounceSound();
        }
        
        /// <summary>
        /// 网络命中事件回调
        /// </summary>
        public virtual void OnNetworkHit(Vector3 hitPoint, Vector3 hitNormal, string targetTag, float damage)
        {
            // 播放命中特效
            PlayImpactEffect(hitPoint, hitNormal);
            PlayImpactSound();
        }
        
        #endregion
        
        #region 调试
        
#if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            if (!_showTrajectory || _rigidbody == null) return;
            
            // 绘制轨迹
            Gizmos.color = Color.yellow;
            Vector3 velocity = _rigidbody.velocity;
            Vector3 position = transform.position;
            
            for (int i = 0; i < 20; i++)
            {
                float time = i * 0.1f;
                Vector3 futurePos = position + velocity * time + 0.5f * Physics.gravity * time * time;
                Gizmos.DrawWireSphere(futurePos, 0.05f);
            }
        }
#endif

        #endregion

        /// <summary>
        /// 配置投射物参数（使用ProjectileSettings）
        /// </summary>
        /// <param name="settings">投射物设置</param>
        /// <param name="velocity">初始速度</param>
        /// <param name="direction">发射方向</param>
        /// <param name="sourceWeapon">来源武器</param>
        /// <param name="sourcePlayer">来源玩家</param>
        public virtual void Configure(ProjectileSettings settings, Vector3 velocity, Vector3 direction, WeaponBase sourceWeapon = null, GameObject sourcePlayer = null)
        {
            if (settings == null)
            {
                Debug.LogError("[ProjectileBase] ProjectileSettings为空，无法配置投射物");
                return;
            }

            // 应用ProjectileSettings
            ApplyProjectileSettings(settings);

            // 设置基础参数
            _sourceWeapon = sourceWeapon;
            _sourcePlayer = sourcePlayer;
            _initialVelocity = velocity;

            // 设置物理属性
            if (_rigidbody != null)
            {
                _rigidbody.velocity = velocity;
                _rigidbody.mass = settings.Mass;
                _rigidbody.drag = settings.Drag;

                if (settings.UseGravity)
                {
                    // 自定义重力需要在Update中处理
                    _useCustomGravity = true;
                    _gravityScale = settings.GravityScale;
                }
            }

            // 设置旋转
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            // 播放发射音效
            PlayLaunchSound();

            // 调用子类配置
            OnConfigureWithSettings(settings, velocity, direction);

            if (_showDebugInfo)
            {
                Debug.Log($"[投射物] {gameObject.name} 使用ProjectileSettings配置完成");
                Debug.Log($"[投射物] 速度: {velocity}, 伤害: {settings.Damage}, 生命周期: {settings.Lifetime}");
                Debug.Log($"[投射物] 弹跳: {settings.MaxBounceCount}, 引力: {settings.GravityForce}, 爆炸: {settings.ExplosionRadius}");
            }
        }

        /// <summary>
        /// 应用ProjectileSettings到投射物
        /// </summary>
        protected virtual void ApplyProjectileSettings(ProjectileSettings settings)
        {
            // 基础设置
            _damage = settings.Damage;
            _speed = settings.Speed;
            _lifetime = settings.Lifetime;

            // 物理设置
            _useCustomGravity = settings.UseGravity;
            _gravityScale = settings.GravityScale;
            _drag = settings.Drag;

            // 弹跳设置
            _maxBounces = settings.MaxBounceCount;
            _bounceEnergyLoss = settings.BounceEnergyLoss;
            // 特效设置
            _impactEffectPrefab = settings.ImpactEffectPrefab;
            _explosionEffectPrefab = settings.ExplosionEffectPrefab;
            _launchSound = settings.LaunchSound;
            _impactSound = settings.ImpactSound;
            _explosionSound = settings.ExplosionSound;
            _bounceSound = settings.BounceSound;
            // 调试信息
            if (_showDebugInfo)
            {
                Debug.Log($"[投射物] ApplyProjectileSettings: ImpactEffectPrefab = {(_impactEffectPrefab != null ? _impactEffectPrefab.name : "NULL")}");
                Debug.Log($"[投射物] ApplyProjectileSettings: ExplosionEffectPrefab = {(_explosionEffectPrefab != null ? _explosionEffectPrefab.name : "NULL")}");
                Debug.Log($"[投射物] ApplyProjectileSettings: LaunchSound = {(_launchSound != null ? _launchSound.name : "NULL")}");
                Debug.Log($"[投射物] ApplyProjectileSettings: ImpactSound = {(_impactSound != null ? _impactSound.name : "NULL")}");
                Debug.Log($"[投射物] ApplyProjectileSettings: ExplosionSound = {(_explosionSound != null ? _explosionSound.name : "NULL")}");
            }

            // 其他设置会在子类中处理
        }
          /// <summary>
        /// 子类重写的ProjectileSettings配置逻辑
        /// </summary>
        protected virtual void OnConfigureWithSettings(ProjectileSettings settings, Vector3 velocity, Vector3 direction) 
        { 
            // 子类可以重写此方法进行额外配置
        }
        
        #region 伤害处理系统
        
        /// <summary>
        /// 统一伤害处理方法 - 集成DamageSystem
        /// </summary>
        /// <param name="hitCollider">命中的碰撞体</param>
        /// <param name="hitPoint">命中点</param>
        /// <param name="hitNormal">命中法线</param>
        /// <param name="isHeadshot">是否爆头</param>
        /// <returns>是否成功应用伤害</returns>
        protected virtual bool ApplyDamageToTarget(Collider hitCollider, Vector3 hitPoint, Vector3 hitNormal, bool isHeadshot = false)
        {
            // 网络同步检查：只有投射物的拥有者才能造成伤害，避免重复伤害
            if (photonView != null && !photonView.IsMine)
            {
                if (_showDebugInfo)
                    Debug.Log($"[投射物] 非拥有者，跳过伤害处理: PhotonView.IsMine = {photonView.IsMine}");
                return false;
            }

            // 查找目标的IDamageable组件
            DWHITE.IDamageable damageable = hitCollider.GetComponent<DWHITE.IDamageable>();
            if (damageable == null)
            {
                damageable = hitCollider.GetComponentInParent<DWHITE.IDamageable>();
            }

            if (damageable == null)
            {
                if (_showDebugInfo)
                    Debug.Log($"[投射物] 目标 {hitCollider.name} 不具有IDamageable接口");
                return false;
            }

            if (!damageable.IsAlive)
            {
                if (_showDebugInfo)
                    Debug.Log($"[投射物] 目标 {hitCollider.name} 已死亡，跳过伤害");
                return false;
            }

            // 计算最终伤害（考虑爆头等因素）
            float finalDamage = _damage;
            if (isHeadshot && _sourceWeapon?.WeaponData?.CanHeadshot == true)
            {
                finalDamage *= _sourceWeapon.WeaponData.HeadshotMultiplier;
            }

            // 使用DamageSystem统一处理伤害
            if (DamageSystem.Instance != null)
            {
                // 创建伤害信息
                DamageInfo damageInfo = new DamageInfo
                {
                    damage = finalDamage,
                    damageType = DamageType.Projectile,
                    source = _sourcePlayer,
                    weapon = _sourceWeapon,
                    projectile = this,
                    hitPoint = hitPoint,
                    hitDirection = Velocity.normalized,
                    hitNormal = hitNormal,
                    isHeadshot = isHeadshot,
                    distance = Vector3.Distance(hitPoint, transform.position)
                };

                // 通过DamageSystem应用伤害
                DamageSystem.ApplyDamage(damageable, damageInfo);

                if (_showDebugInfo)
                    Debug.Log($"[投射物] 通过DamageSystem对 {hitCollider.name} 造成 {finalDamage:F1} 伤害 (爆头: {isHeadshot})");
                
                return true;
            }
            else
            {
                // 回退到直接调用IDamageable接口
                damageable.TakeDamage(finalDamage, hitPoint, Velocity.normalized);
                
                if (_showDebugInfo)
                    Debug.Log($"[投射物] 直接对 {hitCollider.name} 造成 {finalDamage:F1} 伤害 (爆头: {isHeadshot})");
                
                return true;
            }
        }

        /// <summary>
        /// 检查命中是否为爆头
        /// </summary>
        /// <param name="hitCollider">命中的碰撞体</param>
        /// <param name="hitPoint">命中点</param>
        /// <returns>是否为爆头</returns>
        protected virtual bool IsHeadshot(Collider hitCollider, Vector3 hitPoint)
        {
            // 检查武器是否支持爆头
            if (_sourceWeapon?.WeaponData?.CanHeadshot != true)
                return false;

            // 简单的爆头检测：检查碰撞体是否为头部标签
            if (hitCollider.CompareTag("Head"))
                return true;

            // 或者通过名称检测
            if (hitCollider.name.ToLower().Contains("head"))
                return true;

            // 更复杂的爆头检测可以在子类中重写此方法
            return false;
        }

        /// <summary>
        /// 检查目标是否可以被伤害
        /// </summary>
        /// <param name="hitCollider">命中的碰撞体</param>
        /// <returns>是否可以被伤害</returns>
        protected virtual bool CanDamageTarget(Collider hitCollider)
        {
            // 基本检查：确保目标具有IDamageable接口且存活
            DWHITE.IDamageable damageable = hitCollider.GetComponent<DWHITE.IDamageable>();
            if (damageable == null)
            {
                damageable = hitCollider.GetComponentInParent<DWHITE.IDamageable>();
            }

            return damageable != null && damageable.IsAlive;
        }

        #endregion
    }
}
