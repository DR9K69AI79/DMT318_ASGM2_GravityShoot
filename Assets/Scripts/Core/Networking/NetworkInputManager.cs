using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

namespace DWHITE
{
    /// <summary>
    /// 网络输入管理器 - 处理输入的网络同步和缓冲
    /// 专为重力射击游戏设计，支持输入预测和平滑
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class NetworkInputManager : MonoBehaviourPun, IPunObservable
    {
        #region Input Data Structure
        
        [System.Serializable]
        public struct NetworkInputData
        {
            public Vector2 moveInput;
            public Vector2 lookInput;
            public bool jumpPressed;
            public bool jumpHeld;
            public bool firePressed;
            public bool fireHeld;
            public bool sprintPressed;
            public bool sprintHeld;
            public float timestamp;
            public int frameNumber;
            
            public bool IsValid => timestamp > 0;
        }
        
        #endregion
        
        #region Configuration
        
        [Header("输入同步设置")]
        [SerializeField] private float _inputSyncRate = 30f;
        [SerializeField] private bool _enableInputPrediction = true;
        [SerializeField] private bool _enableInputSmoothing = true;
        
        [Header("输入缓冲")]
        [SerializeField] private int _maxInputBuffer = 32;
        [SerializeField] private float _inputBufferTime = 0.5f;
        [SerializeField] private bool _interpolateInputs = true;
        
        [Header("延迟补偿")]
        [SerializeField] private bool _enableLagCompensation = true;
        [SerializeField] private float _maxCompensationTime = 0.2f;
        
        [Header("调试")]
        [SerializeField] private bool _showInputDebug = false;
        [SerializeField] private bool _logInputEvents = false;
        
        #endregion
        
        #region Private Fields
        
        // 组件引用
        private PlayerInput _playerInput;
        private NetworkPlayerController _networkController;
        
        // 输入缓冲
        private Queue<NetworkInputData> _inputBuffer;
        private Queue<NetworkInputData> _predictionBuffer;
        
        // 当前输入状态
        private NetworkInputData _currentInput;
        private NetworkInputData _previousInput;
        private NetworkInputData _targetInput;
        
        // 网络同步
        private float _lastSendTime;
        private float _lastReceiveTime;
        private float _sendInterval;
        
        // 输入平滑
        private Vector2 _smoothedMoveInput;
        private Vector2 _smoothedLookInput;
        private float _inputSmoothTime = 0.1f;
        private Vector2 _moveVelocity;
        private Vector2 _lookVelocity;
        
        // 输入事件
        private bool _localJumpPressed;
        private bool _localFirePressed;
        private bool _localSprintPressed;
        
        #endregion
        
        #region Properties
        
        public bool IsLocalPlayer => photonView.IsMine;
        public NetworkInputData CurrentInput => _currentInput;
        public Vector2 SmoothedMoveInput => _smoothedMoveInput;
        public Vector2 SmoothedLookInput => _smoothedLookInput;
        public float InputLatency => (float)(PhotonNetwork.Time - _lastReceiveTime);
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            _networkController = GetComponent<NetworkPlayerController>();
            
            _inputBuffer = new Queue<NetworkInputData>();
            _predictionBuffer = new Queue<NetworkInputData>();
            
            _sendInterval = 1f / _inputSyncRate;
        }
        
        private void Start()
        {
            // 只有本地玩家处理输入
            if (!IsLocalPlayer && _playerInput != null)
            {
                _playerInput.enabled = false;
            }
            
            LogInputDebug($"网络输入管理器初始化 - 本地玩家: {IsLocalPlayer}");
        }
        
        private void Update()
        {
            if (IsLocalPlayer)
            {
                CaptureLocalInput();
                ProcessLocalInput();
            }
            else
            {
                ProcessRemoteInput();
            }
        }
        
        #endregion
        
        #region Local Input Handling
        
        /// <summary>
        /// 捕获本地输入
        /// </summary>
        private void CaptureLocalInput()
        {
            if (_playerInput == null) return;
            
            // 检测按下事件
            _localJumpPressed = _playerInput.JumpPressed || _localJumpPressed;
            _localFirePressed = _playerInput.FirePressed || _localFirePressed;
            _localSprintPressed = _playerInput.SprintPressed || _localSprintPressed;
            
            // 创建输入数据
            NetworkInputData inputData = new NetworkInputData
            {
                moveInput = _playerInput.MoveInput,
                lookInput = _playerInput.LookInput,
                jumpPressed = _localJumpPressed,
                jumpHeld = _playerInput.JumpHeld,
                firePressed = _localFirePressed,
                fireHeld = _playerInput.FireHeld,
                sprintPressed = _localSprintPressed,
                sprintHeld = _playerInput.SprintHeld,
                timestamp = (float)PhotonNetwork.Time,
                frameNumber = Time.frameCount
            };
            
            _currentInput = inputData;
            
            // 记录到预测缓冲
            if (_enableInputPrediction)
            {
                RecordInputForPrediction(inputData);
            }
        }
        
