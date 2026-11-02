# PlayControllerManager 结构与主线接入说明

## 1. 需求与背景

2026-07-24：为新增画线单关玩法准备独立控制器生命周期层。

目标结构为：

```text
GameManager
  -> PlayControllerManager
      -> PlayControllerPolicyResolver
          -> IPlayControllerPolicy
      -> 当前 IPlayController
          -> PlayCtrl（主线）
          -> DrawPlayCtrl（后续）
```

当前已接入可运行 `DrawPlayCtrl`：StartPanel 的 Draw 入口先切换到 Draw 控制器并打开 `DrawGame_ChoiceLevelPanel`，由玩家选择关卡后加载对应 DLevel；主线 `10999 / Level_999` 选关项保持主线进关，玩家进入 `Level_999` 后再通过关内 `enterDrawGame` 入口直达当前有效排序中最前的已解锁且未通关 Draw 关卡。关卡内打开 Draw HUD、复用设置弹窗暂停，并可返回 StartPanel。基础画线、重开、提示、胜负结算、商店、广告加墨、Draw 选关存档和独立插屏策略已接入。

## 2. 修改前行为基线

- `GameManager.ReadyToPlay()` 直接取得 `PlayCtrl.Instance`，然后调用 `PlayCtrl.Init(GameManager.Bag)`。
- `GameManager.Update()` 每帧直接调用 `PlayCtrl.Update()`。
- `GameManager.OnDestroy()` 直接调用 `PlayCtrl.OnDestroy()`。
- `PlayCtrl.Init(...)` 同时完成主线依赖初始化、FSM 创建、`START` 状态进入、`StartPanel` 创建和全局玩法事件订阅。
- 主线 `GamePlayPanel` 的暂停按钮直接派发 `SwitchGameStateEvent(PAUSE)`，随后创建 `GameSettingPopup`。
- `GameSettingPopup` 恢复和退出操作直接派发主线 `SwitchGameStateEvent`；退出时固定打开 `ChoiceLevelPanel`。

## 3. 当前结构

### 3.1 统一控制器契约

新增 `IPlayController : IPlayPauseHost`：

- `Mode`：控制器负责的玩法类型。
- `Initialize(...)`：注入 `PlayControllerContext`，执行一次性初始化。
- `Activate()` / `Deactivate()`：控制控制器是否接收玩法事件与 Tick。
- `Tick()`：由管理器统一驱动逐帧逻辑。
- `Shutdown()`：统一释放状态机、事件和运行时资源。
- `Pause()` / `Resume()` / `ExitToStartPanel()`：提供给复用暂停弹窗的最小玩法能力。

`PlayControllerContext` 当前只包含由 `GameManager` 持有的共享 `SimpleBag`。主线或画线的专属关卡数据不能加入共享上下文。

### 3.2 Manager 职责

`PlayControllerManager` 是由 `GameManager` 持有的普通 C# 对象，不是新单例。它负责：

- 按 `PlayModeType` 注册 `IPlayController`。
- 把 `PlayEntryRequest` 交给 Policy 解析。
- 首次进入时初始化目标控制器。
- 停用旧控制器、激活新控制器；激活失败时尝试恢复旧控制器。
- 只 Tick 当前控制器。
- 退出应用生命周期时统一 Shutdown 已注册控制器。

`GameManager` 暂时保留 `PlayCtrl` 公开属性，避免现有 49 个左右的主线直接调用点被迫一起迁移；新玩法入口应通过 `ControllerManager`，不应继续在 `GameManager` 添加第二套逐帧分支。

### 3.3 Policy 职责

`PlayControllerPolicyResolver` 按注册顺序选择首个能处理请求的 `IPlayControllerPolicy`。

当前注册 `MainPlayControllerPolicy` 与 `DrawPlayControllerPolicy`：

```text
PlayEntryRequest(Main)
  -> MainPlayControllerPolicy
  -> PlayModeType.Main
  -> 已注册 PlayCtrl

PlayEntryRequest(Draw)
  -> DrawPlayControllerPolicy
  -> PlayModeType.Draw
  -> 已注册 DrawPlayCtrl
```

`DrawPlayCtrl` 激活后先持有 `DrawGame_ChoiceLevelPanel`，选关面板通过 `DrawLevelSort.Path -> DrawLevelData.Id` 解析实际资源，再请求活动中的 Draw 控制器加载对应 DLevel。异步加载结果通过激活版本校验，控制器停用后返回的旧加载结果不会重新创建 Draw 现场。

### 3.4 主线接入

- `PlayCtrl` 实现 `IPlayController`。
- `Init(SimpleBag)` 作为兼容 API 保留；管理器使用 `Initialize(PlayControllerContext)`。
- 主线原有 FSM 创建、`START` 状态、`StartPanel.Create()` 和关卡流程保持原位置与顺序。
- 全局玩法事件订阅从一次性初始化移到 `Activate()`；`Deactivate()` 会释放订阅，并销毁主线关卡、GameplayPanel、引导现场及复位 FSM，避免非当前控制器继续运行或显示。
- `GameManager.Update()` 改为 `ControllerManager.Tick()`。
- `GameManager.OnDestroy()` 改为 `ControllerManager.Shutdown()`。

### 3.5 PausePopup 复用

- 主线暂停按钮从 `ControllerManager.ActivePauseHost` 取得当前玩法能力。
- `GameSettingPopup.Create(...)` 可注入 `IPlayPauseHost`。
- 恢复和返回 StartPanel 调用注入的当前玩法能力，不再由弹窗固定依赖 `PlayCtrl`。
- 若旧入口未注入 `IPlayPauseHost`，弹窗通过 `GameManager.ReturnToStartPanel(...)` 恢复统一入口。
- `PlayCtrl.Pause/Resume` 继续复用既有主线状态事件，因此 `GamePlayPanel.OnGameStateChanged(...)` 的 `isPause` 同步行为保持不变；退出统一交给 `GameManager.ReturnToStartPanel(...)`。

