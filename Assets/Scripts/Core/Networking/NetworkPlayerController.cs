using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

namespace DWHITE
{
    /// <summary>
    /// 网络玩家控制器 - 处理玩家的网络同步和状态管理
    /// 基于项目的物理重力系统设计，支持客户端预测和服务器校正
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class NetworkPlayerController : NetworkSyncBase
    {
        #region Configuration
        
        [Header("网络同步设置")]
        [SerializeField] private float _sendRate = 30f;
        [SerializeField] private float _interpolationRate = 15f;
        [SerializeField] private float _snapThreshold = 2f;
        
        [Header("平滑插值")]
        [SerializeField] private bool _enableSmoothing = true;
        
        [Header("重力同步")]
        [SerializeField] private bool _syncGravityDirection = true;
        [SerializeField] private float _gravitySnapThreshold = 0.1f;
        
        [Header("调试")]
        [SerializeField] private bool _showNetworkDebug = false;
        [SerializeField] private bool _showPredictionGizmos = false;
        [SerializeField] private bool _showSyncStats = false;

        [Header("引用")]
        [SerializeField] private GameObject _playerCamera;
        [SerializeField] private GameObject _gameUI;
        [SerializeField] private List<GameObject> _localCullingObjects;
        
        #endregion
        
        #region Network State Data

        private struct NetworkPlayerState
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity;
            public Vector3 gravityDirection;
            public bool isGrounded;
            public float timestamp;
            public int frameNumber;

            // 输入状态
            public Vector2 moveInput;
            public Vector2 lookInput;
            public bool jumpPressed;
            public bool firePressed;
            
            // PlayerState 数据用于动画同步
            public bool isSprinting;
            public bool isJumping;
            public bool isOnSteep;
            public float speed;
            public float speedMultiplier;
            public Vector3 upAxis;
            public Vector3 forwardAxis;
            public Vector3 rightAxis;
        }
        
        #endregion
        
        #region Private Fields
          // 组件引用
        private PlayerMotor _playerMotor;
        private PlayerInput _playerInput;
        private PlayerView _playerView;
        private PlayerStatusManager _playerStatusManager;
        private Rigidbody _rigidbody;

        // 网络状态
        private NetworkPlayerState _networkState;
        private NetworkPlayerState _previousState;
        private NetworkPlayerState _targetState;
        
        // 远程玩家状态缓存
        private PlayerStateData _remotePlayerState;
        
        // 插值
        private Vector3 _networkPosition;
        private Quaternion _networkRotation;
        private Vector3 _networkVelocity;
        private Vector3 _networkGravityDirection;
        private bool _networkIsGrounded;
        
        // 时间和帧同步
        private float _lastSendTime;
        private float _lastReceiveTime;
        private float _sendInterval;
        
        #endregion
        
        #region Properties
        
        public bool IsLocalPlayer => photonView.IsMine;
        public float NetworkLatency => (float)(PhotonNetwork.Time - _lastReceiveTime);
        public bool IsGrounded => _networkIsGrounded;
        
        #endregion
        
        #region Unity Lifecycle
          private void Awake()
        {
            // 获取组件引用
            _playerMotor = GetComponent<PlayerMotor>();
            _playerInput = GetComponent<PlayerInput>();
            _playerView = GetComponentInChildren<PlayerView>();
            _rigidbody = GetComponent<Rigidbody>();
            _playerStatusManager = GetComponent<PlayerStatusManager>();
            
            // 计算发送间隔
            _sendInterval = 1f / _sendRate;
        }
        
        private void Start()
        {
            // 只有本地玩家启用输入和物理
            if (IsLocalPlayer)
            {
                EnableLocalPlayerComponents();
                InitializeLocalPlayerVisiual();
            }
            else
            {
                EnableRemotePlayerComponents();
                InitializeRemotePlayerVisual();
            }
            
            // 设置网络视图观察目标
            if (photonView.ObservedComponents.Count == 0)
            {
                photonView.ObservedComponents.Add(this);
            }
            
            LogNetworkDebug($"网络玩家初始化 - 本地玩家: {IsLocalPlayer}");
        }
        
        private void FixedUpdate()
        {
            if (IsLocalPlayer)
            {
                HandleLocalPlayerUpdate();
            }
            else
            {
                HandleRemotePlayerUpdate();
            }
        }
        
