# Postcard_Star 1.0 风险评估

## 评估范围

- 评估日期：2026-06-30
- 变更入口：当前工作区未提交改动
- 涉及文件：`Postcard_Star.cs`、`PlayEvents.cs`、`GamePlayPanel.cs`、`PropBlockNotice.cs`、`Postcard_Star.prefab`、`GamePlayPanel.prefab`
- 评估方法：按知识库“Git 变更驱动风险评估工作流”，从脚本和 prefab diff 扩散到触发链、事件链、UI 链、存档链、背包链、打点链、平台验证缺口。

## 原始行为基线

- `Postcard_Star` 原运行时只保留 `index` 配置和编辑器排序工具，没有玩家触发收集逻辑。
- `Postcard.LevelCollectedStar` 已由 `Postcard_Partial.Init()` 初始化为 `Dictionary<string, List<int>>`，但没有由 `Postcard_Star` 消费。
- `GamePlayPanel` 原订阅移动、重置、暂停、提示、遮罩、淡入等事件，不处理明信片星星收集事件。
- `PropBlock` 原默认自动订阅 `UpdatePropEvent` 并在背包变更时刷新数字。

## 变更后链路

1. 玩家碰到 `Postcard_Star` 的 2D trigger。
2. `Postcard_Star` 校验 `ActorCtrl` 或 `Player` tag，关闭 trigger，读取 `PlayCtrl.Instance.Level.CurrentLevelData.Id`。
3. 当前 `levelData.Id` 与 `index` 组合未记录时，写入 `DB.Postcard.LevelCollectedStar` 并调用 `DB.SavePostcard()`。
4. `DB.SavePostcard()` 成功后发放 `GameProps.Star`；失败时回滚本次写入，恢复触发，不播放收集动画。
5. 场景星星尝试飞向 `GamePlayPanel.TryGetStarBlockWorldPosition()` 返回的 HUD Star `PropBlock` 世界坐标；缺失时走本地缩放消失。
6. 飞行动画结束后派发 `PostcardGetStarEvent`。
7. `GamePlayPanel` 调用 `PropBlockNotice.ShowAndRefreshAfterHold()`，淡入、刷新 `PropBlock.UpdateData()`、停留、淡出。

## 已缓解风险

- 重复触发：`isCollecting`、`isCollected` 和 `triggerCollider.enabled = false` 在收集入口阻止同一触发期重复奖励。
- 重复进入：`Awake()` 根据 `DB.Postcard.LevelCollectedStar[levelData.Id]` 和 `index` 隐藏已收集对象。
- 重复 index：`TrySaveCollectionAndReward(...)` 内部会在保存前再次检查 `collectedIndexes.Contains(starIndex)`，命中时按已收集处理并不发放 Star，避免同一关卡误配相同 index 的第二个对象重复奖励。
- HUD 缺失：`TryGetStarBlockWorldPosition()` 返回 false 时星星走无 HUD 目标的降级动画，奖励和存档不被 UI 绑定阻断。
- 早刷新：`GamePlayPanel.prefab` 中 Star `PropBlock.IsAutoUpdate` 关闭，避免 `AddTool` 派发 `UpdatePropEvent` 后 HUD 数字早于飞行动画变化。
- 生命周期残留：`Postcard_Star.OnDisable/OnDestroy()` 终止收集 tween，`GamePlayPanel.OnClose/OnDestroy()` 终止并隐藏 `PropBlockNotice`。
- 保存失败：`DB.SavePostcard()` 失败时会移除本次新增的 `index` 记录，恢复触发器，不发放 Star、不隐藏星星，并输出 warning。

## P1 风险

### P1-1 特殊 GamePlayPanel 变体需要验证 starBlock 继承或降级表现