## 4. 数据、资源、事件与平台影响

- 数据：Draw 选关独立读写 `DB.DrawGame.CurrentLevel`、`UnlockLevels` 与 `ChoiceLevelPanelPageIndex`；`SimpleBag` 仍由 `GameManager` 创建并通过上下文注入。
- 配置：本次切换闭环不修改 Luban 表或生成代码。
- 资源：Draw 控制器按 `DrawLevelData.Path` 加载 `Assets/Game/Prefabs/DrawLevel/{Path}.prefab`，并复用 `DrawGame_GamePlayPanel`。当前工作区只有 `DLevel_001.prefab`，其余配置关卡资源仍待制作。
- UI：主线和 Draw 共用 `GameSettingPopup`；MenuBtn 统一返回 StartPanel，不再返回 `ChoiceLevelPanel`。
- 事件：不新增 `PlayEvents`；主线内部继续使用既有 `SwitchGameStateEvent`。
- Analytics、IAP：本阶段无行为改动；Draw 广告锁定沿用现有奖励广告入口，Draw 选关开始写入独立存档。
- 性能：每帧只增加一次普通对象转发 `GameManager.Update -> ControllerManager.Tick -> PlayCtrl.Tick`，无每帧分配、查表或反射。
- Android/iOS：未增加平台分支；返回键处理仍在激活的 `PlayCtrl.Tick()` 中。

## 5. 验证记录

- CodeGraph 核对了 `GameManager.ReadyToPlay/Update/OnDestroy`、`PlayCtrl.Init/AddListener/Update/OnDestroy`、`GamePlayPanel` 暂停入口和 `GameSettingPopup` 恢复/退出调用链。
- `git diff --check` 对本次运行时代码通过，仅报告仓库现有 LF/CRLF 转换提示。
- 原始 `Assembly-CSharp.csproj` 构建被工作区同时包含 `LevelItem` 与 `MainLevel` 的双份关卡脚本阻断，结果为 3396 个重复类型错误；错误属于当前未完成的关卡目录迁移。
- 为隔离验证本次结构，临时在 MSBuild `CoreCompile` 前从编译项移除旧 `Assets/Game/Scripts/Level/LevelItem/**/*.cs`，保留 `MainLevel` 和本次全部代码；构建成功，结果为 `0 error / 103 warning`。
- 临时验证 target 已删除，没有修改、移动或提交关卡目录。

## 6. 风险与未验证项

- Unity PlayMode 尚未验证：首次启动、进入主线、暂停/恢复、退出选关、手机返回键和重复 `ReadyToPlay()`。
- Android/iOS 横屏真机尚未验证暂停弹窗、应用切后台和返回键行为。
- `PlayCtrl.Deactivate()` 当前会销毁主线关卡与 Gameplay UI，不保留主线现场；再次激活主线时处于 `START` 状态。
- Draw Menu 返回目标已确定为 StartPanel；Draw 不复用主线 `SwitchGameStateEvent`，当前只记录本地暂停状态。
- Draw 画线输入、基础墨水、重开、提示、选关与基础选关存档已经实现；具体机关演出、胜负触发、结算 UI、商店、广告加墨、通关解锁推进与 analytics 契约仍为 `uncertain`。
- 现有主线模块仍有直接访问 `PlayCtrl.Instance` 的调用点。本阶段保留兼容入口以降低回归面，不能据此认为跨玩法解耦已经完成。

## 7. 回滚路径

1. 将 `GameManager.ReadyToPlay/Update/OnDestroy` 恢复为直接调用 `PlayCtrl.Init/Update/OnDestroy`。
2. 将 `PlayCtrl` 移除 `IPlayController` 实现，把 `AddListener()` 调用恢复到 `Init(...)` 末尾。
3. 将 `GamePlayPanel` 暂停按钮和 `GameSettingPopup` 恢复为直接派发 `SwitchGameStateEvent`。
4. 删除 `Assets/Game/Scripts/Play/Controller` 目录及其 `.meta`。

本次没有存档、配置、资源或 prefab 迁移，回滚不需要处理玩家数据。

## 8. 2026-07-24 初始化失败闭环

### 问题

原实现会在 `PlayCtrl.Init(...)` 开始时提前设置 `isReady`。若后续关卡数据、广告事件、状态机或 `StartPanel` 初始化抛出异常，管理器会保留一个被误判为已初始化的主线控制器；同时 `GameManager.ReadyToPlay()` 不返回结果，Loading 仍会继续进入 Main 场景。

### 最小修复

- `PlayCtrl` 仅在全部初始化步骤成功后设置 `isReady`；失败时释放本次创建的状态机和广告事件订阅，并清空本次写入的引用。
- `LevelCtrl` 同样延后设置 `isReady`，并对路径 `10999` 的“未完待续”条目增加幂等判断，避免初始化重试时重复插入。
- `GameManager.ReadyToPlay(out error)` 显式返回主线初始化/激活结果，成功后才公开 `GameManager.PlayCtrl`。
- Loading 收到失败后停止后续 PostProcess，并阻止加载 Main 场景；当前异常提示沿用 Loading 已有的红字错误展示方式。

### 验证与未验证项

- CodeGraph 确认 `ReadyToPlay` 当前只有 `LoadManager.OnPostProcess()` 一个调用方，接口调整未扩散到其他模块。
- 已确认失败分支不会执行 `Purchase.SyncRestore()` 及后续 PostProcess，也不会调用 `LoadSceneAsync("Main")`。
- Unity Editor 已检测四个运行时脚本变更并完成热重载，当前 `Editor.log` 未出现编译错误。
- 独立 `dotnet build` 因工程生成项目的关卡目录重复编译问题耗时超限而终止；已停止本次启动的编译子进程并删除临时 target。尚未执行完整 Domain Reload、PlayMode 启动及失败注入验证。

