# GravityShoot - 基于物理的PUN2网络同步设计文档

## 项目概述

本文档详细设计了GravityShoot项目的PUN2网络同步架构，重点关注：
1. **可操作游戏场景的匹配大厅** - 玩家在游戏世界中通过物理交互进行房间匹配
2. **基于物理的网络同步** - 所有游戏逻辑基于物理碰撞和交互
3. **重力系统网络化** - 复杂重力场的多客户端同步

---

## 核心设计理念

### 1. 物理优先的网络架构
- 所有游戏交互基于Physics系统
- 子弹、重力场、玩家移动均使用物理模拟
- 网络同步保持物理状态的一致性

### 2. 沉浸式匹配体验
- 匹配大厅即游戏场景
- 玩家通过物理交互（碰撞、触发器）进行房间操作
- 无传统UI，全部基于3D交互

### 3. 权威性物理同步
- Master Client作为物理权威
- 客户端预测 + 服务器校正
- 重要物理事件的强制同步

---

## 系统架构设计

### 核心网络管理器

#### NetworkGameManager
```csharp
public class NetworkGameManager : MonoBehaviourPunPV, IPunObservable
{
    [Header("游戏状态管理")]
    public GamePhase currentPhase = GamePhase.Lobby;
    public int maxPlayersPerRoom = 8;
    public float lobbyTimeout = 300f;
    
    [Header("物理权威")]
    public bool isMasterClientAuthority = true;
    public float physicsUpdateRate = 60f;
    
    public enum GamePhase
    {
        Lobby,          // 大厅阶段 - 玩家自由探索
        Matching,       // 匹配阶段 - 等待玩家
        PreGame,        // 游戏准备
        InGame,         // 游戏进行中
        PostGame        // 游戏结束
    }
    
    // 房间状态同步
    private Dictionary<string, RoomInstanceData> activeRooms;
    private Queue<PhysicsEventData> physicsEventQueue;
    
    public struct RoomInstanceData
    {
        public string roomId;
        public Vector3 roomPortalPosition;
        public int currentPlayers;
        public int maxPlayers;
        public GamePhase phase;
        public float elapsedTime;
    }
    
    // 网络同步方法
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info);
    public void CreatePhysicsRoom(Vector3 portalPosition);
    public void JoinPhysicsRoom(string roomId, NetworkPlayer player);
}
```

### 物理交互式匹配大厅

#### LobbyPhysicsManager
```csharp
public class LobbyPhysicsManager : MonoBehaviourPunPV
{
    [Header("大厅物理配置")]
    public Transform[] roomPortalPositions;
    public GameObject roomPortalPrefab;
    public LayerMask playerLayer;
    public LayerMask interactionLayer;
    
    [Header("房间创建区域")]
    public BoxCollider roomCreationZone;
    public ParticleSystem creationEffect;
    
    private Dictionary<string, RoomPortal> spawnedPortals;
    private List<LobbyInteractionObject> interactionObjects;
    
    // 房间门户管理
    public class RoomPortal : MonoBehaviourPunPV
    {
        public string roomId;
        public int playerCount;
        public int maxPlayers;
        public RoomState state;
        
        [Header("视觉反馈")]
        public Material[] stateMaterials; // 不同状态的材质
        public ParticleSystem portalEffect;
        public AudioSource portalAudio;
        
        public enum RoomState
        {
            Available,    // 可加入 - 绿色
            Joining,      // 加入中 - 黄色
            Full,         // 已满 - 红色
            InProgress    // 游戏中 - 蓝色
        }
        
        void OnTriggerEnter(Collider other)
        {
            NetworkPlayer player = other.GetComponent<NetworkPlayer>();
            if (player != null && player.photonView.IsMine)
            {
                TryJoinRoom(player);
            }
        }
        
        [PunRPC]
        void TryJoinRoom(NetworkPlayer player);
        void UpdatePortalVisuals();
        void PlayJoinEffect();
    }
    
    // 交互对象基类
    public abstract class LobbyInteractionObject : MonoBehaviourPunPV
    {
        [Header("物理交互")]
        public InteractionType interactionType;
        public float interactionCooldown = 1f;
        public UnityEvent OnInteractionStart;
        public UnityEvent OnInteractionComplete;
        
        public enum InteractionType
        {
            Collision,    // 碰撞触发
            Trigger,      // 触发器
            Proximity,    // 接近触发
            Physics       // 物理操作（推、拉等）
        }
        
        protected abstract void ExecuteInteraction(NetworkPlayer player);
        protected virtual void ShowInteractionFeedback();
    }
}
```

#### 具体交互对象实现

##### RoomCreationPlatform（房间创建平台）
```csharp
public class RoomCreationPlatform : LobbyInteractionObject
{
    [Header("房间创建")]
    public float activationForce = 50f;
    public float platformDepression = 0.5f;
    public GameObject hologramPrefab;
    
    private Rigidbody platformRb;
    private Vector3 originalPosition;
    private bool isActivated = false;
    
    protected override void ExecuteInteraction(NetworkPlayer player)
    {
        if (!isActivated && photonView.IsMine)
        {
            // 玩家跳到平台上，产生足够的力
            if (CalculateImpactForce(player) >= activationForce)
            {
                photonView.RPC("ActivateRoomCreation", RpcTarget.All, player.photonView.ViewID);
            }
        }
    }
    
    [PunRPC]
    void ActivateRoomCreation(int playerId)
    {
        // 创建新房间门户
        Vector3 portalPos = transform.position + Vector3.up * 2f;
        NetworkGameManager.Instance.CreatePhysicsRoom(portalPos);
        
        // 播放创建效果
        ShowCreationEffect();
    }
    
    private float CalculateImpactForce(NetworkPlayer player);
    private void ShowCreationEffect();
}
```

