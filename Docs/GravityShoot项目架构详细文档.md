# GravityShoot 项目架构详细文档

## 目录
1. [项目概述](#项目概述)
2. [整体架构设计](#整体架构设计)
3. [核心系统详解](#核心系统详解)
4. [脚本功能职责划分](#脚本功能职责划分)
5. [数据流分析](#数据流分析)
6. [网络架构](#网络架构)
7. [最佳实践与设计模式](#最佳实践与设计模式)
8. [扩展指南](#扩展指南)

---

## 项目概述

**GravityShoot** 是一个基于Unity引擎开发的多人在线重力射击游戏。项目采用**模块化架构**设计，支持自定义重力环境下的第一人称射击游戏玩法。

### 核心特性
- 🌌 **自定义重力系统**：支持多重力场叠加，创造独特的3D空间游戏体验
- 🎮 **多人网络对战**：基于PUN2的稳定网络架构，支持客户端预测和延迟补偿
- 🔫 **物理射击系统**：基于物理的投射物系统，支持多种武器类型
- 🎯 **精确控制系统**：适配重力环境的角色控制，支持复杂地形移动
- 📱 **新输入系统**：基于Unity Input System的现代化输入处理

### 技术栈
- **引擎**: Unity 2022.3+ LTS
- **网络**: Photon PUN2
- **输入**: Unity Input System
- **物理**: Unity Physics + 自定义重力
- **架构**: 模块化组件设计
- **编程语言**: C#

---

## 整体架构设计

### 架构层次图

```
┌─────────────────────────────────────────────────┐
│                   UI 层                          │
│              (游戏界面系统)                        │
└─────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────┐
│                  输入层                          │
│        InputManager → PlayerInput               │
└─────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────┐
│                  控制层                          │
│  PlayerMotor | PlayerView | WeaponController    │
└─────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────┐
│                  系统层                          │
│    Gravity | Physics | Animation | Audio        │
└─────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────┐
│                  网络层                          │
│      NetworkPlayerController | PUN2             │
└─────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────┐
│                  数据层                          │
│           ScriptableObject 配置系统              │
└─────────────────────────────────────────────────┘
```

### 核心设计原则

1. **模块化分离**：各系统功能独立，降低耦合度
2. **事件驱动**：使用事件系统实现组件间通信
3. **数据驱动**：通过ScriptableObject实现配置与代码分离
4. **网络友好**：从设计初期考虑网络同步需求
5. **扩展性优先**：抽象基类设计，便于功能扩展

---

## 核心系统详解

### 1. 重力系统 (Gravity System)

#### 系统架构
```
CustomGravity (静态管理器)
    ↓
GravitySource (抽象基类)
    ↓
├── GravitySphere (球形重力)
├── GravityBox (盒形重力)  
├── GravityPlane (平面重力)
└── GravityBoxInside (内部重力)
```

#### 核心类说明

**CustomGravity** - 重力系统静态管理器
```csharp
public static class CustomGravity
{
    // 注册/注销重力源
    public static void Register(GravitySource source)
    public static void Unregister(GravitySource source)
    
    // 获取指定位置的累积重力
    public static Vector3 GetGravity(Vector3 position)
    public static Vector3 GetGravity(Vector3 position, out Vector3 upAxis)
    
    // 获取最强重力源的上轴方向
    public static Vector3 GetUpAxis(Vector3 position)
}
```

**GravitySource** - 重力源抽象基类
```csharp
public abstract class GravitySource : MonoBehaviour
{
    // 计算指定位置的重力加速度
    public abstract Vector3 GetGravity(Vector3 position);
    
    // 获取上轴方向
    public virtual Vector3 GetUpAxis(Vector3 position);
    
    // 自动注册/注销
    protected virtual void OnEnable() => CustomGravity.Register(this);
    protected virtual void OnDisable() => CustomGravity.Unregister(this);
}
```

#### 重力源实现

1. **GravitySphere** - 模拟行星引力
   - 支持平方反比定律
   - 可配置重力半径和强度

2. **GravityBox** - 复杂盒形重力场
   - 支持内外距离控制
   - 衰减因子配置
   - 边界检测算法

3. **GravityPlane** - 恒定方向重力
   - 适用于传统重力环境
   - 可配置影响范围

4. **GravityBoxInside** - 内部重力体
   - 仅在指定体积内生效
   - 支持任意方向重力

#### 技术特点
- **多重力场叠加**：支持多个重力源同时作用
- **实时计算**：每帧计算当前位置的重力向量
- **网络同步**：重力方向在网络间保持一致
- **性能优化**：空间分割和层次检测优化

### 2. 角色控制系统 (Character Control System)

#### 系统架构
```
PlayerInput (输入接口)
    ↓
PlayerMotor (运动控制) ←→ PlayerView (视角控制)
    ↓
PlayerAnimationController (动画控制)
    ↓
PlayerStatusManager (状态管理)
```

#### 核心类详解

**PlayerMotor** - 基于Rigidbody的运动控制器
```csharp
public class PlayerMotor : MonoBehaviour
{
    // 核心属性
    public Vector3 Velocity { get; }           // 当前速度
    public Vector3 UpAxis { get; }             // 当前"上"方向
    public bool IsGrounded { get; }            // 是否在地面
    
    // 移动控制
    public float MoveSpeed { get; }            // 移动速度
    public bool IsSprinting { get; }           // 是否在奔跑
    
    // 重力适配
    private void UpdateSmoothedUpAxis()        // 平滑更新上轴方向
    private void UpdateRotation()              // 统一旋转更新
    
    // 地面检测
    private bool CheckGround()                 // 地面检测算法
    private bool SnapToGround()                // 地面吸附算法
}
```

**设计亮点**：
- **平滑重力转换**：通过插值消除重力切换时的抖动
- **精确地面检测**：支持复杂地形的地面识别
- **物理优先**：直接控制速度而非施加力，确保精确控制
- **Coyote Time**：宽容的跳跃时机判定

**PlayerView** - 摄像机和视角控制
```csharp
public class PlayerView : MonoBehaviour
{
    // 视角控制
    private void UpdateCameraRotation()        // 相机旋转更新
    private void UpdatePitchAngleToUpAxis()    // 俯仰角适配重力
    
    // 重力响应
    private void HandleGravityTransition()     // 重力转换处理
    public void UpdateViewForGravityChange()  // 视角重力适配
}
```

**设计亮点**：
- **重力自适应**：摄像机自动适配当前重力方向
- **平滑转换**：重力变化时的视角平滑过渡
- **姿态保持**：在复杂重力环境中保持合理的视角

### 3. 武器系统 (Weapon System)

#### 系统架构
```
PlayerWeaponController (武器控制器)
    ↓
WeaponBase (武器基类)
    ↓
├── ProjectileWeapon (投射型武器)
└── HitscanWeapon (即时命中武器)
    ↓
ProjectileBase (投射物基类)
    ↓
├── BasicProjectile (基础投射物)
├── GrenadeLauncher (榴弹投射物)
└── BouncingProjectile (弹跳投射物)
```

#### 核心类详解

**PlayerWeaponController** - 武器控制器
```csharp
public class PlayerWeaponController : MonoBehaviourPun
{
    // 武器管理
    public WeaponBase CurrentWeapon { get; }
    public int CurrentWeaponIndex { get; }
    public Vector3 CurrentAimDirection { get; }
    
    // 输入处理
    private void UpdateFireInput()             // 射击输入处理
    private void UpdateWeaponSwitching()       // 武器切换处理
    
    // 瞄准系统
    private Vector3 CalculatePhysicsAimDirection() // 物理瞄准计算
    public Ray GetAimRay()                     // 获取瞄准射线
    
    // 射击控制
    public bool TryFire()                      // 尝试射击
}
```

**WeaponBase** - 武器抽象基类
```csharp
public abstract class WeaponBase : MonoBehaviourPun
{
    // 状态属性
    public bool IsEquipped { get; }
    public bool IsReloading { get; }
    public bool HasAmmo { get; }
    public bool CanFire { get; }
    
    // 核心接口
    public abstract bool Fire(Vector3 direction, float timestamp);
    public abstract void NetworkFire(Vector3 direction, float timestamp);
    
    // 弹药管理
    public virtual bool Reload()
    public virtual void ConsumeAmmo()
}
```

**ProjectileBase** - 投射物抽象基类
```csharp
public abstract class ProjectileBase : MonoBehaviourPun
{
    // 发射接口
    public virtual void Launch(Vector3 direction, float speed, 
                              WeaponBase sourceWeapon, GameObject sourcePlayer)
    
    // 物理处理
    protected virtual void ApplyCustomGravity()    // 自定义重力应用
    protected virtual void HandleCollision()       // 碰撞处理
    
    // 弹跳系统
    protected virtual void TryBounce()             // 弹跳逻辑
    
    // 网络同步
    public virtual void OnNetworkHit()             // 网络命中事件
    public virtual void OnNetworkBounce()          // 网络弹跳事件
}
```

#### 技术特点
- **物理瞄准**：基于射线检测的真实瞄准系统
- **网络同步**：RPC同步射击事件，确保多人一致性
- **投射物池**：对象池管理，优化性能
- **弹跳系统**：支持复杂的弹跳物理计算
- **重力适配**：投射物自动适应当前重力环境

### 4. 输入系统 (Input System)

#### 系统架构
```
Unity Input System (底层)
    ↓
InputManager (中央管理器)
    ↓
PlayerInput (本地过滤器)
    ↓
各种控制器 (PlayerMotor, WeaponController, etc.)
```

#### 核心设计

**InputManager** - 输入系统中央管理器
```csharp
public class InputManager : Singleton<InputManager>
{
    // 输入状态
    public Vector2 MoveInput { get; }          // 移动输入
    public Vector2 LookInput { get; }          // 视角输入
    public bool FirePressed { get; }           // 射击按下
    public bool JumpPressed { get; }           // 跳跃按下
    
    // 输入事件
    public static event Action<Vector2> OnMoveInput;
    public static event Action OnFirePressed;
    public static event Action OnJumpPressed;
    
    // 输入处理
    private void ProcessInputs()               // 输入处理管道
    private Vector2 ApplyDeadZone(Vector2 input) // 死区应用
}
```

**PlayerInput** - 输入过滤器
```csharp
public class PlayerInput : MonoBehaviour
{
    // 过滤设置
    public bool EnableMove { get; set; }
    public bool EnableLook { get; set; }
    public bool EnableFire { get; set; }
    
    // 输入访问
    public Vector2 MoveInput { get; }          // 过滤后的移动输入
    public bool FirePressed { get; }           // 过滤后的射击输入
    
    // 输入控制
    public void SetInputEnabled(bool move, bool look, bool fire)
    public void SetCursorLock(bool locked)
}
```

#### 设计优势
- **事件驱动**：解耦输入与控制逻辑
- **分层过滤**：支持局部输入控制
- **缓冲机制**：输入缓冲提升响应性
- **网络友好**：输入状态易于网络同步

### 5. 网络系统 (Network System)

#### 系统架构
```
Photon PUN2 (底层网络)
    ↓
NetworkSyncBase (同步基类)
    ↓
├── NetworkPlayerController (玩家同步)
├── NetworkInputManager (输入同步)
└── NetworkProjectileManager (投射物同步)
```

#### 核心类详解

**NetworkPlayerController** - 玩家网络同步
```csharp
public class NetworkPlayerController : NetworkSyncBase
{
    // 状态管理
    public bool IsLocalPlayer { get; }
    public Vector3 PredictedPosition { get; }
    public float NetworkLatency { get; }
    
    // 客户端预测
    private void HandleLocalPlayerUpdate()     // 本地玩家更新
    private void HandleRemotePlayerUpdate()    // 远程玩家更新
    
    // 状态同步
    protected override void WriteData(PhotonStream stream)
    protected override void ReadData(PhotonStream stream, PhotonMessageInfo info)
    
    // 延迟补偿
    private void ApplyLagCompensation()        // 延迟补偿算法
    private void InterpolateRemotePlayer()     // 远程玩家插值
}
```

#### 网络同步策略

1. **玩家状态同步**
   - 位置、旋转、速度
   - 重力方向和上轴
   - 移动状态（跳跃、奔跑等）

2. **输入同步**
   - 移动输入向量
   - 按键状态
   - 时间戳同步

3. **武器同步**
   - 射击事件RPC
   - 弹药状态
   - 武器切换

4. **投射物同步**
   - 网络实例化
   - 轨迹同步
   - 命中事件

#### 优化技术
- **客户端预测**：减少输入延迟感知
- **状态插值**：平滑远程玩家移动
- **延迟补偿**：公平的命中判定
- **压缩传输**：减少网络带宽消耗

---

## 脚本功能职责划分

### 核心角色控制脚本

| 脚本名 | 主要职责 | 依赖关系 |
|--------|----------|----------|
| `PlayerMotor` | 物理运动控制、重力适配、地面检测 | PlayerInput, CustomGravity |
| `PlayerView` | 摄像机控制、视角管理、重力转换 | PlayerMotor, PlayerInput |
| `PlayerInput` | 输入过滤、状态暴露 | InputManager |
| `PlayerStatusManager` | 生命值、状态管理、事件分发 | PlayerMotor, WeaponController |
| `PlayerAnimationController` | 动画控制、状态机管理 | PlayerStatusManager |

### 武器系统脚本

| 脚本名 | 主要职责 | 依赖关系 |
|--------|----------|----------|
| `PlayerWeaponController` | 武器管理、射击控制、瞄准计算 | PlayerInput, WeaponBase |
| `WeaponBase` | 武器基础功能、弹药管理 | WeaponData (SO) |
| `ProjectileWeapon` | 投射型武器实现 | WeaponBase, ProjectileBase |
| `ProjectileBase` | 投射物物理、碰撞检测、网络同步 | CustomGravity |

### 重力系统脚本

| 脚本名 | 主要职责 | 依赖关系 |
|--------|----------|----------|
| `CustomGravity` | 重力源管理、重力计算 | GravitySource |
| `GravitySource` | 重力源抽象接口 | - |
| `GravitySphere` | 球形重力场实现 | GravitySource |
| `GravityBox` | 盒形重力场实现 | GravitySource |
| `GravityPlane` | 平面重力场实现 | GravitySource |

### 网络系统脚本

| 脚本名 | 主要职责 | 依赖关系 |
|--------|----------|----------|
| `NetworkPlayerController` | 玩家状态网络同步 | PlayerMotor, PUN2 |
| `NetworkSyncBase` | 网络同步基类 | PUN2 |
| `NetworkInputManager` | 输入网络同步 | InputManager, PUN2 |

### 输入系统脚本

| 脚本名 | 主要职责 | 依赖关系 |
|--------|----------|----------|
| `InputManager` | 输入系统管理、事件分发 | Unity Input System |
| `PlayerInput` | 输入过滤、本地控制 | InputManager |

---

## 数据流分析

### 1. 玩家移动数据流

```
用户输入 → Unity Input System → InputManager → PlayerInput 
    ↓
PlayerMotor → CustomGravity → 重力计算 → 物理更新
    ↓
NetworkPlayerController → PUN2 → 网络同步
    ↓
远程客户端 → 状态插值 → 视觉更新
```

### 2. 射击系统数据流

```
射击输入 → PlayerInput → PlayerWeaponController → WeaponBase
    ↓
ProjectileWeapon → 创建投射物 → ProjectileBase → 物理模拟
    ↓
碰撞检测 → 伤害计算 → RPC同步 → 远程客户端更新
```

### 3. 重力系统数据流

```
重力源注册 → CustomGravity → 重力计算
    ↓
PlayerMotor → 重力应用 → 旋转更新 → 摄像机适配
    ↓
投射物 → 重力影响 → 轨迹计算 → 网络同步
```

### 4. 网络同步数据流

```
本地状态变化 → NetworkPlayerController → 数据打包
    ↓
PUN2传输 → 远程客户端 → 数据解包 → 状态应用
    ↓
客户端预测 → 状态校正 → 插值处理 → 视觉呈现
```

---

## 网络架构

### 网络拓扑

GravityShoot采用**PUN2的主机模式**网络架构：

```
Master Client (主机)
    ↓
├── Client 1 (玩家A)
├── Client 2 (玩家B) 
├── Client 3 (玩家C)
└── Client 4 (玩家D)
```

### 同步策略

#### 1. 玩家状态同步 (30Hz)
```csharp
// 发送数据
stream.SendNext(position);
stream.SendNext(rotation);
stream.SendNext(velocity);
stream.SendNext(gravityDirection);
stream.SendNext(isGrounded);

// 接收数据
position = (Vector3)stream.ReceiveNext();
rotation = (Quaternion)stream.ReceiveNext();
velocity = (Vector3)stream.ReceiveNext();
```

#### 2. 输入同步 (60Hz)
```csharp
// 同步输入状态
public struct NetworkInputData
{
    public Vector2 moveInput;
    public Vector2 lookInput;
    public bool jumpPressed;
    public bool firePressed;
    public float timestamp;
}
```

#### 3. 射击事件同步 (RPC)
```csharp
[PunRPC]
void RPC_WeaponFired(float posX, float posY, float posZ, 
                     float dirX, float dirY, float dirZ)
{
    Vector3 muzzlePosition = new Vector3(posX, posY, posZ);
    Vector3 aimDirection = new Vector3(dirX, dirY, dirZ);
    // 播放远程射击效果
}
```

### 客户端预测

#### 预测算法
```csharp
// 记录状态历史
private void RecordStateHistory()
{
    var state = new NetworkPlayerState
    {
        position = transform.position,
        velocity = rigidbody.velocity,
        timestamp = PhotonNetwork.Time
    };
    _stateHistory.Enqueue(state);
}

// 状态校正
private void ReconcileState(NetworkPlayerState serverState)
{
    float timeDiff = Mathf.Abs(serverState.timestamp - PhotonNetwork.Time);
    if (timeDiff > _reconciliationThreshold)
    {
        // 应用服务器状态
        transform.position = serverState.position;
        rigidbody.velocity = serverState.velocity;
    }
}
```

### 延迟补偿

#### 回溯命中检测
```csharp
public bool CheckHitWithCompensation(Ray ray, float timestamp)
{
    // 计算延迟
    float latency = (float)(PhotonNetwork.Time - timestamp);
    
    // 回溯玩家位置
    Vector3 compensatedPosition = GetPlayerPositionAtTime(timestamp - latency);
    
    // 执行命中检测
    return Physics.Raycast(ray, out hit);
}
```

---

## 最佳实践与设计模式

### 1. 设计模式应用

#### 单例模式 (Singleton)
- **应用**: InputManager, GameManager
- **优势**: 全局访问点，状态统一管理
- **实现**: 线程安全的延迟初始化

```csharp
public class InputManager : Singleton<InputManager>
{
    protected override void Awake()
    {
        base.Awake();
        InitializeInputSystem();
    }
}
```

#### 抽象工厂模式 (Abstract Factory)
- **应用**: WeaponBase, ProjectileBase, GravitySource
- **优势**: 易于扩展新类型，代码复用
- **实现**: 抽象基类定义接口，具体类实现功能

#### 观察者模式 (Observer)
- **应用**: 输入事件系统，状态变化通知
- **优势**: 解耦组件间通信，灵活的事件处理
- **实现**: C#事件和委托机制

```csharp
public static event Action<Vector2> OnMoveInput;
public static event Action OnFirePressed;
```

#### 组件模式 (Component)
- **应用**: Unity MonoBehaviour架构
- **优势**: 模块化设计，功能独立
- **实现**: 单一职责的组件设计

### 2. 编码最佳实践

#### 命名规范
```csharp
// 类名：PascalCase
public class PlayerMotor

// 公共属性：PascalCase
public Vector3 Velocity { get; }

// 私有字段：下划线+camelCase
private float _moveSpeed;

// 常量：UPPER_SNAKE_CASE
private const float MAX_SPEED = 20f;
```

#### 性能优化实践

1. **对象池模式**
```csharp
// 投射物对象池
public class ProjectilePool : MonoBehaviour
{
    private Queue<ProjectileBase> _pool = new Queue<ProjectileBase>();
    
    public ProjectileBase GetProjectile()
    {
        return _pool.Count > 0 ? _pool.Dequeue() : CreateNewProjectile();
    }
    
    public void ReturnProjectile(ProjectileBase projectile)
    {
        projectile.Reset();
        _pool.Enqueue(projectile);
    }
}
```

2. **缓存组件引用**
```csharp
public class PlayerMotor : MonoBehaviour
{
    private Rigidbody _rb;
    private PlayerInput _playerInput;
    
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _playerInput = GetComponent<PlayerInput>();
    }
}
```

3. **减少GC分配**
```csharp
// 避免频繁创建Vector3
private Vector3 _tempVector = Vector3.zero;

private void UpdateMovement()
{
    _tempVector.Set(x, y, z);  // 复用现有对象
    transform.position = _tempVector;
}
```

#### 调试与日志

```csharp
// 条件调试
[SerializeField] private bool _showDebugInfo = false;

private void LogDebug(string message)
{
    if (_showDebugInfo)
        Debug.Log($"[{GetType().Name}] {message}");
}

// 性能分析标记
private void ExpensiveOperation()
{
    Profiler.BeginSample("ExpensiveOperation");
    // 复杂计算...
    Profiler.EndSample();
}
```

### 3. 架构设计原则

#### SOLID原则应用

1. **单一职责原则 (SRP)**
   - PlayerMotor只负责运动控制
   - PlayerView只负责摄像机管理
   - WeaponController只负责武器管理

2. **开闭原则 (OCP)**
   - WeaponBase可扩展新武器类型
   - GravitySource可添加新重力源
   - ProjectileBase支持新投射物类型

3. **里氏替换原则 (LSP)**
   - 所有WeaponBase子类可互相替换
   - 所有ProjectileBase子类行为一致
   - GravitySource实现类接口统一

4. **依赖倒置原则 (DIP)**
   - PlayerMotor依赖于PlayerInput抽象
   - WeaponController依赖于WeaponBase抽象
   - 高层模块不依赖低层实现细节

---

## 扩展指南

### 1. 添加新武器类型

#### 步骤1：创建武器数据配置
```csharp
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Weapons/Weapon Data")]
public class LaserWeaponData : WeaponData
{
    [Header("激光武器特有属性")]
    public float beamWidth = 0.1f;
    public float maxRange = 100f;
    public AnimationCurve damageFalloff;
}
```

#### 步骤2：实现武器逻辑
```csharp
public class LaserWeapon : WeaponBase
{
    private LaserWeaponData LaserData => _weaponData as LaserWeaponData;
    
    public override bool Fire(Vector3 direction, float timestamp)
    {
        // 激光武器射击逻辑
        CreateLaserBeam(direction);
        return true;
    }
    
    protected override void NetworkFire(Vector3 direction, float timestamp)
    {
        // 网络同步逻辑
        photonView.RPC("RPC_LaserFired", RpcTarget.Others, direction);
    }
}
```

### 2. 添加新重力源类型

#### 创建重力源
```csharp
public class GravityTunnel : GravitySource
{
    [SerializeField] private Vector3 _tunnelStart;
    [SerializeField] private Vector3 _tunnelEnd;
    [SerializeField] private float _tunnelRadius = 5f;
    [SerializeField] private float _gravity = 9.8f;
    
    public override Vector3 GetGravity(Vector3 position)
    {
        // 计算隧道重力逻辑
        Vector3 closestPoint = GetClosestPointOnTunnel(position);
        Vector3 direction = (closestPoint - position).normalized;
        float distance = Vector3.Distance(position, closestPoint);
        
        if (distance <= _tunnelRadius)
        {
            return direction * _gravity;
        }
        
        return Vector3.zero;
    }
}
```

### 3. 扩展投射物类型

#### 创建新投射物
```csharp
public class HomingProjectile : ProjectileBase
{
    [Header("制导设置")]
    [SerializeField] private float _homingStrength = 5f;
    [SerializeField] private float _maxTurnRate = 90f;
    [SerializeField] private LayerMask _targetLayers;
    
    private Transform _target;
    
    protected override void UpdateProjectile()
    {
        base.UpdateProjectile();
        
        if (_target != null)
        {
            Vector3 directionToTarget = (_target.position - transform.position).normalized;
            Vector3 newDirection = Vector3.Slerp(transform.forward, directionToTarget, 
                                                _homingStrength * Time.deltaTime);
            
            _rigidbody.velocity = newDirection * _speed;
            transform.rotation = Quaternion.LookRotation(newDirection);
        }
    }
    
    protected override bool ProcessHit(RaycastHit hit)
    {
        // 制导弹命中处理
        CreateExplosion(hit.point);
        return true; // 销毁投射物
    }
}
```

### 4. 自定义网络同步

#### 扩展网络同步数据
```csharp
public class CustomNetworkSync : NetworkSyncBase
{
    [Header("自定义同步数据")]
    [SerializeField] private float _customValue;
    [SerializeField] private bool _customState;
    
    protected override void WriteData(PhotonStream stream)
    {
        base.WriteData(stream);
        stream.SendNext(_customValue);
        stream.SendNext(_customState);
    }
    
    protected override void ReadData(PhotonStream stream, PhotonMessageInfo info)
    {
        base.ReadData(stream, info);
        _customValue = (float)stream.ReceiveNext();
        _customState = (bool)stream.ReceiveNext();
    }
}
```

### 5. 添加新输入动作

#### 扩展输入系统
```csharp
// 1. 在Input Actions中添加新动作
// 2. 更新InputManager
public class InputManager : Singleton<InputManager>
{
    // 添加新的输入事件
    public static event Action OnSpecialActionPressed;
    
    private void BindPlayerInputCallbacks()
    {
        // 绑定新动作
        playerActions.SpecialAction.performed += OnSpecialActionInputPressed;
    }
    
    private void OnSpecialActionInputPressed(InputAction.CallbackContext context)
    {
        OnSpecialActionPressed?.Invoke();
    }
}

// 3. 在PlayerInput中添加过滤
public class PlayerInput : MonoBehaviour
{
    public bool SpecialActionPressed => _enableSpecialAction ? _filteredSpecialActionPressed : false;
    
    private void OnSpecialActionPressed()
    {
        if (_enableSpecialAction)
        {
            _filteredSpecialActionPressed = true;
        }
    }
}
```

---

## 总结

GravityShoot项目采用了现代化的Unity游戏开发架构，具有以下核心优势：

### 架构优势
1. **模块化设计**：各系统职责明确，便于维护和扩展
2. **网络友好**：从设计初期考虑多人游戏需求
3. **性能优化**：合理的对象池和缓存策略
4. **扩展性强**：抽象基类设计支持快速添加新功能
5. **代码质量**：遵循SOLID原则，易于测试和调试

### 技术亮点
1. **自定义重力系统**：独特的多重力场环境
2. **客户端预测**：优秀的网络游戏体验
3. **物理射击**：真实的弹道计算和碰撞检测
4. **事件驱动**：解耦的组件通信机制
5. **数据驱动**：ScriptableObject配置系统

### 学习价值
本项目是学习Unity高级开发技术的优秀案例，涵盖了：
- 复杂物理系统设计
- 网络游戏架构
- 现代输入系统应用
- 性能优化技巧
- 代码架构最佳实践

通过深入理解GravityShoot的架构设计，开发者可以掌握构建高质量Unity游戏的核心技能和设计思想。

---

*文档版本: v1.0*  
*最后更新: 2025年6月23日*  
*作者: GitHub Copilot*  
*项目: GravityShoot Unity Game*
