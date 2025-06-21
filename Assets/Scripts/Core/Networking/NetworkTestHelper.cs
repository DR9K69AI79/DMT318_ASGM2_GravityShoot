using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace DWHITE
{
    /// <summary>
    /// 网络测试助手 - 用于快速测试网络同步功能
    /// 包含自动初始化、多客户端测试支持和调试工具
    /// </summary>
    public class NetworkTestHelper : MonoBehaviourPun, IConnectionCallbacks, IMatchmakingCallbacks, IInRoomCallbacks
    {        [Header("测试配置")]
        [SerializeField] private bool _autoStartOnAwake = true;
        [SerializeField] private string _testRoomName = "TestRoom";
        [SerializeField] private byte _maxPlayers = 4;
        [SerializeField] private bool _useRandomRoomName = false; // 改为false，确保多人加入同一房间
        
        [Header("玩家生成")]
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private bool _autoSpawnPlayer = true;
        
        [Header("调试选项")]
        [SerializeField] private bool _showConnectionStatus = true;
        [SerializeField] private bool _showRoomInfo = true;
        [SerializeField] private bool _enableVerboseLogging = true;        [SerializeField] private KeyCode _quickJoinKey = KeyCode.F1;
        [SerializeField] private KeyCode _quickLeaveKey = KeyCode.F2;
        [SerializeField] private KeyCode _spawnPlayerKey = KeyCode.F3;
        [SerializeField] private KeyCode _fixJumpKey = KeyCode.F4; // 新增：修复跳跃功能快捷键
        
        [Header("网络测试")]
        [SerializeField] private bool _testSyncObject = true;
        [SerializeField] private GameObject _testObjectPrefab;
        [SerializeField] private float _testObjectSpawnInterval = 5f;
        
        private bool _isConnecting = false;
        private bool _hasSpawnedPlayer = false;
        private Coroutine _testSpawnCoroutine;
        private List<GameObject> _spawnedPlayers = new List<GameObject>();
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // 确保对象不会被销毁
            DontDestroyOnLoad(gameObject);
            if (_autoStartOnAwake)
            {
                StartNetworkTest();
            }
        }
        
        private void Start()
        {
            // 设置PUN日志级别
            if (_enableVerboseLogging)
            {
                PhotonNetwork.LogLevel = PunLogLevel.Full;
            }
            
            // 注册回调
            PhotonNetwork.AddCallbackTarget(this);
        }
        
        private void OnDestroy()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            
            if (_testSpawnCoroutine != null)
            {
                StopCoroutine(_testSpawnCoroutine);
            }
        }
        
        private void Update()
        {
            HandleInput();
        }
        
        private void OnGUI()
        {
            if (!_showConnectionStatus && !_showRoomInfo) return;
            
            GUI.Box(new Rect(10, 10, 300, 150), "网络测试状态");
            
            int yOffset = 35;
            int lineHeight = 20;
            
            if (_showConnectionStatus)
            {
                GUI.Label(new Rect(20, yOffset, 280, lineHeight), $"网络状态: {PhotonNetwork.NetworkClientState}");
                yOffset += lineHeight;
                
                GUI.Label(new Rect(20, yOffset, 280, lineHeight), $"已连接: {PhotonNetwork.IsConnected}");
                yOffset += lineHeight;
                
                GUI.Label(new Rect(20, yOffset, 280, lineHeight), $"在房间中: {PhotonNetwork.InRoom}");
                yOffset += lineHeight;
                
                if (PhotonNetwork.IsConnected)
                {
                    GUI.Label(new Rect(20, yOffset, 280, lineHeight), $"Ping: {PhotonNetwork.GetPing()}ms");
                    yOffset += lineHeight;
                }
            }
            
            if (_showRoomInfo && PhotonNetwork.InRoom)
            {
                GUI.Label(new Rect(20, yOffset, 280, lineHeight), $"房间: {PhotonNetwork.CurrentRoom.Name}");
                yOffset += lineHeight;
                
                GUI.Label(new Rect(20, yOffset, 280, lineHeight), $"玩家: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}");
            }
            
            // 操作按钮
            yOffset = 170;
            if (GUI.Button(new Rect(10, yOffset, 100, 30), "快速加入"))
            {
                QuickJoin();
            }
            
            if (GUI.Button(new Rect(120, yOffset, 100, 30), "离开房间"))
            {
                QuickLeave();
            }
              if (GUI.Button(new Rect(230, yOffset, 100, 30), "生成玩家"))
            {
                SpawnTestPlayer();
            }
            
            // 新增：修复跳跃按钮
            if (GUI.Button(new Rect(340, yOffset, 100, 30), "修复跳跃"))
            {
                FixJumpFunctionality();
            }
            
            yOffset += 40;
            if (GUI.Button(new Rect(10, yOffset, 150, 30), "测试网络对象"))
            {
                SpawnTestNetworkObject();
            }
            
            if (GUI.Button(new Rect(170, yOffset, 150, 30), "清理测试对象"))
            {
                CleanupTestObjects();
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// 开始网络测试
        /// </summary>
        public void StartNetworkTest()
        {
            Log("开始网络测试...");
            
            if (!PhotonNetwork.IsConnected)
            {
                ConnectToPhoton();
            }
            else if (!PhotonNetwork.InRoom)
            {
                JoinOrCreateTestRoom();
            }
        }
        
        /// <summary>
        /// 连接到Photon网络
        /// </summary>
        public void ConnectToPhoton()
        {
            if (_isConnecting)
            {
                Log("已在连接中...");
                return;
            }
            
            _isConnecting = true;
            Log("连接到Photon网络...");
            
            PhotonNetwork.GameVersion = "Test_1.0";
            PhotonNetwork.ConnectUsingSettings();
        }
        
        /// <summary>
        /// 快速加入房间
        /// </summary>
        public void QuickJoin()
        {
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                StartNetworkTest();
                return;
            }
            
            if (PhotonNetwork.InRoom)
            {
                Log("已在房间中");
                return;
            }
            
            JoinOrCreateTestRoom();
        }
        
        /// <summary>
        /// 快速离开房间
        /// </summary>
        public void QuickLeave()
        {
            if (PhotonNetwork.InRoom)
            {
                Log("离开房间");
                PhotonNetwork.LeaveRoom();
                _hasSpawnedPlayer = false;
                CleanupSpawnedPlayers();
            }
        }        /// <summary>
        /// 生成测试玩家
        /// </summary>
        public void SpawnTestPlayer()
        {
            if (!PhotonNetwork.InRoom)
            {
                Log("❌ 需要先加入房间才能生成玩家");
                return;
            }
            
            if (_hasSpawnedPlayer)
            {
                Log("⚠️ 已生成玩家");
                return;
            }
            
            if (_playerPrefab == null)
            {
                Log("❌ 错误: 未设置玩家预制体！请在Inspector中设置 _playerPrefab");
                return;
            }

            // 验证玩家预制体是否有PhotonView
            PhotonView prefabPV = _playerPrefab.GetComponent<PhotonView>();
            if (prefabPV == null)
            {
                Log("❌ 错误: 玩家预制体缺少PhotonView组件！");
                return;
            }
            
            try
            {
                Vector3 spawnPosition = GetSpawnPosition();
                Log($"🎯 准备在位置 {spawnPosition} 生成玩家");
                
                // 验证生成位置是否有效
                if (!IsValidSpawnPosition(spawnPosition))
                {
                    Log("⚠️ 警告: 生成位置可能无效，使用安全位置");
                    spawnPosition = GetSafeSpawnPosition();
                }                GameObject player = PhotonNetwork.Instantiate(_playerPrefab.name, spawnPosition, Quaternion.identity);
                
                if (player != null)
                {
                    _spawnedPlayers.Add(player);
                    _hasSpawnedPlayer = true;
                    Log($"✅ 成功生成玩家: {PhotonNetwork.LocalPlayer.NickName} 位置: {spawnPosition}");
                    
                    // 启动玩家初始化修复协程
                    StartCoroutine(FixPlayerInitialization(player));
                }
                else
                {
                    Log("❌ 错误: 玩家生成失败，返回null");
                }
            }
            catch (System.Exception ex)
            {
                Log($"❌ 生成玩家时发生错误: {ex.Message}");
                Log($"📝 堆栈跟踪: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 生成测试网络对象
        /// </summary>
        public void SpawnTestNetworkObject()
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            {
                Log("需要是主客户端才能生成网络对象");
                return;
            }
            
            if (_testObjectPrefab == null)
            {
                Log("未设置测试对象预制体");
                return;
            }
            
            Vector3 spawnPos = GetRandomSpawnPosition();
            PhotonNetwork.Instantiate(_testObjectPrefab.name, spawnPos, Random.rotation);
            
            Log($"生成测试对象于: {spawnPos}");
        }
        
        /// <summary>
        /// 清理测试对象
        /// </summary>
        public void CleanupTestObjects()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                Log("需要是主客户端才能清理对象");
                return;
            }
            
            // 清理所有网络对象（除了玩家）
            foreach (PhotonView pv in PhotonNetwork.PhotonViewCollection)
            {
                if (pv.gameObject.CompareTag("TestObject"))
                {
                    PhotonNetwork.Destroy(pv.gameObject);
                }
            }
            
            Log("清理测试对象完成");
        }
        
        /// <summary>
        /// 验证网络测试配置
        /// </summary>
        [ContextMenu("验证网络测试配置")]
        public void ValidateConfiguration()
        {
            Log("=== 网络测试配置验证 ===");
            
            // 检查玩家预制体
            if (_playerPrefab == null)
            {
                Log("❌ 错误: 未设置玩家预制体！");
            }
            else
            {
                PhotonView pv = _playerPrefab.GetComponent<PhotonView>();
                if (pv == null)
                {
                    Log("❌ 错误: 玩家预制体缺少PhotonView组件！");
                }
                else
                {
                    Log($"✅ 玩家预制体: {_playerPrefab.name}");
                }
            }
            
            // 检查生成点配置
            if (_spawnPoints == null || _spawnPoints.Length == 0)
            {
                Log("⚠️ 警告: 未配置生成点，将使用默认位置");
            }
            else
            {
                int validSpawnPoints = 0;
                for (int i = 0; i < _spawnPoints.Length; i++)
                {
                    if (_spawnPoints[i] != null)
                    {
                        validSpawnPoints++;
                        Log($"✅ 生成点 {i}: {_spawnPoints[i].name} at {_spawnPoints[i].position}");
                    }
                    else
                    {
                        Log($"❌ 生成点 {i}: 为null！");
                    }
                }
                Log($"总计 {validSpawnPoints}/{_spawnPoints.Length} 个有效生成点");
            }
            
            // 检查房间配置
            Log($"📡 房间名称: {_testRoomName}");
            Log($"🎲 使用随机房间名: {(_useRandomRoomName ? "是（可能导致多客户端分离）" : "否（推荐用于测试）")}");
            Log($"👥 最大玩家数: {_maxPlayers}");
            
            Log("=== 配置验证完成 ===");
        }

        #endregion
        
        #region Private Methods
          private void HandleInput()
        {
            if (Input.GetKeyDown(_quickJoinKey))
            {
                QuickJoin();
            }
            
            if (Input.GetKeyDown(_quickLeaveKey))
            {
                QuickLeave();
            }
            
            if (Input.GetKeyDown(_spawnPlayerKey))
            {
                SpawnTestPlayer();
            }
            
            if (Input.GetKeyDown(_fixJumpKey))
            {
                FixJumpFunctionality();
            }
        }
          private void JoinOrCreateTestRoom()
        {
            // 使用固定房间名，确保所有客户端都加入同一个房间
            string roomName = _useRandomRoomName ? 
                $"{_testRoomName}_{Random.Range(1000, 9999)}" : 
                _testRoomName;
            
            // 为了确保测试时多个客户端加入同一房间，建议使用固定名称
            if (_useRandomRoomName)
            {
                Log("警告: 使用随机房间名可能导致多个客户端进入不同房间，建议设置 _useRandomRoomName = false");
            }
            
            RoomOptions roomOptions = new RoomOptions()
            {
                MaxPlayers = _maxPlayers,
                IsVisible = true,
                IsOpen = true
            };
            
            // 设置房间属性
            roomOptions.CustomRoomProperties = new Hashtable()
            {
                { "gameMode", "Test" },
                { "version", PhotonNetwork.GameVersion }
            };
            
            Log($"尝试加入或创建房间: {roomName}");
            PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
        }        private Vector3 GetSpawnPosition()
        {
            // 简化生成点逻辑：仅使用手动配置的生成点
            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                // 选择基于玩家索引的生成点
                int playerIndex = PhotonNetwork.LocalPlayer.ActorNumber - 1;
                int spawnIndex = playerIndex % _spawnPoints.Length;

                // 安全检查：确保生成点Transform不为null
                Transform spawnPoint = _spawnPoints[spawnIndex];
                if (spawnPoint != null)
                {
                    Log($"使用生成点 {spawnIndex}: {spawnPoint.position}");
                    return spawnPoint.position;
                }
                else
                {
                    Log($"警告: 生成点 {spawnIndex} 为null，尝试查找其他可用生成点");

                    // 尝试找到第一个非null的生成点
                    for (int i = 0; i < _spawnPoints.Length; i++)
                    {
                        if (_spawnPoints[i] != null)
                        {
                            Log($"使用生成点 {i} 作为替代: {_spawnPoints[i].position}");
                            return _spawnPoints[i].position;
                        }
                    }

                    Log("错误: 所有配置的生成点都为null！请在Inspector中正确设置生成点");
                }
            }
            else
            {
                Log("警告: 未配置生成点数组！请在Inspector中设置 _spawnPoints");
            }

            // 返回默认生成位置
            Vector3 defaultPosition = Vector3.zero + Vector3.up * 2f;
            Log($"使用默认生成位置: {defaultPosition}");
            return defaultPosition;
        }
        
        private Vector3 GetRandomSpawnPosition()
        {
            return new Vector3(
                Random.Range(-10f, 10f),
                Random.Range(2f, 8f),
                Random.Range(-10f, 10f)
            );
        }
        
        private void CleanupSpawnedPlayers()
        {
            for (int i = _spawnedPlayers.Count - 1; i >= 0; i--)
            {
                if (_spawnedPlayers[i] != null)
                {
                    if (_spawnedPlayers[i].GetComponent<PhotonView>()?.IsMine == true)
                    {
                        PhotonNetwork.Destroy(_spawnedPlayers[i]);
                    }
                }
            }
            _spawnedPlayers.Clear();
        }
        
        private void StartTestSpawnCoroutine()
        {
            if (_testSyncObject && _testSpawnCoroutine == null)
            {
                _testSpawnCoroutine = StartCoroutine(TestSpawnCoroutine());
            }
        }
        
        private IEnumerator TestSpawnCoroutine()
        {
            while (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            {
                yield return new WaitForSeconds(_testObjectSpawnInterval);
                
                if (_testObjectPrefab != null)
                {
                    SpawnTestNetworkObject();
                }
            }
        }
        
        private void Log(string message)
        {
            if (_enableVerboseLogging)
            {
                Debug.Log($"[NetworkTestHelper] {message}");
            }
        }
        
        /// <summary>
        /// 验证生成位置是否有效
        /// </summary>
        private bool IsValidSpawnPosition(Vector3 position)
        {
            // 检查位置是否为NaN或无穷大
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z))
            {
                return false;
            }
            
            // 检查Y坐标是否过低（可能掉落到地面以下）
            if (position.y < -100f)
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 获取安全的生成位置
        /// </summary>
        private Vector3 GetSafeSpawnPosition()
        {
            // 返回一个确保安全的位置
            return new Vector3(0f, 5f, 0f);
        }

        #endregion
        
        #region Photon Callbacks
        
        public void OnConnected()
        {
            Log("已连接到Photon服务器");
        }
        
        public void OnConnectedToMaster()
        {
            _isConnecting = false;
            Log("已连接到主服务器");
            
            // 设置玩家昵称
            PhotonNetwork.LocalPlayer.NickName = $"TestPlayer_{Random.Range(100, 999)}";
            
            // 自动加入房间
            JoinOrCreateTestRoom();
        }
        
        public void OnDisconnected(DisconnectCause cause)
        {
            _isConnecting = false;
            Log($"已断开连接: {cause}");
            
            // 清理状态
            _hasSpawnedPlayer = false;
            CleanupSpawnedPlayers();
            
            if (_testSpawnCoroutine != null)
            {
                StopCoroutine(_testSpawnCoroutine);
                _testSpawnCoroutine = null;
            }
        }
          public void OnJoinedRoom()
        {
            Log($"成功加入房间: {PhotonNetwork.CurrentRoom.Name}");
            Log($"房间玩家数: {PhotonNetwork.CurrentRoom.PlayerCount}");
            
            // 自动生成玩家 - 增加延迟确保所有系统初始化完成
            if (_autoSpawnPlayer && !_hasSpawnedPlayer)
            {
                Invoke(nameof(SpawnTestPlayer), 2f); // 增加到2秒延迟，确保初始化完成
                Log("🕐 将在2秒后自动生成玩家，确保所有系统初始化完成");
            }
            
            // 开始测试协程
            StartTestSpawnCoroutine();
        }
        
        public void OnJoinRoomFailed(short returnCode, string message)
        {
            Log($"加入房间失败: {message} (代码: {returnCode})");
        }
        
        public void OnCreatedRoom()
        {
            Log("房间创建成功");
        }
        
        public void OnCreateRoomFailed(short returnCode, string message)
        {
            Log($"创建房间失败: {message} (代码: {returnCode})");
        }
        
        public void OnLeftRoom()
        {
            Log("已离开房间");
            _hasSpawnedPlayer = false;
            CleanupSpawnedPlayers();
            
            if (_testSpawnCoroutine != null)
            {
                StopCoroutine(_testSpawnCoroutine);
                _testSpawnCoroutine = null;
            }
        }
        
        public void OnPlayerEnteredRoom(Player newPlayer)
        {
            Log($"玩家 {newPlayer.NickName} 加入房间");
        }
        
        public void OnPlayerLeftRoom(Player otherPlayer)
        {
            Log($"玩家 {otherPlayer.NickName} 离开房间");
        }
        
        public void OnMasterClientSwitched(Player newMasterClient)
        {
            Log($"主客户端切换到: {newMasterClient.NickName}");
            
            // 重新开始测试协程
            StartTestSpawnCoroutine();
        }
        
        // 其他未使用的回调方法
        public void OnRegionListReceived(RegionHandler regionHandler) { }
        public void OnCustomAuthenticationResponse(Dictionary<string, object> data) { }
        public void OnCustomAuthenticationFailed(string debugMessage) { }
        public void OnFriendListUpdate(List<FriendInfo> friendList) { }
        public void OnJoinRandomFailed(short returnCode, string message) { }
        public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) { }
        public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) { }
        
        #endregion

        #region 修复玩家初始化问题

        /// <summary>
        /// 修复玩家初始化问题的协程 - 专门解决跳跃功能失效问题
        /// </summary>
        private IEnumerator FixPlayerInitialization(GameObject player)
        {
            if (player == null) yield break;
            
            // 只对本地玩家进行修复
            PhotonView pv = player.GetComponent<PhotonView>();
            if (pv == null || !pv.IsMine)
            {
                Log("🔧 跳过非本地玩家的初始化修复");
                yield break;
            }
            
            Log("🔧 开始修复本地玩家初始化...");
            
            // 等待足够的时间让所有组件完成初始化
            yield return new WaitForSeconds(0.2f);
            
            // 获取关键组件
            PlayerInput playerInput = player.GetComponent<PlayerInput>();
            PlayerMotor playerMotor = player.GetComponent<PlayerMotor>();
            
            // 修复PlayerInput初始化问题
            if (playerInput != null)
            {
                if (!playerInput.InputEnabled)
                {
                    Log("🔧 检测到PlayerInput未正确初始化，尝试修复...");
                    playerInput.ForceReinitialize();
                    yield return new WaitForSeconds(0.1f);
                    
                    if (playerInput.InputEnabled)
                    {
                        Log("✅ PlayerInput修复成功");
                    }
                    else
                    {
                        Log("❌ PlayerInput修复失败");
                    }
                }
                else
                {
                    Log("✅ PlayerInput状态正常");
                }
            }
            else
            {
                Log("❌ 警告: 玩家对象缺少PlayerInput组件");
            }
            
            // 重置PlayerMotor状态
            if (playerMotor != null)
            {
                playerMotor.ResetState();
                Log("✅ PlayerMotor状态已重置");
            }
            else
            {
                Log("❌ 警告: 玩家对象缺少PlayerMotor组件");
            }
            
            // 最终验证：测试跳跃输入是否正常工作
            yield return new WaitForSeconds(0.1f);
            StartCoroutine(ValidateJumpFunctionality(playerInput));
            
            Log("🔧 玩家初始化修复完成");
        }
        
        /// <summary>
        /// 验证跳跃功能是否正常工作
        /// </summary>
        private IEnumerator ValidateJumpFunctionality(PlayerInput playerInput)
        {
            if (playerInput == null) yield break;
            
            Log("🧪 开始验证跳跃功能...");
            
            // 监控跳跃输入几秒钟
            float testDuration = 3f;
            float startTime = Time.time;
            bool jumpDetected = false;
            
            while (Time.time - startTime < testDuration)
            {
                if (playerInput.JumpPressed)
                {
                    jumpDetected = true;
                    Log("✅ 跳跃输入检测正常！");
                    break;
                }
                yield return null;
            }
            
            if (!jumpDetected)
            {
                Log("⚠️ 在测试期间未检测到跳跃输入。请按空格键测试跳跃功能。");
                Log("💡 如果跳跃仍不工作，请尝试以下步骤：");
                Log("   1. 退出房间 (F2)");
                Log("   2. 重新加入房间 (F1)");
                Log("   3. 重新生成玩家 (F3)");
            }
        }

        /// <summary>
        /// 手动修复跳跃功能 - 用于解决运行时跳跃失效问题
        /// </summary>
        public void FixJumpFunctionality()
        {
            if (!PhotonNetwork.InRoom)
            {
                Log("❌ 需要在房间中才能修复跳跃功能");
                return;
            }
            
            // 查找本地玩家对象
            GameObject localPlayer = null;
            foreach (GameObject player in _spawnedPlayers)
            {
                if (player != null)
                {
                    PhotonView pv = player.GetComponent<PhotonView>();
                    if (pv != null && pv.IsMine)
                    {
                        localPlayer = player;
                        break;
                    }
                }
            }
            
            if (localPlayer == null)
            {
                Log("❌ 未找到本地玩家，请先生成玩家 (F3)");
                return;
            }
            
            Log("🔧 开始手动修复跳跃功能...");
            StartCoroutine(FixPlayerInitialization(localPlayer));
        }

        #endregion
    }
}