- 问题：多数 `Assets/Game/UI/GamePanel/Resources/GamePlayPanel_*` 是 `GamePlayPanel.prefab` 变体，但 `GamePlayPanel_013` 和 `GamePlayPanel_019_56` 存在直接 `GamePlayPanel` 脚本实例文本记录；当前方案保留 `starBlock` 字段并在该节点上复用 `PropBlockNotice`，这些变体是否继承 `starBlock` 与组件绑定仍需要 Editor 验证。
- 影响文件：`GamePlayPanel.prefab`、`GamePlayPanel_013.prefab`、`GamePlayPanel_019_56.prefab`
- 扩散链路：关卡配置 `gamePlayPanelName` -> UIManager 打开面板 -> `TryGetStarBlockWorldPosition()` -> 场景星星飞行动画终点或降级。
- 风险影响：特殊面板可能没有 HUD 目标，玩家仍拿到奖励和存档，但飞行动画可能只缩放消失，HUD 提示不出现。
- 当前处理：脚本缺绑定时返回 false 并降级，不阻断收集。
- 后续验证：在至少一个默认面板和一个特殊面板关卡中分别触发收集，确认目标位置、提示显示和降级行为。

### P1-2 `PropBlockNotice` Tween 被关闭或销毁时需要 PlayMode 验证

- 问题：提示淡入后等待、刷新、停留、淡出都依赖 DOTween 和 UniTask，面板关闭、重置或销毁可能中断异步流程。
- 影响文件：`Assets/Game/UI/Global/Scripts/PropBlockNotice.cs`、`Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.cs`
- 扩散链路：HUD 收到事件 -> `ShowAndRefreshAfterHold()` -> DOTween sequence -> `OnClose/OnDestroy/OnDisable` 清理。
- 风险影响：如果清理不完整，可能留下透明度、raycast 或 tween 状态异常。
- 当前处理：`StopAndHide()`、`OnDisable()`、`OnDestroy()` 都会停止 tween，并设置 `blocksRaycasts` / `interactable` 为 false。
- 后续验证：在提示进行中执行 pause、reset、fail、success、exit，确认 HUD 不残留、不拦截点击。

## 观察项

- `SimpleBag.AddTool(GameProps, ...)` 当前会派发两次 `UpdatePropEvent`，这是既有实现；本次通过关闭 Star `PropBlock.IsAutoUpdate` 避免该 HUD 块早刷新，没有修改 `SimpleBag`。
- `Postcard_Star` 使用 `ActorCtrl` 父级检测和 `Player` tag 兜底判断玩家，若个别关卡玩家碰撞体结构特殊，需要在对应关卡补验。
- 当前没有新增资源加载、RemoteConfig、广告、IAP、登录或成就逻辑；但发放 Star 会走既有 `GameSDKService.Instance.EarnVirtualCurrentcy(...)` 虚拟币打点路径，需要确认该道具类型的打点语义符合预期。

## 验证证据

- `git diff --check`：通过，仅有 LF/CRLF 转换提示。
- 静态 API 证据：`LevelCtrl.CurrentLevelData`、`DB.SavePostcard()`、`Postcard.LevelCollectedStar`、`SimpleBag.AddTool(GameProps, ...)`、`PropBlock.UpdateData()`、Bear `DispatchEvent<TEvent>(Witness<TEvent>._)` 均存在。
- 静态生命周期扫描：未发现新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`。
- 编译验证：当前 worktree 无 `Assembly-CSharp.csproj`，未能执行 `dotnet build`。

## 未覆盖验证

- Unity Editor 编译和 Domain Reload。
- Unity PlayMode：首次收集、重复触发、重开、失败、胜利、退出再进、缺 HUD 绑定、特殊 GamePlayPanel 变体。
- Android/iOS 横屏：触摸收集、动画流畅度、HUD 不遮挡操作、退出或切后台时状态一致性。
- 保存失败路径：模拟 `DB.SavePostcard()` 返回 false，确认本次不发放 Star、不隐藏星星、触发器恢复且 warning 可见。

## 回滚路径

- 回退 `Postcard_Star.cs` 运行时收集逻辑，保留或恢复原编辑器排序工具。
- 删除 `PostcardGetStarEvent` 及 `GamePlayPanel` 对应订阅、处理和 HUD 坐标 API。
- 从 `GamePlayPanel.prefab` 移除 `PropBlockNotice` 组件绑定，并恢复 Star `PropBlock.IsAutoUpdate`。
- 删除 `PropBlockNotice.cs` 与 `.meta`。

## 2026-07-01 Speckit Implement 执行记录

### 已执行验证

- 规格质量清单：`specs/032-postcard-star/checklists/requirements.md` 为 16/16 完成。
- `git diff --check`：通过，仅输出 LF/CRLF 转换提示。
- 静态 API 顺序：`Postcard_Star.cs` 中 `DB.SavePostcard()` 位于 `PlayCtrl.Instance.Bag.AddTool(GameProps.Star, 1, RewardType.Other)` 之前。
- 静态生命周期扫描：目标脚本未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`。
- 范围扫描：`DBManager_Generated.cs`、`SimpleBag.cs`、`BrainPunk/Luban`、`BrainPunk/ProjectSettings`、`BrainPunk/Packages` 无本功能 diff。
- Prefab 静态证据：`Postcard_Star.prefab` 已有收集动画字段绑定；`GamePlayPanel.prefab` 保留 `starBlock` 绑定，并在该节点上补充 `PropBlockNotice`、`CanvasGroup`、目标 Star `PropBlock` 绑定，且 Star `PropBlock.IsAutoUpdate` 为关闭覆盖。
- 影响面扫描：`BrainPunk/Assets/Game/Prefabs/Level/` 中存在 131 条 `Postcard_Star` 文本命中，说明需要至少一个默认面板关卡和一个特殊面板或代表性关卡补充 PlayMode 验证。

