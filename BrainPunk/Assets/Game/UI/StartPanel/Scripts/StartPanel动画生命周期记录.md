# StartPanel 动画生命周期记录

## 需求与背景

`StartPanel` 的按钮和 Spine 进场动画只应在面板实例首次创建时播放。面板被其它 Normal 层界面遮挡后恢复，或关闭后复用已有实例时，不应由 `OnOpen` / `OnShow` 再次触发进场动画。

## 影响范围

- 代码：`Assets/Game/UI/StartPanel/Scripts/StartPanel.cs`
- UI：复用 `Assets/Game/UI/StartPanel/Resources/StartPanel.prefab` 的既有序列化引用，未修改 prefab
- 系统：Bear.UI 生命周期、DOTween 按钮动画、既有 `UISpineCtrl` 动画
- 未修改存档、配置、资源地址、全局事件、分析、广告或平台流程

## 修改前基线

- Bear `UIManager.OpenUI` 创建新实例时依次调用 `OnCreate()`、`OnOpen()`、`OnShow()`；复用实例时只调用 `OnOpen()`、`OnShow()`。
- `PlayBtn` 缩放动画和 `ChoiceLevelBtn` 位移动画位于 `OnOpen()`，因此复用打开时会再次播放。
- Spine 的 `loading -> loading01` 动画已位于 `OnCreate()`。
- `StartPanel.prefab` 根节点仅挂载 `StartPanel` 业务组件，没有额外挂载实现 `IUIAnimation` 的组件；当前 `OnShow()` 没有 StartPanel 专属进场动画来源。

## 修改后行为

- `PlayBtn` 缩放、`ChoiceLevelBtn` 位移与 Spine 动画统一在 `OnCreate()` 启动，每个面板实例只触发一次。
- `OnOpen()` 仅保留活动入口状态刷新，不再触发进场动画；当前会刷新 DrawGame 入口与 `PostcardEnter`。
- 未新增 `OnShow()` 覆写；恢复显示时继续使用 Bear.UI 基类的可见性处理，但不会重播本面板的按钮或 Spine 进场动画。

## 验证记录

- CodeGraph 静态确认 `UIManager.OpenUI` 的首次创建与复用生命周期调用顺序。
- 静态确认 `StartPanel` 的 DOTween 与 Spine 播放入口均位于 `OnCreate()`，`OnOpen()` 仅刷新活动入口状态。
- 静态确认 `StartPanel.prefab` 根节点没有额外 `IUIAnimation` 组件。
- `dotnet build Assembly-CSharp.csproj --no-restore`：默认并行和单进程禁用构建服务器两次尝试均长时间无输出并超时终止，编译结果为 `uncertain`；已清理本次构建产生的残留进程。
- Unity PlayMode、动画实际节奏与 Android/iOS 横屏验证：`uncertain`，尚未执行。

## 剩余风险与回滚

- 动画仅在实例创建时播放；若未来产品要求每次销毁后重建播放，现有行为仍会自然满足，因为新实例会重新执行 `OnCreate()`。
- 若未来在根节点新增 `IUIAnimation` 组件，Bear.UI 基类 `OnShow()` 会播放该组件动画，需要同步复核本记录的边界。
- 回滚方式：将 `PlayBtn` 和 `ChoiceLevelBtn` 的 DOTween 初始化移回 `OnOpen()`，即可恢复复用打开时重播。

## 2026-08-25：OnOpen 刷新明信片入口状态

### 需求与影响范围

- `StartPanel` 每次执行 `OnOpen()` 时，需按最新状态刷新其子层级中的 `PostcardEnter`。
- 代码只修改 `Assets/Game/UI/StartPanel/Scripts/StartPanel.cs`；复用现有嵌套 `PostcardEnter.prefab`，不修改 prefab、自动生成字段、动画、事件、存档或配置定义。

### 行为基线与修改后行为

- 修改前：`PostcardEnter` 依赖自身 `OnEnable()` 刷新；入口因条件不满足而隐藏后，后续 `StartPanel.OnOpen()` 不会主动重新判断，可能保留旧显隐状态。
- 修改后：`StartPanel.OnOpen()` 使用包含 inactive 子节点的层级查询解析一次并缓存 `PostcardEnter`，随后每次调用现有 `Refresh()`；首次打开可能与 `PostcardEnter.OnEnable()` 各刷新一次，现有按钮去重绑定可避免重复订阅。
- 入口刷新只读取现有 RemoteConfig、通关列表和引导状态，并复用现有红点刷新；不写存档、不新增全局事件，也不改变 `OnCreate()` 中仅播放一次的进场动画。