##### GravityRoomSelector（重力房间选择器）
```csharp
public class GravityRoomSelector : LobbyInteractionObject
{
    [Header("重力房间配置")]
    public GravityConfiguration[] availableConfigs;
    public Transform selectorKnob;
    public float rotationSpeed = 45f;
    
    [System.Serializable]
    public struct GravityConfiguration
    {
        public string configName;
        public Vector3 gravityDirection;
        public float gravityStrength;
        public GravityType gravityType;
        public Material visualMaterial;
    }
    
    public enum GravityType
    {
        Uniform,     // 统一重力
        Spherical,   // 球形重力
        Multiple,    // 多重力源
        Shifting     // 动态变化
    }
    
    private int currentConfigIndex = 0;
    private Quaternion targetRotation;
    
    protected override void ExecuteInteraction(NetworkPlayer player)
    {
        // 玩家推动选择器旋钮
        Vector3 pushDirection = (transform.position - player.transform.position).normalized;
        float pushForce = Vector3.Dot(player.GetComponent<RBPlayerMotor>().Velocity, pushDirection);
        
        if (pushForce > 10f) // 足够的推力
        {
            photonView.RPC("RotateSelector", RpcTarget.All, pushDirection);
        }
    }
    
    [PunRPC]
    void RotateSelector(Vector3 direction)
    {
        currentConfigIndex = (currentConfigIndex + 1) % availableConfigs.Length;
        targetRotation = Quaternion.Euler(0, currentConfigIndex * 45f, 0);
        UpdateVisualFeedback();
    }
    
    void UpdateVisualFeedback()
    {
        // 更新重力方向的视觉指示
        GravityConfiguration config = availableConfigs[currentConfigIndex];
        GetComponent<Renderer>().material = config.visualMaterial;
    }
    
    public GravityConfiguration GetSelectedConfig()
    {
        return availableConfigs[currentConfigIndex];
    }
}
```

### 基于物理的玩家网络系统

#### NetworkPhysicsPlayer
```csharp
public class NetworkPhysicsPlayer : MonoBehaviourPunPV, IPunObservable
{
    [Header("物理网络同步")]
    public float positionThreshold = 0.1f;
    public float velocityThreshold = 0.5f;
    public float rotationThreshold = 1f;
    
    [Header("预测和校正")]
    public bool enableClientPrediction = true;
    public int maxPredictionFrames = 60;
    public float reconciliationThreshold = 0.5f;
    
    [Header("物理权威")]
    public bool useServerAuthority = true;
    public float authorityDistance = 20f; // 超过此距离使用服务器权威
    
    // 网络状态数据
    private struct NetworkPhysicsState
    {
        public Vector3 position;
        public Vector3 velocity;
        public Quaternion rotation;
        public Vector3 angularVelocity;
        public Vector3 gravityDirection;
        public float timestamp;
        public bool isGrounded;
        public int frameNumber;
    }
    
    private NetworkPhysicsState networkState;
    private Queue<NetworkPhysicsState> stateHistory;
    private Queue<NetworkPhysicsState> predictionHistory;
    
    // 组件引用
    private RBPlayerMotor playerMotor;
    private FPSGravityCamera gravityCamera;
    private Rigidbody playerRb;
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 发送物理状态
            WritePhysicsState(stream);
        }
        else
        {
            // 接收物理状态
            ReadPhysicsState(stream, info);
        }
    }
    
    private void WritePhysicsState(PhotonStream stream)
    {
        // 压缩位置数据
        Vector3 compressedPos = CompressPosition(transform.position);
        stream.SendNext(compressedPos);
        
        // 发送速度（使用动态精度）
        Vector3 velocity = playerRb.velocity;
        if (velocity.magnitude > velocityThreshold)
        {
            stream.SendNext(velocity);
        }
        else
        {
            stream.SendNext(Vector3.zero);
        }
        
        // 旋转数据
        stream.SendNext(transform.rotation);
        stream.SendNext(gravityCamera.transform.rotation);
        
        // 重力状态
        Vector3 gravity = CustomGravity.GetGravity(transform.position);
        stream.SendNext(gravity.normalized);
        
        // 物理状态标志
        stream.SendNext(playerMotor.IsGrounded);
        stream.SendNext(playerMotor.IsInGravityField);
        
        // 时间戳
        stream.SendNext(Time.fixedTime);
    }
    
    private void ReadPhysicsState(PhotonStream stream, PhotonMessageInfo info)
    {
        networkState.position = (Vector3)stream.ReceiveNext();
        networkState.velocity = (Vector3)stream.ReceiveNext();
        networkState.rotation = (Quaternion)stream.ReceiveNext();
        Quaternion cameraRotation = (Quaternion)stream.ReceiveNext();
        networkState.gravityDirection = (Vector3)stream.ReceiveNext();
        networkState.isGrounded = (bool)stream.ReceiveNext();
        bool isInGravityField = (bool)stream.ReceiveNext();
        networkState.timestamp = (float)stream.ReceiveNext();
        
        // 应用网络状态
        if (!photonView.IsMine)
        {
            ApplyNetworkPhysicsState(info);
        }
    }
    
    private void ApplyNetworkPhysicsState(PhotonMessageInfo info)
    {
        // 计算网络延迟补偿
        float timeDifference = (float)(PhotonNetwork.Time - info.SentServerTime);
        Vector3 predictedPosition = networkState.position + networkState.velocity * timeDifference;
        
        // 距离检查 - 决定使用插值还是瞬移
        float distance = Vector3.Distance(transform.position, predictedPosition);
        
        if (distance > reconciliationThreshold)
        {
            // 距离过大，直接设置位置
            transform.position = predictedPosition;
            playerRb.velocity = networkState.velocity;
        }
        else
        {
            // 平滑插值
            float lerpRate = Time.deltaTime * 15f;
            transform.position = Vector3.Lerp(transform.position, predictedPosition, lerpRate);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkState.rotation, lerpRate);
            
            // 速度混合
            playerRb.velocity = Vector3.Lerp(playerRb.velocity, networkState.velocity, lerpRate);
        }
        
        // 应用重力方向
        if (Vector3.Angle(playerMotor.CurrentGravityDirection, networkState.gravityDirection) > 5f)
        {
            playerMotor.SetNetworkGravityDirection(networkState.gravityDirection);
        }
    }
    
    // 客户端预测
    private void RecordPredictionState()
    {
        if (!photonView.IsMine) return;
        
        NetworkPhysicsState state = new NetworkPhysicsState
        {
            position = transform.position,
            velocity = playerRb.velocity,
            rotation = transform.rotation,
            gravityDirection = playerMotor.CurrentGravityDirection,
            timestamp = Time.fixedTime,
            frameNumber = Time.renderedFrameCount,
            isGrounded = playerMotor.IsGrounded
        };
        
        predictionHistory.Enqueue(state);
        
        // 限制历史长度
        while (predictionHistory.Count > maxPredictionFrames)
        {
            predictionHistory.Dequeue();
        }
    }
    
    // 服务器校正
    public void ServerReconciliation(Vector3 serverPosition, float serverTime)
    {
        if (!photonView.IsMine) return;
        
        // 找到对应时间的预测状态
        NetworkPhysicsState? matchingState = FindPredictionState(serverTime);
        
        if (matchingState.HasValue)
        {
            float positionError = Vector3.Distance(matchingState.Value.position, serverPosition);
            
            if (positionError > reconciliationThreshold)
            {
                // 需要校正 - 重新应用输入
                transform.position = serverPosition;
                ReplayInputsFromFrame(matchingState.Value.frameNumber);
            }
        }
    }
    
    private Vector3 CompressPosition(Vector3 position);
    private NetworkPhysicsState? FindPredictionState(float timestamp);
    private void ReplayInputsFromFrame(int frameNumber);
}
```