        /// <summary>
        /// 处理本地输入
        /// </summary>
        private void ProcessLocalInput()
        {
            // 应用输入平滑
            if (_enableInputSmoothing)
            {
                ApplyInputSmoothing();
            }
            else
            {
                _smoothedMoveInput = _currentInput.moveInput;
                _smoothedLookInput = _currentInput.lookInput;
            }
            
            // 定期发送输入到网络
            if (Time.time - _lastSendTime >= _sendInterval)
            {
                SendInputToNetwork();
                _lastSendTime = Time.time;
            }
            
            // 重置按下状态
            ResetPressedInputs();
        }
        
        /// <summary>
        /// 应用输入平滑
        /// </summary>
        private void ApplyInputSmoothing()
        {
            _smoothedMoveInput = Vector2.SmoothDamp(
                _smoothedMoveInput, 
                _currentInput.moveInput, 
                ref _moveVelocity, 
                _inputSmoothTime
            );
            
            _smoothedLookInput = Vector2.SmoothDamp(
                _smoothedLookInput, 
                _currentInput.lookInput, 
                ref _lookVelocity, 
                _inputSmoothTime * 0.5f // 视角输入需要更快的响应
            );
        }
        
        /// <summary>
        /// 发送输入到网络
        /// </summary>
        private void SendInputToNetwork()
        {
            // 将输入添加到缓冲区
            _inputBuffer.Enqueue(_currentInput);
            
            // 限制缓冲区大小
            while (_inputBuffer.Count > _maxInputBuffer)
            {
                _inputBuffer.Dequeue();
            }
            
            if (_logInputEvents)
            {
                LogInputDebug($"发送输入 - 移动: {_currentInput.moveInput}, 视角: {_currentInput.lookInput}");
            }
        }
        
        /// <summary>
        /// 记录输入用于预测
        /// </summary>
        private void RecordInputForPrediction(NetworkInputData input)
        {
            _predictionBuffer.Enqueue(input);
            
            // 清理过期的预测数据
            float cutoffTime = (float)PhotonNetwork.Time - _inputBufferTime;
            while (_predictionBuffer.Count > 0 && _predictionBuffer.Peek().timestamp < cutoffTime)
            {
                _predictionBuffer.Dequeue();
            }
        }
        
        /// <summary>
        /// 重置按下状态
        /// </summary>
        private void ResetPressedInputs()
        {
            _localJumpPressed = false;
            _localFirePressed = false;
            _localSprintPressed = false;
        }
        
        #endregion
        
        #region Remote Input Handling
        
        /// <summary>
        /// 处理远程输入
        /// </summary>
        private void ProcessRemoteInput()
        {
            // 应用延迟补偿
            if (_enableLagCompensation)
            {
                ApplyLagCompensation();
            }
            
            // 输入插值
            if (_interpolateInputs)
            {
                InterpolateRemoteInput();
            }
            else
            {
                _smoothedMoveInput = _targetInput.moveInput;
                _smoothedLookInput = _targetInput.lookInput;
            }
        }
        
        /// <summary>
        /// 应用延迟补偿
        /// </summary>
        private void ApplyLagCompensation()
        {
            float lagTime = Mathf.Min(InputLatency, _maxCompensationTime);
            
            if (lagTime > 0)
            {
                // 这里可以实现更复杂的延迟补偿算法
                // 目前简单地使用目标输入
                _currentInput = _targetInput;
            }
        }
        
        /// <summary>
        /// 插值远程输入
        /// </summary>
        private void InterpolateRemoteInput()
        {
            float lerpSpeed = Time.deltaTime * (1f / _inputSmoothTime);
            
            _smoothedMoveInput = Vector2.Lerp(_smoothedMoveInput, _targetInput.moveInput, lerpSpeed);
            _smoothedLookInput = Vector2.Lerp(_smoothedLookInput, _targetInput.lookInput, lerpSpeed);
        }
        
        #endregion
        
