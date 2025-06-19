using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

namespace DWHITE
{
    /// <summary>
    /// 简化的网络管理器 - 仅处理基础的连接和房间管理
    /// 专为学习网络基础设计，移除了所有高级功能
    /// </summary>
    public class SimpleNetworkManager : MonoBehaviourPunCallbacks
    {
        #region Configuration
        
        [Header("基础设置")]
        [SerializeField] private string _gameVersion = "1.0";
        [SerializeField] private byte _maxPlayersPerRoom = 4;
        [SerializeField] private bool _autoConnectOnStart = true;
        
        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = false;
        
        #endregion
        
        #region Private Fields
        
        private bool _isConnecting = false;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Start()
        {
            // 设置Photon基础配置
            PhotonNetwork.GameVersion = _gameVersion;
            PhotonNetwork.SendRate = 30; // 每秒发送30次
            PhotonNetwork.SerializationRate = 20; // 每秒序列化20次
            
            if (_autoConnectOnStart)
            {
                ConnectToPhoton();
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// 连接到Photon服务器
        /// </summary>
        public void ConnectToPhoton()
        {
            if (PhotonNetwork.IsConnected)
            {
                DebugLog("已经连接到Photon服务器");
                return;
            }
            
            _isConnecting = true;
            DebugLog("正在连接到Photon服务器...");
            PhotonNetwork.ConnectUsingSettings();
        }
        
        /// <summary>
        /// 创建或加入房间
        /// </summary>
        public void JoinOrCreateRoom(string roomName = "TestRoom")
        {
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                DebugLog("未连接到服务器，无法加入房间");
                return;
            }
            
            DebugLog($"正在加入或创建房间: {roomName}");
            
            RoomOptions roomOptions = new RoomOptions
            {
                MaxPlayers = _maxPlayersPerRoom,
                IsVisible = true,
                IsOpen = true
            };
            
            PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
        }
        
        /// <summary>
        /// 离开当前房间
        /// </summary>
        public void LeaveRoom()
        {
            if (PhotonNetwork.InRoom)
            {
                DebugLog("正在离开房间...");
                PhotonNetwork.LeaveRoom();
            }
        }
        
        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            DebugLog("正在断开连接...");
            PhotonNetwork.Disconnect();
        }
        
        #endregion
        
        #region Photon Callbacks
        
        public override void OnConnectedToMaster()
        {
            _isConnecting = false;
            DebugLog("已连接到Master服务器");
            
            // 自动加入大厅
            PhotonNetwork.JoinLobby();
        }
        
        public override void OnJoinedLobby()
        {
            DebugLog("已加入大厅");
        }
        
        public override void OnDisconnected(DisconnectCause cause)
        {
            _isConnecting = false;
            DebugLog($"已断开连接，原因: {cause}");
        }
        
        public override void OnJoinedRoom()
        {
            DebugLog($"成功加入房间: {PhotonNetwork.CurrentRoom.Name}");
            DebugLog($"房间内玩家数量: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}");
            
            // 生成玩家
            SpawnPlayer();
        }
        
        public override void OnLeftRoom()
        {
            DebugLog("已离开房间");
        }
        
        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            DebugLog($"加入房间失败: {message} (Code: {returnCode})");
        }
        
        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            DebugLog($"创建房间失败: {message} (Code: {returnCode})");
        }
        
        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            DebugLog($"玩家 {newPlayer.NickName} 加入了房间");
        }
        
        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            DebugLog($"玩家 {otherPlayer.NickName} 离开了房间");
        }
        
        #endregion
        
        #region Player Management
        
        /// <summary>
        /// 生成玩家
        /// </summary>
        private void SpawnPlayer()
        {
            // 寻找玩家预制体
            GameObject playerPrefab = FindPlayerPrefab();
            if (playerPrefab == null)
            {
                DebugLog("未找到玩家预制体，无法生成玩家");
                return;
            }
            
            // 在随机位置生成玩家
            Vector3 spawnPosition = GetRandomSpawnPosition();
            
            DebugLog($"在位置 {spawnPosition} 生成玩家");
            PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, Quaternion.identity);
        }
        
        /// <summary>
        /// 查找玩家预制体
        /// </summary>
        private GameObject FindPlayerPrefab()
        {
            // 尝试从Resources文件夹加载
            GameObject playerPrefab = Resources.Load<GameObject>("PlayerPrefab");
            if (playerPrefab != null)
                return playerPrefab;
            
            // 如果没有找到，尝试其他常见名称
            playerPrefab = Resources.Load<GameObject>("Player");
            if (playerPrefab != null)
                return playerPrefab;
            
            DebugLog("警告: 未在Resources文件夹中找到玩家预制体");
            return null;
        }
        
        /// <summary>
        /// 获取随机生成位置
        /// </summary>
        private Vector3 GetRandomSpawnPosition()
        {
            // 简单的随机位置生成
            float x = Random.Range(-10f, 10f);
            float z = Random.Range(-10f, 10f);
            return new Vector3(x, 1f, z);
        }
        
        #endregion
        
        #region Debug
        
        private void DebugLog(string message)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[SimpleNetworkManager] {message}");
            }
        }
        
        private void OnGUI()
        {
            if (!_showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            
            GUILayout.Label("=== 简化网络管理器 ===");
            GUILayout.Label($"连接状态: {PhotonNetwork.NetworkClientState}");
            GUILayout.Label($"房间: {(PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name : "无")}");
            GUILayout.Label($"玩家数量: {(PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.PlayerCount.ToString() : "0")}");
            
            GUILayout.Space(10);
            
            if (!PhotonNetwork.IsConnected && !_isConnecting)
            {
                if (GUILayout.Button("连接"))
                {
                    ConnectToPhoton();
                }
            }
            
            if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom)
            {
                if (GUILayout.Button("加入/创建房间"))
                {
                    JoinOrCreateRoom();
                }
            }
            
            if (PhotonNetwork.InRoom)
            {
                if (GUILayout.Button("离开房间"))
                {
                    LeaveRoom();
                }
            }
            
            if (PhotonNetwork.IsConnected)
            {
                if (GUILayout.Button("断开连接"))
                {
                    Disconnect();
                }
            }
            
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
