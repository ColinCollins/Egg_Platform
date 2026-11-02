# DrawGame_ChoiceLevelPanel 功能与风险评估

## 2026-10-13 当前有效约定：分页与主线一致

### 用户确认与修改前核对

- 用户已确认“按主线规则替换旧约定”。本节替代下文 2026-08-04 的首页最大可玩页、关内退出指定页，以及后续浏览翻页、关闭和远端刷新保存页码的规则；历史段落仅保留为修改前证据。
- 验收对象：Draw 选关的页码记录和打开定位。主线 `ChoiceLevelPanel.GetInitialPageIndex` 在当前关卡数据存在且排序 ID 可定位时显示当前关卡所在页，否则夹取存档页码；`EnterLevel` 才写入页码，左右按钮、QuickTurn 拖动、`OnClose` 不写入。`LevelCtrl.CurrentLevelData` 根据持久化 `CurrentLevel` 查询，并不是“场上仍有运行关卡”的判断。
- 来源：`Assets/Game/UI/LevelChoicePanel/Scripts/ChoiceLevelPanel.cs` 的 `GetInitialPageIndex / RefreshPageIndex / EnterLevel / OnClose`，`Assets/Game/Scripts/Play/LevelCtrl.cs` 的 `CurrentLevel / CurrentLevelData`；Draw 基线为本面板 `OnOpen / OnClose / RefreshInitialPageIndex / RefreshRemoteConfig / ChangePage / OnQuickTurnPageChanged / TryEnterSelectedLevel`，`StartPanel.OnClickDrawLevel`、`GameManager.TryEnterDrawAtHighestPlayableLevel`、`DrawPlayCtrl.ExitToLevelSelection / FocusLevelSelectionOnHighestPlayable / FocusLevelSelectionOn`。
- 原流程与维护边界：Draw 在浏览、关闭和远端刷新时保存页码，首页再次覆盖为最高可玩关卡页，关内退出覆盖为退出关卡页；存档使用已有手写 `DrawGame.ChoiceLevelPanelPageIndex` 和 `DB.SaveDrawGame`。本轮不修改 DB 字段、生成器或生成文件、配置、Prefab、主线、已验收关卡、BearGame、Packages、GuruSDK、GF，也不改变远端初始化、广告、关卡清理或打点契约。
- 拟修改位置：本面板取消非进关入口的页码保存，恢复时补齐当前关卡数据有效性检查；首页复用 `GameManager.TryEnterDraw`，删除仅为旧分页定位使用的入口与方法；关内退出继续先恢复首页再打开 Draw 选关，但采用普通打开定位。集合补全、首关/TBC 解锁、胜利推进及其保存不在本轮改动范围内。
- 验证场景：首次打开、浏览按钮/拖动后关闭重开、选关进入后重开、当前关卡与保存页不一致、当前关卡数据缺失、保存页码越界、排序变化、首页打开和关内退出（包括加载中退出）。比较同一输入下主线与 Draw 的定位结果和页码写入时机；另检查首页与退出调用链、Prefab/生成绑定保留、编译和定向 diff。
- 未决项：无待确认产品前提。CodeGraph CLI 当前不可用，按当前源码、文档及资源只读核对；AGENTS 引用的 `specs/037-remote-resource-loading/plan.md` 不存在，本轮不涉及该计划的远端资源流程。Unity UI、真实存档和 Android/iOS 横屏体验需单独验证，不能由隔离逻辑验证或编译代替。

### 实施结果与验证

- 已修改四个运行时文件：本面板、`StartPanel.cs`、`GameManager.cs`、`DrawPlayCtrl.cs`。保留独立 Draw 存档、原进关入口、按钮/QuickTurn 表现与广告门禁；删除旧分页定位专用方法，没有新脚本或共享抽象。
- 首次修改后的补充核对（在对应补充修改前完成）：`TryInitializeData` 原先把不在排序中的 `CurrentLevel` 改为第一关，导致首次创建根本进不到“保存页码兜底”；主线 `LevelCtrl.Init / RefreshLevel` 没有对应的当前关卡回写。移除此项选关页修正才能满足缺失当前 ID 的分页验收。Draw 正常选关在 `TryEnterSelectedLevel` 接受选择后设置有效 `CurrentLevel`；进关/Replay/胜利分别使用实际选中身份、运行时身份或有效下一项，主线 Level999 与 OneLink 也先解析有效候选，不能依赖选关页提前把进度改成第一关。本轮仅移除此项回写，保留集合修复、首关解锁和其它进度消费者；新增首次创建时非法当前 ID 的同输入对比场景。
- 页码数据流：打开/重开 → 当前有效排序与当前关卡配置 → 当前关卡页，不能定位时夹取已有保存页；按钮/拖动/远端刷新 → 内存显示页、按钮、Item、QuickTurn；有效选关进入 → `CurrentLevel` 和 `ChoiceLevelPanelPageIndex` → 原 `DB.SaveDrawGame` → 原进关请求。首页和关内返回均走普通打开，不再二次覆盖页码。
- 同一套隔离验证执行实际 Draw 面板完整源码、从当前主线源码直接提取的分页/进入/关闭方法及 `LevelCtrl.CurrentLevelData`，并执行实际首页点击与关内退出方法。48 项检查：修改前 14 项不符合新验收，修改后 48 项全部通过；首次非法当前 ID 用例在补充修改前单独复现失败后再修正。Unity 对象、DB 落盘、SDK、控制器切换与显示为桩，只证明所执行方法的分页结果、写入时机和调用轨迹。
- Editor 条件编译：1222 个响应文件源码、0 error / 113 warning；Android 条件编译：1222 个源码、0 error / 118 warning。使用当前 Unity Bee 响应文件及 Unity Roslyn 编译器，仅将输出写入 TEMP，不重新构建或改写 Packages/GF。
- 静态复核：主线源码与修改前 SHA256 相同；本面板序列化字段声明一致，Prefab/生成绑定/DB 文件无本轮 diff；页码赋值仅保留在 `TryEnterSelectedLevel`。游戏侧源码和 Unity YAML 无已删除 Draw 定位接口引用；Dig 独立同名方法不受影响。定向 `git diff --check` 通过。
- 完整场景、日志、SHA256 与证据边界见 `DevelopMD/验证记录/Draw选关分页_2026-10-13/验证记录.md`。

### 剩余风险与回滚

- `uncertain`：未执行正式 Unity PlayMode 的完整点击/遮罩/动画/真实存档重启，以及 Android/iOS 真机、后台恢复、原生广告成功/失败/取消/无结果/晚到回调；iOS 条件编译未执行。原广告和加载流程只做未改代码及页码相关门禁核对，不能据此宣称所有入口平台表现一致。
- 旧档页码原来可能被浏览覆盖，本轮直接沿用历史值，不迁移或猜测真正的上次进关页；当前有效关卡仍优先。首次遇到失效当前 ID 时不再写回第一关，选中有效项后才沿原进关链更新当前关卡。
- 回滚仅按本轮四个代码文件 diff 恢复页码保存、两类定位及非法当前 ID 回写，并撤回本节/首页补充记录；修改前源码留存于 `C:/Users/admin/AppData/Local/Temp/brainpunk-draw-pagination-20261013/before/`。不得整文件覆盖后续用户改动；本轮没有 DB schema、配置、Prefab 或资源迁移。

## 需求与范围

- Draw 玩法需要一套参考主线 `ChoiceLevelPanel` 的分页选关功能。
- Draw 选关只读取 `DrawLevelSort`、`DrawLevelData` 和独立的 `DB.DrawGame`，不读写主线 `LevelSort`、`LevelData` 或 `DB.GameData`。
- 本次同时接入面板关闭按钮，并把选关项 prefab 作为 `choiceLevelItem` 暴露在 Inspector，便于后续替换样式。

## 原行为基线