### 本次发现并修正的风险

- 风险：同一关卡内如果两个 `Postcard_Star` 误配同一个 `index`，第二个对象在外层检查之后进入保存 helper 时，可能因为内存记录已存在但 helper 未二次返回而继续发奖。
- 修正：`TrySaveCollectionAndReward(...)` 改为返回 `SavedAndRewarded`、`AlreadyCollected`、`Failed` 三态；内部命中已存在 index 时返回 `AlreadyCollected`，调用方隐藏当前对象但不保存、不发奖。
- 验证：静态扫描确认 `collectedIndexes.Contains(starIndex)` 在 `DB.SavePostcard()` 和 `AddTool(GameProps.Star...)` 之前执行。

### 仍未覆盖

- 当前 worktree 的 `BrainPunk/` 下没有 `.csproj`，未执行 `dotnet build`；需要 Unity Editor 编译和 Domain Reload 补验。
- 未运行 Unity PlayMode：首次收集、重复触发、重开、失败、胜利、退出再进、保存失败注入、缺 HUD 绑定、特殊 `GamePlayPanel` 变体仍未验证。
- 未运行 Android/iOS 横屏 smoke：触摸收集、动画流畅度、HUD 不遮挡操作、暂停/退出/切后台状态一致性仍未验证。

## 2026-07-01 PropBlockNotice 淡出 API 抽取风险记录

### 原始行为基线

- `PropBlockNotice.PlayNotice(...)` 原本在停留结束后内联创建淡出 sequence：`CanvasGroup.DOFade(0f, fadeOutDuration)`，完成后关闭交互和射线拦截。
- `StopAndHide()`、`OnDisable()` 和 `OnDestroy()` 负责立即终止提示，避免关闭、重置或销毁路径残留 tween。

### 本次变更

- 新增公开 `HideWithFade()`，供外部需要保留淡出过渡的场景调用；API 会停止当前提示流程，接管 `CanvasGroup.DOFade`，完成后保持透明、不拦截点击。
- `ShowAndRefreshAfterHold()` 停留结束后改为复用 `HideWithFade()`，减少同一组件内淡出隐藏逻辑重复。
- 未修改 `GamePlayPanel` 订阅、`PostcardGetStarEvent`、`Postcard_Star` 奖励保存、prefab 绑定、背包、存档、配置、资源加载、analytics、广告、IAP 或平台 SDK。

### 风险项

- P2-1 外部直接调用 `HideWithFade()` 时，如果调用方期望立即清理，可能留下 `fadeOutDuration` 的过渡时间；当前保留 `StopAndHide()` 作为立即清理 API，关闭/销毁路径不改用淡出。
- P2-2 `HideWithFade()` 会先 `StopNotice()` 并递增版本号，因此会取消正在进行的 `ShowAndRefreshAfterHold()`；这符合“外部接管隐藏”的 API 边界，但调用方不能同时期望旧提示继续刷新。
- P2-3 淡出 sequence 仍依赖 DOTween 和 `CanvasGroup`，真实 UI 栈关闭、暂停、切后台时仍需 PlayMode 和移动端 smoke 验证。

