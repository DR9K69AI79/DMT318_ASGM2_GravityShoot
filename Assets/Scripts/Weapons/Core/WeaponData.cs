using UnityEngine;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 武器配置数据 ScriptableObject
    /// 数据驱动的武器参数配置，支持设计师调参
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "GravityShoot/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [Header("基础信息")]
        [SerializeField] private string _weaponName = "New Weapon";
        [SerializeField] private string _description = "武器描述";
        [SerializeField] private Sprite _weaponIcon;
        
        [Header("发射设置")]
        [SerializeField] private float _fireRate = 1f; // 次/秒
        [SerializeField] private bool _automatic = false;
        [SerializeField] private float _projectileSpeed = 20f;
        [SerializeField] private int _projectilesPerShot = 1; // 散弹枪支持
        [SerializeField] private float _spreadAngle = 0f; // 散射角度
        
        [Header("弹药系统")]
        [SerializeField] private int _magazineSize = 30; // 0 = 无限弹药
        [SerializeField] private float _reloadTime = 1.5f;
        [SerializeField] private bool _infiniteAmmo = false;
        
        [Header("伤害设置")]
        [SerializeField] private float _damage = 20f;
        [SerializeField] private float _maxRange = 100f;
        [SerializeField] private bool _canHeadshot = true;
        [SerializeField] private float _headshotMultiplier = 2f;
        
        [Header("特殊效果")]
        [SerializeField] private int _maxBounceCount = 0; // 弹跳次数
        [SerializeField] private float _bounceEnergyLoss = 0.1f; // 弹跳能量损失
        [SerializeField] private float _gravityForce = 0f; // 黑洞吸力
        [SerializeField] private float _gravityRadius = 5f; // 引力作用半径
        [SerializeField] private float _explosionRadius = 0f; // 爆炸半径
        [SerializeField] private float _explosionDamage = 0f; // 爆炸伤害
        
        [Header("音效")]
        [SerializeField] private AudioClip _fireSound;
        [SerializeField] private AudioClip _reloadSound;
        [SerializeField] private AudioClip _emptySound;
        
        [Header("视觉效果")]
        [SerializeField] private GameObject _muzzleFlashPrefab;
        [SerializeField] private GameObject _impactEffectPrefab;
        [SerializeField] private GameObject _trailEffectPrefab;
        
        [Header("后坐力")]
        [SerializeField] private Vector2 _recoilPattern = new Vector2(1f, 2f); // X: 水平, Y: 垂直
        [SerializeField] private float _recoilRecoveryTime = 0.5f;
        
        [Header("网络设置")]
        [SerializeField] private bool _syncProjectiles = true; // 是否同步投射物
        [SerializeField] private int _networkPriority = 1; // 网络优先级
        
        #region 属性访问器
        
        public string WeaponName => _weaponName;
        public string Description => _description;
        public Sprite WeaponIcon => _weaponIcon;
        
        // 发射设置
        public float FireRate => _fireRate;
        public bool Automatic => _automatic;
        public float ProjectileSpeed => _projectileSpeed;
        public int ProjectilesPerShot => _projectilesPerShot;
        public float SpreadAngle => _spreadAngle;
        
        // 弹药系统
        public int MagazineSize => _magazineSize;
        public float ReloadTime => _reloadTime;
        public bool InfiniteAmmo => _infiniteAmmo;
        
        // 伤害设置
        public float Damage => _damage;
        public float MaxRange => _maxRange;
        public bool CanHeadshot => _canHeadshot;
        public float HeadshotMultiplier => _headshotMultiplier;
        
        // 特殊效果
        public int MaxBounceCount => _maxBounceCount;
        public float BounceEnergyLoss => _bounceEnergyLoss;
        public float GravityForce => _gravityForce;
        public float GravityRadius => _gravityRadius;
        public float ExplosionRadius => _explosionRadius;
        public float ExplosionDamage => _explosionDamage;
        
        // 音效
        public AudioClip FireSound => _fireSound;
        public AudioClip ReloadSound => _reloadSound;
        public AudioClip EmptySound => _emptySound;
        
        // 视觉效果
        public GameObject MuzzleFlashPrefab => _muzzleFlashPrefab;
        public GameObject ImpactEffectPrefab => _impactEffectPrefab;
        public GameObject TrailEffectPrefab => _trailEffectPrefab;
        
        // 后坐力
        public Vector2 RecoilPattern => _recoilPattern;
        public float RecoilRecoveryTime => _recoilRecoveryTime;
        
        // 网络设置
        public bool SyncProjectiles => _syncProjectiles;
        public int NetworkPriority => _networkPriority;
        
        #endregion
        
        #region 计算属性
        
        /// <summary>
        /// 射击间隔时间（秒）
        /// </summary>
        public float FireInterval => _fireRate > 0 ? 1f / _fireRate : 0f;
        
        /// <summary>
        /// 是否是无限弹药武器
        /// </summary>
        public bool HasInfiniteAmmo => _infiniteAmmo || _magazineSize <= 0;
        
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
        /// 是否是散弹武器
        /// </summary>
        public bool IsSpreadWeapon => _projectilesPerShot > 1;
        
        #endregion
        
        #region 验证
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // 确保数值在合理范围内
            _fireRate = Mathf.Max(0.1f, _fireRate);
            _projectileSpeed = Mathf.Max(0.1f, _projectileSpeed);
            _damage = Mathf.Max(0f, _damage);
            _magazineSize = Mathf.Max(0, _magazineSize);
            _reloadTime = Mathf.Max(0f, _reloadTime);
            _maxBounceCount = Mathf.Max(0, _maxBounceCount);
            _bounceEnergyLoss = Mathf.Clamp01(_bounceEnergyLoss);
            _gravityForce = Mathf.Max(0f, _gravityForce);
            _gravityRadius = Mathf.Max(0f, _gravityRadius);
            _explosionRadius = Mathf.Max(0f, _explosionRadius);
            _explosionDamage = Mathf.Max(0f, _explosionDamage);
            _projectilesPerShot = Mathf.Max(1, _projectilesPerShot);
            _spreadAngle = Mathf.Clamp(_spreadAngle, 0f, 90f);
            _headshotMultiplier = Mathf.Max(1f, _headshotMultiplier);
            _recoilRecoveryTime = Mathf.Max(0.1f, _recoilRecoveryTime);
            _networkPriority = Mathf.Clamp(_networkPriority, 1, 10);
        }
#endif
        
        #endregion
    }
}
