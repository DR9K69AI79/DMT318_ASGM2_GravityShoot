# NetworkTestHelper 使用指南

## 🎯 主要改进

### 1. **房间同步问题已修复**
- 默认关闭了随机房间名功能 (`_useRandomRoomName = false`)
- 所有客户端现在会加入同一个房间："TestRoom"
- 如需自定义房间名，请修改 `_testRoomName` 字段

### 2. **生成点配置简化**
- 移除了自动生成逻辑
- 现在完全依赖手动配置的 `_spawnPoints` 数组
- 添加了详细的错误提示和配置验证

## 🛠️ 配置步骤

### 1. 基本配置
1. 在Scene中创建一个GameObject，添加 `NetworkTestHelper` 组件
2. 设置 `Player Prefab` - 必须包含 `PhotonView` 组件
3. 在Scene中创建生成点GameObject，并将它们拖拽到 `Spawn Points` 数组中

### 2. 生成点设置
```
推荐做法：
1. 创建空GameObject作为生成点
2. 将这些GameObject放置在合适的位置
3. 将它们拖拽到NetworkTestHelper的 Spawn Points数组中
4. 确保数组中没有null引用
```

### 3. 验证配置
- 在Inspector中右键点击 `NetworkTestHelper` 组件
- 选择 "验证网络测试配置"
- 检查控制台输出，确保所有配置正确

## 🎮 测试流程

### 多客户端测试：
1. 构建应用程序
2. 启动多个客户端实例
3. 每个客户端按 `F1` 快速加入房间
4. 按 `F3` 生成玩家
5. 所有玩家应该出现在同一个房间中

### 调试快捷键：
- `F1`: 快速加入房间
- `F2`: 离开房间  
- `F3`: 生成玩家

## ⚠️ 常见问题解决

### 问题1: "所有配置的生成点都为null"
**解决**: 检查Spawn Points数组，确保所有元素都指向有效的Transform

### 问题2: "玩家预制体缺少PhotonView组件"
**解决**: 为玩家预制体添加PhotonView组件，并正确配置

### 问题3: 多个客户端进入不同房间
**解决**: 确保 `_useRandomRoomName` 设置为 `false`

### 问题4: NullReferenceException
**解决**: 运行配置验证，检查所有引用是否正确设置

## 📝 配置检查清单

- [ ] Player Prefab 已设置且包含PhotonView
- [ ] Spawn Points 数组已填充且无null引用
- [ ] _useRandomRoomName 设置为 false（用于测试）
- [ ] 运行了配置验证且无错误
- [ ] PhotonServerSettings 中的AppId正确配置