### 回滚

回退本节对应的 `PlayCtrl.Init(...)`、`LevelCtrl.Init()`、`GameManager.ReadyToPlay(...)` 和 `LoadManager` 失败分支即可；不涉及存档结构迁移。

## 9. 2026-07-24 P0-2 实际玩法切换与返回 StartPanel

### 需求与行为基线

- 修改前 `PlayCtrl.Deactivate()` 只停止 Tick 和事件订阅，切换 Controller 后主线关卡与 GameplayPanel 仍可能继续存在。
- 修改前主线 Pause/Setting 的 MenuBtn 进入 `FAILED` 后打开 `ChoiceLevelPanel`。
- 用户确认 Main 与 Draw 的 MenuBtn 均应回到应用首次启动后的 StartPanel 状态；主线 `10999`/TBN 和 StartPanel 均可作为 Draw 入口。

### 当前实现

- `GameManager.TryEnterDraw(...)` 统一发起 Draw Policy 切换；StartPanel 的 `drawPlay_btn` 与可点击状态的 `10999` 关卡项调用该入口。
- `DrawPlayCtrl` 采用最小单关生命周期：异步加载 `DLevel_001`、实例化到 Main 场景 `Scene` 根节点、打开 Draw HUD；停用时销毁关卡和 HUD。
- Draw 异步加载使用激活版本门禁；切换离开后才完成的加载不会实例化旧现场。加载、场景根节点或 HUD 创建失败时回到 StartPanel。
- Main 的 `Deactivate()` 会取消引导、按既有 `FAILED` 状态记录退出、销毁关卡和 GameplayPanel，并将 FSM 复位为 `START`；不会自行打开 StartPanel。
- `GameManager.ReturnToStartPanel(...)` 先切回 Main Controller，再关闭残留 Popup/Normal UI、恢复启动相机并重新打开 StartPanel。
- `IPlayPauseHost` 的退出契约由 `ExitToSelection()` 改为 `ExitToStartPanel()`；主线与 Draw HUD 均向复用的 `GameSettingPopup` 注入当前活动 Controller。

### 验证

- CodeGraph 与静态搜索确认不存在旧 `ExitToSelection` 调用，Main/Draw 两个实现均满足新接口。
- 静态确认 Draw HUD prefab 的 `PauseBtn` 与 `LevelTxt` 已绑定。
- `git diff --check` 对本次目标代码通过。
- Unity Editor 完整脚本编译完成，耗时 16 秒，当前日志未出现 C# 编译错误。

### 剩余风险与回滚

- 尚未执行 PlayMode 点击链验证；需验证 StartPanel→Draw、10999→Draw、Main Pause→StartPanel、Draw Pause→StartPanel、Draw 加载失败回退。
- Draw 当前已有基础画线输入和局部结果接口，`Pause()` 会同步门禁输入；`DLevel_001` 尚未绑定具体机关演出、胜负触发和结算 UI。
- 本节记录时 Draw 进入期间尚未显示专用 Loading UI；该状态已由下方“Draw 选关入口与主线同款进关过场”记录更新。
- 回滚时删除 `DrawPlayCtrl` 及注册，恢复 Pause Host 旧退出接口，并还原 `PlayCtrl.Deactivate()` 与 MenuBtn 行为；不需要迁移玩家存档。

## 10. 2026-07-27 Draw 画线运行时最小闭环

### 需求与原行为

- 需求：先实现 Draw 玩法的基础画线能力，并为关卡演出、胜负、广告加墨和铅笔皮肤预留必要接口。
- 原行为：`DrawPlayCtrl` 只负责加载 `DLevel_001`、创建 HUD、暂停门禁与销毁资源；HUD 的重开、提示和墨水条没有运行时绑定。

### 当前行为

- `DrawLevelCtrl` 在关卡本地坐标系中采样输入，权威线长采用 DrawSpace 本地世界单位。
- 默认按固定间距采样，避免屏幕分辨率、帧率和输入事件密度直接改变墨水消耗。
- 每帧只更新 `LineRenderer`；一笔结束时才生成一次 `EdgeCollider2D`，避免拖动期间反复重建物理形状。
- 支持单笔/多笔门禁、墨水不足截断、暂停中断退款、重开、提示显示和剩余墨水星级计算。
- `DrawPlayCtrl` 负责 Intro、Drawing、Resolved 三个局部阶段，并将暂停、重开和销毁同步给关卡运行时；关卡专属成功演出由关卡本地状态承担，不占用共享阶段枚举。
- `DrawLevelCtrl` 在 DLevel 内部通过 `IDrawStrokeCompletedHandler` 通知关卡机关接收落笔完成与重开，不经过 `DrawPlayCtrl`，也不新增全局 `PlayEvents`。
- `IDrawLevelRuntime` 预留广告加墨、画笔样式、关卡结果和自定义 Intro 接口；本阶段不实现商业规则或商店 UI。

### 数据与资源边界

- 当前不读取 `DB.DrawGame`，不写存档，不发送 analytics。
- `DLevel_001.prefab` 根节点绑定 `DrawLevelCtrl`，现有 Tips 线仅作为模板与提示资源使用。
- `DrawGame_GamePlayPanel` 仅通过控制器传入的局部回调执行重开、提示和墨水显示。

### 验证与未验证项

- 已完成目标脚本的隔离 C# 编译：0 error。
- 已完成 prefab GUID、组件引用、HUD 墨水条引用及 `git diff --check` 静态验证。
- 完整 Unity 编译被工作区中并行生成的 `DrawGame_ChoiceLevelPanel.Generated.cs` 语法错误阻断，该文件不属于本次修改。
- 尚未完成 Unity PlayMode、真机触控、横屏分辨率、物理机关和 shader stripping 验证。

### 回滚