        private void Update()
        {
            if (!IsLocalPlayer)
            {
                InterpolateRemotePlayer();
                SyncRemotePlayerPhysics();
                LogSyncStats();
            }
        }
        
        #endregion
        
        #region Component Management
        
        /// <summary>
        /// 启用本地玩家组件
        /// </summary>
        private void EnableLocalPlayerComponents()
        {
            // 启用输入和摄像机
            if (_playerInput != null) _playerInput.enabled = true;
            if (_playerView != null) _playerView.enabled = true;
            if (_gameUI != null) _gameUI.SetActive(true); // 本地玩家需要UI
            
            // 启用物理模拟
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                _rigidbody.useGravity = false; // 使用自定义重力
            }
        }

        /// <summary>
        /// 初始化本地玩家视觉
        /// </summary>
        private void InitializeLocalPlayerVisiual()
        {
            _playerCamera.SetActive(true);
            foreach (var obj in _localCullingObjects)
            {
                if (obj != null)
                {
                    obj.layer = LayerMask.NameToLayer("LocalPlayerCulling");
                }
            }
        }

        /// <summary>
        /// 启用远程玩家组件
        /// </summary>
        private void EnableRemotePlayerComponents()
        {
            // 禁用输入和摄像机
            if (_playerInput != null) _playerInput.enabled = false;
            if (_playerView != null) _playerView.enabled = false;
            if (_gameUI != null) _gameUI.SetActive(false); // 远程玩家不需要本地UI

            // 禁用不需要的组件，但保持物理组件
            if (_playerMotor != null)
            {
                _playerMotor.enabled = false; // 远程玩家不需要本地物理计算
            }
            
            // 确保远程玩家的Rigidbody设置正确
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true; // 远程玩家使用Kinematic模式
                _rigidbody.useGravity = false; // 禁用重力
                _rigidbody.velocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
            
            LogNetworkDebug("远程玩家组件设置完成 - Rigidbody设为Kinematic模式");
        }
        
        /// <summary>
        /// 初始化远程玩家视觉
        /// </summary>
        private void InitializeRemotePlayerVisual()
        {
            _playerCamera.SetActive(false);
            foreach (var obj in _localCullingObjects)
            {
                if (obj != null)
                {
                    obj.layer = LayerMask.NameToLayer("Default"); // 恢复默认层
                }
            }
        }
        
        #endregion

        #region Local Player Handling        /// <summary>
        /// 处理本地玩家更新
        /// </summary>
        private void HandleLocalPlayerUpdate()
        {
            if (_playerMotor == null || _playerInput == null) return;

            // 收集PlayerState数据用于动画同步
            PlayerStateData localPlayerState = PlayerStateData.Empty;            if (_playerStatusManager != null)
            {
                localPlayerState = _playerStatusManager.GetStateSnapshot();
            }

            // 收集当前状态
            NetworkPlayerState currentState = new NetworkPlayerState
            {
                position = transform.position,
                rotation = transform.rotation,
                velocity = _rigidbody.velocity,
                gravityDirection = CustomGravity.GetGravity(transform.position).normalized,
                isGrounded = _playerMotor.IsGrounded,
                timestamp = (float)PhotonNetwork.Time,
                frameNumber = Time.fixedUnscaledTime.GetHashCode(),

                // 输入状态
                moveInput = _playerInput.MoveInput,
                lookInput = _playerInput.LookInput,
                jumpPressed = _playerInput.JumpPressed,
                firePressed = _playerInput.FirePressed,
                
                // PlayerState数据用于动画同步
                isSprinting = localPlayerState.isSprinting,
                isJumping = localPlayerState.isJumping,
                isOnSteep = localPlayerState.isOnSteep,
                speed = localPlayerState.speed,
                speedMultiplier = localPlayerState.currentSpeedMultiplier,
                upAxis = localPlayerState.upAxis,
                forwardAxis = localPlayerState.forwardAxis,
                rightAxis = localPlayerState.rightAxis
            };

            // 定期发送状态
            if (Time.time - _lastSendTime >= _sendInterval)
            {
                _networkState = currentState;
                _lastSendTime = Time.time;

                LogNetworkDebug($"发送状态 - 位置: {currentState.position}, 速度: {currentState.velocity}, Sprint: {currentState.isSprinting}");
            }
        }
        
        #endregion
        
        #region Remote Player Handling
        
        /// <summary>
        /// 处理远程玩家更新
        /// </summary>
        private void HandleRemotePlayerUpdate()
        {
            // 应用网络状态
            ApplyNetworkState();
        }
        
        /// <summary>
        /// 插值远程玩家
        /// </summary>
        private void InterpolateRemotePlayer()
        {
            if (_rigidbody == null) return;
            
            // 位置插值
            float distance = Vector3.Distance(transform.position, _networkPosition);
            
            if (distance > _snapThreshold)
            {
                // 距离过大，直接瞬移
                transform.position = _networkPosition;
                transform.rotation = _networkRotation;
                _teleportCount++;
                LogNetworkDebug($"位置瞬移 - 距离: {distance}m");
                
                // 瞬移时同时设置Rigidbody位置，确保物理碰撞箱同步
                if (_rigidbody != null && !_rigidbody.isKinematic)
                {
                    _rigidbody.position = _networkPosition;
                    _rigidbody.rotation = _networkRotation;
                    // 清除速度避免抖动
                    _rigidbody.velocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
            }
            else if (_enableSmoothing)
            {
                // 平滑插值 - 使用更稳定的插值方式
                float lerpSpeed = Time.deltaTime * _interpolationRate;
                
                // 位置插值
                Vector3 targetPosition = Vector3.Lerp(transform.position, _networkPosition, lerpSpeed);
                transform.position = targetPosition;
                
                // 旋转插值
                Quaternion targetRotation = Quaternion.Lerp(transform.rotation, _networkRotation, lerpSpeed);
                transform.rotation = targetRotation;
                
                // 同步更新Rigidbody位置
                if (_rigidbody != null && !_rigidbody.isKinematic)
                {
                    _rigidbody.MovePosition(targetPosition);
                    _rigidbody.MoveRotation(targetRotation);
                }
                
                _smoothUpdateCount++;
            }
            else
            {
                // 直接设置位置
                transform.position = _networkPosition;
                transform.rotation = _networkRotation;
                
                // 同步Rigidbody
                if (_rigidbody != null && !_rigidbody.isKinematic)
                {
                    _rigidbody.position = _networkPosition;
                    _rigidbody.rotation = _networkRotation;
                }
            }
            
            DiagnoseSyncIssues();
        }
        
        /// <summary>
        /// 应用网络状态
        /// </summary>
        private void ApplyNetworkState()
        {
            // 直接应用网络位置，不做延迟补偿
            _networkPosition = _targetState.position;
            _networkRotation = _targetState.rotation;
            _networkVelocity = _targetState.velocity;
            _networkGravityDirection = _targetState.gravityDirection;
            _networkIsGrounded = _targetState.isGrounded;
        }
          /// <summary>
        /// 应用远程玩家的PlayerState数据
        /// </summary>
        private void ApplyRemotePlayerState()
        {
            if (_playerStatusManager == null) return;
            
            // 创建PlayerStateData快照
            var remoteStateData = new PlayerStateData
            {
                isSprinting = _targetState.isSprinting,
                isJumping = _targetState.isJumping,
                isOnSteep = _targetState.isOnSteep,
                speed = _targetState.speed,
                currentSpeedMultiplier = _targetState.speedMultiplier,
                upAxis = _targetState.upAxis,
                forwardAxis = _targetState.forwardAxis,
                rightAxis = _targetState.rightAxis,
                // 将其他基本状态也同步
                velocity = _targetState.velocity,
                isGrounded = _targetState.isGrounded,
                moveInput = _targetState.moveInput,
                gravityDirection = _targetState.gravityDirection,
                jumpPressed = _targetState.jumpPressed
            };
            
            // 对于远程玩家，直接存储状态快照供动画系统使用
            _remotePlayerState = remoteStateData;
            
            LogNetworkDebug($"应用远程PlayerState - Sprint: {remoteStateData.isSprinting}, Jump: {remoteStateData.isJumping}, Speed: {remoteStateData.speed:F2}");
        }
        
        /// <summary>
        /// 强制同步远程玩家的物理状态
        /// </summary>
        private void SyncRemotePlayerPhysics()
        {
            if (IsLocalPlayer || _rigidbody == null) return;
            
            // 确保Transform和Rigidbody位置一致
            if (Vector3.Distance(_rigidbody.position, transform.position) > 0.01f)
            {
                _rigidbody.position = transform.position;
                LogNetworkDebug("修正Rigidbody位置不一致问题");
            }
            
            if (Quaternion.Angle(_rigidbody.rotation, transform.rotation) > 1f)
            {
                _rigidbody.rotation = transform.rotation;
                LogNetworkDebug("修正Rigidbody旋转不一致问题");
            }
        }
        
        #endregion
        
        #region NetworkSyncBase Implementation
        protected override void WriteData(PhotonStream stream)
        {
            stream.SendNext(_networkState.position);
            stream.SendNext(_networkState.rotation);
            stream.SendNext(_networkState.velocity);

            if (_syncGravityDirection)
            {
                stream.SendNext(_networkState.gravityDirection);
            }

            stream.SendNext(_networkState.isGrounded);
            stream.SendNext(_networkState.timestamp);

            stream.SendNext(_networkState.moveInput);
            stream.SendNext(_networkState.lookInput);
            stream.SendNext(_networkState.jumpPressed);
            stream.SendNext(_networkState.firePressed);

            stream.SendNext(_networkState.isSprinting);
            stream.SendNext(_networkState.isJumping);
            stream.SendNext(_networkState.isOnSteep);
            stream.SendNext(_networkState.speed);
            stream.SendNext(_networkState.speedMultiplier);
            stream.SendNext(_networkState.upAxis);
            stream.SendNext(_networkState.forwardAxis);
            stream.SendNext(_networkState.rightAxis);
        }

        protected override void ReadData(PhotonStream stream, PhotonMessageInfo info)
        {
            _targetState.position = (Vector3)stream.ReceiveNext();
            _targetState.rotation = (Quaternion)stream.ReceiveNext();
            _targetState.velocity = (Vector3)stream.ReceiveNext();

            if (_syncGravityDirection)
            {
                _targetState.gravityDirection = (Vector3)stream.ReceiveNext();
            }

            _targetState.isGrounded = (bool)stream.ReceiveNext();
            _targetState.timestamp = (float)stream.ReceiveNext();

            _targetState.moveInput = (Vector2)stream.ReceiveNext();
            _targetState.lookInput = (Vector2)stream.ReceiveNext();
            _targetState.jumpPressed = (bool)stream.ReceiveNext();
            _targetState.firePressed = (bool)stream.ReceiveNext();

            _targetState.isSprinting = (bool)stream.ReceiveNext();
            _targetState.isJumping = (bool)stream.ReceiveNext();
            _targetState.isOnSteep = (bool)stream.ReceiveNext();
            _targetState.speed = (float)stream.ReceiveNext();
            _targetState.speedMultiplier = (float)stream.ReceiveNext();
            _targetState.upAxis = (Vector3)stream.ReceiveNext();
            _targetState.forwardAxis = (Vector3)stream.ReceiveNext();
            _targetState.rightAxis = (Vector3)stream.ReceiveNext();

            _lastReceiveTime = (float)PhotonNetwork.Time;

            ApplyRemotePlayerState();

            LogNetworkDebug($"接收状态 - 位置: {_targetState.position}, 延迟: {info.SentServerTime}");
        }

        #endregion
        
        #region Public API
        
        /// <summary>
        /// 设置网络同步参数
        /// </summary>
        public void SetNetworkParams(float sendRate, float interpolationRate, float snapThreshold)
        {
            _sendRate = Mathf.Clamp(sendRate, 10f, 60f);
            _interpolationRate = Mathf.Clamp(interpolationRate, 5f, 30f);
            _snapThreshold = Mathf.Clamp(snapThreshold, 0.5f, 5f);
            
            _sendInterval = 1f / _sendRate;
            
            LogNetworkDebug($"网络参数更新 - 发送频率: {_sendRate}Hz, 插值: {_interpolationRate}, 瞬移阈值: {_snapThreshold}m");
        }
        
        /// <summary>
        /// 强制同步位置
        /// </summary>
        public void ForceSync()
        {
            if (IsLocalPlayer)
            {
                _lastSendTime = 0f; // 强制下一帧发送
            }
        }
        
        /// <summary>
        /// 获取网络状态信息
        /// </summary>
        public string GetNetworkInfo()
        {
            return $"延迟: {NetworkLatency * 1000:F1}ms | " +
                   $"位置: {transform.position} | " +
                   $"速度: {_networkVelocity.magnitude:F2}m/s | " +
                   $"本地玩家: {IsLocalPlayer}";
        }
        
        /// <summary>
        /// 获取远程玩家的PlayerState数据（仅用于动画系统）
        /// </summary>
        /// <returns>远程玩家的状态快照，如果是本地玩家则返回Empty</returns>
        public PlayerStateData GetRemotePlayerState()
        {
            if (IsLocalPlayer)
            {
                return PlayerStateData.Empty;
            }
            
            return _remotePlayerState;
        }
        
        /// <summary>
        /// 检查是否为远程玩家且有有效的状态数据
        /// </summary>
        /// <returns>true如果是远程玩家且有状态数据</returns>
        public bool HasRemotePlayerState()
        {
            return !IsLocalPlayer && _remotePlayerState.speed >= 0; // 简单检查状态是否有效
        }
        #endregion
        
        #region Utility Methods
        
        private void LogNetworkDebug(string message)
        {
            if (_showNetworkDebug)
            {
                Debug.Log($"[NetworkPlayerController][{(IsLocalPlayer ? "Local" : "Remote")}] {message}");
            }
        }
        
        private void LogSyncStats()
        {
            if (_showSyncStats && !IsLocalPlayer)
            {
                float positionError = Vector3.Distance(transform.position, _networkPosition);
                float rotationError = Quaternion.Angle(transform.rotation, _networkRotation);
                
                Debug.Log($"[SyncStats] PosError: {positionError:F3}m, RotError: {rotationError:F1}°, Latency: {NetworkLatency*1000:F1}ms");
            }
        }
        
        #endregion
        
        #region Gizmos
        
        private void OnDrawGizmos()
        {
            if (!_showPredictionGizmos) return;
            
            // 绘制网络位置
            Gizmos.color = IsLocalPlayer ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            
            // 绘制速度向量
            if (_networkVelocity.magnitude > 0.1f)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(transform.position, _networkVelocity);
            }
            
            // 绘制重力方向
            if (_syncGravityDirection && _networkGravityDirection.magnitude > 0.1f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(transform.position, _networkGravityDirection * 2f);
            }
        }
        
        #endregion
        
        #region Synchronization Debugging
        
        [Header("同步问题诊断")]
        [SerializeField] private bool _enableSyncDiagnostics = false;
        [SerializeField] private float _positionErrorThreshold = 0.1f;
        [SerializeField] private float _velocityDifferenceThreshold = 1f;
        
        private Vector3 _lastNetworkPosition;
        private float _lastDiagnosticTime;
        private int _teleportCount;
        private int _smoothUpdateCount;
        
        /// <summary>
        /// 诊断网络同步问题
        /// </summary>
        private void DiagnoseSyncIssues()
        {
            if (!_enableSyncDiagnostics || IsLocalPlayer) return;
            
            float currentTime = Time.time;
            if (currentTime - _lastDiagnosticTime < 1f) return; // 每秒检查一次
            
            _lastDiagnosticTime = currentTime;
            
            // 检查位置误差
            float posError = Vector3.Distance(transform.position, _networkPosition);
            if (posError > _positionErrorThreshold)
            {
                Debug.LogWarning($"[同步诊断] 位置误差过大: {posError:F3}m " +
                    $"本地位置: {transform.position} 网络位置: {_networkPosition}");
            }
            
            // 检查速度变化
            float velocityChange = Vector3.Distance(_lastNetworkPosition, _networkPosition) / Time.fixedDeltaTime;
            if (velocityChange > _velocityDifferenceThreshold)
            {
                Debug.LogWarning($"[同步诊断] 速度变化过大: {velocityChange:F2}m/s " +
                    $"可能导致抖动");
            }
            
            // 记录瞬移和平滑更新的比例
            float teleportRatio = _teleportCount / (float)(_teleportCount + _smoothUpdateCount + 1);
            if (teleportRatio > 0.1f) // 超过10%的瞬移
            {
                Debug.LogWarning($"[同步诊断] 瞬移比例过高: {teleportRatio:P1} " +
                    $"瞬移次数: {_teleportCount}, 平滑次数: {_smoothUpdateCount}");
            }
            
            _lastNetworkPosition = _networkPosition;
        }
        
        #endregion
    }
}
