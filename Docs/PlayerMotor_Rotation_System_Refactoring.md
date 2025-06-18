# PlayerMotor 旋转系统重构 - 分离重力对齐与视角旋转

## 重构概述

将原来混合在一起的 `AlignRotationToGravity()` 方法分解为两个独立且职责明确的方法：

### 1. `AlignToGravity()` - 重力对齐
**职责：** 确保角色的Up轴始终与当前重力方向对齐
**特点：**
- 这是基础对齐，优先级最高
- 不涉及任何视角相关的旋转
- 保持角色在重力场中的正确姿态

### 2. `ApplyViewRotation()` - 视角旋转对齐  
**职责：** 在重力对齐的基础上应用视角控制的旋转
**特点：**
- 只影响绕UpAxis的旋转（Yaw）
- 在重力对齐之后执行，不会干扰重力系统
- 支持向量版本和四元数版本的输入

## 执行顺序

```csharp
// FixedUpdate 中的执行顺序
ClearState();

// 1. 首先进行重力对齐 - 确保角色与重力场正确对齐
AlignToGravity();

// 2. 然后应用视角旋转 - 在重力对齐基础上进行Yaw旋转
ApplyViewRotation();
```

## 核心优势

### 🎯 **职责清晰分离**
- **重力对齐**：处理角色在3D重力场中的基础姿态
- **视角旋转**：处理玩家输入驱动的方向控制

### 🔧 **避免旋转冲突**
- 重力对齐先确定基础旋转
- 视角旋转在此基础上只做Yaw调整
- 不会相互覆盖或产生意外的旋转

### 🎮 **支持多种输入方式**
- **向量版本**：`SetTargetYawDirection(Vector3)` - 推荐使用
- **角度版本**：`SetTargetYaw(float)` - 精确控制
- **四元数版本**：`SetTargetRotation(Quaternion)` - 兼容性支持

### 🚀 **性能优化**
- 每种旋转类型只在需要时计算
- 避免复杂的旋转分解和重组
- 更直观的向量运算

## 方法详解

### AlignToGravity()

```csharp
private void AlignToGravity()
{
    // 1. 获取当前在水平面的前向方向
    Vector3 currentForward = Vector3.ProjectOnPlane(_rb.rotation * Vector3.forward, _upAxis).normalized;
    
    // 2. 处理无效方向的情况
    if (currentForward == Vector3.zero) {
        // 使用相机方向或世界前向作为后备
    }
    
    // 3. 创建仅考虑重力对齐的旋转
    Quaternion gravityAlignedRotation = Quaternion.LookRotation(currentForward, _upAxis);
    
    // 4. 平滑应用
    _rb.MoveRotation(Quaternion.Slerp(current, target, speed));
}
```

**关键点：**
- 只关心保持Up轴与重力对齐
- 不受视角输入影响
- 为视角旋转提供稳定的基础

### ApplyViewRotation()

```csharp
private void ApplyViewRotation()
{
    if (_hasTargetYaw) {
        // 1. 计算当前和目标的水平前向方向
        Vector3 currentForward = Vector3.ProjectOnPlane(_rb.rotation * Vector3.forward, _upAxis);
        Vector3 targetForward = Quaternion.AngleAxis(_targetYaw, _upAxis) * referenceForward;
        
        // 2. 计算绕UpAxis的旋转差异
        Quaternion yawRotation = Quaternion.FromToRotation(currentForward, targetForward);
        
        // 3. 在当前旋转基础上应用Yaw调整
        Quaternion targetViewRotation = yawRotation * _rb.rotation;
        
        // 4. 平滑应用
        _rb.MoveRotation(Quaternion.Slerp(current, target, speed));
    }
}
```

**关键点：**
- 只影响Yaw旋转，保持重力对齐
- 基于当前旋转状态进行调整
- 支持多种输入格式

## 与 PlayerView 的配合

### 向量版本 PlayerView (推荐)
```csharp
// PlayerView 中
_motor.SetTargetYawDirection(_bodyForwardDirection);
```

### 传统版本 PlayerView
```csharp
// PlayerView 中  
_motor.SetTargetYaw(_currentBodyYaw);
```

## 调试和验证

可以通过以下方式验证分离是否正确工作：

1. **重力对齐测试**：在不同重力方向下，角色应该正确对齐
2. **视角旋转测试**：视角旋转不应影响重力对齐
3. **复合测试**：同时改变重力和视角，两者应该独立工作

## 总结

这个重构实现了：

✅ **清晰的职责分离** - 重力 vs 视角  
✅ **避免旋转冲突** - 顺序执行，互不干扰  
✅ **支持向量运算** - 更直观的PlayerView集成  
✅ **保持兼容性** - 旧版本PlayerView仍然工作  
✅ **性能优化** - 减少不必要的旋转计算  

这为复杂的重力环境和精确的FPS控制提供了坚实的基础。