        #region IPunObservable Implementation
        
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // 发送本地输入状态
                stream.SendNext(_currentInput.moveInput);
                stream.SendNext(_currentInput.lookInput);
                stream.SendNext(_currentInput.jumpPressed);
                stream.SendNext(_currentInput.jumpHeld);
                stream.SendNext(_currentInput.firePressed);
                stream.SendNext(_currentInput.fireHeld);
                stream.SendNext(_currentInput.sprintPressed);
                stream.SendNext(_currentInput.sprintHeld);
                stream.SendNext(_currentInput.timestamp);
            }
            else
            {
                // 接收远程输入状态
                NetworkInputData receivedInput = new NetworkInputData
                {
                    moveInput = (Vector2)stream.ReceiveNext(),
                    lookInput = (Vector2)stream.ReceiveNext(),
                    jumpPressed = (bool)stream.ReceiveNext(),
                    jumpHeld = (bool)stream.ReceiveNext(),
                    firePressed = (bool)stream.ReceiveNext(),
                    fireHeld = (bool)stream.ReceiveNext(),
                    sprintPressed = (bool)stream.ReceiveNext(),
                    sprintHeld = (bool)stream.ReceiveNext(),
                    timestamp = (float)stream.ReceiveNext(),
                    frameNumber = Time.frameCount
                };
                
                _targetInput = receivedInput;
                _lastReceiveTime = (float)PhotonNetwork.Time;
                
                if (_logInputEvents)
                {
                    LogInputDebug($"接收输入 - 移动: {receivedInput.moveInput}, 延迟: {info.SentServerTime}");
                }
            }
        }
        
        #endregion
        
        #region Input Prediction
        
        /// <summary>
        /// 输入预测校正
        /// </summary>
        /// <param name="serverInput">服务器确认的输入</param>
        /// <param name="serverTime">服务器时间戳</param>
        public void InputReconciliation(NetworkInputData serverInput, float serverTime)
        {
            if (!IsLocalPlayer || !_enableInputPrediction) return;
            
            // 查找对应时间戳的预测输入
            NetworkInputData? predictedInput = FindPredictedInput(serverTime);
            
            if (predictedInput.HasValue)
            {
                // 检查输入差异
                bool inputMismatch = !InputsMatch(predictedInput.Value, serverInput);
                
                if (inputMismatch)
                {
                    LogInputDebug($"输入预测校正 - 时间: {serverTime}");
                    
                    // 可以在这里实现输入回放逻辑
                    CorrectInputPrediction(serverInput, serverTime);
                }
            }
        }
        
        /// <summary>
        /// 查找预测输入
        /// </summary>
        private NetworkInputData? FindPredictedInput(float timestamp)
        {
            foreach (var input in _predictionBuffer)
            {
                if (Mathf.Abs(input.timestamp - timestamp) < 0.05f)
                {
                    return input;
                }
            }
            return null;
        }
        
        /// <summary>
        /// 检查输入是否匹配
        /// </summary>
        private bool InputsMatch(NetworkInputData input1, NetworkInputData input2)
        {
            const float epsilon = 0.01f;
            
            return Vector2.Distance(input1.moveInput, input2.moveInput) < epsilon &&
                   Vector2.Distance(input1.lookInput, input2.lookInput) < epsilon &&
                   input1.jumpPressed == input2.jumpPressed &&
                   input1.firePressed == input2.firePressed;
        }
        
        /// <summary>
        /// 校正输入预测
        /// </summary>
        private void CorrectInputPrediction(NetworkInputData serverInput, float serverTime)
        {
            // 实现输入校正逻辑
            // 可以通知相关组件进行状态校正
            if (_networkController != null)
            {
                // 触发位置校正
                _networkController.ForceSync();
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// 获取指定类型的输入值
        /// </summary>
        public bool GetInputPressed(string inputName)
        {
            switch (inputName.ToLower())
            {
                case "jump": return _currentInput.jumpPressed;
                case "fire": return _currentInput.firePressed;
                case "sprint": return _currentInput.sprintPressed;
                default: return false;
            }
        }
        
        /// <summary>
        /// 获取指定类型的输入持续状态
        /// </summary>
        public bool GetInputHeld(string inputName)
        {
            switch (inputName.ToLower())
            {
                case "jump": return _currentInput.jumpHeld;
                case "fire": return _currentInput.fireHeld;
                case "sprint": return _currentInput.sprintHeld;
                default: return false;
            }
        }
        
        /// <summary>
        /// 设置输入同步参数
        /// </summary>
        public void SetInputParams(float syncRate, bool enableSmoothing, float smoothTime)
        {
            _inputSyncRate = Mathf.Clamp(syncRate, 10f, 60f);
            _enableInputSmoothing = enableSmoothing;
            _inputSmoothTime = Mathf.Clamp(smoothTime, 0.05f, 0.5f);
            
            _sendInterval = 1f / _inputSyncRate;
            
            LogInputDebug($"输入参数更新 - 同步频率: {_inputSyncRate}Hz, 平滑: {_enableInputSmoothing}");
        }
        
        /// <summary>
        /// 强制发送输入
        /// </summary>
        public void ForceSendInput()
        {
            if (IsLocalPlayer)
            {
                _lastSendTime = 0f; // 强制下一帧发送
            }
        }
        
        /// <summary>
        /// 获取输入统计信息
        /// </summary>
        public string GetInputStats()
        {
            return $"输入延迟: {InputLatency * 1000:F1}ms | " +
                   $"缓冲大小: {_inputBuffer.Count} | " +
                   $"预测缓冲: {_predictionBuffer.Count} | " +
                   $"同步频率: {_inputSyncRate}Hz";
        }
        
        #endregion
        
        #region Utility Methods
        
        private void LogInputDebug(string message)
        {
            if (_showInputDebug)
            {
                //Debug.Log($"[NetworkInputManager] {photonView.owner?.NickName ?? "Unknown"}: {message}");
            }
        }
        
        #endregion
    }
}
