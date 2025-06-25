using UnityEngine;
using Photon.Pun;
using DWHITE.Weapons.Network;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 标准投射物实现
    /// 通用的投射物行为，适用于大多数武器
    /// </summary>
    public class StandardProjectile : ProjectileBase, IPunInstantiateMagicCallback
    {
        #region 配置
        
        [Header("标准投射物设置")]
        [SerializeField] private bool _explodeOnImpact = false;
        [SerializeField] private float _explosionDelay = 0f;
        [SerializeField] private bool _penetrateTargets = false;
        [SerializeField] private int _maxPenetrations = 0;
          [Header("伤害设置")]
        [SerializeField] private bool _damageOnTouch = true;
        [SerializeField] private string _damageableTag = "Player";
        [SerializeField] private LayerMask _damageableLayers = -1;
        
        [Header("距离设置")]
        [SerializeField] private float _maxRange = 100f;
        
        #endregion
        
        #region 状态变量
        
        private int _penetrationCount = 0;
        private bool _hasExploded = false;

        #endregion

        #region Unity 生命周期

        protected override void Start()
        {
            base.Start();

            // 如果是网络生成的投射物，从初始化数据中获取参数
            if (photonView != null && photonView.InstantiationData != null)
            {
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物] 开始网络配置，所有者: {photonView.Owner?.NickName}, IsMine: {photonView.IsMine}");

                ConfigureFromNetworkData(photonView.InstantiationData);

                // 启用网络同步组件的调试信息
                var networkSync = GetComponent<ProjectileNetworkSync>();
                if (networkSync != null && _showDebugInfo)
                {
                    networkSync.EnableDebugInfo(true);
                }
            }
            else if (_showDebugInfo)
            {
                Debug.Log("[标准投射物] 本地投射物，无网络数据");
            }
        }
        
        #endregion
        
        #region 网络初始化
        
        /// <summary>
        /// Photon 网络实例化回调
        /// </summary>
        public void OnPhotonInstantiate(PhotonMessageInfo info)
        {
            if (info.photonView.InstantiationData != null)
            {
                ConfigureFromNetworkData(info.photonView.InstantiationData);
            }
        }

        /// <summary>
        /// 从网络数据配置投射物
        /// </summary>
        private void ConfigureFromNetworkData(object[] data)
        {
            try
            {
                if (data.Length >= 4)
                {
                    // 解析速度数据（前3个元素总是速度）
                    Vector3 velocity = new Vector3((float)data[0], (float)data[1], (float)data[2]);

                    // 检查数据格式：简单格式 vs ProjectileSettings格式
                    if (data.Length == 5)
                    {
                        // 简单格式：velocity.x, velocity.y, velocity.z, damage, sourceWeaponID
                        _damage = (float)data[3];
                        if (data.Length >= 5)
                        {
                            // 获取武器来源ID
                            int sourceWeaponViewID = (int)data[4];
                            PhotonView sourceView = PhotonView.Find(sourceWeaponViewID);
                            if (sourceView != null)
                            {
                                _sourceWeapon = sourceView.GetComponent<WeaponBase>();
                                _sourcePlayer = sourceView.transform.root.gameObject;
                                // 从源武器获取完整特效设置
                                if (_sourceWeapon != null && _sourceWeapon.WeaponData != null && _sourceWeapon.WeaponData.UseProjectileSettings)
                                {
                                    var settings = _sourceWeapon.WeaponData.ProjectileSettings;
                                    if (settings != null)
                                    {
                                        _impactEffectPrefab = settings.ImpactEffectPrefab;
                                        _explosionEffectPrefab = settings.ExplosionEffectPrefab;
                                        _launchSound = settings.LaunchSound;
                                        _impactSound = settings.ImpactSound;
                                        _explosionSound = settings.ExplosionSound;
                                        _bounceSound = settings.BounceSound;

                                        if (_showDebugInfo)
                                            Debug.Log($"[标准投射物] 简单格式网络投射物特效配置完成: ImpactEffect={(_impactEffectPrefab != null ? _impactEffectPrefab.name : "NULL")}, ExplosionEffect={(_explosionEffectPrefab != null ? _explosionEffectPrefab.name : "NULL")}");
                                    }
                                }
                            }
                        }

                        if (_showDebugInfo)
                            Debug.Log($"[标准投射物] 简单格式网络配置完成，速度: {velocity}, 伤害: {_damage}");
                    }
                    else if (data.Length >= 19)
                    {
                        // ProjectileSettings格式：velocity.x, velocity.y, velocity.z, damage, maxRange, lifetime, etc.
                        int index = 3;
                        _damage = (float)data[index++];           // data[3] - Damage
                        _maxRange = (float)data[index++];         // data[4] - MaxRange  
                        _lifetime = (float)data[index++];         // data[5] - Lifetime

                        // 物理设置
                        float mass = (float)data[index++];        // data[6] - Mass
                        float drag = (float)data[index++];        // data[7] - Drag
                        bool useGravity = (bool)data[index++];    // data[8] - UseGravity
                        float gravityScale = (float)data[index++]; // data[9] - GravityScale

                        // 弹跳设置
                        _maxBounces = (int)data[index++];         // data[10] - MaxBounceCount
                        _bounceEnergyLoss = (float)data[index++]; // data[11] - BounceEnergyLoss

                        // 引力设置
                        float gravityForce = (float)data[index++]; // data[12] - GravityForce
                        float gravityRadius = (float)data[index++]; // data[13] - GravityRadius

                        // 爆炸设置
                        float explosionRadius = (float)data[index++]; // data[14] - ExplosionRadius
                        float explosionDamage = (float)data[index++]; // data[15] - ExplosionDamage

                        // 穿透设置
                        _maxPenetrations = (int)data[index++];    // data[16] - PenetrationCount
                        float penetrationReduction = (float)data[index++]; // data[17] - PenetrationDamageReduction

                        // 武器来源ID
                        if (index < data.Length)
                        {
                            int sourceWeaponViewID = (int)data[index++]; // data[18] - SourceWeaponID
                            if (sourceWeaponViewID != -1)
                            {
                                PhotonView sourceView = PhotonView.Find(sourceWeaponViewID);
                                if (sourceView != null)
                                {
                                    _sourceWeapon = sourceView.GetComponent<WeaponBase>();
                                    _sourcePlayer = sourceView.transform.root.gameObject;
                                }
                            }
                        }
                        // 应用物理设置
                        if (_rigidbody != null)
                        {
                            _rigidbody.mass = mass;
                            _rigidbody.drag = drag;
                            _rigidbody.useGravity = useGravity;
                        }
                        // 从源武器获取完整特效设置
                        if (_sourceWeapon != null && _sourceWeapon.WeaponData != null && _sourceWeapon.WeaponData.UseProjectileSettings)
                        {
                            var settings = _sourceWeapon.WeaponData.ProjectileSettings;
                            if (settings != null)
                            {
                                _impactEffectPrefab = settings.ImpactEffectPrefab;
                                _explosionEffectPrefab = settings.ExplosionEffectPrefab;
                                _launchSound = settings.LaunchSound;
                                _impactSound = settings.ImpactSound;
                                _explosionSound = settings.ExplosionSound;
                                _bounceSound = settings.BounceSound;

                                if (_showDebugInfo)
                                    Debug.Log($"[标准投射物] 网络投射物特效配置完成: ImpactEffect={(_impactEffectPrefab != null ? _impactEffectPrefab.name : "NULL")}, ExplosionEffect={(_explosionEffectPrefab != null ? _explosionEffectPrefab.name : "NULL")}");
                            }
                        }

                        if (_showDebugInfo)
                            Debug.Log($"[标准投射物] ProjectileSettings网络配置完成，速度: {velocity}, 伤害: {_damage}, 生命周期: {_lifetime}");
                    }
                    else
                    {
                        // 只有基础数据，使用默认值
                        _damage = (float)data[3];
                        // 尝试从源武器获取完整特效设置（如果有的话）
                        if (_sourceWeapon != null && _sourceWeapon.WeaponData != null && _sourceWeapon.WeaponData.UseProjectileSettings)
                        {
                            var settings = _sourceWeapon.WeaponData.ProjectileSettings;
                            if (settings != null)
                            {
                                _impactEffectPrefab = settings.ImpactEffectPrefab;
                                _explosionEffectPrefab = settings.ExplosionEffectPrefab;
                                _launchSound = settings.LaunchSound;
                                _impactSound = settings.ImpactSound;
                                _explosionSound = settings.ExplosionSound;
                                _bounceSound = settings.BounceSound;

                                if (_showDebugInfo)
                                    Debug.Log($"[标准投射物] 基础格式网络投射物特效配置完成: ImpactEffect={(_impactEffectPrefab != null ? _impactEffectPrefab.name : "NULL")}, ExplosionEffect={(_explosionEffectPrefab != null ? _explosionEffectPrefab.name : "NULL")}");
                            }
                        }

                        if (_showDebugInfo)
                            Debug.Log($"[标准投射物] 基础网络配置完成，速度: {velocity}, 伤害: {_damage}");
                    }

                    // 应用速度（所有格式都需要）
                    if (_rigidbody != null)
                    {
                        _rigidbody.velocity = velocity;
                    }

                    _initialVelocity = velocity;
                    _speed = velocity.magnitude;
                }
                else
                {
                    Debug.LogWarning($"[标准投射物] 网络数据长度不足: {data.Length}，需要至少4个元素");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[标准投射物] 网络数据配置异常: {e.Message}");
                Debug.LogError($"[标准投射物] 数据长度: {data?.Length}, 异常堆栈: {e.StackTrace}");

                // 异常时使用默认配置
                if (data != null && data.Length >= 4)
                {
                    try
                    {
                        Vector3 velocity = new Vector3((float)data[0], (float)data[1], (float)data[2]);
                        _damage = (float)data[3];

                        if (_rigidbody != null)
                        {
                            _rigidbody.velocity = velocity;
                        }

                        _initialVelocity = velocity;
                        _speed = velocity.magnitude;

                        Debug.LogWarning("[标准投射物] 使用最小配置作为回退方案");
                    }
                    catch (System.Exception fallbackException)
                    {
                        Debug.LogError($"[标准投射物] 回退配置也失败: {fallbackException.Message}");
                    }
                }
            }
        }
        
        #endregion
        
        #region 碰撞处理实现
          /// <summary>
        /// 处理命中
        /// </summary>
        protected override bool ProcessHit(RaycastHit hit)
        {
            if (_hasHit && !_penetrateTargets) return true;
            
            Collider hitCollider = hit.collider;
            if (hitCollider == null) return false;
            
            // 记录命中位置
            Vector3 hitPoint = hit.point != Vector3.zero ? hit.point : transform.position;
            Vector3 hitNormal = hit.normal != Vector3.zero ? hit.normal : -transform.forward;
            
            // 处理伤害 - 使用基类的统一伤害处理方法
            bool shouldDestroy = ProcessDamage(hitCollider, hitPoint, hitNormal);
            
            // 播放撞击效果
            PlayImpactEffect(hitPoint, hitNormal);
            PlayImpactSound();

            // 处理爆炸
            if (_explodeOnImpact && !_hasExploded)
            {
                if (_explosionDelay > 0f)
                {
                    Invoke(nameof(Explode), _explosionDelay);
                }
                else
                {
                    Explode();
                }
            }
            
            // 触发命中事件（在ProjectileBase中处理）
            TriggerProjectileHit(hit); // 由基类处理事件触发
            
            // 标记已命中
            _hasHit = true;
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物] 命中 {hitCollider.name}，应该销毁: {shouldDestroy}");
            
            return shouldDestroy;
        }
          /// <summary>
        /// 处理伤害 - 重构为使用基类的统一伤害处理
        /// </summary>
        private bool ProcessDamage(Collider hitCollider, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (!_damageOnTouch) return true;
            
            // 检查是否为可伤害目标
            bool isDamageable = IsTargetDamageable(hitCollider);
            if (!isDamageable) return true;
            
            // 检查是否可以造成伤害
            if (!CanDamageTarget(hitCollider)) return true;
            
            // 检查爆头
            bool isHeadshot = IsHeadshot(hitCollider, hitPoint);
            
            // 使用基类的统一伤害处理方法
            bool damageDealt = ApplyDamageToTarget(hitCollider, hitPoint, hitNormal, isHeadshot);
            
            // 处理穿透
            if (_penetrateTargets && damageDealt)
            {
                _penetrationCount++;
                
                if (_penetrationCount >= _maxPenetrations)
                {
                    return true; // 达到最大穿透次数，销毁投射物
                }
                
                // 继续穿透，不销毁投射物
                return false;
            }
            
            return true; // 默认情况下销毁投射物
        }
        
        /// <summary>
        /// 检查目标是否可伤害
        /// </summary>
        private bool IsTargetDamageable(Collider hitCollider)
        {
            // 检查标签
            if (!string.IsNullOrEmpty(_damageableTag) && !hitCollider.CompareTag(_damageableTag))
            {
                return false;
            }
            
            // 检查层级
            if ((_damageableLayers.value & (1 << hitCollider.gameObject.layer)) == 0)
            {
                return false;
            }
            
            return true;
        }        
        #endregion

        #region 爆炸系统

        /// <summary>
        /// 爆炸处理 - 逐步迁移到DamageSystem
        /// </summary>
        private void Explode()
        {
            if (_hasExploded) return;
            _hasExploded = true;

            // 获取爆炸参数
            float explosionRadius = _sourceWeapon?.WeaponData?.ExplosionRadius ?? 0f;
            float explosionDamage = _sourceWeapon?.WeaponData?.ExplosionDamage ?? 0f;

            if (explosionRadius <= 0f || explosionDamage <= 0f)
            {
                if (_showDebugInfo)
                    Debug.Log("[标准投射物] 爆炸参数无效，跳过爆炸");
                return;
            }

            if (_showDebugInfo)
                Debug.Log($"[标准投射物] 爆炸: 半径={explosionRadius}, 伤害={explosionDamage}");

            // TODO: 当DamageSystem完全集成后，使用这个方法：
            DamageSystem.ApplyExplosionDamage(transform.position, explosionRadius, explosionDamage, _sourcePlayer, _damageableLayers, _sourceWeapon);

            // 当前使用本地爆炸处理逻辑
            Collider[] targets = Physics.OverlapSphere(transform.position, explosionRadius, _damageableLayers);

            foreach (Collider target in targets)
            {
                if (ShouldIgnoreCollision(target)) continue;

                // 计算距离衰减
                float distance = Vector3.Distance(transform.position, target.transform.position);
                float damageMultiplier = 1f - (distance / explosionRadius);
                damageMultiplier = Mathf.Clamp01(damageMultiplier);
                // 尝试Core接口优先
                DWHITE.IDamageable coreDamageable = target.GetComponent<DWHITE.IDamageable>();
                if (coreDamageable == null)
                {
                    coreDamageable = target.GetComponentInParent<DWHITE.IDamageable>();
                }

                if (coreDamageable != null)
                {
                    float finalDamage = explosionDamage * damageMultiplier;
                    Vector3 explosionDirection = (target.transform.position - transform.position).normalized;

                    coreDamageable.TakeDamage(finalDamage, target.transform.position, explosionDirection);

                    if (_showDebugInfo)
                        Debug.Log($"[标准投射物] 爆炸伤害对 {target.name}: {finalDamage:F1} (距离: {distance:F1})");
                }
                else
                {
                    // 回退到本地接口
                    IDamageable localDamageable = target.GetComponent<IDamageable>();
                    if (localDamageable == null)
                    {
                        localDamageable = target.GetComponentInParent<IDamageable>();
                    }
                    if (localDamageable != null)
                    {
                        // 使用适配器处理爆炸伤害
                        bool damageApplied = DWHITE.Weapons.DamageableAdapter.ApplyDamage(
                            target,
                            explosionDamage * damageMultiplier,
                            target.transform.position,
                            (target.transform.position - transform.position).normalized,
                            _sourcePlayer,
                            _sourceWeapon,
                            this
                        );

                        if (damageApplied && _showDebugInfo)
                            Debug.Log($"[标准投射物] 本地爆炸伤害对 {target.name}: {explosionDamage * damageMultiplier:F1}");
                    }
                }
            }
            // 播放爆炸效果和音效
            PlayExplosionEffect(transform.position);
            PlayExplosionSound();

            if (_showDebugInfo)
                Debug.Log($"[标准投射物] 爆炸，影响 {targets.Length} 个目标");

            // 销毁投射物
            DestroyProjectile();
        }        
        #endregion
        
        #region 调试
        
