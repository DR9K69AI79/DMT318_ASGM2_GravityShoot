# GravityShoot - PUN2 网络同步实现方案

## 项目概述

GravityShoot是一个基于重力的第一人称射击游戏，具有复杂的自定义重力系统、基于物理的角色移动和重力感知相机控制。本文档详细说明了如何使用PUN2实现多人网络同步。

## 技术架构概览

### 核心系统
- **重力系统**: 支持多种重力源（球形、平面、盒形等）
- **物理移动**: 基于Rigidbody的直接速度控制
- **相机系统**: 随重力方向动态调整的FPS相机
- **输入系统**: Unity Input System集成
- **角色动画**: PuppetMaster ragdoll系统

## 第一阶段：PUN2基础设置

### 1.1 安装和配置

```bash
# 通过Unity Package Manager安装PUN2
Window > Package Manager > My Assets > PUN2 FREE/PLUS
```

### 1.2 核心网络组件设计

#### 网络玩家预制体架构
```
NetworkPlayer (PhotonView)
├── PlayerAvatar (可见模型)
│   ├── RBPlayerMotor (本地控制)
│   ├── NetworkPlayerController (网络同步)
│   └── FPSGravityCamera (仅本地玩家激活)
├── PuppetMaster (物理模拟)
│   └── ragdoll components
└── NetworkComponents
    ├── PhotonTransformView
    ├── NetworkGravitySync
    └── NetworkInputRelay
```

## 第二阶段：网络同步架构

### 2.1 玩家移动同步

#### NetworkPlayerController (新建组件)
```csharp
public class NetworkPlayerController : MonoBehaviourPunPV, IPunObservable
{
    [Header("同步设置")]
    public float sendRate = 30f;           // 数据发送频率
    public float interpolationRate = 15f;  // 插值平滑度
    
    [Header("预测设置")]
    public bool enablePositionPrediction = true;
    public bool enableRotationPrediction = true;
    public float maxPredictionTime = 0.5f;
    
    // 网络同步数据
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private Vector3 networkVelocity;
    private Vector3 networkGravityDirection;
    
    // 本地引用
    private RBPlayerMotor playerMotor;
    private FPSGravityCamera gravityCamera;
    private PhotonView photonView;
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 发送数据到其他客户端
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(playerMotor.Velocity);
            stream.SendNext(CustomGravity.GetGravity(transform.position).normalized);
            stream.SendNext(playerMotor.IsGrounded);
            stream.SendNext(gravityCamera.transform.rotation);
        }
        else
        {
            // 接收其他客户端数据
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkVelocity = (Vector3)stream.ReceiveNext();
            networkGravityDirection = (Vector3)stream.ReceiveNext();
            bool isGrounded = (bool)stream.ReceiveNext();
            Quaternion cameraRotation = (Quaternion)stream.ReceiveNext();
            
            // 应用网络插值
            ApplyNetworkData(info);
        }
    }
    
    private void ApplyNetworkData(PhotonMessageInfo info)
    {
        if (!photonView.IsMine)
        {
            // 时间同步补偿
            float timeDifference = (float)(PhotonNetwork.Time - info.SentServerTime);
            Vector3 predictedPosition = networkPosition + networkVelocity * timeDifference;
            
            // 平滑插值到目标位置
            transform.position = Vector3.Lerp(transform.position, predictedPosition, Time.deltaTime * interpolationRate);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * interpolationRate);
        }
    }
}
```

### 2.2 重力系统网络同步

#### NetworkGravityManager (新建组件)
```csharp
public class NetworkGravityManager : MonoBehaviourPunPV
{
    [Header("重力同步设置")]
    public bool syncGravitySources = true;
    public float gravityUpdateRate = 10f;
    
    // 重力源网络同步
    private Dictionary<int, GravitySourceData> networkGravitySources;
    
    [System.Serializable]
    public struct GravitySourceData
    {
        public int sourceId;
        public Vector3 position;
        public Vector3 gravity;
        public float range;
        public bool isActive;
    }
    
    [PunRPC]
    public void SyncGravitySource(int sourceId, Vector3 position, Vector3 gravity, float range, bool isActive)
    {
        if (networkGravitySources.ContainsKey(sourceId))
        {
            networkGravitySources[sourceId] = new GravitySourceData
            {
                sourceId = sourceId,
                position = position,
                gravity = gravity,
                range = range,
                isActive = isActive
            };
        }
        
        // 应用到本地重力系统
        ApplyGravitySourceUpdate(sourceId, position, gravity, range, isActive);
    }
    
    private void ApplyGravitySourceUpdate(int sourceId, Vector3 position, Vector3 gravity, float range, bool isActive)
    {
        // 在本地重力系统中更新对应的重力源
        var gravitySource = FindGravitySourceById(sourceId);
        if (gravitySource != null)
        {
            gravitySource.transform.position = position;
            gravitySource.enabled = isActive;
        }
    }
}
```

