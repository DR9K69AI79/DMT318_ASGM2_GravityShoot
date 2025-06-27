using UnityEngine;
using Photon.Pun;
using System.Collections;
using DWHITE;

namespace DWHITE.Weapons.Network
{
    /// <summary>
    /// 投射物网络同步组件 - 简化版本
    /// 只处理网络创建和销毁事件，所有物理运动由本地处理
    /// </summary>
    public class ProjectileNetworkSync : NetworkSyncBase, IPunInstantiateMagicCallback
    {
        [Header("网络设置")]
        [SerializeField] private float _networkCullingDistance = 100f;
        [SerializeField] private bool _ownershipTransfer = false;
        
        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = true;
        
        // 组件引用
        private ProjectileBase _projectile;
        
        // 网络状态（仅用于初始化和销毁）
        private bool _networkIsDestroyed = false;
        
        #region Unity 生命周期
        
        private void Awake()
        {
            _projectile = GetComponent<ProjectileBase>();
            
            if (_projectile == null)
            {
                LogNetwork("警告: 未找到ProjectileBase组件");
            }
        }
        
        private void Start()
        {
            LogNetwork($"投射物网络同步已初始化（简化模式） - 是否为本地: {photonView.IsMine}");
        }
        
        private void Update()
        {
            // 简化：只检查销毁条件，不进行位置同步
            if (photonView.IsMine)
            {
                CheckDestroyConditions();
            }
        }
        
        private void OnDestroy()
        {
            LogNetwork("投射物网络同步组件已销毁");
        }
        
        #endregion
        
        #region IPunInstantiateMagicCallback 实现
        