### 物理子弹系统

#### NetworkPhysicsBullet
```csharp
public class NetworkPhysicsBullet : MonoBehaviourPunPV, IPunObservable
{
    [Header("子弹物理配置")]
    public float damage = 25f;
    public float lifeTime = 5f;
    public float maxSpeed = 100f;
    public LayerMask hitLayers;
    
    [Header("网络同步")]
    public bool useServerAuthority = true;
    public float syncRate = 30f;
    
    // 子弹状态
    private struct BulletState
    {
        public Vector3 position;
        public Vector3 velocity;
        public Quaternion rotation;
        public float timeAlive;
        public bool isActive;
    }
    
    private BulletState networkState;
    private Rigidbody bulletRb;
    private TrailRenderer trail;
    private bool hasHit = false;
    private int ownerPlayerId;
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // 发送子弹状态
            stream.SendNext(transform.position);
            stream.SendNext(bulletRb.velocity);
            stream.SendNext(transform.rotation);
            stream.SendNext(Time.time - spawnTime);
            stream.SendNext(!hasHit);
        }
        else
        {
            // 接收子弹状态
            networkState.position = (Vector3)stream.ReceiveNext();
            networkState.velocity = (Vector3)stream.ReceiveNext();
            networkState.rotation = (Quaternion)stream.ReceiveNext();
            networkState.timeAlive = (float)stream.ReceiveNext();
            networkState.isActive = (bool)stream.ReceiveNext();
            
            // 应用网络状态（仅非拥有者）
            if (!photonView.IsMine)
            {
                ApplyNetworkState(info);
            }
        }
    }
    
    private void ApplyNetworkState(PhotonMessageInfo info)
    {
        // 延迟补偿
        float timeDiff = (float)(PhotonNetwork.Time - info.SentServerTime);
        Vector3 predictedPos = networkState.position + networkState.velocity * timeDiff;
        
        // 平滑插值
        transform.position = Vector3.Lerp(transform.position, predictedPos, Time.deltaTime * syncRate);
        transform.rotation = Quaternion.Lerp(transform.rotation, networkState.rotation, Time.deltaTime * syncRate);
        bulletRb.velocity = networkState.velocity;
        
        // 检查子弹是否应该销毁
        if (!networkState.isActive && !hasHit)
        {
            OnBulletHit(Vector3.zero, null);
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        
        // 只有拥有者处理碰撞
        if (!photonView.IsMine) return;
        
        // 检查命中目标
        if (IsValidTarget(other))
        {
            ProcessHit(other);
        }
    }
    
    private bool IsValidTarget(Collider target)
    {
        // 不命中自己
        NetworkPhysicsPlayer hitPlayer = target.GetComponent<NetworkPhysicsPlayer>();
        if (hitPlayer != null && hitPlayer.photonView.ViewID == ownerPlayerId)
            return false;
        
        // 检查层级
        return ((1 << target.gameObject.layer) & hitLayers) != 0;
    }
    
    private void ProcessHit(Collider target)
    {
        hasHit = true;
        Vector3 hitPoint = GetClosestPoint(target);
        Vector3 hitNormal = GetHitNormal(target, hitPoint);
        
        // 通过RPC同步命中事件
        photonView.RPC("OnBulletHit", RpcTarget.All, hitPoint, hitNormal, target.transform.position);
        
        // 处理伤害
        NetworkPhysicsPlayer hitPlayer = target.GetComponent<NetworkPhysicsPlayer>();
        if (hitPlayer != null)
        {
            hitPlayer.photonView.RPC("TakeDamage", RpcTarget.All, damage, ownerPlayerId, hitPoint);
        }
        
        // 物理交互
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            Vector3 force = bulletRb.velocity.normalized * damage * 10f;
            targetRb.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);
        }
    }
    
    [PunRPC]
    void OnBulletHit(Vector3 hitPoint, Vector3 hitNormal, Vector3 targetPosition)
    {
        hasHit = true;
        
        // 播放命中效果
        PlayHitEffect(hitPoint, hitNormal);
        
        // 停止子弹移动
        bulletRb.velocity = Vector3.zero;
        bulletRb.isKinematic = true;
        
        // 延迟销毁
        StartCoroutine(DestroyAfterDelay(2f));
    }
    
    private Vector3 GetClosestPoint(Collider target);
    private Vector3 GetHitNormal(Collider target, Vector3 point);
    private void PlayHitEffect(Vector3 point, Vector3 normal);
    private IEnumerator DestroyAfterDelay(float delay);
    
    // 子弹初始化
    public void InitializeBullet(Vector3 direction, float speed, int shooterId)
    {
        ownerPlayerId = shooterId;
        bulletRb.velocity = direction.normalized * speed;
        spawnTime = Time.time;
        
        // 应用重力影响
        CustomGravity.ApplyGravityToBullet(bulletRb);
    }
}
```

