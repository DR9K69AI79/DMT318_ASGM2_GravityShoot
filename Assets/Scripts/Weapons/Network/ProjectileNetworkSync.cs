using UnityEngine;
using Photon.Pun;
using System.Collections;

namespace DWHITE.Weapons.Network
{
    /// <summary>
    /// 投射物网络同步组件
    /// 处理投射物的网络同步和生命周期管理
    /// </summary>
    public class ProjectileNetworkSync : MonoBehaviourPun, IPunObservable
    {
        [Header("同步配置")]
        [SerializeField] private bool _syncPosition = true;
        [SerializeField] private bool _syncRotation = true;
        [SerializeField] private bool _syncVelocity = true;
        [SerializeField] private bool _useInterpolation = true;
        
        [Header("网络设置")]
        [SerializeField] private float _sendRate = 20f;
        [SerializeField] private bool _ownershipTransfer = false;
        [SerializeField] private float _networkCullingDistance = 100f;
        
        [Header("预测设置")]
        [SerializeField] private bool _enableClientPrediction = true;
        [SerializeField] private float _predictionTolerance = 1f;
        [SerializeField] private float _correctionSpeed = 10f;
        
        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = false;
        [SerializeField] private bool _showNetworkGizmos = false;
        
        // 组件引用
        private ProjectileBase _projectile;
        private Rigidbody _rigidbody;
        
        // 网络状态
        private Vector3 _networkPosition;
        private Quaternion _networkRotation;
        private Vector3 _networkVelocity;
        private float _lastSendTime;
        
        // 预测和插值
        private Vector3 _lastReceivePosition;
        private Vector3 _predictedPosition;
        private bool _hasPrediction = false;        // 投射物状态
        private float _networkLifeTime;
        private bool _networkIsDestroyed = false;
        private int _networkBounceCount = 0;
        
        #region Unity 生命周期
        
        private void Awake()
        {
            _projectile = GetComponent<ProjectileBase>();
            _rigidbody = GetComponent<Rigidbody>();
            
            if (_projectile == null)
            {
                LogNetwork("警告: 未找到ProjectileBase组件");
            }
        }        private void Start()
        {
            // 初始化网络状态
            _networkPosition = transform.position;
            _networkRotation = transform.rotation;
            _networkVelocity = _rigidbody ? _rigidbody.velocity : Vector3.zero;
            
            LogNetwork($"投射物网络同步已初始化 - 所有者: {photonView.Owner?.NickName}");
        }
        
        private void Update()
        {
            if (!photonView.IsMine)
            {
                // 非所有者：应用网络状态
                ApplyNetworkState();
            }
            else
            {
                // 所有者：检查是否需要销毁
                CheckDestroyConditions();
            }
        }
        
        private void OnDestroy()
        {
            LogNetwork("投射物网络同步组件已销毁");
        }
        
        #endregion
        
        #region 网络状态应用
        
        private void ApplyNetworkState()
        {
            if (_networkIsDestroyed)
            {
                DestroyProjectile();
                return;
            }
            
            // 应用位置同步
            if (_syncPosition)
            {
                ApplyPositionSync();
            }
            
            // 应用旋转同步
            if (_syncRotation)
            {
                if (_useInterpolation)
                {
                    transform.rotation = Quaternion.Lerp(transform.rotation, _networkRotation, Time.deltaTime * _correctionSpeed);
                }
                else
                {
                    transform.rotation = _networkRotation;
                }
            }
            
            // 应用速度同步
            if (_syncVelocity && _rigidbody != null)
            {
                float velocityDifference = Vector3.Distance(_rigidbody.velocity, _networkVelocity);
                if (velocityDifference > _predictionTolerance)
                {
                    _rigidbody.velocity = Vector3.Lerp(_rigidbody.velocity, _networkVelocity, Time.deltaTime * _correctionSpeed);
                }
            }
        }
        
        private void ApplyPositionSync()
        {
            float distance = Vector3.Distance(transform.position, _networkPosition);
            
            if (_enableClientPrediction && _hasPrediction)
            {
                // 使用客户端预测
                ApplyClientPrediction();
            }
            else if (_useInterpolation && distance < _predictionTolerance)
            {
                // 使用插值
                transform.position = Vector3.Lerp(transform.position, _networkPosition, Time.deltaTime * _correctionSpeed);
            }
            else if (distance > _predictionTolerance)
            {
                // 距离过大，直接设置位置
                transform.position = _networkPosition;
                LogNetwork($"位置校正 - 距离: {distance:F2}");
            }
        }
        
