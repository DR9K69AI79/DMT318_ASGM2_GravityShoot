# GravityShoot 项目架构文档

## 项目概述

**GravityShoot** 是一个基于 Unity 引擎开发的多人在线FPS重力射击游戏。项目采用模块化架构设计，支持自定义重力环境下的角色移动和武器系统，通过 PUN2 实现多人网络同步。

### 核心特性
- 🌍 **自定义重力系统**：支持多重力场环境，角色可在不同重力方向间切换
- 🔫 **模块化武器系统**：数据驱动的武器配置，支持多种特殊效果
- 🎮 **精准输入控制**：基于新Unity Input System的响应式输入处理
- 🌐 **网络多人同步**：使用PUN2实现低延迟的多人游戏体验
- ⚙️ **数据驱动设计**：通过ScriptableObject实现灵活的配置管理

## 技术栈

- **游戏引擎**：Unity 2022.3 LTS
- **网络框架**：Photon PUN2
- **输入系统**：Unity Input System
- **物理引擎**：Unity Physics (Rigidbody)
- **编程语言**：C# (.NET)
- **架构模式**：模块化组件架构 + 事件驱动

## 整体架构设计

### 系统层次结构

```
应用层 (Application Layer)
├── 输入处理层 (Input Layer)
│   ├── InputManager (全局输入管理)
│   └── PlayerInput (玩家输入过滤)
│
├── 业务逻辑层 (Business Logic Layer)
│   ├── 角色系统 (Character System)
│   ├── 武器系统 (Weapon System)
│   ├── 重力系统 (Gravity System)
│   └── 网络系统 (Network System)
│
├── 数据配置层 (Configuration Layer)
│   ├── MovementTuningSO (移动调参)
│   ├── WeaponData (武器配置)
│   └── 其他配置数据
│
└── 基础框架层 (Framework Layer)
    ├── Singleton Pattern (单例模式)
    ├── NetworkSyncBase (网络同步基类)
    └── 工具类 (Utility)
```

### 数据流向

```
用户输入 → InputManager → PlayerInput → 各业务控制器
                              ↓
                    PlayerMotor ← PlayerWeaponController
                              ↓
                    NetworkPlayerController → PUN2网络层
```

## 核心系统详解

## 1. 输入系统 (Input System)

### 架构设计
输入系统采用分层设计，实现输入的集中管理和分发：

**InputManager (核心输入管理器)**
- **职责**：作为全局单例，管理所有输入事件的捕获和分发
- **技术特点**：基于Unity Input System，支持事件驱动架构
- **关键功能**：
  - 输入动作绑定和处理
  - 鼠标灵敏度和反转设置
  - 死区处理和输入过滤
  - 输入状态的实时更新

**PlayerInput (玩家输入过滤器)**
- **职责**：为单个玩家提供输入过滤和状态暴露接口
- **设计理念**：不主动转发事件，由其他组件主动拉取状态
- **关键功能**：
  - 输入权限控制（可禁用特定输入类型）
  - 输入状态缓存和访问
  - 调试信息显示

### 输入事件流

```
硬件输入 → Unity Input System → InputManager → 事件分发
                                        ↓
PlayerInput(过滤) → PlayerMotor/PlayerWeaponController → 业务逻辑执行
```

### 设计优势
- **解耦合**：输入处理与业务逻辑分离
- **可扩展**：易于添加新的输入类型
- **可配置**：支持运行时输入权限控制
- **调试友好**：完整的调试信息支持

## 2. 角色系统 (Character System)

### 系统组成

角色系统由多个专职组件协作实现完整的角色控制：

**PlayerMotor (角色物理运动控制器)**
- **核心职责**：基于Rigidbody的精确物理移动控制
- **技术特点**：
  - 自定义重力环境适配
  - 直接速度控制而非力的施加
  - 支持地面检测和坡度行走
  - 平滑的重力方向过渡

**关键算法**：
```csharp
// 重力对齐的平滑过渡算法
_smoothedUpAxis = Vector3.Slerp(_smoothedUpAxis, _currentUpAxis, 
    _tuning.gravityAlignmentSpeed * Time.fixedDeltaTime);

// 速度调整算法
Vector3 xAxis = ProjectDirectionOnPlane(RightAxis, _contactNormal);
Vector3 zAxis = ProjectDirectionOnPlane(ForwardAxis, _contactNormal);
```

**PlayerView (相机和视角控制器)**
- **核心职责**：第一人称视角控制和相机管理
- **技术特点**：
  - 重力自适应的相机对齐
  - 平滑的视角过渡
  - 头部倾斜和后仰限制

**PlayerAnimationController (动画控制器)**
- **核心职责**：角色动画的播放和同步
- **网络特性**：支持网络动画同步

**PlayerStateData (状态数据管理)**
- **核心职责**：角色状态信息的集中管理
- **数据内容**：移动状态、健康值、装备信息等