### 武器系统网络化

#### NetworkPhysicsWeapon
```csharp
public class NetworkPhysicsWeapon : MonoBehaviourPunPV
{
    [Header("武器配置")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float bulletSpeed = 50f;
    public float fireRate = 600f; // RPM
    public int magazineSize = 30;
    public float reloadTime = 2f;
    
    [Header("物理反冲")]
    public float recoilForce = 10f;
    public Vector3 recoilPattern = new Vector3(0, 0.5f, -1f);
    public float recoilRecoverySpeed = 5f;
    
    [Header("网络同步")]
    public bool predictiveShootin = true;
    public float bulletSyncRate = 60f;
    
    private float lastFireTime;
    private int currentAmmo;
    private bool isReloading;
    private Queue<FireCommand> fireCommandQueue;
    
    private struct FireCommand
    {
        public Vector3 origin;
        public Vector3 direction;
        public float timestamp;
        public int commandId;
    }
    
    private NetworkPhysicsPlayer playerController;
    private FPSGravityCamera gravityCamera;
    
    void Start()
    {
        playerController = GetComponentInParent<NetworkPhysicsPlayer>();
        gravityCamera = GetComponentInParent<FPSGravityCamera>();
        currentAmmo = magazineSize;
    }
    
    public void TryFire()
    {
        if (!photonView.IsMine) return;
        if (isReloading || currentAmmo <= 0) return;
        
        float timeSinceLastFire = Time.time - lastFireTime;
        float fireInterval = 60f / fireRate;
        
        if (timeSinceLastFire >= fireInterval)
        {
            Fire();
        }
    }
    
    private void Fire()
    {
        Vector3 fireOrigin = firePoint.position;
        Vector3 fireDirection = CalculateFireDirection();
        
        // 立即执行本地射击（预测性）
        if (predictiveShootin)
        {
            SpawnBulletLocal(fireOrigin, fireDirection);
        }
        
        // 同步到网络
        int commandId = GenerateCommandId();
        photonView.RPC("NetworkFire", RpcTarget.Others, fireOrigin, fireDirection, Time.time, commandId);
        
        // 消耗弹药
        currentAmmo--;
        lastFireTime = Time.time;
        
        // 应用后坐力
        ApplyRecoil();
        
        // 播放射击效果
        PlayFireEffects();
    }
    
    [PunRPC]
    void NetworkFire(Vector3 origin, Vector3 direction, float timestamp, int commandId)
    {
        // 延迟补偿
        float currentTime = Time.time;
        float timeDiff = currentTime - timestamp;
        
        // 调整射击位置（补偿网络延迟）
        Vector3 compensatedOrigin = origin + direction * bulletSpeed * timeDiff;
        
        // 生成网络子弹
        SpawnBulletNetwork(compensatedOrigin, direction, commandId);
        
        // 播放远程射击效果
        PlayRemoteFireEffects(origin);
    }
    
    private void SpawnBulletLocal(Vector3 origin, Vector3 direction)
    {
        GameObject bullet = PhotonNetwork.Instantiate(bulletPrefab.name, origin, Quaternion.LookRotation(direction));
        NetworkPhysicsBullet bulletScript = bullet.GetComponent<NetworkPhysicsBullet>();
        bulletScript.InitializeBullet(direction, bulletSpeed, photonView.ViewID);
    }
    
    private void SpawnBulletNetwork(Vector3 origin, Vector3 direction, int commandId)
    {
        // 为网络子弹创建临时物理对象
        GameObject tempBullet = Instantiate(bulletPrefab, origin, Quaternion.LookRotation(direction));
        
        // 配置为仅视觉效果，不参与碰撞检测
        Rigidbody rb = tempBullet.GetComponent<Rigidbody>();
        rb.velocity = direction * bulletSpeed;
        
        // 添加重力影响
        CustomGravity.ApplyGravityToBullet(rb);
        
        // 设置销毁时间
        Destroy(tempBullet, 5f);
    }
    
    private Vector3 CalculateFireDirection()
    {
        // 基于相机方向计算射击方向
        Vector3 baseDirection = gravityCamera.transform.forward;
        
        // 添加随机散布
        Vector3 spread = Random.insideUnitCircle * 0.02f; // 2度散布
        Vector3 up = gravityCamera.transform.up;
        Vector3 right = gravityCamera.transform.right;
        
        Vector3 finalDirection = baseDirection + up * spread.y + right * spread.x;
        return finalDirection.normalized;
    }
    
    private void ApplyRecoil()
    {
        if (!photonView.IsMine) return;
        
        // 对玩家施加后坐力
        Rigidbody playerRb = playerController.GetComponent<Rigidbody>();
        Vector3 recoilVector = -gravityCamera.transform.forward * recoilForce;
        
        // 考虑重力方向的后坐力
        Vector3 gravityDirection = playerController.GetComponent<RBPlayerMotor>().CurrentGravityDirection;
        recoilVector = ApplyGravityToRecoil(recoilVector, gravityDirection);
        
        playerRb.AddForce(recoilVector, ForceMode.Impulse);
        
        // 相机后坐力
        gravityCamera.AddRecoil(recoilPattern);
    }
    
    public void TryReload()
    {
        if (!photonView.IsMine || isReloading || currentAmmo == magazineSize) return;
        
        StartCoroutine(ReloadCoroutine());
        photonView.RPC("PlayReloadAnimation", RpcTarget.Others);
    }
    
    private IEnumerator ReloadCoroutine()
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = magazineSize;
        isReloading = false;
    }
    
    [PunRPC]
    void PlayReloadAnimation()
    {
        // 播放换弹动画和音效
    }
    
    private Vector3 ApplyGravityToRecoil(Vector3 recoil, Vector3 gravityDir);
    private int GenerateCommandId();
    private void PlayFireEffects();
    private void PlayRemoteFireEffects(Vector3 origin);
}
```