### 缓解与验证

- `StartHideFade()` 集中创建淡出 sequence，并在完成时调用 `SetVisibleState(false)`，继续关闭 `interactable` 和 `blocksRaycasts`。
- `StopAndHide()`、`OnDisable()`、`OnDestroy()` 保持立即 kill 和隐藏，不引入关闭路径异步等待。
- 静态扫描确认本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、`Instantiate`、`PlayerPrefs`、`Resources`、`YooAsset`、`Analytics`、`IAP` 或 `Advertisement`。
- `git diff --check -- Assets/Game/UI/Global/Scripts/PropBlockNotice.cs Assets/Game/Scripts/Postcard/Postcard_Star功能说明.md Assets/Game/Scripts/Postcard/Postcard_Star风险评估.md DevelopMD/已有功能设计与使用记录.md` 已通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error / 97 warning。

### 未覆盖验证

- Unity Editor PlayMode 和 Domain Reload。
- Unity PlayMode：`ShowAndRefreshAfterHold()` 完整淡入、刷新、停留、淡出；外部直接调用 `HideWithFade()`；提示中 reset、close、destroy 后无 alpha、raycast 或 tween 残留。
- Android/iOS 横屏：HUD 淡出过渡期间不遮挡触摸，暂停、退出、切后台后状态一致。

### 回滚路径

- 删除 `HideWithFade()` 和 `StartHideFade()`，把 `PlayNotice(...)` 末尾恢复为内联淡出 sequence。

## 2026-07-02 Postcard 存档读写集中到 PostcardManager 风险记录

### 原始行为基线

- `Postcard_Star` 原本在 `Awake()` 和收集保存 helper 中直接读取 `DB.Postcard.LevelCollectedStar`。
- 保存永久星记录时，`Postcard_Star` 直接调用 `DB.SavePostcard()`；保存失败时回滚本次新增 id，不发放 Star。

### 本次变更

- `Postcard_Star.IsSavedCollected(...)` 改为调用 `GameManager.Instance?.Postcard.IsLevelStarCollected(...)`。
- 收集保存改为调用 `GameManager.Instance?.Postcard.TryCollectLevelStar(...)`，由 `PostcardManager` 集中处理 `DB.Postcard` 字典初始化、去重、保存和失败回滚。
- `Postcard_Star` 仍负责 `PlayCtrl.Instance.Bag.AddTool(GameProps.Star, 1, RewardType.Other)`、`PostcardGetStarEvent` 派发和对象隐藏。

### 风险项

- P1-1 `Postcard_Star.Awake()` 现在依赖 `GameManager.Instance.Postcard` 已创建；静态链路显示正常加载流程中 `LoadManager.EnterGame()` 会先创建 manager，再进入玩法，但开发者直开玩法场景仍需验证。
- P1-2 如果 `PostcardManager` 缺失，`IsSavedCollected(...)` 会返回 false，已收集星可能保持可触发；触发收集时会因 manager 缺失中止并恢复触发器，不会写 DB 或发奖。
- P2-1 保存失败回滚逻辑从 `Postcard_Star` 迁移到 `PostcardManager.TryCollectLevelStar(...)`，需要 PlayMode 注入或断点确认失败时仍不发奖、不派发事件。

### 缓解与验证

- 静态扫描确认 `Postcard_Star.cs` 不再直接出现 `DB.Postcard` 或 `DB.SavePostcard()`，存档写入集中到 `PostcardManager.cs`。
- `TryCollectLevelStar(...)` 保存失败时会移除本次新增 id，并在创建的新关卡条目为空时移除该条目。
- `Postcard_Star` 只有在 `TryCollectLevelStar(...)` 返回成功且 `isNewCollection == true` 后才发放 Star。
- `git diff --check` 与 `dotnet build` 需在本次集中迁移收尾后统一复跑。

### 未覆盖验证

- Unity PlayMode：正常加载进入关卡、已收集星隐藏、首次触发收集、重复触发、保存失败注入。
- 开发者直开玩法场景：确认 manager 初始化时序或缺失时的降级表现。
- Android/iOS 横屏：触摸收集、保存耗时、HUD 表现和退出/重开状态一致性。

