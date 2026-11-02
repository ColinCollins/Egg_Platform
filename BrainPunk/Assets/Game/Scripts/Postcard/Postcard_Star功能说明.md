# Postcard_Star 编辑器排序工具记录

## 需求背景

`Postcard_Star.SearchAllStarInScene()` 通过 Odin Inspector 按钮触发，用于在当前关卡或 prefab 根节点下查找全部 `Postcard_Star`，并自动整理每个星星的 `Postcard_StarNum` 序号。原方法只有注释，没有实际排序、越界提示或 prefab 修改记录。

## 影响范围

- 修改脚本：`Assets/Game/Scripts/Postcard/Postcard_Star.cs`
- 相关资源：`Assets/Game/Prefabs/LevelItems/Postcard/Postcard_Star.prefab`
- 已知使用点：`Assets/Game/Prefabs/Level/Level_070.prefab` 中存在 `Postcard_Star` prefab 实例文本记录
- 未修改存档、DB、RemoteConfig、Luban 配置、资源加载、热更、广告、内购、analytics 或平台 SDK

## 原有行为基线

- `Postcard_Star` 只有 `index` 序列化字段和 `Postcard_StarNum.One/Two/Three` 枚举。
- `SearchAllStarInScene()` 位于 `#if UNITY_EDITOR` 内，已有 Odin `[Button("Search level")]` 入口，但方法体只有“遍历当前 level prefab，查看是否有其他的 Star，StarNum 自动排序。如果超出，弹出警告。”注释。
- 点击按钮不会修改任何星星序号，也不会提示超过三颗星的配置错误。

## 当前行为

- 点击 `Search level` 后，从当前对象向父级查找名称以 `Level_` 开头的根节点；找不到时回退到 `transform.root`。
- 在该根节点下包含 inactive 对象查找全部 `Postcard_Star`。
- 按 Unity 层级 sibling index 路径排序，依次写入 `One`、`Two`、`Three`。
- 如果查到的星星数量超过 `Postcard_StarNum` 可用数量，弹出 `EditorUtility.DisplayDialog` 警告并停止写入，避免越界或部分排序。
- 写入时使用 `Undo.RecordObjects` 支持撤销，`EditorUtility.SetDirty` 标记修改；如果对象属于 prefab instance，则调用 `PrefabUtility.RecordPrefabInstancePropertyModifications` 记录覆盖。

## 数据与资源流

- 该工具只读当前层级中已存在的 `Postcard_Star` 组件，只写组件本地序列化字段 `index`。
- 不创建对象，不实例化 prefab，不访问运行时 DB/save/config/manager，不读取或写入资源加载路径。
- 运行时代码不包含该方法实现；相关逻辑被 `#if UNITY_EDITOR` 包裹。

## 验证记录

- `git diff --check -- Assets\Game\Scripts\Postcard\Postcard_Star.cs`：通过。
- 静态风险扫描 `Postcard_Star.cs`：未发现 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、Instantiate、PlayerPrefs、Resources、YooAsset、Analytics、IAP 或 Advertisement；命中项仅为编辑器 prefab 记录 API 和工具日志。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 error，96 warning。warning 属于当前工程既有范围。

## 剩余风险

- 尚未在 Unity Editor 中实际点击 Odin `Search level` 按钮验证 Inspector 显示、Undo、prefab override 和弹窗交互。
- 当前排序依据为层级顺序；如果策划期望按场景坐标或手动命名排序，需要另行确认规则。
- Android/iOS 无需运行时验证，因为工具代码只在编辑器宏内编译。

## 回滚方式

- 回退 `Postcard_Star.cs` 中 `SearchAllStarInScene()` 的实现和新增 editor using/helper 方法。
- 删除本记录文件即可恢复到仅保留 Odin 按钮和空方法的状态。

## 2026-06-30：Postcard_Star 1.0 运行时收集流程

### 需求背景

`Postcard_Star` 需要在玩家触碰后完成一次性收集：使用当前 `levelData.Id` 作为 `DB.Postcard.LevelCollectedStar` 的关卡存储编号，记录当前 `Postcard_StarNum`，同时给玩家背包增加 1 个 `GameProps.Star`。收集反馈需要结合 `PostcardGetStarEvent` 和 HUD 星星 `PropBlock`，并且 `PropBlock` 数字刷新必须发生在场景星星飞行动画完成之后。

### 影响范围

- 脚本：`Assets/Game/Scripts/Postcard/Postcard_Star.cs`
- 事件：`Assets/Game/Scripts/Play/PlayEvents.cs` 中新增 `PostcardGetStarEvent`
- UI 脚本：`Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.cs`
- 新增 UI 辅助组件：`Assets/Game/UI/Global/Scripts/PropBlockNotice.cs`
- Prefab：`Assets/Game/Prefabs/LevelItems/Postcard/Postcard_Star.prefab`
- Prefab：`Assets/Game/UI/GamePanel/Prefabs/GamePlayPanel.prefab`
- 数据：`DB.Postcard.LevelCollectedStar`、`DB.SavePostcard()`、`PlayCtrl.Instance.Bag.AddTool(GameProps.Star, 1, RewardType.Other)`