### 重力场网络同步

#### NetworkGravityField
```csharp
public class NetworkGravityField : MonoBehaviourPunPV, IPunObservable
{
    [Header("重力场配置")]
    public GravitySource gravitySource;
    public bool syncDynamicChanges = true;
    public float syncUpdateRate = 10f;
    
    [Header("物理影响")]
    public bool affectBullets = true;
    public bool affectPlayers = true;
    public bool affectObjects = true;
    public LayerMask affectedLayers;
    
    [Header("网络优化")]
    public float changeThreshold = 0.1f;
    public bool useAreaOfInterest = true;
    public float maxSyncDistance = 100f;
    
    private struct GravityFieldState
    {
        public Vector3 position;
        public Vector3 gravityDirection;
        public float gravityStrength;
        public float range;
        public bool isActive;
        public GravityType type;
    }
    
    private GravityFieldState networkState;
    private GravityFieldState lastSentState;
    private List<NetworkPhysicsPlayer> affectedPlayers;
    private List<NetworkPhysicsBullet> affectedBullets;
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            WriteGravityState(stream);
        }
        else
        {
            ReadGravityState(stream, info);
        }
    }
    
    private void WriteGravityState(PhotonStream stream)
    {
        // 只在状态发生显著变化时发送
        if (HasSignificantChange())
        {
            stream.SendNext(transform.position);
            stream.SendNext(gravitySource.GetGravityDirection());
            stream.SendNext(gravitySource.GetGravityStrength());
            stream.SendNext(gravitySource.GetRange());
            stream.SendNext(gravitySource.enabled);
            stream.SendNext((int)gravitySource.GetGravityType());
            
            lastSentState = GetCurrentState();
        }
    }
    
    private void ReadGravityState(PhotonStream stream, PhotonMessageInfo info)
    {
        networkState.position = (Vector3)stream.ReceiveNext();
        networkState.gravityDirection = (Vector3)stream.ReceiveNext();
        networkState.gravityStrength = (float)stream.ReceiveNext();
        networkState.range = (float)stream.ReceiveNext();
        networkState.isActive = (bool)stream.ReceiveNext();
        networkState.type = (GravityType)stream.ReceiveNext();
        
        // 应用网络状态
        if (!photonView.IsMine)
        {
            ApplyNetworkGravityState();
        }
    }
    
    private void ApplyNetworkGravityState()
    {
        // 平滑更新重力场参数
        float lerpRate = Time.deltaTime * syncUpdateRate;
        
        transform.position = Vector3.Lerp(transform.position, networkState.position, lerpRate);
        gravitySource.SetGravityDirection(Vector3.Slerp(gravitySource.GetGravityDirection(), networkState.gravityDirection, lerpRate));
        gravitySource.SetGravityStrength(Mathf.Lerp(gravitySource.GetGravityStrength(), networkState.gravityStrength, lerpRate));
        gravitySource.SetRange(Mathf.Lerp(gravitySource.GetRange(), networkState.range, lerpRate));
        gravitySource.enabled = networkState.isActive;
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (!photonView.IsMine) return;
        
        // 玩家进入重力场
        NetworkPhysicsPlayer player = other.GetComponent<NetworkPhysicsPlayer>();
        if (player != null && affectPlayers)
        {
            photonView.RPC("PlayerEnterGravityField", RpcTarget.All, player.photonView.ViewID);
            affectedPlayers.Add(player);
        }
        
        // 子弹进入重力场
        NetworkPhysicsBullet bullet = other.GetComponent<NetworkPhysicsBullet>();
        if (bullet != null && affectBullets)
        {
            ApplyGravityToBullet(bullet);
            affectedBullets.Add(bullet);
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (!photonView.IsMine) return;
        
        NetworkPhysicsPlayer player = other.GetComponent<NetworkPhysicsPlayer>();
        if (player != null)
        {
            photonView.RPC("PlayerExitGravityField", RpcTarget.All, player.photonView.ViewID);
            affectedPlayers.Remove(player);
        }
        
        NetworkPhysicsBullet bullet = other.GetComponent<NetworkPhysicsBullet>();
        if (bullet != null)
        {
            affectedBullets.Remove(bullet);
        }
    }
    
    [PunRPC]
    void PlayerEnterGravityField(int playerId)
    {
        PhotonView playerView = PhotonView.Find(playerId);
        if (playerView != null)
        {
            NetworkPhysicsPlayer player = playerView.GetComponent<NetworkPhysicsPlayer>();
            player.EnterGravityField(this);
            
            // 播放进入效果
            PlayFieldEnterEffect(player.transform.position);
        }
    }
    
    [PunRPC]
    void PlayerExitGravityField(int playerId)
    {
        PhotonView playerView = PhotonView.Find(playerId);
        if (playerView != null)
        {
            NetworkPhysicsPlayer player = playerView.GetComponent<NetworkPhysicsPlayer>();
            player.ExitGravityField(this);
        }
    }
    
    void FixedUpdate()
    {
        if (!photonView.IsMine) return;
        
        // 持续应用重力影响
        ApplyGravityToAffectedObjects();
    }
    
    private void ApplyGravityToAffectedObjects()
    {
        // 对受影响的子弹施加重力
        foreach (var bullet in affectedBullets.ToArray())
        {
            if (bullet == null)
            {
                affectedBullets.Remove(bullet);
                continue;
            }
            
            ApplyGravityToBullet(bullet);
        }
        
        // 对受影响的物理对象施加重力
        ApplyGravityToPhysicsObjects();
    }
    
    private void ApplyGravityToBullet(NetworkPhysicsBullet bullet)
    {
        Vector3 gravityForce = CalculateGravityForce(bullet.transform.position);
        bullet.GetComponent<Rigidbody>().AddForce(gravityForce, ForceMode.Acceleration);
    }
    
    private void ApplyGravityToPhysicsObjects()
    {
        Collider[] objects = Physics.OverlapSphere(transform.position, gravitySource.GetRange(), affectedLayers);
        
        foreach (var obj in objects)
        {
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                Vector3 gravityForce = CalculateGravityForce(obj.transform.position);
                rb.AddForce(gravityForce, ForceMode.Acceleration);
            }
        }
    }
    
    private Vector3 CalculateGravityForce(Vector3 position)
    {
        return gravitySource.GetGravity(position);
    }
    
    private bool HasSignificantChange()
    {
        GravityFieldState current = GetCurrentState();
        
        return Vector3.Distance(current.position, lastSentState.position) > changeThreshold ||
               Vector3.Angle(current.gravityDirection, lastSentState.gravityDirection) > changeThreshold ||
               Mathf.Abs(current.gravityStrength - lastSentState.gravityStrength) > changeThreshold ||
               current.isActive != lastSentState.isActive;
    }
    
    private GravityFieldState GetCurrentState()
    {
        return new GravityFieldState
        {
            position = transform.position,
            gravityDirection = gravitySource.GetGravityDirection(),
            gravityStrength = gravitySource.GetGravityStrength(),
            range = gravitySource.GetRange(),
            isActive = gravitySource.enabled,
            type = gravitySource.GetGravityType()
        };
    }
    
    private void PlayFieldEnterEffect(Vector3 position);
}
```