### 回滚路径

- 将 `Postcard_Star` 的已收集判断和收集保存 helper 恢复为直接访问 `DB.Postcard.LevelCollectedStar` 与 `DB.SavePostcard()`。
- 保留或移除 `PostcardManager` 新增 API 取决于 `Postcard_VictoryStarGroupCtrl` 和 `PlayCtrl_Success` 是否仍依赖这些 API。

## 2026-07-02 Postcard_Star 全关卡批量刷新菜单风险记录

### 原始行为基线

- `Postcard_Star.SearchAllStarInScene()` 是单组件 Inspector/Odin 手动入口，按当前关卡根节点层级顺序刷新 `Postcard_Star.index`。
- 原有流程没有全关卡 prefab 批量菜单，批量整理依赖人工逐个打开和点击。

### 本次变更

- 新增 `Assets/Editor/PostcardStarLevelPrefabRefresher.cs`，菜单路径为 `Tools/Bear/Postcard RefreshAllLevelStar`。
- 工具逐个 `LoadPrefabContents()` 打开 `Assets/Game/Prefabs/Level/*.prefab`，每个 prefab 只从第一个 `Postcard_Star` 调用一次 `SearchAllStarInScene()`。
- 仅 dirty 内容会通过 `PrefabUtility.SaveAsPrefabAsset()` 保存；无星星 prefab 和未变更 prefab 不写回。

### 风险项

- P2-1 批处理会集中修改多个关卡 prefab 的 `Postcard_Star.index`，如果排序规则不是策划期望的层级顺序，可能造成星星序号与人工预期不一致。当前缓解是复用已有单关工具，不引入新的排序规则。
- P2-2 某个 prefab 中星星数量超过 `Postcard_StarNum` 可用数量时，原方法会弹窗并停止该 prefab 写入；批量执行期间需要人工处理该弹窗，否则批处理体验会被打断。
- P2-3 `LoadPrefabContents()` 打开的 prefab 内容不等同于真实 PlayMode 场景验证；保存后的 prefab 仍需 Unity Inspector 或抽样 PlayMode 检查。
- P3-1 工具依赖 `EditorUtility.IsDirty()` 判断是否保存；如果未来 `SearchAllStarInScene()` 的实现改为不显式 SetDirty，则批量保存可能漏写。当前 `SearchAllStarInScene()` 已对变更星星调用 `EditorUtility.SetDirty()`。

### 缓解与验证

