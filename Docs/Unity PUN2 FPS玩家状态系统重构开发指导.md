# Unity PUN2 FPS玩家状态系统重构开发指导

## 1. 引言

本文档旨在为开发人员提供一份详细的Unity PUN2 FPS玩家状态系统重构指导。基于对现有GitHub项目代码的分析以及《Unity PUN2 FPS玩家状态系统重构方案》PDF文档的深入理解，本文将重构任务结构化为一系列可执行的步骤，以确保重构过程的顺利进行和最终系统的稳定高效。

## 2. 现有系统分析

当前项目的玩家状态同步逻辑存在分散和重复的问题，主要体现在以下几个方面：

### 2.1 玩家状态管理 (PlayerStateManager)

`PlayerStateManager` 负责收集玩家的运动状态（如移动、跳跃、冲刺等物理状态），并通过静态事件（如 `OnMovementChanged`、`OnJumpStateChanged`）通知其他组件。然而，它不涉及玩家的生命值或武器信息，也未直接处理Photon网络同步，仅用于本地状态分发。这导致玩家的完整状态信息被割裂。

### 2.2 武器与射击逻辑

武器与射击逻辑由 `PlayerWeaponController` 和 `WeaponBase` 类处理。`PlayerWeaponController` 管理武器切换，并通过事件 `OnWeaponSwitched` 通知UI更新。武器的网络同步方面，`WeaponBase` 定义了 `[PunRPC] NetworkFire` 虚方法用于远程播放开火效果。此外，`WeaponNetworkSync` 组件集中处理武器状态同步，通过Photon的流机制发送武器索引、弹药数、装弹状态，并使用RPC转发开火、换弹事件。现有问题是 `PlayerWeaponController` 和 `WeaponNetworkSync` 在武器状态网络广播上存在逻辑重复和分散，甚至使用反射调用，增加了维护复杂度。

### 2.3 投射物与伤害

投射物和伤害逻辑由 `ProjectileBase` 及其子类处理。投射物通过Photon网络实例化并同步数据。当投射物命中玩家时，调用目标的 `IDamageable.TakeDamage` 接口处理伤害。`DamageNetworkSync` 脚本处理生命值的网络同步和验证，但在本地扣减生命值后通过Photon RPC通知其他客户端。目前，玩家的生命值实现分散且不明显，生命值状态与网络同步逻辑独立于 `PlayerStateManager`，增加了系统复杂度。

### 2.4 总结

综上所述，玩家状态（状态量）及其同步逻辑在当前系统中较为分散：运动状态事件在 `PlayerStateManager`，武器状态在 `PlayerWeaponController`/`WeaponNetworkSync`，生命值在 `DamageNetworkSync`。这种分散导致不同模块各自使用事件或RPC进行同步，缺乏统一管理。这为重构提供了依据，即需要一个统一的 `PlayerStatusManager` 来整合玩家各类状态，并通过Photon集中同步。

## 3. 新架构设计：PlayerStatusManager

新的 `PlayerStatusManager` 将作为单一来源管理玩家的重要状态，在本地更新并通过Photon网络广播给远端，同步状态给UI和其他游戏玩法组件。其架构流程如下：

### 3.1 本地状态收集

`PlayerStatusManager` 将挂载于玩家对象，通过引用获取必要的组件（如 `PlayerMotor`、`PlayerWeaponController`、`PlayerInput` 等）。当本地玩家状态变化（生命值减少、武器更换、弹药变化、移动状态改变等）时，`PlayerStatusManager` 捕获这些变化并更新内部状态数据结构（如 `PlayerStatusData`）。例如，监听武器控制器的武器切换事件以更新 `CurrentWeaponIndex`，监测 `DamageNetworkSync` 或实现自己的伤害处理以更新 `CurrentHealth`。

### 3.2 事件驱动与UI更新

`PlayerStatusManager` 在本地更新状态后，将触发相应的事件（可定义为非静态或静态事件），例如 `OnHealthChanged`、`OnWeaponChanged`、`OnAmmoChanged` 等。UI组件（如 `WeaponUIManager`、血量HUD）以及动画/音频组件（如 `PlayerAnimationController`、`PlayerAudioController`）将订阅这些事件，从而统一从 `PlayerStatusManager` 获取更新，而不再分别订阅多个分散的事件。这确保了UI和反馈与底层状态解耦，只关注 `PlayerStatusManager` 提供的集中状态变化通知。

