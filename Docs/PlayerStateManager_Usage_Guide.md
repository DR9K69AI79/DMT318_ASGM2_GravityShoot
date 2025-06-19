# Player State Manager & Animation Controller 使用指南

## 📋 概述

这是一个基于事件驱动的玩家状态管理和动画控制系统，旨在实现以下目标：
- 解耦运动逻辑和动画控制
- 提供统一的状态中心供其他系统订阅
- 为后续音效、粒子特效等系统奠定基础

## 🚀 快速开始

### 1. 添加组件

在包含 `PlayerMotor` 的GameObject上添加以下组件：

```
PlayerGameObject
├── PlayerMotor (已存在)
├── PlayerInput (已存在)  
├── PlayerStateManager (新增)
└── PlayerAnimationController (新增)
```

### 2. 基本配置

#### PlayerStateManager 配置
- **Update Rate**: 状态更新频率 (默认30fps)
- **Show Debug Info**: 是否显示调试信息

#### PlayerAnimationController 配置
- **Animation Smoothing**: 动画参数平滑速度
- **动画参数名称**: 确保与Animator Controller中的参数名匹配
  - velocityForward (float)
  - velocityStrafe (float)
  - isSprinting (bool)
  - isGrounded (bool)
  - isInAir (bool) - **新增**
  - triggerJump (trigger) - **新增**
  - triggerLand (trigger) - **新增**

## 📊 系统架构

### 数据流向
```
PlayerMotor → PlayerStateManager → PlayerAnimationController
     ↓              ↓                    ↓
  物理运动    →   状态收集分发    →     动画更新
                    ↓
               (其他订阅系统)
```

### 状态数据结构

`PlayerStateData` 包含以下分类的状态：

#### 运动状态 (MovementState)
- `isGrounded`: 是否接地
- `isOnSteep`: 是否在陡坡上
- `isSprinting`: 是否在冲刺
- `velocity`: 当前速度向量
- `speed`: 速度大小
- `moveInput`: 移动输入
- `currentSpeedMultiplier`: 当前速度倍率

#### 跳跃状态 (JumpState)
- `isJumping`: 是否在跳跃
- `jumpPhase`: 跳跃阶段
- `canJump`: 是否可以跳跃

#### 环境状态 (EnvironmentState)
- `gravityDirection`: 重力方向
- `upAxis`: 上方向
- `forwardAxis`: 前方向
- `rightAxis`: 右方向

#### 输入状态 (InputState)
- `lookInput`: 视角输入
- `firePressed`: 开火按键
- `jumpPressed`: 跳跃按键
- `sprintPressed`: 冲刺按键

## 🎯 扩展使用

### 订阅状态变化

```csharp
public class CustomSystem : MonoBehaviour
{
    private void OnEnable()
    {
        // 订阅通用状态变化
        PlayerStateManager.OnStateChanged += HandleStateChanged;
        
        // 订阅特定状态变化
        PlayerStateManager.OnMovementChanged += HandleMovementChanged;
        PlayerStateManager.OnGroundStateChanged += HandleGroundStateChanged;
    }
    
    private void OnDisable()
    {
        // 取消订阅
        PlayerStateManager.OnStateChanged -= HandleStateChanged;
        PlayerStateManager.OnMovementChanged -= HandleMovementChanged;
        PlayerStateManager.OnGroundStateChanged -= HandleGroundStateChanged;
    }
    
    private void HandleStateChanged(PlayerStateChangedEventArgs args)
    {
        var currentState = args.CurrentState;
        var previousState = args.PreviousState;
        
        // 处理状态变化逻辑
    }
}
```

### 创建音效系统

```csharp
public class PlayerAudioController : MonoBehaviour
{
    [SerializeField] private AudioSource _footstepAudio;
    [SerializeField] private AudioSource _jumpAudio;
    
    private void OnEnable()
    {
        PlayerStateManager.OnGroundStateChanged += HandleGroundStateChanged;
        PlayerStateManager.OnMovementChanged += HandleMovementChanged;
    }
    
    private void HandleGroundStateChanged(PlayerStateChangedEventArgs args)
    {
        if (args.CurrentState.isGrounded && !args.PreviousState.isGrounded)
        {
            // 播放着地音效
            _jumpAudio.Play();
        }
    }
    
    private void HandleMovementChanged(PlayerStateChangedEventArgs args)
    {
        // 根据移动速度调整脚步声
        if (args.CurrentState.isGrounded && args.CurrentState.speed > 0.1f)
        {
            _footstepAudio.pitch = Mathf.Lerp(0.8f, 1.2f, args.CurrentState.speed / 10f);
        }
    }
}
```

#### 新增的跳跃动画功能

PlayerAnimationController现在支持更精确的跳跃动画控制：

```csharp
// 获取PlayerAnimationController引用
var animController = GetComponent<PlayerAnimationController>();

// 手动触发跳跃动画
animController.TriggerJump();

// 手动触发着地动画
animController.TriggerLand();

// 手动设置空中状态
animController.SetInAirState(true);
```

#### 动画触发逻辑

系统会自动检测以下状态变化并触发相应动画：

1. **跳跃检测**: 从地面到空中 + 有向上速度 → 触发 `triggerJump`
2. **着地检测**: 从空中到地面 → 触发 `triggerLand`  
3. **空中状态**: 自动设置 `isInAir = !isGrounded`

## ⚡ 性能考虑

### 优化建议
1. **更新频率**: 根据需要调整 `_updateRate`，不是所有系统都需要60fps的状态更新
2. **事件过滤**: 只订阅需要的特定事件类型，避免不必要的处理
3. **状态缓存**: 系统内部已实现状态缓存，避免重复计算

### 内存优化
- `PlayerStateData` 设计为值类型(struct)，减少GC压力
- 事件参数复用，避免频繁分配

## 🔧 调试功能

### 可视化调试
- **PlayerStateManager**: 在Inspector中启用 `Show Debug Info` 查看实时状态
- **PlayerAnimationController**: 显示动画参数的当前值和目标值

### 调试面板
运行时按 `~` 键可能会显示调试信息（取决于实现）

## 🚨 迁移说明

### 从旧系统迁移
1. 移除对 `PlayerMotor.UpdateAnimator()` 的直接调用
2. 添加 `PlayerStateManager` 和 `PlayerAnimationController` 组件
3. 将动画相关逻辑迁移到新的事件订阅模式

### 向后兼容
- `PlayerMotor.UpdateAnimator()` 被标记为过时但仍可使用
- 建议逐步迁移到新系统以享受事件驱动的优势

## 📋 检查清单

- [ ] 已添加 PlayerStateManager 组件
- [ ] 已添加 PlayerAnimationController 组件  
- [ ] 动画参数名称已正确配置
- [ ] 已测试基本的移动和动画功能
- [ ] 已订阅需要的状态变化事件
- [ ] 已验证性能表现符合预期

## 🔮 后续扩展

这个系统为以下功能预留了接口：
- 音效系统 (`PlayerAudioController`)
- 粒子特效系统 (`PlayerEffectsController`)
- 网络同步优化
- VR适配支持
- 输入回放系统
- AI行为树集成

每个新系统只需订阅相关的状态变化事件即可，无需修改核心逻辑。