### 移动调参系统

**MovementTuningSO (移动调参配置)**
- **设计理念**：数据驱动的"手感"调节
- **配置内容**：
  - 地面/空中移动参数
  - 跳跃和重力设置
  - 奔跑和加速度曲线
  - 地面检测参数

```csharp
[CreateAssetMenu(fileName = "MovementTuning", menuName = "GravityShoot/Movement Tuning")]
public class MovementTuningSO : ScriptableObject
{
    public AnimationCurve groundAcceleration; // 加速度曲线
    public float maxGroundSpeed;              // 最大地面速度
    public SprintMode sprintMode;             // 奔跑模式
    // ... 更多配置参数
}
```

### 系统协作关系

```
PlayerInput → PlayerMotor (移动控制)
              ↓
PlayerView (视角控制) ← PlayerMotor (位置同步)
              ↓
PlayerAnimationController (动画播放)
```

## 3. 重力系统 (Gravity System)

### 架构设计

重力系统采用静态管理器模式，支持多重力源的累积计算：

**CustomGravity (静态重力管理器)**
- **核心职责**：全局重力源注册和重力计算
- **技术实现**：
```csharp
public static class CustomGravity
{
    private static List<GravitySource> _sources = new List<GravitySource>();
    
    public static Vector3 GetGravity(Vector3 position)
    {
        Vector3 gravity = Vector3.zero;
        for (int i = 0; i < _sources.Count; i++)
        {
            gravity += _sources[i].GetGravity(position);
        }
        return gravity;
    }
}
```

### 重力源类型

**GravitySource (重力源基类)**
- **职责**：定义重力源的通用接口

**GravityPlane (平面重力)**
- **特性**：提供统一方向的重力场
- **应用**：地面、天花板等平面区域

**GravitySphere (球形重力)**
- **特性**：向中心点的径向重力
- **应用**：星球、重力井效果

**GravityBox (盒形重力)**
- **特性**：盒形区域内的定向重力
- **应用**：房间、走廊等封闭空间

### 物理集成

**CustomGravityRigidbody (重力物理组件)**
- **职责**：为Rigidbody对象应用自定义重力
- **特性**：自动替换Unity默认重力

```csharp
void FixedUpdate()
{
    Vector3 gravity = CustomGravity.GetGravity(transform.position);
    _rigidbody.AddForce(gravity, ForceMode.Acceleration);
}
```

## 4. 武器系统 (Weapon System)

### 系统架构

武器系统采用基于继承的模块化设计，支持数据驱动配置：

**WeaponBase (武器抽象基类)**
- **核心职责**：定义所有武器的通用接口和行为
- **关键功能**：
  - 射击控制和冷却管理
  - 弹药系统和装弹逻辑
  - 后坐力系统
  - 音效和特效管理
  - 网络同步支持

**关键设计模式**：
```csharp
public abstract class WeaponBase : MonoBehaviourPun
{
    // 模板方法模式
    public virtual bool TryFire(Vector3 targetDirection)
    {
        if (!CanFire) return false;
        Fire(targetDirection);  // 抽象方法，子类实现
        OnFireComplete(targetDirection); // 通用后处理
        return true;
    }
    
    protected abstract void Fire(Vector3 direction); // 子类必须实现
}
```

**PlayerWeaponController (武器控制器)**
- **核心职责**：管理武器切换、输入处理和瞄准系统
- **技术特点**：
  - 武器库管理和切换逻辑
  - 瞄准系统（物理射线 vs 屏幕中心）
  - 射击输入缓冲
  - 网络同步控制

### 数据驱动配置

**WeaponData (武器配置数据)**
- **设计理念**：完全数据驱动的武器参数配置
- **配置内容**：
  - 基础属性（名称、图标、描述）
  - 发射参数（射速、散射、投射物数量）
  - 弹药系统（弹夹容量、装弹时间）
  - 伤害设置（基础伤害、爆头倍率、射程）
  - 特殊效果（弹跳、重力、爆炸）
  - 音效和视觉效果
  - 网络同步设置

```csharp
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "GravityShoot/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("发射设置")]
    public float _fireRate = 1f;
    public bool _automatic = false;
    public float _projectileSpeed = 20f;
    public int _projectilesPerShot = 1; // 散弹枪支持
    
    [Header("特殊效果")]
    public int _maxBounceCount = 0;      // 弹跳次数
    public float _gravityForce = 0f;     // 黑洞吸力
    public float _explosionRadius = 0f;  // 爆炸半径
}
```

### 投射物系统

**ProjectileBase (投射物基类)**
- **核心职责**：投射物的物理移动和碰撞处理
- **特殊功能**：
  - 重力环境适配
  - 弹跳和反射
  - 引力效果
  - 爆炸和范围伤害

### 网络同步设计