### 3.3 Photon网络同步

`PlayerStatusManager` 将继承 `MonoBehaviourPun` 或项目的 `NetworkSyncBase`，利用 `PhotonView` 将关键状态在网络上同步：

*   **持续状态 (Continuous state)**：对于当前武器索引、当前弹药数、当前生命值等持续状态，`PlayerStatusManager` 将通过 `OnPhotonSerializeView` 周期性发送更新。远端玩家的 `PlayerStatusManager` 在 `ReadData` 时接收这些值并更新其内部状态副本，然后触发本地事件供UI/动画更新，确保每帧（或设定频率）远端都能获取最新状态。
*   **瞬时事件 (Discrete events)**：对于开火动作、装弹开始/结束、死亡等瞬时事件，将使用 Photon RPC 即时广播。`PlayerStatusManager` 可以定义RPC方法（如 `RPC_FireWeapon(dir)`、`RPC_WeaponSwitch(index)`、`RPC_TakeDamage(amount)` 等）。本地玩家执行动作时调用RPC通知其他客户端执行对应状态变更或效果。在远端 `PlayerStatusManager` 接收到RPC时，调用本地方法处理（例如远端播放枪口火焰特效、更新远端角色的当前武器）。通过组合持续同步与RPC，既保证状态一致性，又降低网络延迟对即时效果的影响。

### 3.4 远端状态应用

对于非本地玩家，对应的 `PlayerStatusManager` 作为 `PhotonView` 观察者接收数据。它将远端玩家状态更新应用于游戏对象：例如调用远端玩家的 `PlayerWeaponController` 切换武器模型、更新远端的生命值用于判断是否死亡等。远端 `PlayerStatusManager` 通常不需要触发UI（本地不会显示其他玩家具体弹药/生命UI），但可能触发Gameplay逻辑（如击杀计分、死亡动画）。远端状态主要用于动画表现（跑步、开火动作）和生效游戏规则（例如某玩家生命值<=0则触发淘汰）。

## 4. 重构任务清单

重构将按照类/脚本逐步进行，创建新组件并整合、替换旧逻辑。以下是详细任务列表：

### 4.1 新建 PlayerStatusManager 类

1.  **创建 `PlayerStatusManager.cs` 文件**：
    *   新建C#脚本 `PlayerStatusManager.cs`。
    *   使其继承 `NetworkSyncBase` 或 `MonoBehaviourPun`，并实现 `IPunObservable` 接口。
    *   使其实现 `IDamageable` 接口。

2.  **定义状态字段和引用**：
    *   添加需要同步的字段，例如：
        ```csharp
        public class PlayerStatusManager : NetworkSyncBase, IDamageable {
            [SerializeField] private float _maxHealth = 100f;
            private float _currentHealth;
            private int _currentWeaponIndex;
            private int _currentAmmo;
            // 引用
            private PlayerWeaponController _weaponController;
            private PlayerMotor _playerMotor;
            // 事件
            public static event Action<float> OnHealthChanged;
            public static event Action<int, int> OnAmmoChanged; // 当前弹药/最大弹药
            public static event Action<int> OnWeaponChanged;
            // ... 以及移动状态事件（可选）
        }
        ```

3.  **初始化和组件引用**：
    *   在 `Awake()` 方法中获取必要的组件引用，例如 `_weaponController = GetComponent<PlayerWeaponController>()`。
    *   初始化 `_currentHealth` 为 `_maxHealth`，`_currentWeaponIndex` 为起始武器索引等。
    *   实现 `IDamageable` 接口的 `TakeDamage()` 等方法。

### 4.2 实现网络序列化

1.  **实现 `WriteData` 方法**：
    *   在 `WriteData(PhotonStream stream)` 方法中，周期性发送关键状态数据：
        ```csharp
        protected override void WriteData(PhotonStream stream) {
            stream.SendNext(_currentHealth);
            stream.SendNext(_currentWeaponIndex);
            stream.SendNext(_currentAmmo);
        }
        ```