### 原有行为基线

- `Postcard_Star` 运行时没有触发收集、背包奖励、存档写入或 HUD 提示逻辑。
- `Postcard.LevelCollectedStar` 已存在为 `Dictionary<string, List<int>>`，但 `Postcard_Star` 未在运行时读写该字段。
- `PropBlock` 默认会订阅 `UpdatePropEvent` 自动刷新；`SimpleBag.AddTool(GameProps, ...)` 会派发道具更新事件并保存 `GameData`。

### 当前行为

- `Awake()` 仅在 PlayMode 中检查当前关卡 `Level.CurrentLevelData.Id`，如果该关卡 id 下已记录当前 `index`，则隐藏并关闭触发器。
- 玩家触碰未收集星星时，脚本先关闭触发器并设置收集中状态，避免重复触发。
- 收集流程先把当前 `index` 写入 `DB.Postcard.LevelCollectedStar[levelData.Id]` 并调用 `DB.SavePostcard()`；保存成功后才调用 `PlayCtrl.Instance.Bag.AddTool(GameProps.Star, 1, RewardType.Other)` 发放 1 个星星道具。
- 如果 `PlayCtrl` / `Bag` 缺失或 `DB.SavePostcard()` 返回失败，本次收集会中止，回滚本次写入的内存记录，恢复触发器，不发放 Star、不隐藏星星，并记录 warning。
- 星星视觉根节点会飞向 `GamePlayPanel.TryGetStarBlockWorldPosition()` 返回的 HUD 星星块世界坐标，并缩放到 `collectedScale` 后隐藏；如果 HUD 目标缺失，则执行本地缩放消失降级动画。
- 飞行动画完成后派发 `PostcardGetStarEvent`；`GamePlayPanel` 收到事件后优先复用 `starBlock` 节点上的 `PropBlockNotice.ShowAndRefreshAfterHold()`，由 `PropBlockNotice` 淡入 HUD 星星块、刷新 `PropBlock.UpdateData()`、短暂停留并淡出；缺少该组件时降级为直接通过 `starBlock` 的 `CanvasGroup` 淡入、刷新、停留和淡出。
- `GamePlayPanel.prefab` 中 Star 对应 `PropBlock.IsAutoUpdate` 已关闭，避免 `SimpleBag.AddTool` 派发的 `UpdatePropEvent` 让 HUD 数字早于星星飞行动画刷新。

### 数据、事件与 UI 流

1. `Postcard_Star.OnTriggerEnter2D()` 检测到 `ActorCtrl` 或 `Player` tag 后进入收集流程。
2. `TryGetCurrentLevelKey()` 读取 `PlayCtrl.Instance.Level.CurrentLevelData.Id` 作为存储 key。
3. `TrySaveCollectionAndReward()` 写入 `DB.Postcard.LevelCollectedStar[levelKey]` 并保存 `Postcard`；保存失败时回滚本次写入并结束流程。
4. 保存成功后调用 `PlayCtrl.Instance.Bag.AddTool(GameProps.Star, 1, RewardType.Other)` 发放 Star。
5. `PlayCollectFeedbackAsync()` 读取 `CurrentGamePlayPanel.TryGetStarBlockWorldPosition()` 并播放 DOTween 动画。
6. 动画结束后派发 `PostcardGetStarEvent`。
7. `GamePlayPanel` 通过 `starBlock` 节点上的 `PropBlockNotice` 延迟刷新 Star `PropBlock`，提示结束后保持透明且不拦截点击；缺少组件时使用本地降级动画。

### 验证记录

- `git diff --check`：通过，仅有 Git 工作区 LF/CRLF 转换提示。
- 静态检查确认 `PostcardGetStarEvent` 的 Bear 事件派发签名存在，`CurrentGamePlayPanel`、`Level.CurrentLevelData`、`DB.Postcard.LevelCollectedStar`、`DB.SavePostcard()`、`SimpleBag.AddTool(GameProps, ...)`、`PropBlock.UpdateData()` 均有项目内代码证据。
- 静态生命周期扫描命中项仅为本功能预期的 `DB.Postcard`、`DB.SavePostcard`、`OnGameReset` 等运行时路径；未发现新增 `OnValidate`、`Reset`、`ExecuteAlways` 或 `ExecuteInEditMode`。
- 当前 worktree 未找到 `Assembly-CSharp.csproj`，无法用 `dotnet build .\Assembly-CSharp.csproj` 做 C# 编译验证。

### 剩余风险

- 尚未在 Unity Editor PlayMode 中验证真实触碰、动画终点、HUD 淡入淡出、数字刷新顺序、重复触发、重开、失败、胜利、退出和再次进入。
- 尚未在 Android/iOS 横屏设备验证飞行动画、HUD 提示和触摸输入是否有卡顿或遮挡。
- `DB.SavePostcard()` 失败路径已按规格中止本次收集，但仍需 PlayMode 注入或日志补验该失败分支是否恢复触发器且不发放 Star。
- 如果具体关卡使用特殊 `GamePlayPanel` 变体或移除了 Star `PropBlock`，奖励和存档会完成，但 HUD 目标可能走降级表现，需要逐关验证 prefab 绑定。

