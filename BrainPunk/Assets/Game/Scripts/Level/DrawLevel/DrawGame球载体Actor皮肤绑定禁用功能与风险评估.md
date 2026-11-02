# DrawGame 球载体 Actor 皮肤绑定禁用功能与风险评估

## 1. 需求与原行为基线

- 需求：DrawGame 中以 `yellow_ball` 承载受控角色的关卡，球内 Actor 必须保持关卡预设外观，不能触发
  `ActorSkinBinding` 自动读取并应用玩家当前皮肤。
- 原行为：`DrawLevelCtrl` 实现 `IActorSkinBindingOwner`，并通过
  `GetComponentInChildren<ActorCtrl>(true)` 解析当前层级第一个 Actor；球内 Actor 若保留
  `ActorSkinBinding`，其中一个 Actor 会通过 `owner.Actor == actor` 校验，在 `OnEnable` / `Start`
  读取 `DB.GameData`、订阅 `ActorSkinSelectionChangedEvent`，并可能加载 Back/Face/Body 部件 Prefab。
- 多球关卡依赖层级扫描顺序决定哪个 Actor 通过校验，不适合作为球载体是否允许换肤的业务规则。

## 2. 修改范围与当前覆盖

- 共享 `Assets/Game/Prefabs/DrawLevel/Shared/DrawGame_AlienClipBallRig.prefab` 在嵌套
  `Actor.prefab` 上新增 removed-component override，移除 `ActorSkinBinding`。当前直接引用该共享 Rig
  的 16 个关卡为：`DLevel_002`、`006`、`009`、`013`、`015`、`018`、`019`、`020`、`025`、
  `026`、`027`、`029`、`031`、`034`、`039`、`047`。
- 本地球结构关卡 `DLevel_028`、`030`、`032`、`033`、`042`、`043` 同步移除球内 Actor 的
  `ActorSkinBinding`；其中 `032`、`033`、`042`、`043` 的左右两球均已处理。
- 通用 `Assets/Game/Prefabs/LevelItems/Actor/Actor.prefab`、`ActorSkinBinding.cs`、`DrawLevelCtrl.cs`
  均未修改；普通 Actor 关卡、主线关卡和非球载体 Actor 保持原皮肤绑定行为。

## 3. 修改后行为与系统边界

- 上述 22 个 Draw 关卡的球内 Actor 不再拥有有效 `ActorSkinBinding`，因此不会进入其
  `Awake`、`OnEnable`、`Start`、换肤事件订阅、DB 读取、FullBody Skin 应用或部件异步加载链。
- ActorCtrl、Spine、碰撞体、刚体、球释放、失败、成功、渐隐和 Reset/Replay 结构保持不变；默认外观继续
  由关卡 Prefab/Actor Spine 的序列化初态提供。
- 不新增关卡 ID 判断、GameObject 名称判断、Tag/Layer 过滤、全局事件、配置、存档字段、资源地址、UI、
  analytics、广告、IAP 或平台分支。
- `EnterDoorAnimCtrl` 对 `ActorSkinBinding` 使用可空查找；球载体关卡没有该组件时会直接跳过皮肤应用后的
  Renderer 重收集，不阻断既有门表现。

## 4. 风险评估

| 风险 | 等级 | 当前控制 | 未验证缺口 |
| --- | --- | --- | --- |
| 共享 Rig 修改影响非目标关卡 | 高 | 静态反向扫描确认 16 个直接消费者均使用共享 `yellow_ball` 球载体，且通用 Actor.prefab 未改 | 16 关 PlayMode 逐关回归待执行 |
| 双球只移除一侧导致外观不一致 | 高 | D032/D033/D042/D043 两侧内嵌 Actor 的组件和 GameObject 引用均已删除 | 真实左右球外观待 PlayMode 目视确认 |
| 删除 YAML 文档后产生悬空 fileID | 高 | 7 个实际修改 Prefab 均为 0 重复 anchor、0 缺失本地引用，`git diff --check` 通过 | Unity PrefabImporter 待验证 |
| 默认角色外观不符合策划预期 | 中 | 不再覆盖序列化 Spine 初态，未修改 Actor/Spine 资源 | 首次进入、Reset、Replay 后外观待确认 |
| 后续新建本地 yellow_ball 关卡漏移除组件 | 中 | 共享 Rig 新关卡自动继承；独立球结构仍需在本关结构检查中确认 | 尚未增加自动化 Validator |
| Android/iOS 行为差异 | 低 | 无平台分支、无新运行时代码或分配 | 横屏真机未验证 |

## 5. 验证证据

- 反向扫描确认共享 Rig 当前有 16 个 `DLevel_*.prefab` 直接消费者。
- 共享 Rig 与 D030 均包含 Actor.prefab 的 `ActorSkinBinding` removed-component override。
- D028/D032/D033/D042/D043 中 `ActorSkinBinding` 脚本 GUID 数量为 0；D030 没有本地脚本实例，且
  nested removed-component 数量为 1。
- 共享 Rig、D028、D030、D032、D033、D042、D043 的 YAML 静态检查分别为
  `72/211/209/259/228/252/244` 个唯一 anchor，全部 `duplicate=0 / missingLocal=0`。
- 目标 Prefab 的 `git diff --check` 通过；仅报告工作区 LF/CRLF 转换提示。
- `dotnet build Assembly-CSharp.csproj --no-restore` 通过，结果为 `0 error / 895 warning`；warning 为
  当前工程与第三方包既有范围，本次没有修改 C#。
- `uncertain`：Unity PrefabImporter、22 关首次进入、失败 Reset、Replay、成功渐隐、皮肤切换事件、
  Android/iOS 横屏均尚未运行验证。

## 6. 回滚路径

- 共享 Rig 与 D030：从对应 Actor.prefab 实例的 `m_RemovedComponents` 删除
  `ActorSkinBinding` source fileID `7468361278302465112`。
- D028/D032/D033/D042/D043：恢复各 Actor GameObject 的 component fileID，并恢复对应
  `ActorSkinBinding` MonoBehaviour YAML 文档。
- 回滚不需要修改通用 Actor.prefab、C#、DB、Luban、YooAsset、资源地址或玩家存档。