1. 从 `DLevel_001.prefab` 移除 `DrawLevelCtrl` 组件。
2. 回退 `DrawPlayCtrl` 与 `DrawGame_GamePlayPanel` 的运行时绑定。
3. 删除 `DrawLevelRuntimeTypes.cs`、`DrawLevelCtrl.cs` 及对应 meta，即恢复为仅加载关卡与 HUD 的阶段。

## 11. 2026-07-27 Draw 控制器内选关生命周期

### 需求与修正前行为

- 用户确认 Draw 与主线的入口阶段一致：从 StartPanel 点击 Draw 入口后，应先进入 Draw 控制器，再打开 `DrawGame_ChoiceLevelPanel`，玩家从该面板选择要进入的 Draw 关卡。
- 修正前的临时实现由 StartPanel 或主线 `10999` 直接创建 Draw 选关面板，此时活动控制器仍是主线；选中关卡后才配置并切换到 `DrawPlayCtrl`。
- 该顺序使 Draw 选关 UI 不属于 Draw 控制器生命周期，主线仍在接收 Tick/事件，且控制器激活默认直接加载 `DLevel_001`，无法表达“先选关、后进关”。

### 当前职责与流程

```text
StartPanel.drawPlay_btn / 主线 LevelSort.Path=10999
    -> GameManager.TryEnterDraw()
    -> PlayControllerManager.TryEnter(Draw)
        -> PlayCtrl.Deactivate()
        -> DrawPlayCtrl.Activate()
            -> CloseAllUI(Normal)
            -> DrawGame_ChoiceLevelPanel.Create()
    -> 玩家选择 DrawLevelSort
        -> DrawLevelSort.Path 查 DrawLevelData
        -> 保存 DB.DrawGame 当前关卡与页码
        -> GameManager.TryEnterDrawLevel(sortData, levelData)
        -> 活动 DrawPlayCtrl.TryEnterLevel(...)
            -> 关闭 Draw 选关
            -> 异步加载对应 DLevel
            -> 创建 DrawGame_GamePlayPanel
```

- `PlayEntryRequest` 继续只承载玩法类型，不加入关卡 ID；玩法切换与玩法内部选关是两层职责。
- `PlayControllerManager` 只处理 Controller 的初始化、停用、激活与 Tick，不感知 Draw 选关数据。
- `DrawPlayCtrl` 持有并清理 Draw 选关面板、关卡实例和 HUD。停用时三者全部清理，避免切回 Main 后残留 Draw UI 或输入。
- Draw 选关的 `close_btn` 不单独弹出 UI 栈；它调用 `GameManager.ReturnToStartPanel(...)`，同步停用 Draw 控制器并恢复 StartPanel。
- `GameManager.TryEnterDrawLevel(sortData, levelData)` 只允许活动控制器确实为当前 `_drawPlayCtrl` 时转发关卡请求，防止在主线状态下绕过 Draw 生命周期直接加载 DLevel。
- 关卡加载失败时保持 Draw 控制器活动并重新打开 Draw 选关；只有选关面板恢复也失败时才回到 StartPanel。
- Pause/Setting 的 MenuBtn 行为不变：从 Draw 关卡退出仍统一回到 StartPanel，不回 Draw 选关。

### 数据、资源与风险

- `DrawGame_ChoiceLevelPanel` 使用 `DrawLevelSort.Id` 作为玩家可见顺序与独立进度 ID，使用 `DrawLevelSort.Path` 索引 `DrawLevelData.Id`，最后使用 `DrawLevelData.Path` 组装 prefab 地址。
- 面板首次打开会修复空集合、保证第一条排序关卡解锁，并保存当前关卡和分页索引；不写入主线 `DB.GameData`。
- 当前配置列出 `DLevel_001` 至 `DLevel_005`，但工作区只存在 `DLevel_001.prefab`。默认仅补解锁第一关；若测试存档手动解锁了缺失资源的关卡，加载会失败并回到 Draw 选关。
- 现有 Draw 通关结果尚未写入 `PassedLevels`、推进 `MaxLevel` 或解锁下一关，因此完整选关进度闭环仍未完成。
- 未新增全局 `PlayEvents`，选关到关卡的请求保持在面板、GameManager 与当前 Draw 控制器的局部调用链中。

### 验证与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` 通过：0 error / 102 个项目既有 warning。
- 静态确认 Draw 选关 prefab 的 `content`、`choiceLevelItem`、`gridLayout`、`lastBtn`、`nextBtn`、`closeBtn` 已序列化绑定；`choiceLevelItem` 可在 Inspector 替换样式，但需保留当前选关项节点契约；当前资源目录只有 `DLevel_001.prefab`。
- 尚未执行 Unity PlayMode 点击链、异步资源失败注入、重复快速点击、Android/iOS 横屏与应用切后台验证。
- 回滚本节时，可把 StartPanel/10999 恢复为直接创建 Draw 选关、把选关后流程恢复为先配置再切控制器，并让 `DrawPlayCtrl.Activate()` 直接加载默认关卡；不涉及存档 schema 迁移。

## 12. 2026-07-27 Draw 选关入口与主线同款进关过场

### 需求、基线与当前行为

- 需求：StartPanel 的 `drawLevelBtn` 先打开 `DrawGame_ChoiceLevelPanel` 选关，选定关卡后使用与主线一致的进关过场。
- 修改前 StartPanel 在切换到 Draw 控制器前先创建 `ClickTransformPanel`；Draw 选关确认后则直接关闭面板并异步加载 DLevel，没有复用主线 `EnterLevelLoading`。
- 修改后 StartPanel 直接调用 `GameManager.TryEnterDraw(...)`，由 `DrawPlayCtrl.Activate()` 打开 Draw 选关；用户选定关卡后，`DrawPlayCtrl.TryEnterLevel(...)` 先关闭选关 Popup，等待一帧后创建 `EnterLevelLoading`。
- `EnterLevelLoading` 翻页完成回调等待关卡、HUD 与 Draw Intro 初始化；擦除完成回调确认激活版本及 `IDrawLevelRuntime.IsRuntimeReady` 后再调用 `BeginDrawing()`，避免过场未结束就开放输入。
- 加载失败仍由 `HandleOpenFailure(...)` 清理现场并恢复 Draw 选关；擦除完成回调发现运行时不存在时不会推进 Drawing 阶段。