- Draw 入口能够切换到 `DrawPlayCtrl`，但选关面板没有完整的配置分页、状态显示、独立进度和关卡加载闭环。
- `DrawPlayCtrl` 的早期实现仅能固定加载 `DLevel_001`。
- Draw 面板 prefab 的 Content 没有选关项，新增的 `close_btn` 也没有运行时回调。

## 当前行为

1. StartPanel 的 `drawLevelBtn` 通过 `PlayControllerManager` 激活 `DrawPlayCtrl`；StartPanel 入口不再提前播放仅用于界面切换的 `ClickTransformPanel`。主线 `10999 / Level_999` 选关项保持主线进关，不直接打开 Draw 选关。
2. `DrawPlayCtrl.Activate()` 打开 `DrawGame_ChoiceLevelPanel`。
3. 面板按 `DrawLevelSort` 分页，并用 `DrawLevelSort.Path` 查询 `DrawLevelData.Id`。
4. 选中关卡后，将 `DrawLevelSort.Id` 保存为 Draw 独立进度 ID，并将 `DrawLevelData.Path` 交给活动的 `DrawPlayCtrl`。
5. `DrawPlayCtrl` 复用主线 `EnterLevelLoading`：翻页完成回调中关闭 Draw 选关、加载并初始化目标关卡与 HUD；擦除完成回调确认关卡仍有效后才进入 Drawing 阶段并开放输入。
6. `close_btn` 通过 `GameManager.ReturnToStartPanel(...)` 退出 Draw 控制器并恢复 StartPanel，不会留下活动但无界面的 Draw 状态。

`choiceLevelItem` 是可在 Inspector 替换的 `DrawGame_LevelChoiceItem` prefab 引用，并提供只读的 `ChoiceLevelItem` 公共属性。替换样式时，新 prefab 必须显式绑定 Draw 组件声明的按钮、文本、状态节点、CanvasGroup 和 Draw 星星组。

## 数据、资源与事件流

```text
DrawLevelSort.Id
    -> DB.DrawGame.CurrentLevel / UnlockLevels / PassedLevels

DrawLevelSort.Path
    -> DrawLevelData.Id
    -> DrawLevelData.Path
    -> Assets/Game/Prefabs/DrawLevel/{Path}.prefab
```

- `DB.DrawGame` 保存当前关卡、最大关卡、解锁/通过集合和选关进关页码；面板初始化补齐空集合和首关解锁，失效当前关卡 ID 按 2026-10-13 有效约定保留并使用保存页码兜底。
- 第一条 `DrawLevelSort` 默认加入解锁集合。
- `DrawGame_LevelChoiceItem` 使用 Draw 专用 prefab 和显式序列化引用，状态、点击与星星展示完全读取 Draw 数据；星星组通过 Draw 专用组件读取 `DB.DrawGame.LevelWaitingAnimStar`，不再挂载或运行主线 `LevelChoiceItem`。
- 广告锁沿用现有奖励广告入口，成功后写入 Draw 解锁集合并进入关卡。
- `EnterLevelLoading` 继续负责主线同款翻页、擦除与淡出表现；Draw 不接管主线公告、免广告弹窗或主线 FSM，只在本地完成回调中开放 Draw 输入。
- 本次没有新增全局 `PlayEvents`；入口、选关、加载和关闭均走控制器与面板之间的局部调用链。

## 影响文件与系统

- `DrawGame_ChoiceLevelPanel.cs`：配置读取、分页、按钮、存档和进入关卡。
- `DrawGame_LevelChoiceItem.cs`：Draw 独立选关项状态、显示、星星与点击逻辑。
- `DrawGame_ChoiceLevelPanel.prefab`：Content、翻页、关闭按钮和 `choiceLevelItem` 引用。
- `DrawGame_Partial.cs`、`DB.DrawGame.cs`：Draw 存档初始化、修复和静态访问。
- `GameManager.cs`、`DrawPlayCtrl.cs`：Draw 控制器切换、主线同款过场与指定关卡加载。
- `StartPanel.cs`、`LevelChoiceItem.cs`：Draw 入口路由。

## 验证证据

- 静态调用链确认 StartPanel 的 Draw 入口不再创建 `ClickTransformPanel`，Draw 选关仍由激活后的 `DrawPlayCtrl` 持有。
- 静态调用链确认 `DrawPlayCtrl.TryEnterLevel(...)` 创建 `EnterLevelLoading`，翻页回调执行关卡加载，擦除回调在运行时有效后调用 `BeginDrawing()`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：0 个错误，258 个项目既有警告；首次 124 秒工具超时后已终止对应构建进程并以 300 秒窗口重新执行通过。
- Unity Editor 正在运行，`Editor.log` 于 2026-07-27 14:26:22 更新；末尾 800 行未发现 `error CS`、`Scripts have compiler errors` 或 `Compilation failed`。
- 静态确认 5 条 `DrawLevelSort` 都能映射到对应 `DrawLevelData`。
- 静态确认面板 prefab 已绑定 `content`、`choiceLevelItem`、`gridLayout`、`lastBtn`、`nextBtn` 和 `closeBtn`。

## 风险与未验证项

- `uncertain`：正式需求尚未明确 Draw 进度应使用排序 ID 还是配置 ID；当前实现使用 `DrawLevelSort.Id`，并把 `DrawLevelSort.Path` 仅作为 `DrawLevelData.Id` 外键。
- 当前 5 条排序配置中，资源目录只有 `DLevel_001.prefab`；`DLevel_002` 至 `DLevel_005` 点击后会进入现有加载失败回退流程。
- Draw 通关结果写入 `PassedLevels`、推进 `MaxLevel` 和解锁下一关尚未形成完整闭环。
- 替换 `choiceLevelItem` 样式时若漏绑任一必需引用，Draw 选关项会记录错误并隐藏数据项；星星组缺失只记录警告，不阻断关卡选择。
- `uncertain`：Draw 自身 `PlayIntroAsync()` 当前在 EnterLevelLoading 翻页完成回调内执行，玩家可见节奏需要在 PlayMode 确认；擦除完成前 Draw 输入保持关闭。
- 尚未执行 Unity PlayMode 的 StartPanel→Draw 选关→进关过场点击链、关闭返回、奖励广告、重复快速点击、加载失败注入、Android/iOS 横屏和切后台验证。

## 回滚

1. 将 `StartPanel.OnClickDrawLevel(...)` 恢复为先创建 `ClickTransformPanel`、再调用 `GameManager.TryEnterDraw(...)`。
2. 将 `DrawPlayCtrl.TryEnterLevel(...)` 恢复为关闭选关后直接启动 `OpenDrawLevelAsync(...)`，并把 `BeginDrawing()` 恢复到异步加载方法末尾。
3. 若需整体回退 Draw 选关，再回退 `GameManager`、`DrawPlayCtrl` 的指定关卡接口及面板的数据分页、独立选关项适配和关闭回调。
4. 本次没有修改存档 schema、配置或资源，单独回退过场接入不需要处理玩家数据。

## 2026-07-28 更新：Draw 选关星星组

### 需求与原行为基线

- 需求：参考 `Postcard_ChoiceLevelStarGroupCtrl` 实现 Draw 专用选关星星组，星星数量只读取 `DB.DrawGame`。
- 修改前 `DrawGame_LevelChoiceItem.ResolveReferences()` 会查找并隐藏复制自主线 prefab 的 `Postcard_ChoiceLevelStarGroupCtrl`，Draw 选关不显示任何历史星星。
- `DB.DrawGame` 已有 `levelWaitingAnimStar` 字典，但没有公共属性供 Draw UI 读取。

### 当前行为与数据流

```text
DrawGame_LevelChoiceItem.RefreshVisualState
    -> DrawGame_ChoiceLevelStarGroupCtrl.Refresh(DrawLevelData)
    -> DB.DrawGame.LevelCollectedStar[DrawLevelData.Id]
    -> 前 N 个 Toggle 点亮（限制为 0..3）
```

