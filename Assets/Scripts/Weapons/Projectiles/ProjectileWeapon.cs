using UnityEngine;
using Photon.Pun;

namespace DWHITE.Weapons
{    /// <summary>
    /// 投射型武器实现
    /// 发射物理投射物的武器基类
    /// </summary>
    public class ProjectileWeapon : WeaponBase
    {
        #region 配置
        
        [Header("投射物设置")]
        [SerializeField] protected GameObject _ProjectilePrefab;
        [SerializeField] protected bool _inheritPlayerVelocity = true;
        [SerializeField] protected float _inheritVelocityFactor = 0.5f;
        [SerializeField] protected Vector3 _spawnOffset = Vector3.zero;
        
        [Header("散射设置")]
        [SerializeField] protected bool _useSpread = false;
        [SerializeField] protected AnimationCurve _spreadPattern = AnimationCurve.Linear(0, 0, 1, 1);
        
        [Header("网络限制")]
        [SerializeField] protected int _maxNetworkProjectilesPerPlayer = 200;
        [SerializeField] protected float _projectileSpamCheckInterval = 0.1f;
        
        #endregion
        
        #region 组件引用
        
        protected PlayerMotor _playerMotor;
        
        #endregion
        
        #region Unity 生命周期
        
        protected override void Awake()
        {
            base.Awake();
            _playerMotor = GetComponentInParent<PlayerMotor>();
        }
        
        #endregion
        
        #region 射击实现
          /// <summary>
        /// 执行射击
        /// </summary>
        protected override void Fire(Vector3 direction)
        {
            Debug.Log("[投射武器] Fire方法开始执行");
            Debug.Log($"[投射武器] 武器数据存在: {_weaponData != null}");
            Debug.Log($"[投射武器] 投射物预制件存在: {_ProjectilePrefab != null}");
            
            if (_weaponData == null || _ProjectilePrefab == null)
            {
                Debug.LogError($"[投射武器] {gameObject.name} 缺少投射物预制件");
                Debug.LogError($"[投射武器] 武器数据: {(_weaponData != null ? "存在" : "不存在")}");
                Debug.LogError($"[投射武器] 投射物预制件: {(_ProjectilePrefab != null ? "存在" : "不存在")}");
                return;
            }
            
            Debug.Log("[投射武器] 开始计算发射参数");
            // 计算发射参数
            Vector3 spawnPosition = CalculateSpawnPosition();
            Vector3 fireDirection = direction.normalized;
            
            Debug.Log($"[投射武器] 生成位置: {spawnPosition}");
            Debug.Log($"[投射武器] 射击方向: {fireDirection}");
            Debug.Log($"[投射武器] 是否散射武器: {_weaponData.IsSpreadWeapon}");
            Debug.Log($"[投射武器] 使用散射: {_useSpread}");
            
            // 处理散射
            if (_weaponData.IsSpreadWeapon || _useSpread)
            {
                Debug.Log("[投射武器] 发射多个投射物（散射）");
                FireMultipleProjectiles(spawnPosition, fireDirection);
            }
            else
            {
                Debug.Log("[投射武器] 发射单个投射物");
                FireSingleProjectile(spawnPosition, fireDirection);
            }
            
            Debug.Log("[投射武器] 播放枪口闪光");
            // 播放枪口闪光
            PlayMuzzleFlash();
            
            Debug.Log("[投射武器] 完成射击处理");
            // 完成射击处理
            OnFireComplete(direction);
        }
          /// <summary>
        /// 发射单个投射物
        /// </summary>
        protected virtual void FireSingleProjectile(Vector3 spawnPosition, Vector3 direction)
        {
            Debug.Log("[投射武器] FireSingleProjectile开始执行");
            
            // 计算最终速度
            float finalSpeed = CalculateFinalSpeed();
            Vector3 finalVelocity = direction * finalSpeed;
            
            Debug.Log($"[投射武器] 计算出的最终速度: {finalSpeed}");
            Debug.Log($"[投射武器] 最终速度向量: {finalVelocity}");
            
            // 应用玩家速度继承
            if (_inheritPlayerVelocity && _playerMotor != null)
            {
                Vector3 playerVelocity = _playerMotor.Velocity * _inheritVelocityFactor;
                finalVelocity += playerVelocity;
                Debug.Log($"[投射武器] 玩家速度: {_playerMotor.Velocity}");
                Debug.Log($"[投射武器] 继承速度: {playerVelocity}");
                Debug.Log($"[投射武器] 应用继承后的最终速度: {finalVelocity}");
            }
            else
            {
                Debug.Log($"[投射武器] 不继承玩家速度 (继承开关: {_inheritPlayerVelocity}, PlayerMotor存在: {_playerMotor != null})");
            }
            
            Debug.Log("[投射武器] 调用CreateProjectile");
            // 创建投射物
            CreateProjectile(spawnPosition, finalVelocity, direction);
        }
        
