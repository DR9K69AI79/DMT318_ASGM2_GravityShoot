using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

namespace DWHITE
{
    /// <summary>
    /// 网络玩家控制器 - 处理玩家的网络同步和状态管理
    /// 基于项目的物理重力系统设计，支持客户端预测和服务器校正
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class NetworkPlayerController : MonoBehaviourPun, IPunObservable
    {
        #region Configuration
        
        [Header("网络同步设置")]
        [SerializeField] private float _sendRate = 30f;
        [SerializeField] private float _interpolationRate = 15f;
        [SerializeField] private float _snapThreshold = 2f;
        
        [Header("预测和校正")]
        [SerializeField] private bool _enableClientPrediction = true;
        [SerializeField] private bool _enableLagCompensation = true;
        [SerializeField] private float _maxPredictionTime = 0.5f;
        [SerializeField] private float _reconciliationThreshold = 0.3f;
        
        [Header("重力同步")]
        [SerializeField] private bool _syncGravityDirection = true;
        [SerializeField] private float _gravitySnapThreshold = 0.1f;
        
        [Header("调试")]
        [SerializeField] private bool _showNetworkDebug = false;
        [SerializeField] private bool _showPredictionGizmos = false;
        
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
        }
        
        #endregion
        
        #region Private Fields
        
        // 组件引用
        private PlayerMotor _playerMotor;
        private PlayerInput _playerInput;
        private PlayerView _playerView;
        private Rigidbody _rigidbody;
        
        // 网络状态
        private NetworkPlayerState _networkState;
        private NetworkPlayerState _previousState;
        private NetworkPlayerState _targetState;
        
        // 预测和插值
        private Vector3 _networkPosition;
        private Quaternion _networkRotation;
        private Vector3 _networkVelocity;
        private Vector3 _networkGravityDirection;
        private bool _networkIsGrounded;
        
        // 时间和帧同步
        private float _lastSendTime;
        private float _lastReceiveTime;
        private float _sendInterval;
        
        // 客户端预测
        private System.Collections.Generic.Queue<NetworkPlayerState> _stateHistory;
        private const int MAX_STATE_HISTORY = 64;
        
        #endregion
        
        #region Properties
        
        public bool IsLocalPlayer => photonView.IsMine;
        //public Player Owner => photonView.owner;
        public float NetworkLatency => (float)(PhotonNetwork.Time - _lastReceiveTime);
        public Vector3 PredictedPosition { get; private set; }
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
            
            // 初始化状态历史
            _stateHistory = new System.Collections.Generic.Queue<NetworkPlayerState>();
            