### 回滚方式

- 回退 `Postcard_Star.cs` 中运行时收集、存档、奖励和动画逻辑。
- 移除 `PlayEvents.cs` 中 `PostcardGetStarEvent` 以及 `GamePlayPanel.cs` 的订阅、处理和 `TryGetStarBlockWorldPosition()` API。
- 从 `GamePlayPanel.prefab` 移除 `PropBlockNotice` 组件绑定，并恢复 Star `PropBlock.IsAutoUpdate` 为原值。
- 删除 `Assets/Game/UI/Global/Scripts/PropBlockNotice.cs` 及其 `.meta`。

## 2026-07-01：Speckit Implement 执行补充

### 本次补充

- `Postcard_Star.TrySaveCollectionAndReward(...)` 增加内部二次去重结果：当 `DB.Postcard.LevelCollectedStar[levelData.Id]` 中已经包含当前 `index` 时，返回已收集状态并隐藏当前对象，不再调用 `DB.SavePostcard()` 或发放 Star。
- 该保护覆盖同一关卡中两个 `Postcard_Star` 误配相同 `index`、或多个触发路径几乎同时进入保存 helper 的情况，避免仅依赖外层 `IsSavedCollected(...)` 检查。
- 保存成功路径仍保持先 `DB.SavePostcard()`，再 `PlayCtrl.Instance.Bag.AddTool(GameProps.Star, 1, RewardType.Other)`；保存失败路径仍回滚本次新增 index、恢复触发器且不隐藏星星。

### 静态验证记录

- `specs/032-postcard-star/checklists/requirements.md`：16/16 已完成。
- `git diff --check`：通过，仅有 Git 工作区 LF/CRLF 转换提示。
- 生命周期扫描：未发现新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`。
- 范围扫描：`DBManager_Generated.cs`、`SimpleBag.cs`、`Luban`、`ProjectSettings`、`Packages` 无本功能 diff。
- Prefab 静态证据：`Postcard_Star.prefab` 已绑定 `index`、`triggerCollider`、`flyVisualRoot`、动画时长、fallback 时长、ease、`collectedScale`；`GamePlayPanel.prefab` 保留并绑定 `starBlock`，在该节点上补充 `PropBlockNotice`、`CanvasGroup` 与目标 Star `PropBlock` 绑定，并对 Star `PropBlock.IsAutoUpdate` 做关闭覆盖。
- 当前 `BrainPunk/` 下没有 `.csproj`，无法在本环境执行 `dotnet build`；需要用 Unity Editor 打开工程完成编译和 Domain Reload 验证。

### 剩余风险

- 未运行 Unity Editor PlayMode，真实触碰、动画终点、HUD 刷新顺序、重复触发、重开/失败/胜利/退出再进仍需补验。
- 未运行 Android/iOS 横屏设备 smoke，动画流畅度、触摸遮挡和暂停/切后台状态仍需补验。
- `DB.SavePostcard()` 返回 false 的失败分支已静态实现，但仍需 PlayMode 注入或断点补验。
- 关卡 prefab 中存在大量 `Postcard_Star` 使用点；误配重复 `index` 已有运行时防重复发奖保护，但仍建议在 Unity Inspector 中用排序工具或逐关抽样验证配置。

## 2026-07-01：PropBlockNotice 淡出 API 抽取

### 需求背景

`PropBlockNotice` 原本在 `ShowAndRefreshAfterHold()` 的内部流程末尾直接创建 `CanvasGroup.DOFade(0f, fadeOutDuration)` 淡出 sequence。为便于后续外部在需要保留隐藏过渡时复用同一套隐藏表现，本次把淡出隐藏抽为公开 `HideWithFade()` API。

### 影响范围

- 修改脚本：`Assets/Game/UI/Global/Scripts/PropBlockNotice.cs`
- 关联调用：`ShowAndRefreshAfterHold()` 停留结束后改为调用 `HideWithFade()`
- 未修改 `Postcard_Star.cs`、`GamePlayPanel.cs`、`PlayEvents.cs`、prefab、存档、背包、配置、资源加载、广告、IAP、analytics 或平台 SDK。

### 原有行为基线

- `PropBlockNotice` 淡入后可按需刷新 `PropBlock.UpdateData()`，等待 `holdDuration` 后在内部直接执行 `targetCanvasGroup.DOFade(0f, fadeOutDuration)`。
- 淡出完成后会把 `CanvasGroup.alpha` 置为 0，并保持 `interactable=false`、`blocksRaycasts=false`。
- `StopAndHide()` 语义是立即停止并隐藏，用于面板关闭、关卡重置或生命周期清理。

### 当前行为

- 新增 `HideWithFade()`：调用时会解析 `CanvasGroup`，停止当前提示流程，关闭交互和射线拦截，然后播放淡出动画。
- `ShowAndRefreshAfterHold()` 的内部流程停留结束后复用 `HideWithFade()`，玩家可见的淡出耗时、alpha 目标和不拦截点击状态保持不变。
- `StopAndHide()` 仍保持立即清理，不改为淡出，避免关闭、销毁、重置路径留下未完成 tween。

### UI 与事件流

1. `PostcardGetStarEvent` 触发后，`GamePlayPanel` 仍调用 `PropBlockNotice.ShowAndRefreshAfterHold()`。
2. `PropBlockNotice` 淡入 HUD 星星块，必要时刷新 `PropBlock.UpdateData()`。
3. 停留结束后统一进入 `HideWithFade()`，播放淡出并在完成后保持透明、不拦截点击。

### 验证记录

- 静态 diff 确认淡出 sequence 已集中到 `HideWithFade()` / `StartHideFade()`。
- `git diff --check -- Assets/Game/UI/Global/Scripts/PropBlockNotice.cs Assets/Game/Scripts/Postcard/Postcard_Star功能说明.md Assets/Game/Scripts/Postcard/Postcard_Star风险评估.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有工作区 LF/CRLF 转换提示。
- 生命周期关键字扫描：`PropBlockNotice.cs` 未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、`Instantiate`、`PlayerPrefs`、`Resources`、`YooAsset`、`Analytics`、`IAP` 或 `Advertisement`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 error / 97 warning。

