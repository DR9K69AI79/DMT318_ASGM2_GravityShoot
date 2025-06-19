using UnityEngine;
using Photon.Pun;

namespace DWHITE
{
    /// <summary>
    /// 简化的网络玩家控制器 - 仅处理基础的位置和旋转同步
    /// 专为学习网络基础设计，移除了所有高级功能
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class SimpleNetworkPlayerController : MonoBehaviourPun, IPunObservable
    {
        #region Configuration
        
        [Header("基础同步设置")]
        [SerializeField] private float _interpolationSpeed = 10f;
        [SerializeField] private bool _showDebugInfo = false;
        
        #endregion
        
        #region Private Fields
        
        // 组件引用
        private PlayerMotor _playerMotor;
        private PlayerInput _playerInput;
        
        // 网络状态 - 仅保留必需的
        private Vector3 _networkPosition;
        private Quaternion _networkRotation;
        
        #endregion
        
        #region Properties
        
        public bool IsLocalPlayer => photonView.IsMine;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // 获取基本组件引用
            _playerMotor = GetComponent<PlayerMotor>();
            _playerInput = GetComponent<PlayerInput>();
            
            // 初始化网络位置
            _networkPosition = transform.position;
            _networkRotation = transform.rotation;
        }
        
        private void Start()
        {
            // 只有本地玩家启用输入
            if (IsLocalPlayer)
            {
                EnableLocalPlayerComponents();
                Debug.Log("本地玩家初始化完成");
            }
            else
            {
                EnableRemotePlayerComponents();
                Debug.Log("远程玩家初始化完成");
            }
            
            // 设置网络视图观察目标
            if (photonView.ObservedComponents.Count == 0)
            {
                photonView.ObservedComponents.Add(this);
            }
        }
        
        private void Update()
        {
            // 只有远程玩家需要插值到网络位置
            if (!IsLocalPlayer)
            {
                InterpolateToNetworkTransform();
            }
            
            if (_showDebugInfo)
            {
                ShowDebugInfo();
            }
        }
        
        #endregion
        
        #region Component Management
        
        /// <summary>
        /// 启用本地玩家组件
        /// </summary>
        private void EnableLocalPlayerComponents()
        {
            // 启用输入
            if (_playerInput != null) 
                _playerInput.enabled = true;
            
            // 启用物理
            if (_playerMotor != null) 
                _playerMotor.enabled = true;
            
            Debug.Log("本地玩家组件已启用");
        }
        
        /// <summary>
        /// 启用远程玩家组件
        /// </summary>
        private void EnableRemotePlayerComponents()
        {
            // 禁用输入
            if (_playerInput != null) 
                _playerInput.enabled = false;
            
            // 保持物理开启但不接受输入
            if (_playerMotor != null) 
                _playerMotor.enabled = false;
            
            Debug.Log("远程玩家组件已设置");
        }
        
        #endregion
        
        #region Network Interpolation
        
        /// <summary>
        /// 插值到网络位置（仅用于远程玩家）
        /// </summary>
        private void InterpolateToNetworkTransform()
        {
            // 平滑插值到网络位置
            transform.position = Vector3.Lerp(transform.position, _networkPosition, Time.deltaTime * _interpolationSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, _networkRotation, Time.deltaTime * _interpolationSpeed);
        }
        
        #endregion
        
        #region IPunObservable Implementation
        
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // 本地玩家发送数据
                stream.SendNext(transform.position);
                stream.SendNext(transform.rotation);
            }
            else
            {
                // 远程玩家接收数据
                _networkPosition = (Vector3)stream.ReceiveNext();
                _networkRotation = (Quaternion)stream.ReceiveNext();
            }
        }
        
        #endregion
        
        #region Debug
        
        private void ShowDebugInfo()
        {
            if (!_showDebugInfo) return;
            
            // 简单的调试信息显示
            if (IsLocalPlayer)
            {
                Debug.Log($"[本地玩家] 位置: {transform.position}");
            }
            else
            {
                Debug.Log($"[远程玩家] 当前位置: {transform.position}, 网络位置: {_networkPosition}");
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!_showDebugInfo || IsLocalPlayer) return;
            
            // 显示网络位置
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_networkPosition, 0.5f);
            
            // 显示当前位置到网络位置的连线
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _networkPosition);
        }
        
        #endregion
    }
}
