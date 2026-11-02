# PostcardEnter 活动入口风险评估

## 评估范围

- 评估日期：2026-07-01
- 变更入口：`PostcardEnter` 注释驱动实现
- 涉及文件：`Assets/Game/UI/Postcard/Scripts/PostcardEnter.cs`
- 涉及系统：Postcard UI 入口、RemoteConfig/GlobalConst 活动开关、Bear UI 打开流程

## 原始行为基线

- `PostcardEnter` 空实现，不读取活动开关，不绑定点击，不打开 `PostcardPopup`。
- `PostcardEnter.prefab` 根节点持有 `PostcardEnter`，子节点 `clickArea` 持有 `CustomButton`。
- `PostcardPopup.Create()` 已是明信片弹窗打开入口。
- `ConfigManager.RemoteConfig.IsOpenPostcard()` 已提供明信片活动开关读取能力。

## 变更后链路

1. `PostcardEnter.OnEnable()` 在运行态读取 `ConfigManager.RemoteConfig.IsOpenPostcard()`。
2. 活动关闭时，入口 `gameObject` 被隐藏。
3. 活动开启时，脚本解析子节点 `CustomButton` 并绑定点击。
4. 点击后调用 `PostcardPopup.Create()`。
5. `PostcardEnter.OnDisable()` 解除点击绑定。

## 已缓解风险

- 编辑器态数据链路：`OnEnable()` 外层使用 `Application.isPlaying`，编辑器预览不会读取 RemoteConfig/DB 缓存。
- 重复绑定：绑定前先执行 `button.OnClick -= OpenPostcardPopup`，`OnDisable()` 也解除绑定。
- prefab 子节点绑定：脚本使用 `GetComponentInChildren<CustomButton>(true)`，能覆盖当前 `clickArea` 子节点结构。
- 入口关闭：配置为 `false` 时直接隐藏根物体，不保留可点击区域。

## 风险项

### P1-1 活动关闭后入口隐藏可能改变所在 UI 布局

- 问题：`gameObject.SetActive(false)` 会隐藏整个入口根节点。
- 影响：如果父级布局依赖该节点尺寸，入口关闭时可能改变其它 UI 排布。
- 当前处理：按注释要求“不显示 gameObject”实现。
- 后续验证：需要在承载该 prefab 的 UI 中检查活动开启/关闭两种状态布局。

### P1-2 RemoteConfig 未初始化时入口默认关闭

- 问题：`IsOpenPostcard()` 在远端值和 GlobalConst 都不可用时返回 `false`。
- 影响：若入口早于配置初始化显示，可能被隐藏。
- 当前处理：沿用项目已有 `ConfigManager.RemoteConfig.IsOpenPostcard()` 语义，不在入口脚本私自定义默认开启。
- 后续验证：需要确认 `PostcardEnter` 实例化时机晚于配置初始化，或由承载 UI 在配置完成后再启用。

### P2-1 CustomButton 结构变化可能导致点击未绑定

- 问题：当前脚本运行时自动查找子节点 `CustomButton`。
- 影响：如果 prefab 后续移除或替换 `CustomButton`，入口会显示但无法点击。
- 当前处理：当前 prefab 静态证据显示 `clickArea` 已绑定 `CustomButton`。
- 后续验证：prefab 结构调整时需要同步检查 `PostcardEnter`。

## 验证证据

- 静态证据：`PostcardEnter.cs` 保留原注释文本，新增运行时逻辑外层有 `Application.isPlaying`。
- 静态证据：`PostcardEnter.prefab` 根节点绑定 `PostcardEnter`，子节点 `clickArea` 绑定 `CustomButton`。
- 静态证据：`ConfigManager.RemoteConfig.IsOpenPostcard()` 读取 `level_config.isOpenPostcard` 或 `GlobalConst.IsOpenPostcard`。
- `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardEnter.cs Assets/Game/UI/Postcard/Scripts/PostcardEnter功能说明.md Assets/Game/UI/Postcard/Scripts/PostcardEnter风险评估.md Assets/Game/UI/Postcard/Scripts/PostcardEnter功能说明.md.meta Assets/Game/UI/Postcard/Scripts/PostcardEnter风险评估.md.meta DevelopMD/已有功能设计与使用记录.md`：通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，结果为 0 error / 97 warnings。

## 未覆盖验证

