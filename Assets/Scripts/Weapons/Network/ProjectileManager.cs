using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Photon.Pun;

namespace DWHITE.Weapons.Network
{
    /// <summary>
    /// 投射物管理器
    /// 处理投射物的网络创建、销毁和池化，减少网络开销
    /// </summary>
    public class ProjectileManager : MonoBehaviourPun, IPunObservable
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
        
        [Header("投射物池配置")]
        [SerializeField] private int _maxActiveProjectiles = 100;
        [SerializeField] private float _cleanupInterval = 5f;
        [SerializeField] private float _networkUpdateRate = 30f;
        
        [Header("性能优化")]
        [SerializeField] private bool _enableCulling = true;
        [SerializeField] private float _cullingDistance = 200f;
        [SerializeField] private bool _batchNetworkCalls = true;
        [SerializeField] private int _maxBatchSize = 10;
        
        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = false;
        [SerializeField] private bool _logNetworkActivity = false;
        
        #endregion
        
        #region 状态管理
        
        private Dictionary<int, ProjectileBase> _activeProjectiles = new Dictionary<int, ProjectileBase>();
        private Queue<int> _pendingDestroyQueue = new Queue<int>();
        private List<ProjectileData> _batchedProjectileData = new List<ProjectileData>();
        private float _lastNetworkUpdate;
        private int _nextProjectileId = 1;
        
        [System.Serializable]
        private struct ProjectileData
        {
            public int id;
            public Vector3 position;
            public Vector3 velocity;
            public Quaternion rotation;
            public float damage;
            public bool isDestroyed;
        }
        
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
            if (photonView.IsMine)
            {
                ProcessPendingDestroy();
                
                if (_batchNetworkCalls && Time.time - _lastNetworkUpdate >= 1f / _networkUpdateRate)
                {
                    BatchUpdateProjectiles();
                    _lastNetworkUpdate = Time.time;
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
        /// 请求销毁投射物
        /// </summary>
        public void RequestDestroyProjectile(int projectileId)
        {
            if (_activeProjectiles.ContainsKey(projectileId))
            {
                _pendingDestroyQueue.Enqueue(projectileId);
                LogActivity($"请求销毁投射物 ID: {projectileId}");
            }
        }
        
        #endregion
        
        #region 网络同步优化
        
        /// <summary>
        /// 批量更新投射物状态
        /// </summary>
        private void BatchUpdateProjectiles()
        {
            _batchedProjectileData.Clear();
            
            foreach (var kvp in _activeProjectiles)
            {
                ProjectileBase projectile = kvp.Value;
                if (projectile != null && !projectile.IsDestroyed)
                {
                    // 距离剔除
                    if (_enableCulling && Vector3.Distance(projectile.transform.position, transform.position) > _cullingDistance)
                    {
                        RequestDestroyProjectile(kvp.Key);
                        continue;
                    }
                    
                    _batchedProjectileData.Add(new ProjectileData
                    {
                        id = kvp.Key,
                        position = projectile.transform.position,
                        velocity = projectile.Velocity,
                        rotation = projectile.transform.rotation,
                        damage = projectile.Damage,
                        isDestroyed = false
                    });
                }
                else
                {
                    RequestDestroyProjectile(kvp.Key);
                }
                
                if (_batchedProjectileData.Count >= _maxBatchSize)
                    break;
            }
            
            // 通过网络同步批量数据
            if (_batchedProjectileData.Count > 0)
            {
                photonView.RPC("OnProjectileBatchUpdate", RpcTarget.Others, SerializeProjectileData(_batchedProjectileData.ToArray()));
            }
        }
        
        [PunRPC]
        private void OnProjectileBatchUpdate(byte[] serializedData)
        {
            ProjectileData[] projectileData = DeserializeProjectileData(serializedData);
            
            foreach (var data in projectileData)
            {
                if (_activeProjectiles.TryGetValue(data.id, out ProjectileBase projectile))
                {
                    if (projectile != null && !projectile.IsDestroyed)
                    {
                        // 应用网络状态
                        ApplyNetworkStateToProjectile(projectile, data);
                    }
                }
            }
        }
        
        #endregion
        
        #region 清理系统
        
        /// <summary>
        /// 处理待销毁队列
        /// </summary>
        private void ProcessPendingDestroy()
        {
            int processCount = 0;
            while (_pendingDestroyQueue.Count > 0 && processCount < _maxBatchSize)
            {
                int projectileId = _pendingDestroyQueue.Dequeue();
                
                if (_activeProjectiles.TryGetValue(projectileId, out ProjectileBase projectile))
                {
                    SafeDestroyProjectile(projectile, projectileId);
                }
                
                processCount++;
            }
        }
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
                
                if (keysToRemove.Count >= _maxBatchSize)
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
        
        #region 数据序列化
        
        private byte[] SerializeProjectileData(ProjectileData[] data)
        {
            // 简单的二进制序列化，实际项目中可以使用更高效的方案
            return System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new SerializableArray<ProjectileData> { items = data }));
        }
        
        private ProjectileData[] DeserializeProjectileData(byte[] data)
        {
            string json = System.Text.Encoding.UTF8.GetString(data);
            return JsonUtility.FromJson<SerializableArray<ProjectileData>>(json).items;
        }
        
        [System.Serializable]
        private class SerializableArray<T>
        {
            public T[] items;
        }
        
        #endregion
        
        #region 辅助方法
        
        private int GetNextProjectileId()
        {
            return _nextProjectileId++;
        }
        
        private void ApplyNetworkStateToProjectile(ProjectileBase projectile, ProjectileData data)
        {
            // 应用位置、旋转和速度
            projectile.transform.position = Vector3.Lerp(projectile.transform.position, data.position, Time.deltaTime * 10f);
            projectile.transform.rotation = Quaternion.Lerp(projectile.transform.rotation, data.rotation, Time.deltaTime * 10f);
            
            if (projectile.GetComponent<Rigidbody>() != null)
            {
                projectile.GetComponent<Rigidbody>().velocity = Vector3.Lerp(projectile.GetComponent<Rigidbody>().velocity, data.velocity, Time.deltaTime * 5f);
            }
        }
        
        private void LogActivity(string message)
        {
            if (_logNetworkActivity)
            {
                Debug.Log($"[投射物管理器] {message}");
            }
        }
        
        #endregion
        
        #region IPunObservable 实现
        
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // 发送活跃投射物数量
                stream.SendNext(_activeProjectiles.Count);
            }
            else
            {
                // 接收数据（用于监控）
                int remoteCount = (int)stream.ReceiveNext();
                if (_showDebugInfo)
                {
                    LogActivity($"远程投射物数量: {remoteCount}, 本地: {_activeProjectiles.Count}");
                }
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
                    SafeDestroyProjectile(kvp.Value, kvp.Key);
                }
            }
            _activeProjectiles.Clear();
            _pendingDestroyQueue.Clear();
        }
        
        #endregion
    }
}