        /// <summary>
        /// 网络实例化回调 - 用于接收网络创建数据
        /// </summary>
        public void OnPhotonInstantiate(PhotonMessageInfo info)
        {
            // 接收网络创建数据
            object[] instantiationData = photonView.InstantiationData;
            
            if (instantiationData != null && instantiationData.Length >= 5) // 简化数据：速度(3) + 伤害(1) + 武器ID(1)
            {
                try
                {
                    // 解析速度
                    Vector3 velocity = new Vector3(
                        (float)instantiationData[0],
                        (float)instantiationData[1], 
                        (float)instantiationData[2]
                    );
                    
                    // 解析伤害值
                    float damage = (float)instantiationData[3];
                    
                    // 获取源武器
                    int sourceWeaponViewID = (int)instantiationData[4];
                    WeaponBase sourceWeapon = null;
                    if (sourceWeaponViewID > 0)
                    {
                        PhotonView weaponView = PhotonView.Find(sourceWeaponViewID);
                        if (weaponView != null)
                        {
                            sourceWeapon = weaponView.GetComponent<WeaponBase>();
                        }
                    }
                    
                    // 配置投射物
                    if (_projectile != null)
                    {
                        Vector3 direction = velocity.normalized;
                        float speed = velocity.magnitude;
                        
                        // 检查是否是StandardProjectile，如果是，使用其专门的网络配置方法
                        var standardProjectile = _projectile as DWHITE.Weapons.StandardProjectile;
                        if (standardProjectile != null)
                        {
                            standardProjectile.ConfigureFromNetworkData(instantiationData);
                            LogNetwork($"StandardProjectile网络配置完成 - 速度: {velocity}, 伤害: {damage}");
                        }
                        else
                        {
                            // 对于其他类型的投射物，使用基础Launch方法
                            _projectile.Launch(direction, speed, sourceWeapon, null);
                            LogNetwork($"基础投射物网络配置完成 - 速度: {velocity}, 伤害: {damage}");
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ProjectileNetworkSync] 网络初始化失败: {e.Message}");
                }
            }
            else
            {
                LogNetwork("警告: 网络实例化数据不完整或为空");
            }
        }
        
        #endregion
        
        #region NetworkSyncBase 实现
        
        protected override void WriteData(PhotonStream stream)
        {
            // 简化版本：投射物不需要持续同步数据
            // 所有运动都由本地物理处理
        }
        
        protected override void ReadData(PhotonStream stream, PhotonMessageInfo info)
        {
            // 简化版本：投射物不需要持续接收数据
            // 所有运动都由本地物理处理
        }
        
        #endregion
        
        #region 生命周期管理
        
        private void CheckDestroyConditions()
        {
            if (_projectile == null) return;
            
            // 检查生命时间
            if (_projectile.Lifetime <= 0)
            {
                RequestDestroy();
                return;
            }
            
            // 检查距离剔除
            if (Vector3.Distance(transform.position, Vector3.zero) > _networkCullingDistance)
            {
                RequestDestroy();
                return;
            }
        }
        
        public void RequestDestroy()
        {
            if (photonView.IsMine && !_networkIsDestroyed)
            {
                _networkIsDestroyed = true;
                
                // 使用延迟销毁确保网络同步
                photonView.RPC("OnProjectileDestroyRPC", RpcTarget.All);
                StartCoroutine(DelayedLocalDestroy());
                
                LogNetwork("请求销毁投射物");
            }
        }
        
        private IEnumerator DelayedLocalDestroy()
        {
            // 等待一帧确保RPC发送
            yield return null;
            DestroyProjectile();
        }
        
        [PunRPC]
        private void OnProjectileDestroyRPC()
        {
            _networkIsDestroyed = true;
            
            if (!photonView.IsMine)
            {
                DestroyProjectile();
            }
        }
        
        private void DestroyProjectile()
        {
            if (_projectile != null)
            {
                _projectile.NetworkDestroy();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        #endregion
        
        #region 特殊事件同步
        
        public void OnProjectileBounce(Vector3 bouncePoint, Vector3 bounceNormal)
        {
            if (photonView.IsMine)
            {
                photonView.RPC("OnProjectileBounceRPC", RpcTarget.Others, 
                    bouncePoint.x, bouncePoint.y, bouncePoint.z,
                    bounceNormal.x, bounceNormal.y, bounceNormal.z);
            }
        }
        
        [PunRPC]
        private void OnProjectileBounceRPC(float posX, float posY, float posZ,
                                         float normalX, float normalY, float normalZ)
        {
            Vector3 bouncePoint = new Vector3(posX, posY, posZ);
            Vector3 bounceNormal = new Vector3(normalX, normalY, normalZ);
            
            // 通知投射物处理弹跳效果
            if (_projectile != null)
            {
                _projectile.OnNetworkBounce(bouncePoint, bounceNormal);
            }
            
            LogNetwork($"接收到弹跳事件 - 位置: {bouncePoint}, 法线: {bounceNormal}");
        }
        
        public void OnProjectileHit(Vector3 hitPoint, Vector3 hitNormal, string targetTag, float damage)
        {
            if (photonView.IsMine)
            {
                photonView.RPC("OnProjectileHitRPC", RpcTarget.Others,
                    hitPoint.x, hitPoint.y, hitPoint.z,
                    hitNormal.x, hitNormal.y, hitNormal.z,
                    targetTag, damage);
            }
        }
        
        [PunRPC]
        private void OnProjectileHitRPC(float posX, float posY, float posZ,
                                      float normalX, float normalY, float normalZ,
                                      string targetTag, float damage)
        {
            Vector3 hitPoint = new Vector3(posX, posY, posZ);
            Vector3 hitNormal = new Vector3(normalX, normalY, normalZ);
            
            // 通知投射物处理命中效果
            if (_projectile != null)
            {
                _projectile.OnNetworkHit(hitPoint, hitNormal, targetTag, damage);
            }
            
            LogNetwork($"接收到命中事件 - 位置: {hitPoint}, 目标: {targetTag}, 伤害: {damage}");
        }
        
        #endregion
        
        #region 调试和日志
        
        private void LogNetwork(string message)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[ProjectileNetworkSync] {message}");
            }
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 启用调试信息（供外部调用）
        /// </summary>
        public void EnableDebugInfo(bool enable)
        {
            _showDebugInfo = enable;
            LogNetwork($"[ProjectileNetworkSync]调试信息已{(enable ? "启用" : "禁用")}");
        }
        
        #endregion
    }
}