        /// <summary>
        /// 发射多个投射物（散弹）
        /// </summary>
        protected virtual void FireMultipleProjectiles(Vector3 spawnPosition, Vector3 baseDirection)
        {
            int projectileCount = _weaponData.ProjectilesPerShot;
            float spreadAngle = _weaponData.SpreadAngle;
            
            for (int i = 0; i < projectileCount; i++)
            {
                // 计算散射方向
                Vector3 spreadDirection = CalculateSpreadDirection(baseDirection, spreadAngle, i, projectileCount);
                
                // 计算速度
                float finalSpeed = CalculateFinalSpeed();
                Vector3 finalVelocity = spreadDirection * finalSpeed;
                
                // 应用玩家速度继承
                if (_inheritPlayerVelocity && _playerMotor != null)
                {
                    Vector3 playerVelocity = _playerMotor.Velocity * _inheritVelocityFactor;
                    finalVelocity += playerVelocity;
                }
                
                // 创建投射物
                CreateProjectile(spawnPosition, finalVelocity, spreadDirection);
            }
        }        /// <summary>
        /// 创建投射物
        /// </summary>
        protected virtual void CreateProjectile(Vector3 position, Vector3 velocity, Vector3 direction)
        {
            Debug.Log($"[投射武器] CreateProjectile 开始执行");
            Debug.Log($"[投射武器] 投射物预制件: {(_ProjectilePrefab != null ? _ProjectilePrefab.name : "NULL")}");
            Debug.Log($"[投射武器] 生成位置: {position}");
            Debug.Log($"[投射武器] 速度: {velocity}");
            Debug.Log($"[投射武器] 方向: {direction}");
            Debug.Log($"[投射武器] 网络同步: {_weaponData?.SyncProjectiles}");
            Debug.Log($"[投射武器] PhotonView存在: {photonView != null}");
            Debug.Log($"[投射武器] 是我的PhotonView: {photonView?.IsMine}");
            
            // 网络投射物限制检查
            if (_weaponData.SyncProjectiles && photonView != null && photonView.IsMine)
            {
                int currentNetworkProjectiles = CountPlayerNetworkProjectiles();
                if (currentNetworkProjectiles >= _maxNetworkProjectilesPerPlayer)
                {
                    Debug.LogWarning($"[投射武器] 达到网络投射物限制 ({currentNetworkProjectiles}/{_maxNetworkProjectilesPerPlayer})，跳过创建");
                    return;
                }
            }
            
            GameObject projectileObj = null;
            
            try
            {
                // 根据网络设置决定创建方式
                if (_weaponData.SyncProjectiles && photonView != null && photonView.IsMine)
                {
                    Debug.Log("[投射武器] 使用网络生成投射物");
                    // 网络生成投射物
                    object[] initData = new object[] 
                    { 
                        velocity.x, velocity.y, velocity.z,
                        _weaponData.Damage,
                        photonView.ViewID // 武器来源ID
                    };
                    
                    projectileObj = PhotonNetwork.Instantiate(
                        _ProjectilePrefab.name, 
                        position, 
                        Quaternion.LookRotation(direction),
                        0,
                        initData
                    );
                    Debug.Log($"[投射武器] 网络投射物创建成功: {projectileObj.name}");
                }
                else
                {
                    Debug.Log("[投射武器] 使用本地生成投射物");
                    // 本地生成投射物
                    projectileObj = Instantiate(_ProjectilePrefab, position, Quaternion.LookRotation(direction));
                    Debug.Log($"[投射武器] 本地投射物创建成功: {projectileObj.name}");
                }
                
                // 配置投射物
                ProjectileBase projectile = projectileObj.GetComponent<ProjectileBase>();
                if (projectile != null)
                {
                    Debug.Log("[投射武器] 开始配置投射物");
                    ConfigureProjectile(projectile, velocity, direction);
                    Debug.Log("[投射武器] 投射物配置完成");
                }
                else
                {
                    Debug.LogError($"[投射武器] 投射物预制件 {_ProjectilePrefab.name} 缺少 ProjectileBase 组件");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[投射武器] 创建投射物时发生异常: {e.Message}");
                Debug.LogError($"[投射武器] 异常堆栈: {e.StackTrace}");
            }
        }

        /// <summary>
        /// 配置投射物参数
        /// </summary>
        protected virtual void ConfigureProjectile(ProjectileBase projectile, Vector3 velocity, Vector3 direction)
        {
            // 发射投射物
            projectile.Launch(direction, velocity.magnitude, this, transform.root.gameObject);
        }
        
        #endregion
        
        #region 计算方法
        
        /// <summary>
        /// 计算生成位置
        /// </summary>
        protected virtual Vector3 CalculateSpawnPosition()
        {
            Vector3 basePosition = GetMuzzlePosition();
            
            // 应用偏移
            if (_spawnOffset != Vector3.zero)
            {
                Transform muzzle = MuzzlePoint ?? transform;
                basePosition += muzzle.TransformDirection(_spawnOffset);
            }
            
            return basePosition;
        }
        
        /// <summary>
        /// 计算最终速度
        /// </summary>
        protected virtual float CalculateFinalSpeed()
        {
            return _weaponData.ProjectileSpeed;
        }
        
        /// <summary>
        /// 计算散射方向
        /// </summary>
        protected virtual Vector3 CalculateSpreadDirection(Vector3 baseDirection, float spreadAngle, int index, int total)
        {
            if (total <= 1 || spreadAngle <= 0f)
                return baseDirection;
            
            // 计算散射角度
            float angleStep = spreadAngle * 2f / (total - 1);
            float currentAngle = -spreadAngle + (angleStep * index);
            
            // 应用散射模式
            if (_useSpread && _spreadPattern != null)
            {
                float normalizedIndex = (float)index / (total - 1);
                float patternFactor = _spreadPattern.Evaluate(normalizedIndex);
                currentAngle *= patternFactor;
            }
            
            // 随机化散射（添加一些随机性）
            currentAngle += Random.Range(-spreadAngle * 0.1f, spreadAngle * 0.1f);
            
            // 计算最终方向
            Vector3 right = Vector3.Cross(baseDirection, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, baseDirection).normalized;
            
            // 在一个圆锥内随机分布
            float randomAngle = Random.Range(0f, 360f);
            Vector3 spreadOffset = (right * Mathf.Cos(randomAngle * Mathf.Deg2Rad) + 
                                  up * Mathf.Sin(randomAngle * Mathf.Deg2Rad)) * 
                                  Mathf.Tan(currentAngle * Mathf.Deg2Rad);
            
            return (baseDirection + spreadOffset).normalized;
        }
        
        #endregion
        
        #region 效果系统
        
        /// <summary>
        /// 播放枪口闪光
        /// </summary>
        protected virtual void PlayMuzzleFlash()
        {
            if (_weaponData?.MuzzleFlashPrefab != null && MuzzlePoint != null)
            {
                GameObject flash = Instantiate(_weaponData.MuzzleFlashPrefab, MuzzlePoint.position, MuzzlePoint.rotation);
                Destroy(flash, 1f);
            }
        }
        
        #endregion
        
        #region 网络同步
        
        /// <summary>
        /// 网络同步射击
        /// </summary>
        [PunRPC]
        public override void NetworkFire(Vector3 direction, float timestamp)
        {
            // 计算时间差进行延迟补偿
            float timeDiff = (float)(PhotonNetwork.Time - timestamp);
            
            // 如果不是本地玩家，播放射击效果但不创建真实投射物
            if (!photonView.IsMine)
            {
                PlayMuzzleFlash();
                PlayFireSound();
                
                // 可以创建纯视觉的投射物轨迹
                CreateVisualTrail(direction, timeDiff);
            }
        }
        
        /// <summary>
        /// 创建视觉轨迹（仅用于网络同步）
        /// </summary>
        protected virtual void CreateVisualTrail(Vector3 direction, float timeOffset)
        {
            // 这里可以实现纯视觉的弹道轨迹，用于网络同步时的视觉反馈
            // 例如：粒子轨迹、线渲染器等
        }
        
        #endregion
        
        #region 工具方法
        
        /// <summary>
        /// 验证投射物预制件
        /// </summary>
        protected override void ValidateConfiguration()
        {
            base.ValidateConfiguration();
            
            if (_ProjectilePrefab != null)
            {
                ProjectileBase projectileComponent = _ProjectilePrefab.GetComponent<ProjectileBase>();
                if (projectileComponent == null)
                {
                    Debug.LogError($"[投射武器] 投射物预制件 {_ProjectilePrefab.name} 缺少 ProjectileBase 组件");
                }
                
                // 检查网络组件
                if (_weaponData.SyncProjectiles)
                {
                    PhotonView projectilePhotonView = _ProjectilePrefab.GetComponent<PhotonView>();
                    if (projectilePhotonView == null)
                    {
                        Debug.LogWarning($"[投射武器] 网络同步投射物 {_ProjectilePrefab.name} 缺少 PhotonView 组件");
                    }
                }
            }
        }
        
        #endregion
        
        #region 调试
        
#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            
            if (_weaponData != null && _weaponData.IsSpreadWeapon)
            {
                // 绘制散射角度
                Vector3 muzzlePos = GetMuzzlePosition();
                Vector3 forward = GetMuzzleDirection();
                float spreadAngle = _weaponData.SpreadAngle;
                
                Gizmos.color = Color.yellow;
                
                // 绘制散射锥
                for (int i = 0; i < _weaponData.ProjectilesPerShot; i++)
                {
                    Vector3 spreadDir = CalculateSpreadDirection(forward, spreadAngle, i, _weaponData.ProjectilesPerShot);
                    Gizmos.DrawRay(muzzlePos, spreadDir * 3f);
                }
            }
        }
#endif
        
        #endregion

        /// <summary>
        /// 计算当前玩家的网络投射物数量
        /// </summary>
        protected virtual int CountPlayerNetworkProjectiles()
        {
            if (photonView == null || !photonView.IsMine) return 0;
            
            int count = 0;
            ProjectileBase[] allProjectiles = FindObjectsOfType<ProjectileBase>();
            
            foreach (var projectile in allProjectiles)
            {
                if (projectile.photonView != null && 
                    projectile.photonView.Owner == photonView.Owner && 
                    !projectile.IsDestroyed)
                {
                    count++;
                }
            }
            
            return count;
        }
    }
}