### 数据、资源、事件与平台边界

- 不修改 `DB.DrawGame` schema、Luban 配置、Draw 资源地址、主线存档、analytics、广告或 IAP。
- 复用现有 `EnterLevelLoading` prefab 与 Bear UI 打开流程，不新增资源和全局事件。
- `EnterLevelLoading` 自身仍会派发既有主线暂停事件；活动主线控制器已在进入 Draw 时停用，Draw 的输入门禁由本地阶段和完成回调控制。
- Android/iOS 没有新增平台分支；横屏触控下的过场遮挡、重复点击与切后台恢复仍需真机补验。

### 影响文件

- `Assets/Game/UI/StartPanel/Scripts/StartPanel.cs`
- `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
- `Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_ChoiceLevelPanel功能与风险评估.md`
- `DevelopMD/PlayControllerManager_Design.md`
- `DevelopMD/已有功能设计与使用记录.md`

### 验证、风险与回滚

- 静态确认入口顺序为 `StartPanel -> DrawPlayCtrl.Activate -> DrawGame_ChoiceLevelPanel`，选关后才创建 `EnterLevelLoading`。
- 静态确认重复点击仍由 `_isOpeningLevel` 拒绝，控制器切换后的旧异步结果仍由 `_activationVersion` 拒绝。
- 首次命令行构建在 124 秒工具时限内未返回；终止对应构建进程后，以 300 秒窗口重新执行同一构建命令并通过，结果为 0 error / 258 个项目既有 warning。
- Unity Editor 运行期间的 `Editor.log` 已更新到 2026-07-27 14:26:22，末尾 800 行未发现 C# 编译失败标记。
- `uncertain`：`PlayIntroAsync()` 在翻页完成回调内执行，实际可见时长和过场节奏尚未在 PlayMode 目视确认。
- 回滚时恢复 StartPanel 的 `ClickTransformPanel` 包装，并让 `DrawPlayCtrl.TryEnterLevel(...)` 直接关闭选关、启动原异步加载，同时把 `BeginDrawing()` 恢复到加载方法末尾；不涉及数据迁移。

## 13. 2026-07-27 DrawLevel 接入 YooAsset AB 收集

### 需求与原行为

- DrawPlayCtrl 通过 Bear `ResourceManager.LoadAsync<GameObject>()` 使用完整路径加载 `Assets/Game/Prefabs/DrawLevel/{DrawLevelData.Path}.prefab`。
- 主线 `Assets/Game/Prefabs/Level` 已配置在 YooAsset `GamePackage / Level` 组中，使用目录 Collector、`PackSeparately` 和 `CollectPrefab`。
- 修改前 `Assets/Game/Prefabs/DrawLevel` 不在 `AssetBundleCollectorSetting` 中。Editor 资源模式不代表正式 AB 构建一定包含该目录，因此正式包或本地 AB 模式存在找不到 Draw 关卡资源的风险。

### 当前行为

- 在 `Assets/BPGame/AssetBundleCollectorSetting.asset` 的 `GamePackage / Level` 组增加 `Assets/Game/Prefabs/DrawLevel` Collector。
- Draw Collector 与主线 Level 使用相同规则：
  - `CollectorType: MainAssetCollector`
  - `AddressRuleName: AddressByFileName`
  - `PackRuleName: PackSeparately`
  - `FilterRuleName: CollectPrefab`
- `GamePackage.EnableAddressable` 保持关闭，DrawPlayCtrl 继续使用完整 AssetPath 加载，不修改运行时代码或配置表路径。
- CollectorGUID 使用 `Assets/Game/Prefabs/DrawLevel.meta` 的真实目录 GUID `a04bf0166b76d9e49991fe8a3f1227c9`，不是人工生成的业务 ID。

### 影响、验证与风险

- 当前目录包含 `DLevel_001.prefab`，会作为主资源参与 GamePackage 收集；未来加入同目录的 `DLevel_*.prefab` 会自动沿用独立打包规则。
- 本次不修改 Draw prefab、主线 Level Collector、脚本、存档、配置、UI、事件、analytics 或平台分支。
- 静态验证确认 Draw Collector 规则与主线 Level 一致、CollectPath 存在、CollectorGUID 与目录 meta 一致，且全部 CollectorGUID 保持唯一。
- 尚未执行一次完整 YooAsset GamePackage 构建、Manifest 内容检查和 Android/iOS 真机 AB 加载；这些仍是发包前验证项。
- 回滚时只需从 `AssetBundleCollectorSetting.asset` 删除 DrawLevel Collector；不会影响玩家存档，但正式 AB 将再次不包含 DrawLevel 主资源。

## 14. 2026-07-27 Draw 失败与 Reset 对齐主线

### 需求与行为基线

- Draw 失败判断以主线 `BaseLevelCtrl.CheckFinished()` 为依据：失败区域成立或当前 Actor 已死亡。
- 修改前 `DrawLevelCtrl` 不监听失败事件，`DrawGame_GamePlayPanel.ResetBtn` 只清理画线运行时，不能恢复死亡 Actor 和关卡机关。

### 当前流程

```text
OnTriggerTrapEvent
    -> ActorCtrl.Die()
    -> 等待 1.5 秒
OnTriggerFailAreaEvent
    -> failed 音效
    -> 等待 0.2 秒