### 验证、风险与回滚

- 静态确认 `GetComponentInChildren<PostcardEnter>(true)` 可覆盖已隐藏的入口节点，缓存避免每次打开重复遍历层级。
- 静态确认 `PostcardEnter.Refresh()` 具备运行态门禁、按钮去重绑定和显隐判断。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 error / 106 warning。
- 目标文件 `git diff --check`：通过，仅有工作区 LF/CRLF 转换提示。
- `uncertain`：尚需 Unity PlayMode 覆盖入口由隐藏变显示、由显示变隐藏、引导强制显示后再次打开首页，以及 Android/iOS 横屏表现。
- 回滚时删除 `StartPanel` 的 `postcardEnter` 缓存字段和 `OnOpen()` 中两行解析/刷新调用；不涉及数据迁移或 prefab 回退。

## 2026-08-25：OnOpen 刷新 Draw 与 IWanner 入口进度

### 需求、行为与影响范围

- `StartPanel.OnOpen()` 新增对 `DrawGameEnter`、`IWannerGameEnter` 的包含 inactive 子节点查找、缓存与 `Refresh()` 调用，
  确保从对应玩法返回或复用首页时重新读取最新星星/Group 通关进度。
- 入口组件仍在自身 `OnEnable()` 刷新；首次创建可能重复执行一次有界只读遍历，不新增 Tween、Spine 动画、
  `Update()`、事件订阅或存档写入，`OnCreate()` 的一次性进场动画边界不变。
- Draw 仍先执行既有 RemoteConfig 显隐刷新，IWanner 的 IsOpen 判定仍由 `IWannerGameEnter.Refresh()` 自己维护。

### 验证、风险与回滚

- CodeGraph 确认两个入口只有 `StartPanel.OnOpen()` 调用其公开刷新 API；C# 构建通过：`0 error / 118 warning`。
- `uncertain`：Unity PlayMode 的首次创建、Normal 层遮挡恢复、玩法返回、inactive 入口重新显示及移动端横屏尚未验证。
- 回滚时删除 `drawGameEnter`、`iwannerGameEnter` 两个缓存字段及 `OnOpen()` 中对应调用；Postcard 刷新和动画生命周期不变。

## 2026-08-25：OnOpen 刷新 BtnGroups 成就入口

- `StartPanel.OnOpen()` 现在还会按 `GetOpenGaps()` 刷新 `BtnGroups/gas_btn` 成就入口显隐；iOS 真机叠加 Game Center 已认证门禁。
- 该刷新只进行常量级状态读取，不播放或重置 `OnCreate()` 中的一次性按钮/Spine 进场动画，也不新增逐帧逻辑。
- C# 构建通过；PlayMode、Android/iOS 原生成就 UI 和横屏触控仍为 `uncertain`。完整按钮职责、风险与回滚见
  `Assets/Game/UI/StartPanel/Scripts/StartPanelBtnGroups功能说明.md`。

## 2026-08-25：移除 ChoiceLevelBtn 进场动画

### 需求、基线与修改后行为

- 用户要求 `ChoiceLevelBtn` 不再播放动画。修改前 `StartPanel.OnCreate()` 会先把按钮锚点 X 改为 300，
  再延迟 1.4 秒以 0.3 秒 Tween 移回 X=0。
- 当前已删除该位置覆盖和 `DOAnchorPosX` 调用；按钮从面板创建起直接保持 `StartPanel.prefab` 的序列化位置。
- `ChoiceLevelBtn.OnClick -> OnShowChoiceLevel` 绑定、`PlayBtn` 缩放动画、BtnGroups 控制与用户现有注释掉的 Spine 代码均未修改。

### 验证、风险与回滚

- 静态确认 `StartPanel.cs` 已无 `ChoiceLevelBtn` 位置写入和 `DOAnchorPosX` 调用，点击回调仍保留。
- 本次不新增运行时分配、事件、配置、存档或平台分支；Android/iOS 共用同一静态按钮位置。
- `uncertain`：Unity PlayMode 首次打开与不同横屏分辨率下的最终布局尚未目测。
- 回滚时恢复 `OnCreate()` 中原锚点 X=300 与延迟 Tween 代码即可；prefab 和数据无需回滚。

