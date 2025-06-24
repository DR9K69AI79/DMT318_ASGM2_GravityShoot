# ShotGun 子弹不飞出问题修复指南

## 问题描述
ShotGun的子弹生成后固定在原位置不飞出。

## 已识别的问题和修复方案

### 1. 重力设置问题 ✅ 已修复
**问题**: ShotGun.asset中`_useGravity: 0`，导致投射物没有重力
**修复**: 已将`_useGravity`改为`1`

### 2. 投射物调试工具
创建了`ProjectileDebugger.cs`脚本，用于诊断投射物问题。

## 其他可能的问题和检查点

### 3. 投射物预制体检查
需要检查投射物预制体(`Bullet01.prefab`或类似)是否有以下问题：
- Rigidbody组件的`isKinematic`是否设为`true`
- Collider组件是否设置正确
- 是否有不必要的约束(constraints)

### 4. CustomGravityRigidbody组件冲突
如果投射物预制体上有`CustomGravityRigidbody`组件，它会：
- 在Awake中设置`body.useGravity = false`
- 在FixedUpdate中应用自定义重力

**建议**: 检查投射物预制体是否需要这个组件，或者确保它与ProjectileSettings正确配合。

### 5. 网络同步问题
在多人游戏中，确保：
- 武器拥有者正确创建投射物
- 网络数据传输正确
- 非拥有者正确接收投射物

### 6. 速度计算问题
检查以下代码路径的速度设置：
- `ProjectileBase.Launch()`
- `ProjectileBase.Configure()`
- `StandardProjectile.ConfigureFromNetworkData()`

## 调试步骤

### 1. 使用ProjectileDebugger
1. 将`ProjectileDebugger.cs`脚本添加到投射物预制体
2. 发射武器观察控制台输出
3. 使用脚本的Context Menu功能进行调试

### 2. 检查投射物预制体
打开投射物预制体，确认：
```
Rigidbody设置:
- Mass: 1 (或合理值)
- Drag: 0 (或很小的值)
- Angular Drag: 0.05 (默认值)
- Use Gravity: true (如果不使用自定义重力)
- Is Kinematic: false
- Constraints: None (或必要的约束)
```

### 3. 检查武器配置
在ShotGun.asset中确认：
```yaml
_projectileSettings:
  _speed: 20 (或更高值)
  _useGravity: 1
  _gravityScale: 1
  _mass: 0.5 (合理值)
  _drag: 0.1 (较小值)
```

### 4. 运行时检查
在游戏运行时，选择生成的投射物GameObject：
1. 查看Inspector中的Rigidbody组件
2. 检查速度值是否为零
3. 确认物理约束设置

## 快速修复检查清单

- [ ] ShotGun.asset中`_useGravity`设为1 ✅
- [ ] 投射物预制体Rigidbody的`isKinematic`为false
- [ ] 投射物预制体没有过大的drag值
- [ ] 投射物预制体没有不必要的物理约束
- [ ] 确认投射物速度计算正确
- [ ] 检查是否有CustomGravityRigidbody组件冲突
- [ ] 验证网络同步设置

## 如果问题仍然存在

1. 在投射物预制体上添加ProjectileDebugger脚本
2. 发射武器并观察控制台输出
3. 使用"显示详细状态"Context Menu
4. 检查输出中的异常值或警告

## 常见解决方案

### 方案1: 重置投射物物理
如果投射物卡住，在运行时选择投射物GameObject，在ProjectileDebugger上使用"重置物理状态"。

### 方案2: 手动测试速度
使用ProjectileDebugger的"设置测试速度"来验证物理系统是否正常工作。

### 方案3: 检查层级冲突
确保投射物和环境对象在正确的物理层级上，没有意外的碰撞导致投射物卡住。
