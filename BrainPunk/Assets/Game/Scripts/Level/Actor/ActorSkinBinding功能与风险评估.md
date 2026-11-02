# ActorSkinBinding 功能与风险评估

## 2026-08-04：解除 BaseLevelCtrl 强耦合

### 需求与原行为基线

- `ActorSkinBinding` 的默认 `MainPlayerOnly` 模式通过全局查找 `BaseLevelCtrl`，并要求
  `BaseLevelCtrl.Actor` 与当前 Actor 相同，才允许读取玩家当前皮肤。
- 该门禁用于阻止由玩家 Actor 克隆出来的工具、NPC 或敌人自动套用玩家皮肤；
  `ExplicitOptIn` 模式则始终允许绑定。
- Draw 流程由 `DrawPlayCtrl` 和 `DrawLevelCtrl` 管理，不存在对应的 `BaseLevelCtrl`，因此
  Draw 玩家 Actor 即使带有 `ActorSkinBinding`，默认模式也会被判定为不可绑定。

### 修改后结构与行为

- 新增 `IActorSkinBindingOwner`，只声明实现方当前负责的 `Actor`。
- `BaseLevelCtrl` 和 `DrawLevelCtrl` 实现该接口；`ActorSkinBinding` 不再引用或全局查找
  `BaseLevelCtrl`，而是从当前 Actor 父层级取得最近的所有者，并校验
  `owner.Actor == actor`。
- 默认枚举名称由 `MainPlayerOnly` 改为 `LevelPlayerOnly`。枚举数值顺序保持 `0/1`，现有
  prefab 的 `bindingMode: 0` 与 `bindingMode: 1` 无需迁移。
- Draw 的所有者属性复用 `DrawLevelCtrl.ResolveReferences()`，在组件 `OnEnable` 早于
  `InitializeRuntime()` 时也能解析当前 DLevel 的 Actor。
- `ExplicitOptIn`、`DB.GameData` 读取、FullBody Spine Skin、Back/Face/Body 异步 prefab
  加载、版本保护、取消令牌、部件清理和 `ActorSkinSelectionChangedEvent` 均保持不变。

### 数据、资源、事件与性能边界

- 数据链仍为 `DB.GameData` 的当前 Skin 保存值；不新增或修改存档字段，也不触发保存。
- 资源链仍由 `SkinUtils` 解析并通过既有 Bear ResourceSystem 加载；不修改资源地址、配置、
  Luban、YooAsset 或热更规则。
- 事件链仍只订阅已有 `ActorSkinSelectionChangedEvent`，不新增全局事件。
- 身份检查由全局 `FindFirstObjectByType<BaseLevelCtrl>()` 改为 Actor 父层级组件查询；只在
  `OnEnable`、`Start` 或皮肤刷新时执行，不新增逐帧逻辑。

### 风险、验证与未验证缺口

- 工具或 NPC 即使由玩家 Actor 克隆，只要所有者声明的 Actor 仍是原玩家 Actor，克隆体比较
  就不会通过；Level26/64 现有的显式禁用和部件清理仍保留。
- 多 Actor Draw 关卡仍以 `DrawLevelCtrl.ResolveReferences()` 解析出的第一个 Actor 作为玩家；
  这与 DrawLevelCtrl 原有胜负、死亡和渐隐目标边界一致，但完整运行时层级顺序仍需 PlayMode 验证。
- Actor 必须位于所有者父层级之下；现有主线和 Draw prefab 属于该结构。运行时若把 Actor 移到
  所有者层级之外，后续皮肤事件会判定为不可绑定，此场景尚未发现本地调用证据。
- `dotnet build Assembly-CSharp.csproj --no-restore ...` 已通过，结果为 `0 error / 101 warning`；
  warning 为当前工程既有范围。
- 目标文件 `git diff --check` 已通过，仅有工作区 LF/CRLF 转换提示。
- `uncertain`：尚未执行 Unity PlayMode、主线进门、Draw 首次进入/Reset/Replay、Level60/83
  特殊 Actor、Android/iOS 横屏和资源包加载验证。

### 回滚路径

- 从 `BaseLevelCtrl`、`DrawLevelCtrl` 移除 `IActorSkinBindingOwner` 实现和 Draw 的 `Actor` 属性。
- 从 `ActorSkinBinding` 删除接口，将默认枚举名恢复为 `MainPlayerOnly`，并恢复
  `FindFirstObjectByType<BaseLevelCtrl>()` 身份判断，即可回到修改前行为；不需要迁移 prefab、
  配置、资源或玩家存档。

## 2026-08-05：DrawGame yellow_ball 球载体显式退出皮肤绑定

- 用户确认：以 `yellow_ball` 承载受控角色的 Draw 关卡必须保持关卡预设外观，不允许球内 Actor 自动应用
  玩家当前皮肤。
- 本次不修改 `ActorSkinBinding` 运行时代码和通用 `Actor.prefab`；共享
  `DrawGame_AlienClipBallRig.prefab` 通过 nested removed-component 一次覆盖 16 个消费者，另外在
  D028/D030/D032/D033/D042/D043 移除本地球内 Actor 的组件。双球关卡两侧均处理。
- 修改后目标 Actor 不进入组件生命周期、DB 读取、换肤事件订阅和部件资源加载链；普通 Draw Actor、主线
  Actor、显式 OptIn 和非球载体皮肤行为保持不变。
- 7 个实际修改 Prefab 的 YAML 检查均为 0 重复 anchor、0 缺失本地引用，目标差异格式检查通过；
  工程编译为 `0 error / 895 warning`。Unity PrefabImporter、22 关 PlayMode、Reset/Replay、成功渐隐和
  移动端仍为 `uncertain`。
- 完整覆盖、风险与回滚见
  `Assets/Game/Scripts/Level/DrawLevel/DrawGame球载体Actor皮肤绑定禁用功能与风险评估.md`。