武器系统的网络同步分为两个层级：
1. **即时反馈**：本地客户端立即执行射击，提供即时响应
2. **权威同步**：通过RPC同步关键事件，确保游戏状态一致性

```csharp
[PunRPC]
public virtual void NetworkFire(Vector3 direction, float timestamp)
{
    // 网络同步的射击执行
}
```

## 5. 网络系统 (Network System)

### 系统架构

网络系统基于Photon PUN2构建，采用客户端-服务器架构：

**NetworkManager (网络管理器)**
- **核心职责**：PUN2连接管理、房间管理和游戏状态同步
- **关键功能**：
  - 自动连接和重连机制
  - 房间创建和加入逻辑
  - 玩家状态管理
  - 网络质量监控

**NetworkPlayerController (网络玩家控制器)**
- **核心职责**：玩家的网络状态同步和客户端预测
- **技术特点**：
  - 位置、旋转、速度同步
  - 重力方向同步
  - 客户端预测和服务器校正
  - 延迟补偿

### 同步策略

**状态同步模型**：
```csharp
private struct NetworkPlayerState
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 gravityDirection;
    public bool isGrounded;
    public float timestamp;
}
```

**客户端预测算法**：
1. 本地玩家立即执行移动
2. 发送输入到服务器
3. 接收服务器权威状态
4. 进行状态校正（如果差异超过阈值）

### 网络优化

- **自适应发送频率**：根据网络状况调整同步频率
- **优先级系统**：重要事件（射击、死亡）优先传输
- **数据压缩**：减少不必要的数据传输
- **预测和插值**：平滑网络延迟造成的抖动

## 6. 工具和扩展系统

### 调试工具

**Debug System**
- 实时性能监控
- 网络状态显示
- 物理参数可视化
- 输入状态调试

### 编辑器扩展

**Editor Tools**
- 重力场可视化编辑器
- 武器配置验证工具
- 网络测试辅助工具

## 设计模式应用

### 1. 单例模式 (Singleton Pattern)
- **应用**：InputManager, NetworkManager, CustomGravity
- **优势**：全局访问，资源管理统一

### 2. 模板方法模式 (Template Method)
- **应用**：WeaponBase.TryFire()
- **优势**：定义算法骨架，子类实现细节

### 3. 观察者模式 (Observer Pattern)
- **应用**：武器事件系统，输入事件分发
- **优势**：松耦合的事件通信

### 4. 数据驱动设计 (Data-Driven Design)
- **应用**：MovementTuningSO, WeaponData
- **优势**：配置与代码分离，易于调参

### 5. 组件模式 (Component Pattern)
- **应用**：玩家角色由多个专职组件组成
- **优势**：模块化，易于扩展和维护

## 性能优化策略

### 1. 物理优化
- 使用直接速度控制而非力的施加
- 智能地面检测，减少不必要的射线检测
- 重力计算缓存机制

### 2. 网络优化
- 客户端预测减少延迟感知
- 状态压缩和优先级传输
- 自适应同步频率

### 3. 内存管理
- 对象池管理投射物和特效
- 事件系统使用静态事件减少GC
- ScriptableObject数据共享

## 扩展性设计

### 1. 武器系统扩展
- 新武器类型：继承WeaponBase实现
- 新特效：通过WeaponData配置
- 新投射物：继承ProjectileBase

### 2. 重力系统扩展
- 新重力源：继承GravitySource
- 复杂重力场：组合多个重力源
- 动态重力：时间变化的重力效果

### 3. 网络功能扩展
- 新同步数据：扩展NetworkPlayerState
- 新RPC方法：添加网络事件
- 反作弊系统：服务器验证机制

## 最佳实践总结

### 1. 代码组织
- 使用命名空间（DWHITE）组织代码
- 按功能模块分离目录结构
- 接口和抽象类定义清晰的契约

### 2. 配置管理
- ScriptableObject进行数据驱动设计
- 运行时参数调整支持
- 配置验证和默认值处理

### 3. 调试和维护
- 完善的调试信息输出
- 可视化调试工具支持
- 错误处理和异常恢复

### 4. 性能考虑
- 避免在Update中进行复杂计算
- 使用事件系统减少轮询
- 合理的内存分配策略

## 总结

GravityShoot项目展现了一个成熟的Unity游戏架构设计，通过模块化、数据驱动和事件驱动的设计理念，实现了：

1. **高度可维护性**：清晰的模块划分和职责分离
2. **良好的扩展性**：基于接口和抽象类的设计
3. **优秀的性能**：针对性的优化策略
4. **稳定的网络体验**：基于PUN2的可靠同步机制
5. **灵活的配置能力**：数据驱动的参数调整

该架构为重力射击游戏这一特殊类型提供了完整的解决方案，同时也为其他类型的Unity游戏开发提供了有价值的参考。
