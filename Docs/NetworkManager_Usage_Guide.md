# GravityShoot 网络管理器使用指南

## 概述

为 GravityShoot 项目设计并实现了完整的网络管理系统，基于 Photon PUN2，专为物理重力射击游戏优化。

## 核心组件

### 1. NetworkManager (网络管理器)
**位置**: `Assets/Scripts/Core/Networking/NetworkManager.cs`

**功能**:
- 管理 Photon 网络连接
- 处理房间创建和加入逻辑
- 自动重连机制
- 玩家管理和属性同步

**主要特性**:
- 单例模式，全局访问
- 自动连接和断线重连
- 房间匹配和创建
- 完整的回调事件系统

**使用方法**:
```csharp
// 连接到 Photon 网络
NetworkManager.Instance.ConnectToPhoton();

// 创建房间
NetworkManager.Instance.CreateRoom("MyRoom", 8);

// 加入随机房间
NetworkManager.Instance.JoinRandomRoom();

// 设置玩家昵称
NetworkManager.Instance.SetPlayerNickname("PlayerName");

// 监听事件
NetworkManager.Instance.OnConnectedToMasterEvent += OnConnectedToMaster;
NetworkManager.Instance.OnJoinedRoomEvent += OnJoinedRoom;
```

### 2. NetworkGameManager (网络游戏管理器)
**位置**: `Assets/Scripts/Core/Networking/NetworkGameManager.cs`

**功能**:
- 游戏状态同步 (大厅、匹配、游戏中等)
- 比赛计时和状态管理
- 玩家准备状态跟踪
- 得分和排行榜系统

**游戏阶段**:
- `Lobby`: 大厅阶段
- `Matching`: 匹配阶段
- `PreGame`: 游戏准备
- `InGame`: 游戏进行中
- `PostGame`: 游戏结束
- `Paused`: 游戏暂停

**使用方法**:
```csharp
// 设置玩家准备状态
gameManager.SetPlayerReady(true);

// 手动开始比赛
gameManager.StartMatchManually();

// 更新玩家得分
gameManager.UpdatePlayerScore(player, 100);

// 监听游戏事件
gameManager.OnMatchStarted += OnMatchStarted;
gameManager.OnGamePhaseChanged += OnPhaseChanged;
```

### 3. NetworkPlayerController (网络玩家控制器)
**位置**: `Assets/Scripts/Core/Networking/NetworkPlayerController.cs`

**功能**:
- 玩家位置和状态网络同步
- 客户端预测和服务器校正
- 重力方向同步
- 延迟补偿和插值

**特性**:
- 自动区分本地/远程玩家
- 物理状态网络同步
- 预测和校正机制
- 可调节的同步参数

**使用方法**:
```csharp
// 设置网络同步参数
playerController.SetNetworkParams(30f, 15f, 2f);

// 强制同步位置
playerController.ForceSync();

// 获取网络信息
string info = playerController.GetNetworkInfo();
```

### 4. NetworkInputManager (网络输入管理器)
**位置**: `Assets/Scripts/Core/Networking/NetworkInputManager.cs`

**功能**:
- 输入状态网络同步
- 输入预测和校正
- 输入平滑和缓冲
- 延迟补偿

**使用方法**:
```csharp
// 获取平滑后的输入
Vector2 moveInput = inputManager.SmoothedMoveInput;
Vector2 lookInput = inputManager.SmoothedLookInput;

// 检查按键状态
bool jumpPressed = inputManager.GetInputPressed("jump");
bool fireHeld = inputManager.GetInputHeld("fire");

// 设置输入参数
inputManager.SetInputParams(30f, true, 0.1f);
```

## 设置指南

### 1. 基础设置

1. **导入 PUN2 包**
   - 通过 Unity Package Manager 导入 PUN2
   - 配置 Photon 应用 ID

2. **创建网络管理器**
   ```csharp
   // 在场景中创建空物体并添加 NetworkManager 组件
   GameObject networkManager = new GameObject("NetworkManager");
   networkManager.AddComponent<NetworkManager>();
   ```