### 剩余风险

- 尚未运行 Unity Editor PlayMode 验证 `HideWithFade()` 被外部直接调用时的 UI 过渡。
- 尚未在 Android/iOS 横屏设备验证 HUD 淡出过渡和触摸遮挡状态；当前静态证据仅能确认 `CanvasGroup` 继续关闭交互与 raycast。

### 回滚方式

- 删除 `PropBlockNotice.HideWithFade()` 和 `StartHideFade()`，把 `PlayNotice()` 末尾恢复为直接创建 `CanvasGroup.DOFade(0f, fadeOutDuration)` 的内联淡出 sequence。

## 2026-07-02：Postcard 存档读写集中到 PostcardManager

### 需求背景

为减少 `DB.Postcard` 读写散落，`Postcard_Star` 的永久星收集判断和保存改为通过 `PostcardManager` 公开 API 完成。`Postcard_Star` 继续负责触发判定、背包奖励和事件派发，不直接读写明信片存档。

### 影响范围

- 修改脚本：`Assets/Game/Scripts/Postcard/Postcard_Star.cs`
- 新增依赖 API：`PostcardManager.IsLevelStarCollected(...)`
- 新增依赖 API：`PostcardManager.TryCollectLevelStar(...)`
- 未修改 `PostcardGetStarEvent`、`GamePlayPanel` HUD 表现、背包奖励数量、prefab、配置、资源加载、广告、IAP、analytics 或平台 SDK。

### 当前行为

- `Awake()` 仍只在 PlayMode 中读取当前关卡 id；已收集状态由 `GameManager.Instance.Postcard.IsLevelStarCollected(levelKey, starIndex)` 判断。
- 玩家触发收集后，`TryCollectStarAndReward(...)` 调用 `PostcardManager.TryCollectLevelStar(levelKey, starIndex, out bool isNewCollection)`。
- `isNewCollection == false` 时按已收集处理并隐藏对象，不保存、不发奖。
- 新收集保存成功后才调用 `PlayCtrl.Instance.Bag.AddTool(GameProps.Star, 1, RewardType.Other)`，后续事件派发和 UI 表现顺序保持不变。
- 如果 `PostcardManager` 缺失或保存失败，收集流程中止，恢复触发器，不发奖、不派发事件。

### 验证记录

- 静态扫描确认 `Postcard_Star.cs` 不再直接出现 `DB.Postcard` 或 `DB.SavePostcard()`。
- 静态链路证据：`LoadManager.EnterGame()` 先调用 `GameManager.Instance.Init()` 创建 `PostcardManager`，之后 `GameManager.Instance.ReadyToPlay()` 才初始化玩法流程。
- `git diff --check` 和 `dotnet build` 需在本次集中迁移收尾后统一复跑。

### 剩余风险

- 尚未运行 Unity PlayMode 验证正常启动进入关卡时已收集星会在 `Awake()` 隐藏。
- 尚未验证开发者直开玩法场景时 `GameManager.Instance.Postcard` 是否已经存在；缺失时脚本会保守视为未收集。
- 尚未在 Android/iOS 横屏设备验证收集、保存和 HUD 表现。

### 回滚方式

- 将 `Postcard_Star` 的 `IsSavedCollected(...)` 和收集保存 helper 恢复为直接访问 `DB.Postcard.LevelCollectedStar` 与 `DB.SavePostcard()` 的旧实现。
- 从 `PostcardManager` 移除仅为 `Postcard_Star` 暴露的 `IsLevelStarCollected(...)` 和 `TryCollectLevelStar(...)` API，前提是没有其它调用方依赖。

## 2026-07-02：全关卡 Postcard_Star 批量刷新菜单