2.  **实现 `ReadData` 方法**：
    *   在 `ReadData(PhotonStream stream, PhotonMessageInfo info)` 方法中，接收并更新远端状态：
        ```csharp
        protected override void ReadData(PhotonStream stream, PhotonMessageInfo info) {
            _currentHealth = (float)stream.ReceiveNext();
            int newWeaponIndex = (int)stream.ReceiveNext();
            _currentAmmo = (int)stream.ReceiveNext();
            // 更新远端状态并触发相应逻辑
            if(newWeaponIndex != _currentWeaponIndex) {
                _currentWeaponIndex = newWeaponIndex;
                ApplyRemoteWeaponSwitch(newWeaponIndex);
            }
            // 可根据需要触发事件，例如远端模型更新
        }
        ```
    *   **注意**：初期可保持简单可靠，每帧同步生命值和武器状态。后续可根据性能需求降低Photon发送频率或仅在状态变化时发送。

### 4.3 添加 Photon RPC 方法

1.  **定义瞬时事件RPC方法**：
    *   根据需要添加用于瞬时事件的RPC方法，例如：
        ```csharp
        [PunRPC]
        private void RPC_FireWeapon(Vector3 direction) {
            // 远端玩家开火效果：调用当前Weapon.NetworkFire或播放特效
            _weaponController.CurrentWeapon?.NetworkFire(direction, Time.time);
        }

        [PunRPC]
        private void RPC_SwitchWeapon(int newIndex) {
            _currentWeaponIndex = newIndex;
            _weaponController.SwitchToWeapon(newIndex);
        }
        ```

2.  **本地调用RPC**：
    *   本地玩家在执行开火或切换武器时，调用 `photonView.RPC("RPC_FireWeapon", RpcTarget.Others, dir)` 等方法进行广播。
    *   这些RPC在远端触发时，通过 `WeaponController` 执行实际动作，实现与原有 `WeaponNetworkSync` 中RPC相同的效果，并移除先前通过反射调用 `NetworkFire` 的不优雅做法。

### 4.4 整合健康值同步逻辑

1.  **移植 `IDamageable` 接口实现**：
    *   将 `DamageNetworkSync` 中定义的 `IDamageable` 接口方法实现到 `PlayerStatusManager` 中，确保玩家对象挂载 `PlayerStatusManager` 即可被 `Projectile` 识别为 `IDamageable`。
    *   示例 `TakeDamage` 方法：
        ```csharp
        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection) {
            if (_currentHealth <= 0) return;
            _currentHealth = Mathf.Max(0, _currentHealth - damage);
            OnHealthChanged?.Invoke(_currentHealth);
            if(photonView.IsMine) {
                // 本地扣血后，通过RPC通知他人更新该玩家血量
                photonView.RPC("RPC_UpdateHealth", RpcTarget.Others, _currentHealth);
            }
            if(_currentHealth <= 0) HandleDeath();
        }

        [PunRPC] 
        private void RPC_UpdateHealth(float newHealth) {
            _currentHealth = newHealth;
            OnHealthChanged?.Invoke(_currentHealth);
        }
        ```
    *   **注意**：移植时保留关键的防作弊检查思想，例如在 `TakeDamage` 中加入简单验证或速率限制。

2.  **生命值事件与UI**：
    *   借助 `PlayerStatusManager` 的 `OnHealthChanged` 事件，将其绑定到玩家HUD血条更新逻辑。
    *   如果之前没有Health UI，可在重构时新增简单的血量显示脚本，订阅此事件以更新显示当前血量/最大血量。

3.  **死亡处理**：
    *   在 `PlayerStatusManager` 的 `TakeDamage` 方法中，当 `_currentHealth` 降至0时调用 `HandleDeath()`。
    *   `HandleDeath()` 方法可处理玩家死亡流程（如播放死亡动画、通过Photon通知 `GameManager` 淘汰玩家）。需要考虑Photon网络销毁或重生，这里提供接口，具体实现可在下一步完善。

### 4.5 合并武器状态同步逻辑