- `DrawGame_ChoiceLevelStarGroupCtrl` 复用主线星星组的空值保护、重复刷新复位和当前关卡位置偏移逻辑。
- 普通、当前、已通关状态显示 Draw 星星组；普通锁和广告锁状态隐藏。当前状态按 `stateOffset` 偏移，其余状态回到 prefab 初始锚点。
- Draw 星星 prefab 的三个 Toggle 保持显式序列化绑定；选关只读永久最高星记录 `LevelCollectedStar`，不读取或消费 `LevelWaitingAnimStar`。
- Draw 星级链路对齐主线职责：`DrawPlayCtrl` 在打开 VictoryPanel 前写入永久最高星和本轮新增等待数，VictoryPanel 一次性消费等待数，选关只读取永久最高星。
- Draw 数据完全保留在 `DB.DrawGame`，不访问 `DB.Postcard`、`PostcardManager`，不派发全局事件、不做逐帧刷新。

### 影响文件与资源

- `Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_ChoiceLevelStarGroupCtrl.cs`
- `Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_LevelChoiceItem.cs`
- `Assets/Game/UI/DrawGame/Prefabs/DrawGame_ChoiceLevelStarGroup.prefab`
- `Assets/Game/DB/DrawGame.cs`
- `Assets/Game/DB/DrawGame_Partial.cs`
- `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
- `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel.cs`
- `Assets/Game/UI/GameVictory/Scripts/DrawGame_VictoryStarGroup.cs`

### 验证证据

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 error / 1120 个工程现有 warning。
- 静态确认 `DrawGame_ChoiceLevelStarGroup.prefab` 的脚本 GUID 指向新组件，`starToggles` 显式绑定 3 个 Toggle，`stateOffset` 保持 `{x: 0, y: -16}`。
- 静态扫描未发现新组件包含 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`Update`、全局事件订阅或派发。

### 风险、未验证项与回滚

- 尚未执行 Unity PlayMode 的首次获得、重复同星、低星重玩、高星覆盖、Victory 等待数消费、0/1/2/3 星、锁定/当前/已通关和翻页重复刷新，也未执行 Android/iOS 横屏 smoke。
- 新增 `levelCollectedStar` 字段对旧档采用空字典兼容；旧档没有历史 Draw 星级可迁移，因此已通关旧关卡会保持 0 星，直到再次获胜产生 Draw 星级记录。
- 若回滚本次星级存档链，需同时恢复选关数据源、VictoryPanel 消费逻辑、Draw 胜利星组接口和 `DrawGame` 星字段；已写入的新增字段可由旧代码忽略，无需强制清档。

## 2026-07-28 更新：Draw 独立选关项

### 需求与原行为基线

- 需求：参考主线 `LevelChoiceItem` 重写 `DrawGame_LevelChoiceItem`，显示切换基本一致，但配置、进度和星星组全部使用 Draw 自有数据。
- 修改前 Draw 面板的 `choiceLevelItem` 字段类型仍是主线 `LevelChoiceItem`。运行时先实例化主线 prefab，再动态添加 `DrawGame_LevelChoiceItem`。
- 旧 Draw 适配器会禁用主线组件，并按节点名递归查找按钮、文本、状态节点和星星组；prefab 缺失或改名只能在运行时发现。

### 当前行为与数据流

```text
DrawGame_ChoiceLevelPanel
    -> Instantiate(DrawGame_LevelChoiceItem prefab)
    -> SetData(DrawLevelSort, DrawLevelData)
    -> DB.DrawGame.UnlockLevels / CurrentLevel / PassedLevels
    -> LevelLock / AdLock / Current / Passed / Normal
    -> DrawGame_ChoiceLevelStarGroupCtrl
```

- Draw prefab 直接挂载 `DrawGame_LevelChoiceItem`，面板不再动态 `AddComponent`，组件也不再查找或禁用主线 `LevelChoiceItem`。
- `DrawGame_LevelChoiceItem` 继承 `BaseAutoUIBind` 并拆分为 partial：点击区域、四组文本、四个状态节点和广告图标由 `DrawGame_LevelChoiceItem.Generated.cs` 维护，Draw 星星组与根节点 CanvasGroup 保留在业务脚本中显式绑定。
- `Init()` 负责校验自动绑定和 Draw 专用引用，带面板参数的 `Init(DrawGame_ChoiceLevelPanel)` 先记录所属面板，再复用该校验入口。
- 状态优先级与主线表现契约一致：未解锁时根据 `DrawLevelData.LockType` 显示普通锁或广告锁；已解锁时当前关卡优先于已通关状态，其余显示普通状态。
- 普通、当前和已通关状态进入 Draw 关卡；广告锁走现有 Draw 解锁入口；普通锁显示既有未开放提示。
- 数据边界保持独立：显示 ID 使用 `DrawLevelSort.Id`，锁类型使用 `DrawLevelData.LockType`，解锁/当前/通关读取 `DB.DrawGame`，星星使用 `DrawGame_ChoiceLevelStarGroupCtrl`。
- 本次没有新增全局事件、逐帧刷新、编辑器生命周期入口、存档字段、配置或平台分支。

### 影响文件与资源

- `Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_LevelChoiceItem.cs`
- `Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_LevelChoiceItem.Generated.cs`
- `Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_ChoiceLevelPanel.cs`
- `Assets/Game/UI/DrawGame/Prefabs/DrawGame_LevelChoiceItem.prefab`
- `Assets/Game/UI/DrawGame/ChoiceLevelPopup/Resources/DrawGame_ChoiceLevelPanel.prefab`

### 验证证据

- 在生成的 `Assembly-CSharp.csproj` 中临时纳入新 partial 文件后执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 error / 102 个工程现有 warning；验证后已移除临时工程项，不把 Unity 生成文件作为功能改动保留。
- 静态确认 Draw item prefab 根组件 GUID 指向 `DrawGame_LevelChoiceItem`，12 个序列化引用均非空；Draw 面板 prefab 的 `choiceLevelItem` 指向该 Draw prefab。
- 静态确认 prefab 的自动绑定字段名与 generated partial 完全一致，业务脚本不再重复声明这些 UI 字段，并实现了 `BaseAutoUIBind.Init()`。
- 静态确认 Draw 面板不再调用 `AddComponent<DrawGame_LevelChoiceItem>()`，Draw item 不再包含 `GetComponent<LevelChoiceItem>()`、节点名递归查找或 `Postcard_ChoiceLevelStarGroupCtrl`。
- 变更 BaseAutoUIBind 之前的 Unity `Editor.log` 已确认 Draw item、Draw 星星 prefab 与原脚本成功导入；本次尝试用 Unity batchmode 刷新 generated partial 时，编辑器在初始化阶段进入 crash handler，因此本轮没有取得 generated partial 的 Unity 导入证据。

### 风险、未验证项与回滚

- 尚未执行 Unity PlayMode 的五种状态、分页复用、广告回调期间翻页、点击进入、锁定提示和 0/1/2/3 星视觉验证。
- generated partial 仍需在 Unity 正常启动并刷新工程后确认导入、脚本重载和 Inspector 引用；命令行 C# 构建已通过，但不能替代该验证。
- Android/iOS 横屏、触控区域和不同分辨率布局尚未验证。
- 回滚时将面板字段恢复为主线 `LevelChoiceItem`，恢复实例化后动态添加 Draw 适配器，并把面板 prefab 引用恢复为主线 item prefab；不涉及存档迁移。

## 2026-07-27 更新：Draw 选关阶段保留 StartPanel

### 需求与原行为

- 需求：进入 `DrawGame_ChoiceLevelPanel` 时，StartPanel 不销毁也不关闭，继续作为 Draw 选关背后的主界面。
- 修改前 `DrawPlayCtrl.Activate()` 在创建 Draw 选关面板前调用 `UIManager.CloseAllUI(UILayer.Normal)`，会关闭 Normal 层的 StartPanel。

### 修改后行为与 UI 流程

```text
StartPanel.drawLevelBtn
    -> DrawPlayCtrl.Activate()
    -> 保留 StartPanel
    -> 打开 DrawGame_ChoiceLevelPanel(Popup)
    -> 玩家确认关卡
    -> EnterLevelLoading 翻页完成回调
    -> CloseAllUI(Normal)
    -> 关闭 StartPanel，销毁 Draw 选关并加载 DLevel