---

## 核心交互场景设计

### 1. 大厅主区域 (Main Lobby Area)

#### 区域布局
```
大厅中央平台
├── 房间创建平台 (RoomCreationPlatform) - 玩家跳跃激活
├── 重力配置选择器 (GravityRoomSelector) - 物理旋转选择
├── 玩家计数显示台 - 显示在线玩家数
└── 传送门生成区域 - 动态生成房间门户

环形走廊
├── 房间门户区域 - 显示可用房间
├── 设置调整站 - 音量、画质等物理滑块
├── 教程传送门 - 单人练习模式
└── 重力体验区 - 预览不同重力效果
```

#### 物理交互元素

##### 用户名设置台 (UsernameInputStation)
```csharp
public class UsernameInputStation : LobbyInteractionObject
{
    [Header("用户名输入")]
    public Transform[] letterCubes; // 26个字母方块
    public Transform[] namePlates;  // 显示当前用户名的板子
    public int maxNameLength = 12;
    
    private Queue<char> selectedLetters;
    private string currentUsername;
    
    protected override void ExecuteInteraction(NetworkPlayer player)
    {
        // 玩家推动字母方块来拼写用户名
        LetterCube letter = GetLetterCubeFromCollision(player);
        if (letter != null)
        {
            AddLetterToName(letter.letter);
            UpdateNameDisplay();
        }
    }
    
    private void AddLetterToName(char letter)
    {
        if (selectedLetters.Count >= maxNameLength) return;
        
        selectedLetters.Enqueue(letter);
        currentUsername = new string(selectedLetters.ToArray());
        
        // 应用用户名
        if (selectedLetters.Count >= 3) // 最少3个字符
        {
            PhotonNetwork.LocalPlayer.NickName = currentUsername;
        }
    }
    
    // 删除按钮 - 玩家推倒特殊方块
    public void RemoveLastLetter()
    {
        if (selectedLetters.Count > 0)
        {
            selectedLetters = new Queue<char>(selectedLetters.Take(selectedLetters.Count - 1));
            currentUsername = new string(selectedLetters.ToArray());
            UpdateNameDisplay();
        }
    }
}
```

##### 重力体验区 (GravityPreviewZone)
```csharp
public class GravityPreviewZone : MonoBehaviourPunPV
{
    [Header("重力预览")]
    public GravityConfiguration[] previewConfigs;
    public float cycleInterval = 10f;
    public ParticleSystem gravityVisualizer;
    
    private int currentPreviewIndex = 0;
    private float lastCycleTime;
    
    void Update()
    {
        if (Time.time - lastCycleTime > cycleInterval)
        {
            CycleToNextGravityPreview();
            lastCycleTime = Time.time;
        }
    }
    
    void OnTriggerStay(Collider other)
    {
        NetworkPhysicsPlayer player = other.GetComponent<NetworkPhysicsPlayer>();
        if (player != null)
        {
            // 让玩家体验当前重力配置
            ApplyPreviewGravity(player);
        }
    }
    
    private void CycleToNextGravityPreview()
    {
        currentPreviewIndex = (currentPreviewIndex + 1) % previewConfigs.Length;
        UpdateGravityVisualizer();
        
        // 同步到所有客户端
        photonView.RPC("SyncGravityPreview", RpcTarget.Others, currentPreviewIndex);
    }
    
    [PunRPC]
    void SyncGravityPreview(int index)
    {
        currentPreviewIndex = index;
        UpdateGravityVisualizer();
    }
    
    private void ApplyPreviewGravity(NetworkPhysicsPlayer player);
    private void UpdateGravityVisualizer();
}
```

### 2. 游戏房间场景