- 菜单 validate 限制非 PlayMode 且目标目录存在时可执行。
- 工具只使用 `AssetDatabase`、`PrefabUtility`、`EditorUtility` 和已有 `Postcard_Star` 本地排序入口，不访问 DB、Save、RemoteConfig、资源加载、manager、广告、IAP、analytics 或平台 SDK。
- `git diff --check -- Assets/Editor/PostcardStarLevelPrefabRefresher.cs Assets/Editor/PostcardStarLevelPrefabRefresher.cs.meta` 已通过。
- 静态关键字扫描未发现新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode` 或运行时数据链路。
- `dotnet build .\Assembly-CSharp-Editor.csproj` 因 `Temp\bin\Debug\*.dll` Unity 生成程序集缺失失败，未能完成 C# 编译验证。

### 未覆盖验证

- 尚未在 Unity Editor 中实际执行 `Tools/Bear/Postcard RefreshAllLevelStar`。
- 尚未验证执行后 prefab 的 Inspector 显示、Undo/保存行为和 YAML 变更范围。
- 尚未对代表性关卡做 PlayMode 收集流程回归；本工具理论上只影响 `index` 配置，但真实序号仍需抽样确认。

### 回滚路径

- 删除 `Assets/Editor/PostcardStarLevelPrefabRefresher.cs` 和 `.meta`。
- 如果菜单已执行并产生 prefab diff，按 Git diff 回滚对应 `Assets/Game/Prefabs/Level/*.prefab` 的 `Postcard_Star.index` 序列化变更。

## 2026-07-02 Postcard_Star 收集表现迁移到 GamePlayPanel 风险记录

### 原始行为基线

- `Postcard_Star` 原本在收集成功后直接移动场景内 `flyVisualRoot`，由场景对象自己飞向 HUD 星星块并缩放消失。
- `PostcardGetStarEvent` 原本是无参事件，并且在场景星星飞行动画结束后才派发。
- `GamePlayPanel` 原本只在事件到达后负责 `starBlock` 的淡入、延迟刷新和隐藏，不创建 UI 星星实例。
- `Postcard_Star.SearchAllStarInScene()` 是编辑器宏内工具，本次要求保留该能力。

### 本次变更

- `Postcard_Star` 不再依赖 DOTween 执行自身飞行表现，只在保存和发奖成功后派发带 `StarWorldPosition` 的 `PostcardGetStarEvent` 并隐藏当前对象。
- `GamePlayPanel.PostcardStar` 接管表现：把事件中的场景星星世界坐标转换到 Canvas 本地坐标，实例化 `postcardStar` UI 表现物，按二次贝塞尔弧线飞向 `starBlock`。
- UI 飞行动画完成后，继续复用 `PropBlockNotice.ShowAndRefreshAfterHold()` 或 CanvasGroup 降级流程刷新星星道具块。
- `GamePlayPanel.prefab` 和 `GamePlayPanel_025_70.prefab` 已绑定 `Assets/Game/UI/Postcard/Prefabs/Postcard_UIStarCollection.prefab`。
- 编辑器排序代码仍在 `#if UNITY_EDITOR` 内，未改动排序入口、排序规则、Undo、SetDirty 或批量刷新工具调用关系。

### 风险项

- P1-1 事件派发时机从“场景星星飞行动画结束后”前移到“存档和发奖成功后”，`PostcardManager` 的 `LevelWaitingAnimStar` 计数会更早增加；当前 UI 刷新仍等待 `GamePlayPanel` UI 飞行动画完成，但 Victory 或退出流程如果在飞行期间发生，可能提前消费计数，需 PlayMode 验证。
- P1-2 `GamePlayPanel.PostcardStar` 的坐标换算依赖当前 Canvas、Canvas camera 和关卡 `GameCamera` / `Camera.main`；特殊面板、特殊相机或 World Space Canvas 可能导致起点偏移。
- P1-3 UI 星星实例使用运行时 `Instantiate`/`Destroy`，虽然只在收集事件触发时执行，不在每帧执行，但仍需移动端 smoke 验证 GC、卡顿和多星连续收集表现。
- P2-1 `Postcard_Star.prefab` 和部分 level prefab YAML 中仍可能保留旧序列化字段文本；脚本不再读取旧字段，但 Unity Inspector 未保存前可能出现旧 override 残留，需后续 prefab 清理或确认不会误导配置维护。
- P2-2 特殊 `GamePlayPanel` 变体如果未继承或未绑定 `postcardStar`、`starBlock`，表现会降级为仅 HUD 刷新或跳过刷新；奖励和存档不会被阻断，但玩家可见反馈可能弱化。
- P2-3 如果面板关闭、重置或下一颗星连续触发时旧异步动画未正确终止，可能造成 UI 星星残留或旧流程晚到刷新；当前通过 `postcardStarPlayVersion`、`StopPostcardStarFlight()`、`StopPostcardStarEffects()` 缓解，仍需 PlayMode 验证。

### 缓解与验证

- `Postcard_Star` 保存失败时仍回滚本次 index、恢复触发器、不发放 Star、不派发事件，保持原失败边界。
- `PostcardGetStarEvent` 仍只有一个派发点，事件扫描确认监听方为 `GamePlayPanel` 和 `PostcardManager`；`PostcardManager` 不使用新坐标字段。
- `GamePlayPanel.PostcardStar` 在 `postcardStar`、Canvas、Camera 或 `starBlock` 缺失时跳过 UI 飞行，并继续走 HUD 刷新降级，不阻断奖励和存档。
- `GamePlayPanel.OnClose()`、`OnDestroy()` 进入 `StopPostcardStarEffects()`，会 kill 当前 UI 飞行 sequence、销毁当前 UI 星星实例，并清理 `PropBlockNotice` / CanvasGroup tween。
- `git diff --check -- Assets/Game/Scripts/Postcard/Postcard_Star.cs Assets/Game/Scripts/Play/PlayEvents.cs Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.cs Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs Assets/Game/UI/GamePanel/Prefabs/GamePlayPanel.prefab Assets/Game/UI/GamePanel/Resources/GamePlayPanel_025_70.prefab` 已通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 warning / 0 error。

### 未覆盖验证

- Unity Editor PlayMode：首次收集、重复触发、连续收集、重开、失败、胜利、退出再进、面板关闭中断动画。
- 坐标表现：默认 `GamePlayPanel.prefab` 与 `GamePlayPanel_025_70.prefab` 的 UI 星星起点、弧线、终点和 HUD 刷新顺序。
- 特殊面板关卡：未绑定 `postcardStar` 或没有标准 `starBlock` 的降级行为。
- Android/iOS 横屏：UI 飞行动画流畅度、触摸遮挡、暂停/退出/切后台状态一致性。
- 保存失败注入：确认保存失败不会派发带坐标事件，也不会创建 UI 星星实例。

### 回滚路径

- 回退 `Postcard_Star.cs` 到场景星星自身 DOTween 飞行动画方案。
- 回退 `PlayEvents.cs` 中 `PostcardGetStarEvent : EventBase<Vector3>` 为无参事件，并恢复 `Postcard_Star` 的无参派发。
- 回退 `GamePlayPanel.cs` 和 `GamePlayPanel.PostcardStar.cs` 的事件处理、UI 星星实例和清理改动。
- 删除 `GamePlayPanel.prefab` 和 `GamePlayPanel_025_70.prefab` 中 `postcardStar`、`postcardStarFlyDuration`、`postcardStarArcHeight`、`postcardStarFlyEase` 的新增绑定。

## 2026-07-02 Postcard_Star HUD 星星块展示反馈优化风险记录

### 原始行为基线

- `GamePlayPanel.PostcardStar` 原本在收到 `PostcardGetStarEvent` 后先播放 UI 星星飞行动画。
- HUD 星星道具块原本在 UI 星星飞行完成后才淡入、刷新数量、停留、淡出。
- 关卡开局没有从 `GamePlayPanel.PostcardStar` 主动展示 HUD 星星道具块。
- `Postcard_Star` 保存、发奖、事件派发和 `SearchAllStarInScene()` 编辑器排序能力不属于本次优化范围。

### 本次变更

- `GamePlayPanel.OnOpen()` 新增调用 `ShowPostcardStarBlockAtLevelStart()`，让 `starBlock` 开局展示后渐隐。
- 收到 `PostcardGetStarEvent` 时，`GamePlayPanel.PostcardStar` 先展示 `starBlock`，再播放 UI 星星飞行动画。
- UI 星星飞行动画完成后，`GamePlayPanel.PostcardStar` 刷新 `PropBlock.UpdateData()`，并对当前 `starBlock.transform` 播放一次简单缩放反馈。
- 星星块显隐由 `GamePlayPanel.PostcardStar` 自己维护 `starBlockSequence`，避免旧的停留/淡出流程覆盖新的收集展示。
- 面板关闭或销毁时会停止星星飞行、星星块显隐和缩放 tween，并恢复星星块原始缩放。

### 风险项

- P1-1 开局默认展示改变了 HUD 初始可见节奏，可能与个别关卡的开场演出、引导遮罩或按钮动画重叠；当前只影响 `starBlock` CanvasGroup，不改变玩法输入、存档和奖励。
- P1-2 收集时立即展示 `starBlock` 会让 HUD 在 UI 星星飞行期间可见，如果特殊面板布局靠近操作按钮，可能造成视觉遮挡；当前 `blocksRaycasts=false`、`interactable=false`，不应拦截触摸。
- P2-1 连续收集时旧隐藏 tween 被取消，后一次收集以最新流程为准；如果真实关卡允许极短时间连续收集，需要 PlayMode 验证最终数字和缩放反馈节奏。
- P2-2 缩放反馈作用在 `starBlock.transform`，如果特殊 prefab 上该 transform 同时承担布局缩放或父级动画，可能出现短暂叠加；当前关闭面板时会恢复缓存的原始缩放。
- P2-3 缺少 CanvasGroup 的特殊 `starBlock` 无法执行开局淡出和收集后淡出，只能刷新和缩放；该风险延续自依赖 CanvasGroup 的 HUD 提示设计，需要 prefab 抽样确认。

### 缓解与验证

- 未新增全局事件，继续使用已有 `PostcardGetStarEvent` 监听入口。
- 未修改 `Postcard_Star.cs`、`Bag.AddTool(...)`、`PostcardManager` 等待动画计数规则或编辑器排序逻辑。
- `GamePlayPanel.PostcardStar` 所有运行时入口均在 `GamePlayPanel.OnOpen()` 或 `PostcardGetStarEvent` PlayMode 监听路径内执行，并显式跳过非 PlayMode 收集事件处理。
- `starBlockCanvasGroup.blocksRaycasts=false` 且 `interactable=false`，HUD 展示不接管点击。
- `git diff --check -- Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.cs Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs` 已通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error，项目仍有既有 warning。

### 未覆盖验证

- Unity Editor PlayMode：开局展示后渐隐、收集时立即展示、UI 星星飞完后刷新和缩放、连续收集、面板关闭中断、重开、失败、胜利、退出再进。
- 默认 `GamePlayPanel.prefab` 与 `GamePlayPanel_025_70.prefab` 的 HUD 星星块 CanvasGroup、缩放原点和遮挡关系。
- 特殊面板变体：缺失 CanvasGroup、缺失 `postcardStar` 或有额外布局动画时的降级表现。
- Android/iOS 横屏：帧率、触摸遮挡、暂停/切后台状态一致性。

### 回滚路径

- 删除 `GamePlayPanel.OnOpen()` 中的 `ShowPostcardStarBlockAtLevelStart()` 调用。
- 回退 `GamePlayPanel.PostcardStar.cs` 中 `starBlockSequence`、`starBlockScaleSequence`、开局展示、收集时立即展示和刷新后缩放反馈相关改动。
- 若只需临时关闭缩放表现，可将 `starBlockScaleMultiplier` 调为 `1`，保留展示和刷新时序。

## 2026-07-15 GameObjectActiveSelfListener_Bubble 风险记录

### 原始行为基线与变更

- 原脚本只有序列化引用和需求注释，不会响应 `Postcard_Star` 的隐藏状态。
- 本次实现只观察 `star.activeSelf`：`Start` 时已隐藏则立即关闭提示，运行中由显示变隐藏则同步渐隐 `SpriteRenderer` 与 `TMP_Text`，完成后关闭提示节点。
- `Level_003.prefab` 当前仅绑定 `star`，脚本会从 `StarTipBubble` 子层级自动查找未绑定的 `bubble` 与 `content`。

### 风险项

- P2-1 自动查找使用子层级中的第一个 `SpriteRenderer` 和第一个 `TMP_Text`；当前层级各只有一个符合对象，若未来增加同类型表现，应改为显式绑定并重新验证。
- P2-2 检测依据按需求使用 `activeSelf`，不会把仅由父节点失活造成的 `activeInHierarchy == false` 视为星星收集；当前 `Postcard_Star` 的已收集和收集成功路径都会直接关闭星星自身。
- P3-1 目标可见期间每帧读取一次 `activeSelf`；静态绑定只命中 Level_003 的一个组件，开销有限，但移动端尚未实测。
- P3-2 DOTween 渐隐依赖当前箭头和文字颜色 alpha；如果后续其他动画同时修改这两个颜色，可能出现表现覆盖，需要 PlayMode 验证后再决定是否协调。

### 缓解、验证与回滚

- `isFading` 保证一次隐藏过程只创建一个 Sequence；组件保持启用，因此提示节点或关卡层级禁用时 `OnDisable()` 能 Kill 当前 Sequence。
- `star` 缺失时提示会记录警告并直接隐藏，不会留下永久误导提示。
- 未修改 DB/save、奖励、背包、事件、资源加载、配置、analytics、广告、IAP 或平台 SDK。
- 局部 `Assembly-CSharp` 构建通过，0 error / 99 warning；Unity PlayMode、Level_003 已收集/未收集/现场收集、重开退出和 Android/iOS 横屏仍未验证。
- 回滚方式：恢复 `GameObjectActiveSelfListener_Bubble.cs` 的空实现；需要完整撤销时再移除 `Level_003.prefab` 上对应 MonoBehaviour。