### 2.3 输入系统网络化

#### NetworkInputManager (新建组件)
```csharp
public class NetworkInputManager : MonoBehaviourPunPV
{
    [Header("输入同步设置")]
    public float inputSyncRate = 60f;
    
    // 输入数据结构
    [System.Serializable]
    public struct NetworkInputData
    {
        public Vector2 moveInput;
        public Vector2 lookInput;
        public bool jumpPressed;
        public bool shootPressed;
        public bool isGrounded;
        public float timestamp;
    }
    
    private Queue<NetworkInputData> inputBuffer = new Queue<NetworkInputData>();
    private PlayerInput playerInput;
    
    void Start()
    {
        playerInput = GetComponent<PlayerInput>();
        
        // 只在本地玩家上启用输入
        if (!photonView.IsMine)
        {
            playerInput.enabled = false;
        }
    }
    
    void FixedUpdate()
    {
        if (photonView.IsMine)
        {
            // 收集本地输入
            NetworkInputData inputData = new NetworkInputData
            {
                moveInput = playerInput.GetMoveInput(),
                lookInput = playerInput.GetLookInput(),
                jumpPressed = playerInput.GetJumpPressed(),
                shootPressed = playerInput.GetShootPressed(),
                timestamp = Time.fixedTime
            };
            
            // 发送输入数据
            SendInputData(inputData);
        }
        else
        {
            // 处理接收到的输入数据
            ProcessReceivedInput();
        }
    }
    
    [PunRPC]
    void SendInputData(NetworkInputData inputData)
    {
        if (!photonView.IsMine)
        {
            inputBuffer.Enqueue(inputData);
        }
    }
}
```

## 第三阶段：物理同步优化

### 3.1 客户端预测与服务器权威

#### ClientPrediction (新建组件)
```csharp
public class ClientPrediction : MonoBehaviourPunPV
{
    [Header("预测设置")]
    public bool enablePrediction = true;
    public int maxPredictionFrames = 60;
    public float reconciliationThreshold = 0.1f;
    
    // 预测历史
    private Queue<PredictionState> predictionHistory = new Queue<PredictionState>();
    
    [System.Serializable]
    public struct PredictionState
    {
        public Vector3 position;
        public Vector3 velocity;
        public Quaternion rotation;
        public float timestamp;
        public int frameNumber;
    }
    
    public void RecordPredictionState()
    {
        if (!photonView.IsMine) return;
        
        PredictionState state = new PredictionState
        {
            position = transform.position,
            velocity = GetComponent<RBPlayerMotor>().Velocity,
            rotation = transform.rotation,
            timestamp = Time.fixedTime,
            frameNumber = Time.fixedUnscaledTime.GetHashCode()
        };
        
        predictionHistory.Enqueue(state);
        
        // 限制历史记录大小
        while (predictionHistory.Count > maxPredictionFrames)
        {
            predictionHistory.Dequeue();
        }
    }
    
    public void ReconcileWithServer(Vector3 serverPosition, float serverTimestamp)
    {
        if (!photonView.IsMine) return;
        
        // 找到对应时间戳的预测状态
        PredictionState? matchingState = FindPredictionState(serverTimestamp);
        
        if (matchingState.HasValue)
        {
            float positionError = Vector3.Distance(matchingState.Value.position, serverPosition);
            
            if (positionError > reconciliationThreshold)
            {
                // 需要进行校正
                transform.position = serverPosition;
                
                // 重新执行后续的预测帧
                ReplayPredictionFromState(matchingState.Value);
            }
        }
    }
}
```

### 3.2 重力场插值同步

#### GravityFieldInterpolator (新建组件)
```csharp
public class GravityFieldInterpolator : MonoBehaviourPunPV
{
    [Header("重力场同步")]
    public float gravityTransitionSpeed = 5f;
    public bool smoothGravityTransitions = true;
    
    private Vector3 targetGravityDirection;
    private Vector3 currentGravityDirection;
    
    void FixedUpdate()
    {
        if (!photonView.IsMine)
        {
            // 为远程玩家插值重力方向
            InterpolateGravityDirection();
        }
    }
    
    private void InterpolateGravityDirection()
    {
        if (smoothGravityTransitions)
        {
            currentGravityDirection = Vector3.Slerp(
                currentGravityDirection, 
                targetGravityDirection, 
                Time.fixedDeltaTime * gravityTransitionSpeed
            );
        }
        else
        {
            currentGravityDirection = targetGravityDirection;
        }
        
        // 应用插值后的重力方向到相机和角色控制器
        ApplyInterpolatedGravity();
    }
    
    public void SetTargetGravityDirection(Vector3 newDirection)
    {
        targetGravityDirection = newDirection.normalized;
    }
}
```

