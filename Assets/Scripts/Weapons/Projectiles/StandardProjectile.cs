using UnityEngine;
using Photon.Pun;
using DWHITE.Weapons.Network;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 标准投射物实现
    /// 通用的投射物行为，适用于大多数武器
    /// </summary>
    public class StandardProjectile : ProjectileBase
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

            if (_showDebugInfo)
            {
                Debug.Log($"[标准投射物] 初始化完成 - 是否网络对象: {(photonView != null)}, 所有者: {(photonView?.Owner?.NickName ?? "本地")}");
            }
        }
        
        #endregion
        
        #region 网络初始化
        
        /// <summary>
        /// 从网络数据配置投射物（仅供ProjectileNetworkSync调用）
        /// </summary>
        public void ConfigureFromNetworkData(object[] data)
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
        protected override bool ProcessHit(Collider hitCollider, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-碰撞] ===== 开始处理碰撞 =====");
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-碰撞] 命中目标: {hitCollider.name}");
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-碰撞] 碰撞点: {hitPoint}");
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-碰撞] 法线: {hitNormal}");
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-碰撞] 已命中状态: {_hasHit}, 可穿透: {_penetrateTargets}");
            
            if (_hasHit && !_penetrateTargets) 
            {
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物-碰撞] 已命中且不可穿透，直接销毁");
                return true;
            }
            
            if (hitCollider == null) 
            {
                if (_showDebugInfo)
                    Debug.LogError($"[标准投射物-碰撞] hitCollider为空，返回false");
                return false;
            }
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-碰撞] 开始处理伤害逻辑");
            
            // 处理伤害 - 使用基类的统一伤害处理方法
            bool shouldDestroy = ProcessDamage(hitCollider, hitPoint, hitNormal);
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-碰撞] 伤害处理结果: shouldDestroy = {shouldDestroy}");
            
            // 播放撞击效果
            PlayImpactEffect(hitPoint, hitNormal);
            PlayImpactSound();

            // 处理爆炸
            if (_explodeOnImpact && !_hasExploded)
            {
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物-碰撞] 触发爆炸逻辑，延迟: {_explosionDelay}秒");
                
                if (_explosionDelay > 0f)
                {
                    Invoke(nameof(Explode), _explosionDelay);
                }
                else
                {
                    Explode();
                }
            }
            else if (_explodeOnImpact && _hasExploded)
            {
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物-碰撞] 已经爆炸过，跳过爆炸逻辑");
            }
            
            // 触发命中事件（在ProjectileBase中处理）
            TriggerProjectileHit(hitCollider, hitPoint, hitNormal); // 由基类处理事件触发
            
            // 标记已命中
            _hasHit = true;
            
            if (_showDebugInfo)
            {
                Debug.Log($"[标准投射物-碰撞] 命中处理完成");
                Debug.Log($"[标准投射物-碰撞] 最终销毁决定: {shouldDestroy}");
                Debug.Log($"[标准投射物-碰撞] ===== 碰撞处理结束 =====");
            }
            
            return shouldDestroy;
        }
          /// <summary>
        /// 处理伤害 - 重构为使用基类的统一伤害处理
        /// </summary>
        private bool ProcessDamage(Collider hitCollider, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-伤害] ===== 开始伤害处理 =====");
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-伤害] 目标: {hitCollider.name}");
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-伤害] 目标标签: {hitCollider.tag}");
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-伤害] 目标层级: {hitCollider.gameObject.layer}");
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-伤害] 伤害开关: {_damageOnTouch}");
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-伤害] 投射物伤害值: {_damage}");

            if (!_damageOnTouch) 
            {
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物-伤害] _damageOnTouch = false，跳过伤害处理");
                return true;
            }

            // 检查是否为可伤害目标
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-伤害] 检查目标是否符合伤害条件...");
            
            bool isDamageable = IsTargetDamageable(hitCollider);
            if (!isDamageable) 
            {
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物-伤害] ❌ 目标 {hitCollider.name} 不符合伤害条件 (标签: {hitCollider.tag}, 层级: {hitCollider.gameObject.layer})");
                return true;
            }
            else
            {
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物-伤害] ✅ 目标符合伤害条件");
            }

            // 检查是否可以造成伤害
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-伤害] 检查目标是否可以被伤害...");
            
            if (!CanDamageTarget(hitCollider)) 
            {
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物-伤害] ❌ 目标 {hitCollider.name} 无法被伤害 (没有IDamageable或已死亡)");
                return true;
            }
            else
            {
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物-伤害] ✅ 目标可以被伤害");
            }

            // 检查爆头
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-伤害] 检查是否爆头...");
            
            bool isHeadshot = IsHeadshot(hitCollider, hitPoint);
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-伤害] 爆头检测结果: {isHeadshot}");

            if (_showDebugInfo)
            {
                Debug.Log($"[标准投射物-伤害] ===== 准备应用伤害 =====");
                Debug.Log($"[标准投射物-伤害] 目标: {hitCollider.name}");
                Debug.Log($"[标准投射物-伤害] 基础伤害: {_damage}");
                Debug.Log($"[标准投射物-伤害] 是否爆头: {isHeadshot}");
                Debug.Log($"[标准投射物-伤害] 命中点: {hitPoint}");
                Debug.Log($"[标准投射物-伤害] 法线: {hitNormal}");
                Debug.Log($"[标准投射物-伤害] 来源武器: {(_sourceWeapon?.WeaponData?.WeaponName ?? "Unknown")}");
                Debug.Log($"[标准投射物-伤害] 来源玩家: {(_sourcePlayer?.name ?? "Unknown")}");
            }

            // 使用基类的统一伤害处理方法
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-伤害] 调用ApplyDamageToTarget...");
            
            bool damageDealt = ApplyDamageToTarget(hitCollider, hitPoint, hitNormal, isHeadshot);
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-伤害] ApplyDamageToTarget返回结果: {(damageDealt ? "✅ 成功" : "❌ 失败")}");

            // 处理穿透
            if (_penetrateTargets && damageDealt)
            {
                _penetrationCount++;
                
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物-伤害] 穿透逻辑 - 穿透计数: {_penetrationCount}/{_maxPenetrations}");
                
                if (_penetrationCount >= _maxPenetrations)
                {
                    if (_showDebugInfo)
                        Debug.Log($"[标准投射物-伤害] 达到最大穿透次数，销毁投射物");
                    return true; // 达到最大穿透次数，销毁投射物
                }
                
                // 继续穿透，不销毁投射物
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物-伤害] 继续穿透，投射物不销毁");
                return false;
            }
            else if (_penetrateTargets && !damageDealt)
            {
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物-伤害] 穿透武器但伤害失败，仍然销毁投射物");
            }

            if (_showDebugInfo)
            {
                Debug.Log($"[标准投射物-伤害] 伤害处理完成，返回true（销毁投射物）");
                Debug.Log($"[标准投射物-伤害] ===== 伤害处理结束 =====");
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
        /// 爆炸处理 - 简化为本地直接处理
        /// </summary>
        private void Explode()
        {
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-爆炸] ===== 开始爆炸处理 =====");
            
            if (_hasExploded) 
            {
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物-爆炸] 已经爆炸过，直接返回");
                return;
            }
            
            _hasExploded = true;
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-爆炸] 爆炸位置: {transform.position}");

            // 获取爆炸参数
            float explosionRadius = _sourceWeapon?.WeaponData?.ExplosionRadius ?? 0f;
            float explosionDamage = _sourceWeapon?.WeaponData?.ExplosionDamage ?? 0f;

            if (_showDebugInfo)
            {
                Debug.Log($"[标准投射物-爆炸] 爆炸半径: {explosionRadius}");
                Debug.Log($"[标准投射物-爆炸] 爆炸伤害: {explosionDamage}");
                Debug.Log($"[标准投射物-爆炸] 伤害层级: {_damageableLayers}");
            }

            if (explosionRadius <= 0f || explosionDamage <= 0f)
            {
                if (_showDebugInfo)
                    Debug.Log("[标准投射物-爆炸] ❌ 爆炸参数无效，跳过爆炸");
                return;
            }

            // 本地爆炸处理逻辑
            Collider[] targets = Physics.OverlapSphere(transform.position, explosionRadius, _damageableLayers);
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物-爆炸] 检测到 {targets.Length} 个潜在目标");

            int damageCount = 0;
            foreach (Collider target in targets)
            {
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物-爆炸] 检查目标: {target.name}");
                
                if (ShouldIgnoreCollision(target)) 
                {
                    if (_showDebugInfo)
                        Debug.Log($"[标准投射物-爆炸] 跳过目标 {target.name} (碰撞忽略规则)");
                    continue;
                }

                // 计算距离衰减
                float distance = Vector3.Distance(transform.position, target.transform.position);
                float damageMultiplier = 1f - (distance / explosionRadius);
                damageMultiplier = Mathf.Clamp01(damageMultiplier);
                float finalDamage = explosionDamage * damageMultiplier;
                
                if (_showDebugInfo)
                {
                    Debug.Log($"[标准投射物-爆炸] 目标 {target.name}:");
                    Debug.Log($"[标准投射物-爆炸]   距离: {distance:F2}");
                    Debug.Log($"[标准投射物-爆炸]   伤害倍数: {damageMultiplier:F2}");
                    Debug.Log($"[标准投射物-爆炸]   最终伤害: {finalDamage:F2}");
                }
                
                // 查找IDamageable组件并直接应用伤害
                DWHITE.IDamageable coreDamageable = target.GetComponent<DWHITE.IDamageable>();
                if (coreDamageable == null)
                {
                    coreDamageable = target.GetComponentInParent<DWHITE.IDamageable>();
                    if (_showDebugInfo)
                        Debug.Log($"[标准投射物-爆炸] 在父对象中查找IDamageable: {(coreDamageable != null ? "找到" : "未找到")}");
                }

                if (coreDamageable != null && coreDamageable.IsAlive)
                {
                    Vector3 explosionDirection = (target.transform.position - transform.position).normalized;
                    
                    if (_showDebugInfo)
                    {
                        Debug.Log($"[标准投射物-爆炸] ✅ 对 {target.name} 应用爆炸伤害");
                        Debug.Log($"[标准投射物-爆炸]   爆炸方向: {explosionDirection}");
                        Debug.Log($"[标准投射物-爆炸]   目标生命值(应用前): {coreDamageable.GetCurrentHealth():F1}");
                    }
                    
                    coreDamageable.TakeDamage(finalDamage, target.transform.position, explosionDirection);
                    damageCount++;

                    if (_showDebugInfo)
                    {
                        Debug.Log($"[标准投射物-爆炸] ✅ 爆炸伤害应用完成");
                        Debug.Log($"[标准投射物-爆炸]   目标生命值(应用后): {coreDamageable.GetCurrentHealth():F1}");
                        Debug.Log($"[标准投射物-爆炸]   实际伤害: {finalDamage:F1}");
                    }
                }
                else if (coreDamageable != null && !coreDamageable.IsAlive)
                {
                    if (_showDebugInfo)
                        Debug.Log($"[标准投射物-爆炸] ❌ 目标 {target.name} 已死亡，跳过伤害");
                }
                else
                {
                    if (_showDebugInfo)
                        Debug.Log($"[标准投射物-爆炸] 尝试本地IDamageable接口...");
                    
                    // 回退到本地接口
                    IDamageable localDamageable = target.GetComponent<IDamageable>();
                    if (localDamageable == null)
                    {
                        localDamageable = target.GetComponentInParent<IDamageable>();
                        if (_showDebugInfo)
                            Debug.Log($"[标准投射物-爆炸] 在父对象中查找本地IDamageable: {(localDamageable != null ? "找到" : "未找到")}");
                    }
                    
                    if (localDamageable != null)
                    {
                        // 直接调用IDamageable接口处理爆炸伤害
                        Vector3 explosionDirection = (target.transform.position - transform.position).normalized;
                        localDamageable.TakeDamage(finalDamage, target.transform.position, explosionDirection);
                        damageCount++;

                        if (_showDebugInfo)
                            Debug.Log($"[标准投射物-爆炸] ✅ 本地爆炸伤害对 {target.name}: {finalDamage:F1}");
                    }
                    else
                    {
                        if (_showDebugInfo)
                            Debug.Log($"[标准投射物-爆炸] ❌ 目标 {target.name} 没有IDamageable组件");
                    }
                }
            }
            
            if (_showDebugInfo)
            {
                Debug.Log($"[标准投射物-爆炸] 爆炸处理完成");
                Debug.Log($"[标准投射物-爆炸] 总共对 {damageCount} 个目标造成伤害");
                Debug.Log($"[标准投射物-爆炸] ===== 爆炸处理结束 =====");
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
