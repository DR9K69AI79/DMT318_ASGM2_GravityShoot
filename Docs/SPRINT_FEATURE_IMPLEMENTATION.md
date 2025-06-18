# 奔跑功能实现文档

## 概述

本文档描述了在 GravityShoot 项目中实现的奔跑功能。该功能按照现有代码架构和规范进行实现，提供了灵活的配置选项和平滑的游戏体验。

## 实现的功能

### 1. 奔跑模式支持
- **Hold模式**：按住Sprint键（默认Left Shift）进行奔跑
- **Toggle模式**：按下Sprint键切换奔跑状态

### 2. 速度控制
- 可配置的奔跑速度倍率（1.2x - 3x）
- 可配置的奔跑加速度倍率（1x - 2x）
- 平滑的速度过渡效果

### 3. 限制条件
- 只能在地面上奔跑
- 必须有移动输入才能奔跑
- 空中时自动停止奔跑

## 修改的文件

### 1. MovementTuningSO.cs
**新增内容**：
- `SprintMode` 枚举（Hold/Toggle）
- 奔跑相关参数：
  - `sprintSpeedMultiplier`：奔跑速度倍率
  - `sprintAccelerationMultiplier`：奔跑加速度倍率
  - `sprintMode`：奔跑模式
  - `sprintTransitionSpeed`：速度过渡平滑度

### 2. InputManager.cs
**新增内容**：
- Sprint输入状态变量
- Sprint输入事件：`OnSprintPressed`、`OnSprintReleased`
- Sprint输入属性：`SprintPressed`、`SprintHeld`
- Sprint输入回调处理

### 3. PlayerInput.cs
**新增内容**：
- Sprint输入过滤支持
- Sprint输入属性访问
- Sprint输入启用/禁用控制
- 调试信息显示

### 4. RBPlayerMotor.cs
**新增内容**：
- 奔跑状态变量：`_isSprinting`、`_sprintToggled`、`_currentSpeedMultiplier`
- 奔跑逻辑处理：`HandleSprintInput()`
- 动态速度计算：`GetCurrentMaxSpeed()`
- 奔跑状态调试信息

## 输入配置

### 键盘&鼠标
- **Sprint键**：Left Shift

### 手柄
- **Sprint键**：East Button（通常是B键或Circle键）

## 使用方法

### 1. 设计师配置
在 MovementTuningSO 资源中可以调整：
- 奔跑速度倍率（推荐1.5-2.0）
- 奔跑加速度倍率（推荐1.0-1.5）
- 奔跑模式（Hold推荐用于竞技游戏，Toggle推荐用于探索游戏）
- 过渡速度（影响加速和减速的平滑度）

### 2. 程序员接口
```csharp
// 检查是否在奔跑
bool isSprinting = playerMotor.IsSprinting;

// 获取当前速度倍率
float speedMultiplier = playerMotor.CurrentSpeedMultiplier;

// 启用/禁用奔跑输入
playerInput.SetSprintEnabled(false);
```

## 技术特点

### 1. 符合现有架构
- 遵循输入管理器的事件驱动模式
- 继承MovementTuningSO的数据驱动设计
- 保持RBPlayerMotor的物理驱动原则

### 2. 性能优化
- 平滑的速度过渡避免突变
- 只在必要时计算奔跑状态
- 复用现有的地面检测逻辑

### 3. 调试支持
- 完整的调试信息显示
- 实时状态监控
- Inspector中的参数验证

## 测试建议

### 1. 基本功能测试
- [ ] Hold模式：按住Shift键能否正常奔跑
- [ ] Toggle模式：按下Shift键能否切换奔跑状态
- [ ] 空中限制：跳跃时是否自动停止奔跑
- [ ] 停止移动：不按WASD时是否停止奔跑

### 2. 参数调整测试
- [ ] 不同速度倍率的感受
- [ ] 不同加速度倍率的响应性
- [ ] 过渡速度的平滑度

### 3. 边界情况测试
- [ ] 快速切换奔跑状态
- [ ] 在斜坡上奔跑
- [ ] 与跳跃功能的配合

## 未来扩展建议

### 1. 耐力系统
- 添加奔跑耐力条
- 耐力耗尽时强制停止奔跑
- 耐力恢复机制

### 2. 音效支持
- 奔跑开始/停止音效
- 不同表面的奔跑音效
- 脚步声频率调整

### 3. 动画系统
- 奔跑动画状态
- 速度混合树
- 过渡动画

### 4. 网络支持
- 奔跑状态同步
- 客户端预测
- 服务器验证

## 注意事项

1. **性能**：奔跑状态检查在每帧进行，但计算量很小
2. **兼容性**：与现有移动系统完全兼容
3. **扩展性**：易于添加更多奔跑相关功能
4. **调试**：提供完整的调试信息用于问题排查

## 总结

本奔跑功能实现完全遵循了项目的现有架构和编码规范，提供了灵活的配置选项和良好的游戏体验。通过数据驱动的设计，设计师可以轻松调整奔跑参数以达到理想的游戏手感。
