using UnityEngine;
using Photon.Pun;
using DWHITE.Weapons.Network;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 投射型武器实现
    /// 发射物理投射物的武器基类    /// </summary>
    public class ProjectileWeapon : WeaponBase
    {
        #region 配置
        
        [Header("投射物设置")]
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
        /// 具体的开火实现
        /// </summary>
        /// <param name="direction">射击方向</param>
        /// <returns>是否成功开火</returns>
        protected override bool FireImplementation(Vector3 direction)
        {            if (_showDebugInfo)
            {
                Debug.Log("[投射武器] FireImplementation方法开始执行");
                Debug.Log($"[投射武器] 武器数据存在: {_weaponData != null}");
                Debug.Log($"[投射武器] 投射物预制件存在: {ProjectilePrefab != null}");
            }

            if (_weaponData == null || ProjectilePrefab == null)
            {
                Debug.LogError($"[投射武器] {gameObject.name} 缺少投射物预制件");
                Debug.LogError($"[投射武器] 武器数据: {(_weaponData != null ? "存在" : "不存在")}");
                Debug.LogError($"[投射武器] 投射物预制件: {(ProjectilePrefab != null ? "存在" : "不存在")}");
                return false;
            }

            // 计算发射参数
            Vector3 spawnPosition = CalculateSpawnPosition();
            Vector3 fireDirection = direction.normalized;

            if (_showDebugInfo)
            {
                Debug.Log("[投射武器] 开始计算发射参数");
                Debug.Log($"[投射武器] 生成位置: {spawnPosition}");
                Debug.Log($"[投射武器] 射击方向: {fireDirection}");
                Debug.Log($"[投射武器] 是否散射武器: {_weaponData.IsSpreadWeapon}");
                Debug.Log($"[投射武器] 使用散射: {_useSpread}");
            }

            // 处理散射
            if (_weaponData.IsSpreadWeapon || _useSpread)
            {
                if (_showDebugInfo)
                    Debug.Log("[投射武器] 发射多个投射物（散射）");
                FireMultipleProjectiles(spawnPosition, fireDirection);
            }
            else
            {
                if (_showDebugInfo)
                    Debug.Log("[投射武器] 发射单个投射物");
                FireSingleProjectile(spawnPosition, fireDirection);
            }

            // 播放枪口闪光
            PlayMuzzleFlash();
            
            if (_showDebugInfo)
                Debug.Log("[投射武器] 完成射击处理");
            
            // 返回成功
            return true;
        }
        
        /// <summary>
        /// 发射单个投射物
        /// </summary>
        protected virtual void FireSingleProjectile(Vector3 spawnPosition, Vector3 direction)
        {
            if (_showDebugInfo)
                Debug.Log("[投射武器] FireSingleProjectile开始执行");
            
            // 计算最终速度
            float finalSpeed = CalculateFinalSpeed();
            Vector3 finalVelocity = direction * finalSpeed;
            
            if (_showDebugInfo)
            {
                Debug.Log($"[投射武器] 计算出的最终速度: {finalSpeed}");
                Debug.Log($"[投射武器] 最终速度向量: {finalVelocity}");
            }
            
            // 应用玩家速度继承
            if (_inheritPlayerVelocity && _playerMotor != null)
            {
                Vector3 playerVelocity = _playerMotor.Velocity * _inheritVelocityFactor;
                finalVelocity += playerVelocity;
                
                if (_showDebugInfo)
                {
                    Debug.Log($"[投射武器] 玩家速度: {_playerMotor.Velocity}");
                    Debug.Log($"[投射武器] 继承速度: {playerVelocity}");
                    Debug.Log($"[投射武器] 应用继承后的最终速度: {finalVelocity}");
                }
            }
            else if (_showDebugInfo)
            {
                Debug.Log($"[投射武器] 不继承玩家速度 (继承开关: {_inheritPlayerVelocity}, PlayerMotor存在: {_playerMotor != null})");
            }
            
            if (_showDebugInfo)
                Debug.Log("[投射武器] 调用CreateProjectile");
            
            // 创建投射物
            CreateProjectile(spawnPosition, finalVelocity, direction);
        }
        
        /// <summary>
        /// 发射多个投射物（散弹）
        /// </summary>
        protected virtual void FireMultipleProjectiles(Vector3 spawnPosition, Vector3 baseDirection)
        {
            if (_showDebugInfo)
                Debug.Log("[投射武器] FireMultipleProjectiles开始执行");
            
            int projectileCount = _weaponData.ProjectilesPerShot;
            float spreadAngle = _weaponData.SpreadAngle;
            
            if (_showDebugInfo)
            {
                Debug.Log($"[投射武器] 散弹投射物数量: {projectileCount}");
                Debug.Log($"[投射武器] 散射角度: {spreadAngle}");
            }
            
            for (int i = 0; i < projectileCount; i++)
            {
                if (_showDebugInfo)
                    Debug.Log($"[投射武器] 创建第 {i + 1}/{projectileCount} 个投射物");
                
                // 计算散射方向
                Vector3 spreadDirection = CalculateSpreadDirection(baseDirection, spreadAngle, i, projectileCount);
                
                if (_showDebugInfo)
                    Debug.Log($"[投射武器] 散射方向 {i + 1}: {spreadDirection}");
                
                // 计算速度
                float finalSpeed = CalculateFinalSpeed();
                Vector3 finalVelocity = spreadDirection * finalSpeed;
                
                if (_showDebugInfo)
                {
                    Debug.Log($"[投射武器] 投射物 {i + 1} 计算出的最终速度: {finalSpeed}");
                    Debug.Log($"[投射武器] 投射物 {i + 1} 最终速度向量: {finalVelocity}");
                }
                
                // 应用玩家速度继承
                if (_inheritPlayerVelocity && _playerMotor != null)
                {
                    Vector3 playerVelocity = _playerMotor.Velocity * _inheritVelocityFactor;
                    finalVelocity += playerVelocity;
                    
                    if (_showDebugInfo)
                    {
                        Debug.Log($"[投射武器] 投射物 {i + 1} 玩家速度: {_playerMotor.Velocity}");
                        Debug.Log($"[投射武器] 投射物 {i + 1} 继承速度: {playerVelocity}");
                        Debug.Log($"[投射武器] 投射物 {i + 1} 应用继承后的最终速度: {finalVelocity}");
                    }
                }
                else if (_showDebugInfo)
                {
                    Debug.Log($"[投射武器] 投射物 {i + 1} 不继承玩家速度 (继承开关: {_inheritPlayerVelocity}, PlayerMotor存在: {_playerMotor != null})");
                }
                
                if (_showDebugInfo)
                    Debug.Log($"[投射武器] 调用CreateProjectile创建投射物 {i + 1}");
                
                // 创建投射物
                CreateProjectile(spawnPosition, finalVelocity, spreadDirection);
            }
            
            if (_showDebugInfo)
                Debug.Log("[投射武器] FireMultipleProjectiles执行完成");
        }

        /// <summary>
        /// 创建投射物
        /// </summary>
        protected virtual void CreateProjectile(Vector3 position, Vector3 velocity, Vector3 direction)
        {            if (_showDebugInfo)
            {
                Debug.Log($"[投射武器] CreateProjectile 开始执行");
                Debug.Log($"[投射武器] 投射物预制件: {(ProjectilePrefab != null ? ProjectilePrefab.name : "NULL")}");
                Debug.Log($"[投射武器] 生成位置: {position}");
                Debug.Log($"[投射武器] 速度: {velocity}");
                Debug.Log($"[投射武器] 方向: {direction}");
                Debug.Log($"[投射武器] 网络同步: {_weaponData?.SyncProjectiles}");
                Debug.Log($"[投射武器] PhotonView存在: {photonView != null}");
                Debug.Log($"[投射武器] 是我的PhotonView: {photonView?.IsMine}");
            }

            // 网络投射物限制检查
            if (_weaponData.SyncProjectiles && photonView != null && photonView.IsMine)
            {
                int currentNetworkProjectiles = CountPlayerNetworkProjectiles();
                if (currentNetworkProjectiles >= _maxNetworkProjectilesPerPlayer)
                {
                    if (_showDebugInfo)
                        Debug.LogWarning($"[投射武器] 达到网络投射物限制 ({currentNetworkProjectiles}/{_maxNetworkProjectilesPerPlayer})，跳过创建");
                    return;
                }
            }
            
            GameObject projectileObj = null;

            try
            {
                // 检查是否使用ProjectileSettings
                if (_weaponData.UseProjectileSettings)
                {
                    if (_showDebugInfo)
                        Debug.Log("[投射武器] 使用ProjectileSettings创建投射物");                    // 使用新的ProjectileSettings方法
                    projectileObj = ProjectileManager.Instance.SpawnProjectile(
                        ProjectilePrefab,
                        position,
                        direction,
                        velocity,
                        _weaponData.ProjectileSettings,
                        this, // 来源武器
                        gameObject, // 来源玩家
                        _weaponData.SyncProjectiles // 是否使用网络同步
                    );
                }
                else
                {
                    if (_showDebugInfo)
                        Debug.Log("[投射武器] 使用传统参数创建投射物");

                    // 使用传统的参数方法
                    projectileObj = ProjectileManager.Instance.SpawnProjectile(
                        ProjectilePrefab,
                        position,
                        direction,
                        velocity,
                        _weaponData.Damage,
                        this, // 来源武器
                        gameObject, // 来源玩家
                        _weaponData.SyncProjectiles // 是否使用网络同步
                    );
                }

                if (projectileObj != null)
                {
                    if (_showDebugInfo)
                        Debug.Log($"[投射武器] 投射物创建成功: {projectileObj.name}");
                }
                else
                {
                    Debug.LogError("[投射武器] 投射物创建失败");
                    return;
                }

                // 额外配置投射物（子类可以重写）
                ProjectileBase projectile = projectileObj.GetComponent<ProjectileBase>();
                if (projectile != null)
                {
                    if (_showDebugInfo)
                        Debug.Log("[投射武器] 开始额外配置投射物");
                    
                    ConfigureProjectile(projectile, velocity, direction);
                    
                    if (_showDebugInfo)
                        Debug.Log("[投射武器] 投射物额外配置完成");
                }                else
                {
                    Debug.LogError($"[投射武器] 投射物预制件 {ProjectilePrefab.name} 缺少 ProjectileBase 组件");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[投射武器] 创建投射物时发生异常: {e.Message}");
                Debug.LogError($"[投射武器] 异常堆栈: {e.StackTrace}");
            }
        }

        /// <summary>
        /// 配置投射物参数（额外配置，在ProjectileManager配置之后）
        /// </summary>
        protected virtual void ConfigureProjectile(ProjectileBase projectile, Vector3 velocity, Vector3 direction)
        {
            // 默认实现：什么都不做，因为ProjectileManager已经完成了基础配置
            // 子类可以重写此方法进行特殊配置
            
            if (_showDebugInfo)
                Debug.Log($"[投射武器] 投射物额外配置完成: {projectile.name}");
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
            if (_weaponData?.MuzzleFlashPrefab != null && MuzzleFxPoint != null)
            {
                GameObject flash = Instantiate(_weaponData.MuzzleFlashPrefab, MuzzleFxPoint.position, MuzzleFxPoint.rotation);
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
            if (!photonView.IsMine)            {
                PlayMuzzleFlash();
                PlayFireSound();
            }        }
        
        #endregion
        
        #region 工具方法
        
        /// <summary>
        /// 验证投射物预制件
        /// </summary>
        protected override void ValidateConfiguration()        {
            base.ValidateConfiguration();
            
            if (ProjectilePrefab != null)
            {
                ProjectileBase projectileComponent = ProjectilePrefab.GetComponent<ProjectileBase>();
                if (projectileComponent == null)
                {
                    Debug.LogError($"[投射武器] 投射物预制件 {ProjectilePrefab.name} 缺少 ProjectileBase 组件");
                }
                
                // 检查网络组件
                if (_weaponData.SyncProjectiles)
                {
                    PhotonView projectilePhotonView = ProjectilePrefab.GetComponent<PhotonView>();
                    if (projectilePhotonView == null)
                    {
                        Debug.LogWarning($"[投射武器] 网络同步投射物 {ProjectilePrefab.name} 缺少 PhotonView 组件");
                    }
                }
            }
        }
        
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
        
        #endregion
        
        #region 属性访问器
        
        /// <summary>
        /// 获取投射物预制体（从WeaponData的ProjectileSettings中获取）
        /// </summary>
        protected GameObject ProjectilePrefab => _weaponData?.ProjectileSettings?.ProjectilePrefab;
        
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
        
        #region 向后兼容性和迁移提示
        
#if UNITY_EDITOR
        /// <summary>
        /// 编辑器中的配置验证和迁移提示
        /// </summary>
        private void OnValidate()
        {
            if (_weaponData == null)
            {
                Debug.LogWarning($"[投射武器] {gameObject.name} 缺少 WeaponData 配置");
                return;
            }
            
            if (!_weaponData.UseProjectileSettings || _weaponData.ProjectileSettings == null)
            {
                Debug.LogWarning($"[投射武器] {gameObject.name} 建议启用 ProjectileSettings 系统以获得更好的配置管理");
            }
            else if (_weaponData.ProjectileSettings.ProjectilePrefab == null)
            {
                Debug.LogError($"[投射武器] {gameObject.name} 的 ProjectileSettings 中缺少投射物预制体配置");
            }
        }
#endif
        #endregion
    }
}