1.  **替换 `PlayerWeaponController` 网络广播**：
    *   移除 `PlayerWeaponController` 中对 `PhotonView` 的直接RPC调用。
    *   当本地切换武器时，`PlayerWeaponController` 仍完成本地逻辑并触发事件。
    *   网络同步改为 `PlayerStatusManager` 监听 `PlayerWeaponController.OnWeaponSwitched` 事件，在本地响应时调用自己的RPC广播。
    *   示例 `PlayerStatusManager` 中订阅和处理事件：
        ```csharp
        // 在PlayerStatusManager的Awake中订阅
        PlayerWeaponController.OnWeaponSwitched += HandleWeaponSwitched;
        ...
        private void HandleWeaponSwitched(PlayerWeaponController ctrl, WeaponBase newWpn) {
            if(!photonView.IsMine) return;
            _currentWeaponIndex = ctrl.CurrentWeaponIndex;
            _currentAmmo = newWpn.CurrentAmmo;
            // 触发本地事件
            OnWeaponChanged?.Invoke(_currentWeaponIndex);
            OnAmmoChanged?.Invoke(_currentAmmo, newWpn.MaxAmmo);
            // 通过Photon发送RPC
            photonView.RPC("RPC_SwitchWeapon", RpcTarget.Others, _currentWeaponIndex);
        }
        ```
    *   远端 `PlayerStatusManager` 在 `RPC_SwitchWeapon` 中调用本地的 `PlayerWeaponController.SwitchToWeapon`，实现远端模型同步。

2.  **整合弹药和装填**：
    *   监听 `WeaponBase` 的 `OnAmmoChanged`、`OnReloadStarted`/`Completed` 事件。
    *   在 `PlayerStatusManager` 中更新弹药计数并通过Photon序列化或RPC同步。
    *   例如，当本地武器开火减少弹药时，`WeaponBase` 触发 `OnAmmoChanged`，`PlayerStatusManager` 捕获事件后更新 `_currentAmmo` 并调用自身事件 `OnAmmoChanged` (UI更新)，然后下一次 `OnPhotonSerialize` 会发送最新弹药数。
    *   对于装弹动作，可通过RPC在远端触发对应特效或动画。
    *   通过合并，这些状态变化全部经由 `PlayerStatusManager` 转发，避免UI直接依赖 `WeaponBase` 静态事件。UI只需订阅 `PlayerStatusManager.OnAmmoChanged` 等即可。

3.  **精简 `NetworkFire` 调用**：
    *   由于现在 `RPC_FireWeapon` 由 `PlayerStatusManager` 统一处理，`WeaponBase` 的 `NetworkFire` 方法可改为 `public` 或通过接口调用，消除 `PlayerWeaponController` 里反射调用的黑箱做法。
    *   建议将 `WeaponBase.NetworkFire` 改为 `public` 方法或由 `PlayerStatusManager` 直接调用 `WeaponBase` 的受保护方法，以提高代码透明度。

4.  **废弃 `WeaponNetworkSync` 脚本**：
    *   其功能（武器索引、弹药、装弹的流同步和RPC事件）已经由 `PlayerStatusManager` 取代，因此可移除整个 `WeaponNetworkSync` 组件。
    *   需要在玩家预制体上去除该组件的挂载，并删除 `PhotonView` 对它的观察设置，改为 `PhotonView` 观察 `PlayerStatusManager`。
    *   **注意**：在移除前对比 `WeaponNetworkSync` 的功能清单，确保 `PlayerStatusManager` 全部实现（例如同步枪口特效/声音，可在 `RPC_FireWeapon` 中调用本地 `WeaponBase.Fire` 或手动播放特效）。
    *   `WeaponNetworkSync` 的部分未完成逻辑（如远端 `ApplyWeaponSwitch` 中切枪调用被注释）在新实现中应当补全。

### 4.6 调整相关模块引用

1.  **`NetworkPlayerController`**：
    *   将 `NetworkPlayerController` 中对 `PlayerStateManager` 的引用替换为 `PlayerStatusManager`。
    *   示例：
        ```csharp
        - private PlayerStateManager _playerStateManager;
        + private PlayerStatusManager _playerStatusManager;
        ...
        - _playerStateManager = GetComponent<PlayerStateManager>();
        + _playerStatusManager = GetComponent<PlayerStatusManager>();
        ...
        // 发送时
        PlayerStateData localState = PlayerStateData.Empty;
        - if(_playerStateManager != null) localState = _playerStateManager.GetStateSnapshot();
        + if(_playerStatusManager != null) localState = _playerStatusManager.GetStateSnapshot(); // StatusManager 提供类似接口或直接嵌入运动状态
        // 接收时
        - ApplyRemotePlayerState(); // 利用_playerStateManager
        + // 若动画仍需远端状态，可扩展PlayerStatusManager提供更多数据或通过其它途径获得
        ```
    *   **注意**：若动画系统需要，可让 `PlayerStatusManager` 持续监听 `PlayerMotor` 状态更新自身的 `PlayerStateData`，用于 `NetworkPlayerController.GetRemotePlayerState()` 提供远端动画信息。