## 第四阶段：高级同步功能

### 4.1 射击同步

#### NetworkWeaponController (新建组件)
```csharp
public class NetworkWeaponController : MonoBehaviourPunPV
{
    [Header("武器同步")]
    public LayerMask hitLayers;
    public float maxShootDistance = 100f;
    
    [PunRPC]
    void FireWeapon(Vector3 origin, Vector3 direction, float timestamp)
    {
        // 执行射线检测
        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, maxShootDistance, hitLayers))
        {
            // 处理命中
            ProcessHit(hit, timestamp);
        }
        
        // 播放射击效果
        PlayShootEffects(origin, direction);
    }
    
    private void ProcessHit(RaycastHit hit, float timestamp)
    {
        // 检查命中的是否是其他玩家
        NetworkPlayerController hitPlayer = hit.collider.GetComponent<NetworkPlayerController>();
        if (hitPlayer != null)
        {
            // 发送伤害信息
            hitPlayer.photonView.RPC("TakeDamage", RpcTarget.All, 25f, timestamp);
        }
    }
    
    public void Shoot()
    {
        if (!photonView.IsMine) return;
        
        Vector3 shootOrigin = Camera.main.transform.position;
        Vector3 shootDirection = Camera.main.transform.forward;
        
        // 立即执行本地射击
        FireWeapon(shootOrigin, shootDirection, PhotonNetwork.Time);
        
        // 同步到其他客户端
        photonView.RPC("FireWeapon", RpcTarget.Others, shootOrigin, shootDirection, PhotonNetwork.Time);
    }
}
```

### 4.2 动态重力源同步

#### DynamicGravitySourceSync (新建组件)
```csharp
public class DynamicGravitySourceSync : MonoBehaviourPunPV, IPunObservable
{
    [Header("动态重力源")]
    public GravitySource gravitySource;
    public bool syncPosition = true;
    public bool syncIntensity = true;
    
    private Vector3 networkPosition;
    private float networkIntensity;
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            if (syncPosition) stream.SendNext(transform.position);
            if (syncIntensity) stream.SendNext(gravitySource.gravity.magnitude);
        }
        else
        {
            if (syncPosition) networkPosition = (Vector3)stream.ReceiveNext();
            if (syncIntensity) networkIntensity = (float)stream.ReceiveNext();
            
            // 应用网络数据
            if (!photonView.IsMine)
            {
                if (syncPosition)
                    transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 10f);
                if (syncIntensity)
                    gravitySource.gravity = gravitySource.gravity.normalized * networkIntensity;
            }
        }
    }
}
```

## 第五阶段：性能优化

### 5.1 LOD系统

#### NetworkLODManager (新建组件)
```csharp
public class NetworkLODManager : MonoBehaviourPunPV
{
    [Header("LOD设置")]
    public float highDetailDistance = 20f;
    public float mediumDetailDistance = 50f;
    public float lowDetailDistance = 100f;
    
    public enum LODLevel
    {
        High,    // 60Hz同步
        Medium,  // 30Hz同步
        Low,     // 15Hz同步
        Culled   // 不同步
    }
    
    private LODLevel currentLOD = LODLevel.High;
    
    void Update()
    {
        if (photonView.IsMine) return;
        
        // 计算到本地玩家的距离
        float distanceToLocalPlayer = GetDistanceToLocalPlayer();
        
        // 根据距离确定LOD级别
        LODLevel newLOD = CalculateLOD(distanceToLocalPlayer);
        
        if (newLOD != currentLOD)
        {
            ApplyLOD(newLOD);
            currentLOD = newLOD;
        }
    }
    
    private void ApplyLOD(LODLevel lod)
    {
        NetworkPlayerController playerController = GetComponent<NetworkPlayerController>();
        
        switch (lod)
        {
            case LODLevel.High:
                playerController.sendRate = 60f;
                break;
            case LODLevel.Medium:
                playerController.sendRate = 30f;
                break;
            case LODLevel.Low:
                playerController.sendRate = 15f;
                break;
            case LODLevel.Culled:
                playerController.sendRate = 0f;
                break;
        }
    }
}
```

### 5.2 数据压缩