```

- 当前可见界面确实为 StartPanel 时保留 Normal 层；主线 `10999 / Level_999` 不再直接激活 Draw 控制器，不属于该选关背景流程。
- StartPanel 只在选关阶段保留；真正进入 Draw 关卡时仍会关闭，避免其继续显示或接收实际玩法阶段的交互。
- Draw 选关的 `close_btn` 仍通过 `GameManager.ReturnToStartPanel(...)` 恢复主线控制器；没有新增 UI 实例、全局事件、存档、配置或资源依赖。
- StartPanel 与 Draw 选关分属 Normal / Popup 层，Draw 选关继续使用现有 Popup 遮罩和层级拦截。

### 验证、风险与回滚

- 静态确认 `DrawPlayCtrl.Activate()` 在 StartPanel 当前打开且可见时保留 Normal 层，其它入口仍关闭 Normal 层；`OpenDrawLevelAsync(...)` 在关闭 Draw 选关和加载资源前关闭保留的 Normal 层。
- `Assembly-CSharp.csproj` 构建通过：0 error / 102 个项目既有 warning；目标 `git diff --check` 通过。
- `uncertain`：尚未在 PlayMode 确认 Popup 遮罩下 StartPanel 的可见效果、触控拦截，以及关闭选关返回时 StartPanel 动画是否符合预期。
- Android/iOS 横屏、返回键、快速重复点击和切后台尚未验证。
- 回滚时把 `CloseAllUI(UILayer.Normal)` 从 `OpenDrawLevelAsync(...)` 移回 `DrawPlayCtrl.Activate()`；不涉及数据迁移。

## 2026-07-28 更新：进关前释放 Popup 共享遮罩

### 需求、问题与原行为基线

- Draw 选关使用 `UILayer.Popup` 并由 Bear UI 显示该层共享 Mask；`EnterLevelLoading` 同样位于 Popup 层，但创建时明确不请求 Mask。
- 原流程先创建 `EnterLevelLoading`，再在其翻页完成回调中销毁 Draw 选关。此时 Popup 栈仍有转场 UI，`UIManager.DestroyUI(...)` 不会隐藏该层共享 Mask，导致选关遮罩被转场继承。
- 用户要求在创建进关转场前先关闭选关，并等待一帧让 Popup Mask 进入隐藏流程。

### 修复后行为与 UI 流程

```text
DrawGame_ChoiceLevelPanel.EnterLevel
    -> DrawPlayCtrl.TryEnterLevel
    -> 销毁 Draw 选关 Popup
    -> Popup 栈为空，UIManager.HideMask(Popup)
    -> 等待一帧
    -> 创建 EnterLevelLoading(Popup, isShowMask:false)
    -> 翻页完成后加载 DLevel、Draw HUD 与 Intro
    -> 擦除完成后开放 Draw 输入
```

- 等待放在 `DrawPlayCtrl` 的选关关闭与转场创建之间；仅在当前控制器及激活版本仍有效时继续。
- `EnterLevelLoading` 的关卡加载回调、擦除完成回调和 Draw 输入门禁保持不变。
- 转场创建异常改由既有 `HandleOpenFailure(...)` 记录并恢复 Draw 选关；异步请求仍以 `_isOpeningLevel` 防止重复点击。

### 影响范围与数据边界

- 运行时代码仅修改 `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`。
- 复用 Bear UI 的 `DestroyUI`、Popup 栈和 `UIMask.Hide()`，不修改共享 `UIManager`、`UIMask` 或 `EnterLevelLoading`。
- 不修改 `DB.DrawGame`、配置、资源路径、Prefab、全局事件、analytics、广告、IAP 或平台分支。

### 验证、剩余风险与回滚

- 静态确认选关销毁发生在 `EnterLevelLoading.Create(...)` 之前，二者之间存在一次 `UniTask.NextFrame()`。
- 静态确认等待后会复核 `_activationVersion`、控制器活动状态和 `_isOpeningLevel`，停用后的旧请求不会创建转场。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 1120 个工程既有 warning。
- 尚未执行 Unity PlayMode 目视验证和 Android/iOS 横屏 smoke；Popup Mask 淡出时长与翻页首帧的最终视觉效果仍需 PlayMode 确认。
- 回滚时删除 `OpenDrawLevelTransitionAsync(...)`，并把 `EnterLevelLoading.Create(...)` 恢复到 `TryEnterLevel(...)`；不涉及数据迁移。

## 2026-07-30 更新：Draw 选关当前态改按 MaxLevel

### 需求与原行为基线

- 需求：`DrawGame_LevelChoiceItem` 的 `CurrentState` 必须按 `DB.DrawGame.MaxLevel` 判断。
- 修改前 `DrawGame_ChoiceLevelPanel.IsCurrent(...)` 比较的是
  `DB.DrawGame.CurrentLevel`；玩家点击关卡或通关推进下一关后，`CurrentLevel` 会改变，
  因而选关当前态表达的是最近选择/准备进入的关卡。

### 修改后行为与数据流

```text
DrawGame_LevelChoiceItem.ResolveState
    -> DrawGame_ChoiceLevelPanel.IsCurrent(DrawLevelSort.Id)
    -> DB.DrawGame.MaxLevel == DrawLevelSort.Id
    -> State.Current
    -> CurrentState.SetActive(true)
```

- 只替换 `IsCurrent(...)` 的存档字段来源；`CurrentLevel` 仍用于选择关卡、进入/重玩关卡
  和初始化选关页，不改变这些既有职责。
- 未解锁状态仍优先返回普通锁或广告锁；已解锁项中，命中 `MaxLevel` 的项仍优先显示
  `CurrentState`，否则再根据 `PassedLevels` 显示已通关或普通状态。
- 本次不修改存档结构、通关推进、解锁集合、配置、Prefab、资源、全局事件、广告、
  analytics 或平台分支，也不增加逐帧刷新。

### 影响、验证、风险与回滚

- 影响代码：
  `Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_ChoiceLevelPanel.cs`。
- 静态检查已确认 `IsCurrent(...)` 唯一比较 `DB.DrawGame.MaxLevel`，并确认
  `DrawGame_LevelChoiceItem.ResolveState()` 的既有优先级未变化；定向
  `git diff --check` 已通过。
- 执行
  `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1 /p:UseSharedCompilation=false /nr:false`
  已通过，结果为 `0 error / 1119 warning`；警告为当前工程既有警告。
- `MaxLevel` 初始值为 `0` 时，不会有排序 ID 为正数的选关项显示当前态；首次通关后，
  当前态跟随已记录的最大通关关卡。该表现符合本次明确需求，但仍需 Unity PlayMode
  验证初始存档、通关推进、重开和返回选关四种场景。
- Android/iOS 横屏没有新增布局或触控逻辑，但当前态视觉仍需随上述 PlayMode 场景一并
  做设备 smoke。
- 回滚时仅把 `IsCurrent(...)` 的比较字段恢复为 `DB.DrawGame.CurrentLevel`；不涉及存档迁移。

## 2026-07-30 更新：Draw MaxLevel 推进规则对齐主线

### 需求与原行为基线

- 需求：`DB.DrawGame.MaxLevel` 的更新逻辑与主线 `DB.GameData.MaxLevel` 保持一致。
- 主线在当前关首次通关时，把 `MaxLevel` 更新为
  `Math.Max(旧 MaxLevel, CurrentLevel + 1)`；重复通关不会再次推进。
- 修改前 Draw 在 `RecordVictoryProgress()` 中独立于首次通关判断，使用当前
  `DrawLevelSort.Id` 推进 `MaxLevel`；因此首次通关第 1 关后保存的是 `1`，与主线保存
  下一关 ID 的语义不同。

### 修改后行为与数据流

```text
DrawPlayCtrl.RecordVictoryProgress
    -> nextLevel = 当前 DrawLevelSort.Id + 1
    -> 当前关不在 PassedLevels
       -> 写入 PassedLevels
       -> MaxLevel = Math.Max(旧 MaxLevel, nextLevel)
    -> 按 DrawLevelSort 配置顺序解锁下一关并更新 CurrentLevel
    -> DB.SaveDrawGame