2.  **UI管理器**：
    *   `WeaponUIManager` 等UI脚本目前订阅了 `PlayerWeaponController` 和 `WeaponBase` 的事件。需要修改为订阅 `PlayerStatusManager` 事件。
    *   示例：
        ```csharp
        - PlayerWeaponController.OnWeaponSwitched += OnWeaponSwitched;
        - WeaponBase.OnAmmoChanged += OnAmmoChanged;
        + PlayerStatusManager.OnWeaponChanged += OnWeaponSwitched;
        + PlayerStatusManager.OnAmmoChanged += OnAmmoChanged;
        ```
    *   同时，`OnWeaponSwitched` 处理函数签名可能改变（新事件传递的是 `weaponIndex` 而非 `WeaponBase` 对象），需相应调整UI更新逻辑（通过索引从 `WeaponController` 获取 `WeaponData` 等）。

3.  **音频、动画等**：
    *   检查 `PlayerAudioController`、`PlayerAnimationController` 等是否订阅了 `PlayerStateManager` 事件。
    *   将其改为订阅 `PlayerStatusManager` 对应事件。
    *   例如，如果 `AudioController` 以前监听 `PlayerStateManager.OnGroundStateChanged` 以播放落地声音，那么改为让 `PlayerStatusManager` 也触发 `OnGroundStateChanged` 事件，然后 `AudioController` 订阅之。
    *   或者，为了简化，可让 `PlayerStatusManager` 在内部继续使用 `PlayerStateManager` 的事件机制：即在 `PlayerStatusManager.Update` 中收集运动状态，如果变化则 `Invoke OnMovementChanged` 等（这样其他系统感觉不到更换了管理器，只是事件来源类名变了）。
    *   最终目的都是所有模块统一通过 `PlayerStatusManager` 交互，移除对旧 `PlayerStateManager` 的依赖。

### 4.7 Prefab 与场景配置

1.  **修改玩家预制体**：
    *   在Unity编辑器中，将玩家预制体中的 `PlayerStateManager`、`WeaponNetworkSync`、`DamageNetworkSync` 组件移除。
    *   新增 `PlayerStatusManager` 组件。
    *   配置其初始属性（如最大生命值），并确保挂载 `PhotonView` 观察列表包含 `PlayerStatusManager`（替换原先的 `WeaponNetworkSync` 等）。
    *   测试Prefab使其在运行时能够正确找到引用（`WeaponController` 等）。

2.  **更新场景中的玩家对象**：
    *   若有场景中手动放置的玩家对象，也要更新其组件。

### 4.8 命名与目录结构调整

1.  **类命名**：
    *   使用“Status”统一术语。将旧的 `PlayerStateManager` 脚本重命名为 `PlayerStatusManager.cs`（如果直接新建文件，也可删除旧文件）。
    *   引入一个数据结构类 `PlayerStatusData` 用于封装玩家所有状态（扩展自原 `PlayerStateData`）：例如新增字段 `health`、`weaponIndex` 等。这样 `NetworkPlayerController` 和其他系统可以引用 `PlayerStatusData` 快照，一次性包含运动和游戏状态。

2.  **目录归类**：
    *   考虑将玩家状态/属性相关脚本放在一起。可新建或使用 `Core/Player` 或 `Core/Gameplay` 目录，存放 `PlayerStatusManager`、`PlayerMotor`、`PlayerInput` 等核心玩家逻辑。
    *   武器相关网络同步已融入 `PlayerStatusManager`，可减少 `Weapons/Network` 子目录冗余。
    *   建议：按照功能拆分子目录，例如：
        *   `Scripts/Player/` 下包含 `PlayerStatusManager`、`PlayerStateData`、`PlayerHealthUI` 等玩家状态与UI脚本。
        *   `Scripts/Weapons/` 保留武器和弹药逻辑，但将网络同步部分移除或归档。
        *   如果项目规模较大，也可以建立 `Scripts/Networking/` 专门放置网络同步基类和管理器，如 `NetworkPlayerController`、`NetworkManager` 等，清晰区分网络层与游戏逻辑层。