### 需求背景

策划整理关卡内明信片星星时，需要对 `Assets/Game/Prefabs/Level` 下所有 level prefab 批量执行一次 `Postcard_Star.SearchAllStarInScene()`，避免逐个打开 prefab 手动点击 Odin 按钮。

### 影响范围

- 新增编辑器工具：`Assets/Editor/PostcardStarLevelPrefabRefresher.cs`
- 新增菜单：`Tools/Bear/Postcard RefreshAllLevelStar`
- 读取/写入资源范围：`Assets/Game/Prefabs/Level/*.prefab` 中已有的 `Postcard_Star.index` 本地序列化字段
- 本次未修改 `Postcard_Star.cs` 运行时收集逻辑、`GamePlayPanel`、`PostcardGetStarEvent`、存档、背包、Luban 配置、RemoteConfig、资源加载、广告、IAP、analytics 或平台 SDK

### 原有行为基线

- 单个 `Postcard_Star` Inspector/Odin 按钮可以调用 `SearchAllStarInScene()`，从当前关卡根节点收集全部 `Postcard_Star`，按层级顺序写入 `One/Two/Three`。
- 批量处理关卡 prefab 需要人工逐个打开 prefab 并点击按钮，缺少统一菜单入口。

### 当前行为

- 菜单 `Tools/Bear/Postcard RefreshAllLevelStar` 仅在非 PlayMode 且 `Assets/Game/Prefabs/Level` 存在时可用。
- 工具通过 `AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Prefabs/Level" })` 收集 prefab 路径，按路径排序后逐个处理。
- 每个 prefab 使用 `PrefabUtility.LoadPrefabContents()` 打开内容，查找 inactive 在内的全部 `Postcard_Star`。
- 如果 prefab 中存在星星，只从第一个 `Postcard_Star` 调用一次 `SearchAllStarInScene()`；该方法会在关卡根节点内完成全量排序。
- 工具检查星星组件或 prefab root 是否 dirty；只有发生修改时才调用 `PrefabUtility.SaveAsPrefabAsset()` 保存，未变更的 prefab 只计入 unchanged。
- 执行期间显示 `EditorUtility.DisplayProgressBar()`，结束后在 Console 输出 saved / unchanged / no star / failure 汇总，并弹出结果对话框。

### 数据与资源流

- 工具只处理本地 prefab 资产内容，不把 prefab 实例化到当前场景。
- 工具复用 `Postcard_Star.SearchAllStarInScene()` 的排序规则，不新增排序标准。
- 工具不会访问运行时 `DB`、`SavePostcard()`、`PlayCtrl.Instance`、RemoteConfig、资源加载器或 manager 单例。

### 验证记录

- `git diff --check -- Assets/Editor/PostcardStarLevelPrefabRefresher.cs Assets/Editor/PostcardStarLevelPrefabRefresher.cs.meta`：通过。
- 静态菜单检查确认菜单路径为 `Tools/Bear/Postcard RefreshAllLevelStar`。
- 静态生命周期/风险关键字扫描：新增工具未包含 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB.`、`RemoteConfig`、`Instantiate`、`PlayerPrefs`、`Resources`、`YooAsset`、`Analytics`、`IAP` 或 `Advertisement`；命中项为预期的 `MenuItem`、`LoadPrefabContents`、`SaveAsPrefabAsset`、`AssetDatabase.SaveAssets` 和 `SearchAllStarInScene`。
- `dotnet build .\Assembly-CSharp-Editor.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：未进入真实编译，失败原因为 `Temp\bin\Debug\*.dll` 大量 Unity 生成程序集缺失。

### 剩余风险

- 尚未在 Unity Editor 中实际点击 `Tools/Bear/Postcard RefreshAllLevelStar` 验证批量保存结果、Console 汇总和进度条交互。
- 如果某个关卡下 `Postcard_Star` 数量超过 `Postcard_StarNum` 枚举数量，仍会走 `SearchAllStarInScene()` 原有弹窗和停止写入逻辑；批处理期间可能需要人工确认弹窗。
- 尚未对执行后的全部 prefab YAML 做 Unity Inspector 级别 Missing 引用验证。

### 回滚方式

- 删除 `Assets/Editor/PostcardStarLevelPrefabRefresher.cs` 和对应 `.meta`。
- 如果已执行菜单并保存 prefab，可按 Git diff 回滚对应 `Assets/Game/Prefabs/Level/*.prefab` 中的 `Postcard_Star.index` 变更。

## 2026-07-02：Postcard_Star 收集表现迁移到 GamePlayPanel

### 需求背景

用户重新创建了 `GamePlayPanel.PostcardStar.cs`，要求按该脚本注释重写 `postcard_star` 实现思路：场景 `Postcard_Star` 不再自己执行飞向 HUD 的场景物体动画，而是在成功获得后把星星世界坐标交给 `GamePlayPanel`，由面板创建 `postcardStar` UI 实例并播放弧线飞行；编辑器排序功能和获得后的 HUD 刷新思路需要保留。

### 影响范围