#### 动态重力竞技场
```csharp
public class NetworkGravityArena : MonoBehaviourPunPV
{
    [Header("竞技场配置")]
    public Transform[] spawnPoints;
    public NetworkGravityField[] gravityFields;
    public float matchDuration = 300f; // 5分钟
    
    [Header("动态元素")]
    public GameObject[] movingPlatforms;
    public NetworkPhysicsObject[] destructibleObjects;
    
    private float matchStartTime;
    private GamePhase currentPhase;
    
    public enum GamePhase
    {
        Warmup,     // 热身阶段 - 无伤害
        Active,     // 激烈战斗
        Overtime,   // 加时赛 - 重力加强
        Ending      // 比赛结束
    }
    
    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            InitializeArena();
        }
    }
    
    private void InitializeArena()
    {
        // 随机激活重力场
        RandomizeGravityFields();
        
        // 启动动态平台
        ActivateMovingPlatforms();
        
        // 开始比赛倒计时
        matchStartTime = Time.time;
        photonView.RPC("StartMatch", RpcTarget.All, matchStartTime);
    }
    
    [PunRPC]
    void StartMatch(float startTime)
    {
        matchStartTime = startTime;
        currentPhase = GamePhase.Warmup;
        
        // 显示倒计时UI
        ShowMatchCountdown();
    }
    
    void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        float elapsedTime = Time.time - matchStartTime;
        
        // 阶段转换
        CheckPhaseTransition(elapsedTime);
        
        // 动态重力场变化
        UpdateDynamicGravityFields(elapsedTime);
    }
    
    private void CheckPhaseTransition(float elapsedTime)
    {
        GamePhase newPhase = currentPhase;
        
        if (elapsedTime > 30f && currentPhase == GamePhase.Warmup)
        {
            newPhase = GamePhase.Active;
        }
        else if (elapsedTime > matchDuration && currentPhase == GamePhase.Active)
        {
            newPhase = GamePhase.Overtime;
        }
        
        if (newPhase != currentPhase)
        {
            photonView.RPC("TransitionToPhase", RpcTarget.All, (int)newPhase);
        }
    }
    
    [PunRPC]
    void TransitionToPhase(int phaseIndex)
    {
        currentPhase = (GamePhase)phaseIndex;
        
        switch (currentPhase)
        {
            case GamePhase.Active:
                EnableDamage();
                break;
            case GamePhase.Overtime:
                BoostGravityFields();
                SpawnPowerups();
                break;
        }
    }
    
    private void UpdateDynamicGravityFields(float elapsedTime)
    {
        // 每30秒随机改变一个重力场
        if (Mathf.FloorToInt(elapsedTime / 30f) > Mathf.FloorToInt((elapsedTime - Time.deltaTime) / 30f))
        {
            RandomizeGravityFields();
        }
    }
    
    private void RandomizeGravityFields();
    private void ActivateMovingPlatforms();
    private void EnableDamage();
    private void BoostGravityFields();
    private void SpawnPowerups();
    private void ShowMatchCountdown();
}
```

---

## 性能优化策略

### 1. 物理同步优化

#### PhysicsLODManager
```csharp
public class PhysicsLODManager : MonoBehaviour
{
    [Header("LOD配置")]
    public float highDetailRange = 25f;
    public float mediumDetailRange = 50f;
    public float lowDetailRange = 100f;
    
    [Header("同步频率")]
    public float highDetailRate = 60f;
    public float mediumDetailRate = 30f;
    public float lowDetailRate = 15f;
    public float culledRate = 0f;
    
    private Dictionary<NetworkPhysicsPlayer, LODLevel> playerLODs;
    private NetworkPhysicsPlayer localPlayer;
    
    public enum LODLevel
    {
        High,    // 完整物理同步
        Medium,  // 降低频率
        Low,     // 仅位置同步
        Culled   // 不同步
    }
    
    void Update()
    {
        UpdateLODLevels();
    }
    
    private void UpdateLODLevels()
    {
        if (localPlayer == null) return;
        
        foreach (var player in NetworkPhysicsPlayer.AllPlayers)
        {
            if (player == localPlayer) continue;
            
            float distance = Vector3.Distance(localPlayer.transform.position, player.transform.position);
            LODLevel newLOD = CalculateLOD(distance);
            
            if (!playerLODs.ContainsKey(player) || playerLODs[player] != newLOD)
            {
                ApplyLOD(player, newLOD);
                playerLODs[player] = newLOD;
            }
        }
    }
    
    private LODLevel CalculateLOD(float distance)
    {
        if (distance <= highDetailRange) return LODLevel.High;
        if (distance <= mediumDetailRange) return LODLevel.Medium;
        if (distance <= lowDetailRange) return LODLevel.Low;
        return LODLevel.Culled;
    }
    
    private void ApplyLOD(NetworkPhysicsPlayer player, LODLevel lod)
    {
        PhotonTransformView transformView = player.GetComponent<PhotonTransformView>();
        
        switch (lod)
        {
            case LODLevel.High:
                transformView.enabled = true;
                transformView.m_SynchronizePosition = true;
                transformView.m_SynchronizeRotation = true;
                transformView.m_SynchronizeScale = false;
                break;
                
            case LODLevel.Medium:
                transformView.enabled = true;
                transformView.m_SynchronizePosition = true;
                transformView.m_SynchronizeRotation = false;
                break;
                
            case LODLevel.Low:
                transformView.enabled = true;
                transformView.m_SynchronizePosition = true;
                transformView.m_SynchronizeRotation = false;
                break;
                
            case LODLevel.Culled:
                transformView.enabled = false;
                break;
        }
    }
}
```

### 2. 数据压缩系统