#### NetworkDataCompressor (新建工具类)
```csharp
public static class NetworkDataCompressor
{
    // 压缩Vector3到16位精度
    public static ushort[] CompressVector3(Vector3 vector, float range = 1000f)
    {
        ushort[] compressed = new ushort[3];
        compressed[0] = (ushort)Mathf.Clamp((vector.x + range) / (2 * range) * 65535, 0, 65535);
        compressed[1] = (ushort)Mathf.Clamp((vector.y + range) / (2 * range) * 65535, 0, 65535);
        compressed[2] = (ushort)Mathf.Clamp((vector.z + range) / (2 * range) * 65535, 0, 65535);
        return compressed;
    }
    
    public static Vector3 DecompressVector3(ushort[] compressed, float range = 1000f)
    {
        Vector3 vector;
        vector.x = (compressed[0] / 65535f) * (2 * range) - range;
        vector.y = (compressed[1] / 65535f) * (2 * range) - range;
        vector.z = (compressed[2] / 65535f) * (2 * range) - range;
        return vector;
    }
    
    // 压缩四元数到最小3个分量
    public static byte[] CompressQuaternion(Quaternion quaternion)
    {
        // 找到最大的分量并省略它
        float maxValue = Mathf.Max(
            Mathf.Abs(quaternion.x),
            Mathf.Abs(quaternion.y),
            Mathf.Abs(quaternion.z),
            Mathf.Abs(quaternion.w)
        );
        
        byte maxIndex = 0;
        if (Mathf.Abs(quaternion.y) == maxValue) maxIndex = 1;
        else if (Mathf.Abs(quaternion.z) == maxValue) maxIndex = 2;
        else if (Mathf.Abs(quaternion.w) == maxValue) maxIndex = 3;
        
        byte[] compressed = new byte[7];
        compressed[0] = maxIndex;
        
        // 压缩其他3个分量
        int componentIndex = 1;
        for (int i = 0; i < 4; i++)
        {
            if (i == maxIndex) continue;
            
            float value = quaternion[i] / maxValue;
            ushort compressedValue = (ushort)((value + 1f) * 32767.5f);
            
            compressed[componentIndex++] = (byte)(compressedValue & 0xFF);
            compressed[componentIndex++] = (byte)(compressedValue >> 8);
        }
        
        return compressed;
    }
}
```

## 第六阶段：同步状态管理

### 6.1 游戏状态同步

#### NetworkGameManager (新建组件)
```csharp
public class NetworkGameManager : MonoBehaviourPunPV, IPunObservable
{
    [Header("游戏状态")]
    public enum GameState
    {
        Waiting,
        Starting,
        Playing,
        Paused,
        Ended
    }
    
    public GameState currentState = GameState.Waiting;
    public float gameTime = 0f;
    public int playersReady = 0;
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext((int)currentState);
            stream.SendNext(gameTime);
            stream.SendNext(playersReady);
        }
        else
        {
            currentState = (GameState)stream.ReceiveNext();
            gameTime = (float)stream.ReceiveNext();
            playersReady = (int)stream.ReceiveNext();
        }
    }
    
    [PunRPC]
    public void StartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            currentState = GameState.Playing;
            gameTime = 0f;
        }
    }
    
    [PunRPC]
    public void PlayerReady(int playerId)
    {
        playersReady++;
        
        if (playersReady >= PhotonNetwork.CurrentRoom.PlayerCount)
        {
            photonView.RPC("StartGame", RpcTarget.All);
        }
    }
}
```

## 第七阶段：实现清单和优先级

### 高优先级 (立即实现)
1. ✅ 基础PUN2设置和房间管理
2. ✅ 玩家生成和基础移动同步
3. ✅ 重力方向同步
4. ✅ 相机方向同步
5. ✅ 基础输入同步

### 中优先级 (第二阶段)
1. ⏳ 射击系统网络化
2. ⏳ 物理预测和校正
3. ⏳ 动态重力源同步
4. ⏳ 性能优化 (LOD)
5. ⏳ 数据压缩

### 低优先级 (优化阶段)
1. ⏸️ 高级物理同步
2. ⏸️ 延迟补偿
3. ⏸️ 防作弊机制
4. ⏸️ 断线重连
5. ⏸️ 观察者模式

## 测试计划

### 单元测试
- 网络数据序列化/反序列化
- 预测算法准确性
- 重力场同步正确性

### 集成测试
- 多客户端移动同步
- 重力转换同步
- 射击命中检测

### 性能测试
- 网络带宽使用
- 帧率影响测试
- 延迟模拟测试

## 部署注意事项

### 网络设置
- 推荐使用Photon Cloud欧洲服务器
- 配置合适的发送频率 (30-60Hz)
- 启用数据压缩
- 设置合理的超时时间

### 调试工具
- PUN2 Statistics GUI
- 网络延迟模拟器
- 丢包率测试工具
- 实时带宽监控

---

## 总结

该实现方案考虑了GravityShoot项目的独特重力机制和物理系统，提供了完整的网络同步解决方案。重点关注了：

1. **重力系统的网络同步** - 确保所有客户端的重力环境一致
2. **物理预测** - 减少网络延迟对游戏体验的影响
3. **性能优化** - 通过LOD和数据压缩保证流畅运行
4. **可扩展性** - 模块化设计便于后续功能扩展

建议按照优先级逐步实现，先确保基础功能稳定运行，再添加高级特性。