- 修改脚本：`Assets/Game/Scripts/Postcard/Postcard_Star.cs`
- 修改事件：`Assets/Game/Scripts/Play/PlayEvents.cs`
- 修改 UI 脚本：`Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.cs`
- 新增/完善 UI 分部脚本：`Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs`
- 修改 prefab：`Assets/Game/UI/GamePanel/Prefabs/GamePlayPanel.prefab`
- 修改 Level70 面板变体：`Assets/Game/UI/GamePanel/Resources/GamePlayPanel_025_70.prefab`
- 复用表现资源：`Assets/Game/UI/Postcard/Prefabs/Postcard_UIStarCollection.prefab`
- 本次未修改 `Postcard_Star.SearchAllStarInScene()` 编辑器排序实现、`PostcardManager` 临时计数、`PropBlockNotice`、存档字段、背包实现、Luban 配置、RemoteConfig、资源加载、广告、IAP、analytics 或平台 SDK。

### 原有行为基线

- `Postcard_Star` 在保存并发放 `GameProps.Star` 后，自己用 DOTween 移动 `flyVisualRoot` 到 `GamePlayPanel.TryGetStarBlockWorldPosition()` 返回的 HUD 世界坐标，并缩放隐藏。
- 场景星星飞行动画完成后才派发无参 `PostcardGetStarEvent`，`GamePlayPanel` 监听事件后通过 `PropBlockNotice.ShowAndRefreshAfterHold()` 或 CanvasGroup 降级流程淡入、刷新、停留、淡出。
- `Postcard_Star.SearchAllStarInScene()` 位于 `#if UNITY_EDITOR` 内，按关卡根节点层级顺序整理 `index`。

### 当前行为

- `Postcard_Star` 只负责 PlayMode 触发、当前关卡/星星 index 去重、通过 `PostcardManager.TryCollectLevelStar(...)` 请求保存永久星记录、`PlayCtrl.Instance.Bag.AddTool(GameProps.Star, 1, RewardType.Other)` 发奖，以及派发 `PostcardGetStarEvent`。
- `PostcardGetStarEvent` 改为携带 `Vector3 StarWorldPosition`，该坐标来自 `flyVisualRoot.position`，用于 UI 面板计算飞行动画起点；`PostcardManager` 仍只监听事件并累加临时星星计数，不依赖该坐标。
- `GamePlayPanel.PostcardStar` 监听 `PostcardGetStarEvent` 后，优先在当前 Canvas 下实例化 `postcardStar`，把场景星星世界坐标转换为 Canvas 本地坐标作为起点，再按二次贝塞尔弧线飞到 `starBlock`。
- UI 星星飞行动画结束后，继续复用原获得后刷新思路：优先调用 `PropBlockNotice.ShowAndRefreshAfterHold()`，缺少该组件时使用原 CanvasGroup 淡入、`starBlock.UpdateData()`、停留、淡出的降级流程。
- 如果 `postcardStar`、Canvas、Camera 或 `starBlock` 缺失，面板会跳过 UI 飞行表现，仍执行 HUD 刷新降级，不阻断已完成的存档和发奖。
- `GamePlayPanel.prefab` 和 `GamePlayPanel_025_70.prefab` 已绑定 `Postcard_UIStarCollection.prefab` 到 `postcardStar` 字段，并写入本地飞行耗时、弧线高度和 Ease 配置。
- `GamePlayPanel.OnClose()` 和 `OnDestroy()` 统一调用 `StopPostcardStarEffects()`，停止 UI 星星飞行、清理实例，并保留 `PropBlockNotice.StopAndHide()` / CanvasGroup tween 清理。

### 数据、事件与 UI 流

1. `Postcard_Star.OnTriggerEnter2D()` 检测到 `ActorCtrl` 或 `Player` tag 后进入收集流程。
2. `Postcard_Star` 读取当前 `PlayCtrl.Instance.Level.CurrentLevelData.Id` 和本地 `index`，先检查永久收集记录。
3. 未收集时调用 `PostcardManager.TryCollectLevelStar(...)` 写入 `LevelCollectedStar[levelId]` 并保存；保存失败则恢复触发器。
4. 保存成功后调用 `PlayCtrl.Instance.Bag.AddTool(GameProps.Star, 1, RewardType.Other)` 发放星星道具。
5. `Postcard_Star` 派发 `PostcardGetStarEvent`，参数为本次场景星星世界坐标，并立即隐藏当前场景星星对象。
6. `GamePlayPanel.PostcardStar` 把世界坐标投影到 Canvas 本地坐标，创建 `postcardStar` UI 实例并播放弧线飞向 `starBlock`。
7. UI 飞行完成后调用原 HUD 刷新链路，`PropBlockNotice` 或 CanvasGroup 降级流程负责展示、刷新、停留和隐藏。

### 验证记录

