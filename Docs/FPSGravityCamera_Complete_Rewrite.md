# FPSGravityCamera 完全重构文档

## 概述
完全重写了 `FPSGravityCamera.cs`，专注于提供高质量的FPS视角控制体验。新版本解决了重力对齐时的额外旋转问题，并实现了精确的偏航角和俯仰角控制。

## 核心设计理念

### 🎯 分离关注点
- **偏航角（Yaw）**：控制角色身体的水平旋转
- **俯仰角（Pitch）**：控制摄像机的垂直视角
- **重力对齐**：完全委托给RBPlayerMotor处理

### 🎮 优质FPS手感
- 精确的鼠标输入处理
- 可选的视角平滑
- 合理的垂直视角限制
- 支持Y轴反转
- 武器后坐力等视角冲击效果

## 关键特性

### 1. 精确的鼠标控制
```csharp
// 处理水平旋转（偏航角）
_targetYaw += mouseInput.x;

// 处理垂直旋转（俯仰角）
float pitchInput = _invertY ? mouseInput.y : -mouseInput.y;
_targetPitch += pitchInput;
```

### 2. 智能的旋转应用
```csharp
// 偏航角控制角色身体旋转（考虑重力Up轴）
Vector3 upAxis = _playerMotor != null ? _playerMotor.UpAxis : Vector3.up;
Quaternion yawRotation = Quaternion.AngleAxis(_yaw, upAxis);
transform.rotation = yawRotation;

// 俯仰角只应用到摄像机本身
_playerCamera.transform.localRotation = Quaternion.AngleAxis(_pitch, Vector3.right);
```

### 3. 可选的视角平滑
```csharp
if (_enableSmoothing)
{
    // 平滑插值到目标角度
    float smoothing = _smoothingFactor * Time.deltaTime;
    _yaw = Mathf.LerpAngle(_yaw, _targetYaw, smoothing);
    _pitch = Mathf.Lerp(_pitch, _targetPitch, smoothing);
}
```

## 配置参数

### 鼠标视角控制
- `_mouseSensitivity`: 鼠标灵敏度（默认2.0）
- `_verticalLookLimit`: 垂直视角限制（默认90度）
- `_invertY`: Y轴反转（默认false）

### 视角平滑
- `_enableSmoothing`: 启用平滑（默认false，追求响应性）
- `_smoothingFactor`: 平滑系数（默认10.0）

### 高级设置
- `_lockCursorOnStart`: 开始时锁定光标（默认true）

## 公共API

### 基础控制
```csharp
// 设置参数
SetMouseSensitivity(float sensitivity)
SetInvertY(bool invert)
SetVerticalLimit(float limit)

// 视角操作
ResetView()
AddViewKick(float yawKick, float pitchKick)
AddViewKick(Vector2 kickAmount)

// 光标控制
SetCursorLock(bool locked)
```

### 查询接口
```csharp
// 获取视角信息
Ray GetViewRay()
bool IsInView(Vector3 worldPosition, float maxAngle = 60f)

// 访问器属性
float Yaw { get; }
float Pitch { get; }
Vector3 CameraForward { get; }
Vector3 CameraRight { get; }
Vector3 CameraUp { get; }
```

## 解决的问题

### ❌ 原有问题
1. **重力对齐时的额外旋转**：导致玩家视角被动改变
2. **复杂的重力处理逻辑**：增加了不必要的复杂性
3. **不完整的偏航角控制**：只处理俯仰角，缺少水平控制
4. **手感不一致**：水平和垂直控制响应不匹配

### ✅ 解决方案
1. **分离旋转控制**：偏航角控制角色，俯仰角控制摄像机
2. **委托重力处理**：让RBPlayerMotor完全处理重力对齐
3. **完整的FPS控制**：同时处理偏航和俯仰角
4. **统一的输入处理**：确保水平和垂直控制手感一致

## 性能优化

### 高效的更新循环
```csharp
private void LateUpdate()
{
    ProcessMouseInput();    // 处理输入
    UpdateViewAngles();     // 更新角度
    ApplyCameraTransform(); // 应用变换
    
    // 调试信息（可选）
    if (_showDebugInfo) UpdateDebugInfo();
}
```

### 最小化计算
- 避免不必要的三角函数调用
- 重用Vector3和Quaternion计算
- 条件性调试输出

## 调试支持

### 信息输出
- 当前偏航角和俯仰角
- 重力Up轴状态
- 摄像机朝向向量
- 角色接地状态

### 可视化调试
- 摄像机位置和朝向（Gizmos）
- 角色重力Up轴
- 摄像机坐标系

## 使用建议

### 1. 基础设置
```csharp
// 在Inspector中设置
mouseSensitivity = 2.0f;
verticalLookLimit = 90f;
invertY = false;
```

### 2. 性能考虑
```csharp
// 对于竞技游戏，关闭平滑以获得最佳响应性
enableSmoothing = false;

// 对于休闲游戏，可以启用平滑
enableSmoothing = true;
smoothingFactor = 10f;
```

### 3. 特效集成
```csharp
// 武器后坐力
fpsCamera.AddViewKick(0f, -2f); // 向上冲击

// 爆炸冲击
fpsCamera.AddViewKick(Random.Range(-1f, 1f), Random.Range(-0.5f, 0.5f));
```

## 向前兼容性
- 保持所有重要的公共接口
- 参数名称更清晰但功能相同
- 新增的功能都有合理的默认值

## 测试要点
1. ✅ 鼠标输入响应性和精确度
2. ✅ 重力方向变化时的稳定性
3. ✅ 垂直视角限制的正确性
4. ✅ Y轴反转功能
5. ✅ 视角冲击效果
6. ✅ 不同灵敏度设置下的手感

这个重写版本专注于FPS游戏的核心需求：精确、响应和稳定的视角控制。