## 2026-08-25：ChoiceLevelBtn 补齐缩放进场动画

### 需求、行为与影响范围

- 用户后续要求 `ChoiceLevelBtn` 参考 `PlayBtn` 补充 Scale 动画；本条替代上方“按钮完全不播放动画”的结论，
  但不恢复已经移除的锚点位移动画。
- `StartPanel.OnCreate()` 现在同时把 `PlayBtn`、`ChoiceLevelBtn` 本地缩放设为 0，延迟 1 秒后以 0.4 秒
  `Ease.OutBack(3)` 缩放到 1；两个按钮的参数和一次性生命周期一致。
- `OnDestroy()` 分别对两个按钮执行 `DOKill()`，避免面板销毁后 Tween 继续持有目标。
- 本次只修改 `StartPanel.cs`，不修改 prefab、字体、旋转、位置、按钮绑定、配置、存档、事件或平台流程。

### 验证、风险与回滚

- 静态确认两个按钮均存在 Scale 初始化、同参数 `DOScale` 和销毁清理；ChoiceLevelBtn 无 `DOAnchorPosX` 或其它位置写入。
- `uncertain`：Unity PlayMode 首次打开、动画期间点击，以及 Android/iOS 横屏表现尚未目测。
- 回滚时删除 `ChoiceLevelBtn` 的 Scale 初始化、`DOScale` 和 `DOKill` 三处增量即可；PlayBtn 与 prefab 无需回滚。

## 2026-08-26：main/clickArea 复用 PlayBtn 进入主线

### 需求、基线与影响范围

- `StartPanel.prefab` 的 `main` 节点补充大范围 `clickArea`，点击功能要求与 `PlayBtn` 一致。
- 修改前，`main/clickArea` 已包含 `CustomButton` 和射线接收组件，但 `StartPanel` 没有持有该按钮引用，点击不会触发主线进入流程。
- 本次影响 `StartPanel.cs` 与 `StartPanel.prefab`；不修改关卡、配置、存档、资源地址、分析打点、广告、内购或平台 SDK。

### 修改后行为与 UI/事件流

- Prefab 将 `main/clickArea` 的 `CustomButton` 绑定到 `mainClickAreaBtn`。
- `StartPanel.OnCreate()` 将该按钮绑定到既有 `OnClickPlay()`，`OnDestroy()` 对称解绑；没有复制进入主线的业务实现。
- 点击 `main/clickArea` 或 `PlayBtn` 都会派发相同的 `EnterLevelEvent`，参数均为当前 `CurrentLevelSort`，随后复用 `WaitToClose()` 延迟关闭首页。
- 新点击区域只增加一次按钮事件绑定，不新增 `Update()`、运行时实例化、存档访问或移动端平台分支。

### 验证、风险与回滚