- `git diff --check -- Assets/Game/Scripts/Postcard/Postcard_Star.cs Assets/Game/Scripts/Play/PlayEvents.cs Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.cs Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs Assets/Game/UI/GamePanel/Prefabs/GamePlayPanel.prefab Assets/Game/UI/GamePanel/Resources/GamePlayPanel_025_70.prefab`：通过，仅有工作区 LF/CRLF 转换提示。
- 事件链扫描确认 `PostcardGetStarEvent` 监听点为 `GamePlayPanel` 和 `PostcardManager`；派发点为 `Postcard_Star`，且派发携带 `ResolveCollectionWorldPosition()`。
- prefab 静态扫描确认 `GamePlayPanel.prefab` 与 `GamePlayPanel_025_70.prefab` 均绑定 `postcardStar`、`postcardStarFlyDuration`、`postcardStarArcHeight`、`postcardStarFlyEase`。
- 关键字扫描确认本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`；命中项为运行时预期的 `PostcardManager` 保存 API 和 UI 飞行实例 `Instantiate`/`Destroy`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 warning / 0 error。

### 剩余风险

- 尚未在 Unity Editor PlayMode 中验证真实触碰、UI 星星起点/终点、弧线飞行、HUD 刷新顺序、重复触发、重开、失败、胜利和退出再进。
- 尚未在 Android/iOS 横屏设备验证 UI 星星飞行流畅度、HUD 提示不遮挡触摸，以及暂停、退出、切后台状态一致性。
- `Postcard_Star.prefab` 和个别 level prefab YAML 中仍可能保留旧序列化字段或旧 prefab override 文本；脚本已不再读取这些字段，需后续 Unity 保存 prefab 时自然清理或按变更范围专项清理。
- 特殊 `GamePlayPanel` 变体若没有绑定 `postcardStar` 或 `starBlock`，会降级为无 UI 飞行或仅刷新，仍需要代表性关卡抽样验证。

### 回滚方式

- 回退 `Postcard_Star.cs` 中事件派发时机和收集表现迁移改动，恢复场景星星自身 DOTween 飞向 HUD 的逻辑。
- 回退 `PlayEvents.cs` 中 `PostcardGetStarEvent` 的 `Vector3` 参数定义，并恢复无参派发。
- 回退 `GamePlayPanel.cs` 与 `GamePlayPanel.PostcardStar.cs` 中 UI 星星实例创建、弧线飞行和清理逻辑。
- 从 `GamePlayPanel.prefab` 与 `GamePlayPanel_025_70.prefab` 移除 `postcardStar` 相关新增序列化字段。

## 2026-07-02：Postcard_Star HUD 星星块展示反馈优化

### 需求背景

用户要求优化 `GamePlayPanel` 中明信片星星 HUD 表现：`starBlock` / `PropBlock` 在关卡开局默认展示后渐隐；玩家捡到明信片星星时需要立刻重新展示；UI 星星飞行动画完成后，星星道具块刷新数量并播放一个简单的个体缩放反馈。

### 影响范围

- 修改 UI 脚本：`Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.cs`
- 修改 UI 分部脚本：`Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs`
- 复用组件：`PropBlock`、`CanvasGroup`、`PropBlockNotice`、DOTween
- 本次未修改 `Postcard_Star.cs`、`PostcardGetStarEvent`、`PostcardManager`、编辑器排序工具、prefab 绑定、存档字段、背包奖励、Luban 配置、RemoteConfig、资源加载、广告、IAP、analytics 或平台 SDK。

### 原有行为基线

- `GamePlayPanel.PostcardStar` 收到 `PostcardGetStarEvent` 后先播放 UI 星星飞行动画。
- UI 星星飞行结束后才调用 `PropBlockNotice.ShowAndRefreshAfterHold()` 或 CanvasGroup 降级流程，让 HUD 星星块淡入、刷新、停留、淡出。
- 关卡开局没有由 `GamePlayPanel.PostcardStar` 主动展示星星道具块。

### 当前行为

- `GamePlayPanel.OnOpen()` 在订阅事件后调用 `ShowPostcardStarBlockAtLevelStart()`，开局直接展示星星道具块，停留 `starBlockInitialHoldDuration` 后渐隐。
- 收到 `PostcardGetStarEvent` 时，`ShowStarBlockForCollecting()` 会先停止旧的隐藏流程并立即展示星星道具块，然后继续播放 UI 星星飞向 `starBlock` 的动画。
- UI 星星飞行动画完成后，`RefreshPulseAndHideStarBlock()` 调用 `starBlock.UpdateData()` 刷新数量，随后对当前 `starBlock.transform` 播放一次放大再还原的缩放反馈。
- 缩放反馈结束与否不阻塞后续隐藏流程；刷新后星星道具块停留 `starBlockCollectHoldDuration`，再通过 CanvasGroup 淡出。
- 如果 `starBlock` 没有 CanvasGroup，逻辑会保留刷新和缩放反馈，但不会强制隐藏节点；已有 prefab 仍建议保持 CanvasGroup 配置。
- `StopPostcardStarEffects()` 现在同时清理 UI 星星飞行、星星块显隐 sequence、缩放 sequence，并在关闭面板时恢复 `starBlock` 原始缩放。

### 数据、事件与 UI 流

1. 关卡玩法面板打开，`GamePlayPanel.OnOpen()` 初始化状态、订阅事件，并展示一次 HUD 星星道具块。
2. 开局展示只读取本地序列化字段和当前 `starBlock` 引用，不读取 DB、不发奖、不派发事件。
3. 玩家触碰 `Postcard_Star` 后，保存和发奖仍由 `Postcard_Star` 完成，成功后派发已有 `PostcardGetStarEvent`。
4. `GamePlayPanel.PostcardStar` 收到事件后立即展示 `starBlock`，再根据事件坐标创建 UI 星星并飞向 HUD。
5. 飞行完成后刷新 `PropBlock` 数量，播放当前星星块缩放反馈，再停留并淡出。

### 验证记录

- `git diff --check -- Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.cs Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs`：通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 error，项目仍有既有 warning。
- 静态关键字扫描确认本次未新增 `OnValidate`、`ExecuteAlways`、`ExecuteInEditMode`、`DB.`、`Save`、`RemoteConfig`；`Instantiate` 命中为既有 UI 星星实例创建路径。

### 剩余风险

- 尚未在 Unity Editor PlayMode 中实际验证开局展示、收集时立刻展示、飞行完成后的缩放反馈、连续收集、面板关闭中断和重开。
- 尚未在 Android/iOS 横屏设备验证 HUD 显隐和缩放反馈的流畅度、触摸遮挡和暂停/切后台状态。
- 特殊 `GamePlayPanel` 变体如果缺少 `starBlock`、CanvasGroup 或 `postcardStar` 绑定，表现会按当前降级路径弱化，仍需代表性关卡抽样检查。

### 回滚方式

- 删除 `GamePlayPanel.OnOpen()` 中的 `ShowPostcardStarBlockAtLevelStart()` 调用。
- 回退 `GamePlayPanel.PostcardStar.cs` 中星星块显隐 sequence、开局展示、刷新后缩放反馈和相关序列化参数。
- 如只需关闭新增缩放反馈，可把 `starBlockScaleMultiplier` 设为 `1` 或回退 `PlayStarBlockScaleFeedback()` 调用点。

## 2026-07-15 GameObjectActiveSelfListener_Bubble 星星提示联动

### 需求与影响范围

- `GameObjectActiveSelfListener_Bubble` 用于让关卡内的星星说明气泡跟随目标 `Postcard_Star` 的显示状态：进入关卡时目标已隐藏则立即隐藏提示，游玩中目标被收集后则让箭头气泡与文字一起渐隐。
- 代码只修改 `Assets/Game/Scripts/Postcard/GameObjectActiveSelfListener_Bubble.cs`；现有绑定证据只命中 `Assets/Game/Prefabs/Level/Level_003.prefab` 的 `StarTipBubble`。
- 未修改 `Postcard_Star` 的存档、奖励、事件、音效、粒子、索引和编辑器排序流程，也未新增全局事件、配置、资源加载、analytics 或平台 SDK。

### 原有行为基线

- 脚本原本只有 `star`、`bubble`、`content` 三个序列化字段和需求注释，没有生命周期或显隐实现。
- `Level_003.prefab` 已绑定目标 `star`，但 `bubble` 和 `content` 字段为空；当前 `StarTipBubble` 子层级各有一个 `SpriteRenderer` 箭头和一个 `TMP_Text` 提示文本。
- `Postcard_Star.Awake()` 会根据已有收集状态隐藏自身，收集成功后也会通过 `gameObject.SetActive(false)` 隐藏自身。

### 当前行为

- `Start()` 自动补齐子层级中的 `SpriteRenderer` 和 `TMP_Text`；随后检查 `star.activeSelf`，目标已隐藏时直接关闭整个提示节点。
- 目标初始可见时，组件在 `Update()` 检测其变为 `activeSelf == false`，只触发一次 DOTween 渐隐；`bubble` 与 `content` 共用 `fadeDuration`，完成后关闭整个提示节点。
- `fadeDuration` 默认为 0.3 秒，可在 Inspector 调整；为 0 或没有可渐隐表现时立即隐藏。
- `star` 缺失时通过 Bear Logger 记录警告并隐藏提示；组件禁用时清理当前 Sequence，避免关卡卸载或节点关闭后残留 Tween。

### 验证与剩余风险

- 静态检查确认脚本 meta GUID 只在 `Level_003.prefab` 命中，且该实例的 `star` 已绑定；自动查找目标与当前子层级类型一致。
- `dotnet build Assembly-CSharp.csproj --no-restore /p:BuildProjectReferences=false` 通过：0 个错误、99 个警告。
- 尚未执行 Unity PlayMode：需要验证首次进入且星星已收集时提示不闪现、未收集时正常显示、收集后箭头与文字同步渐隐、关卡重开和退出清理。
- 尚未执行 Android/iOS 横屏验证；本次只增加 Level_003 单组件的轻量状态检测，但帧率与暂停/切后台表现仍未实测。
- 回滚方式：恢复该脚本为空实现；如同时撤销关卡配置，再移除 `Level_003.prefab` 上的组件绑定。