3.  **文件与组件名**：
    *   确保一致性和易读。例如，`DamageNetworkSync` 功能并入后可删除该文件。
    *   如果存在“Damageable”相关脚本或接口文件，可以将 `IDamageable` 接口移动到更通用的命名空间（如 `DWHITE.Core`），文件名改为 `IDamageable.cs` 放入 `Interfaces` 文件夹，方便团队识别。

### 4.9 潜在重构风险及后续建议

1.  **反射及魔术方法风险**：
    *   原有代码通过反射调用 `WeaponBase.NetworkFire`。重构需彻底移除此隐患，通过公开方法或事件回调替代。
    *   这要求仔细检查所有使用反射或字符串查找的地方（例如Photon RPC方法名、动画事件），确保改名后不会失效。
    *   建议使用统一接口：比如让 `WeaponBase` 实现一个 `IWeaponNetwork` 接口，提供 `NetworkFire` 方法，这样 `PlayerStatusManager` 可以通过接口直接调用，避免反射。

2.  **跨模块引用**：
    *   更换核心管理器后，可能有隐藏的依赖需要更新。例如，某些脚本在Inspector中引用了 `PlayerStateManager`（通过拖拽或 `GetComponent`）。
    *   应搜索项目中所有出现“`PlayerStateManager`”的地方，逐一替换为 `PlayerStatusManager`，防止遗漏导致空引用异常。
    *   同样地，如果 `Animator` 或动画状态机通过 `SendMessage` 调用了 `PlayerStateManager` 的方法（虽然未明显发现这种用法，但值得注意），需要一并更改。
    *   **解决方案**：重构完成后，运行时查看控制台和日志，捕捉 `NullReference` 或 `MissingComponent` 错误并修复。

3.  **状态同步一致性**：
    *   合并后，确保不同状态同步频率匹配合理。例如之前运动状态30Hz，武器状态10Hz。现在若统一通过一个 `PhotonView` 发送，默认情况下所有状态用同一频率同步。
    *   **风险**：这可能造成非关键数据（如生命值、弹药）过于频繁同步，或关键数据不够及时。
    *   **优化**：可以通过调整 `PlayerStatusManager` 的发送频率（例如结合 `PhotonNetwork.SendRate` 或在 `WriteData` 中自行节流发送）来优化。
    *   后续可以考虑分组同步：例如高频状态（位置、朝向）仍由 `NetworkPlayerController` 处理，低频状态（生命值、武器）用 `PlayerStatusManager` 处理，各自有独立 `PhotonView`。这需要在Prefab上挂两个 `PhotonView`，但简化了数据分流。当前实现先采用单View方便整合，再根据性能需求优化。

4.  **已有功能回归**：
    *   重构需验证原有功能是否完整保留。例如，`WeaponUI` 的弹药数更新、换枪提示，开火准星动画等是否仍正常；多人生涯中，远程玩家看到的武器切换、生命值变化是否正确。
    *   **建议**：编写测试用例或手动测试场景：两台客户端互相射击、切枪，检查UI和状态同步的准确性。特别注意死亡后的同步：远端玩家死亡可能涉及禁用控制、同步ragdoll等，这部分在重构中需要设计（如在 `PlayerStatusManager.HandleDeath` 中 `PhotonNetwork.Destroy` 玩家物体，或者触发 `GameManager` 事件）。

5.  **文档和维护**：
    *   更新项目文档（如 `PlayerStateManager_Usage_Guide.md`）以反映新架构，注明 `PlayerStatusManager` 的用法、事件列表、同步原理，方便后续维护。
    *   原文档和代码可能存在出入，重构完成后应同步更正。
    *   未来如果增加新的玩家状态（例如护盾值、特殊能力状态），应统一由 `PlayerStatusManager` 管理，避免再度分散。
    *   定期代码审查，确保没有人绕过 `PlayerStatusManager` 直接操作状态，以保持架构整洁。

完成上述任务后，玩家状态系统将更加清晰：一个 `PlayerStatusManager` 组件纵向打通本地->网络->远端的状态流转，减少重复同步逻辑和隐式依赖。尽管重构跨度较大，逐步验证各部分功能可以降低风险。此举将为后续功能扩展和调试打下良好基础，例如更容易地实现玩家重生、结算统计以及复杂状态（Buff/Debuff）同步等。