ActorCtrl.IsDied / 失败区域标记
    -> IDrawLevelRuntime.TryResolve(Failed)
    -> DrawLevelCtrl 等待 1 秒
    -> DrawGameResetEvent(Failed)
    -> DrawPlayCtrl.OnGameResetEvent()
    -> 黑幕 0.4 秒渐显并等待 0.5 秒
    -> 保留同一个 HUD 与 BlackMask，销毁并重新加载当前 DLevel
    -> 新 DLevel 与 Intro 准备完成后在全黑状态重置 HUD
    -> GamePlayPanelFadeInEvent
    -> 跳过仅进关 delay，等待 Reset 必需 Intro 与 0.5 秒黑幕门禁后开放输入

DrawGame_GamePlayPanel.ResetBtn
    -> DrawGameResetEvent(Manually)
    -> DrawPlayCtrl.OnGameResetEvent()
    -> 黑幕 0.4 秒渐显并等待 0.5 秒
    -> 保留同一个 HUD 与 BlackMask，销毁并重新加载当前 DLevel
    -> 新 DLevel 与 Intro 准备完成后在全黑状态重置 HUD
    -> GamePlayPanelFadeInEvent
    -> 黑幕 0.4 秒渐隐，跳过仅进关 delay，等待 0.5 秒黑幕门禁后开放输入