```

- Draw 现在与主线一致：只在首次通关分支内更新 `MaxLevel`，写入“当前排序 ID + 1”，
  并通过 `Math.Max` 保证最大进度不回退。
- 重复通关已记录在 `PassedLevels` 的关卡不会修改 `MaxLevel`。
- 下一关解锁和 `CurrentLevel` 推进仍使用 `TryGetNextLevel()` 返回的配置顺序，不改变
  Draw 原有加载、胜利面板、星级、下一关按钮或回退流程。
- 当前 `tbdrawlevelsort.json` 的排序 ID 为连续的 `1～11`，所以 `当前 ID + 1` 与配置
  顺序中的下一关 ID 一致。若未来允许非连续 ID，必须同步调整主线一致性约束或配置门禁。
- `DrawGame_LevelChoiceItem` 继续使用 `MaxLevel == DrawLevelSort.Id` 显示
  `CurrentState`：首次通关第 N 关后，第 N 关显示已通关；若配置中存在并解锁第 N+1
  关，则第 N+1 关显示当前态。
- 本次不修改存档字段、Prefab、资源地址、配置内容、全局事件、广告、analytics 或平台分支。

### 影响、验证、风险与回滚

- 影响代码：
  `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`。
- 已静态对比 `LevelCtrl.Victory()` 与 `DrawPlayCtrl.RecordVictoryProgress()`，确认二者的
  首次通关门禁、`CurrentLevel + 1` 和 `Math.Max` 更新规则一致；定向
  `git diff --check` 已通过。
- 执行
  `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1 /p:UseSharedCompilation=false /nr:false`
  已通过，结果为 `0 error / 101 warning`；警告为当前工程既有警告。
- 旧 Draw 存档若已由原规则保存为“最大已通关 ID”，重复通关不会自动补写为下一关 ID；
  后续首次通关更高关卡时会按新规则推进。是否需要主动迁移旧档属于 `uncertain`，本次
  没有增加与主线不同的初始化修复。
- 通关当前配置末关时，`MaxLevel` 会与主线一致推进为“末关 ID + 1”，但不会解锁或显示
  不存在的选关项；该末关返回选关表现仍需 PlayMode 确认。
- 尚未执行 Unity PlayMode 的首次通关、重复通关、跨关跳转、末关结算和返回选关验证，
  也未执行 Android/iOS 横屏 smoke。
- 回滚时把 `MaxLevel` 更新恢复为首次通关判断之外的
  `if (MaxLevel < 当前排序 ID) MaxLevel = 当前排序 ID`；不涉及存档结构回滚。

## 2026-08-04 更新：DLevel_999 隐藏选关星星

### 需求与原行为基线

- 需求：Draw 选关中的 `Level_999 / DLevel_999` 不显示星星。
- 修改前 `DrawGame_LevelChoiceItem.RefreshVisualState()` 只在普通锁和广告锁状态隐藏星星；
  999 解锁后进入 `Normal` 或 `Current` 状态，因而会重新显示 Draw 星星组。

### 修改后行为与数据流

```text
DrawGame_LevelChoiceItem.RefreshVisualState
  -> DrawLevelData.Id == 20999
  -> DrawGame_ChoiceLevelStarGroupCtrl.Refresh(isShow:false)
  -> 星星组 GameObject 隐藏
```

- `DrawGame_LevelChoiceItem` 使用稳定的 `DrawLevelData.Id = 20999` 识别未完待续关卡；
  不依赖其动态 `DrawLevelSort.Id`。
- 999 在锁定、普通、当前或已通关状态下都不显示星星；普通 Draw 关卡仍按原规则在
  `AdLock / LevelLock` 隐藏、在 `Normal / Current / Passed` 显示。
- 本次只修改 `Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_LevelChoiceItem.cs`；
  不修改 prefab、星级存档、配置、资源、点击、解锁、广告、全局事件、analytics 或平台分支。

### 验证、风险与回滚

- `git diff --check` 通过。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo`：`0 error / 101 warning`；
  警告为项目现有告警。
- 静态确认 `tbdrawleveldata.json` 中 `20999 -> DLevel_999`，且显示门禁只改变传给星星组的
  `isShow`，不改变选关项状态和按钮绑定。
- `uncertain`：尚未执行 Unity PlayMode 的 999 锁定/解锁、分页复用和不同分辨率目视验证，
  Android/iOS 横屏也尚未验证。
- 回滚时删除 `WaitingForNextLevelDataId`，并恢复星星可见性只检查 `AdLock / LevelLock`；
  不涉及存档或资源回滚。

## 2026-08-04 更新：DrawGameEnter 与关内退出的选关分页定位

### 需求与原行为基线

- 主界面点击 `DrawGameEnter` 时，需要默认显示当前可玩的最大 Draw 关卡所在页。
- 玩家从运行中的 DrawGame 关卡退出时，需要先恢复主界面，再自动打开 Draw 选关并显示退出关卡所在页。
- 修改前 Draw 选关每次打开都优先按 `DB.DrawGame.CurrentLevel` 定位；主界面入口无法保证落到有效排序中最后一个可玩关卡，关内退出只恢复 `StartPanel`，不会自动重开 Draw 选关。

### 修改后行为与数据、UI 流程

```text
StartPanel.DrawGameEnter
  -> GameManager.TryEnterDrawAtHighestPlayableLevel
  -> DrawPlayCtrl.Activate / 打开 DrawGame_ChoiceLevelPanel
  -> 从最终有效排序末尾反查“已解锁且 DrawLevelData 存在”的关卡
  -> 显示该关卡所在页

DrawPlayCtrl.ExitToStartPanel（存在运行中或正在加载的 Draw 关卡）
  -> 捕获退出时 DrawLevelSort.Id
  -> GameManager.ReturnToStartPanel
  -> GameManager.TryEnterDraw
  -> StartPanel 作为 Normal 层背景，重新打开 Draw 选关 Popup
  -> 显示退出关卡所在页
```

- “当前能玩的最大关卡”按 `DrawGameLevelCtrl.LevelSorts` 的最终有效顺序判定，不按数值大小排序；候选必须同时存在于 `DB.DrawGame.UnlockLevels`，且能从 `DrawGameLevelCtrl` 取得 `DrawLevelData`。
- 关内退出使用退出前运行时保存的 `_drawLevelDisplayId`，不使用可能已被胜利推进到下一项的 `DB.DrawGame.CurrentLevel`。
- 仅在 `_isOpeningLevel`、Draw 关卡运行时、关卡实例或 Draw HUD 至少一项存在时自动重开选关；从选关页本身关闭或退出时仍只返回主界面，避免形成重复重开。
- 主线 `10999 / Level_999` 选关项不再调用 `GameManager.TryEnterDraw(...)`；玩家先进入 `Level_999`，再由关内 `enterDrawGame` 入口直达当前可玩的最大 Draw 关卡。
- 分页定位只刷新 `_pageIndex`、翻页按钮和当前页选关项；既有 `OnClose()` 继续保存页码。没有新增存档字段、全局事件、配置、资源、Prefab、广告、analytics 或平台分支。