#### PhysicsDataCompressor
```csharp
public static class PhysicsDataCompressor
{
    public static byte[] CompressVelocity(Vector3 velocity, float maxSpeed = 100f)
    {
        // 压缩速度向量到16位
        byte[] compressed = new byte[6];
        
        float magnitude = Mathf.Clamp(velocity.magnitude, 0f, maxSpeed);
        Vector3 direction = velocity.normalized;
        
        // 压缩方向（球坐标）
        float theta = Mathf.Atan2(direction.z, direction.x);
        float phi = Mathf.Acos(direction.y);
        
        ushort thetaCompressed = (ushort)((theta + Mathf.PI) / (2 * Mathf.PI) * 65535);
        ushort phiCompressed = (ushort)(phi / Mathf.PI * 65535);
        ushort magnitudeCompressed = (ushort)(magnitude / maxSpeed * 65535);
        
        compressed[0] = (byte)(thetaCompressed & 0xFF);
        compressed[1] = (byte)(thetaCompressed >> 8);
        compressed[2] = (byte)(phiCompressed & 0xFF);
        compressed[3] = (byte)(phiCompressed >> 8);
        compressed[4] = (byte)(magnitudeCompressed & 0xFF);
        compressed[5] = (byte)(magnitudeCompressed >> 8);
        
        return compressed;
    }
    
    public static Vector3 DecompressVelocity(byte[] compressed, float maxSpeed = 100f)
    {
        ushort thetaCompressed = (ushort)(compressed[0] | (compressed[1] << 8));
        ushort phiCompressed = (ushort)(compressed[2] | (compressed[3] << 8));
        ushort magnitudeCompressed = (ushort)(compressed[4] | (compressed[5] << 8));
        
        float theta = (thetaCompressed / 65535f) * (2 * Mathf.PI) - Mathf.PI;
        float phi = (phiCompressed / 65535f) * Mathf.PI;
        float magnitude = (magnitudeCompressed / 65535f) * maxSpeed;
        
        Vector3 direction = new Vector3(
            Mathf.Sin(phi) * Mathf.Cos(theta),
            Mathf.Cos(phi),
            Mathf.Sin(phi) * Mathf.Sin(theta)
        );
        
        return direction * magnitude;
    }
    
    public static byte[] CompressRotation(Quaternion rotation)
    {
        // 最小3分量压缩法
        byte[] compressed = new byte[7];
        
        float[] components = { rotation.x, rotation.y, rotation.z, rotation.w };
        float maxValue = 0f;
        int maxIndex = 0;
        
        for (int i = 0; i < 4; i++)
        {
            if (Mathf.Abs(components[i]) > maxValue)
            {
                maxValue = Mathf.Abs(components[i]);
                maxIndex = i;
            }
        }
        
        compressed[0] = (byte)maxIndex;
        
        int compIndex = 1;
        for (int i = 0; i < 4; i++)
        {
            if (i == maxIndex) continue;
            
            float normalized = components[i] / maxValue;
            ushort compressedValue = (ushort)((normalized + 1f) * 32767.5f);
            
            compressed[compIndex++] = (byte)(compressedValue & 0xFF);
            compressed[compIndex++] = (byte)(compressedValue >> 8);
        }
        
        return compressed;
    }
}
```

---

## 实现计划和优先级

### 第一阶段：基础网络架构 (1-2周)
1. **PUN2基础设置**
   - 安装PUN2包
   - 配置Photon应用设置
   - 创建基础网络管理器

2. **大厅场景实现**
   - 创建可操作的匹配大厅场景
   - 实现用户名设置台
   - 房间创建平台基础功能

3. **基础物理网络同步**
   - NetworkPhysicsPlayer基础版本
   - 简单的位置和旋转同步

### 第二阶段：物理交互系统 (2-3周)
1. **大厅交互对象**
   - 完整的房间门户系统
   - 重力配置选择器
   - 重力体验区

2. **物理权威系统**
   - 客户端预测实现
   - 服务器校正机制
   - 冲突解决策略

3. **重力场网络同步**
   - NetworkGravityField实现
   - 动态重力场同步
   - 重力转换插值

### 第三阶段：战斗系统 (2-3周)
1. **武器系统网络化**
   - NetworkPhysicsWeapon实现
   - 物理子弹同步
   - 命中检测和伤害系统

2. **竞技场系统**
   - 动态重力竞技场
   - 比赛阶段管理
   - 动态环境元素

3. **物理对象同步**
   - 可破坏物体网络化
   - 移动平台同步
   - 物理道具系统

### 第四阶段：优化和完善 (1-2周)
1. **性能优化**
   - LOD系统实现
   - 数据压缩
   - 网络带宽优化

2. **用户体验**
   - 网络状态指示器
   - 延迟补偿优化
   - 断线重连机制

3. **测试和调试**
   - 多客户端测试
   - 性能基准测试
   - 网络稳定性测试

---

## 技术挑战和解决方案

### 1. 物理同步一致性
**挑战**: 不同客户端的物理模拟可能产生分歧
**解决方案**: 
- Master Client作为物理权威
- 定期强制同步关键物理状态
- 使用确定性物理设置

### 2. 重力场同步复杂性
**挑战**: 复杂重力场的实时同步
**解决方案**:
- 分层同步策略（静态vs动态重力源）
- 区域兴趣管理
- 重力影响预测缓存

### 3. 高频物理数据传输
**挑战**: 物理数据量大，容易造成网络拥塞
**解决方案**:
- 智能数据压缩
- 基于距离的LOD系统
- 增量更新策略

### 4. 延迟补偿
**挑战**: 网络延迟影响物理交互体验
**解决方案**:
- 客户端预测+服务器校正
- 延迟自适应的插值算法
- 关键事件的时间戳回溯

---

## 总结

本设计文档提供了一个完整的基于物理的PUN2网络同步架构，重点特色包括：

1. **创新的匹配体验** - 玩家在3D世界中通过物理交互进行房间匹配
2. **物理优先的设计** - 所有游戏逻辑基于真实物理模拟
3. **高性能网络同步** - 通过LOD、压缩和预测技术保证流畅体验
4. **可扩展的架构** - 模块化设计支持后续功能扩展

该架构充分利用了GravityShoot项目的独特重力机制，将其与网络多人游戏体验完美结合，创造出独特而富有沉浸感的多人游戏体验。
