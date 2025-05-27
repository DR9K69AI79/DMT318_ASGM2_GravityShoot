# RBPlayerMotor 优化总结

## 优化目标
根据建议修复 `RBPlayerMotor.cs` 中的视觉/物理问题：
- **问题1**: 玩家Y轴抖动（尤其是在球形重力下）
- **问题2**: 地面检测导致玩家浮空

## 主要修改

### 1. 新增地面吸附偏移参数
```csharp
[SerializeField] private float _groundSnapOffset = 0.02f; // 微小的离地偏移，防止穿模
```
- **作用**: 防止玩家因为贴地检测而浮空
- **默认值**: 0.02m，可在Inspector中调整

### 2. 优化重力轴向对齐 (`UpdateGravity()`)
```csharp
Vector3 targetUpAxis = newUpAxisFromCustomGravity;
// 当在地面且非陡坡时，优先使用实际的地面法线作为目标"上"方向
if (_onGround && !_onSteep && _groundNormal != Vector3.zero) 
{
    targetUpAxis = _groundNormal;
}
```
- **解决问题**: Y轴抖动
- **原理**: 着地时让 `_upAxis` 与地面法线对齐，而不是仅依赖全局重力方向
- **效果**: 减少了在球形重力下角色本地坐标系与实际表面不一致导致的抖动

### 3. 增强空值检查
```csharp
if (_tuning != null && _tuning.turnResponsiveness > 0f)
{
    _upAxis = Vector3.Slerp(_upAxis, targetUpAxis, _tuning.turnResponsiveness * Time.fixedDeltaTime).normalized;
}
else if (_tuning != null)
{
    _upAxis = targetUpAxis; // 立即吸附
}
```
- **作用**: 防止空引用异常，提高代码健壮性

### 4. 修正贴地检测 (`SnapToGround()`)
```csharp
// 修改前：可能导致浮空
transform.position = hit.point + _upAxis * _groundCheckRadius;

// 修改后：正确的脚部位置
transform.position = hit.point + _upAxis * _groundSnapOffset;
```
- **解决问题**: 玩家浮空
- **原理**: 将玩家脚部定位到地面上方一个微小偏移，而不是 `_groundCheckRadius` 的距离
- **新增**: 立即更新 Rigidbody 速度以停止下沉运动

### 5. 启用角色旋转对齐
```csharp
// 在 FixedUpdate 中添加
AlignRotation();
```
- **作用**: 让角色旋转平滑对齐到重力方向
- **效果**: 改善在重力变化区域的视觉表现

### 6. 改进调试可视化
```csharp
// 绘制地面法线 (当在地面上时)
if (_onGround && _groundNormal != Vector3.zero)
{
    Gizmos.color = Color.white;
    Gizmos.DrawLine(transform.position, transform.position + _groundNormal * 1.8f);
}
```
- **新增**: 白色线条显示地面法线
- **区分**: 绿色 = `_upAxis`，白色 = `_groundNormal`
- **便于**: 调试轴向对齐问题

### 7. 增强调试信息
```csharp
GUILayout.Label($"On Steep: {_onSteep}");
```
- **新增**: 显示是否在陡坡上
- **作用**: 帮助调试坡度相关的问题

### 8. 参数验证优化
```csharp
private void OnValidate()
{
    _groundCheckRadius = Mathf.Max(0.01f, _groundCheckRadius);
    _groundSnapOffset = Mathf.Max(0.001f, _groundSnapOffset);
}
```
- **新增**: 对 `_groundSnapOffset` 的验证
- **作用**: 确保参数在合理范围内

## 技术原理

### Y轴抖动的根因
- **问题**: `_upAxis`（来自全局重力）与 `_groundNormal`（来自局部碰撞）可能略有不同
- **后果**: 在投影速度、施加力时产生不一致，导致抖动
- **解决**: 着地时优先使用地面法线作为 `_upAxis`

### 浮空问题的根因
- **问题**: `SnapToGround()` 中使用了错误的偏移量 `_groundCheckRadius`
- **后果**: 玩家脚部被放置在地面上方过高的位置
- **解决**: 使用专门的微小偏移量 `_groundSnapOffset`

## 预期效果

1. **减少抖动**: 在球形重力源周围移动时更加平滑
2. **消除浮空**: 玩家贴地更自然，不会悬浮
3. **改善手感**: 移动更加稳定和可预测
4. **增强调试**: 更容易识别和解决相关问题

## 使用建议

1. **调整 `_groundSnapOffset`**: 根据角色模型大小调整（建议 0.01-0.05m）
2. **观察调试线条**: 
   - 绿色线（`_upAxis`）应与白色线（`_groundNormal`）尽可能对齐
   - 在平地上两条线应该重合
3. **测试不同重力源**: 确保在各种重力环境下都能正常工作
4. **监控性能**: 新增的对齐计算对性能影响很小，但可在必要时禁用 `AlignRotation()`

## 兼容性

- ✅ 保持现有API不变
- ✅ 向后兼容所有调参配置
- ✅ 不影响现有游戏逻辑
- ✅ 可通过Inspector轻松调整新参数
