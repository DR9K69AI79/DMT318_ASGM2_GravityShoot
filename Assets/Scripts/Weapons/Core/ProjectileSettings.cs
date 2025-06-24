using UnityEngine;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 投射物相关设置的数据结构
    /// 用于分离投射物配置，减少WeaponData的职责
    /// </summary>
    [System.Serializable]
    public class ProjectileSettings
    {
        [Header("━━━ 投射物预制体 ━━━")]
        [Tooltip("投射物预制体，必须包含ProjectileBase组件")]
        [SerializeField] private GameObject _projectilePrefab;
        
        [Header("━━━ 基础投射物设置 ━━━")]
        [Tooltip("投射物飞行速度 (m/s)，推荐范围: 10-200")]
        [Range(1f, 500f)]
        [SerializeField] private float _speed = 20f;
        [Tooltip("基础伤害值，推荐范围: 5-100")]
        [Range(0.1f, 1000f)]
        [SerializeField] private float _damage = 20f;
        [Tooltip("最大射程 (m)，超出后投射物自动销毁")]
        [Range(5f, 2000f)]
        [SerializeField] private float _maxRange = 100f;
        [Tooltip("投射物生命周期 (秒)，时间到后自动销毁")]
        [Range(0.5f, 60f)]
        [SerializeField] private float _lifetime = 10f;
        
        [Space(10)]
        [Header("━━━ 物理设置 ━━━")]
        [Tooltip("投射物质量，影响物理交互和惯性")]
        [Range(0.1f, 100f)]
        [SerializeField] private float _mass = 1f;
        [Tooltip("空气阻力系数，越大减速越快")]
        [Range(0f, 10f)]
        [SerializeField] private float _drag = 0f;
        [Tooltip("是否受重力影响（抛物线弹道）")]
        [SerializeField] private bool _useGravity = false;
        [Tooltip("重力缩放倍数，1.0为标准重力")]
        [Range(0f, 5f)]
        [SerializeField] private float _gravityScale = 1f;
          [Space(10)]
        [Header("━━━ 弹跳设置 ━━━")]
        [Tooltip("最大弹跳次数，0表示不弹跳")]
        [Range(0, 20)]
        [SerializeField] private int _maxBounceCount = 0;
        [Tooltip("每次弹跳的能量损失率 (0=无损失, 1=完全损失)")]
        [Range(0f, 1f)]
        [SerializeField] private float _bounceEnergyLoss = 0.1f;
        [Tooltip("可弹跳的图层遮罩")]
        [SerializeField] private LayerMask _bounceLayerMask = -1;
        
        [Space(10)]
        [Header("━━━ 黑洞引力设置 ━━━")]
        [Tooltip("引力强度，0表示无引力效果")]
        [Range(0f, 200f)]
        [SerializeField] private float _gravityForce = 0f;
        [Tooltip("引力作用半径 (m)")]
        [Range(0f, 100f)]
        [SerializeField] private float _gravityRadius = 5f;
        [Tooltip("是否对其他投射物产生引力影响")]
        [SerializeField] private bool _affectOtherProjectiles = false;
        
        [Space(10)]
        [Header("━━━ 爆炸设置 ━━━")]
        [Tooltip("爆炸半径 (m)，0表示无爆炸")]
        [Range(0f, 50f)]
        [SerializeField] private float _explosionRadius = 0f;
        [Tooltip("爆炸伤害值")]
        [Range(0f, 1000f)]
        [SerializeField] private float _explosionDamage = 0f;
        [Tooltip("是否对队友造成伤害")]
        [SerializeField] private bool _friendlyFire = false;
        [Tooltip("爆炸影响的图层遮罩")]
        [SerializeField] private LayerMask _explosionLayerMask = -1;
          [Space(10)]
        [Header("━━━ 穿透设置 ━━━")]
        [Tooltip("穿透次数，0表示击中后立即销毁")]
        [Range(0, 10)]
        [SerializeField] private int _penetrationCount = 0;
        [Tooltip("每次穿透的伤害衰减率 (0=无衰减, 1=完全衰减)")]
        [Range(0f, 1f)]
        [SerializeField] private float _penetrationDamageReduction = 0.2f;
          [Space(10)]
        [Header("━━━ 视觉效果 ━━━")]
        [Tooltip("撞击特效预制体")]
        [SerializeField] private GameObject _impactEffectPrefab;
        [Tooltip("爆炸特效预制体")]
        [SerializeField] private GameObject _explosionEffectPrefab;
        
        [Space(10)]
        [Header("━━━ 音效系统 ━━━")]
        [Tooltip("发射音效")]
        [SerializeField] private AudioClip _launchSound;
        [Tooltip("撞击音效")]
        [SerializeField] private AudioClip _impactSound;
        [Tooltip("爆炸音效")]
        [SerializeField] private AudioClip _explosionSound;
        [Tooltip("弹跳音效")]
        [SerializeField] private AudioClip _bounceSound;
        
        [Space(10)]
        [Header("━━━ 网络设置 ━━━")]
        [Tooltip("是否同步投射物移动（性能消耗较大）")]
        [SerializeField] private bool _syncMovement = true;
        [Tooltip("网络同步间隔 (秒)，越小越精确但消耗更多带宽")]
        [Range(0.02f, 1f)]
        [SerializeField] private float _syncInterval = 0.1f;
        [Tooltip("是否启用客户端预测（减少网络延迟感）")]
        [SerializeField] private bool _enablePrediction = true;
        
        #region 属性访问器
        
        // 基础设置
        public GameObject ProjectilePrefab => _projectilePrefab;
        public float Speed => _speed;
        public float Damage => _damage;
        public float MaxRange => _maxRange;
        public float Lifetime => _lifetime;
        
        // 物理设置
        public float Mass => _mass;
        public float Drag => _drag;
        public bool UseGravity => _useGravity;
        public float GravityScale => _gravityScale;
        
        // 弹跳设置
        public int MaxBounceCount => _maxBounceCount;
        public float BounceEnergyLoss => _bounceEnergyLoss;
        public LayerMask BounceLayerMask => _bounceLayerMask;
        
        // 引力设置
        public float GravityForce => _gravityForce;
        public float GravityRadius => _gravityRadius;
        public bool AffectOtherProjectiles => _affectOtherProjectiles;
        
        // 爆炸设置
        public float ExplosionRadius => _explosionRadius;
        public float ExplosionDamage => _explosionDamage;
        public bool FriendlyFire => _friendlyFire;
        public LayerMask ExplosionLayerMask => _explosionLayerMask;
        
        // 穿透设置
        public int PenetrationCount => _penetrationCount;
        public float PenetrationDamageReduction => _penetrationDamageReduction;
          // 视觉效果
        public GameObject ImpactEffectPrefab => _impactEffectPrefab;
        public GameObject ExplosionEffectPrefab => _explosionEffectPrefab;
        
        // 音效
        public AudioClip LaunchSound => _launchSound;
        public AudioClip ImpactSound => _impactSound;
        public AudioClip ExplosionSound => _explosionSound;
        public AudioClip BounceSound => _bounceSound;
        
        // 网络设置
        public bool SyncMovement => _syncMovement;
        public float SyncInterval => _syncInterval;
        public bool EnablePrediction => _enablePrediction;
        
        #endregion
        
        #region 计算属性
        
        /// <summary>
        /// 是否有弹跳效果
        /// </summary>
        public bool HasBounce => _maxBounceCount > 0;
        
        /// <summary>
        /// 是否有引力效果
        /// </summary>
        public bool HasGravityEffect => _gravityForce > 0f;
        
        /// <summary>
        /// 是否有爆炸效果
        /// </summary>
        public bool HasExplosion => _explosionRadius > 0f && _explosionDamage > 0f;
        
        /// <summary>
        /// 是否有穿透效果
        /// </summary>
        public bool HasPenetration => _penetrationCount > 0;
        
        /// <summary>
        /// 计算指定距离下的伤害（考虑距离衰减）
        /// </summary>
        public float CalculateDamageAtDistance(float distance)
        {
            if (_maxRange <= 0f) return _damage;
            
            if (distance >= _maxRange) return 0f;
            
            // 线性衰减模型，可以根据需要改为其他衰减模式
            float damageRatio = 1f - (distance / _maxRange);
            return _damage * damageRatio;
        }
        
        /// <summary>
        /// 计算穿透后的伤害
        /// </summary>
        public float CalculatePenetrationDamage(int penetrationIndex)
        {
            if (penetrationIndex <= 0) return _damage;
            
            float damageReduction = Mathf.Pow(1f - _penetrationDamageReduction, penetrationIndex);
            return _damage * damageReduction;
        }
        
        #endregion
        
        #region 验证
        
#if UNITY_EDITOR
        public void OnValidate()
        {
            // 验证投射物预制体
            if (_projectilePrefab != null)
            {
                var projectileBase = _projectilePrefab.GetComponent<ProjectileBase>();
                if (projectileBase == null)
                {
                    UnityEngine.Debug.LogError($"投射物预制体 {_projectilePrefab.name} 缺少 ProjectileBase 组件");
                }
            }
            
            // 确保数值在合理范围内
            _speed = Mathf.Max(0.1f, _speed);
            _damage = Mathf.Max(0f, _damage);
            _maxRange = Mathf.Max(0f, _maxRange);
            _lifetime = Mathf.Max(0.1f, _lifetime);
            _mass = Mathf.Max(0.01f, _mass);
            _drag = Mathf.Max(0f, _drag);
            _gravityScale = Mathf.Max(0f, _gravityScale);
            _maxBounceCount = Mathf.Max(0, _maxBounceCount);
            _bounceEnergyLoss = Mathf.Clamp01(_bounceEnergyLoss);
            _gravityForce = Mathf.Max(0f, _gravityForce);
            _gravityRadius = Mathf.Max(0f, _gravityRadius);
            _explosionRadius = Mathf.Max(0f, _explosionRadius);
            _explosionDamage = Mathf.Max(0f, _explosionDamage);
            _penetrationCount = Mathf.Max(0, _penetrationCount);
            _penetrationDamageReduction = Mathf.Clamp01(_penetrationDamageReduction);
            _syncInterval = Mathf.Max(0.01f, _syncInterval);
        }
#endif
        
        #endregion
        
        #region 默认预设
        
        /// <summary>
        /// 创建标准投射物配置
        /// </summary>
        public static ProjectileSettings CreateStandard()
        {
            var settings = new ProjectileSettings();
            settings._speed = 20f;
            settings._damage = 20f;
            settings._maxRange = 100f;
            settings._lifetime = 10f;
            return settings;
        }
        
        /// <summary>
        /// 创建爆炸投射物配置
        /// </summary>
        public static ProjectileSettings CreateExplosive()
        {
            var settings = CreateStandard();
            settings._explosionRadius = 5f;
            settings._explosionDamage = 30f;
            settings._useGravity = true;
            settings._gravityScale = 0.5f;
            return settings;
        }
        
        /// <summary>
        /// 创建弹跳投射物配置
        /// </summary>
        public static ProjectileSettings CreateBouncy()
        {
            var settings = CreateStandard();
            settings._maxBounceCount = 3;
            settings._bounceEnergyLoss = 0.2f;
            return settings;
        }
        
        /// <summary>
        /// 创建引力投射物配置
        /// </summary>
        public static ProjectileSettings CreateGravity()
        {
            var settings = CreateStandard();
            settings._gravityForce = 10f;
            settings._gravityRadius = 8f;
            settings._affectOtherProjectiles = true;
            return settings;
        }
        
        #endregion
        
        #region 迁移工具
        
        /// <summary>
        /// 从旧版本的ProjectileWeapon配置迁移到ProjectileSettings
        /// </summary>
        /// <param name="projectilePrefab">投射物预制体</param>
        /// <param name="speed">飞行速度</param>
        /// <param name="damage">伤害值</param>
        /// <param name="range">射程</param>
        public static ProjectileSettings CreateFromLegacyConfiguration(
            GameObject projectilePrefab, 
            float speed = 20f, 
            float damage = 20f, 
            float range = 100f)
        {
            var settings = new ProjectileSettings();
            settings._projectilePrefab = projectilePrefab;
            settings._speed = speed;
            settings._damage = damage;
            settings._maxRange = range;
            settings._lifetime = 10f;
            return settings;
        }
        
        #endregion
    }
}
