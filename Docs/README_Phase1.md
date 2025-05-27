# 重力系统开发文档

## 第一阶段完成 - 单机验证

本阶段已完成的核心功能：

### 1. 重力系统框架
- **GravitySource** - 抽象重力源基类
- **GravitySphere** - 球形行星重力（支持平方反比定律）
- **GravityPlane** - 平面重力场
- **GravityBox** - 盒状重力区域
- **CustomGravity** - 静态重力管理器

### 2. 角色控制系统
- **PlayerInput** - 输入处理（WASD移动，鼠标视角，空格跳跃）
- **PlayerMotor** - 基于自定义重力的角色运动控制
- **CameraRig** - 重力环境下的第一人称相机系统

### 3. 开发工具
- **GravitySystemEditor** - Unity Editor工具，快速创建测试场景
- **GravityTestManager** - 运行时调试和测试管理器

## 快速开始

### 1. 创建测试场景
在Unity Editor中：
1. 打开菜单 `Tools > Gravity System > Setup Test Scene`
2. 这将自动创建：
   - 玩家角色（带有所有必要组件）
   - 主行星（半径10m）
   - 小行星（半径5m）
   - 重力平面

### 2. 手动创建玩家
如果需要单独创建玩家：
1. 使用菜单 `Tools > Gravity System > Create Player`
2. 或者手动在GameObject上添加：
   - `CharacterController`
   - `PlayerInput`
   - `PlayerMotor`
   - `CameraRig`

### 3. 创建重力源
使用编辑器菜单：
- `Tools > Gravity System > Create Gravity Sphere` - 球形重力
- `Tools > Gravity System > Create Gravity Plane` - 平面重力
- `Tools > Gravity System > Create Gravity Box` - 盒状重力

## 控制说明

### 基础控制
- **WASD** - 移动
- **鼠标** - 视角控制
- **空格** - 跳跃

### 调试快捷键
- **G** - 切换重力矢量显示
- **R** - 重置玩家位置
- **T** - 切换调试信息UI

## 核心特性

### 1. 多重重力叠加
系统支持多个重力源同时作用，自动计算累积重力向量。

### 2. 平滑重力对齐
角色会平滑地对齐到重力方向，支持在不同行星间无缝移动。

### 3. 物理真实感
- 支持平方反比定律的引力计算
- 基于物理的跳跃和移动
- 准确的地面检测和着陆

### 4. 视角稳定
第一人称相机在重力变化时保持稳定，避免眩晕感。

## 参数配置

### GravitySphere（球形重力）
- **Gravity** - 重力强度 (m/s²)
- **Radius** - 影响半径 (m)
- **Use Inverse Square** - 是否使用平方反比定律

### GravityPlane（平面重力）
- **Gravity** - 重力强度 (m/s²)
- **Range** - 影响距离 (m)

### GravityBox（盒状重力）
- **Gravity** - 重力向量 (m/s²)
- **Size** - 盒子尺寸 (m)

### PlayerMotor（玩家运动）
- **Move Speed** - 移动速度 (m/s)
- **Jump Speed** - 跳跃初速度 (m/s)
- **Acceleration** - 加速度 (m/s²)
- **Air Control** - 空中控制系数 (0-1)
- **Align Speed** - 重力对齐速度

## 调试功能

### 可视化
- Scene视图中显示重力范围（选中重力源时）
- Gizmos显示重力方向和强度
- 运行时重力矢量显示

### 调试信息
GravityTestManager提供实时调试信息：
- 玩家位置和状态
- 当前重力向量
- 运动参数
- 重力源数量

## 性能考虑

- 重力计算在FixedUpdate中执行（50Hz）
- 支持运行时动态添加/移除重力源
- 自动清理无效的重力源引用

## 下一阶段

第二阶段将添加：
- 武器系统 (WeaponBase, Projectile, Explosion)
- 网络同步 (NetworkPlayer)
- 基础UI系统

## 故障排除

### 常见问题

1. **玩家不受重力影响**
   - 检查是否有GravitySource在场景中
   - 确认重力源的影响半径包含玩家位置
   - 查看CustomGravity.SourceCount是否 > 0

2. **移动不平滑**
   - 调整PlayerMotor的Acceleration参数
   - 检查FixedUpdate频率设置

3. **视角不稳定**
   - 调整CameraRig的Align Speed
   - 确认鼠标灵敏度设置合理

4. **Input System错误**
   - 确保安装了Input System包
   - 检查PlayerInputActions配置

## 技术细节

### 重力计算
```csharp
// 获取累积重力
Vector3 gravity = CustomGravity.GetGravity(position, out Vector3 upAxis);

// 获取最强重力源的上轴
Vector3 upAxis = CustomGravity.GetUpAxis(position);
```

### 自定义重力源
创建新的重力类型只需继承GravitySource：
```csharp
public class MyGravitySource : GravitySource
{
    public override Vector3 GetGravity(Vector3 position)
    {
        // 实现自定义重力计算
        return customGravityVector;
    }
}
```