```

- Draw 复用主线已有陷阱和失败区域事件类型，并新增结构与主线 Reset 一致的 `DrawGameResetEvent`。`DrawPlayCtrl` 只在激活期间订阅，`DrawLevelCtrl` 与 Draw HUD 分别派发失败和手动类型。
- Draw 不直接派发 `GameResetEvent`：常驻的 `InterstitialAdPolicy` 会消费主线 Reset 并改变失败广告冷却与失败计数。专用事件避免 Draw Reset 意外进入主线广告策略。
- 没有修改共享 `PlayCtrl`、`BaseLevelCtrl` 或 `GamePlayPanel`；Draw HUD 仍使用其专用脚本。
- 自动失败重试与手动 Reset 都使用当前 `_drawLevelPath`；`_activationVersion` 负责淘汰旧失败延迟和过期加载结果。
- Draw HUD 复用主线 `GamePlayPanel` 的黑幕时序和既有 `GamePlayPanelFadeInEvent`；
  Reset 期间不调用 `CloseAllUI(UILayer.Normal)`、不重建 HUD，由同一块 BlackMask 连续遮挡
  加载与 Intro。全黑状态通过 `ResetForNewAttempt(...)` 恢复 HUD 后再渐显。
- Reset 开始时按主线关闭 Popup；保留 HUD 后以请求版本门禁淘汰 Reset 前未返回的
  RewardAD 回调，避免跨尝试发奖。
- Draw GameplayPanel 同步订阅 `GameReplayEvent`；胜利重玩先渐黑并走
  `ResetGameAsync("VictoryReplay", ...)` 的 0.5 秒等待，再复用相同重载与渐显链。
- Draw HUD 的 `ClickBlock` 复用主线 `GamePlayPanelSwitchBlockEvent` 开关，面板打开时默认关闭，供关卡流程按事件参数控制点击拦截。
- 2026-07-30 起，Reset/Replay 不再重放 `DelayAutoBeginDrawHandle` 等仅进关门禁；真实入场和
  状态恢复 Intro 仍保留，完整契约见第 17 节。
- 本次不接入失败广告、失败/重试 analytics、Draw 存档计数或结算 UI，这些仍需独立产品契约。

### 验证、风险与回滚

- `Assembly-CSharp.csproj` 最终构建通过：`0 error / 1120` 个项目既有 warning；目标 `git diff --check` 通过。
- 静态确认 `DLevel_001` 有 `ActorCtrl`，Draw HUD 的 `ResetBtn` 与用户新增 `BlackMask` 已绑定；未修改配置。
- `uncertain`：尚未执行 Unity PlayMode 的陷阱、失败区域、Actor 外部死亡、Reset 连点、黑幕全屏覆盖、ClickBlock 事件开关和暂停/切后台回归；Android/iOS 真机重载峰值也未验证。
- 回滚时删除 DrawLevelCtrl 的失败监听与 `DrawGameResetEvent`，把 Draw HUD 恢复为 Action 绑定、把 Reset 恢复为局部 `ResetAttempt()`；不涉及存档或资源迁移。

## 15. 2026-07-27 Draw 选关阶段保留 StartPanel

### 需求、基线与当前流程

- 用户确认进入 `DrawGame_ChoiceLevelPanel` 时不能销毁或关闭 StartPanel。
- 修改前 `DrawPlayCtrl.Activate()` 会先执行 `CloseAllUI(UILayer.Normal)`，再创建 Popup 层的 Draw 选关面板。
- 当前从打开且可见的 StartPanel 激活 Draw 控制器时保留 Normal 层，并直接打开 Popup 层 Draw 选关；主线 `10999` 等其它入口仍关闭原 Normal 界面。只有玩家确认具体关卡、`EnterLevelLoading` 翻页完成并进入 `OpenDrawLevelAsync(...)` 后，才关闭保留的 Normal 层。
- StartPanel 不会残留到 DLevel 与 Draw HUD 阶段；选关关闭仍走 `GameManager.ReturnToStartPanel(...)`，不改变 Controller 生命周期或返回目标。

### 影响、验证与风险

- 运行时代码只调整 `Assets/Game/Scripts/Play/DrawPlayCtrl.cs` 中 Normal 层关闭时机，不修改 StartPanel、Draw 选关 prefab、存档、配置、资源、事件、analytics 或平台分支。
- 静态确认 StartPanel 保留范围只覆盖“StartPanel 当前打开且可见”的 Draw 选关阶段，关卡加载前仍会关闭 Normal 层；其它入口保持原关闭行为。
- `Assembly-CSharp.csproj` 构建通过：0 error / 102 个项目既有 warning；目标差异格式检查通过。
- `uncertain`：Popup 遮罩透明度、StartPanel 背景可见性、触控拦截与关闭选关后的动画恢复尚需 Unity PlayMode 目视验证。
- Android/iOS 横屏和返回键尚未验证；回滚时把 Normal 层关闭调用移回 `DrawPlayCtrl.Activate()` 即可。

## 16. 2026-07-27 主线与 Draw 插屏策略完全隔离

### 需求与职责

- 用户确认主线与 Draw 的插屏 CD、激励视频后 CD、失败计数和胜利间隔计数必须完全分离。
- `IInterstitialAdPolicyHost` 是玩法控制器可选的广告能力；`PlayControllerManager`
  仍只负责活动控制器生命周期，不保存或判断广告业务数据。
- `PlayCtrl` 提供主线 `InterstitialAdPolicy` 和主线当前关卡；
  `DrawPlayCtrl` 提供 `DrawInterstitialAdPolicy` 和 `DrawLevelSort.Id`。
- `InterstitialAdHelper` 与 `RewardAdHelper` 从 `ActiveController` 捕获请求发起时的
  策略；SDK 回调不会因为期间切换控制器而写入另一玩法。

### 配置与存档边界

- 主线继续使用 `ad_*` RemoteConfig 和 `DB.GameData` 原四项插屏字段。
- Draw 使用 `drawGame_*` RemoteConfig，缺失时回退到 GlobalConst。
- Draw 在 `DB.DrawGame` 新增独立的上次插屏时间、上次激励时间、失败计数和胜利计数，
  只通过 `DB.SaveDrawGame()` 保存。
- Draw Reset 继续只派发 `DrawGameResetEvent`，但当前 `DrawPlayCtrl` 会在本地调用
  Draw Policy 增加失败计数并请求失败插屏；不会派发主线 `GameResetEvent`。
- Draw 胜利在创建胜利面板前增加 Draw 胜利计数并请求胜利插屏。

### 验证与风险

- `Assembly-CSharp.csproj` 构建通过：`0 error / 102 warning`。
- 目标差异格式检查通过；静态确认主线原配置、原存档、Postcard、Rating 和
  DailyChallenge 拦截仍保留。
- `uncertain`：Unity PlayMode、旧 Draw 存档样本、真实广告、模式切换、Android/iOS
  横屏与切后台尚未验证。
- Draw 每日免广告弹窗本次只补 RemoteConfig getter，尚未接入独立展示次数和 UI 触发。
- 完整功能、数据流、验证缺口和回滚见
  `DevelopMD/InterstitialAdPolicy_DrawGame_Isolation_Record.md`。

## 17. 2026-07-30 Draw Reset 不重放仅进关延迟

### 需求与原行为

- 用户反馈 Draw Reset 后仍有约 1～2 秒不可点击。
- 原 Reset 重载在派发 `GamePlayPanelFadeInEvent` 后再次等待完整关卡 Intro；
  `DelayAutoBeginDrawHandle` 因实现可见 Intro 接口而重复执行 prefab 配置的进关延迟，
  并与黑幕 0.5 秒输入门禁取较长值。

### 当前流程与职责

- `IDrawLevelEnterOnlyIntroHandler` 标识不承担状态恢复的首次进关 Handle；
  `DelayAutoBeginDrawHandle` 使用该标记。
- 正常进关继续执行完整 `PlayIntroAsync()`；Reset/Replay 使用
  `PlayResetIntroAsync()`，只跳过仅进关 Handle。
- DLevel_002/010 等真实入场控制器不实现仅进关标记，Reset 时仍重放入场和物体状态
  恢复；既有黑幕渐显、资源重载、HUD 重置、0.4 秒淡出和 0.5 秒输入门禁不变。
- 本次不修改 prefab、DB、存档、配置、资源地址、事件类型、analytics、广告或平台分支。

### 验证、风险与回滚

- C# 全量单进程构建通过：`0 error / 101 warning`；静态确认当前只有
  `DrawLevelCtrl` 实现 `IDrawLevelRuntime`。
- Unity PlayMode 的 DLevel_001、DLevel_003～009 Reset 输入时序，以及 DLevel_002/010
  Reset 入场恢复仍待验证；Android/iOS 横屏触控仍待验证。
- 若未来错误标记承担状态恢复的 Intro，Reset 会跳过必要流程；仅进关标记只能用于无状态
  等待或装饰性演出。
- 回滚时删除 Reset 专用 Intro 契约并恢复 `ReloadCurrentLevelAsync()` 调用完整
  `PlayIntroAsync()`；无需迁移数据或资源。

## 18. 2026-08-04 DrawGameLevelCtrl 一次性关卡序列

### 需求与职责调整

- Draw 最终关卡顺序不再由每次创建的 `DrawGame_ChoiceLevelPanel` 构建。
- `GameManager` 持有 `DrawGameLevelCtrl` 普通对象；`LoadManager` 在 RemoteConfig 与 Luban 表完成后调用一次幂等 `Init()`。
- 控制器负责本地排序、可选远端排序、无效远端 path 过滤、`20999` 追加和 DrawLevelData 查询；选关面板只消费只读结果并管理 UI/存档。

### 生命周期与失败边界

- 正常启动和“ConfigManager 已初始化”的分支都会尝试初始化 Draw 控制器。
- 初始化成功后重复调用不重建序列；面板销毁、重开或玩法切换不会重新解析 RemoteConfig。
- Draw 初始化失败不阻断主线，Loading 记录错误，Draw 面板检测 `IsReady` 后拒绝使用半初始化数据。
- 详细影响、验证缺口与回滚见 `Assets/Game/Scripts/Level/DrawLevel/DrawGameLevelCtrl功能与风险评估.md`。

## 19. 2026-08-04 Draw 选关阶段统一保留 Normal 层

### 需求与原行为基线

- 需求：进入 DrawGame 选关时不关闭现有 Normal 层，只有玩家确认进入具体 Draw 关卡后才关闭。
- 修改前 `DrawPlayCtrl.Activate()` 仅在检测到已打开且可见的 `StartPanel` 时保留 Normal 层；
  其它入口会在创建 `DrawGame_ChoiceLevelPanel` 前调用 `CloseAllUI(UILayer.Normal)`。

### 修改后流程与边界

```text
GameManager.TryEnterDraw
  -> DrawPlayCtrl.Activate
  -> 保留现有 Normal 层
  -> DrawGame_ChoiceLevelPanel.Create(Popup)

