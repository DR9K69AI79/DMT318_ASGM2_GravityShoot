using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace DWHITE
{
    /// <summary>
    /// 网络管理器 - 处理PUN2连接、房间管理和游戏状态同步
    /// 基于项目的物理重力射击游戏需求设计
    /// </summary>
    public class NetworkManager : Singleton<NetworkManager>, IConnectionCallbacks, IMatchmakingCallbacks, IInRoomCallbacks, ILobbyCallbacks
    {
        #region Events & Delegates
        
        public System.Action OnConnectedToMasterEvent;
        public System.Action<DisconnectCause> OnDisconnectedEvent;
        public System.Action OnJoinedRoomEvent;
        public System.Action<short, string> OnJoinRoomFailedEvent;
        public System.Action<Player> OnPlayerEnteredRoomEvent;
        public System.Action<Player> OnPlayerLeftRoomEvent;
        public System.Action<Player> OnMasterClientSwitchedEvent;
        
        #endregion
        
        #region Configuration
        
        [Header("连接设置")]
        [SerializeField] private string _gameVersion = "1.0";
        [SerializeField] private byte _maxPlayersPerRoom = 8;
        [SerializeField] private bool _autoConnectOnStart = true;
        
        [Header("房间设置")]
        [SerializeField] private string _defaultRoomName = "GravityShoot_Room";
        [SerializeField] private bool _isRoomVisible = true;
        [SerializeField] private bool _isRoomOpen = true;
        
        [Header("网络同步")]
        [SerializeField] private int _sendRate = 30;           // 每秒发送频率
        [SerializeField] private int _serializationRate = 20;  // 序列化频率
        
        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = false;
        [SerializeField] private bool _enableAutoReconnect = true;
        
        #endregion
        
        #region Properties
        
        /// <summary>是否已连接到主服务器</summary>
        public bool IsConnectedToMaster => PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby;
        
        /// <summary>是否在房间中</summary>
        public bool IsInRoom => PhotonNetwork.InRoom;
        
        /// <summary>是否是主客户端</summary>
        public bool IsMasterClient => PhotonNetwork.IsMasterClient;
        
        /// <summary>当前房间玩家数量</summary>
        public int PlayerCount => PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;
        
        /// <summary>网络连接状态</summary>
        public ClientState NetworkState => PhotonNetwork.NetworkClientState;
        
        /// <summary>当前ping值</summary>
        public int Ping => PhotonNetwork.GetPing();
        
        /// <summary>本地玩家</summary>
        public Player LocalPlayer => PhotonNetwork.LocalPlayer;
        
        /// <summary>房间中所有玩家</summary>
        public Dictionary<int, Player> Players => PhotonNetwork.CurrentRoom?.Players;
        
        #endregion
        
        #region Private Fields
        
        private bool _isConnecting = false;
        private bool _shouldAutoReconnect = false;
        private Coroutine _reconnectCoroutine;
        private string _lastRoomName;
        
        #endregion
        
        #region Unity Lifecycle
        
        protected override void Awake()
        {
            base.Awake();
            
            // 设置PUN网络配置
            PhotonNetwork.GameVersion = _gameVersion;
            PhotonNetwork.SendRate = _sendRate;
            PhotonNetwork.SerializationRate = _serializationRate;
            
            // 注册回调
            PhotonNetwork.AddCallbackTarget(this);
            
            LogDebug($"NetworkManager 初始化完成 - 游戏版本: {_gameVersion}");
        }
        
        private void Start()
        {
            if (_autoConnectOnStart && !PhotonNetwork.IsConnected)
            {
                ConnectToPhoton();
            }
        }
        
        private void OnDestroy()
        {
            if (_reconnectCoroutine != null)
            {
                StopCoroutine(_reconnectCoroutine);
            }
            
            PhotonNetwork.RemoveCallbackTarget(this);
        }
        
        private void Update()
        {
            // 显示调试信息
            if (_showDebugInfo && Time.frameCount % 60 == 0) // 每秒更新一次
            {
                LogDebug($"网络状态: {NetworkState} | Ping: {Ping}ms | 房间玩家: {PlayerCount}");
            }
        }
        
        #endregion
        
        #region Public Connection Methods
        
        /// <summary>
        /// 连接到Photon网络
        /// </summary>
        public void ConnectToPhoton()
        {
            if (_isConnecting)
            {
                LogDebug("已在连接中，忽略重复连接请求");
                return;
            }
            
            if (PhotonNetwork.IsConnected)
            {
                LogDebug("已连接到Photon网络");
                return;
            }
            
            _isConnecting = true;
            LogDebug("开始连接到Photon网络...");
            
            PhotonNetwork.ConnectUsingSettings();
        }
        
        /// <summary>
        /// 断开网络连接
        /// </summary>
        public void DisconnectFromPhoton()
        {
            _shouldAutoReconnect = false;
            
            if (_reconnectCoroutine != null)
            {
                StopCoroutine(_reconnectCoroutine);
                _reconnectCoroutine = null;
            }
            
            if (PhotonNetwork.IsConnected)
            {
                LogDebug("断开Photon网络连接");
                PhotonNetwork.Disconnect();
            }
        }
        
        #endregion
        
        #region Room Management
        
        /// <summary>
        /// 创建房间
        /// </summary>
        /// <param name="roomName">房间名称，为空则自动生成</param>
        /// <param name="maxPlayers">最大玩家数</param>
        /// <param name="isVisible">是否在房间列表中可见</param>
        /// <param name="isOpen">是否允许新玩家加入</param>
        public void CreateRoom(string roomName = null, byte maxPlayers = 0, bool isVisible = true, bool isOpen = true)
        {
            if (!IsConnectedToMaster)
            {
                LogDebug("未连接到主服务器，无法创建房间");
                return;
            }
            
            if (string.IsNullOrEmpty(roomName))
            {
                roomName = $"{_defaultRoomName}_{UnityEngine.Random.Range(1000, 9999)}";
            }
            
            if (maxPlayers == 0)
            {
                maxPlayers = _maxPlayersPerRoom;
            }
            
            RoomOptions roomOptions = new RoomOptions()
            {
                MaxPlayers = maxPlayers,
                IsVisible = isVisible,
                IsOpen = isOpen,
                CleanupCacheOnLeave = true
            };
            
            // 设置房间自定义属性
            roomOptions.CustomRoomProperties = new Hashtable()
            {
                { "gameType", "GravityShoot" },
                { "version", _gameVersion },
                { "created", DateTime.Now.Ticks }
            };
            
            _lastRoomName = roomName;
            LogDebug($"创建房间: {roomName} (最大玩家: {maxPlayers})");
            
            PhotonNetwork.CreateRoom(roomName, roomOptions);
        }
        
        /// <summary>
        /// 加入指定房间
        /// </summary>
        /// <param name="roomName">房间名称</param>
        public void JoinRoom(string roomName)
        {
            if (!IsConnectedToMaster)
            {
                LogDebug("未连接到主服务器，无法加入房间");
                return;
            }
            
            if (string.IsNullOrEmpty(roomName))
            {
                LogDebug("房间名称不能为空");
                return;
            }
            
            _lastRoomName = roomName;
            LogDebug($"尝试加入房间: {roomName}");
            
            PhotonNetwork.JoinRoom(roomName);
        }
        
        /// <summary>
        /// 加入随机房间
        /// </summary>
        public void JoinRandomRoom()
        {
            if (!IsConnectedToMaster)
            {
                LogDebug("未连接到主服务器，无法加入随机房间");
                return;
            }
            
            LogDebug("尝试加入随机房间...");
            
            // 设置匹配条件
            Hashtable expectedProperties = new Hashtable()
            {
                { "gameType", "GravityShoot" },
                { "version", _gameVersion }
            };
            
            PhotonNetwork.JoinRandomRoom(expectedProperties, _maxPlayersPerRoom);
        }
        
        /// <summary>
        /// 离开当前房间
        /// </summary>
        public void LeaveRoom()
        {
            if (IsInRoom)
            {
                LogDebug("离开当前房间");
                PhotonNetwork.LeaveRoom();
            }
        }
        
        /// <summary>
        /// 创建或加入房间（如果房间不存在则创建）
        /// </summary>
        /// <param name="roomName">房间名称</param>
        public void JoinOrCreateRoom(string roomName = null)
        {
            if (string.IsNullOrEmpty(roomName))
            {
                roomName = _defaultRoomName;
            }
            
            RoomOptions roomOptions = new RoomOptions()
            {
                MaxPlayers = _maxPlayersPerRoom,
                IsVisible = _isRoomVisible,
                IsOpen = _isRoomOpen
            };
            
            roomOptions.CustomRoomProperties = new Hashtable()
            {
                { "gameType", "GravityShoot" },
                { "version", _gameVersion }
            };
            
            _lastRoomName = roomName;
            LogDebug($"尝试加入或创建房间: {roomName}");
            
            PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
        }
        
        #endregion
        
        #region Player Management
        
        /// <summary>
        /// 设置本地玩家昵称
        /// </summary>
        /// <param name="nickname">昵称</param>
        public void SetPlayerNickname(string nickname)
        {
            if (string.IsNullOrEmpty(nickname))
            {
                nickname = $"Player_{UnityEngine.Random.Range(1000, 9999)}";
            }
            
            PhotonNetwork.NickName = nickname;
            LogDebug($"设置玩家昵称: {nickname}");
        }
        
        /// <summary>
        /// 设置玩家自定义属性
        /// </summary>
        /// <param name="properties">自定义属性</param>
        public void SetPlayerProperties(Hashtable properties)
        {
            if (properties != null)
            {
                PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
                LogDebug("更新玩家自定义属性");
            }
        }
        
        /// <summary>
        /// 获取玩家自定义属性
        /// </summary>
        /// <param name="player">玩家</param>
        /// <param name="key">属性键</param>
        /// <returns>属性值</returns>
        public object GetPlayerProperty(Player player, string key)
        {
            return player?.CustomProperties.TryGetValue(key, out object value) == true ? value : null;
        }
        
        #endregion
        
        #region Network Events & Utilities
        
        /// <summary>
        /// 尝试自动重连
        /// </summary>
        private void TryAutoReconnect()
        {
            if (_enableAutoReconnect && _shouldAutoReconnect && _reconnectCoroutine == null)
            {
                _reconnectCoroutine = StartCoroutine(AutoReconnectCoroutine());
            }
        }
        
        /// <summary>
        /// 自动重连协程
        /// </summary>
        private IEnumerator AutoReconnectCoroutine()
        {
            LogDebug("开始自动重连...");
            
            int attemptCount = 0;
            int maxAttempts = 5;
            float delay = 2f;
            
            while (attemptCount < maxAttempts && _shouldAutoReconnect)
            {
                attemptCount++;
                LogDebug($"重连尝试 {attemptCount}/{maxAttempts}");
                
                yield return new WaitForSeconds(delay);
                
                if (!PhotonNetwork.IsConnected)
                {
                    ConnectToPhoton();
                    
                    // 等待连接结果
                    float timeout = 10f;
                    float elapsed = 0f;
                    
                    while (elapsed < timeout && !PhotonNetwork.IsConnected)
                    {
                        elapsed += Time.deltaTime;
                        yield return null;
                    }
                    
                    if (PhotonNetwork.IsConnected)
                    {
                        LogDebug("自动重连成功！");
                        
                        // 如果之前在房间中，尝试重新加入
                        if (!string.IsNullOrEmpty(_lastRoomName))
                        {
                            JoinRoom(_lastRoomName);
                        }
                        
                        break;
                    }
                }
                else
                {
                    LogDebug("网络已恢复连接");
                    break;
                }
                
                delay = Mathf.Min(delay * 1.5f, 30f); // 指数退避
            }
            
            if (attemptCount >= maxAttempts)
            {
                LogDebug("自动重连失败，已达到最大尝试次数");
            }
            
            _reconnectCoroutine = null;
        }
        
        /// <summary>
        /// 调试日志输出
        /// </summary>
        private void LogDebug(string message)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[NetworkManager] {message}");
            }
        }
        
        #endregion
        
        #region Photon PUN2 Callbacks
        
        // IConnectionCallbacks 接口实现
        public void OnConnected()
        {
            LogDebug("已连接到Photon服务器");
        }
        
        public void OnConnectedToMaster()
        {
            _isConnecting = false;
            _shouldAutoReconnect = true;
            
            LogDebug("已连接到主服务器");
            OnConnectedToMasterEvent?.Invoke();
            
            // 自动加入大厅
            if (!PhotonNetwork.InLobby)
            {
                PhotonNetwork.JoinLobby();
            }
        }
        
        public void OnDisconnected(DisconnectCause cause)
        {
            _isConnecting = false;
            
            LogDebug($"已断开连接: {cause}");
            OnDisconnectedEvent?.Invoke(cause);
              // 根据断开原因决定是否自动重连
            switch (cause)
            {
                case DisconnectCause.DisconnectByClientLogic:
                    _shouldAutoReconnect = false;
                    break;
                case DisconnectCause.DisconnectByServerLogic:
                case DisconnectCause.DisconnectByServerReasonUnknown:
                    _shouldAutoReconnect = false;
                    break;
                default:
                    TryAutoReconnect();
                    break;
            }
        }
        
        public void OnRegionListReceived(RegionHandler regionHandler)
        {
            LogDebug($"接收到地区列表，可用地区数: {regionHandler.EnabledRegions.Count}");
        }
        
        public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
            LogDebug("收到自定义认证响应");
        }
        
        public void OnCustomAuthenticationFailed(string debugMessage)
        {
            LogDebug($"自定义认证失败: {debugMessage}");
        }
        
        // IMatchmakingCallbacks 接口实现
        public void OnFriendListUpdate(List<FriendInfo> friendList)
        {
            LogDebug($"好友列表更新，好友数量: {friendList.Count}");
        }
        
        public void OnCreatedRoom()
        {
            LogDebug($"房间创建成功: {PhotonNetwork.CurrentRoom.Name}");
        }
        
        public void OnCreateRoomFailed(short returnCode, string message)
        {
            LogDebug($"房间创建失败: {message} (代码: {returnCode})");
            
            // 尝试加入随机房间作为备选
            JoinRandomRoom();
        }
        
        public void OnJoinedRoom()
        {
            LogDebug($"成功加入房间: {PhotonNetwork.CurrentRoom.Name} | 玩家数: {PhotonNetwork.CurrentRoom.PlayerCount}");
            OnJoinedRoomEvent?.Invoke();
        }
        
        public void OnJoinRoomFailed(short returnCode, string message)
        {
            LogDebug($"加入房间失败: {message} (代码: {returnCode})");
            OnJoinRoomFailedEvent?.Invoke(returnCode, message);
            
            // 如果加入失败，尝试创建房间
            CreateRoom(_lastRoomName);
        }
        
        public void OnJoinRandomFailed(short returnCode, string message)
        {
            LogDebug($"加入随机房间失败: {message} (代码: {returnCode})");
            
            // 如果没有可用房间，创建一个新房间
            CreateRoom();
        }
        
        public void OnLeftRoom()
        {
            LogDebug("已离开房间");
        }
        
        // IInRoomCallbacks 接口实现
        public void OnPlayerEnteredRoom(Player newPlayer)
        {
            LogDebug($"玩家 {newPlayer.NickName} 加入房间 | 当前玩家数: {PhotonNetwork.CurrentRoom.PlayerCount}");
            OnPlayerEnteredRoomEvent?.Invoke(newPlayer);
        }
        
        public void OnPlayerLeftRoom(Player otherPlayer)
        {
            LogDebug($"玩家 {otherPlayer.NickName} 离开房间 | 当前玩家数: {PhotonNetwork.CurrentRoom.PlayerCount}");
            OnPlayerLeftRoomEvent?.Invoke(otherPlayer);
        }
        
        public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            LogDebug($"房间属性更新: {propertiesThatChanged.Count} 个属性");
        }
        
        public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            LogDebug($"玩家 {targetPlayer.NickName} 属性更新: {changedProps.Count} 个属性");
        }
        
        public void OnMasterClientSwitched(Player newMasterClient)
        {
            LogDebug($"主客户端切换到: {newMasterClient.NickName}");
            OnMasterClientSwitchedEvent?.Invoke(newMasterClient);
        }
        
        // ILobbyCallbacks 接口实现
        public void OnJoinedLobby()
        {
            LogDebug("已加入大厅");
        }
        
        public void OnLeftLobby()
        {
            LogDebug("已离开大厅");
        }
        
        public void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            LogDebug($"房间列表更新: {roomList.Count} 个房间");
        }
        
        public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics)
        {
            LogDebug($"大厅统计更新: {lobbyStatistics.Count} 个大厅");
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// 获取网络统计信息
        /// </summary>
        /// <returns>格式化的统计信息字符串</returns>
        public string GetNetworkStats()
        {
            if (!PhotonNetwork.IsConnected)
                return "未连接";
                
            return $"Ping: {Ping}ms | 发送速率: {PhotonNetwork.SendRate}Hz | " +
                   $"序列化速率: {PhotonNetwork.SerializationRate}Hz | " +
                   $"房间: {PhotonNetwork.CurrentRoom?.Name ?? "无"} | " +
                   $"玩家: {PlayerCount}";
        }
        
        /// <summary>
        /// 检查网络连接质量
        /// </summary>
        /// <returns>连接质量评级</returns>
        public string GetConnectionQuality()
        {
            if (!PhotonNetwork.IsConnected)
                return "未连接";
                
            int ping = Ping;
            if (ping < 50) return "优秀";
            if (ping < 100) return "良好";
            if (ping < 200) return "一般";
            return "较差";
        }
        
        /// <summary>
        /// 强制同步网络时间
        /// </summary>
        public void SyncNetworkTime()
        {
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.FetchServerTimestamp();
                LogDebug("请求同步网络时间");
            }
        }
        
        /// <summary>
        /// 设置网络发送频率
        /// </summary>
        /// <param name="sendRate">发送频率 (Hz)</param>
        /// <param name="serializationRate">序列化频率 (Hz)</param>
        public void SetNetworkRates(int sendRate, int serializationRate)
        {
            _sendRate = Mathf.Clamp(sendRate, 10, 60);
            _serializationRate = Mathf.Clamp(serializationRate, 10, 30);
            
            PhotonNetwork.SendRate = _sendRate;
            PhotonNetwork.SerializationRate = _serializationRate;
            
            LogDebug($"网络频率已更新 - 发送: {_sendRate}Hz, 序列化: {_serializationRate}Hz");
        }
        
        #endregion
    }
}