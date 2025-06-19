# PUN2 网络开发完全指南

## 目录
1. [项目架构概述](#项目架构概述)
2. [PUN2 基础配置](#pun2-基础配置)
3. [网络组件详解](#网络组件详解)
4. [实战开发流程](#实战开发流程)
5. [子弹同步实现](#子弹同步实现)
6. [物理网络同步](#物理网络同步)
7. [调试与测试](#调试与测试)
8. [性能优化](#性能优化)
9. [常见问题解决](#常见问题解决)

---

## 项目架构概述

### 网络层级结构
```
GravityShoot 网络架构
├── NetworkManager (核心连接管理)
│   ├── 连接处理
│   ├── 房间管理
│   └── 事件分发
├── NetworkGameManager (游戏状态管理)
│   ├── 游戏阶段控制
│   ├── 玩家匹配
│   └── 得分系统
├── NetworkPlayerController (玩家网络同步)
│   ├── 位置同步
│   ├── 输入预测
│   └── 重力方向同步
├── NetworkInputManager (输入处理)
│   ├── 本地输入
│   ├── 网络分发
│   └── 延迟补偿
└── NetworkTestHelper (测试工具)
    ├── 快速连接
    ├── 玩家生成
    └── 调试工具
```

### 核心设计原则
1. **权威性**: Master Client 负责游戏逻辑
2. **预测性**: 客户端预测减少延迟感
3. **一致性**: 所有客户端状态保持同步
4. **容错性**: 网络异常时的回退机制

---

## PUN2 基础配置

### 1. Photon 应用配置

#### 步骤 1: 创建 Photon 应用
1. 访问 [Photon Dashboard](https://dashboard.photonengine.com)
2. 创建新的 PUN2 应用
3. 获取 App ID

#### 步骤 2: Unity 项目配置
```csharp
// 在 PhotonServerSettings 中设置
PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime = "your-app-id";
```

#### 步骤 3: 基础网络设置
```csharp
public class GameNetworkInit : MonoBehaviour
{
    private void Start()
    {
        // 设置游戏版本 - 只有相同版本的客户端才能匹配
        PhotonNetwork.GameVersion = "1.0";
        
        // 设置发送频率 (建议 20-30)
        PhotonNetwork.SendRate = 30;
        
        // 设置序列化频率 (建议 15-20)
        PhotonNetwork.SerializationRate = 20;
        
        // 启用自动场景同步
        PhotonNetwork.AutomaticallySyncScene = true;
        
        // 连接到 Photon
        PhotonNetwork.ConnectUsingSettings();
    }
}
```

### 2. 网络预制体配置

#### PhotonView 组件配置
```csharp
// 每个需要网络同步的对象都需要 PhotonView
public class NetworkObject : MonoBehaviourPun, IPunObservable
{
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 发送数据到其他客户端
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            // 接收来自其他客户端的数据
            transform.position = (Vector3)stream.ReceiveNext();
            transform.rotation = (Quaternion)stream.ReceiveNext();
        }
    }
}
```

#### Resources 文件夹配置
- 所有网络预制体必须放在 `Resources` 文件夹中
- 文件夹结构：
```
Assets/
└── Resources/
    ├── NetworkPrefabs/
    │   ├── Player.prefab
    │   ├── Bullet.prefab
    │   └── Pickup.prefab
    └── UI/
        └── NetworkUI.prefab
```

---

## 网络组件详解

### 1. NetworkManager - 核心连接管理器

#### 主要功能
- **连接管理**: 处理与 Photon 服务器的连接
- **房间管理**: 创建、加入、离开房间
- **玩家管理**: 玩家属性和状态同步
- **事件系统**: 网络事件的统一分发

#### 关键方法解析
```csharp
public class NetworkManager : Singleton<NetworkManager>, IConnectionCallbacks, IMatchmakingCallbacks
{
    // 连接到 Photon 网络
    public void ConnectToPhoton()
    {
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }
    
    // 创建房间
    public void CreateRoom(string roomName = null, byte maxPlayers = 4)
    {
        RoomOptions options = new RoomOptions()
        {
            MaxPlayers = maxPlayers,
            IsVisible = true,
            IsOpen = true
        };
        
        PhotonNetwork.CreateRoom(roomName, options);
    }
    
    // 加入随机房间
    public void JoinRandomRoom()
    {
        PhotonNetwork.JoinRandomRoom();
    }
}
```

#### 事件处理
```csharp
// 连接成功回调
public void OnConnectedToMaster()
{
    Debug.Log("连接到主服务器成功");
    // 可以开始创建或加入房间
}

// 加入房间回调
public void OnJoinedRoom()
{
    Debug.Log($"成功加入房间: {PhotonNetwork.CurrentRoom.Name}");
    // 生成玩家对象
    SpawnPlayer();
}
```

### 2. NetworkPlayerController - 玩家网络同步

#### 同步策略
1. **位置同步**: 使用插值平滑移动
2. **输入同步**: 本地预测 + 服务器校正
3. **状态同步**: 重要状态实时同步

#### 实现代码
```csharp
public class NetworkPlayerController : MonoBehaviourPun, IPunObservable
{
    [Header("网络同步")]
    public float sendRate = 30f;
    public float interpolationRate = 15f;
    
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 本地玩家发送数据
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(rigidbody.velocity);
        }
        else
        {
            // 远程玩家接收数据
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            Vector3 networkVelocity = (Vector3)stream.ReceiveNext();
            
            // 计算延迟补偿
            float lag = Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime));
            networkPosition += networkVelocity * lag;
        }
    }
    
    private void Update()
    {
        if (!photonView.IsMine)
        {
            // 插值到网络位置
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * interpolationRate);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * interpolationRate);
        }
    }
}
```

### 3. NetworkGameManager - 游戏状态管理

#### 游戏阶段管理
```csharp
public enum GamePhase
{
    Lobby,      // 大厅等待
    Countdown,  // 倒计时准备
    Playing,    // 游戏进行中
    GameOver    // 游戏结束
}

public class NetworkGameManager : MonoBehaviourPun
{
    private GamePhase currentPhase = GamePhase.Lobby;
    
    [PunRPC]
    public void ChangeGamePhase(GamePhase newPhase)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            currentPhase = newPhase;
            photonView.RPC("OnGamePhaseChanged", RpcTarget.Others, (int)newPhase);
        }
    }
    
    [PunRPC]
    public void OnGamePhaseChanged(int phaseInt)
    {
        currentPhase = (GamePhase)phaseInt;
        // 处理阶段变化逻辑
    }
}
```

---

## 实战开发流程

### 第一步：设置基础网络框架

1. **创建网络管理器**
```csharp
// 1. 创建空对象并添加 NetworkManager 脚本
// 2. 配置连接参数
[SerializeField] private string gameVersion = "1.0";
[SerializeField] private byte maxPlayersPerRoom = 4;

// 3. 在 Start() 中初始化
private void Start()
{
    PhotonNetwork.GameVersion = gameVersion;
    PhotonNetwork.ConnectUsingSettings();
}
```

2. **设置玩家预制体**
```csharp
// 1. 创建玩家预制体
// 2. 添加 PhotonView 组件
// 3. 添加 NetworkPlayerController 脚本
// 4. 配置 PhotonView 的 Observed Components
// 5. 保存到 Resources 文件夹
```

3. **创建测试场景**
```csharp
// 1. 创建测试场景
// 2. 添加 NetworkTestHelper 到场景中
// 3. 配置生成点和预制体引用
// 4. 设置快捷键和调试选项
```

### 第二步：实现玩家生成和基础同步

```csharp
public class PlayerSpawner : MonoBehaviourPun
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    
    public void SpawnPlayer()
    {
        if (playerPrefab == null) return;
        
        Vector3 spawnPos = GetSpawnPosition();
        GameObject player = PhotonNetwork.Instantiate(playerPrefab.name, spawnPos, Quaternion.identity);
        
        // 设置玩家属性
        var playerController = player.GetComponent<NetworkPlayerController>();
        playerController.SetupPlayer(PhotonNetwork.LocalPlayer);
    }
    
    private Vector3 GetSpawnPosition()
    {
        if (spawnPoints.Length == 0) return Vector3.zero;
        
        // 基于玩家 ID 选择生成点
        int playerIndex = PhotonNetwork.LocalPlayer.ActorNumber - 1;
        int spawnIndex = playerIndex % spawnPoints.Length;
        
        return spawnPoints[spawnIndex].position;
    }
}
```

### 第三步：添加输入和移动同步

```csharp
public class NetworkPlayerInput : MonoBehaviourPun
{
    private PlayerMotor playerMotor;
    private PlayerView playerView;
    
    private void Update()
    {
        if (!photonView.IsMine) return;
        
        // 收集输入
        Vector2 moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        Vector2 lookInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space);
        
        // 本地立即执行（客户端预测）
        playerMotor.ProcessInput(moveInput, jumpPressed);
        playerView.ProcessLookInput(lookInput);
        
        // 发送输入到其他客户端
        if (moveInput != Vector2.zero || jumpPressed)
        {
            photonView.RPC("OnInputReceived", RpcTarget.Others, moveInput.x, moveInput.y, jumpPressed);
        }
    }
    
    [PunRPC]
    public void OnInputReceived(float moveX, float moveY, bool jump)
    {
        Vector2 moveInput = new Vector2(moveX, moveY);
        playerMotor.ProcessInput(moveInput, jump);
    }
}
```

---

## 子弹同步实现

### 方案 1：完全同步子弹（适合慢速、重要弹药）

```csharp
public class NetworkBullet : MonoBehaviourPun, IPunObservable
{
    [Header("子弹属性")]
    public float speed = 20f;
    public float damage = 25f;
    public float lifetime = 5f;
    
    private Vector3 direction;
    private Player shooter;
    private float networkTime;
    
    private void Start()
    {
        // 如果不是创建者，等待网络数据
        if (!photonView.IsMine)
        {
            GetComponent<Rigidbody>().isKinematic = true;
        }
        
        // 设置销毁计时
        Destroy(gameObject, lifetime);
    }
    
    public void Initialize(Vector3 shootDirection, Player bulletShooter)
    {
        direction = shootDirection;
        shooter = bulletShooter;
        
        // 立即应用物理
        GetComponent<Rigidbody>().velocity = direction * speed;
    }
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 发送子弹状态
            stream.SendNext(transform.position);
            stream.SendNext(direction);
            stream.SendNext(GetComponent<Rigidbody>().velocity);
            stream.SendNext(PhotonNetwork.Time);
        }
        else
        {
            // 接收子弹状态
            Vector3 networkPos = (Vector3)stream.ReceiveNext();
            direction = (Vector3)stream.ReceiveNext();
            Vector3 networkVelocity = (Vector3)stream.ReceiveNext();
            networkTime = (double)stream.ReceiveNext();
            
            // 延迟补偿
            float lag = Mathf.Abs((float)(PhotonNetwork.Time - networkTime));
            transform.position = networkPos + networkVelocity * lag;
            GetComponent<Rigidbody>().velocity = networkVelocity;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // 只有子弹所有者处理碰撞
        if (!photonView.IsMine) return;
        
        if (other.CompareTag("Player"))
        {
            var targetPlayer = other.GetComponent<NetworkPlayerController>();
            if (targetPlayer != null && targetPlayer.photonView.owner != shooter)
            {
                // 造成伤害
                targetPlayer.photonView.RPC("TakeDamage", RpcTarget.All, damage, shooter.ActorNumber);
                
                // 销毁子弹
                PhotonNetwork.Destroy(gameObject);
            }
        }
    }
}
```

### 方案 2：射线检测同步（适合高速子弹）

```csharp
public class NetworkRaycastWeapon : MonoBehaviourPun
{
    [Header("武器配置")]
    public float damage = 50f;
    public float range = 100f;
    public LayerMask hitLayers = -1;
    
    [Header("特效")]
    public GameObject muzzleFlash;
    public GameObject hitEffect;
    public LineRenderer tracerLine;
    
    public void Fire()
    {
        if (!photonView.IsMine) return;
        
        Vector3 firePoint = transform.position;
        Vector3 fireDirection = transform.forward;
        
        // 本地立即执行射线检测
        RaycastHit hit;
        bool hasHit = Physics.Raycast(firePoint, fireDirection, out hit, range, hitLayers);
        
        Vector3 hitPoint = hasHit ? hit.point : firePoint + fireDirection * range;
        
        // 发送射击事件到所有客户端
        photonView.RPC("OnWeaponFired", RpcTarget.All, firePoint, hitPoint, hasHit);
        
        // 如果击中了玩家，处理伤害
        if (hasHit && hit.collider.CompareTag("Player"))
        {
            var targetPlayer = hit.collider.GetComponent<NetworkPlayerController>();
            if (targetPlayer != null)
            {
                targetPlayer.photonView.RPC("TakeDamage", RpcTarget.All, damage, PhotonNetwork.LocalPlayer.ActorNumber);
            }
        }
    }
    
    [PunRPC]
    public void OnWeaponFired(Vector3 startPos, Vector3 endPos, bool didHit)
    {
        // 播放特效
        StartCoroutine(ShowFireEffects(startPos, endPos, didHit));
    }
    
    private IEnumerator ShowFireEffects(Vector3 start, Vector3 end, bool hit)
    {
        // 枪口闪光
        if (muzzleFlash != null)
        {
            muzzleFlash.SetActive(true);
            yield return new WaitForSeconds(0.1f);
            muzzleFlash.SetActive(false);
        }
        
        // 子弹轨迹
        if (tracerLine != null)
        {
            tracerLine.SetPosition(0, start);
            tracerLine.SetPosition(1, end);
            tracerLine.enabled = true;
            
            yield return new WaitForSeconds(0.1f);
            tracerLine.enabled = false;
        }
        
        // 击中特效
        if (hit && hitEffect != null)
        {
            Instantiate(hitEffect, end, Quaternion.identity);
        }
    }
}
```

### 方案 3：混合方案（推荐）

```csharp
public class NetworkProjectileManager : MonoBehaviourPun
{
    [Header("子弹类型配置")]
    public ProjectileConfig[] projectileConfigs;
    
    [System.Serializable]
    public class ProjectileConfig
    {
        public string name;
        public ProjectileType type;
        public GameObject prefab;
        public float speed;
        public float damage;
        public bool usePhysicsSync; // 是否使用物理同步
    }
    
    public enum ProjectileType
    {
        Bullet,      // 高速，使用射线
        Rocket,      // 慢速，使用物理同步
        Grenade,     // 抛物线，使用物理同步
        Plasma       // 中速，可选择方案
    }
    
    public void FireProjectile(ProjectileType type, Vector3 startPos, Vector3 direction)
    {
        if (!photonView.IsMine) return;
        
        var config = GetProjectileConfig(type);
        if (config == null) return;
        
        if (config.usePhysicsSync)
        {
            // 生成物理同步子弹
            GameObject projectile = PhotonNetwork.Instantiate(config.prefab.name, startPos, Quaternion.LookRotation(direction));
            projectile.GetComponent<NetworkBullet>().Initialize(direction, PhotonNetwork.LocalPlayer);
        }
        else
        {
            // 使用射线检测
            PerformRaycastAttack(startPos, direction, config);
        }
    }
    
    private void PerformRaycastAttack(Vector3 start, Vector3 direction, ProjectileConfig config)
    {
        RaycastHit hit;
        bool hasHit = Physics.Raycast(start, direction, out hit, config.speed * 10f);
        Vector3 endPos = hasHit ? hit.point : start + direction * (config.speed * 10f);
        
        // 同步射击效果
        photonView.RPC("ShowProjectileEffect", RpcTarget.All, start, endPos, config.name);
        
        // 处理伤害
        if (hasHit && hit.collider.CompareTag("Player"))
        {
            var target = hit.collider.GetComponent<NetworkPlayerController>();
            target?.photonView.RPC("TakeDamage", RpcTarget.All, config.damage, PhotonNetwork.LocalPlayer.ActorNumber);
        }
    }
}
```

---

## 物理网络同步

### 重力系统同步

```csharp
public class NetworkGravitySync : MonoBehaviourPun, IPunObservable
{
    [Header("重力同步")]
    public float gravitySyncThreshold = 0.1f;
    
    private Vector3 currentGravityDirection;
    private Vector3 networkGravityDirection;
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(currentGravityDirection);
        }
        else
        {
            Vector3 receivedGravity = (Vector3)stream.ReceiveNext();
            
            // 只在重力方向显著改变时更新
            if (Vector3.Distance(receivedGravity, networkGravityDirection) > gravitySyncThreshold)
            {
                networkGravityDirection = receivedGravity;
            }
        }
    }
    
    public void ChangeGravityDirection(Vector3 newDirection)
    {
        if (!photonView.IsMine) return;
        
        currentGravityDirection = newDirection;
        
        // 立即应用重力变化
        ApplyGravityChange(newDirection);
        
        // 发送重力变化事件
        photonView.RPC("OnGravityChanged", RpcTarget.Others, newDirection.x, newDirection.y, newDirection.z);
    }
    
    [PunRPC]
    public void OnGravityChanged(float x, float y, float z)
    {
        Vector3 newGravity = new Vector3(x, y, z);
        ApplyGravityChange(newGravity);
    }
    
    private void ApplyGravityChange(Vector3 gravityDir)
    {
        // 应用到角色控制器
        var playerMotor = GetComponent<PlayerMotor>();
        if (playerMotor != null)
        {
            playerMotor.SetGravityDirection(gravityDir);
        }
        
        // 更新相机方向
        var playerView = GetComponent<PlayerView>();
        if (playerView != null)
        {
            playerView.UpdateGravityOrientation(gravityDir);
        }
    }
}
```

### 物理对象同步优化

```csharp
public class NetworkPhysicsObject : MonoBehaviourPun, IPunObservable
{
    [Header("同步设置")]
    public bool syncPosition = true;
    public bool syncRotation = true;
    public bool syncVelocity = true;
    public float positionThreshold = 0.1f;
    public float rotationThreshold = 5f;
    
    private Rigidbody rb;
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private Vector3 networkVelocity;
    private Vector3 networkAngularVelocity;
    
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // 只有所有者处理物理
        if (!photonView.IsMine)
        {
            rb.isKinematic = true;
        }
    }
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 发送物理状态
            if (syncPosition) stream.SendNext(transform.position);
            if (syncRotation) stream.SendNext(transform.rotation);
            if (syncVelocity) stream.SendNext(rb.velocity);
            stream.SendNext(rb.angularVelocity);
        }
        else
        {
            // 接收物理状态
            if (syncPosition) networkPosition = (Vector3)stream.ReceiveNext();
            if (syncRotation) networkRotation = (Quaternion)stream.ReceiveNext();
            if (syncVelocity) networkVelocity = (Vector3)stream.ReceiveNext();
            networkAngularVelocity = (Vector3)stream.ReceiveNext();
        }
    }
    
    private void Update()
    {
        if (!photonView.IsMine)
        {
            // 平滑插值到网络状态
            if (syncPosition)
            {
                float distance = Vector3.Distance(transform.position, networkPosition);
                if (distance > positionThreshold)
                {
                    transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 10f);
                }
            }
            
            if (syncRotation)
            {
                float angle = Quaternion.Angle(transform.rotation, networkRotation);
                if (angle > rotationThreshold)
                {
                    transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * 10f);
                }
            }
        }
    }
}
```

---

## 调试与测试

### 使用 NetworkTestHelper

1. **快速设置测试环境**
```csharp
// 1. 在场景中创建空对象
// 2. 添加 NetworkTestHelper 脚本
// 3. 配置测试参数：
[SerializeField] private GameObject playerPrefab;      // 玩家预制体
[SerializeField] private Transform[] spawnPoints;     // 生成点
[SerializeField] private bool autoStartOnAwake = true; // 自动开始

// 4. 设置快捷键
F1 - 快速加入房间
F2 - 离开房间
F3 - 生成玩家
```

2. **多客户端测试流程**
```
步骤 1: 在 Unity Editor 中运行场景
步骤 2: Build 项目生成 .exe 文件
步骤 3: 同时运行 Editor 和 Build 版本
步骤 4: 使用 NetworkTestHelper 的 GUI 或快捷键进行测试
步骤 5: 观察网络同步效果
```

### 网络调试工具

```csharp
public class NetworkDebugger : MonoBehaviourPun
{
    [Header("调试显示")]
    public bool showNetworkStats = true;
    public bool showPlayerList = true;
    public bool showRoomInfo = true;
    
    private void OnGUI()
    {
        if (!showNetworkStats) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Box("网络调试信息");
        
        // 连接状态
        GUILayout.Label($"网络状态: {PhotonNetwork.NetworkClientState}");
        GUILayout.Label($"Ping: {PhotonNetwork.GetPing()}ms");
        GUILayout.Label($"发送频率: {PhotonNetwork.SendRate}Hz");
        GUILayout.Label($"序列化频率: {PhotonNetwork.SerializationRate}Hz");
        
        // 房间信息
        if (PhotonNetwork.InRoom && showRoomInfo)
        {
            GUILayout.Label($"房间: {PhotonNetwork.CurrentRoom.Name}");
            GUILayout.Label($"玩家数: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}");
            GUILayout.Label($"是否主客户端: {PhotonNetwork.IsMasterClient}");
        }
        
        // 玩家列表
        if (PhotonNetwork.InRoom && showPlayerList)
        {
            GUILayout.Label("玩家列表:");
            foreach (var player in PhotonNetwork.PlayerList)
            {
                string playerInfo = $"  {player.NickName} (ID: {player.ActorNumber})";
                if (player.IsMasterClient) playerInfo += " [Master]";
                if (player.IsLocal) playerInfo += " [Local]";
                GUILayout.Label(playerInfo);
            }
        }
        
        GUILayout.EndArea();
    }
}
```

### 性能监控

```csharp
public class NetworkPerformanceMonitor : MonoBehaviourPun
{
    private struct PerformanceData
    {
        public float ping;
        public int fps;
        public float networkTraffic;
        public DateTime timestamp;
    }
    
    private List<PerformanceData> performanceHistory = new List<PerformanceData>();
    private float updateInterval = 1f;
    private float lastUpdateTime;
    
    private void Update()
    {
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            RecordPerformanceData();
            lastUpdateTime = Time.time;
        }
    }
    
    private void RecordPerformanceData()
    {
        var data = new PerformanceData
        {
            ping = PhotonNetwork.GetPing(),
            fps = Mathf.RoundToInt(1f / Time.deltaTime),
            networkTraffic = GetNetworkTraffic(),
            timestamp = DateTime.Now
        };
        
        performanceHistory.Add(data);
        
        // 保持最近 100 条记录
        if (performanceHistory.Count > 100)
        {
            performanceHistory.RemoveAt(0);
        }
        
        // 检测异常
        CheckForAnomalies(data);
    }
    
    private float GetNetworkTraffic()
    {
        if (PhotonNetwork.NetworkingClient?.LoadBalancingPeer?.TrafficStatsIncoming != null)
        {
            return PhotonNetwork.NetworkingClient.LoadBalancingPeer.TrafficStatsIncoming.TotalPacketBytes;
        }
        return 0f;
    }
    
    private void CheckForAnomalies(PerformanceData data)
    {
        // 高延迟警告
        if (data.ping > 200)
        {
            Debug.LogWarning($"高延迟检测: {data.ping}ms");
        }
        
        // 低帧率警告
        if (data.fps < 30)
        {
            Debug.LogWarning($"低帧率检测: {data.fps}FPS");
        }
    }
}
```

---

## 性能优化

### 1. 网络发送频率优化

```csharp
public class AdaptiveNetworkRate : MonoBehaviourPun
{
    [Header("自适应设置")]
    public int baseSendRate = 30;
    public int baseSerializationRate = 20;
    public int minSendRate = 15;
    public int maxSendRate = 60;
    
    private void Start()
    {
        StartCoroutine(AdaptNetworkRates());
    }
    
    private IEnumerator AdaptNetworkRates()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);
            
            if (PhotonNetwork.IsConnected)
            {
                int ping = PhotonNetwork.GetPing();
                int playerCount = PhotonNetwork.CurrentRoom?.PlayerCount ?? 1;
                
                // 根据延迟和玩家数量调整发送频率
                int adaptedSendRate = CalculateOptimalSendRate(ping, playerCount);
                int adaptedSerializationRate = Mathf.RoundToInt(adaptedSendRate * 0.7f);
                
                PhotonNetwork.SendRate = adaptedSendRate;
                PhotonNetwork.SerializationRate = adaptedSerializationRate;
                
                Debug.Log($"网络频率调整: 发送{adaptedSendRate}Hz, 序列化{adaptedSerializationRate}Hz");
            }
        }
    }
    
    private int CalculateOptimalSendRate(int ping, int playerCount)
    {
        int rate = baseSendRate;
        
        // 根据延迟调整
        if (ping > 150) rate -= 10;
        else if (ping < 50) rate += 5;
        
        // 根据玩家数量调整
        rate -= (playerCount - 1) * 2;
        
        return Mathf.Clamp(rate, minSendRate, maxSendRate);
    }
}
```

### 2. 数据压缩优化

```csharp
public class CompressedNetworkSync : MonoBehaviourPun, IPunObservable
{
    [Header("压缩设置")]
    public float positionPrecision = 0.01f;
    public float rotationPrecision = 1f;
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 压缩位置数据（减少精度）
            Vector3 compressedPos = CompressPosition(transform.position);
            stream.SendNext(compressedPos);
            
            // 压缩旋转数据（只发送Y轴）
            float compressedY = CompressRotation(transform.eulerAngles.y);
            stream.SendNext(compressedY);
        }
        else
        {
            Vector3 pos = (Vector3)stream.ReceiveNext();
            float rotY = (float)stream.ReceiveNext();
            
            // 应用数据
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0, rotY, 0);
        }
    }
    
    private Vector3 CompressPosition(Vector3 pos)
    {
        return new Vector3(
            Mathf.Round(pos.x / positionPrecision) * positionPrecision,
            Mathf.Round(pos.y / positionPrecision) * positionPrecision,
            Mathf.Round(pos.z / positionPrecision) * positionPrecision
        );
    }
    
    private float CompressRotation(float angle)
    {
        return Mathf.Round(angle / rotationPrecision) * rotationPrecision;
    }
}
```

### 3. 对象池优化

```csharp
public class NetworkObjectPool : MonoBehaviourPun
{
    [Header("对象池配置")]
    public GameObject bulletPrefab;
    public int poolSize = 50;
    
    private Queue<GameObject> bulletPool = new Queue<GameObject>();
    private List<GameObject> activeBullets = new List<GameObject>();
    
    private void Start()
    {
        InitializePool();
    }
    
    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject bullet = Instantiate(bulletPrefab);
            bullet.SetActive(false);
            bulletPool.Enqueue(bullet);
        }
    }
    
    public GameObject GetBullet()
    {
        GameObject bullet;
        
        if (bulletPool.Count > 0)
        {
            bullet = bulletPool.Dequeue();
        }
        else
        {
            bullet = Instantiate(bulletPrefab);
        }
        
        bullet.SetActive(true);
        activeBullets.Add(bullet);
        return bullet;
    }
    
    public void ReturnBullet(GameObject bullet)
    {
        bullet.SetActive(false);
        activeBullets.Remove(bullet);
        bulletPool.Enqueue(bullet);
    }
    
    [PunRPC]
    public void SpawnNetworkBullet(Vector3 position, Vector3 direction)
    {
        GameObject bullet = GetBullet();
        bullet.transform.position = position;
        bullet.GetComponent<Rigidbody>().velocity = direction * 20f;
        
        // 5秒后回收
        StartCoroutine(ReturnBulletAfterDelay(bullet, 5f));
    }
    
    private IEnumerator ReturnBulletAfterDelay(GameObject bullet, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (bullet.activeInHierarchy)
        {
            ReturnBullet(bullet);
        }
    }
}
```

---

## 常见问题解决

### 1. 连接问题

#### 问题：无法连接到 Photon 服务器
```csharp
// 解决方案：检查网络设置
public void DiagnoseConnection()
{
    Debug.Log($"Photon状态: {PhotonNetwork.NetworkClientState}");
    Debug.Log($"App ID: {PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime}");
    Debug.Log($"区域: {PhotonNetwork.CloudRegion}");
    
    // 重置连接
    if (PhotonNetwork.IsConnected)
    {
        PhotonNetwork.Disconnect();
    }
    
    // 清除缓存
    PhotonNetwork.BestRegionSummaryInPreferences = null;
    
    // 重新连接
    PhotonNetwork.ConnectUsingSettings();
}
```

#### 问题：频繁断线重连
```csharp
// 解决方案：实现可靠的重连机制
public class ReliableReconnect : MonoBehaviourPun, IConnectionCallbacks
{
    private int reconnectAttempts = 0;
    private const int maxReconnectAttempts = 5;
    
    public void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"断线原因: {cause}");
        
        // 根据原因决定是否重连
        if (ShouldReconnect(cause) && reconnectAttempts < maxReconnectAttempts)
        {
            reconnectAttempts++;
            StartCoroutine(ReconnectAfterDelay(2f * reconnectAttempts));
        }
    }
    
    private bool ShouldReconnect(DisconnectCause cause)
    {
        switch (cause)
        {
            case DisconnectCause.DisconnectByClientLogic:
                return false; // 主动断开，不重连
            case DisconnectCause.TimeoutDisconnect:
            case DisconnectCause.ExceptionOnConnect:
                return true;  // 网络问题，尝试重连
            default:
                return true;
        }
    }
    
    private IEnumerator ReconnectAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        PhotonNetwork.ConnectUsingSettings();
    }
}
```

### 2. 同步问题

#### 问题：位置同步不平滑
```csharp
// 解决方案：改进插值算法
public class SmoothNetworkTransform : MonoBehaviourPun, IPunObservable
{
    private Vector3 networkPosition;
    private Vector3 previousPosition;
    private float lastReceiveTime;
    private float lerpRate = 15f;
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
        }
        else
        {
            previousPosition = networkPosition;
            networkPosition = (Vector3)stream.ReceiveNext();
            lastReceiveTime = (float)info.SentServerTime;
        }
    }
    
    private void Update()
    {
        if (!photonView.IsMine)
        {
            // 计算预测位置
            float timeSinceUpdate = (float)(PhotonNetwork.Time - lastReceiveTime);
            Vector3 velocity = (networkPosition - previousPosition) / PhotonNetwork.SerializationRate;
            Vector3 predictedPosition = networkPosition + velocity * timeSinceUpdate;
            
            // 平滑移动到预测位置
            transform.position = Vector3.Lerp(transform.position, predictedPosition, Time.deltaTime * lerpRate);
        }
    }
}
```

#### 问题：输入延迟明显
```csharp
// 解决方案：客户端预测 + 服务器校正
public class PredictiveInput : MonoBehaviourPun
{
    private struct InputState
    {
        public Vector2 moveInput;
        public bool jumpInput;
        public float timestamp;
        public Vector3 position;
    }
    
    private Queue<InputState> inputHistory = new Queue<InputState>();
    private const int maxHistorySize = 60;
    
    private void Update()
    {
        if (!photonView.IsMine) return;
        
        // 收集输入
        Vector2 moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        bool jumpInput = Input.GetKeyDown(KeyCode.Space);
        
        // 记录输入状态
        var inputState = new InputState
        {
            moveInput = moveInput,
            jumpInput = jumpInput,
            timestamp = (float)PhotonNetwork.Time,
            position = transform.position
        };
        
        inputHistory.Enqueue(inputState);
        if (inputHistory.Count > maxHistorySize)
        {
            inputHistory.Dequeue();
        }
        
        // 本地立即处理（预测）
        ProcessInput(moveInput, jumpInput);
        
        // 发送到服务器
        photonView.RPC("ProcessServerInput", RpcTarget.MasterClient, moveInput.x, moveInput.y, jumpInput, (float)PhotonNetwork.Time);
    }
    
    [PunRPC]
    public void ProcessServerInput(float moveX, float moveY, bool jump, float timestamp)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        Vector2 moveInput = new Vector2(moveX, moveY);
        ProcessInput(moveInput, jump);
        
        // 发送权威位置回客户端
        photonView.RPC("ReceiveServerCorrection", photonView.owner, transform.position.x, transform.position.y, transform.position.z, timestamp);
    }
    
    [PunRPC]
    public void ReceiveServerCorrection(float x, float y, float z, float timestamp)
    {
        Vector3 serverPosition = new Vector3(x, y, z);
        float positionError = Vector3.Distance(transform.position, serverPosition);
        
        // 如果误差太大，进行校正
        if (positionError > 0.5f)
        {
            transform.position = serverPosition;
            
            // 重新应用时间戳之后的输入
            ReplayInputsAfterTimestamp(timestamp);
        }
    }
    
    private void ReplayInputsAfterTimestamp(float timestamp)
    {
        var inputsToReplay = new List<InputState>();
        
        foreach (var input in inputHistory)
        {
            if (input.timestamp > timestamp)
            {
                inputsToReplay.Add(input);
            }
        }
        
        foreach (var input in inputsToReplay)
        {
            ProcessInput(input.moveInput, input.jumpInput);
        }
    }
    
    private void ProcessInput(Vector2 moveInput, bool jumpInput)
    {
        // 这里调用实际的移动逻辑
        GetComponent<PlayerMotor>()?.ProcessInput(moveInput, jumpInput);
    }
}
```

### 3. 性能问题

#### 问题：网络流量过大
```csharp
// 解决方案：条件发送和数据压缩
public class OptimizedNetworkSync : MonoBehaviourPun, IPunObservable
{
    [Header("优化设置")]
    public float sendThreshold = 0.1f;  // 位置变化阈值
    public float sendInterval = 0.1f;   // 最小发送间隔
    
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;
    private float lastSendTime;
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 检查是否需要发送更新
            bool shouldSend = ShouldSendUpdate();
            
            stream.SendNext(shouldSend);
            
            if (shouldSend)
            {
                stream.SendNext(transform.position);
                stream.SendNext(transform.rotation);
                
                lastSentPosition = transform.position;
                lastSentRotation = transform.rotation;
                lastSendTime = Time.time;
            }
        }
        else
        {
            bool hasUpdate = (bool)stream.ReceiveNext();
            
            if (hasUpdate)
            {
                Vector3 pos = (Vector3)stream.ReceiveNext();
                Quaternion rot = (Quaternion)stream.ReceiveNext();
                
                // 应用插值
                StartCoroutine(InterpolateToTarget(pos, rot));
            }
        }
    }
    
    private bool ShouldSendUpdate()
    {
        // 检查时间间隔
        if (Time.time - lastSendTime < sendInterval)
            return false;
        
        // 检查位置变化
        float positionDelta = Vector3.Distance(transform.position, lastSentPosition);
        if (positionDelta < sendThreshold)
            return false;
        
        // 检查旋转变化
        float rotationDelta = Quaternion.Angle(transform.rotation, lastSentRotation);
        if (rotationDelta < 5f) // 5度阈值
            return false;
        
        return true;
    }
    
    private IEnumerator InterpolateToTarget(Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        
        float duration = sendInterval;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Lerp(startRot, targetRot, t);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        transform.position = targetPos;
        transform.rotation = targetRot;
    }
}
```

---

## 总结

### 开发要点回顾

1. **网络架构设计**
   - 使用 Master Client 作为游戏权威
   - 客户端预测降低延迟感知
   - 合理划分同步职责

2. **数据同步策略**
   - 重要数据：可靠同步（RPC）
   - 频繁数据：不可靠但高频同步（IPunObservable）
   - 大量数据：条件发送和压缩

3. **性能优化原则**
   - 减少网络包大小和频率
   - 使用对象池避免频繁实例化
   - 自适应调整网络参数

4. **调试测试工具**
   - NetworkTestHelper 快速测试
   - 多客户端验证同步效果
   - 性能监控和异常检测

### 后续开发建议

1. **扩展武器系统**
   - 实现不同类型武器的网络同步
   - 添加武器特效和音效同步
   - 支持武器附件和升级

2. **完善游戏模式**
   - 实现多种游戏模式
   - 添加计分和排行榜
   - 支持观战和回放功能

3. **优化用户体验**
   - 改进网络状态提示
   - 添加断线重连机制
   - 实现服务器选择和延迟显示

通过本指南，您应该能够：
- 理解 PUN2 的基本工作原理
- 使用项目中的网络组件进行开发
- 实现各种游戏功能的网络同步
- 解决常见的网络开发问题
- 优化网络性能和用户体验

记住，网络游戏开发是一个迭代过程，需要不断测试、调试和优化。建议从简单功能开始，逐步增加复杂性，确保每个功能都能稳定运行再继续下一步开发。
