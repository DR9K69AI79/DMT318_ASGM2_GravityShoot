# PlayerView 重力适配俯仰角限制系统增强

## 概述

本文档描述了PlayerView中基于重力方向的自适应俯仰角限制系统的完整实现。该系统确保在任意重力环境下，视角限制都能正确工作，并在重力方向变化时平滑过渡。

## 核心特性

### 1. 重力方向自适应
- **动态重力轴检测**：实时监测PlayerMotor的UpAxis变化
- **自动重新计算**：重力变化时自动重新计算所有方向向量
- **平滑过渡**：尽量保持用户的视角意图，在新重力系统下寻找最接近的有效方向

### 2. 精确的俯仰角计算
- **统一计算方法**：`IsWithinPitchLimits`和`CalculateCurrentPitch`使用相同的数学模型
- **特殊情况处理**：正确处理完全垂直（90°/-90°）的视角方向
- **数值稳定性**：使用容差值避免浮点精度问题

### 3. 智能边界处理
- **极值检测**：正确处理接近±90度的俯仰角
- **零向量保护**：当投影结果为零向量时的后备方案
- **右手坐标系**：确保所有计算遵循一致的坐标系约定

## 关键实现

### 重力变化检测
```csharp
private void CheckGravityUpdate()
{
    Vector3 currentGravityUp = _motor.UpAxis;
    float gravityChangeMagnitude = Vector3.Distance(_lastKnownGravityUp, currentGravityUp);
    
    if (gravityChangeMagnitude > 0.01f) // 重力方向变化超过阈值
    {
        // 保存当前俯仰角意图
        float oldPitch = CalculateCurrentPitchForGravity(oldAimDirection, _lastKnownGravityUp);
        
        // 更新重力轴并重新计算所有方向
        _lastKnownGravityUp = currentGravityUp;
        // ... 重新适配逻辑
    }
}
```

### 精确的俯仰角限制
```csharp
private bool IsWithinPitchLimits(Vector3 direction)
{
    Vector3 gravityUpAxis = _motor.UpAxis;
    Vector3 directionOnPlane = Vector3.ProjectOnPlane(direction, gravityUpAxis).normalized;
    
    // 特殊处理完全垂直的情况
    if (directionOnPlane.magnitude < 0.001f)
    {
        float upDot = Vector3.Dot(direction, gravityUpAxis);
        if (upDot > 0.999f) return _maxPitchUp >= 89.9f;
        if (upDot < -0.999f) return _maxPitchDown >= 89.9f;
    }
    
    // 计算俯仰角并检查限制
    Vector3 rightAxis = Vector3.Cross(gravityUpAxis, directionOnPlane).normalized;
    float pitchAngle = Vector3.SignedAngle(directionOnPlane, direction, rightAxis);
    
    return pitchAngle >= -_maxPitchDown && pitchAngle <= _maxPitchUp;
}
```

### 意图保持机制
```csharp
// 尝试保持相同的俯仰角意图
Vector3 rightAxis = Vector3.Cross(currentGravityUp, horizontalBase).normalized;
Vector3 newAimDirection = RotateVectorAroundAxis(horizontalBase, rightAxis, oldPitch);

// 检查新方向是否在限制范围内
if (IsWithinPitchLimits(newAimDirection))
{
    _currentAimDirection = newAimDirection.normalized;
}
else
{
    // 超出限制时使用水平方向作为安全后备
    _currentAimDirection = horizontalBase;
}
```

## 数学原理

### 俯仰角计算
1. **投影到重力水平面**：`Vector3.ProjectOnPlane(direction, gravityUpAxis)`
2. **构建右轴**：`Vector3.Cross(gravityUpAxis, directionOnPlane)`
3. **计算有符号角度**：`Vector3.SignedAngle(水平投影, 原方向, 右轴)`

### 坐标系约定
- **重力轴**：当前重力方向的"上"轴
- **水平面**：垂直于重力轴的平面
- **右轴**：按右手法则定义的水平面内的右轴
- **俯仰角**：正值向上，负值向下

## 配置参数

### Inspector可配置
- `_maxPitchUp`：向上看的最大角度（0-90度）
- `_maxPitchDown`：向下看的最大角度（0-90度）
- `_showDebugInfo`：启用详细的调试日志输出

### 内部阈值
- 重力变化检测阈值：0.01f
- 零向量检测阈值：0.001f
- 垂直角检测阈值：0.999f

## 调试功能

### 实时信息显示
- 当前俯仰角
- 重力上轴方向
- 俯仰角限制范围
- 各种方向向量

### 新增公共接口
- `ForceGravityUpdate()`：强制重新适配重力
- `GetCurrentPitchLimits()`：获取当前限制范围
- `GetPitchNormalized()`：获取归一化的俯仰角(-1到1)

## 使用场景

### 标准重力环境
- 重力向下（Vector3.down）
- 传统的上下俯仰限制

### 自定义重力环境
- 任意方向的重力
- 动态变化的重力方向
- 球形重力场、旋转重力等

### 重力过渡
- 重力场之间的平滑过渡
- 传送门等瞬间重力变化
- 旋转平台上的重力重定向

## 性能考虑

### 优化措施
- 重力变化检测使用距离阈值避免每帧重计算
- 调试信息按帧间隔输出
- 数学运算优化，避免不必要的归一化

### 复杂度
- 重力变化检测：O(1)
- 俯仰角限制检查：O(1)
- 向量重新适配：O(1)

## 总结

这个增强版的重力适配俯仰角限制系统提供了：

1. **完全的重力方向独立性** - 在任意重力环境下都能正确工作
2. **平滑的重力过渡** - 重力变化时保持用户体验的连续性
3. **数值稳定性** - 处理各种边界情况和数值精度问题
4. **可配置性** - 通过Inspector轻松调整限制参数
5. **调试友好** - 丰富的调试信息和可视化选项

该系统为复杂的3D重力环境提供了可靠的视角控制基础，确保玩家在任何重力条件下都能获得直观、可控的第一人称体验。