        private void ApplyClientPrediction()
        {
            // 基于最后接收位置和速度进行预测
            float timeSinceReceive = Time.time - _lastSendTime;
            _predictedPosition = _lastReceivePosition + _networkVelocity * timeSinceReceive;
            
            // 应用预测位置
            Vector3 currentPos = transform.position;
            Vector3 targetPos = Vector3.Lerp(_networkPosition, _predictedPosition, 0.5f);
            
            float predictionError = Vector3.Distance(currentPos, targetPos);
            if (predictionError > _predictionTolerance)
            {
                transform.position = Vector3.Lerp(currentPos, targetPos, Time.deltaTime * _correctionSpeed);
            }
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
        }        public void RequestDestroy()
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
                _networkBounceCount++;                photonView.RPC("OnProjectileBounceRPC", RpcTarget.Others, 
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
            
            _networkBounceCount++;
            
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
            {                photonView.RPC("OnProjectileHitRPC", RpcTarget.Others,
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
            
            // 播放命中特效
            if (_projectile != null)
            {
                _projectile.OnNetworkHit(hitPoint, hitNormal, targetTag, damage);
            }
            
            LogNetwork($"接收到命中事件 - 目标: {targetTag}, 伤害: {damage}");
        }
        
        #endregion
        
        #region IPunObservable 实现
        
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // 发送数据
                WriteNetworkData(stream);
            }
            else
            {
                // 接收数据
                ReadNetworkData(stream, info);
            }
        }
        
        private void WriteNetworkData(PhotonStream stream)
        {
            // 位置同步
            if (_syncPosition)
            {
                stream.SendNext(transform.position);
            }
            
            // 旋转同步
            if (_syncRotation)
            {
                stream.SendNext(transform.rotation);
            }
            
            // 速度同步
            if (_syncVelocity && _rigidbody != null)
            {
                stream.SendNext(_rigidbody.velocity);
            }
            else
            {
                stream.SendNext(Vector3.zero);
            }
            
            // 投射物状态
            stream.SendNext(_projectile ? _projectile.Lifetime : 0f);
            stream.SendNext(_networkBounceCount);
        }
        
        private void ReadNetworkData(PhotonStream stream, PhotonMessageInfo info)
        {
            // 接收位置
            if (_syncPosition)
            {
                _lastReceivePosition = _networkPosition;
                _networkPosition = (Vector3)stream.ReceiveNext();
                _hasPrediction = true;
            }
            
            // 接收旋转
            if (_syncRotation)
            {
                _networkRotation = (Quaternion)stream.ReceiveNext();
            }
            
            // 接收速度
            if (_syncVelocity)
            {
                _networkVelocity = (Vector3)stream.ReceiveNext();
            }
            
            // 接收投射物状态
            _networkLifeTime = (float)stream.ReceiveNext();
            _networkBounceCount = (int)stream.ReceiveNext();
            
            // 记录接收时间
            _lastSendTime = Time.time;
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 设置同步选项
        /// </summary>
        public void SetSyncOptions(bool position = true, bool rotation = true, bool velocity = true)
        {
            _syncPosition = position;
            _syncRotation = rotation;
            _syncVelocity = velocity;
        }
        
        /// <summary>
        /// 设置网络发送频率
        /// </summary>
        public void SetSendRate(float rate)
        {
            _sendRate = Mathf.Max(1f, rate);
        }
        
        /// <summary>
        /// 启用/禁用客户端预测
        /// </summary>
        public void SetClientPrediction(bool enabled, float tolerance = 1f)
        {
            _enableClientPrediction = enabled;
            _predictionTolerance = tolerance;
        }
        
        /// <summary>
        /// 强制同步当前状态
        /// </summary>
        public void ForceSyncState()
        {
            if (photonView.IsMine)
            {
                // 强制发送当前状态
                _lastSendTime = 0f;
            }
        }
        
        #endregion
        
        #region 调试和可视化
        
        private void OnDrawGizmos()
        {
            if (!_showNetworkGizmos) return;
            
            // 绘制网络位置
            if (!photonView.IsMine)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_networkPosition, 0.2f);
                
                // 绘制预测位置
                if (_hasPrediction)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(_predictedPosition, 0.15f);
                }
                
                // 绘制连接线
                Gizmos.color = Color.white;
                Gizmos.DrawLine(transform.position, _networkPosition);
            }
        }
        
        private void LogNetwork(string message)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[投射物网络同步] {message}");
            }
        }
        
        #endregion
    }
}
