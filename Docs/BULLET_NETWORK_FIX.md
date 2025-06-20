# 网络投射物销毁错误修复报告

## 问题描述

错误信息：`Ev Destroy Failed. Could not find PhotonView with instantiationId 1030. Sent by actorNr: 1`

这个错误通常出现在连续开枪生成大量子弹时，是 Photon Unity Networking (PUN) 中常见的网络对象销毁时序问题。

## 问题原因

1. **时序问题**：快速射击时，大量投射物被创建和销毁
2. **网络延迟**：销毁事件可能比预期晚到达
3. **对象重复销毁**：当网络销毁事件到达时，目标对象可能已经被本地销毁

## 解决方案

### 1. 安全的网络销毁机制

**修改文件**: `ProjectileNetworkSync.cs`
- 添加延迟销毁机制，确保RPC先发送
- 使用协程避免立即销毁本地对象

```csharp
public void RequestDestroy()
{
    if (photonView.IsMine && !_networkIsDestroyed)
    {
        _networkIsDestroyed = true;
        photonView.RPC("OnProjectileDestroyRPC", RpcTarget.All);
        StartCoroutine(DelayedLocalDestroy());
        LogNetwork("请求销毁投射物");
    }
}

private IEnumerator DelayedLocalDestroy()
{
    yield return null; // 等待一帧确保RPC发送
    DestroyProjectile();
}
```

### 2. 投射物销毁异常处理

**修改文件**: `ProjectileBase.cs`
- 添加 try-catch 异常处理
- 在网络销毁失败时回退到本地销毁

```csharp
public virtual void DestroyProjectile()
{
    if (_isDestroyed) return;
    
    _isDestroyed = true;
    
    if (photonView != null && photonView.IsMine)
    {
        if (photonView.ViewID != 0)
        {
            try
            {
                PhotonNetwork.Destroy(gameObject);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[投射物] 网络销毁失败，改为本地销毁: {e.Message}");
                Destroy(gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
```

### 3. 投射物数量限制

**修改文件**: `ProjectileWeapon.cs`
- 添加每个玩家最大网络投射物数量限制
- 防止投射物滥用导致的网络拥堵

```csharp
[Header("网络限制")]
[SerializeField] protected int _maxNetworkProjectilesPerPlayer = 20;
[SerializeField] protected float _projectileSpamCheckInterval = 0.1f;

protected virtual void CreateProjectile(Vector3 position, Vector3 velocity, Vector3 direction)
{
    // 网络投射物限制检查
    if (_weaponData.SyncProjectiles && photonView != null && photonView.IsMine)
    {
        int currentNetworkProjectiles = CountPlayerNetworkProjectiles();
        if (currentNetworkProjectiles >= _maxNetworkProjectilesPerPlayer)
        {
            Debug.LogWarning("达到网络投射物限制，跳过创建");
            return;
        }
    }
    // ...
}
```

### 4. 投射物管理器（可选优化）

**新增文件**: `ProjectileManager.cs`
- 集中管理所有投射物的生命周期
- 批量网络同步以减少网络开销
- 自动清理无效投射物

## 配置建议

1. **武器射击频率**：确保武器的 `FireInterval` 设置合理（建议不小于 0.1 秒）
2. **投射物生命周期**：设置合理的投射物 `lifetime`（建议 5-10 秒）
3. **网络同步频率**：调整 PhotonView 的发送频率（建议 20-30 Hz）

## 测试验证

1. 连续快速射击 30 秒以上
2. 多个玩家同时快速射击
3. 检查控制台是否还有销毁错误
4. 监控网络投射物数量

## 性能影响

- 略微增加内存使用（协程和异常处理）
- 减少网络错误和不必要的重复销毁
- 提高网络稳定性

## 注意事项

1. 这些修改是向后兼容的
2. 不会影响现有的投射物行为
3. 错误处理是保守的，优先保证游戏稳定性