3. **配置网络设置**
   - 设置游戏版本号
   - 调整发送频率和序列化频率
   - 配置最大玩家数量

### 2. 玩家预制体设置

创建网络玩家预制体的推荐结构：
```
NetworkPlayer (PhotonView)
├── PlayerModel (可见模型)
├── PlayerMotor (物理运动)
├── PlayerInput (输入处理)
├── PlayerView (摄像机控制)
├── NetworkPlayerController (网络同步)
├── NetworkInputManager (输入同步)
└── Colliders (碰撞体)
```

**PhotonView 配置**:
- 添加 `NetworkPlayerController` 到 Observed Components
- 添加 `NetworkInputManager` 到 Observed Components
- 设置 View ID 为 Scene View ID (勾选)

### 3. 游戏管理器设置

在场景中添加游戏管理器：
```csharp
GameObject gameManager = new GameObject("GameManager");
PhotonView pv = gameManager.AddComponent<PhotonView>();
NetworkGameManager ngm = gameManager.AddComponent<NetworkGameManager>();

// 设置 PhotonView
pv.ObservedComponents.Add(ngm);
pv.Synchronization = ViewSynchronization.UnreliableOnChange;
```

## 网络架构设计

### 数据流向
```
输入 → NetworkInputManager → 网络传输 → 远程客户端
                ↓
PlayerMotor ← 本地处理 ← NetworkPlayerController
                ↓
物理模拟 → 位置更新 → 网络同步 → 远程客户端
```

### 同步策略
- **位置同步**: 使用插值和预测
- **输入同步**: 实时传输，客户端预测
- **游戏状态**: 主客户端权威
- **物理模拟**: 本地模拟 + 服务器校正

## 性能优化建议

### 1. 网络频率调整
```csharp
// 根据游戏需求调整同步频率
PhotonNetwork.SendRate = 30;           // 每秒30次发送
PhotonNetwork.SerializationRate = 20;  // 每秒20次序列化
```

### 2. 数据压缩
- 使用适当的数据类型
- 只同步必要的数据
- 利用 Photon 的数据压缩功能

### 3. 兴趣管理
```csharp
// 设置兴趣组，减少不必要的数据传输
photonView.Group = 1;
PhotonNetwork.SetInterestGroups(new byte[] { 1 }, null);
```

## 调试工具

### 1. 网络调试信息
```csharp
// 启用调试信息显示
networkManager._showDebugInfo = true;
gameManager._showGameDebugInfo = true;
playerController._showNetworkDebug = true;
```

### 2. 网络统计
```csharp
// 获取网络统计信息
string stats = NetworkManager.Instance.GetNetworkStats();
string quality = NetworkManager.Instance.GetConnectionQuality();
```

### 3. Gizmos 显示
启用预测和网络状态的可视化：
```csharp
playerController._showPredictionGizmos = true;
```

## 常见问题解决

### 1. 连接问题
- 检查 Photon 应用 ID 配置
- 确认网络连接稳定
- 查看 Unity Console 中的错误信息

### 2. 同步问题
- 调整同步频率和插值参数
- 检查 PhotonView 配置
- 确保组件正确添加到 Observed Components

### 3. 性能问题
- 降低同步频率
- 减少同步数据量
- 使用兴趣组管理

## 扩展功能

### 1. 语音聊天
可以集成 Photon Voice 实现语音通信

### 2. 反作弊
- 实现服务器权威验证
- 添加输入验证机制
- 监控异常行为

### 3. 数据持久化
- 保存玩家统计数据
- 实现成就系统
- 添加排行榜功能

## 总结

这套网络管理系统为 GravityShoot 项目提供了：
- 稳定的网络连接管理
- 完整的游戏状态同步
- 高质量的玩家同步
- 响应式的输入处理
- 可扩展的架构设计

系统专为物理重力射击游戏优化，支持复杂的重力环境和高速移动，能够提供流畅的多人游戏体验。