- 静态核对 Prefab：新增节点是 `main` 的直接子节点，序列化引用指向其 `CustomButton`，脚本绑定和解绑均复用 `OnClickPlay()`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:quiet -m:1 /p:UseSharedCompilation=false /nr:false /clp:ErrorsOnly`：通过，`0 error / 1138 warning`。
- `uncertain`：尚未在 Unity PlayMode 与 Android/iOS 横屏上验证点击区域覆盖范围、与相邻按钮的射线层级及快速连点表现。
- 回滚时移除 `mainClickAreaBtn` 字段、生命周期绑定、Prefab 序列化引用和 `main/clickArea` 节点；不涉及数据迁移。

## 2026-08-26：Draw 选关关闭不再重启 StartPanel_Root Animator

### 原因与修改后边界

- 补充既有记录遗漏的资源事实：`StartPanel.prefab/StartPanel_Root` 挂有 `StartPanel_Root.controller`，默认状态为
  `in`，随后转入 `idle`；Animator 的 `m_KeepAnimatorStateOnDisable` 为 `0`，根面板失活再激活会从 `in` 重播。
- Draw 选关虽位于 Popup 层并保留 StartPanel，旧关闭链仍通过完整首页恢复把 Normal 层 StartPanel 失活后重新打开，
  这是关闭 Draw 选关时首页动画重播的直接原因，不是 `StartPanel.OnCreate()` 中 DOTween 再次调度。
- `DrawGame_ChoiceLevelPanel` 现在使用专用退出入口：切回 Main 时只让 `DrawPlayCtrl.Deactivate()` 销毁选关 Popup，
  已打开、可见且激活的 StartPanel 不执行 `OnHide()` / `OnShow()`，因此其 Animator 保持当前 `idle` 状态。
- 本次没有修改工作区已有的 `StartPanel_Root.controller`、`in.anim`、StartPanel prefab 或 `StartPanel.cs`；
  关内完整退出、主线 Normal 层页面返回等其它 StartPanel 恢复路径不在本次行为变更范围。

### 验证、风险与回滚

- 静态确认专用路径会校验 Draw 活动控制器及 StartPanel 的 `IsOpen`、`IsVisible`、`activeInHierarchy`，异常时回退
  原完整恢复；正常路径没有 StartPanel 关闭或创建调用。
- C# 构建通过：`0 error / 120 warning`；`git diff --check` 通过。
- `uncertain`：Unity PlayMode 的实际 Animator 状态、关闭瞬间 Popup Mask/输入、移动端横屏表现尚未目测。
- 回滚代码时恢复 Draw 选关调用 `ReturnToStartPanel()`；本记录对应结论同时失效，无需回退任何动画资源。

## 2026-08-26：进场结束后关闭 StartPanel_Root Animator

### 需求、基线与修改后行为

- 用户要求 `StartPanel_Root` 的 `in` 进场动画播完后关闭 Animator，解除其对 `main` 缩放的持续接管。
- 修改前：Animator 从 `in` 转入循环 `idle` 后仍保持启用；`in.anim` 包含 `main.m_LocalScale` 曲线，运行时对
  `main` 执行的按钮按压缩放会被 Animator 求值结果还原。
- 修改后：`StartPanel.OnCreate()` 启动一次状态监视；Animator 不处于过渡且已进入 `idle`，或非循环
  `in` 的 `normalizedTime >= 1` 时，立即设置 `Animator.enabled = false`。逻辑按实际状态结束，不依赖写死的动画秒数。
- Animator 关闭后，同一 StartPanel 实例不再重播 `in`，`main` 的 `ClickScaleAnim` 可独立写入缩放；同时，
  同控制器 `idle.anim` 中的门循环位移动画也会按用户确认的取舍停止。

### 影响范围与数据流

- 本次仅修改 `Assets/Game/UI/StartPanel/Scripts/StartPanel.cs`；不改动工作区已有的
  `StartPanel_Root.controller`、`in.anim`、`idle.anim` 或 `StartPanel.prefab` 调整。
- Animator 通过固定层级 `StartPanel_Root` 解析；缺失时只记录 Bear warning，不阻断首页其它功能。
- 监视任务随 StartPanel 销毁令牌停止；不新增 `Update()`、全局事件、运行时实例、配置、存档、资源加载、
  打点、广告、内购或平台分支。

### 验证、风险与回滚

- 静态确认关闭条件覆盖正常 `in -> idle` 路径，以及 `in` 播完但转场条件未满足的兜底路径；过渡期间不会提前关闭。
- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:quiet -m:1 /p:UseSharedCompilation=false /nr:false /clp:ErrorsOnly`：
  通过，`0 error / 1138 warning`；Unity Editor 已热重载 `StartPanel.cs`，日志未发现本次脚本编译错误。
- 目标文件 `git diff --check`：通过，仅有工作区 LF/CRLF 转换提示。
- `uncertain`：仍需 Unity PlayMode 目测 `in` 完整播放、门循环停止、长按 `main/clickArea` 时 Scale 保持 `0.95`，
  以及 Android/iOS 横屏触控表现。
- 若控制器后续重命名 `in` / `idle` 状态，监视不会命中，需要同步更新状态 Hash；缺失状态时不会强制关闭 Animator。
- 回滚时删除 `StartPanel.cs` 的两个状态 Hash、`OnCreate()` 中的监视启动和
  `DisableRootAnimatorAfterEnterAsync()`；无需迁移数据或回退 prefab。

## 2026-09-07 进场期间 ClickBlock 与启动 ChestGift 延迟

### 需求与原行为基线

- StartPanel 首次进场动画完成前不允许点击任何按钮；启动 ChestGift 必须在同一个动画完成边界之后才能弹出。
- 修改前，根 `in` Animator 约 1 秒结束，Play 与 ChoiceLevel 两个按钮各自延迟 1 秒并在 0.4 秒内缩放到最终值；三条动画没有统一完成信号，首页按钮从创建起即可接收输入，Loading 的 ChestGift 步骤也会在前置弹窗结束后立即尝试打开。
- 用户已在 `StartPanel.prefab/StartPanel_Root` 最上层加入全屏 `ClickBlock`，默认隐藏；脚本此前未持有或切换该对象。

