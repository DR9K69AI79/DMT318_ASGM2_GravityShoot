using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Photon.Pun;

namespace DWHITE.Weapons.Network
{    /// <summary>
    /// 投射物管理器 - 简化版本
    /// 只处理投射物的网络生成事件，运动交给本地物理计算
    /// </summary>
    public class ProjectileManager : MonoBehaviourPun
    {
        #region 单例模式
        
        private static ProjectileManager _instance;
        public static ProjectileManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ProjectileManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("ProjectileManager");
                        _instance = go.AddComponent<ProjectileManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }
        
        #endregion
          #region 配置
        
        [Header("投射物管理")]
        [SerializeField] private int _maxActiveProjectiles = 100;
        [SerializeField] private float _cleanupInterval = 5f;
        
        [Header("网络设置")]
        [SerializeField] private bool _enableNetworkSpawn = true;
        [SerializeField] private float _spamProtectionInterval = 0.05f; // 防刷保护
        
        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = true;
        
        #endregion
          #region 状态管理
        
        private Dictionary<int, ProjectileBase> _activeProjectiles = new Dictionary<int, ProjectileBase>();
        private float _lastSpawnTime = 0f; // 防刷保护用的时间戳
        private int _nextProjectileId = 1;
        
        #endregion
        
        #region Unity 生命周期
        
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }
        
        private void Start()
        {
            StartCoroutine(CleanupRoutine());
            LogActivity("投射物管理器已启动");
        }
          private void Update()
        {
            // 简化版本：只处理基本的清理工作
            // 投射物的运动完全交给本地物理计算
        }
        
        #endregion
        
        #region 投射物工厂方法
        
        /// <summary>
        /// 生成投射物（简化版本）- 只同步创建事件，运动完全本地处理
        /// </summary>
        /// <param name="prefab">投射物预制体</param>
        /// <param name="position">生成位置</param>
        /// <param name="direction">发射方向</param>
        /// <param name="velocity">初始速度</param>
        /// <param name="damage">伤害值</param>
        /// <param name="sourceWeapon">来源武器</param>
        /// <param name="sourcePlayer">来源玩家</param>
        /// <param name="useNetworking">是否使用网络同步</param>
        /// <param name="customData">自定义数据</param>
        /// <returns>生成的投射物GameObject</returns>
        public GameObject SpawnProjectile(
            GameObject prefab, 
            Vector3 position, 
            Vector3 direction, 
            Vector3 velocity,
            float damage,
            WeaponBase sourceWeapon = null,
            GameObject sourcePlayer = null,
            bool useNetworking = true,
            object[] customData = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[ProjectileManager] 投射物预制体为空");
                return null;
            }

            GameObject projectileObj = null;
            
            // 简化网络逻辑：只有武器拥有者才创建投射物
            bool shouldCreateLocally = !useNetworking || !PhotonNetwork.IsConnected;
            bool isWeaponOwner = sourceWeapon != null && sourceWeapon.photonView != null && sourceWeapon.photonView.IsMine;
            
            if (useNetworking && PhotonNetwork.IsConnected)
            {
                if (isWeaponOwner)
                {
                    // 网络创建：只传递必要的初始化数据
                    object[] initData = CreateSimpleNetworkData(velocity, damage, sourceWeapon);
                    
                    projectileObj = PhotonNetwork.Instantiate(
                        prefab.name, 
                        position, 
                        Quaternion.LookRotation(direction),
                        0,
                        initData
                    );
                    
                    LogActivity($"网络投射物创建成功: {projectileObj.name} (所有者: {sourceWeapon.photonView.Owner?.NickName})");
                    
                    // 网络创建的投射物会通过 IPunInstantiateMagicCallback 自动配置，不需要手动配置
                }
                else
                {
                    // 非拥有者：不创建，等待网络同步
                    LogActivity($"非武器拥有者跳过投射物创建，等待网络同步");
                    return null;
                }
            }
            else
            {
                // 本地创建（单机模式或网络未连接）
                projectileObj = Instantiate(prefab, position, Quaternion.LookRotation(direction));
                LogActivity($"本地投射物创建成功: {projectileObj.name}");
                
                // 只有本地创建的投射物需要手动配置
                if (projectileObj != null)
                {
                    ConfigureProjectile(projectileObj, velocity, direction, damage, sourceWeapon, sourcePlayer);
                }
            }
            
            return projectileObj;
        }
        
        /// <summary>
        /// 带特殊参数的投射物生成
        /// </summary>
        /// <typeparam name="T">投射物类型</typeparam>
        public T SpawnProjectile<T>(
            GameObject prefab,
            Vector3 position,
            Vector3 direction,
            Vector3 velocity,
            float damage,
            WeaponBase sourceWeapon = null,
            GameObject sourcePlayer = null,
            bool useNetworking = true,
            System.Action<T> configureAction = null) where T : ProjectileBase
        {
            GameObject projectileObj = SpawnProjectile(
                prefab, position, direction, velocity, damage, 
                sourceWeapon, sourcePlayer, useNetworking);
                
            if (projectileObj != null)
            {
                T projectileComponent = projectileObj.GetComponent<T>();
                if (projectileComponent != null)
                {
                    // 执行额外配置
                    configureAction?.Invoke(projectileComponent);
                    return projectileComponent;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 使用ProjectileSettings创建投射物（新版本）
        /// </summary>
        /// <param name="prefab">投射物预制体</param>
        /// <param name="position">生成位置</param>
        /// <param name="direction">发射方向</param>
        /// <param name="velocity">初始速度</param>
        /// <param name="projectileSettings">投射物配置</param>
        /// <param name="sourceWeapon">来源武器</param>
        /// <param name="sourcePlayer">来源玩家</param>
        /// <param name="useNetworking">是否使用网络同步</param>
        /// <returns>生成的投射物GameObject</returns>
        public GameObject SpawnProjectile(
            GameObject prefab, 
            Vector3 position, 
            Vector3 direction, 
            Vector3 velocity,
            ProjectileSettings projectileSettings,
            WeaponBase sourceWeapon = null,
            GameObject sourcePlayer = null,
            bool useNetworking = true)
        {
            if (prefab == null)
            {
                Debug.LogError("[ProjectileManager] 投射物预制体为空");
                return null;
            }
            
            if (projectileSettings == null)
            {
                Debug.LogError("[ProjectileManager] ProjectileSettings为空，回退到默认参数");
                return SpawnProjectile(prefab, position, direction, velocity, 20f, sourceWeapon, sourcePlayer, useNetworking);
            }
              GameObject projectileObj = null;
            
            // 修复网络逻辑：检查武器的所有者而不是ProjectileManager的所有者
            bool shouldUseNetworking = useNetworking && PhotonNetwork.IsConnected;
            bool isWeaponOwner = sourceWeapon != null && sourceWeapon.photonView != null && sourceWeapon.photonView.IsMine;
            
            if (shouldUseNetworking && isWeaponOwner)
            {
                // 网络同步投射物 - 使用简化数据格式（与基础方法保持一致）
                float damage = projectileSettings.Damage;
                object[] initData = CreateSimpleNetworkData(velocity, damage, sourceWeapon);
                
                projectileObj = PhotonNetwork.Instantiate(
                    prefab.name, 
                    position, 
                    Quaternion.LookRotation(direction),
                    0,
                    initData
                );
                
                if (_showDebugInfo)
                {
                    LogActivity($"网络投射物创建成功 (ProjectileSettings): {projectileObj.name} (武器所有者: {sourceWeapon.photonView.Owner?.NickName})");
                }
                
                // 网络创建的投射物会通过 IPunInstantiateMagicCallback 自动配置，不需要手动配置
            }
            else if (shouldUseNetworking && !isWeaponOwner)
            {
                // 非武器所有者：不创建投射物，等待网络同步
                if (_showDebugInfo)
                {
                    LogActivity($"非武器所有者跳过投射物创建，等待网络同步 (ProjectileSettings)");
                }
                return null;
            }
            else
            {
                // 本地投射物
                projectileObj = Instantiate(prefab, position, Quaternion.LookRotation(direction));
                
                if (_showDebugInfo)
                {
                    LogActivity($"本地投射物创建成功 (ProjectileSettings): {projectileObj.name}");
                }
                
                // 只有本地创建的投射物需要手动配置
                if (projectileObj != null)
                {
                    ProjectileBase projectile = projectileObj.GetComponent<ProjectileBase>();
                    if (projectile != null)
                    {
                        ConfigureProjectileWithSettings(projectile, velocity, direction, projectileSettings, sourceWeapon, sourcePlayer);
                    }
                    
                    // 注册到管理器
                    ProjectileBase projectileComponent = projectileObj.GetComponent<ProjectileBase>();
                    if (projectileComponent != null)
                    {
                        RegisterProjectile(projectileComponent);
                    }
                }
            }
            
            return projectileObj;
        }
        
        /// <summary>
        /// 创建ProjectileSettings的网络数据
        /// </summary>
        private object[] CreateProjectileSettingsNetworkData(Vector3 velocity, ProjectileSettings settings, WeaponBase sourceWeapon)
        {
            // 基础数据
            var basicData = new object[]
            {
                // 速度
                velocity.x, velocity.y, velocity.z,
                // 基础设置
                settings.Damage, settings.MaxRange, settings.Lifetime,
                // 物理设置
                settings.Mass, settings.Drag, settings.UseGravity, settings.GravityScale,
                // 弹跳设置
                settings.MaxBounceCount, settings.BounceEnergyLoss,
                // 引力设置
                settings.GravityForce, settings.GravityRadius,
                // 爆炸设置
                settings.ExplosionRadius, settings.ExplosionDamage,
                // 穿透设置
                settings.PenetrationCount, settings.PenetrationDamageReduction,
                // 来源武器ID
                sourceWeapon?.photonView?.ViewID ?? -1
            };
            
            return basicData;
        }
          /// <summary>
        /// 创建简化的网络数据（只包含必要信息）
        /// </summary>
        private object[] CreateSimpleNetworkData(Vector3 velocity, float damage, WeaponBase sourceWeapon)
        {
            return new object[]
            {
                // 速度（3个元素）
                velocity.x, velocity.y, velocity.z,
                // 伤害值
                damage,
                // 来源武器ID
                sourceWeapon?.photonView?.ViewID ?? -1
            };
        }

        /// <summary>
        /// 配置投射物参数
        /// </summary>
        private void ConfigureProjectile(GameObject projectileObj, Vector3 velocity, Vector3 direction, float damage, WeaponBase sourceWeapon, GameObject sourcePlayer)
        {
            ProjectileBase projectileBase = projectileObj.GetComponent<ProjectileBase>();
            if (projectileBase != null)
            {
                // 尝试从源武器获取ProjectileSettings
                if (sourceWeapon != null && sourceWeapon.WeaponData != null && sourceWeapon.WeaponData.UseProjectileSettings)
                {
                    var settings = sourceWeapon.WeaponData.ProjectileSettings;
                    if (settings != null)
                    {
                        projectileBase.Configure(settings, velocity, direction, sourceWeapon, sourcePlayer);
                    }
                    else
                    {
                        projectileBase.Launch(direction, velocity.magnitude, sourceWeapon, sourcePlayer);
                    }
                }
                else
                {
                    // 回退到Launch方法（向后兼容旧武器）
                    projectileBase.Launch(direction, velocity.magnitude, sourceWeapon, sourcePlayer);
                }

                // 注册到管理器
                RegisterProjectile(projectileBase);
            }
        }

        // ...existing code...
        
        /// <summary>
        /// 使用ProjectileSettings配置投射物
        /// </summary>
        private void ConfigureProjectileWithSettings(
            ProjectileBase projectile, 
            Vector3 velocity, 
            Vector3 direction, 
            ProjectileSettings settings,
            WeaponBase sourceWeapon, 
            GameObject sourcePlayer)
        {
            try
            {
                // 直接调用ProjectileBase的Configure方法，避免反射
                if (projectile != null && settings != null)
                {
                    if (_showDebugInfo)
                        Debug.Log("[ProjectileManager] 直接调用ProjectileBase.Configure方法");
                    
                    projectile.Configure(settings, velocity, direction, sourceWeapon, sourcePlayer);
                }
                else
                {
                    // 回退到传统Launch方法
                    if (_showDebugInfo)
                        Debug.Log("[ProjectileManager] 投射物或设置为空，使用传统Launch方法");
                    
                    projectile.Launch(direction, velocity.magnitude, sourceWeapon, sourcePlayer);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProjectileManager] 配置投射物时发生异常: {e.Message}");
                Debug.LogError($"[ProjectileManager] 异常堆栈: {e.StackTrace}");
                
                // 异常时回退到传统Launch方法
                try
                {
                    projectile.Launch(direction, velocity.magnitude, sourceWeapon, sourcePlayer);
                    
                    if (_showDebugInfo)
                        Debug.LogWarning("[ProjectileManager] 配置异常，已回退到传统Launch方法");
                }
                catch (System.Exception fallbackException)
                {
                    Debug.LogError($"[ProjectileManager] 回退方法也失败: {fallbackException.Message}");
                }
            }
        }
        
        #endregion
        
        #region 投射物注册管理
        
        /// <summary>
        /// 注册新的投射物
        /// </summary>
        public int RegisterProjectile(ProjectileBase projectile)
        {
            if (_activeProjectiles.Count >= _maxActiveProjectiles)
            {
                LogActivity("达到最大投射物数量限制，强制清理");
                ForceCleanupOldest();
            }
            
            int id = GetNextProjectileId();
            _activeProjectiles[id] = projectile;
            
            if (_showDebugInfo)
            {
                LogActivity($"注册投射物 ID: {id}, 总数: {_activeProjectiles.Count}");
            }
            
            return id;
        }
        
        /// <summary>
        /// 注销投射物
        /// </summary>
        public void UnregisterProjectile(int projectileId)
        {
            if (_activeProjectiles.ContainsKey(projectileId))
            {
                _activeProjectiles.Remove(projectileId);
                
                if (_showDebugInfo)
                {
                    LogActivity($"注销投射物 ID: {projectileId}, 剩余: {_activeProjectiles.Count}");
                }
            }
        }
          /// <summary>
        /// 请求销毁投射物（简化版本）
        /// </summary>
        public void RequestDestroyProjectile(int projectileId)
        {
            if (_activeProjectiles.ContainsKey(projectileId))
            {
                // 直接执行本地销毁，不再进行复杂的网络同步
                if (_activeProjectiles.TryGetValue(projectileId, out ProjectileBase projectile))
                {
                    if (projectile != null && !projectile.IsDestroyed)
                    {
                        projectile.DestroyProjectile();
                    }
                }
                UnregisterProjectile(projectileId);
                LogActivity($"销毁投射物 ID: {projectileId}");
            }
        }
          #endregion
        
        #region 清理系统
          /// <summary>
        /// 安全销毁投射物
        /// </summary>
        private void SafeDestroyProjectile(ProjectileBase projectile, int projectileId)
        {
            try
            {
                if (projectile != null && projectile.photonView != null)
                {
                    // 检查PhotonView是否仍然有效，避免重复销毁
                    if (projectile.photonView.ViewID != 0 && !projectile.IsDestroyed)
                    {
                        // 通知其他客户端（带延迟确保网络稳定）
                        photonView.RPC("OnProjectileDestroyed", RpcTarget.Others, projectileId);
                        
                        // 本地销毁
                        if (projectile.photonView.IsMine)
                        {
                            projectile.DestroyProjectile();
                        }
                    }
                    else
                    {
                        // PhotonView已经无效或对象已销毁，只做本地清理
                        LogActivity($"投射物 {projectileId} 的PhotonView已无效，仅执行本地清理");
                    }
                }
                
                UnregisterProjectile(projectileId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[投射物管理器] 销毁投射物时发生错误: {e.Message}");
                UnregisterProjectile(projectileId);
            }
        }
          [PunRPC]
        private void OnProjectileDestroyed(int projectileId)
        {
            if (_activeProjectiles.TryGetValue(projectileId, out ProjectileBase projectile))
            {
                if (projectile != null && !projectile.IsDestroyed)
                {
                    projectile.NetworkDestroy();
                }
                UnregisterProjectile(projectileId);
                LogActivity($"接收到销毁通知，投射物 {projectileId} 已清理");
            }
            else
            {
                // 投射物可能已经被销毁了，这是正常的网络延迟现象
                LogActivity($"接收到销毁通知，但投射物 {projectileId} 已不存在（可能是网络延迟）");
            }
        }
          /// <summary>
        /// 强制清理最老的投射物
        /// </summary>
        private void ForceCleanupOldest()
        {
            List<int> keysToRemove = new List<int>();
            
            foreach (var kvp in _activeProjectiles)
            {
                if (kvp.Value == null || kvp.Value.IsDestroyed)
                {
                    keysToRemove.Add(kvp.Key);
                }
                
                if (keysToRemove.Count >= 10) // 简化的固定值
                    break;
            }
            
            foreach (int key in keysToRemove)
            {
                UnregisterProjectile(key);
            }
        }
        
        /// <summary>
        /// 定期清理协程
        /// </summary>
        private IEnumerator CleanupRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(_cleanupInterval);
                
                List<int> keysToRemove = new List<int>();
                
                foreach (var kvp in _activeProjectiles)
                {
                    if (kvp.Value == null || kvp.Value.IsDestroyed)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                
                foreach (int key in keysToRemove)
                {
                    UnregisterProjectile(key);
                }
                
                if (_showDebugInfo && keysToRemove.Count > 0)
                {
                    LogActivity($"定期清理: 移除了 {keysToRemove.Count} 个无效投射物");
                }
            }
        }
          #endregion
        
        #region 辅助方法
          private int GetNextProjectileId()
        {
            return _nextProjectileId++;
        }
        
        private void LogActivity(string message)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[投射物管理器] {message}");
            }
        }
          #endregion
        
        #region 公共API
        
        /// <summary>
        /// 获取当前活跃投射物数量
        /// </summary>
        public int GetActiveProjectileCount()
        {
            return _activeProjectiles.Count;
        }
          /// <summary>
        /// 强制清理所有投射物
        /// </summary>
        public void ClearAllProjectiles()
        {
            foreach (var kvp in _activeProjectiles)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.DestroyProjectile();
                }
            }
            _activeProjectiles.Clear();
        }
        
        #endregion
    }
}