- Unity Editor PlayMode：活动开关开启、关闭、点击打开、反复启用禁用。
- Android/iOS 横屏 smoke：触摸点击区域、弹窗层级、入口隐藏后的布局表现。
- 配置初始化时序：冷启动时入口是否晚于 RemoteConfig/GlobalConst 可用。

## 回滚路径

- 回退 `PostcardEnter.cs` 中新增运行时实现。
- 删除 `PostcardEnter功能说明.md`、`PostcardEnter风险评估.md` 及对应 `.meta`。

## 2026-07-13 空通关列表风险修复

### 触发事实与原始风险

- 运行时堆栈确认 `PostcardEnter.IsLockLevel()` 在 `Enumerable.Max()` 抛出 `Sequence contains no elements`。
- `GameData.passedLevels` 默认初始化为空 `List<int>`，关卡胜利后才写入当前关卡，因此空列表是合法的新玩家/未通关状态，不应视为损坏存档。
- `BeginerPopup.Close()` 只是通过 `UIManager.DestroyUI()` 重新显示下层 UI，并间接触发 `PostcardEnter.OnEnable()`；异常根因不在弹窗关闭逻辑。

### 风险处理与影响边界

- `IsLockLevel()` 先检查 `DB.GameData`、`PassedLevels` 和元素数量；仅在非空时调用 `Max()`，否则使用 `0`。
- 保留原活动开关短路、关卡门槛比较、入口隐藏、按钮绑定、红点刷新和 `PostcardPopup` 打开流程。
- 不写 DB、不改变存档结构、不修改 RemoteConfig 默认值，也不触碰 Bear UI 栈、prefab、资源或平台流程。

### 验证证据

- 静态源码确认 `passedLevels` 默认空列表，`LevelCtrl.Victory()` 是已确认的写入入口。
- CodeGraph 确认 `IsLockLevel()` 只有一个调用方，修改没有扩大到共享 UI 或存档模块。
- 目标脚本 `git diff --check` 通过；`Assembly-CSharp.csproj` 构建通过，98 warnings / 0 errors。

### 未验证项与回滚

- Unity PlayMode 尚未覆盖：空列表且门槛大于 0、空列表且门槛等于 0、已有多条通关记录、关闭 `BeginerPopup` 后恢复下层 UI。
- 移动端横屏入口显隐和点击没有真机验证；本次没有新增持续分配或平台分支。
- 回滚为恢复原无保护的 `PassedLevels.Max()`；若业务决定空列表永远锁定，可把空列表分支改为直接返回 `true`，需另行验证门槛为 0 的配置场景。

## 2026-08-25 StartPanel OnOpen 刷新风险补充

### 原始风险与处理

- 原始风险：入口一旦由 `Refresh()` 隐藏，其自身 `OnEnable()` 无法在 inactive 状态下主动执行；复用打开 `StartPanel` 时可能继续显示旧状态。
- 当前处理：`StartPanel.OnOpen()` 使用包含 inactive 子节点的查询缓存 `PostcardEnter`，并在每次打开时调用其既有 `Refresh()`，不复制活动或解锁判断。

### 影响边界与风险项

- 每个 `StartPanel` 实例仅在首次需要时遍历一次子层级，后续复用缓存；没有新增逐帧开销。
- 首次创建可能发生两次刷新，但 `PostcardEnter.Refresh()` 在绑定前先解除同一回调，当前静态证据不支持重复点击订阅风险。
- 刷新仍会读取 RemoteConfig、通关列表与明信片引导状态；若 `StartPanel.OnOpen()` 早于这些运行时数据准备完成，入口仍可能按既有默认语义暂时隐藏。此次未改变配置初始化或缓存回退时序。
- `uncertain`：嵌套 prefab 未来被移除或替换时，查询会得到 null 并安全跳过，但入口状态刷新也会失效；需在 prefab 结构调整时同步检查。

### 验证与回滚

- 静态检查覆盖 inactive 节点查询、缓存复用、运行态门禁、按钮去重绑定，以及未新增存档写入/全局事件/资源实例化。
- `Assembly-CSharp.csproj` 定向构建通过，0 error / 106 warning；目标文件 `git diff --check` 通过，仅有 LF/CRLF 转换提示。
- Unity PlayMode 与 Android/iOS 横屏尚未验证，不能据此宣称运行时视觉和配置初始化时序已通过。
- 回滚只需删除 `StartPanel` 的缓存字段及 `OnOpen()` 调用，不影响 `PostcardEnter.OnEnable()` 的原刷新路径。