### 当前行为与调用流

- `StartPanel` 通过 Inspector 显式绑定 `rootAnimator` 和现有 `ClickBlock`。`OnCreate()` 先启用 ClickBlock，再启动原有两个按钮 Tween，并同时监视根 Animator；不再通过固定层级 `transform.Find("StartPanel_Root")` 解析 Animator。
- 完成边界要求根 Animator 已进入 `idle` 或 `in.normalizedTime >= 1`，并且 Play、ChoiceLevel 两个 Tween 都已结束。三个条件全部满足后，脚本关闭 Animator、解除 ClickBlock，并完成当前实例的 `UniTaskCompletionSource`。
- `LoadManager` 的启动 ChestGift 步骤获取已经由 `ReadyToPlay()` 创建的 StartPanel，等待 `WaitForEnterAnimationAsync()` 后再次确认面板仍处于打开状态，再执行原有 Gift 资格判断和 `OpenStartupOnce()`。公告与旧 Tips 转金币的先后顺序不变。
- StartPanel 入场和按钮动画仍只在实例 `OnCreate()` 播放一次；被其它 UI 遮挡再显示时不会重新锁输入或重播。等待使用各自生命周期取消令牌，不新增常驻 Update、全局事件、存档或配置。

### 验证、剩余风险与回滚

- 静态核对确认 ClickBlock 为 `StartPanel_Root` 最后一个子节点，锚点铺满父节点且 Image 接收射线；Prefab 的 `rootAnimator` 与 `clickBlock` 均为非空序列化引用。
- 运行时程序集构建通过：`0 error / 122 warning`。根 `in.anim` 资源结束时间为 1 秒，按钮 Tween 的总时长为 1.4 秒，因此正常启动时以较晚完成的按钮动画作为解锁边界。
- `uncertain`：尚未在 Unity PlayMode 验证动画期间多点触摸、低帧率、TimeScale 变化、前置公告组合、StartPanel 外部关闭与 ChestGift 实际弹出帧；Android/iOS 横屏触控仍需真机验证。
- 回滚时移除 `rootAnimator/clickBlock` 绑定、统一完成任务和 `LoadManager` 中的等待，恢复原独立 Animator 监视；用户新增 ClickBlock 节点可保留为默认隐藏，不涉及数据迁移。

## 2026-09-09 主页子入口显式绑定与集中刷新

### 原行为与修改后行为

- StartPanel 原先在每次 `OnOpen()` 中分别调用 `GetComponentInChildren(..., true)` 查找 DrawGameEnter、IWannerGameEnter、PostcardEnter 和 DailyChestGiftEnter，再执行各自刷新。入口都已经是 StartPanel.prefab 中的固定嵌套实例，运行时扫描没有必要，也使 ChestGift 启动收回目标依赖一次隐式查找。
- 当前四个入口均改为私有 `[SerializeField]` 字段，并在 StartPanel.prefab 显式绑定对应嵌套组件。`RefreshHomeEntries()` 统一调用原有 `Refresh()` / `RefreshForHome()`，调用时机仍是每次 `OnOpen()`，入口各自的资格、进度、按钮和存档读取逻辑没有调整。
- `ChestGiftReturnTarget` 直接返回已绑定 DailyChestGiftEnter 的 RectTransform，只供 LoadManager 的启动礼包路径读取；普通入口和启动资格边界不变。

### 验证、风险与回滚

- 四组字段、stripped 组件、来源组件和 PrefabInstance 对照均通过，Unity Editor 成功导入 StartPanel.prefab；完整 Assembly-CSharp 源码编译 `0 error / 2014 warning`，定向空白检查通过。
- `uncertain`：新增序列化字段无法证明已经注入当前 PlayMode 中的旧 StartPanel 实例。需退出 PlayMode、等待全量脚本编译，再验证四个入口刷新和启动 ChestGift 收回落点；移动端横屏仍未验证。
- 回滚时删除四个序列化字段和 Prefab 引用，恢复 `OnOpen()` 中原四次 `GetComponentInChildren` 缓存与刷新；其它 StartPanel 动画、ClickBlock、事件和按钮逻辑无需回退。