            // 计算发送间隔
            _sendInterval = 1f / _sendRate;
        }
        
        private void Start()
        {
            // 只有本地玩家启用输入和物理
            if (IsLocalPlayer)
            {
                EnableLocalPlayerComponents();
            }
            else
            {
                EnableRemotePlayerComponents();
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
            
            // 启用物理模拟
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                _rigidbody.useGravity = false; // 使用自定义重力
            }
            
            // 设置摄像机
            if (_playerView != null && Camera.main != null)
            {
                Camera.main.transform.SetParent(_playerView.transform);
                Camera.main.transform.localPosition = Vector3.zero;
                Camera.main.transform.localRotation = Quaternion.identity;
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
            
            // 设置为运动学物理（通过网络同步位置）
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }
            
            // 禁用不需要的组件
            if (_playerMotor != null)
            {
                _playerMotor.enabled = false; // 远程玩家不需要本地物理计算
            }
        }
        
        #endregion
        
        #region Local Player Handling
        
        /// <summary>
        /// 处理本地玩家更新
        /// </summary>
        private void HandleLocalPlayerUpdate()
        {
            if (_playerMotor == null || _playerInput == null) return;
            
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
                firePressed = _playerInput.FirePressed
            };
            
            // 客户端预测
            if (_enableClientPrediction)
            {
                RecordStateHistory(currentState);
            }
            
            // 定期发送状态
            if (Time.time - _lastSendTime >= _sendInterval)
            {
                _networkState = currentState;
                _lastSendTime = Time.time;
                
                LogNetworkDebug($"发送状态 - 位置: {currentState.position}, 速度: {currentState.velocity}");
            }
        }
        
        /// <summary>
        /// 记录状态历史（客户端预测）
        /// </summary>
        private void RecordStateHistory(NetworkPlayerState state)
        {
            _stateHistory.Enqueue(state);
            
            while (_stateHistory.Count > MAX_STATE_HISTORY)
            {
                _stateHistory.Dequeue();
            }
        }
        
        /// <summary>
        /// 服务器校正（客户端预测）
        /// </summary>
        public void ServerReconciliation(Vector3 serverPosition, float serverTime)
        {
            if (!IsLocalPlayer || !_enableClientPrediction) return;
            
            // 查找对应时间戳的预测状态
            NetworkPlayerState? matchingState = FindStateByTimestamp(serverTime);
            
            if (matchingState.HasValue)
            {
                float positionError = Vector3.Distance(matchingState.Value.position, serverPosition);
                
                if (positionError > _reconciliationThreshold)
                {
                    LogNetworkDebug($"服务器校正 - 误差: {positionError}m");
                    
                    // 修正位置并重新模拟
                    transform.position = serverPosition;
                    ReplayFromState(matchingState.Value);
                }
            }
        }
        
        /// <summary>
        /// 根据时间戳查找状态
        /// </summary>
        private NetworkPlayerState? FindStateByTimestamp(float timestamp)
        {
            foreach (var state in _stateHistory)
            {
                if (Mathf.Abs(state.timestamp - timestamp) < 0.1f)
                {
                    return state;
                }
            }
            return null;
        }
        
        /// <summary>
        /// 从指定状态重新模拟
        /// </summary>
        private void ReplayFromState(NetworkPlayerState fromState)
        {
            // 这里可以实现更复杂的状态回放逻辑
            // 目前简单地设置位置和速度
            transform.position = fromState.position;
            _rigidbody.velocity = fromState.velocity;
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
                LogNetworkDebug($"位置瞬移 - 距离: {distance}m");
            }
            else
            {
                // 平滑插值
                transform.position = Vector3.Lerp(transform.position, _networkPosition, Time.deltaTime * _interpolationRate);
                transform.rotation = Quaternion.Lerp(transform.rotation, _networkRotation, Time.deltaTime * _interpolationRate);
            }
            
            // 应用预测位置（延迟补偿）
            if (_enableLagCompensation)
            {
                float lagTime = NetworkLatency;
                PredictedPosition = _networkPosition + _networkVelocity * lagTime;
            }
            else
            {
                PredictedPosition = _networkPosition;
            }
        }
        
        /// <summary>
        /// 应用网络状态
        /// </summary>
        private void ApplyNetworkState()
        {
            // 计算时间差进行延迟补偿
            float timeDifference = (float)(PhotonNetwork.Time - _lastReceiveTime);
            
            // 预测位置
            _networkPosition = _targetState.position;
            _networkRotation = _targetState.rotation;
            _networkVelocity = _targetState.velocity;
            _networkGravityDirection = _targetState.gravityDirection;
            _networkIsGrounded = _targetState.isGrounded;
            
            if (_enableLagCompensation && timeDifference > 0)
            {
                _networkPosition += _networkVelocity * timeDifference;
            }
        }
        
        #endregion
        
        #region IPunObservable Implementation
        
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // 发送本地玩家状态
                stream.SendNext(_networkState.position);
                stream.SendNext(_networkState.rotation);
                stream.SendNext(_networkState.velocity);
                
                if (_syncGravityDirection)
                {
                    stream.SendNext(_networkState.gravityDirection);
                }
                
                stream.SendNext(_networkState.isGrounded);
                stream.SendNext(_networkState.timestamp);
                
                // 发送输入状态（可选）
                stream.SendNext(_networkState.moveInput);
                stream.SendNext(_networkState.lookInput);
                stream.SendNext(_networkState.jumpPressed);
                stream.SendNext(_networkState.firePressed);
            }
            else
            {
                // 接收远程玩家状态
                _targetState.position = (Vector3)stream.ReceiveNext();
                _targetState.rotation = (Quaternion)stream.ReceiveNext();
                _targetState.velocity = (Vector3)stream.ReceiveNext();
                
                if (_syncGravityDirection)
                {
                    _targetState.gravityDirection = (Vector3)stream.ReceiveNext();
                }
                
                _targetState.isGrounded = (bool)stream.ReceiveNext();
                _targetState.timestamp = (float)stream.ReceiveNext();
                
                // 接收输入状态
                _targetState.moveInput = (Vector2)stream.ReceiveNext();
                _targetState.lookInput = (Vector2)stream.ReceiveNext();
                _targetState.jumpPressed = (bool)stream.ReceiveNext();
                _targetState.firePressed = (bool)stream.ReceiveNext();
                
                _lastReceiveTime = (float)PhotonNetwork.Time;
                
                LogNetworkDebug($"接收状态 - 位置: {_targetState.position}, 延迟: {info.SentServerTime}");
            }
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
        
        #endregion
        
        #region Utility Methods
        
        private void LogNetworkDebug(string message)
        {
            if (_showNetworkDebug)
            {
                //Debug.Log($"[NetworkPlayerController] {Owner?.NickName ?? "Unknown"}: {message}");
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
            
            // 绘制预测位置
            if (_enableLagCompensation && PredictedPosition != Vector3.zero)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(PredictedPosition, 0.3f);
                Gizmos.DrawLine(transform.position, PredictedPosition);
            }
            
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
    }
}