#if UNITY_EDITOR
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            
            // 绘制爆炸半径
            if (_explodeOnImpact && _sourceWeapon?.WeaponData != null)
            {
                float explosionRadius = _sourceWeapon.WeaponData.ExplosionRadius;
                if (explosionRadius > 0f)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(transform.position, explosionRadius);
                }
            }
        }
#endif        
        /// <summary>
        /// 强制启用调试信息（运行时调试用）
        /// </summary>
        [ContextMenu("启用详细调试")]
        public void EnableDetailedDebug()
        {
            _showDebugInfo = true;
            
            var networkSync = GetComponent<ProjectileNetworkSync>();
            if (networkSync != null)
            {
                networkSync.EnableDebugInfo(true);
            }
            
            Debug.Log($"[标准投射物] 调试已启用 - 所有者: {(photonView != null ? photonView.Owner?.NickName : "无")}, IsMine: {(photonView != null ? photonView.IsMine : false)}");
            Debug.Log($"[标准投射物] 当前位置: {transform.position}, 速度: {(_rigidbody != null ? _rigidbody.velocity : Vector3.zero)}");
            Debug.Log($"[标准投射物] Rigidbody运动学模式: {(_rigidbody != null ? _rigidbody.isKinematic : false)}");
        }
        
        /// <summary>
        /// 实时状态监控（可在Inspector中调用）
        /// </summary>
        [ContextMenu("显示实时状态")]
        public void ShowRealtimeStatus()
        {
            Debug.Log("=== 投射物实时状态 ===");
            Debug.Log($"GameObject: {gameObject.name}");
            Debug.Log($"位置: {transform.position}");
            Debug.Log($"速度: {(_rigidbody != null ? _rigidbody.velocity : Vector3.zero)} (大小: {(_rigidbody != null ? _rigidbody.velocity.magnitude : 0):F2})");
            Debug.Log($"是否运动学: {(_rigidbody != null ? _rigidbody.isKinematic : false)}");
            Debug.Log($"使用重力: {(_rigidbody != null ? _rigidbody.useGravity : false)}");
            
            if (photonView != null)
            {
                Debug.Log($"网络所有者: {(photonView.Owner != null ? photonView.Owner.NickName : "无所有者")}");
                Debug.Log($"是否本地控制: {photonView.IsMine}");
                Debug.Log($"ViewID: {photonView.ViewID}");
            }
            else
            {
                Debug.Log("无PhotonView组件");
            }
            
            var networkSync = GetComponent<ProjectileNetworkSync>();
            if (networkSync != null)
            {
                Debug.Log("网络同步组件: 存在");
            }
            else
            {
                Debug.Log("网络同步组件: 缺失!");
            }
            Debug.Log("===================");
        }
        #endregion
    }
}
