using UnityEngine;
using Photon.Pun;

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
                ConfigureFromNetworkData(photonView.InstantiationData);
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
            if (data.Length >= 4)
            {
                // 解析速度数据
                Vector3 velocity = new Vector3((float)data[0], (float)data[1], (float)data[2]);
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
                    }
                }
                
                // 应用速度
                if (_rigidbody != null)
                {
                    _rigidbody.velocity = velocity;
                }
                
                _initialVelocity = velocity;
                _speed = velocity.magnitude;
                
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物] 网络配置完成，速度: {velocity}, 伤害: {_damage}");
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
            
            // 处理伤害
            bool shouldDestroy = ProcessDamage(hitCollider, hitPoint);
            
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
            }            // 触发命中事件（在ProjectileBase中处理）
            // OnProjectileHit?.Invoke(this, hit); // 由基类处理事件触发
            
            // 标记已命中
            _hasHit = true;
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物] 命中 {hitCollider.name}，应该销毁: {shouldDestroy}");
            
            return shouldDestroy;
        }
        
        /// <summary>
        /// 处理伤害
        /// </summary>
        private bool ProcessDamage(Collider hitCollider, Vector3 hitPoint)
        {
            if (!_damageOnTouch) return true;
            
            // 检查是否为可伤害目标
            bool isDamageable = IsTargetDamageable(hitCollider);
            if (!isDamageable) return true;
            
            // 尝试造成伤害
            bool damageDealt = DealDamage(hitCollider, hitPoint);
            
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
        
        /// <summary>
        /// 造成伤害
        /// </summary>
        private bool DealDamage(Collider target, Vector3 hitPoint)
        {
            // 查找伤害接收器组件
            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable == null)
            {
                damageable = target.GetComponentInParent<IDamageable>();
            }
            
            if (damageable != null)
            {
                // 创建伤害信息
                DamageInfo damageInfo = new DamageInfo
                {
                    damage = _damage,
                    damageType = DamageType.Projectile,
                    source = _sourcePlayer,
                    weapon = _sourceWeapon,
                    hitPoint = hitPoint,
                    hitDirection = Velocity.normalized,
                    projectile = this
                };
                
                // 造成伤害
                damageable.TakeDamage(damageInfo);
                
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物] 对 {target.name} 造成 {_damage} 点伤害");
                
                return true;
            }
            else
            {
                if (_showDebugInfo)
                    Debug.Log($"[标准投射物] {target.name} 不是可伤害目标");
            }
            
            return false;
        }
        
        #endregion
        
        #region 爆炸系统
        
        /// <summary>
        /// 爆炸
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
            
            // 找到爆炸范围内的所有目标
            Collider[] targets = Physics.OverlapSphere(transform.position, explosionRadius, _damageableLayers);
            
            foreach (Collider target in targets)
            {
                if (ShouldIgnoreCollision(target)) continue;
                
                // 计算距离衰减
                float distance = Vector3.Distance(transform.position, target.transform.position);
                float damageMultiplier = 1f - (distance / explosionRadius);
                damageMultiplier = Mathf.Clamp01(damageMultiplier);
                
                // 造成爆炸伤害
                IDamageable damageable = target.GetComponent<IDamageable>();
                if (damageable == null)
                {
                    damageable = target.GetComponentInParent<IDamageable>();
                }
                
                if (damageable != null)
                {
                    DamageInfo explosionDamageInfo = new DamageInfo
                    {
                        damage = explosionDamage * damageMultiplier,
                        damageType = DamageType.Explosion,
                        source = _sourcePlayer,
                        weapon = _sourceWeapon,
                        hitPoint = target.transform.position,
                        hitDirection = (target.transform.position - transform.position).normalized,
                        projectile = this
                    };
                    
                    damageable.TakeDamage(explosionDamageInfo);
                }
            }
            
            // 播放爆炸效果
            PlayExplosionEffect();
            
            if (_showDebugInfo)
                Debug.Log($"[标准投射物] 爆炸，影响 {targets.Length} 个目标");
            
            // 销毁投射物
            DestroyProjectile();
        }
        
        /// <summary>
        /// 播放爆炸效果
        /// </summary>
        private void PlayExplosionEffect()
        {
            // 这里可以实例化爆炸特效
            // 例如：粒子系统、音效等
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
        
        #endregion
    }
    
    #region 伤害系统接口
    
    /// <summary>
    /// 伤害类型枚举
    /// </summary>
    public enum DamageType
    {
        Projectile,    // 投射物伤害
        Explosion,     // 爆炸伤害
        Hitscan,       // 即时命中伤害
        Environmental  // 环境伤害
    }
    
    /// <summary>
    /// 伤害信息结构
    /// </summary>
    [System.Serializable]
    public struct DamageInfo
    {
        public float damage;
        public DamageType damageType;
        public GameObject source;
        public WeaponBase weapon;
        public Vector3 hitPoint;
        public Vector3 hitDirection;
        public ProjectileBase projectile;
    }
    
    /// <summary>
    /// 可伤害对象接口
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(DamageInfo damageInfo);
        float GetHealth();
        bool IsAlive();
    }
    
    #endregion
}