玩家确认具体 Draw 关卡
  -> DrawPlayCtrl.OpenDrawLevelAsync
  -> CloseAllUI(Normal)
  -> 关闭选关并加载 Draw HUD / DLevel
```

- `DrawPlayCtrl.Activate()` 不再根据 `StartPanel.IsOpen/IsVisible` 关闭 Normal 层；
  `OpenDrawLevelAsync()` 的原关闭点保持不变。
- 只调整 Bear UI Normal/Popup 层的关闭时机；不修改 UIManager、Prefab、资源、存档、配置、
  全局事件、广告、analytics 或平台分支。

### 验证、风险与回滚

- 定向 `git diff --check` 通过，仅有工作区行尾转换提示。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:quiet -m:1
  /p:UseSharedCompilation=false /nr:false /clp:ErrorsOnly` 通过：`0 error / 0 warning`。
- `uncertain`：尚未在 Unity PlayMode 验证 StartPanel、主线关内入口及其它 Normal UI 作为背景时的
  可见性、触控拦截、关闭选关和确认进关流程；Android/iOS 横屏尚未验证。
- 回滚时在 `DrawPlayCtrl.Activate()` 恢复原 `StartPanel` 状态判断与
  `CloseAllUI(UILayer.Normal)`；不涉及数据或资源迁移。

## 20. 2026-08-04 主线 Level999 恢复先进入关卡

### 需求、基线与修复

- 本次确认的有效产品流程为：主线选关点击 `Level999` 先加载 `Level_999`，玩家再点击关内
  `enterDrawGame` 进入当前可玩的最大 Draw 关卡。
- 修复前 `LevelChoiceItem.EnterLevel(...)` 对 `LevelSort.Path == "10999"` 直接调用
  `GameManager.TryEnterDraw(...)`，因此主线 `Level_999` 被绕过并直接显示 Draw 选关。
- 当前删除该特判，主线 `10999` 恢复走 `ChoiceLevelPanel` 既有进关链；关内
  `Level999Ctrl.EnterDrawGameAtHighestPlayableLevel()` 和 prefab UnityEvent 绑定保持不变。
- 本节取代第 9、11、15 节中“主线 `10999/TBN` 选关项可直接作为 Draw 入口”的历史规则；
  StartPanel 仍是 Draw 选关入口，Draw 关内退出恢复选关的流程也不变。

### 影响、验证、风险与回滚

- 运行时代码只修改 `LevelChoiceItem.cs` 的点击路由，并纠正 `GameManager.TryEnterDraw(...)` 的过期注释；
  不修改 prefab、Luban 配置、主线或 Draw 存档、资源、事件、analytics、广告及平台分支。
- 静态扫描确认主线选关脚本不再引用 `TryEnterDraw(...)`，关内 `Level_999.prefab` 绑定仍指向
  `EnterDrawGameAtHighestPlayableLevel()`；目标 `git diff --check` 通过。
- `Assembly-CSharp.csproj` 构建通过：`0 warning / 0 error`。
- `uncertain`：Unity PlayMode 完整点击链与 Android/iOS 横屏触控尚未验证。
- 回滚时恢复 `LevelChoiceItem` 的 `10999` 特判及其 Logger/`IDebuger` 依赖即可；不涉及数据迁移。

## 21. 2026-08-25 主线 Level999 改为最小已解锁未通关 Draw 目标

### 需求与现行规则

- 主线 `Level_999` 关内入口不再从 `DrawGameLevelCtrl.LevelSorts` 末尾反向选择最大已解锁关，
  改为从最终有效排序起点向后选择第一个同时存在于 `UnlockLevels`、不存在于
  `PassedLevels` 且能解析 `DrawLevelData` 的关卡。
- 若没有有效的已解锁未通关候选，先回退到排序最前的有效已解锁关供重玩；若存档没有可用的
  `UnlockLevels`，则继续回退到排序中第一条能解析配置的关卡，兼容清档和旧档。
- `EnterDrawGameAtHighestPlayableLevel()` 方法名为兼容 `Level_999.prefab` 现有 UnityEvent 绑定保留；
  内部权威解析器已更名并执行新规则。

### 边界、验证与回滚

- 不修改 `Level_999.prefab`、Draw 排序配置、主线/Draw 存档结构、控制器切换、Loading、资源路径、广告或平台分支。
- 跨玩法切换仍先结束主线现场，再由 Draw 建立新关 `level_start`；`enter_tbc_portal(type=0)` 仍只在
  Draw 进关请求被接受后上报，与 `DevelopMD/关卡打点生命周期约定.md` 无冲突。
- 静态验证覆盖：排序正向扫描、已通关跳过、缺失 `DrawLevelData` 跳过、全通关重玩回退、
  无解锁数据首关回退、一次性门禁与成功后 TBC 打点保持；Prefab 仍绑定兼容入口方法。
- 定向 `git diff --check` 通过；NuGet restore 后执行 `dotnet build Assembly-CSharp.csproj --no-restore`
  成功，结果为 `0 error / 1124 warning`，warning 未在本次逐项归因。
- `uncertain`：尚未完成 Unity PlayMode 实际存档组合、跨玩法 Loading、Android/iOS 横屏入口验证。
- 回滚时把目标解析器恢复为从 `LevelSorts` 末尾反向查找最大已解锁有效关，保留其它转场和打点代码即可。