### 影响文件与系统

- `Assets/Game/UI/StartPanel/Scripts/StartPanel.cs`
- `Assets/Game/Scripts/GameManager.cs`
- `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
- `Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_ChoiceLevelPanel.cs`
- 复用系统：Bear UI 的 Normal/Popup 层、`PlayControllerManager` 切换、`DrawGameLevelCtrl.LevelSorts`、`DB.DrawGame` 只读进度与既有页码保存。

### 验证证据

- 定向 `git diff --check` 通过；仅出现工作区行尾转换提示，没有空白错误。
- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:quiet -m:1 /p:UseSharedCompilation=false /nr:false /clp:ErrorsOnly`：通过，`0 error / 115 warning`；警告为当前工程既有告警。
- 静态确认 `DrawGameEnter` 改用专用最大可玩分页入口；其它 `TryEnterDraw(...)` 调用点未改。
- 静态确认关内退出先捕获运行时关卡 ID，恢复 StartPanel 后才重新激活 Draw，并且选关页自身退出不会触发自动重开。
- 首次编译尝试在 124 秒工具时限内超时且无错误摘要；随后以 300 秒时限重跑并成功完成，上述成功结果作为有效编译证据。

### 风险、未验证项与回滚

- `uncertain`：尚未执行 Unity PlayMode 的新档/旧档、多页解锁、广告跨页解锁、末尾 `DLevel_999`、关卡加载中退出、关内设置退出、选关页关闭和快速重复点击验证。
- `uncertain`：尚未执行 Android/iOS 横屏、触控遮罩、切后台和 StartPanel 动画被 Popup 覆盖时的目视验证。
- 若远端最终排序移除退出关卡，指定 ID 无法定位时会保留面板既有初始页，不阻断返回流程。
- 回滚时恢复 `StartPanel` 调用原 `TryEnterDraw(...)`，删除 `GameManager.TryEnterDrawAtHighestPlayableLevel(...)`、Draw 控制器的两组分页聚焦调用及面板的定位方法，并把 `ExitToStartPanel()` 恢复为仅调用 `ReturnToStartPanel(...)`；不涉及存档迁移或资源回退。

## 2026-08-04 更新：选关阶段不关闭 Normal 层

### 需求、基线与当前行为

- 需求：`DrawGameEnter` 打开选关 Popup 时不关闭 StartPanel 或其它现有 Normal UI；
  玩家确认进入具体 Draw 关卡时再关闭 Normal 层。
- 修改前 `DrawPlayCtrl.Activate()` 会检查 `StartPanel` 是否已打开且可见；条件不满足时，
  在创建 `DrawGame_ChoiceLevelPanel` 前关闭整个 Normal 层。
- 修改后激活阶段只打开 Popup 层的 Draw 选关，不再关闭 Normal 层；
  `OpenDrawLevelAsync()` 仍在创建 Draw HUD、加载 DLevel 前调用 `CloseAllUI(UILayer.Normal)`。

### 影响、验证、风险与回滚

- 运行时代码只修改 `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`；不修改选关 Prefab、
  `UIManager`、存档、配置、资源路径、事件、广告或 analytics。
- 定向 `git diff --check` 通过；`Assembly-CSharp.csproj` 构建通过，结果为
  `0 error / 0 warning`。
- `uncertain`：Unity PlayMode 尚未验证 Normal 层背景的触控是否被 Popup Mask 完整拦截、
  关闭选关后背景恢复、快速重复进入以及实际进关关闭时机；Android/iOS 横屏尚未验证。
- 回滚时恢复 `DrawPlayCtrl.Activate()` 中原有的 `StartPanel` 可见性判断和 Normal 层关闭调用；
  不涉及存档或资源回退。

## 2026-08-05 更新：Draw 选关 item 显示关卡配置 ID

### 需求、原行为与新行为

- 需求：参考主线 `LevelChoiceItem.OnGUI()`，在 Unity Editor PlayMode 中查看当前页每个 Draw
  选关 item 对应的关卡 ID。
- 修改前 `DrawGame_LevelChoiceItem` 只显示面向玩家的排序 ID，没有编辑器调试标签；若要确认
  item 实际关联的 Draw 关卡配置，需要另行查看配置或运行时数据。
- 修改后每个已激活且已绑定 `DrawLevelSort` 的 item 会在自身上方显示
  `DrawLevelSort.Path`。该字段是面板传给 `DrawGameLevelCtrl.GetLevelDataById(...)` 的
  `DrawLevelData.Id` 索引，例如 `20001`。
- 标签仅在 `UNITY_EDITOR` 编译条件及 Editor PlayMode 下绘制；空数据、隐藏 item 或空 Path
  不绘制。实现放在实际持有 item 数据和位置的 `DrawGame_LevelChoiceItem`，不在面板重复遍历。

### 影响范围与系统边界

- 代码：`Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_LevelChoiceItem.cs`。
- UI：只增加 IMGUI 调试标签，不修改 prefab、序列化字段、玩家可见的正式 UI、点击、分页或动画。
- 数据流：只读取当前 item 已绑定的 `DrawLevelSort.Path`；`OnGUI` 不访问 DB、Save、
  ConfigManager、运行时单例，不创建 GameObject 或其它运行时实例。
- 不修改存档、配置文件、资源地址、全局事件、广告、analytics 或平台分支；玩家构建不会包含
  `UNITY_EDITOR` 分支。

### 验证、剩余风险与回滚

- 定向 `git diff --check` 通过，仅有工作区既有的 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:quiet -m:1
  /p:UseSharedCompilation=false /nr:false /clp:ErrorsOnly` 通过，结果为 `0 error / 101 warning`；
  警告为工程现有告警。
- 静态确认 `OnGUI` 位于 `#if UNITY_EDITOR` 内，且绘制数据只来自 `sortData.Path`。
- `uncertain`：尚未在 Unity PlayMode 目视确认不同 Game 视图分辨率、分页切换和 Canvas 相机模式下
  的标签位置；本功能不会进入 Android/iOS 玩家构建。
- 回滚时删除 `DrawGame_LevelChoiceItem` 的 `Editor Preview` 区域；不涉及数据或资源回滚。

## 2026-08-05 更新：关内设置 Menu 直接返回 Draw 选关

### 需求、原行为与流程

- 需求：Draw 关内 `GameSettingPopup.MenuBtn` 不再先返回 StartPanel，直接恢复 `DrawGame_ChoiceLevelPanel`。
- 修改前 `DrawPlayCtrl.ExitToStartPanel()` 先调用 `GameManager.ReturnToStartPanel(...)` 切到主线并重建 StartPanel，再调用 `TryEnterDraw(...)` 重新激活 Draw 选关。
- 修改后 `DrawPlayCtrl.ExitToLevelSelection()` 保持 Draw 控制器激活，依次结束 analytics、使旧异步请求失效、取消胜利流程、销毁 DLevel/HUD、打开 Draw 选关，并用退出前的 `DrawLevelSort.Id` 定位分页。
- 选关创建失败仍回退 StartPanel；选关页自己的关闭按钮返回 StartPanel 的行为不变。

### 影响、验证、风险与回滚

- 影响运行时代码为 `IPlayPauseHost.cs`、`GameSettingPopup.cs`、`PlayCtrl.cs`、`DrawPlayCtrl.cs`；未修改选关 prefab、DB/Save、配置、资源、事件、广告或平台分支。
- CodeGraph 与精确引用扫描确认暂停宿主新契约只有 `GameSettingPopup` 调用，Main/Draw 两个实现齐全；定向差异格式检查通过。
- `Assembly-CSharp.csproj` 构建通过：`0 error / 1119 warning`，警告为工程现有告警。
- `uncertain`：Unity PlayMode 尚未验证 Draw Intro/Drawing/暂停返回、分页定位、Popup 背景/遮罩、快速重复点击；Android/iOS 横屏尚未验证。
- 回滚时恢复 `ExitToStartPanel()` 契约及 Draw 原先“切主线再重进 Draw”的流程；不涉及数据迁移。

