using UnityEngine;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 武器配置数据 ScriptableObject
    /// 数据驱动的武器参数配置，支持设计师调参
    /// 重构后采用分层配置结构，减少职责耦合，已彻底迁移到ProjectileSettings
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "GravityShoot/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [Header("━━━ 基础信息 ━━━")]
        [SerializeField] private string _weaponName = "New Weapon";
        [Tooltip("武器的详细描述，显示在UI中")]
        [TextArea(2, 4)]
        [SerializeField] private string _description = "武器描述";
        [Tooltip("武器图标，显示在UI界面")]
        [SerializeField] private Sprite _weaponIcon;
        
        [Space(10)]
        [Header("━━━ 发射设置 ━━━")]
        [Tooltip("每秒发射次数 (推荐范围: 0.5-10)")]
        [Range(0.1f, 15f)]
        [SerializeField] private float _fireRate = 1f;
        [Tooltip("是否为全自动武器（按住鼠标连续射击）")]
        [SerializeField] private bool _automatic = false;
        [Tooltip("单次射击的投射物数量（散弹枪效果，推荐: 1-12）")]
        [Range(1, 20)]
        [SerializeField] private int _projectilesPerShot = 1;
        [Tooltip("散射角度，度数（推荐: 0-45度）")]
        [Range(0f, 90f)]
        [SerializeField] private float _spreadAngle = 0f;
        [Tooltip("射击精度，1=完全精确，0=完全随机（推荐: 0.7-1.0）")]
        [Range(0f, 1f)]
        [SerializeField] private float _accuracy = 1f;
        
        [Space(10)]
        [Header("━━━ 弹药系统 ━━━")]
        [Tooltip("弹夹容量，0表示无限弹药（推荐: 10-100）")]
        [Range(0, 200)]
        [SerializeField] private int _magazineSize = 30;
        [Tooltip("装弹时间，秒（推荐: 0.5-5.0秒）")]
        [Range(0.1f, 10f)]
        [SerializeField] private float _reloadTime = 1.5f;
        [Tooltip("是否拥有无限弹药（测试模式）")]
        [SerializeField] private bool _infiniteAmmo = false;
        
        [Space(10)]
        [Header("━━━ 投射物设置 ━━━")] 
        [Tooltip("投射物的详细配置，包含物理、效果、伤害等设置")]
        [SerializeField] private ProjectileSettings _projectileSettings;
        [Tooltip("是否使用新的投射物设置系统（推荐启用）")]
        [SerializeField] private bool _useProjectileSettings = true;
        
        [Space(5)]
        [Header("━━━ 传统伤害设置 ━━━")]
        [Tooltip("是否可以爆头（仅影响Hitscan武器）")]
        [SerializeField] private bool _canHeadshot = true;
        [Tooltip("爆头伤害倍数（仅影响Hitscan武器）")]
        [Range(1f, 10f)]
        [SerializeField] private float _headshotMultiplier = 2f;
        
        [Space(10)]
        [Header("━━━ 音效系统 ━━━")]
        [Tooltip("开火音效")]
        [SerializeField] private AudioClip _fireSound;
        [Tooltip("装弹音效")]
        [SerializeField] private AudioClip _reloadSound;
        [Tooltip("空弹夹音效")]
        [SerializeField] private AudioClip _emptySound;
        
        [Space(10)]
        [Header("━━━ 视觉效果 ━━━")]
        [Tooltip("枪口火焰特效")]
        [SerializeField] private GameObject _muzzleFlashPrefab;
        
        [Space(10)]
        [Header("━━━ 后坐力系统 ━━━")]
        [Tooltip("后坐力模式：X=水平偏移，Y=垂直上扬（推荐: X:0-3, Y:1-5）")]
        [SerializeField] private Vector2 _recoilPattern = new Vector2(1f, 2f);
        [Tooltip("后坐力恢复时间，秒（推荐: 0.2-2.0秒）")]
        [Range(0.1f, 5f)]
        [SerializeField] private float _recoilRecoveryTime = 0.5f;
        
        [Space(10)]
        [Header("━━━ 动画设置 ━━━")]
        [Tooltip("射击动画触发器名称")]
        [SerializeField] private string _fireAnimationName = "Fire";
        [Tooltip("装弹动画触发器名称")]
        [SerializeField] private string _reloadAnimationName = "Reload";
        [Tooltip("装备武器动画触发器名称")]
        [SerializeField] private string _equipAnimationName = "Equip";
        [Tooltip("卸载武器动画触发器名称")]
        [SerializeField] private string _unequipAnimationName = "Unequip";
        [Tooltip("待机动画状态名称")]
        [SerializeField] private string _idleAnimationName = "Idle";
        
        [Space(10)]
        [Header("━━━ 网络设置 ━━━")]
        [Tooltip("是否同步投射物的网络状态")]
        [SerializeField] private bool _syncProjectiles = true;
        [Tooltip("网络更新优先级 (1=低, 5=高)")]
        [Range(1, 5)]
        [SerializeField] private int _networkPriority = 1;

        #region 属性访问器
        
        public string WeaponName => _weaponName;
        public string Description => _description;
        public Sprite WeaponIcon => _weaponIcon;
        
        // 发射设置
        public float FireRate => _fireRate;
        public bool Automatic => _automatic;
        public int ProjectilesPerShot => _projectilesPerShot;
        public float SpreadAngle => _spreadAngle;
        public float Accuracy => _accuracy;
        
        // 弹药系统
        public int MagazineSize => _magazineSize;
        public float ReloadTime => _reloadTime;
        public bool InfiniteAmmo => _infiniteAmmo;
        
        // 投射物设置访问（现在只从ProjectileSettings获取）
        public float ProjectileSpeed => _useProjectileSettings && _projectileSettings != null ? _projectileSettings.Speed : 20f;
        public float Damage => _useProjectileSettings && _projectileSettings != null ? _projectileSettings.Damage : 20f;
        public float MaxRange => _useProjectileSettings && _projectileSettings != null ? _projectileSettings.MaxRange : 100f;
        
        // 直接访问ProjectileSettings
        public ProjectileSettings ProjectileSettings => _projectileSettings;
        public bool UseProjectileSettings => _useProjectileSettings && _projectileSettings != null;
        
        // 特殊效果访问（现在只从ProjectileSettings获取）
        public int MaxBounceCount => _useProjectileSettings && _projectileSettings != null ? _projectileSettings.MaxBounceCount : 0;
        public float BounceEnergyLoss => _useProjectileSettings && _projectileSettings != null ? _projectileSettings.BounceEnergyLoss : 0.1f;
        public float GravityForce => _useProjectileSettings && _projectileSettings != null ? _projectileSettings.GravityForce : 0f;
        public float GravityRadius => _useProjectileSettings && _projectileSettings != null ? _projectileSettings.GravityRadius : 5f;
        public float ExplosionRadius => _useProjectileSettings && _projectileSettings != null ? _projectileSettings.ExplosionRadius : 0f;
        public float ExplosionDamage => _useProjectileSettings && _projectileSettings != null ? _projectileSettings.ExplosionDamage : 0f;
        
        // 传统伤害设置（向后兼容）
        public bool CanHeadshot => _canHeadshot;
        public float HeadshotMultiplier => _headshotMultiplier;
        
        // 音效
        public AudioClip FireSound => _fireSound;
        public AudioClip ReloadSound => _reloadSound;
        public AudioClip EmptySound => _emptySound;
        
        // 视觉效果
        public GameObject MuzzleFlashPrefab => _muzzleFlashPrefab;
        
        // 后坐力
        public Vector2 RecoilPattern => _recoilPattern;
        public float RecoilRecoveryTime => _recoilRecoveryTime;
        
        // 动画设置
        public string FireAnimationName => _fireAnimationName;
        public string ReloadAnimationName => _reloadAnimationName;
        public string EquipAnimationName => _equipAnimationName;
        public string UnequipAnimationName => _unequipAnimationName;
        public string IdleAnimationName => _idleAnimationName;
        
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
        public bool HasBounce => MaxBounceCount > 0;
        
        /// <summary>
        /// 是否有引力效果
        /// </summary>
        public bool HasGravityEffect => GravityForce > 0f;
        
        /// <summary>
        /// 是否有爆炸效果
        /// </summary>
        public bool HasExplosion => ExplosionRadius > 0f && ExplosionDamage > 0f;
        
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
            _magazineSize = Mathf.Max(0, _magazineSize);
            _reloadTime = Mathf.Max(0f, _reloadTime);
            _projectilesPerShot = Mathf.Max(1, _projectilesPerShot);
            _spreadAngle = Mathf.Clamp(_spreadAngle, 0f, 90f);
            _headshotMultiplier = Mathf.Max(1f, _headshotMultiplier);
            _recoilRecoveryTime = Mathf.Max(0.1f, _recoilRecoveryTime);
            _networkPriority = Mathf.Clamp(_networkPriority, 1, 10);
            _accuracy = Mathf.Clamp01(_accuracy);
            
            // 验证ProjectileSettings
            if (_useProjectileSettings && _projectileSettings != null)
            {
                _projectileSettings.OnValidate();
            }
        }
#endif
        
        #endregion
        
        #region ProjectileSettings 便利方法
        
        /// <summary>
        /// 创建或获取ProjectileSettings实例
        /// </summary>
        public ProjectileSettings GetOrCreateProjectileSettings()
        {
            if (_projectileSettings == null)
            {
                _projectileSettings = new ProjectileSettings();
            }
            return _projectileSettings;
        }
        
        /// <summary>
        /// 启用ProjectileSettings系统
        /// </summary>
        public void EnableProjectileSettings()
        {
            _useProjectileSettings = true;
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        
        /// <summary>
        /// 禁用ProjectileSettings，回退到传统配置
        /// </summary>
        public void DisableProjectileSettings()
        {
            _useProjectileSettings = false;
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        
        #endregion
    }
}