## 2026-08-18 更新：关内设置 Menu 先恢复 StartPanel 再打开 Draw 选关

### 需求、原行为与修复后流程

- 新需求：Draw 关内点击 `GameSettingPopup.MenuBtn` 后，必须先恢复主界面 `StartPanel`，再弹出
  `DrawGame_ChoiceLevelPanel`。本节取代 2026-08-05“保持 Draw 控制器激活并直接返回选关”的现行要求。
- 修改前 `DrawPlayCtrl.ExitToLevelSelection()` 手动结束 Draw analytics、清理 DLevel/HUD，并在不切换玩法控制器的
  情况下直接创建 Draw 选关；该链路不会重新执行 `StartPanel` 的恢复入口。
- 修改后流程为：

```text
DrawGame_GamePlayPanel.PauseBtn
    -> GameSettingPopup.MenuBtn
    -> DrawPlayCtrl.ExitToLevelSelection()
    -> GameManager.ReturnToStartPanel()
    -> DrawPlayCtrl.Deactivate()
       -> level_end(exit) + Draw 运行时清理
    -> PlayCtrl.TryRestoreStartPanel()
    -> GameManager.TryEnterDraw()
    -> DrawPlayCtrl.Activate()
    -> DrawGame_ChoiceLevelPanel.Create()
    -> 定位到退出关卡所在分页
```

- `ExitToLevelSelection()` 在切换前保存退出关卡的 `DrawLevelSort.Id`，并只在关卡加载、运行时、关卡实例或 HUD
  任一仍存在时重新打开 Draw 选关；若已没有可恢复的关内现场，则停留在 StartPanel。
- 退出打点与现场清理由 `ReturnToStartPanel()` 触发的 `DrawPlayCtrl.Deactivate()` 统一执行；方法本身不再手动调用
  `ReportLevelEndAnalytics(...)` 或 `CleanupLevelRuntime()`，避免控制器切换时重复发送 `level_end(exit)` 或重复清理。
- 返回 StartPanel 成功、重新激活 Draw 失败时，现有 StartPanel 保持为安全落点；错误使用 `Bear.Logger` 记录。

### 影响范围与系统边界

- 运行时代码只修改 `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`；复用现有 `GameManager.ReturnToStartPanel()`、
  `GameManager.TryEnterDraw()`、`PlayControllerManager` 与 Bear UI 创建链。
- `GameSettingPopup`、`IPlayPauseHost`、Draw 选关 prefab、动画、Popup 遮罩、DB/Save、Luban 配置、资源路径、
  全局事件、广告、IAP、RemoteConfig 和平台 SDK 均未修改。
- 当前关提示资格、奖励墨水会话、异步加载版本、DLevel/HUD 与事件订阅继续由 Draw 控制器既有
  `Deactivate()` / `CleanupRuntime()` 清理；StartPanel 与 Draw 选关仍分别位于 Normal / Popup 层。

### 验证、剩余风险与回滚

- CodeGraph 与静态引用复核确认 Menu 链为 `GameSettingPopup.ExitLevel()` → `ExitToLevelSelection()`；新实现先调用
  `ReturnToStartPanel()`，成功后才调用 `TryEnterDraw()`，退出关卡分页定位仍保留。
- 静态确认本次退出路径不再直接调用 `ReportLevelEndAnalytics(...)`；`DrawPlayCtrl.Deactivate()` 仍是本链唯一的
  `level_end(exit)` 发送入口，符合 `DevelopMD/关卡打点生命周期约定.md` 的退出契约。
- `dotnet build .\Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo -v:quiet
  /p:UseSharedCompilation=false /nr:false /clp:ErrorsOnly` 通过：`0 error / 106 warning`；warning 为工程既有告警。
- `uncertain`：尚未在 Unity PlayMode 目视确认 StartPanel 先恢复、Draw 选关入场动画随后显示、退出关卡分页定位、
  快速重复点击及重新激活失败降级；Android/iOS 横屏触控与切后台尚未验证。
- 回滚时恢复 2026-08-05 的直接返回实现：由 `ExitToLevelSelection()` 手动结束退出打点、清理关内现场并调用
  `OpenLevelSelection()`；不涉及存档、配置、资源或数据迁移。

## 2026-08-26 更新：关闭 Draw 选关时复用保留的 StartPanel

### 需求、原行为与原因

- 需求：从 `DrawGame_ChoiceLevelPanel` 点击关闭回到主界面时，不得重播 `StartPanel` 的进场动画。
- Draw 选关阶段本来就保留 `StartPanel` 作为 Normal 层背景；面板关闭却调用完整的
  `GameManager.ReturnToStartPanel()`。旧链会先 `CloseAllUI(Popup)`，再切回主线，并由
  `PlayCtrl.TryRestoreStartPanel()` 关闭整个 Normal 层后重新打开已有 StartPanel。
- `StartPanel_Root` 带有默认状态为 `in` 的 Animator，且未保留 Disable 前状态。旧链对 StartPanel 根对象执行
  失活再激活，因而会让该 Animator 从 `in` 重新播放；同时 Draw 选关还会先被 Popup 全关，再由
  `DrawPlayCtrl.Deactivate()` 持有的 `CloseLevelSelection()` 销毁，形成重复生命周期处理。

### 修改后行为与 UI/控制器流

```text
DrawGame_ChoiceLevelPanel.ClosePanel
    -> GameManager.TryExitDrawLevelSelection
    -> 校验活动控制器为 Draw、StartPanel 已打开/可见/激活
    -> PlayControllerManager.TryEnter(Main)
    -> DrawPlayCtrl.Deactivate
       -> CloseLevelSelection 唯一销毁 Draw 选关 Popup
       -> 清理 Draw 订阅与临时运行态
    -> 复用仍处于激活状态的 StartPanel
```

- 正常取消选关不再调用 `CloseAllUI(Normal)`、`StartPanel.Create()` 或 StartPanel 的 `OnHide()` / `OnShow()`，
  因此不会重置 `StartPanel_Root` Animator，也不会重复触发首页打开刷新。
- 新入口仅服务 Draw 选关关闭按钮；关内 Menu、加载失败、IWanner 与其它完整退出继续使用
  `ReturnToStartPanel()`，其清理和安全落点语义不变。
- 若活动控制器不是 Draw，专用入口返回错误；若保留的 StartPanel 缺失、已关闭、不可见或层级失活，立即回退
  原完整恢复流程。切回 Main 后若 StartPanel 意外失效，也通过 `PlayCtrl.TryRestoreStartPanel()` 再次兜底。
- 本次只修改 `GameManager.cs` 与 `DrawGame_ChoiceLevelPanel.cs`；不修改 UIManager、StartPanel prefab/Animator、
  Draw 关卡、存档、Luban 配置、资源地址、全局事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。

### 验证、剩余风险与回滚

- CodeGraph 与静态代码确认 Draw 选关正常入口保留 StartPanel，具体进关仍在 `OpenDrawLevelAsync()` 关闭 Normal 层；
  专用关闭链只通过控制器切换触发 `DrawPlayCtrl.Deactivate()`，不预先关闭 Popup。
- `dotnet build .\Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo -v:minimal
  /p:UseSharedCompilation=false /nr:false`：通过，`0 error / 120 warning`，warning 为工程既有项。
- `git diff --check` 通过；目标脚本未新增 `Update`、全局事件、存档写入或资源加载。
- `uncertain`：尚未在 Unity PlayMode 目视确认 Animator 不重播、Popup Mask 隐藏、输入恢复、快速连点、
  控制器切换失败回滚、StartPanel 缺失兜底及 Android/iOS 横屏触控。
- 回滚时将关闭按钮恢复为调用 `ReturnToStartPanel()`，并删除 `GameManager.TryExitDrawLevelSelection()`；
  无需迁移存档、配置或资源。

## 2026-09-18 更新：远端刷新选关副本及广告身份保护

- 背景：RemoteConfig 晚到保存后，原选关 `_sortData` 仍是创建时副本。依据 `DevelopMD/RemoteConfigService_Design.md` 第 20.5 节，本轮补齐刷新而不重开面板或首页。
- 提交链：有效 Draw 候选保存/切换成功 → GameManager → DrawPlayCtrl 已持有面板 → `RefreshRemoteConfig()` → 拷贝新排序及 generation、约束当前页、按既有规则补等待关解锁、刷新按钮/item/真实 ID 星星、保存页码。关闭的缓存面板在下一次 OnOpen 检查版本；BaseUIView.OnOpen 已先设置 IsOpen。
- 影响脚本为本目录面板和 Item，配合 DrawGameLevelCtrl、DrawPlayCtrl、RemoteConfigService、GameManager、Draw HUD 和 StartPanel。复用现有 Prefab 绑定、布局、星星和存档 API，没有新增节点、全局事件或资源加载。
- item 绑定排序版本；普通点击先验证版本、显示序号对应的真实 ID 与数据对象。广告捕获发起时的 owner/sort/data/generation；发起后阻止分页、关闭和其它 item 进入，回调处理完再 finally 释放。旧结果不会按更新后的 item 字段解锁另一内容；成功后转场沿用原流程。
- 广告/暂停/转场期间 Service 保留最新有效 Draw 候选且不提前保存 Draw key；门禁结束后现有 GameManager.Update 提交。缓存和列表保存成功后再触发 UI，不在广告覆盖期间先换序列。
- 历史通关、解锁和 MaxLevel 仍按显示序号；排序缩短不删除旧记录，页面只约束显示范围。等待关的解锁使用已有规则，并非配置更新发放通关奖励；星星读取真实内容 ID。
- 验证：实际源码集合 Roslyn 0 错误；31 项真实方法验证覆盖排序、generation、旧身份、当前内容与转场/暂停门禁；17 项源码断言含副本重绑、页码约束、广告捕获与 finally、主页刷新及下一关受理进度同步。没有执行 Unity UI 动画或真实广告，不能据此认定界面和平台回调已完成验收。
- 风险：原生广告一直无结果或暂停/转场一直未结束时更新仍延后；新排序改变每个序号对应的内容是既定产品规则；刷新 UI 自身异常不会自动回滚已经提交的缓存/列表。真实保存失败、广告成功/失败/取消/无回调、后台恢复、关闭重开及 Android/iOS 横屏仍为 `uncertain`。
- 15 个无对应本地 Prefab 的 ID 已由用户确认为开发中关卡，本轮暂不处理、不作为阻断项；完整七维评估、场景矩阵、证据路径见 `Assets/Game/Scripts/Level/DrawLevel/DrawGameLevelCtrl功能与风险评估.md` 第 12 节。
- 回滚只撤销本节的刷新入口、generation 和广告门禁及其调用方，保留既有关闭复用 StartPanel 逻辑、其它任务代码及 5/10 秒请求策略；不清存档、不修改 SDK/Packages/GF。

## 2026-09-18 补充远端刷新导出日志

- 背景：iOS SDK 报告缺少游戏侧选关刷新证据。在既有 RefreshRemoteConfig 入口使用 SDK 公开持久化日志接口记录 `[DrawRemoteConfigUI]`，包含跳过时的 ready/open/rewardAd/viewGeneration，以及完成后的 generation、数量和页码；不改变门禁、分页、绑定、星星或存档调用。
- 不新增全局事件、Prefab 引用或运行时查找；关闭面板仍在原 OnOpen 路径处理。验证为真实引用编译 0 错误和云控相关 35 项方法/队列检查；选关 UI 显示与 iOS 导出 ZIP 尚未实测，仍为 `uncertain`。
- 完整字段和下次导出验收见 DevelopMD/RemoteConfigService_Design.md 第 34 节。回滚仅移除本次日志，恢复等价的单行早退；保留既有刷新功能。

## 2026-10-09 更新：复用选关快速分页组件

- `DrawGame_ChoiceLevelPanel` 通过 Prefab 显式绑定主线共用的 `LevelChoicePanel_QuickTurn`，以当前有效
  `_sortData.Count`、单页 `maxCount` 和 `_pageIndex` 初始化。共用组件负责拖动、区间显示以及拖动跨页／松手回调，
  不读取 Draw 配置、DB、资源或控制器状态。
- 最终交互调整为拖动跨页时立即更新 `_pageIndex`、分页按钮、Item 并保存页码，不再进入既有 `ChangePage`，因此
  不播放 Item 渐隐渐显或主线 Spine／音效；松手回调仅把 handle 与 `page_txt` 对齐到父面板最终页。左右翻页按钮
  仍进入原 `ChangePage` 并保留其淡入淡出。广告等待期间禁用拖动，远端排序提交与关卡页定位仍会重新同步组件。
- Draw Prefab 直接嵌套 `LevelChoicePanel_QuickTurnComp.prefab`，父脚本和组件内部引用均为 Inspector 显式绑定，
  没有名称扫描、全局事件或新存档字段。完整分组公式、主线复用边界、验证证据及回滚见相邻文档
  `LevelChoicePanel_QuickTurn功能与风险记录.md`。
- 定向 Roslyn 编译为 `0 error / 113 warning`，Prefab YAML fileID 唯一性与引用已静态检查；`uncertain`：尚未在
  Unity PlayMode、Android/iOS 横屏环境验证实际拖动、Draw Popup 层级位置、Canvas 缩放和快速连续切页表现。
- 后续在 Unity Prefab Mode 复核并修复源 QuickTurn 脚本挂载后，Draw 父 Prefab 的 `quickTurnComp` 已重新拖拽绑定、
  保存并由 Inspector 确认不再为 `Missing`；落盘引用指向当前源组件 fileID `2402698189282942233`。
- 共用 QuickTurn 后续把纯 X 平移改为沿轨道实际局部轴投影移动；当前源 Prefab 轨道为 `Z = 1.5°`，因此 Draw 中的
  handle 与 `page_txt` 也沿同一斜线更新。Draw 自身 `_pageIndex`、`ChangePage`、广告门禁、存档和 Item 动画未改变；
  定向 Roslyn 编译为 `0 error / 113 warning`，实际 Draw PlayMode 贴轨观感仍为 `uncertain`。
- 为避免 Comp 状态与 Draw `_pageIndex` 脱节，`RefreshQuickTurn` 现在同时传入当前页读取方法；按下和开始拖动时
  Comp 会先读取 `_pageIndex` 再建立预览基准。广告等待时仍由 `SetInteractable(false)` 阻止同步和拖动，远端刷新、
  左右翻页、退出定位及原 `ChangePage` 保存逻辑不变；定向 Roslyn 编译仍为 `0 error / 113 warning`。
- 拖动即时刷新与松手无表现调整后，定向 Roslyn 编译为 `0 error / 113 warning`；源码静态核对确认 QuickTurn 的
  Draw 拖动回调未调用 `ChangePage` 或 `SetCanvasGroupAlpha`。`uncertain`：尚未完成 Draw PlayMode、Android/iOS
  横屏触控、快速往返拖动和关闭重开后的页码保存实测。
- 左右按钮进入 `ChangePage` 时，QuickTurn 现在和 Draw LevelItem 使用同一节奏：刷新前立即置为透明，刷新后用
  `0.2s` 淡入；拖动即时换页不调用该透明度接口。透明度由源 Prefab 根节点显式绑定的 CanvasGroup 控制，失活时恢复
  Alpha 1。定向 Roslyn 编译为 `0 error / 113 warning`，实际 Popup 中整体淡入和快速连点表现仍为 `uncertain`。
