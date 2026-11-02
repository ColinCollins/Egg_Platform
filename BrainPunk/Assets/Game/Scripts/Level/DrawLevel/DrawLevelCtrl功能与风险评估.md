# DrawLevelCtrl 功能与风险评估

## 1. 需求与背景

需求依据为 `outputs/draw-feature-requirements-20260724/画线功能需求与实现思考留存.xlsx`。Draw 单关生命周期与主线关卡接近，需要先完成可复用的基础画线能力，并为后续机关演出、胜负条件、广告加墨、铅笔皮肤和自定义开场保留明确接口。当前实现优先使用关卡局部通信；Reset 流程使用 Draw 专用事件隔离主线副作用，不读取或写入尚未确认契约的 Draw 存档。

## 2. 原行为基线

- `DrawPlayCtrl` 能加载 `DLevel_001`、创建 Draw HUD、响应暂停和清理资源。
- `DLevel_001` 内已有提示线表现，但当玩家线 `lineTemplate` 未绑定时，旧初始化会直接判定运行时不可用。
- HUD 有 Reset、Tips、Pause 按钮与墨水显示节点，但 Reset、Tips 和墨水显示未绑定运行时逻辑。

## 3. 受影响文件与系统

- `Assets/Game/Scripts/Level/DrawLevel/DrawLevelRuntimeTypes.cs`
- `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl.cs`
- `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
- `Assets/Game/Prefabs/DrawLevel/DLevel_001.prefab`
- `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_GamePlayPanel.cs`
- `Assets/Game/UI/DrawGame/GamePlayPanel/Resources/DrawGame_GamePlayPanel.prefab`

涉及系统：玩法控制器生命周期、关卡局部输入、LineRenderer、EdgeCollider2D、Draw HUD。当前不涉及 DB、存档、analytics 或资源热更新契约；失败监听复用主线已有 `OnTriggerTrapEvent` / `OnTriggerFailAreaEvent`，失败与手动 Reset 统一派发 Draw 专用 `DrawGameResetEvent`。

## 4. 当前行为

### 4.1 输入与线长

- 输入通过关卡相机投射到 DrawSpace 平面，再转换为 DrawSpace 本地坐标。
- 权威线长为本地折线长度，默认固定采样间距为 `0.05`，最大墨水长度为 `10`。
- 屏幕像素、帧率和原始触摸事件数量不直接作为墨水数值，降低设备差异。
- 开始落笔时检查 UI 命中；若未配置 `drawArea`，暂时以相机视口作为可画范围。
- 配置 `drawArea` 时，其 GameObject 必须位于 `Draw` Layer；Layer 不符时运行时初始化失败并保持输入关闭。

### 4.2 线条与碰撞

- 有 `DrawLineRender` 模板时每笔复制模板；未绑定模板时创建最小运行时 `LineRenderer`，不再阻断关卡初始化。
- 拖动期间只更新 `LineRenderer`。
- 一笔完成时才创建并写入一次 `EdgeCollider2D`；取消中的线不会保留碰撞体。
- 暂停、失焦或关闭输入会取消未完成的一笔，并返还该笔已经消耗的墨水。

### 4.3 生命周期与 UI

- `DrawPlayCtrl` 管理 `Intro`、`Drawing`、`Resolved` 三个阶段；关卡专属演出由关卡本地状态承担。
- HUD Reset 按主线重置语义销毁并重新创建当前 DLevel 与 Draw HUD；新 HUD 通过首次打开
  初始化和当前运行时墨水快照恢复关卡号、进度、星级节点、奖励入口与交互状态。
- HUD Tips 显示关卡提示对象。
- Pause 继续复用现有暂停宿主和设置弹窗；暂停状态会同步关闭关卡输入。
- 剩余墨水通过局部回调更新 HUD，并按配置阈值计算三星状态。

## 5. 预留接口

- `IDrawLevelRuntime.PlayIntroAsync()`：关卡专用开场。
- `IDrawStrokeCompletedHandler`：关卡机关接收一笔完成和重开通知。
- `IDrawLevelRuntime.TryResolve()`：胜负来源向控制器提交幂等结果。
- `IDrawLevelRuntime.AddInk()`：广告等外部系统增加墨水。
- `IDrawLevelRuntime.SetDrawingStyle()`：铅笔皮肤更新颜色、宽度和材质。
- `allowMultipleStrokes`：单笔与多笔规则的关卡配置入口。

这些接口只建立边界，不包含广告次数、商品解锁、存档字段、结算奖励或 analytics 规则。

## 6. 性能评估

- 固定距离采样限制点数增长；默认参数下满墨水约产生 200 个线段采样间隔。
- 没有在拖动的每帧重建 `EdgeCollider2D`，碰撞点数组只在落笔结束时分配一次。
- 输入循环复用点列表，常规拖动路径没有主动创建临时 GameObject 或协程。
- 每一笔仍会创建一个运行时线条对象；多笔模式的最大笔数与总点数需要由关卡配置和真机性能验证约束。

## 7. 风险项

| 等级 | 风险 | 当前处理 |
|---|---|---|
| P1 | 新增 DLevel 时若未按本关机制实现 `IDrawStrokeCompletedHandler`，线条完成后不会自动驱动角色或机关 | `DLevel_001` 已由 `DLevel001Ctrl` 实现；后续关卡继续使用关卡本地脚本，不上提到 `DrawPlayCtrl` |
| P0 | 成功门触发、短动画或 Victory UI 任一环节缺失会导致 Draw 无法完成通关 | `DLevel_001` 至 `DLevel_005` 的 `DrawGame_SuccessDoor` 已绑定 `DrawLevelCtrl.OnTriggerSuccess`；成功动画完成后再通过 `TryResolve(Success)` 进入既有 Victory UI |
| P1 | `drawArea` 范围或 Layer 配置错误会导致合法落笔被拒绝 | `DLevel_001/DrawLineArea` 已绑定关卡专用 `Collider2D` 并使用 `Draw` Layer；运行时增加硬校验 |
| P1 | 显式 Draw 材质缺失时仍会回退到 `Sprites/Default` | `DLevel_001` 已绑定 Draw 专用材质；保留 fallback 仅用于错误配置兜底 |
| P1 | DrawSpace 非均匀缩放会造成视觉宽度与权威长度尺度不一致 | 运行时输出警告；正式关卡应保持均匀缩放 |
| P1 | 多笔模式可能累积过多对象和碰撞点 | 当前默认单笔；开启多笔前需设置关卡上限并做真机压力验证 |
| P2 | 提示目前为显隐，没有次数、冷却或广告规则 | 等产品契约明确后由上层策略接入 |

## 8. 验证证据

- 目标脚本通过隔离 C# 编译：0 error。
- `DLevel_001.prefab` 中 `DrawLevelCtrl` 脚本 GUID、DrawSpace、提示对象和 Draw 专用材质引用通过静态检查；`lineTemplate` 当前允许为空。
- Draw HUD prefab 的墨水条序列化引用通过静态检查。
- 目标文件通过 `git diff --check`。

未验证：

- 完整 Unity 编译被工作区中并行生成的 `Assets/Game/UI/LevelChoicePanel/Sprites/Scripts/DrawGame_ChoiceLevelPanel.Generated.cs` 语法错误阻断。
- 未执行 Unity PlayMode、Android/iOS 真机触控、横屏适配、暂停中断手势、机关碰撞、结果面板和 shader stripping 验证。

## 9. 回滚路径

1. 从 `DLevel_001.prefab` 移除 `DrawLevelCtrl`。
2. 回退 `DrawPlayCtrl` 和 `DrawGame_GamePlayPanel` 的 Draw 运行时绑定。
3. 删除 `DrawLevelRuntimeTypes.cs`、`DrawLevelCtrl.cs` 及其 meta。
4. 回退 Draw HUD prefab 的 `InkProgressBars` 引用，即恢复为原先只加载关卡和 HUD 的行为。

## 10. 2026-07-27 Odin AutoBind

- `DrawLevelCtrl` Inspector 新增 `AutoBind` 按钮，仅供编辑器人工点击。
- 按钮只补齐空引用，不覆盖已有人工绑定；可查找当前关卡子层级中的 Camera、`TipsObject`、名为 `DrawLineRender` 的玩家线 `LineRenderer`，并把当前组件 Transform 作为缺省 DrawSpace。
- 可绘制区域只绑定明确命名为 `DrawArea` 或 `DrawAreaCollider` 且自身带有 `Collider2D` 的对象，不会扫描并误用角色或机关碰撞体。
- 操作支持 Unity Undo，设置组件 Dirty，并记录 prefab instance 属性修改；不主动保存资源。
- 工具不创建对象，不访问 DB、Save、配置、运行时单例，也不从 `OnValidate`、`Reset` 或其它自动编辑器生命周期触发。
- 隔离 C# 编译通过：0 error / 1121 个项目既有 warning；验证时只排除了无关的选关生成脚本语法错误、重复 LevelItem 目录和工程文件中的失效 DrawTips 脚本引用，一次性过滤文件已删除。
- 尚未在 Unity Inspector 中人工点击验证按钮展示、Undo 和 prefab 保存结果；若需回滚，删除 `DrawLevelCtrl` 的 Editor Tools 区域及对应 Odin/UnityEditor using 即可。

## 11. 2026-07-27 玩家线模板回退与渐变颜色

### 11.1 需求与行为变更

- `lineTemplate` 从运行时硬依赖改为可选配置。缺少模板时只记录警告，并按 `drawingLineWidth` 创建运行时 `LineRenderer`，避免关卡资源已经加载成功却被判定初始化失败。
- 推荐在 DLevel 根节点下创建名为 `DrawLineRender` 的直接子节点，挂载 `LineRenderer` 并配置宽度、排序层、圆角等表现；点击 `DrawLevelCtrl.AutoBind` 后会补齐 `lineTemplate`。
- 玩家线颜色改为 `drawingLineGradient`，沿 LineRenderer 长度使用顶点色插值。原 `SetDrawingStyle(Material, Color)` 保留并转换为纯色 Gradient，新增 `SetDrawingStyle(Material, Gradient)` 供画笔皮肤或控制器运行时切换。
- 新建 `DrawLevelCtrl` 与 `DLevel_001` 的默认玩家线均为不透明黑色渐变；材质 Tint 保持白色，避免与 LineRenderer 顶点色相乘后改变配置颜色。
- `DLevel_001` 显式绑定 `DrawLineGradient.mat`。其 Shader 仅采样 `_MainTex` 并乘以 LineRenderer 顶点色和材质 Tint，不创建每条线独立材质。
- 提示线 `TipsObject` 保持独立，不受玩家线材质或渐变接口影响。

### 11.2 资源与打包流

- Shader：`Assets/Game/ArtSrc/Shader/DrawGame/DrawLineGradient.shader`
- Material：`Assets/Game/ArtSrc/Shader/DrawGame/DrawLineGradient.mat`
- `DLevel_001.prefab -> DrawLineGradient.mat -> DrawLineGradient.shader` 形成显式依赖；DrawLevel 的 YooAsset Collector 构建关卡包时应收集对应依赖。

### 11.3 性能与风险

- 渐变由 LineRenderer 已有顶点色完成，Shader 没有额外贴图、噪声、分支或逐帧脚本更新；相对纯色线不会新增 GameObject、材质实例或 CPU 采样。
- 渐变精度受 LineRenderer 生成的几何顶点数量影响；当前固定距离采样已经提供足够的长度分段，但极少采样点的短线仍只表现为首尾插值。
- 透明线条仍受排序层、Z 值和透明物体叠加顺序影响，`DrawLineRender` 模板需要按关卡实际画面配置 Sorting Layer / Order。
- Shader 已由 Unity 导入且没有发现对应 Shader error，C# 工程构建为 0 error / 102 个项目既有 warning；尚未完成 PlayMode 视觉截图、Android/iOS 真机和 YooAsset 完整构建验证。

### 11.4 回滚

1. 将 `ApplyDrawingStyle` 恢复为写入 `startColor/endColor`，并移除 Gradient 重载。
2. 将 `DLevel_001.drawingLineMaterial` 清空，删除 DrawLineGradient Shader、Material 及 meta。
3. 如需恢复旧硬依赖，再把 `lineTemplate` 放回初始化失败条件；这会重新引入未绑定模板时无法进关的问题，不建议作为常规回滚。

## 12. 2026-07-27 失败监听与整关 Reset

### 12.1 需求与原行为

- 需求：Draw 失败判断参考主线 `BaseLevelCtrl`，并实现 `DrawGame_GamePlayPanel` 的 Reset 功能。
- 原行为：`DrawLevelCtrl` 只提供 `TryResolve` 接口，没有监听主线陷阱、失败区域或 `ActorCtrl.IsDied`；HUD Reset 仅调用 `ResetAttempt()` 清理线条、碰撞体和墨水，无法恢复已经死亡的 Actor 或已改变状态的机关。
- 主线证据：`BaseLevelCtrl.CheckFinished()` 使用 `IsFail() || Actor.IsDied` 判断失败；陷阱事件先播放死亡演出，失败区域事件短暂延迟后置失败；首次失败后等待 1 秒触发整关重建。

### 12.2 当前行为

- `DrawLevelCtrl.InitializeRuntime()` 通过 Bear `EventSubscriber` 复用已有 `OnTriggerTrapEvent` 和 `OnTriggerFailAreaEvent`，销毁时统一解除订阅。
- 陷阱事件立即调用当前 DLevel 子层级中的 `ActorCtrl.Die()`，等待主线同款 `1.5` 秒死亡演出后提交失败；失败区域事件播放 `failed` 音效，等待主线同款 `0.2` 秒后提交失败。
- `DrawLevelCtrl.Tick()` 即使画线输入已经关闭，仍会检查失败条件；首次满足失败区域标记或 `ActorCtrl.IsDied` 时，先通过 `TryResolve(Failed)` 幂等锁定结果，再派发 `DrawGameResetEvent(Failed)`。
- `DrawGame_GamePlayPanel.ResetBtn` 不再持有控制器 Action，点击后直接派发 `DrawGameResetEvent(Manually)`。
- `DrawLevelCtrl` 失败结算后先等待主线同款 `1` 秒再派发 `DrawGameResetEvent(Failed)`；`DrawPlayCtrl` 对手动和失败 Reset 统一等待 `0.5` 秒黑幕渐显后整关重载。
- 未复用主线 `GameResetEvent`：`InterstitialAdPolicy` 是其常驻订阅者，Draw 派发该事件会额外改变主线失败广告冷却与失败计数。Draw 专用事件保留相同的事件结构，同时隔离该副作用。
- 手动 Reset、失败自动重试和控制器切换共同使用 `_isOpeningLevel` 与 `_activationVersion` 门禁，旧延迟回调不能重复创建关卡。

### 12.3 数据、资源、UI 与平台边界

- 影响脚本：
  - `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl.cs`
  - `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
  - `Assets/Game/Scripts/Play/PlayEvents.cs`
  - `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_GamePlayPanel.cs`
- 未修改 `DLevel_001.prefab`、Luban 配置、DB schema、存档、广告、IAP 或 analytics；Draw HUD prefab 使用用户新增的 `BlackMask` 并补充脚本序列化引用。
- 重载继续使用当前 `_drawLevelPath` 和 Bear `ResourceManager`；旧 Draw HUD 与 DLevel 一并
  销毁，新 DLevel 初始化后由 Bear `UIManager` 创建新 HUD，并绑定关卡配置和初始墨水状态。
- 每次 Reset 会产生 DLevel 和 HUD 的销毁/实例化峰值；不新增常驻 Update、轮询对象或
  每帧分配，Android/iOS 横屏没有新增平台分支。

### 12.4 验证与剩余风险

- 目标脚本 `git diff --check`：通过。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`：最终通过，`0 error / 102` 个项目既有 warning。
- 静态确认 `DLevel_001.prefab` 子层级存在 `ActorCtrl`，Draw HUD prefab 的 `ResetBtn` 已序列化绑定。
- 静态确认 `DrawPlayCtrl` 在 `Activate()` 订阅、`Deactivate()` 解除 `DrawGameResetEvent`；主线 `GameResetEvent` 的 `InterstitialAdPolicy` 常驻订阅链未被 Draw 触发。
- 未验证：Unity PlayMode 中陷阱死亡演出时长、失败区域音效、失败后自动重载、Reset 连点、暂停中失败、物理回调与销毁同帧交错。
- 未验证：Android/iOS 横屏触控、切后台时失败延迟、真机重载峰值和 YooAsset 包模式重载。
- `uncertain`：当前 `DLevel_001` 静态 YAML 未发现 `FailAreaTriggerHandle` 直接引用；失败区域链需在后续包含该组件的 DLevel 或 PlayMode 注入场景中验证。

### 12.5 回滚

1. 从 `DrawLevelCtrl` 删除失败事件订阅、Actor 缓存、失败延迟和 `CheckFinished()` 调用，恢复 Tick 只处理画线输入。
2. 从 `PlayEvents.cs` 删除 `DrawGameResetEvent`，让 `DrawGame_GamePlayPanel` 恢复 Reset Action 绑定。
3. 从 `DrawPlayCtrl` 删除 `DrawGameResetEvent` 订阅和整关重载方法，恢复局部调用 `IDrawLevelRuntime.ResetAttempt()` 与 `IDrawStrokeCompletedHandler.OnDrawAttemptReset()`；无需迁移 prefab、配置或玩家存档。

## 13. 2026-07-27 DrawLineArea Layer 门禁

### 13.1 需求与原行为

- 需求：`DrawLineArea` 必须使用项目已有的 `Draw` Layer。
- 原行为：`DrawLevelCtrl` 只检查 `drawArea` 是否启用、激活以及是否覆盖落笔世界坐标，不校验所属 Layer；错误配置仍可进入运行时。
- 本次开始前的工作区证据显示 `DLevel_001/DrawLineArea` 已由用户设为 Layer 14，`ProjectSettings/TagManager.asset` 中 Layer 14 对应 `Draw`。

### 13.2 当前行为

- `DrawLevelCtrl.InitializeRuntime()` 在启用输入前校验已绑定 `drawArea` 的 GameObject Layer。
- 项目缺少 `Draw` Layer 或 `drawArea` 不在 `Draw` Layer 时，控制器保持 `IsRuntimeReady=false` 并通过 Bear Logger 输出错误，不进入画线输入。
- `drawArea` 为空时继续保留相机视口 fallback，本次不把可绘制区域引用升级为必填项。
- Odin `AutoBind` 新增识别节点名 `DrawLineArea`，并且只绑定自身带 `Collider2D` 且位于 `Draw` Layer 的候选对象；不会自动改 Layer、创建对象或覆盖已有人工引用。

### 13.3 影响、验证与风险

- 影响脚本：`Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl.cs`。
- 配置证据：`Assets/Game/Prefabs/DrawLevel/DLevel_001.prefab` 当前工作区的 `DrawLineArea.m_Layer=14`；该 prefab 还有用户其它未提交改动，本次不改写。
- 不涉及 DB、存档、事件、UI、analytics、资源地址或 Android/iOS 平台分支。
- `git diff --check` 对本次脚本和中文文档通过。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` 通过：`0 error / 102` 个项目既有 warning。
- 待验证：Unity Inspector 中 AutoBind 对错误 Layer 候选的拒绝、PlayMode 中错误 Layer 初始化失败日志、Android/iOS 横屏触控。

### 13.4 回滚

1. 删除 `DrawAreaLayerName`、`ValidateDrawAreaLayer()` 及 `InitializeRuntime()` 中对应门禁。
2. 将 `FindEditorDrawArea()` 恢复为只按节点名和 `Collider2D` 返回候选。
3. prefab Layer 无需随代码回滚；保留 `Draw` Layer 仍符合当前物理分层约定。

## 14. 2026-07-27 Draw Reset 黑幕渐变

### 14.1 需求与原行为

- 需求：Draw 手动 Reset 与失败重试需要复用主线 `GamePlayPanel` 的黑屏渐隐、渐显表现。
- 原行为：`DrawGame_GamePlayPanel` 收到 Reset 后只派发事件，`DrawPlayCtrl` 直接销毁 HUD 与 DLevel；没有黑幕过渡，资源重载过程可能直接暴露给玩家。
- 主线证据：`GamePlayPanel.OnGameReset()` 用 `blackMask.DOFade(1, 0.4)` 让画面渐隐至黑；`PlayCtrl` 等待 `0.5` 秒后重载，重建完成派发 `GamePlayPanelFadeInEvent`，黑幕再用 `0.4` 秒渐隐，主线等待 `1` 秒后恢复 Playing。

### 14.2 当前行为与事件流

- 用户已在 `DrawGame_GamePlayPanel.prefab` 增加 `BlackMask` 与 `ClickBlock`；本次将已有 `BlackMask` Image 序列化绑定到 Draw 面板，没有新建替代节点。
- `DrawGame_GamePlayPanel` 在激活期间订阅 `DrawGameResetEvent` 与既有 `GamePlayPanelFadeInEvent`：
  - 收到手动或失败 Reset 时，黑幕在 `0.4` 秒内由透明变为全黑。
  - DLevel 重载完成后，黑幕在 `0.4` 秒内由全黑变为透明并自动隐藏。
- Draw HUD 同时绑定用户新增的 `ClickBlock`，复用主线 `GamePlayPanelSwitchBlockEvent` 按事件参数开启或关闭点击拦截；面板每次打开时先恢复为关闭状态。
- 失败结果先在 `DrawLevelCtrl` 等待 `1` 秒再派发 Reset；手动和失败 Reset 到达 `DrawPlayCtrl` 后统一等待 `0.5` 秒再重载，并立即关闭 Draw 输入。
- 修正前虽然旧 Draw HUD 先把 `BlackMask` 渐变到全黑，但重载入口随后执行
  `CloseAllUI(UILayer.Normal)`，并在 DLevel Intro 后销毁、重建 HUD；负责遮挡的旧面板会被
  提前关闭，无法保证资源加载和 Intro 全程保持黑幕。
- 修正后 Reset 与主线一致保留同一个 Draw GameplayPanel：不再关闭 Normal UI 或重建 HUD，
  只销毁、重建 DLevel。新运行时与 Intro 准备完成后，在全黑状态调用
  `ResetForNewAttempt(...)` 恢复关卡号、基础进度宽度、星级节点、墨水快照和奖励按钮，
  再向同一面板派发渐显事件。
- Reset 开始时按主线关闭 Popup；保留面板后通过广告请求版本号淘汰 Reset 前尚未返回的
  RewardAD 回调，避免旧请求向新尝试发放墨水。
- Draw 胜利面板的 `GameReplayEvent` 也按主线由 GameplayPanel 先启动同一黑幕，再经过
  `ResetGameAsync()` 的 0.5 秒等待进入相同重载流程，不在加载完成后突然切黑。
- 渐显开始后继续等待当前 `ResetFadeInInputDelaySeconds=0.5` 秒才调用
  `BeginDrawing()`，避免黑幕过渡期间接受画线输入。
- Reset 请求继续使用 `DrawGameResetEvent` 隔离主线广告策略；只复用无业务数据的既有 `GamePlayPanelFadeInEvent`。

### 14.3 影响、验证与风险

- 影响文件：
  - `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
  - `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_GamePlayPanel.cs`
  - `Assets/Game/UI/DrawGame/GamePlayPanel/Resources/DrawGame_GamePlayPanel.prefab`
- `git diff --check`：通过。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`：通过，`0 error / 1120` 个项目既有 warning。
- 未改动 Draw 存档、配置、analytics、广告策略、资源地址或主线 `GamePlayPanel`。
- 未验证：Unity PlayMode 中黑幕实际覆盖范围、`GamePlayPanelSwitchBlockEvent` 开关点击拦截、连续 Reset、失败与手动 Reset 的视觉节奏、低速资源加载、暂停/切后台，以及 Android/iOS 横屏安全区表现。
- `uncertain`：用户新增 `BlackMask` 的 RectTransform 最终全屏覆盖效果尚未经过 GameView 多分辨率目视验证。
- prefab 中另有 `BlackMask (1)` / `BlackMask (2)`，静态证据显示它们是位于
  `y=840/-840`、尺寸 `stretchX + 320 x 600` 且带子 Image 的上下边缘裁剪 Mask；
  Reset 实际绑定的是默认 inactive、全屏 stretch 的独立 `BlackMask`，三者不是同一引用。

### 14.4 回滚

1. 从 Draw 面板移除 Reset/FadeIn/SwitchBlock 事件订阅、`blackMask` / `clickBlock` 字段及 DOTween 渐变方法。
2. 从 `DrawPlayCtrl` 删除统一 `0.5` 秒等待、重载完成的 `GamePlayPanelFadeInEvent` 和
   `0.5` 秒输入等待，并恢复 Reset 直接销毁、重建 HUD。
3. 从 prefab 面板脚本数据中解除 `blackMask` 引用；用户新增的美术节点是否保留由 UI 需求决定。
4. 若只回滚本次遮挡修正，恢复 Reset 调用 `CloseAllUI(UILayer.Normal)`、Intro 后重建 HUD 的旧流程，并删除 `ResetForNewAttempt(...)` 与广告请求版本门禁。

## 15. 2026-07-27 画线功能组件化与 HUD 事件同步

### 15.1 需求与原行为基线

- 需求：把 Draw 输入、线条、碰撞体和长度统计从 `DrawLevelCtrl` 抽到独立脚本；`DrawLevelCtrl` 只负责游戏状态逻辑，持有该功能并可读取长度；长度数据通过 Event 发送给 `DrawGame_GamePlayPanel`。
- 原行为：`DrawLevelCtrl` 同时持有画线序列化配置、触摸/鼠标采样、`LineRenderer`、`EdgeCollider2D`、墨水长度和失败/结算状态，职责混合。
- 原 HUD 数据流为 `DrawLevelCtrl.InkChanged -> DrawPlayCtrl.OnInkChanged -> DrawGame_GamePlayPanel.SetInkState`，控制器直接参与 HUD 长度转发。
- `DLevel_001` 的既有画线参数为迁移基线：原相机、DrawSpace、DrawLineArea、LineRenderer 模板、提示对象、材质、Gradient、线宽、采样间距、最大长度、多笔开关、碰撞体参数和星级阈值均原值迁移，不重新配置。

### 15.2 当前职责与公开边界

- 新增 `DrawLineFeature`，独立负责：
  - 鼠标/触摸输入和 UI 命中排除。
  - 屏幕坐标投射、DrawLineArea 范围判断和 `Draw` Layer 初始化门禁。
  - 固定间距采样、运行时 `LineRenderer`、完成笔 `EdgeCollider2D`。
  - 已画长度、剩余墨水、星级、提示对象和画笔样式。
  - 暂停、失焦、禁用输入时取消未完成笔并返还长度。
- `DrawLevelCtrl` 保留关卡初始化门禁、暂停状态、Actor/失败监听、异步失败延迟、幂等结算、Reset 状态和关卡内 Stroke 完成响应；通过序列化字段持有同一 prefab 上的 `DrawLineFeature`。
- `DrawLevelCtrl.DrawnLength` 与 `IDrawLevelRuntime.DrawnLength` 提供当前尝试已使用的 DrawSpace 折线总长度；`InkState`、笔数、输入状态等继续由控制器门面代理，外围不需要直接查找组件。
- `DrawLineFeature.StrokeCompleted` 只向持有它的 `DrawLevelCtrl` 发本地 C# 通知；`DrawLevelCtrl` 直接通知同一 DLevel 内的 `IDrawStrokeCompletedHandler`，不再通过 `IDrawLevelRuntime` 上送给 `DrawPlayCtrl`。关卡胜负不进入画线组件。

### 15.3 Event、Prefab 与数据流

- 每次有效采样、长度返还、加墨、Reset 和开放输入时，`DrawLineFeature` 派发 Bear `DrawInkStateChangedEvent`，载荷为只读 `DrawInkState`。
- `DrawGame_GamePlayPanel` 在 `OnOpen` 注册该事件，在 `OnClose` 统一解除；收到事件后使用 `RemainingNormalized` 更新现有进度条。
- `DrawPlayCtrl` 已移除 `InkChanged` 订阅、解除和直接 `SetInkState` 调用，不再承担 HUD 长度数据转发。
- 当前数据流为：`DrawLineFeature -> DrawInkStateChangedEvent -> DrawGame_GamePlayPanel`；完成笔的关卡内职责链为 `DrawLineFeature.StrokeCompleted -> DrawLevelCtrl -> IDrawStrokeCompletedHandler`，最终胜负才通过 `DrawLevelCtrl.Resolved -> DrawPlayCtrl` 上送。
- `DLevel_001` 根节点新增 `DrawLineFeature`，原全部画线字段原值迁入该组件；`DrawLevelCtrl.drawFeature` 显式指向它。`DrawLineArea` 继续引用原 Collider，当前工作区 Layer 仍为 14（`Draw`）。
- 不改 DB、Luban、存档、analytics、广告、资源地址或主线 `GamePlayPanel`；新增全局 Event 是用户本次明确指定的数据通道。

### 15.4 风险、验证与未验证缺口

- C# 验证：因 Unity 生成的 `Assembly-CSharp.csproj` 尚未自动收录新文件，验证时临时加入 `DrawLineFeature.cs` 编译项；`dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` 最终为 `0 error`，随后已撤销临时工程文件改动。
- 静态验证：仓库中不再存在 `InkChanged` 订阅或接口成员；Prefab 新脚本 GUID 与 meta 一致，`DrawLevelCtrl.drawFeature` 指向同一根节点组件；画线序列化参数完整保留。
- 性能边界与原行为一致：拖动每个采样点派发一次同步事件并刷新 HUD；没有新增独立 `Update`、材质实例频率或持续物理重建。若未来采样间距显著减小，应在真机重新评估 Event/HUD 刷新频率。
- 未验证：Unity 导入新脚本后的 Inspector 无 Missing Script、Prefab 实例化、PlayMode 鼠标画线、碰撞体生成、Reset/加墨、HUD 首帧和逐点进度。
- 未验证：Android/iOS 横屏多指、UI 遮挡、切后台取消当前笔、真机事件频率与 GC；本次没有新增平台条件分支。
- `uncertain`：当前只有 `DLevel_001` 可核对和迁移；未来新增 DLevel 必须同时配置 `DrawLevelCtrl` 与 `DrawLineFeature`，否则控制器会记录错误并保持 `IsRuntimeReady=false`。

### 15.5 回滚

1. 将 `DrawLineFeature` 的画线字段和实现恢复到 `DrawLevelCtrl`，删除新组件脚本与 meta，并把 `DLevel_001` 的序列化字段迁回原组件。
2. 从 `IDrawLevelRuntime` 删除 `DrawnLength`，恢复 `InkChanged`；让 `DrawPlayCtrl` 重新订阅并调用面板 `SetInkState`。
3. 删除 `DrawInkStateChangedEvent` 及面板订阅。回滚不涉及玩家存档、配置迁移或资源地址。

## 16. 2026-07-27 Draw 专用胜利面板与结算接入

### 16.1 需求与原行为基线

- 需求：参考主玩法 `GameVictoryPanel` 的表现和事件边界，为 Draw 模式实现独立胜利面板；本节记录的初版只预留 `VictoryStarGroup` 动画完成入口，后续接入见第 18 节。
- 原行为：`DrawPlayCtrl.OnLevelResolved` 收到成功结果后只切换阶段并记录日志，不展示胜利 UI，也不写入 `DB.DrawGame` 的通关、解锁和当前关卡数据。
- 主玩法 `GameVictoryPanel` 的下一关和重玩按钮会先销毁自身，再分别派发 `EnterNextLevelEvent` 与 `GameReplayEvent`；活动中的 `PlayCtrl` 订阅事件并执行关卡切换。Draw 面板需要保持相同边界，而不是向 `DrawPlayCtrl` 注入按钮回调或由控制器持有面板。

### 16.2 当前实现与数据流

- `DrawGame_GameVictoryPanel` 在运行时按现有节点名解析 Spine、粒子、提示文字、下一关和重玩按钮；`Configure` 只接收当前 `DrawLevelData` 和结算星数。
- 成功链路为 `DrawLevelCtrl -> DrawPlayCtrl.OnLevelResolved -> RecordVictoryProgress -> DB.DrawGame -> DrawGame_GameVictoryPanel`。结算会记录已通关关卡、最大进度、下一关解锁和当前关卡，并只在数据实际变化时保存。
- 面板下一关按钮销毁自身并派发既有 `EnterNextLevelEvent`；Draw 激活期间由 `DrawPlayCtrl.OnEnterNextLevel` 订阅处理，清理 HUD 和当前关卡后加载配置顺序中的下一关，不存在下一关或加载失败时返回 Draw 选关界面。
- 面板重玩按钮销毁自身并派发既有 `GameReplayEvent`；`DrawPlayCtrl.OnGameReplayEvent` 恢复当前关卡存档指针并复用现有重载链路。
- 主线 `PlayCtrl` 与 `DrawPlayCtrl` 都只在各自激活期间订阅这两个事件，并在停用时通过 `EventsUtils.ResetEvents` 清理订阅，避免两个玩法控制器同时处理同一按钮事件。
- `DrawPlayCtrl` 不持有胜利面板引用，不接收按钮 `Action`，也不为胜利面板持有完整 `DrawLevelData`；展示时根据当前 `DrawLevelSort.Id` 临时取得配置。
- 本节记录的初版尚未接入 `VictoryStarGroup`，当时由占位流程直接调用 `NotifyVictoryStarAnimationCompleted()`；第 18 节已记录真实星组接入及下一关门禁。
- 面板通过 `_isConfigured` 隔离 `OpenUI` 与工厂配置的先后顺序，避免入场动画先完成时使用空关卡数据提前开放下一关。

### 16.3 影响范围、验证与风险

- 影响文件：`DrawPlayCtrl.cs`、`DrawGame_GameVictoryPanel.cs/.meta`、`DrawGame_GameVictoryPanel.prefab`。
- 没有修改主玩法 `GameVictoryPanel`、主玩法存档、明信片流程或 `ActorCtrl`，也没有新增逐帧逻辑和平台条件分支。
- C# 验证：职责修正后执行 `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 `0 error / 1120 warning`；本次未处理工程既有 warning。
- 静态验证：面板不再存在下一关/重玩 `Action` 字段，按钮分别派发 `EnterNextLevelEvent` / `GameReplayEvent`；`DrawPlayCtrl` 不再持有胜利面板和完整关卡配置字段，并在激活期订阅两个事件。
- 未验证：Unity 重新导入 Prefab 后 Inspector 无 Missing Script、旧序列化字段清理、Spine/粒子/提示文字/按钮实际表现。
- 未验证：PlayMode 中主线与 Draw 控制器切换后的事件订阅互斥、首次通关、重复通关、最后一关、下一关配置缺失、面板创建失败、重玩及存档落盘的完整链路。
- 未验证：Android/iOS 横屏安全区、触控按钮、切后台恢复和真机资源加载；本次实现仅在成功结算时运行，不增加常驻性能成本。
- 本节当时的 `uncertain` 项为星组动画时序、星数来源和数据边界；第 18 节已按 Draw 当前结算星数完成局部实现，PlayMode 与真机表现仍待验证。

### 16.4 回滚

1. 删除 Draw 专用胜利面板脚本及 meta，将 Prefab 根组件恢复到改动前状态。
2. 从 `DrawPlayCtrl` 删除胜利进度写入及 `EnterNextLevelEvent` / `GameReplayEvent` 订阅处理。
3. 已经写入玩家存档的通关和解锁数据不会因代码回滚自动删除；若需要撤销已落盘数据，必须另行提供明确的数据迁移或清理方案。

## 17. 2026-07-27 Stroke 完成职责下沉

### 17.1 需求与原行为基线

- 需求：Stroke 的控制不得放在 `DrawPlayCtrl`；`DrawPlayCtrl` 只负责玩法生命周期、阶段、暂停、重载和最终结算。
- 原行为：`DrawPlayCtrl` 在 DLevel 实例化后扫描全部子组件并缓存 `IDrawStrokeCompletedHandler`，订阅 `IDrawLevelRuntime.StrokeCompleted`，一笔完成后逐个执行 Handler，并根据单笔配置关闭输入。
- 风险：上层玩法控制器知道了关卡内部 Stroke 组件和单笔规则，导致新增关卡机关时必须经过 `DrawPlayCtrl`，扩大关卡局部变化的影响范围。

### 17.2 修改后职责与数据流

- `IDrawLevelRuntime` 删除 `StrokeCompleted` 事件，不再向玩法控制器暴露完成笔通知。
- `DrawLevelCtrl.InitializeRuntime()` 在当前 DLevel 子层级缓存 `IDrawStrokeCompletedHandler`。
- `DrawLineFeature` 完成一笔后只通知持有它的 `DrawLevelCtrl`；`DrawLevelCtrl` 在本关内逐个调用 Handler，并在不允许多笔时关闭 `DrawLineFeature` 输入。
- `DrawLevelCtrl.ResetAttempt()` 在重置画线和关卡状态后调用 `IDrawStrokeCompletedHandler.OnDrawAttemptReset()`；单个 Handler 异常通过 Bear Logger 记录，不阻断其它 Handler。
- `DrawPlayCtrl` 删除 Handler 列表、组件扫描、Stroke 订阅/解除、完成笔回调和单笔门禁，只继续处理 DLevel 加载、Intro/Drawing/Resolved 状态、暂停、重载、提示与最终胜负。
- 当前职责链为：
  - 画线：`DrawLineFeature -> DrawLevelCtrl -> IDrawStrokeCompletedHandler`。
  - 结算：`DrawLevelCtrl.Resolved -> DrawPlayCtrl`。
  - HUD 长度：`DrawLineFeature -> DrawInkStateChangedEvent -> DrawGame_GamePlayPanel`。
- 本次未新增全局事件，未修改 prefab、DB、配置、存档、analytics、广告、资源地址或平台分支。

### 17.3 验证、风险与回滚

- `rg` 静态确认 `DrawPlayCtrl` 不再引用 `StrokeCompleted`、`IDrawStrokeCompletedHandler` 或 Handler 缓存逻辑。
- `git diff --check` 对三个目标脚本通过。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` 通过：`0 warning / 0 error`。
- 未验证：Unity PlayMode 中完成第一笔后的关卡 Handler 调用顺序、单笔输入关闭、多笔继续输入和局部 `ResetAttempt()` 回调。
- 未验证：Android/iOS 横屏触控、切后台中断和真机物理交互；本次没有新增逐帧扫描，Handler 只在初始化、完成笔或 Reset 时执行。
- `uncertain`：当前代码库没有具体组件实现 `IDrawStrokeCompletedHandler`，因此静态编译只能验证职责边界，不能验证真实机关演出。
- 回滚方式：恢复 `IDrawLevelRuntime.StrokeCompleted`、`DrawLevelCtrl` 的事件转发，以及 `DrawPlayCtrl` 的 Handler 扫描、完成笔回调和单笔门禁；无需迁移 prefab 或玩家数据。

## 18. 2026-07-27 Draw 独立 VictoryStarGroup

> 2026-07-28：本节记录初版“运行时星数直接传入面板”的行为；当前星级写入、等待动画消费和选关读取链路已由第 24 节替代。

### 18.1 需求与原行为基线

- 需求：参考 `Postcard_VictoryStarGroupCtrl` 的逐颗星动画表现，实现 `DrawGame_VictoryStarGroup`；星星获得来源必须是 Draw 当前尝试自己的星级内容，完全不依赖 Bag。
- 原行为：`DrawPlayCtrl.ShowVictoryPanel()` 已从当前 `IDrawLevelRuntime.InkState.StarCount` 取得结算星数并传给 `DrawGame_GameVictoryPanel.Configure(...)`，但面板只保留 `_victoryStarCount`，入场结束后直接调用完成入口。
- 原资源：`DrawGame_VictoryStarGroup.prefab` 与 Postcard 星组使用相同的三个 `Postcard_UIStarCollection` 子 prefab，但根节点没有运行时控制脚本，三个 Toggle 也没有显式绑定给 Draw 组件。

### 18.2 当前实现、数据与 UI 流

- 新增 `DrawGame_VictoryStarGroup` 局部组件，并绑定到 `DrawGame_VictoryStarGroup.prefab` 根节点；prefab 按第 1、2、3 颗星顺序显式绑定三个 Toggle。
- `DrawGame_GameVictoryPanel` 的 Spine、提示文字、下一关按钮组、下一关按钮、重玩按钮、打开粒子和 Draw 星组全部改为 Inspector 序列化字段，并在胜利面板 prefab 中显式绑定；运行时不再按节点名扫描按钮或文字，也不再通过 `GetComponentsInChildren` 猜测界面功能。
- 星级数据流为 `DrawLineFeature.CreateInkState() -> IDrawLevelRuntime.InkState.StarCount -> DrawPlayCtrl.ShowVictoryPanel() -> DrawGame_GameVictoryPanel.Configure(...) -> DrawGame_VictoryStarGroup.Refresh(...)`。
- `Refresh(...)` 只接收本次结算传入的整数星数，并按当前三个 Toggle 限制到 `0..3`；不读取或写入 `Bag`、`DB.DrawGame`、`DB.Postcard`、`PostcardManager` 或其它永久数据。
- 面板入场完成且配置已注入后启动星组：先清空 Toggle 和呼吸光，等待 `1.7` 秒，再按 `0.18` 秒间隔逐颗触发子节点 Animator 的 `play` Trigger、播放 `collectedStar` 音效并点亮 Toggle。
- 最后一颗星的 Animator 片段时长结束后显示已点亮星的 `UI_common_xingfaguang_01`，再调用 `NotifyVictoryStarAnimationCompleted()`；下一关按钮继续以该完成状态作为交互门禁。
- 星数为 0、Toggle 不可用或胜利面板未找到星组组件时，面板立即完成星组阶段，避免下一关按钮永久锁死。组件停用或销毁时会取消当前 Tween，并安全释放等待中的完成回调。
- 本次没有新增全局事件、存档字段、配置字段、资源加载、逐帧 `Update`、编辑器生命周期入口或平台条件分支；单次表现最多调度三颗星和一个完成 Tween。

### 18.3 受影响文件、验证与剩余风险

- 受影响文件：
  - `Assets/Game/UI/GameVictory/Scripts/DrawGame_VictoryStarGroup.cs/.meta`
  - `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel.cs`
  - `Assets/Game/UI/DrawGame/Prefabs/DrawGame_VictoryStarGroup.prefab`
  - `Assets/Game/UI/DrawGame/VictoryPanel/Resources/DrawGame_GameVictoryPanel.prefab`
- 编译验证：临时让 Unity 生成工程包含新脚本后执行 `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`，结果为 `0 error / 102 warning`；warning 为工程现有项。
- 静态验证：脚本依赖扫描未发现 `Bag`、`DB`、`PostcardManager` 或 `GameManager` 代码引用；胜利面板脚本不再包含节点名常量、`GetComponentsInChildren` 或按名称匹配逻辑；两个 prefab 的脚本 GUID、Inspector 字段和三个 Toggle fileID 与新脚本 meta 一致。
- 待最终收尾复核：目标差异的 `git diff --check`、面板到星组的类型引用以及文档 UTF-8 内容。
- 未验证：Unity 导入后 prefab Inspector 无 Missing Script、三个 Toggle 顺序、Animator `play` Trigger、音效、呼吸光以及下一关按钮实际解锁时机。
- 未验证：0/1/2/3 星 PlayMode 表现、动画中关闭或点击重玩、面板重复打开，以及 Android/iOS 横屏触控、切后台和不同分辨率表现。
- `uncertain`：Animator 的实际片段时长与视觉节奏只能由 Unity 运行态确认；静态代码只证明完成回调按控制器中最长片段时长等待。

### 18.4 回滚路径

1. 从 `DrawGame_GameVictoryPanel` 删除星组 Inspector 引用与 `Refresh(...)` 调用，恢复入场结束后直接调用 `NotifyVictoryStarAnimationCompleted()`；若同时回滚显式绑定要求，再恢复原运行时引用查找逻辑。
2. 从 `DrawGame_VictoryStarGroup.prefab` 删除 Draw 星组组件和三个 Toggle 的显式引用。
3. 删除 `DrawGame_VictoryStarGroup.cs/.meta`；本功能不写星级存档或 Bag，因此无需数据迁移。

## 19. 2026-07-27 DLevel 专用控制脚本与延迟开画 Handle

### 19.1 需求与原行为基线

- 需求：参考主线 `Level{Num}Ctrl` 的职责结构，为不同 Draw 关卡建立各自的 `DLevel{Num}Ctrl`；每个关卡控制脚本自行判断需要实现的接口，在 `DrawStrokeCompleted` 对应位置执行本关逻辑，不使用统一 Executor 强制承载关卡行为。
- DLevel_001 的明确行为是：第一笔有效 Stroke 完成后，玩家 `ActorCtrl` 持续向右移动。
- 同时提供 `DelayAutoBeginDrawHandle`，暴露 `delay` 参数并持有同一 prefab 内的 `DrawLevelCtrl`；延迟结束后放行现有开场流程，由 `DrawPlayCtrl.BeginDrawing()` 在过场完成位置统一开放输入。
- 原行为只有 `IDrawStrokeCompletedHandler` 接口和 `DrawLevelCtrl` 的子层级扫描，没有任何具体 DLevel 控制脚本实现该接口；`DrawLevelCtrl.PlayIntroAsync()` 始终立即完成。

### 19.2 当前实现与职责边界

- 新增 `DLevel001Ctrl`，只实现本关实际需要的 `IDrawStrokeCompletedHandler`：
  - 持有同一 `DLevel_001` 根节点的 `DrawLevelCtrl` 和本关 `ActorCtrl`。
  - 仅在 `DrawStrokeResult.IsFirstStroke` 为 true 时进入自动向右移动状态。
  - 移动期间每帧设置 `ActorCtrl.SetMoveInput(1f)` 并调用 `ActorCtrl.OnUpdate()`，复用 Actor 已有移动、碰撞和动画处理。
  - 暂停、Actor 死亡、关卡结算、尝试重置或组件禁用时停止驱动；已经施加过移动输入时恢复为 0 并刷新动画。
- `DLevel001Ctrl` 不持有 `BaseItemExecutor`，也不要求后续 `DLevel002Ctrl` 等继承统一基类。后续关卡只实现自身需要的接口，关卡差异保留在对应脚本。
- 新增 `IDrawLevelIntroHandler`；`DrawLevelCtrl.InitializeRuntime()` 与 Stroke Handler 一起缓存本关开场 Handle，`PlayIntroAsync()` 按层级扫描顺序逐个等待。
- `DelayAutoBeginDrawHandle` 实现 `IDrawLevelIntroHandler`，默认 `delay=1` 秒，使用缩放时间并绑定同一 `DrawLevelCtrl`。它只负责开场门禁，不直接修改 `DrawPlayCtrl` 阶段。
- `DLevel_001.prefab` 根节点已绑定 `DLevel001Ctrl` 与 `DelayAutoBeginDrawHandle`；两者的 `DrawLevelCtrl` 引用及 `DLevel001Ctrl.actor` 均为显式本地引用。
- `DrawGame_DrawLine.prefab` 自身持有 `SortingGroup(SceneLayer / 50)`，通过该模板实例化的 `RuntimeStroke_*` 直接继承排序；运行时代码不再添加或覆盖 `SortingGroup`。
- 每个新建的 `RuntimeStroke_*` 仍会在运行时绑定或复用 `Rigidbody2D`，二维刚体固定为 `Dynamic`，但绘制期间保持 `simulated=false`，避免未完成线条提前受重力或碰撞影响。
- 只有线条通过有效性检查，并在 `EdgeCollider2D` 写入最终点集后，才会设置 `simulated=true` 并唤醒刚体；取消或长度不足的线条会直接销毁，不会进入物理模拟。

### 19.3 调用与数据流

- 开场：`DrawPlayCtrl.OpenDrawLevelAsync -> DrawLevelCtrl.InitializeRuntime -> DrawLevelCtrl.PlayIntroAsync -> DelayAutoBeginDrawHandle.PlayDrawIntroAsync -> 既有过场完成回调 -> DrawPlayCtrl.BeginDrawing`。
- Stroke：`DrawLineFeature.StrokeCompleted -> DrawLevelCtrl.OnStrokeCompleted -> IDrawStrokeCompletedHandler.OnDrawStrokeCompleted -> DLevel001Ctrl`。
- Actor：`DLevel001Ctrl.Update -> ActorCtrl.SetMoveInput(1f) -> ActorCtrl.OnUpdate()`；关卡 `Resolved`、暂停、死亡、Reset 或 Disable 会停止本地驱动。
- 本次没有新增全局 `PlayEvents`、HUD 消息、存档、Luban 配置、资源地址、analytics、广告或平台分支。

### 19.4 验证、风险与未验证缺口

- `Assembly-CSharp.csproj` 已包含两个新脚本；执行 `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 `0 error / 102 warning`，warning 为工程既有项。
- 2026-07-27 补充验证：`DrawLineFeature` 在创建线条时设置 `simulated=false`，有效线条写入最终 `EdgeCollider2D.points` 后才设置 `simulated=true` 并调用 `WakeUp()`；执行 `dotnet build Assembly-CSharp.csproj --no-restore -v:q` 通过，结果为 `0 error / 1120 warning`，warning 为工程现有项。
- 2026-07-27 磁盘静态扫描：当前未跟踪的 `DrawGame_DrawLine.prefab` 只序列化了 `Transform`、`LineRenderer` 和 `SortingGroup`，尚未发现 `Rigidbody2D`。运行时代码会补充默认刚体，但若需要继承 Inspector 中配置的质量、重力、阻力或约束，必须先在 Unity 中保存该 prefab。
- 静态确认 `DLevel_001.prefab` 的新脚本 GUID 与 meta 一致，`drawLevelCtrl` 指向根节点控制器，`actor` 指向本关唯一的 `ActorCtrl`。
- 未验证：Unity PlayMode 中延迟 1 秒与翻页过场的最终体感、第一笔完成后的向右移动、Actor 朝向/动画、碰撞、进门、胜负和暂停恢复。
- 未验证：Android/iOS 横屏触控、切后台、低帧率下 Actor 移动和真机物理表现；本次没有新增平台条件分支。
- 风险：`DLevel001Ctrl` 复用 `ActorCtrl.OnUpdate()`，因此实际速度、墙体停止、推物和动画取决于 DLevel_001 现有 Actor 参数与碰撞层配置；静态编译不能替代 PlayMode 验证。
- 风险：绘制完成后，运行时线条作为 Dynamic Rigidbody2D 参与二维物理，未额外冻结位置或旋转；最终下落、旋转、碰撞和推动表现取决于 Physics2D 重力、质量、阻力、碰撞层与接触对象，需要在 PlayMode 和移动真机验证。
- 风险：未配置 `lineTemplate` 时的最小回退线条不会自动获得 `SortingGroup`，渲染排序将使用新建 `LineRenderer` 的默认值；要求 `SceneLayer / 50` 的正式 DLevel 必须绑定已正确配置的 `DrawGame_DrawLine.prefab`。
- 风险：`DrawLevelCtrl` 按子层级扫描顺序等待多个 `IDrawLevelIntroHandler`；未来同关配置多个 Handle 时应明确顺序，避免无意叠加过长开场等待。

### 19.5 回滚路径

1. 从 `DLevel_001.prefab` 根节点移除 `DLevel001Ctrl` 与 `DelayAutoBeginDrawHandle` 组件。
2. 删除两个新脚本及 meta，并从 `DrawLevelCtrl` 删除 Intro Handler 缓存和等待逻辑。
3. 从 `DrawLevelRuntimeTypes.cs` 删除 `IDrawLevelIntroHandler`；Stroke Handler 原接口与 `DrawLineFeature` 不需要回滚。
4. 本功能不写玩家数据，回滚无需存档迁移。

## 20. 2026-07-27 Draw 成功门与短胜利动画

### 20.1 需求与原行为基线

- 需求：当前 Actor 接触 `DrawGame_SuccessDoor` 时触发 Draw 胜利；触发前必须确认碰撞对象属于当前关卡 Actor 且 `ActorCtrl.IsDied == false`。
- 胜利表现只需要开门和 Actor 原地渐隐，不复用耗时较长的 `SuccessAnimCtrl`，也不播放入门移动、关门、彩带或主线完整成功演出。
- Actor 接触成功门后必须立即停止移动；开门和渐隐全部结束后才允许弹出 `DrawGame_GameVictoryPanel`。
- 胜利动画期间 `DrawGame_GamePlayPanel` 不允许操作。
- 原行为：`DrawLevelCtrl` 只有通用 `TryResolve(...)` 和失败判定，没有任何代码提交 `DrawLevelResultType.Success`；`DLevel_001` 至 `DLevel_005` 的成功门 `OnTrigger2DHandle.onEnter` 均为空，实际游玩无法进入既有 Draw 胜利结算。

### 20.2 当前实现与职责边界

- `DrawLevelCtrl.OnTriggerSuccess(Collider2D)` 是成功门的唯一运行时入口，只接受当前 DLevel 内解析到的 Actor；Actor 缺失、不是当前 Actor、已经死亡、已经结算、正在播放成功动画或失败已经进入等待阶段时均拒绝触发。
- 成功入口被接受后立即关闭 Draw 输入，清零 Actor 移动输入并调用 `ActorCtrl.StopMoving()`；`_isPlayingSuccess` 同时阻止 Tick、陷阱、失败区域和 Actor 死亡检查在成功动画期间抢占结果。
- Draw 本地成功动画只使用 `DrawLevelCtrl.successDoorSpine` 的 Inspector 序列化引用，强制播放一次 `door_open` 和 `openDoor` 音效；运行时不再扫描成功门子层级，不引用或调用共享 `SuccessAnimCtrl`，也不播放关门流程。
- `DrawLevelCtrl` 提供 Odin `[Button("AutoBind")]`：仅在非 PlayMode 手动点击时，从当前 DLevel 根节点绑定同物体的 `DrawLineFeature`，并精确绑定 `DrawGame_SuccessDoor/door` 上的 `ActorSpineCtrl`。按钮不使用 `OnValidate`、不创建对象、不访问 DB/Save/RemoteConfig 或运行时单例。
- 2026-07-27 运行态异常已确认：成功门 prefab 同时含有 `effect` 与 `door` 两个 `ActorSpineCtrl`，原有“成功门下找到的第一个 Spine”会拿到不包含 `door_open` 的特效 Skeleton，最终由 Spine 抛出 `Animation not found: door_open`。现实现移除该扫描；漏绑或误绑导致 `ArgumentException` 时只记录错误并走最短等待，不再中断 Actor 渐隐与胜利提交。
- Actor 使用现有 `ActorCtrl.SetSpineAlpha(...)` 和同一 Actor 子层级的 `SpriteRendererGroupAlpha` 在 `0.4` 秒内渐隐，覆盖 Spine、动态皮肤 Sprite 和阴影类 SpriteRenderer。
- 结算等待时间取开门 Spine 动画实际时长与 `0.4` 秒渐隐时长的较大值；二者完成后才调用 `TryResolve(Success)`。`DrawPlayCtrl.OnLevelResolved(...)` 随后同步记录既有 Draw 通关进度并创建 `DrawGame_GameVictoryPanel`。
- 动画开始前派发既有 `GamePlayPanelSwitchBlockEvent(true)`；`DrawGame_GamePlayPanel` 使用已绑定的全屏 `clickBlock` 拦截暂停、Reset、商店和提示按钮。若流程在结算前被销毁、Reset 或提交失败，会派发 false 解除遮罩；正常成功后由 Victory UI 接管，下一关或重玩时 HUD 按既有流程销毁并重新创建。
- `DLevel_001` 至 `DLevel_005` 的 `DrawGame_SuccessDoor.OnTrigger2DHandle.onEnter` 均已显式绑定到各自根节点 `DrawLevelCtrl.OnTriggerSuccess(Collider2D)`，并且根 `DrawLevelCtrl.successDoorSpine` 均已绑定对应门实例的 `door.ActorSpineCtrl`；UnityEvent 模式继续使用动态 `Collider2D` 参数。

### 20.3 数据、资源、事件与平台影响

- 编辑器绑定链路：`DrawLevelCtrl Inspector -> Odin AutoBind -> DrawGame_SuccessDoor/door.ActorSpineCtrl -> successDoorSpine`。
- 运行时链路：`DrawGame_SuccessDoor.OnTrigger2DHandle -> DrawLevelCtrl.OnTriggerSuccess -> successDoorSpine.door_open + Actor fade -> DrawLevelCtrl.TryResolve(Success) -> DrawPlayCtrl.OnLevelResolved -> DB.DrawGame + DrawGame_GameVictoryPanel`。
- UI 门禁链路：`DrawLevelCtrl -> GamePlayPanelSwitchBlockEvent(true) -> DrawGame_GamePlayPanel.clickBlock`。
- 本次没有新增全局事件、存档字段、Luban 配置、资源地址、analytics、广告、IAP、RemoteConfig 或平台条件分支；成功后的存档和 Victory UI 沿用第 16、18 节既有链路。
- 成功动画只在一次有效门触发后运行，最多包含一个 Spine 动画、一个 `0.4` 秒逐帧透明度过渡和一次既有同步 UI Event，不增加常驻 Update 或空闲态分配。

### 20.4 受影响文件与验证证据

- 受影响代码：`Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl.cs`。
- 受影响资源：`Assets/Game/Prefabs/DrawLevel/DLevel_001.prefab` 至 `DLevel_005.prefab`。
- 复用但未修改：`ActorCtrl`、`ActorSpineCtrl`、`SpriteRendererGroupAlpha`、`DrawGame_GamePlayPanel`、`DrawPlayCtrl` 和 `DrawGame_GameVictoryPanel`。
- 编译验证：新增 Inspector 引用、Odin AutoBind 和 Spine 异常保护后执行 `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 `0 error / 1120 warning`；warning 为工程现有项。
- 静态验证：五个 DLevel prefab 均只有一个 `OnTriggerSuccess` 持久调用，目标为本 prefab 根节点 `DrawLevelCtrl`，目标类型为 `DrawLevelCtrl, Assembly-CSharp`；五个 `successDoorSpine` 均引用成功门源 prefab 的 `door.ActorSpineCtrl` 组件；Draw HUD 的 `clickBlock` 非空；`DrawLevelCtrl` 不引用 `SuccessAnimCtrl`；成功入口包含 `ActorCtrl.IsDied` 门禁；运行时代码已不存在 `FindSuccessDoorSpine` 或成功门下首个 Spine 的模糊扫描。
- 格式验证：对本次目标代码、五个 DLevel prefab 和中文记录执行 `git diff --check` 通过。

### 20.5 剩余风险与未验证缺口

- 未验证：Unity PlayMode 中 Actor 实际碰撞成功门、`door_open` 视觉时长、Actor Spine/皮肤/阴影同时渐隐、动画完成后 Victory UI 的帧序，以及死亡与成功门同一物理帧竞争时的最终表现。
- 资源操作要求：当前 `DLevel_001` 至 `DLevel_005` 已完成绑定；未来新增 DLevel 或替换成功门后，必须选中根节点 `DrawLevelCtrl` 并点击 Odin `AutoBind` 后保存 prefab，确认 `successDoorSpine` 显示为 `DrawGame_SuccessDoor/door`。漏绑时胜利仍会继续，但不会播放开门动画。
- 未验证：胜利动画期间暂停、Reset、商店、提示按钮和 Draw 输入的实际拦截；静态证据只能确认事件与 `clickBlock` 绑定。
- 未验证：0/1/2/3 星胜利面板衔接、重玩、下一关、最后一关、存档落盘，以及 Android/iOS 横屏触控、切后台和低帧率动画时序。
- `uncertain`：Spine `door_open` 的最终美术节奏必须在 Unity 运行态确认；代码会等待资源声明的动画时长，不会播放关门。

### 20.6 回滚路径

1. 从 `DLevel_001` 至 `DLevel_005` 的成功门 UnityEvent 删除 `OnTriggerSuccess` 绑定。
2. 从 `DrawLevelCtrl` 删除 Success 区域、成功态门禁、`successDoorSpine` Inspector 引用、Odin AutoBind 和 Actor 渐隐逻辑。
3. 不需要回滚 `DrawPlayCtrl`、胜利面板、DB 结构或配置；若本功能已在测试存档写入通关数据，代码回滚不会自动清除已有 `DB.DrawGame` 进度。

## 21. 2026-07-27 HUD 星级阈值与激励加墨

- 原行为：`DrawLineFeature` 的最大墨水在当前尝试中固定，结算使用 prefab 星级阈值；
  HUD 星标位置固定且广告按钮没有加墨回调。
- 当前行为：`DrawPlayCtrl` 在运行时初始化前把当前 `DrawLevelData.MaxInkLength` 和
  `InkStarStage1/2/3` 同步给 `DrawLineFeature`，结算星级和 HUD 达标显示使用同一关卡配置。
- 广告成功时由 `DrawPlayCtrl` 调用 `AddInk(...)`，一次奖励量取基础
  `DrawInkState.MaxInk`；`DrawLineFeature` 把该值加入当前尝试 `_bonusInk`，所以奖励后
  剩余量严格等于奖励前剩余量加一份 MaxInk。150%/165% 只用于 HUD 宽度动画，不参与
  奖励容量计算。
- `DrawInkState.CurrentCapacity` 表示基础容量加奖励容量；进度填充按当前总容量归一化，
  星级直接按实际剩余 Ink 长度与配置阈值比较。Reset 或关卡重载会清除奖励容量。
- 墨水事件链保持为
  `DrawLineFeature -> DrawInkStateChangedEvent -> DrawGame_GamePlayPanel
  -> DrawGame_InkProcessHandle`；面板只转发墨水快照，具体填充、StageItem 和奖励宽度
  动画由 Handle 负责。
- `RewardAdBtn` 不等待 `StrokeCompleted`：既有
  `DrawLineFeature -> DrawInkStateChangedEvent -> DrawGame_GamePlayPanel` 链首次携带
  `UsedInk > 0` 时，面板直接播放 `DOScale` 缩小隐藏。未新增完成事件，也不参与关卡胜负
  或存档。
  广告资格与 SDK 回调留在 HUD/外围控制器，不进入关卡组件。
- C# 构建结果为 `0 error / 102 warning`；Unity PlayMode、真广告、Android/iOS 横屏和
  实际星标布局仍未验证。
- 完整 UI 行为、prefab 绑定、风险与回滚见
  `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_GamePlayPanel墨水进度与激励加墨功能风险评估.md`。

## 22. 2026-07-28 DrawLevelData 墨水配置与 Stage 绝对量

### 22.1 需求与原行为基线

- 需求：`DrawLineFeature` 的基础最大墨水与 1/2/3 星阈值都必须来自当前
  `DrawLevelData`；`InkStarStage1/2/3` 的单位统一为“剩余 Ink 长度”，不再解释为百分比。
- 原行为：`DrawPlayCtrl` 只注入三个 Stage，`DrawLineFeature.maxInkLength` 继续使用
  DLevel prefab 序列化值；Stage 在画线组件和 HUD 中都会除以 100，结算按剩余比例判断。
- 当前配置证据：`LevelData.xlsx/draw_level` 与生成的 `tbdrawleveldata.json` 已使用
  `maxInkLength=10`、`InkStarStage1/2/3=0/2/5`。本次代码修改没有覆盖正在 Excel 中编辑的
  源工作簿，也没有改写用户已经生成的配置代码或 JSON。

### 22.2 当前行为与数据流

- `DrawPlayCtrl.OpenDrawLevelAsync()` 取得当前 `DrawLevelData` 后，在
  `InitializeRuntime()` 前一次性调用 `IDrawLevelRuntime.ConfigureInk(...)`，传入
  `MaxInkLength` 与三个 Stage。
- `DrawLevelCtrl` 只把四个长度参数转发给同一 prefab 上的 `DrawLineFeature`，不读取配置
  单例；`DrawLineFeature` 将它们写入当前关卡实例，并把 Stage 限制到
  `0..MaxInkLength` 且保持 1/2/3 星阈值单调不降。
- 星级判定直接比较
  `RemainingInk >= InkStarStageN`。以当前 `10 / 0 / 2 / 5` 为例，剩余 Ink 为 0 仍获得
  基础一星，达到 2、5 时分别获得二星、三星。
- HUD 的 StageItem 位置只在表现层按 `InkStarStageN / MaxInkLength` 归一化；当前 2、5
  显示在基础进度条的 20%、50% 位置；达标状态仍直接比较剩余 Ink 绝对量。奖励墨水只扩展
  `CurrentCapacity`，不改变基础 MaxInk 或配置阈值。
- 数据链为
  `DrawLevelData -> DrawPlayCtrl -> IDrawLevelRuntime.ConfigureInk
  -> DrawLevelCtrl -> DrawLineFeature`；状态链继续为
  `DrawLineFeature -> DrawInkStateChangedEvent -> DrawGame_GamePlayPanel
  -> DrawGame_InkProcessHandle`。

### 22.3 受影响文件与验证

- 代码：
  - `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawLevelRuntimeTypes.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`
  - `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_InkProcessHandle.cs`
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` 通过：
  `0 error / 1120 warning`，warning 为工程既有项。
- 静态确认：运行时代码不存在 `InkStarStage / 100`；唯一配置入口同时传递
  `MaxInkLength` 与三个 Stage；HUD 位置按 Stage 除以 `MaxInkLength` 计算。

### 22.4 剩余风险与回滚

- 未验证：Unity PlayMode 中实际可画总长度是否严格为配置 `MaxInkLength`，以及剩余 Ink
  跨过 2、5 时 HUD 星标和 Victory 星数是否同帧一致。
- 未验证：Android/iOS 横屏触控、RewardAD 后剩余 Ink 超过基础 MaxInk、Reset/重载后配置
  重新注入，以及不同 MaxInk/Stage 组合的视觉位置。
- 配置约束：正式关卡需满足 `0 <= Stage1 <= Stage2 <= Stage3 <= MaxInkLength`；运行时会
  安全归一，但错误配置可能造成多个星级在同一剩余量同时达成，仍需 Luban 数据检查。
- 回滚时恢复 `ConfigureInkStarStages(...)` 百分比接口、`DrawLineFeature` 的比例判定和 HUD
  的 `/100` 布局；同时恢复 prefab `maxInkLength` 为权威来源。无需迁移存档。

## 23. 2026-07-28 Draw 首次通关奖励胜利面板

### 23.1 需求与原行为基线

- 需求：`DrawPlayCtrl` 成功结算时，当前 Draw 关卡首次获胜打开
  `DrawGame_GameVictoryPanel_Reward`，已经通过的关卡再次获胜继续打开
  `DrawGame_GameVictoryPanel`；奖励广告与金币领取流程和主线保持一致。
- 原行为：成功结算统一先写入 `DB.DrawGame.PassedLevels`，随后无条件打开普通 Draw 胜利
  面板，因此界面创建时已无法区分本次是否首次获胜。

### 23.2 当前行为与数据、资源、事件流

- `DrawPlayCtrl.ShowVictoryPanel()` 在 `RecordVictoryProgress()` 前读取
  `DB.DrawGame.PassedLevels` 并缓存 `isFirstVictory`，判定顺序与主线
  `PlayCtrl_Success` 一致；存档写入、下一关解锁和当前关推进仍由原方法完成。
- 首次获胜打开资源
  `Assets/Game/UI/DrawGame/VictoryPanel/Resources/DrawGame_GameVictoryPanel_Reward.prefab`；
  重复获胜仍打开 `DrawGame_GameVictoryPanel.prefab`。
- `DrawGame_GameVictoryPanel_Reward` 继承 Draw 专用胜利面板，下一关继续派发既有
  `EnterNextLevelEvent`，重玩继续派发 `GameReplayEvent`，由活动中的 `DrawPlayCtrl`
  接收；不会切回主线 `PlayCtrl`。
- 奖励链复用主线语义：
  `RewardAdHelper(GameVictory) -> HorizontalRandomAdRewardCtrl -> Bag.AddTool(Coin,
  RewardType.Ad) -> CommonUIRewardAnimCtrl -> PropBlock.UpdateData`。广告失败时恢复转盘，
  成功后同一面板生命周期只领取一次并置灰按钮。
- 新 prefab 来自主线奖励资源，Draw 脚本会禁用其中的主线分享区域、免广告入口和引导
  遮罩，避免读取 `PlayCtrl.Instance`、`DB.GameData.HasSharedLevel`、触发主线
  Postcard 引导或留下无 Draw 处理器的按钮；原 Postcard 星组已替换为
  `DrawGame_VictoryStarGroup`。
- 首次奖励页与重复胜利页均通过 Draw 胜利面板基类展示 Draw 星级动画。
- 未新增全局事件、存档字段、配置字段或平台条件分支；首次判定只读取既有
  `DB.DrawGame.PassedLevels`。

### 23.3 受影响文件与验证证据

- 代码：
  - `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
  - `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel.cs`
  - `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel_Reward.cs`
- 资源：
  - `Assets/Game/UI/DrawGame/VictoryPanel/Resources/DrawGame_GameVictoryPanel_Reward.prefab`
- 编译验证：执行单进程
  `dotnet msbuild Assembly-CSharp.csproj /t:Build /p:BuildProjectReferences=false
  /p:UseSharedCompilation=false /m:1` 通过，结果为 0 error；目标脚本未产生新增 warning。
- 静态验证：Reward prefab 根组件 GUID 指向
  `DrawGame_GameVictoryPanel_Reward.cs.meta`；资源名与 `OpenUI` 名一致；首次判定发生在
  `PassedLevels` 写入前；普通/奖励面板都使用 Draw 的下一关与重玩事件。

### 23.4 剩余风险与回滚

- 未验证：Unity PlayMode 中首次/重复获胜的实际资源加载、Spine 和按钮时序、广告成功/
  失败/取消、金币到账、下一关、重玩、最后一关，以及 Android/iOS 横屏与切后台恢复。
- 当前首次奖励页按用户提供的资源保留主线奖励布局，但主动隐藏主线分享区域；原 Postcard
  星组已切换并绑定 `DrawGame_VictoryStarGroup`，仍需在 PlayMode 验证布局和逐星动画。
- 回滚时删除 `DrawGame_GameVictoryPanel_Reward.cs` 及其 meta，把 Reward prefab 根脚本
  恢复为原组件，并将 `DrawPlayCtrl.ShowVictoryPanel()` 恢复为无条件创建普通 Draw 胜利
  面板；无需迁移或清理存档。

## 24. 2026-07-28 Draw 星级存档与主线胜利链路对齐

### 24.1 需求与原行为基线

- 需求：Draw 胜利星的写入、VictoryPanel 消费和选关读取保持与主线
  `PlayCtrl_Success -> PostcardManager -> GameVictoryPanel -> LevelChoiceItem` 相同的职责顺序，
  但数据全部保留在 `DB.DrawGame`。
- 修改前 `DrawPlayCtrl` 只把 `_drawLevelRuntime.InkState.StarCount` 直接传给
  `DrawGame_GameVictoryPanel`，不会写入永久星记录；Draw 选关误读空的
  `LevelWaitingAnimStar`，因此返回选关后没有星。
- 主线证据表明 `LevelCollectedStar` 是永久数据，`LevelWaitingAnimStar` 只是本轮新增星的
  一次性动画计数；选关读取永久数据，VictoryPanel 消费临时计数。

### 24.2 当前实现与数据流

```text
DrawLevelCtrl.TryResolve(Success)
    -> DrawPlayCtrl.ShowVictoryPanel
    -> InkState.StarCount 限制到 0..3
    -> TryRecordVictoryStars(DrawLevelData.Id)
       -> DB.DrawGame.LevelCollectedStar：只提高历史最高星
       -> DB.DrawGame.LevelWaitingAnimStar：累加本轮新增差值
       -> DB.SaveDrawGame；保存失败回滚本次两份星数据
    -> DrawGame_GameVictoryPanel.Create / Reward.Create
       -> Configure 读取永久星并一次性移除等待动画计数
       -> DB.SaveDrawGame
       -> DrawGame_VictoryStarGroup：旧星静态，新星逐颗动画
    -> RecordVictoryProgress
       -> PassedLevels / UnlockLevels / CurrentLevel
    -> DrawGame_ChoiceLevelStarGroupCtrl
       -> 只读 LevelCollectedStar
```

- 同星或低星重玩不会降低永久星，也不会新增等待动画；高星重玩只对提高的差值播放动画。
- 首次奖励面板已绑定 Draw 星组；基类 `Configure` 读取永久星、消费等待动画数据并在首次
  奖励页播放新增星，永久星记录和返回选关后的展示保持不变。
- `levelCollectedStar` 为新增的 Draw 独立存档字段，键使用 `DrawLevelData.Id`；
  `levelWaitingAnimStar` 保持临时表现语义。两者都不访问 `DB.Postcard`、`PostcardManager`
  或 Bag，也未新增全局事件和逐帧逻辑。

### 24.3 影响文件与验证证据

- `Assets/Game/DB/DrawGame.cs`
- `Assets/Game/DB/DrawGame_Partial.cs`
- `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
- `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel.cs`
- `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel_Reward.cs`
- `Assets/Game/UI/GameVictory/Scripts/DrawGame_VictoryStarGroup.cs`
- `Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_ChoiceLevelStarGroupCtrl.cs`
- 临时把尚未被 Unity 工程刷新纳入的 `DrawGame_LevelChoiceItem.Generated.cs` 加入
  `Assembly-CSharp.csproj` 后执行单进程 `dotnet build`：0 error / 103 warning；
  warning 未指向本次 Draw 文件，验证后已移除临时工程项。
- 静态确认顺序为“记录星 -> 创建并配置 VictoryPanel -> 记录通关进度”，选关脚本不再读取
  `LevelWaitingAnimStar`，Victory 星组能够区分静态旧星和本轮动画星。

### 24.4 风险、未验证项与回滚

- 旧存档没有 `levelCollectedStar` 时由 `EnsureValidCollections()` 补空字典；历史已通关 Draw
  关卡无法推导当时星级，会保持 0 星，直到再次获胜。
- 尚未执行 Unity PlayMode 的首次 1/2/3 星、同星重玩、低星重玩、高星覆盖、普通/
  奖励 VictoryPanel、返回选关、保存失败注入，以及 Android/iOS 横屏验证。
- 回滚需同时移除 `levelCollectedStar` 字段与属性、恢复 `DrawPlayCtrl` 直接传本次星数、
  恢复 Victory 星组单参数接口，并把选关数据源恢复到回滚目标；旧代码会忽略已保存的新字段。

## 25. 2026-07-28 Draw 首次奖励页切换专用胜利星组

### 25.1 需求与原行为基线

- 需求：`DrawGame_GameVictoryPanel_Reward` 不再使用或隐藏主线 Postcard 星组，改用
  `DrawGame_VictoryStarGroup`。
- 原 Reward 脚本通过 `RequiresVictoryStarGroup => false` 声明不需要 Draw 星组，并持有
  `Postcard_VictoryStarGroupCtrl` 引用在打开时关闭；即使基类已缓存 Draw 星级，也只消费
  等待动画数，不在首次奖励页展示。

### 25.2 修改后行为与数据流

- Reward prefab 中的星组实例使用
  `Assets/Game/UI/DrawGame/Prefabs/DrawGame_VictoryStarGroup.prefab`，根脚本的
  `victoryStarGroup` 字段绑定该实例上的 `DrawGame_VictoryStarGroup` 组件。
- Reward 子类不再关闭 Draw 星组或跳过必需引用校验，直接继承普通 Draw 胜利面板的星级
  刷新链：`Configure -> DB.DrawGame.LevelCollectedStar / LevelWaitingAnimStar ->
  DrawGame_VictoryStarGroup.Refresh`。
- 主线分享区、免广告入口和 Postcard 引导遮罩仍保持关闭；金币奖励、下一关、重玩、存档、
  配置、资源加载、事件和 analytics 流程不变。

### 25.3 影响文件与验证

- `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel.cs`
- `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel_Reward.cs`
- `Assets/Game/UI/DrawGame/VictoryPanel/Resources/DrawGame_GameVictoryPanel_Reward.prefab`
- 静态确认 prefab 的 `victoryStarGroup` 非空，指向 Draw 星组源 prefab 中脚本 GUID
  `52d96a2dea8140ae847820df14165353` 的组件。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly
  /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为
  0 error / 102 个工程既有 warning。
- 目标文件 `git diff --check` 通过；静态扫描确认 Reward 脚本不再引用
  `Postcard_VictoryStarGroupCtrl` 或覆写 `RequiresVictoryStarGroup`。

### 25.4 剩余风险与回滚

- 尚未在 Unity PlayMode 验证首次 0/1/2/3 星、旧星加新星、逐星音效、按钮解锁时序、
  奖励广告期间隐藏/恢复及不同横屏分辨率布局；Android/iOS 真机尚未验证。
- 回滚时恢复 Reward 子类的 `RequiresVictoryStarGroup => false`，移除 prefab 的 Draw 星组
  绑定并恢复原 Postcard 星组禁用引用；不涉及存档迁移。

## 26. 2026-07-28 Draw Replay 重建 HUD

### 26.1 需求与原行为基线

- 用户要求 Draw Replay 与 DLevel 一起重建 HUD，并删除为复用旧 HUD 增加的恢复代码。
- 原流程保留 `DrawGame_GamePlayPanel`，只销毁 DLevel；重载后通过
  `ResetForNewAttempt(...)` 重置奖励、倒计时、进度和关卡显示。

### 26.2 当前行为与边界

- `ReloadCurrentLevelAsync()` 调用 `CleanupLevelRuntime()`，统一解除旧运行时结算回调、
  关闭输入、销毁旧 HUD 和旧 DLevel。
- `CleanupLevelRuntime()` 和 `OpenDrawLevelAsync()` 不再接受保留 HUD 参数；首次进关、
  手动 Reset、失败重试和胜利 Replay 都调用 `TryRefreshGamePlayPanel()` 创建新 HUD。
- 新 HUD 在 `OnOpen()` 清理自身局部状态，随后由控制器调用 `SetLevel(...)` 和
  `SetInkState(...)` 绑定当前配置与新 DLevel 的初始墨水快照。
- 已删除 `ResetForNewAttempt(...)`，未新增接口、事件、配置、存档或共享抽象。

### 26.3 验证、风险与回滚

- `git diff --check` 通过。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false
  /p:BuildInParallel=false` 通过，结果为 `0 error / 102` 个工程既有 warning。
- 静态确认重载调用链为
  `CleanupLevelRuntime -> OpenDrawLevelAsync -> TryRefreshGamePlayPanel`，旧复用参数和
  `ResetForNewAttempt` 无引用。
- 未验证：Unity PlayMode 的连续 Replay、手动/失败 Reset、旧 HUD 渐黑到新 HUD 渐显的
  视觉衔接、RewardAD 未返回竞态，以及 Android/iOS 横屏实例化峰值。
- 回滚时恢复 HUD 复用分支和 `ResetForNewAttempt(...)`；不涉及 prefab、配置或存档迁移。

## 27. 2026-07-28 Draw Reset 黑幕交接

### 27.1 事实与直接结论

- `OnTriggerTrapEvent` 在单个 `DrawLevelCtrl` 中只产生一次失败重置：
  `_isWaitingForTrapFailure` 拦截重复事件，延迟结束后由 `TryResolve()` 先写入
  `_isResolved=true`，再派发 `DrawGameResetEvent(Failed)`。
- 双黑屏来自 HUD 重建后的遮罩生命周期断层，不是陷阱事件或 Reset 重复提交。旧 HUD
  BlackMask 渐黑后被销毁；新 HUD 原本在异步加载完成后才创建，随后又从全黑淡出。

### 27.2 修改后时序

```text
OnTriggerTrapEvent
  -> Actor.Die + 1.5 秒失败演出
  -> DrawGameResetEvent(Failed)
  -> 旧 HUD 渐黑 + 0.5 秒等待
  -> 销毁旧 DLevel/HUD
  -> 同帧创建新 HUD 并立即全黑
  -> 异步加载、实例化和初始化新 DLevel
  -> GamePlayPanelFadeInEvent
  -> 新 HUD 单次淡出
```

- 新增的 `ShowBlackMaskImmediately()` 是 Draw HUD 的局部过渡 API，只接管新 HUD 黑幕，
  不复制或恢复旧 HUD 状态。
- 正常进关继续使用 `EnterLevelLoading`，不启用 Reset 黑幕；失败、手动 Reset 和胜利
  Replay 共用同一个明确的 `showResetMask=true` 重载入口。

### 27.3 验证、风险与回滚

- `git diff --check` 通过；C# 单进程构建为 `0 error / 102` 个工程既有 warning。
- 未执行 Unity PlayMode 与 Android/iOS 真机验证；慢加载期间遮罩连续性、连续陷阱事件、
  切后台以及 HUD/DLevel 同帧实例化峰值仍为未验证风险。
- 回滚时把 HUD 创建移回 DLevel 初始化之后，并删除 `ShowBlackMaskImmediately()`；不涉及
  prefab、配置或存档迁移。

## 28. 2026-07-28 DrawLineFeature 墨水配置来源开关

### 28.1 需求与原行为基线

- 用户要求 `DrawLineFeature` 增加 `isConfig`，默认值为 `false`。
- 默认应使用当前 DLevel prefab Inspector 中的 `maxInkLength` 和 `stage`；只有
  `isConfig=true` 时才允许 `DrawLevelData` 覆盖这两项。
- 修改前 `DrawPlayCtrl` 每次进关都会调用 `ConfigureInk(...)`，因此 Inspector 中的最大
  墨水和星级阶段会被当前 `DrawLevelData` 无条件覆盖。

### 28.2 修改后行为与数据流

- `DrawLineFeature.isConfig=false` 时，`ConfigureInk(...)` 直接返回，运行时继续使用 prefab
  Inspector 的 `maxInkLength` 与 `stage`。
- `isConfig=true` 时，仍沿用既有
  `DrawLevelData -> DrawPlayCtrl -> DrawLevelCtrl -> DrawLineFeature.ConfigureInk`
  调用链，覆盖当前实例的最大墨水和三个阶段值。
- Inspector 字段由 `remainingInkStarThresholds` 更名为 `stage`；使用
  `FormerlySerializedAs` 兼容未同步资源中的旧字段名。
- `tbdrawleveldata.json` 的 DLevel_001 至 DLevel_005 配置明确使用
  `maxInkLength=15`、`InkStarStage1/2/3=0/2/5`，证明 Stage 单位是 DrawSpace
  “剩余墨水绝对长度”，不是 0～1 比例。DLevel_001 至 DLevel_006 prefab 已将旧
  `0.2/0.4/0.6` 校正为 `0/2/5`，各关 Inspector 的 `maxInkLength` 保持原值。
- 本次未修改配置表、生成代码、DB、存档、资源地址、事件或平台分支。

### 28.3 验证、风险与回滚

- `git diff --check` 通过；C# 单进程构建通过，结果为 `0 error / 102 warning`，现有 warning
  未指向本次修改。
- 静态确认 DLevel_001 至 DLevel_006 均显式序列化 `isConfig=0` 和
  `stage=0/2/5`，并保留各自 `maxInkLength`。
- 当时的 `uncertain` 风险：Draw HUD 的阶段节点仍由 `DrawLevelData` 配置，Inspector
  `stage` 与表配置不一致时可能造成显示偏差。该风险已由 28.4 的唯一来源改造消除；
  Unity PlayMode、Android/iOS 横屏仍未验证。
- 回滚时删除 `isConfig` 判断与 `FormerlySerializedAs`，恢复
  `remainingInkStarThresholds` 字段名、Prefab 原 Stage 值及 `ConfigureInk(...)`
  的无条件覆盖；不涉及存档迁移。

### 28.4 HUD 配置来源唯一化

- 已消除 28.3 中记录的 HUD 双配置来源：`DrawInkState` 现在携带
  `DrawLineFeature` 最终采用的 `MaxInk + Stage`，HUD 不再读取 `DrawLevelData`。
- `isConfig=false` 时，DLevel_004 的二星节点按关卡内配置计算为 `2 / 30`；`isConfig=true`
  时，`DrawLineFeature` 先应用表配置，HUD 再消费覆盖后的同一快照。
- 运行时唯一链路为
  `Inspector/DrawLevelData -> DrawLineFeature -> DrawInkState -> GameplayPanel`；
  `isConfig` 只在 `DrawLineFeature.ConfigureInk(...)` 判断一次。
- C# 单进程构建通过（`0 error / 103 warning`）；Unity PlayMode 和 Android/iOS 横屏仍待验证。

## 29. 2026-07-29 画线长度与质量关联及 Mass 配置归位

### 29.1 需求、范围与原行为基线

- 用户要求玩家画线长度与物理重量产生关联，确认长度质量规则默认开启，并要求把满长度
  `Mass` 归入 `DrawLineFeature` 配置，方便在关卡根组件统一查看和调整。
- 修改前 `DrawLineFeature` 只累计 DrawSpace 折线长度；一笔结束后直接启用模板
  `Rigidbody2D`，不根据 `_activeStrokeLength` 修改质量。
- 配置归位前，质量来源位于 `DrawGame_DrawLine.prefab` 的 `Rigidbody2D`：基础
  `mass=20`，DLevel_004 将模板实例覆盖为 `50`，DLevel_005 覆盖为 `5`。
- 影响范围是共享 `DrawLineFeature` 生成的每一笔运行时线条，因此 DLevel_001 至
  DLevel_007 的画线物理手感都属于回归范围。当前工作区部分关卡 prefab 存在其他任务改动；
  本次只在各自 `DrawLineFeature` 数据块追加 `strokeMass`，未改写或归并其它字段。

### 29.2 修改后行为与公式

- `scaleMassByLength` 默认 `true`；每一笔有效线条在 `EdgeCollider2D` 点集完成、物理模拟
  开启前单独计算质量。
- 默认公式为：

```text
normalizedLength = Clamp01(strokeLength / maxInkLength)
curveValue = Clamp01(massByLengthCurve(normalizedLength))
massRatio = Lerp(minimumMassRatio, 1, curveValue)
runtimeMass = strokeMass * massRatio
```

- `strokeMass` 是当前关卡满基础墨水长度线条的质量，也是关闭长度缩放时所有线条直接使用
  的质量。DLevel_001/002/003/006/007 显式配置为 `20`，DLevel_004 为 `50`，
  DLevel_005 为 `5`。
- `minimumMassRatio` 默认 `0.25`，`massByLengthCurve` 默认线性；极短有效线最低为
  `strokeMass` 的 25%，达到或超过基础 `maxInkLength` 时固定使用 `strokeMass`。
- 参考长度使用基础 `maxInkLength`，不包含激励广告加入的 `_bonusInk`，因此奖励墨水不会
  让同样绝对长度的线条变轻。多笔模式下每一笔按自身 `StrokeLength` 独立计算。
- 每笔完成时都把 `DrawLineFeature.strokeMass` 计算结果写入运行时刚体，并显式关闭
  `useAutoMass`；画线模板 `Rigidbody2D.mass` 不再是运行时质量来源。`gravityScale`、
  阻力、约束和碰撞检测配置仍保留 prefab 原值。
- 本需求关联的是 `Rigidbody2D.mass`。Unity 重力加速度不会因为质量增加而加快；可感知
  差异主要来自碰撞响应、冲量影响和转动表现。若未来产品目标改为“长线下落更快”，应另行
  评审 `gravityScale` 曲线，不应把两种效果混为同一配置。

### 29.3 数据、资源、事件与平台边界

- 数据仅来自当前 `DrawLineFeature.strokeMass`、当前笔画长度和 `maxInkLength`；不读取
  DB、存档、Luban、RemoteConfig 或平台数据。
- 未新增资源、全局事件、UI、analytics、广告策略或 Android/iOS 分支。
- 计算仅在一笔完成时执行一次，不增加逐帧逻辑或持续 GC；移动端额外开销可忽略，但物理
  手感仍需在 Android/iOS 横屏设备验证。

### 29.4 验证证据、风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore --nologo` 通过：
  `0 error / 103 warning`；warning 为工程现有告警，输出未发现指向本次字段或方法的错误。
- 代码、文档及 DLevel_001 至 DLevel_006 的 `git diff --check` 通过；DLevel_007 的完整
  diff 检查命中该文件其它并行改动中已有的空值 YAML 行尾空格，本次新增的
  `strokeMass: 20` 行本身无行尾空格，未擅自清理并行改动。
- 静态确认长度仍由固定间距采样累加；质量只在有效笔结束、`simulated=true` 前写入。
- 静态确认 DLevel_001 至 DLevel_007 均显式序列化 `strokeMass`，值依次为
  `20/20/20/50/5/20/20`；运行时代码不再读取克隆刚体原有 mass 作为基准。
- 未验证：Unity PlayMode 中短/中/满长度的实际质量、碰撞与转动手感；DLevel_001 至
  DLevel_007 的解法回归；多笔关卡总质量；Android/iOS 横屏物理一致性。
- 主要风险：默认开启会有意降低短线质量，可能改变旧关卡中压住物体、抵挡箭矢、推动机关
  或支撑角色的既有解法。上线前必须逐关验证关键短线解法，必要时通过 Inspector 调整
  `strokeMass`、`minimumMassRatio`、曲线，或对特定关卡关闭 `scaleMassByLength`。
- 快速回滚可在目标 DLevel 的 `DrawLineFeature` 关闭 `scaleMassByLength`，此时运行时直接
  使用该组件的 `strokeMass`；若需恢复模板质量来源，则删除 `strokeMass` 和
  `ApplyConfiguredStrokeMass(...)`，并恢复从克隆刚体读取 mass 的旧计算。

## 30. 2026-07-29：已完成线段只读访问边界

### 30.1 背景与行为

- DLevel_007 的投掷物就是玩家第一笔有效线段，局部 Handler 需要在
  `StrokeCompleted` 同步回调后取得该笔运行时对象。
- `DrawLineFeature.TryGetCompletedStrokeObject(int strokeIndex, out GameObject)` 按从 1
  开始的笔序返回 `_completedStrokeObjects` 中的当前对象；越界或对象已销毁时返回 false。
- API 只提供引用，不转移所有权。Reset、退出和销毁仍由 DrawLineFeature 统一清理，调用方
  不得把该对象保存为跨尝试或永久引用。
- 未修改 `DrawStrokeResult`、线段创建顺序、物理激活、墨水、星级、事件或其他 DLevel 行为。

### 30.2 风险、验证与回滚

- 风险集中在局部调用方持有过期 Unity 对象；DLevel7 通过 Attempt 版本、Reset/禁用清理和
  Unity null 检查限制生命周期。
- 聚焦 Assembly-CSharp 构建通过：`0 error / 103 warning`；静态确认只有 DLevel7Ctrl
  调用该 API。Unity PlayMode 与 Android/iOS 横屏未验证。
- 回滚时删除该方法与 DLevel7 调用即可；`_completedStrokeObjects` 及既有清理逻辑无需调整。

## 31. 2026-07-29：运行时线段 Transform 中心归位

### 31.1 需求与原行为基线

- 用户指出生成线段的 Transform 中心应位于线段几何中心；当前线段进入物理后出现明显偏转，
  不能只通过提高 Linear/Angular Damping 掩盖。
- 修改前 `RuntimeStroke_*` 始终位于 `RuntimeLines/DrawSpace` 局部原点，LineRenderer 和
  EdgeCollider2D 点集直接保存 DrawSpace 局部坐标。线段几何通常远离 Transform 原点，
  因而刚体旋转时会呈现围绕远处支点画弧、整体“歪掉”的视觉结果。
- DLevel7 已在投掷前局部执行过同类包围盒中心归位，证明该问题此前只在单关投掷路径得到
  补偿；普通 Draw 线段仍没有共享修正。

### 31.2 修改后行为与调用顺序

- `DrawLineFeature.CompleteActiveStroke()` 在有效性检查通过后、创建最终 EdgeCollider2D 和
  开启 Rigidbody2D 模拟前调用 `RecenterActiveStrokeGeometry()`。
- 中心采用折线全部采样点的二维包围盒中心。方法先记录该中心的世界坐标，再把
  `_activePoints` 与 LineRenderer 局部点统一减去中心，最后把 RuntimeStroke Transform
  移到原中心世界坐标，因此线段的世界形状和落笔位置保持不变。
- EdgeCollider2D 随后直接使用已经中心化的 `_activePoints`；Dynamic、重力、质量随线长
  变化、阻力、碰撞层、墨水和事件顺序保持原行为。
- DLevel7 的局部归位暂时保留；共享归位后其再次计算得到接近零的局部中心，不改变投掷位置。
  未在本次顺带重构已验收关卡代码。

### 31.3 数据、资源、性能与平台边界

- 不修改 prefab、DB、存档、Luban、资源地址、UI、全局事件、analytics、广告或 SDK。
- 只在每笔完成时遍历一次已有采样点，不新增 Update/FixedUpdate，不创建临时点数组；
  EdgeCollider2D 原有 `ToArray()` 分配保持不变。
- Android/iOS 共用相同 Unity 2D Transform/Rigidbody2D 流程，无平台条件分支。

### 31.4 验证、风险与回滚

- 静态确认归位发生在 `simulated=false` 期间，并早于 EdgeCollider2D 点集写入和
  `ActivateRuntimeStrokeRigidbody()`；不会在活跃物理模拟中瞬移刚体。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal
  /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false
  /p:BuildInParallel=false` 通过，结果为 `0 error / 103 warning`；warning 为工程现有项。
- 目标代码与中文文档的 `git diff --check` 通过。
- 需要在 Unity PlayMode 验证横线、竖线、斜线、曲线、多笔线段的世界形状不跳动，
  Transform gizmo 位于包围盒中心，并观察落地、旋转、接触、重开与 DLevel7 投掷行为。
- Android/iOS 横屏真机物理表现尚未验证。
- 主要风险是既有关卡若错误依赖 DrawSpace 原点作为旋转支点，物理手感会有意改变；这类
  依赖与当前“中心必须在线段中心”的明确需求冲突，仍需逐关回归关键解法。
- 回滚时删除 `RecenterActiveStrokeGeometry()` 及完成线段时的单次调用即可；不涉及资源、
  配置或存档迁移。

## 32. 2026-07-29：可选笔画间厚碰撞与乱画合并

### 32.1 需求、原行为与风险基线

- 用户要求不同笔画之间能够真实碰撞，并进一步要求同一笔乱画、自交和重复覆盖时表现可控。
- 原实现只生成 EdgeCollider2D。该碰撞形状没有正面积，能够与圆形或多边形物体接触，但
  两个笔画都只有边界时不能可靠产生线线碰撞。
- 直接为每个原始采样间隔生成独立碰撞体会让 20 单位墨水、0.05 采样间隔的单笔达到约
  400 段；多笔和乱画会放大移动端物理负担及重复接触风险。

### 32.2 修改后行为与算法边界

- `DrawLineFeature` 新增 `enableStrokeToStrokeCollision`，默认关闭；关闭时继续沿用原
  EdgeCollider2D，不改变既有关卡。DLevel_010 显式开启。
- 开启后，线段中心归位完成且 Rigidbody2D 仍为 `simulated=false` 时执行：
  1. 使用 RDP 算法简化现有采样点，容差取 `max(sampleSpacing, colliderRadius) * 0.5`。
  2. 简化结果仍过多时按顺序均匀限制为最多 33 点、32 段。
  3. 每段生成一个横向 CapsuleCollider2D 子节点，直径为 `colliderRadius * 2`，长度包含
     两端半径以避免相邻段缝隙。
  4. 所有 Capsule 使用 `CompositeOperation.Merge`，由根节点 Polygon
     CompositeCollider2D 以 Manual 模式一次性 `GenerateGeometry()`。
- 同一笔的胶囊共用同一个根 Rigidbody2D，并合并为一个有面积形状，不发生自碰撞；
  自交和重复覆盖区域做 OR 合并。不同笔各自拥有独立根刚体，因此可以互撞。
- 简化只影响物理轮廓，不修改 LineRenderer 可见笔迹、墨水长度、质量随线长计算、星级或
  StrokeCompleted 顺序。Composite 生成完成后才开启 Rigidbody2D 模拟。

### 32.3 性能、数据与系统边界

- 每笔最多 32 个源 Capsule，Composite 只生成一次；不在 Update/FixedUpdate 重建。
- 仅在笔画完成时产生简化标记数组、点列表和最多 32 个子节点。Reset/退出销毁
  RuntimeStroke 根节点时，子碰撞体随父节点统一清理。
- 不访问 DB、存档、Luban、RemoteConfig，不新增 UI、全局事件、analytics、广告、SDK、
  资源地址或平台分支。
- Android/iOS 共用 Unity Physics2D；乱画、多笔和低端设备物理峰值仍需真机验证。

### 32.4 验证、风险与回滚

- 静态确认旧关卡未序列化新开关，默认保持 EdgeCollider2D；DLevel_010 显式为 true。
- 静态确认 Composite 使用 Polygons 而不是 Outlines，来源 Capsule 使用 Merge，并在
  Dynamic 模拟开启前完成几何生成。
- C# 单进程构建第一次在 124 秒超时且未返回诊断；终止本次残留构建进程后重试成功，
  结果为 `0 error / 314 warning`。warning 为当前工程现有项。
- 代码与中文文档 `git diff --check` 通过。把新增 DLevel_010 prefab 一并检查时会命中该
  文件原有空 YAML 值的尾随空格；本次新增 `enableStrokeToStrokeCollision: 1` 行无尾随空格，
  未擅自格式化整个 prefab。
- Unity PlayMode 仍需验证：两笔交叉、堆叠、同笔自交/反复涂抹、极短笔画、32 段上限、
  外部球体/角色接触、失败/Replay、暂停恢复和完整通关。
- 主要风险：32 段上限会让极复杂笔迹的碰撞轮廓成为近似；Polygon Composite 比原
  EdgeCollider2D 更昂贵；高速 Dynamic 对 Dynamic 接触仍可能受 Physics2D 连续碰撞限制。
- 单关回滚可关闭 `enableStrokeToStrokeCollision`；完整回滚可删除 Composite 构建与简化
  方法，恢复完成笔画时直接创建 EdgeCollider2D，不涉及存档或配置迁移。
## 33. DrawGame 关卡开始/结束打点补充（2026-07-29）

### 33.1 需求与边界

- 在 DrawGame 关卡流程中补充 `level_start` 与 `level_end`，生命周期位置与主线关卡保持一致。
- 本次仅覆盖 DrawGame 进入、失败重开、胜利后重玩、成功结算和退出流程；`PencilShop` 不在本次范围内。
- 关卡统计键统一使用 `DrawLevelData.Id`。持续时长字段的持久化单位与累计语义尚未确认，因此本次不写入 `DrawGame.levelPlayDuration`。

### 33.2 涉及文件与系统

- `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
  - 维护当前 DrawGame 打点上下文和单次尝试时长。
  - 在对应生命周期位置更新游玩、成功、重试统计并发送开始/结束事件。
- `Assets/Game/Scripts/SDK/Services/Events/GameSDKService.Game.cs`
  - 新增 DrawGame 专用的 `level_start`、`level_end` 上报入口。
  - 不复用主线当前关卡字段，避免 DrawGame 流程污染主线打点上下文。
- `Assets/Game/DB/DrawGame.cs`
  - 明确游玩、成功、重试统计字典以 `DrawLevelData.Id` 为键。
- `Assets/Game/DB/DrawGame_Partial.cs`
  - 提供上述统计字典的只读访问入口，供 DrawGame 流程读取累计次数。

### 33.3 行为前后

修改前：

- DrawGame 流程没有发送 `level_start`、`level_end`。
- `levelPlayCount`、`levelSuccessCount`、`levelReviveCount` 已存在，但没有在当前 DrawGame 关卡生命周期中完成对应统计。

修改后：

- 新进关或进入下一关：关卡数据确认后，游玩次数加一，并在资源加载前发送 `level_start`。
- 失败或手动 Reset：重试次数加一并发送 `level_end(fail)`，随后清零本次开始事件对应的打点时长与尝试级提示显示状态、游玩次数加一、发送新的 `level_start` 并执行关卡重建；revive 与当前关已获取提示资格继续保留。
- 胜利后重玩：成功结算阶段已发送成功结束事件并清理周期数据；只有 VictoryPanel 的 Replay 按钮属于业务 Replay，它增加一次游玩次数并在重新加载前发送新的 `level_start`。
- 成功结算：成功次数加一，在胜利弹窗流程开始前发送 `level_end(result=success)`，随后清理当前累计时长与 revive。
- 退出 DrawGame：在流程停用时发送 `level_end(result=exit)`。

### 33.4 数据与事件流

- `level`：自 2026-08-06 起改为主线与 DrawGame 历史累计成功次数之和；开始和退出按总和加一，成功按已写入本次成功后的总和上报。
- `level_name`：使用当前 `DrawLevelData.Path`。
- `item_category`：固定为 `draw`。
- `play_count`：当前关卡每次发送开始事件的持久化累计次数；首次进入、每次 Reset 和 VictoryPanel Replay 均增加一次。
- `start_type`：所有开始事件都按当前 Draw 关卡是否存在于 `PassedLevels` 判定，已通关为 `replay`，未通关为 `play`；不直接表示本次是否点击了 VictoryPanel Replay。
- `duration`、`level_duration`、`total_duration`：当前 `level_start` 配对周期的运行时长，单位按主线实际实现采用毫秒；Reset 的失败结束事件发送后清零，再为新的开始事件计时。
- `revive`：本关自上次成功清理后的失败或手动 Reset 累计次数。
- `result`：成功、失败、退出沿用主线枚举映射；Reset 发送失败结束事件。
- DrawGame 成功在 `level_end(success)` 后发送 `level_end_success_N`；`N` 使用主线与 DrawGame
  累计成功次数之和，事件参数 `level` 使用当前 `DrawLevelData.Id`。

### 33.5 验证证据

- 已通过 CodeGraph 核对主线打点顺序、DrawGame 生命周期入口和新增调用路径。
- 已执行目标文件 `git diff --check`，未发现空白符错误。
- 完整 `Assembly-CSharp.csproj` 构建在限定时间内未完成；缩小范围构建被工作区缺失的 `Temp/bin/Debug` 引用程序集阻断，未取得有效编译结论。
- 尚未在 Unity PlayMode、真实 SDK 后台、Android 和 iOS 设备上验证事件次数、字段值和顺序。

### 33.6 风险、待确认与回滚

- `DrawGame.levelPlayDuration` 的单位、是否累计和写入时机仍为 `uncertain`；确认前不持久化该字段，避免污染已有存档数据。
- 关卡资源加载失败时，已发送的开始事件会在流程停用或下一次成功进入前按退出补结束；需要 SDK 后台验证这一异常路径是否符合统计口径。
- DB 保存失败时当前只记录错误，内存中的统计增量不会自动回滚，与现有保存模式一致，但仍需异常场景验证。
- 回滚时可移除 `DrawPlayCtrl` 的打点调用与上下文、SDK 的 DrawGame 专用事件入口和 DB 只读访问属性；本次未新增存档字段，不需要数据迁移。

## 34. 2026-07-29 DrawLineFeature 线宽同步

### 34.1 需求、基线与影响范围

- 需求：`DrawLineFeature.drawingLineWidth` 必须同步到实际生成的 `LineRenderer`。
- 修改前：只有未绑定 `lineTemplate`、运行时临时添加 `LineRenderer` 时才读取
  `drawingLineWidth`；绑定模板时直接复制模板宽度。当前 DLevel_001～DLevel_010 均绑定
  `DrawGame_DrawLine.prefab`，因此各关 Inspector 中的 `drawingLineWidth` 不控制实际线宽。
- 涉及脚本：`Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`。
- 受影响资源：所有绑定 `DrawLineFeature` 的 DrawLevel prefab 在后续新建笔画时读取各自
  已序列化的 `drawingLineWidth`；本次不修改关卡 prefab、画线模板、Material 或 Shader。

### 34.2 修改后行为与数据流

- `ApplyDrawingStyle(LineRenderer)` 统一把 `drawingLineWidth` 写入目标
  `LineRenderer.widthMultiplier`，随后继续同步材质和 `drawingLineGradient`。
- 模板复制和无模板回退都通过同一入口同步线宽；模板已有的 `widthCurve` 保持不变，
  `drawingLineWidth` 只控制曲线的整体宽度倍率。
- `SetDrawingStyle` 刷新活动线和已完成线时也会重新同步当前序列化线宽，但本次没有新增
  运行时修改线宽的公共 API。
- 不改变输入、墨水长度、采样、碰撞半径、刚体质量、UI、事件、存档、配置、资源加载、
  analytics 或平台分支。

### 34.3 验证、风险与回滚

- 静态确认 DLevel_001～DLevel_010 均绑定同一个玩家线模板，模板
  `widthMultiplier=0.1`，且关卡未覆盖模板的宽度属性；修改后各关将改用自身
  `drawingLineWidth`。
- 已检查代码路径：活动线创建后必经 `ApplyDrawingStyle`，已有活动线和完成线的样式刷新
  也复用该入口。
- 恢复 `DrawGame.cs` 中丢失的 `EnsureValidCollections` 和
  `ChoiceLevelPanelPageIndex` 手写 API 后，执行
  `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1`，
  结果为 `0 error / 0 warning`。
- 待验证：Unity PlayMode 中分别以模板路径和清空模板的回退路径画线，确认
  `drawingLineWidth=0.1/0.15/0.2` 的视觉宽度、宽度曲线和碰撞表现；Android/iOS 横屏视觉
  结果尚未验证。
- 风险：线宽与 `colliderRadius` 仍是独立配置，增大视觉线宽不会自动扩大碰撞体；各关现有
  宽度差异现在会真实显示，需要 PlayMode 回归 DLevel_002 和 DLevel_008。
- 回滚：删除 `ApplyDrawingStyle` 对 `widthMultiplier` 的赋值，并恢复无模板分支对
  `startWidth/endWidth` 的设置，即恢复模板宽度优先的旧行为。

## 35. 2026-07-29 DelayAutoBeginDrawHandle 与 Erase 解耦

### 35.1 需求、基线与影响范围

- 需求：`DelayAutoBeginDrawHandle.delay` 只延迟开放画线输入，不得参与资源加载，也不得
  推迟 `EnterLevelLoading` 的 Erase 开始。
- 修改前：该组件只实现 `IDrawLevelIntroHandler`。`OpenDrawLevelAsync()` 会在
  `EnterLevelLoading` 的加载回调内等待普通 Intro，因此只要 prefab 配置了非零
  `delay`，`StartEraseSequence()` 就会额外延后。
- 涉及脚本：
  - `Assets/Game/Scripts/Level/DrawLevel/DelayAutoBeginDrawHandle.cs`
  - `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
- 当前挂载范围为 DLevel_001、DLevel_003～DLevel_009。本次代码修改不包含任何 prefab
  配置调整，运行时按各 prefab 的序列化 `delay` 执行首次进关门禁。

### 35.2 修改后行为与生命周期

- `DelayAutoBeginDrawHandle` 改为实现现有 `IDrawLevelVisibleIntroHandler` 标记接口。
  `DrawLevelCtrl` 因此把它归入转场结束后执行的 Intro；资源加载、实例化和初始化完成后，
  `EnterLevelLoading` 可以立即开始 Erase。
- Erase 完成回调再执行 `DelayAutoBeginDrawHandle.PlayDrawIntroAsync()`；等待结束后仍由
  `DrawPlayCtrl.CompleteIntroAndBeginDrawing()` 统一开放输入，组件不直接操作阶段或输入。
- Reset 不经过 Erase。2026-07-30 起，`DelayAutoBeginDrawHandle` 被进一步明确为仅进关
  Handle，Reset/Replay 会跳过其 delay；完整边界见第 37 节。
- DLevel_001 Reset 不再等待其 prefab 配置的关卡延迟，DLevel_003～DLevel_009 也不会
  重放该 Handle；
  现有 0.5 秒门禁继续避免黑幕淡出期间提前接收画线输入。
- 不新增全局事件、DB、存档、Luban、资源地址、analytics、广告、SDK 或平台分支。

### 35.3 验证、风险与回滚

- 静态确认 `DelayAutoBeginDrawHandle` 通过继承关系仍属于 `IDrawLevelIntroHandler`，同时会
  命中 `DrawLevelCtrl.CacheLocalHandlers()` 的 `IDrawLevelVisibleIntroHandler` 判定。
- 静态确认八个挂载 prefab 仍由同一 Handle 提供首次进关门禁；本次不把工作区中的
  prefab 序列化值纳入代码提交。
- `git diff --check` 对目标代码通过；执行
  `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1 /p:UseSharedCompilation=false`
  通过，结果为 `0 error / 101 warning`，warning 为当前工程既有项。
- 未验证：Unity PlayMode 中 DLevel_001 翻页后立即开始 Erase、Erase 完成后按当前
  prefab 配置开放输入，
  以及 Reset 跳过进关 delay 后黑幕淡出与输入开放的实际视觉时序。
- 未验证：DLevel_003～DLevel_009 的进入/Reset 回归和 Android/iOS 横屏触控。
- 回滚 Erase 解耦时，把 `DelayAutoBeginDrawHandle` 恢复为只实现
  `IDrawLevelIntroHandler`；这会恢复 DLevel_001 在 Erase 前等待 prefab 配置时长的旧
  耦合行为。
  仅回滚 Reset 跳过规则按第 37 节执行。

## 36. 2026-07-29 Draw Layer 独占可绘制与非 Draw 障碍门禁

### 36.1 需求、基线与影响范围

- 需求：玩家只能在 `Draw` Layer 的 `DrawLineArea` 内绘制；触点或新增长线段碰到任意
  非 `Draw` Layer 的 `Collider2D` 时禁止继续绘制。
- 修改前：`TryBeginStroke()` 和 `TryContinueStroke()` 只通过指定
  `drawArea.OverlapPoint()` 判断触点是否位于可绘制区域。其它 Layer 的碰撞体即使覆盖在
  DrawLineArea 上，也不会阻止落笔或新增采样点。
- 涉及脚本：`Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`。
- 受影响范围：所有使用共享 `DrawLineFeature` 的 DrawGame 关卡。静态 YAML 证据确认
  DLevel_001～DLevel_010 均显式绑定 `drawArea`，对应 `DrawLineArea` 均为 Layer 14
  （`Draw`）；本次不修改关卡 prefab、Layer 配置或 Physics2D 碰撞矩阵。

### 36.2 修改后行为与检测流

- 初始化继续解析项目 `Draw` Layer，并缓存其反向 LayerMask；`drawArea` 缺失或不在
  `Draw` Layer 时关闭画线输入并通过 Bear Logger 报错，不再回退到整个相机视口。
- 落笔必须同时满足：输入/UI/墨水门禁通过、触点位于已绑定 DrawLineArea 内，并且以
  当前视觉线宽一半为半径的 `PhysicsScene2D.OverlapCircle` 没有命中非 Draw Layer 碰撞体。
- 连续画线仍按 `sampleSpacing` 离散采样；每个新点写入前，把上一采样点与候选点转换为
  世界坐标，先检查候选点重叠，再执行同半径 `PhysicsScene2D.CircleCast`。检测到非 Draw
  Layer 碰撞体时不写入该点，并调用现有 `CompleteActiveStroke()`：
  - 已形成有效长度时，保留碰撞前的线段并进入既有完成、碰撞体、质量和事件流程；
  - 只有单点或不足最小长度时，复用现有取消逻辑删除该笔并返还墨水。
- 检测半径按 `drawingLineWidth * 0.5` 和 DrawSpace 最大 XY 世界缩放计算，覆盖快速滑动
  穿越薄碰撞体的情况；没有新增逐帧对象扫描、集合分配、全局事件、DB、存档、配置、
  资源、UI、analytics 或平台分支。
- 已完成的运行时线段使用非 Draw Layer 且带 Collider2D 时，也会按本规则成为后续笔画
  的障碍；这是“除 Draw 外均为障碍”的直接结果。

### 36.3 验证、风险与回滚

- 静态确认 DLevel_001～DLevel_010 的 `drawArea` 引用和 DrawLineArea Layer 14 配置完整。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1
  /p:UseSharedCompilation=false` 通过，结果为 `0 error / 101 warning`；warning 为当前工程
  既有项。
- 待验证：Unity PlayMode 中 DrawArea 空白处正常落笔、非 Draw 静态/Trigger Collider2D
  覆盖处拒绝落笔、快速滑动不穿透、碰撞前有效线段完成、极短线返还墨水、多笔模式下已
  完成线阻挡后续笔画，以及离开 DrawLineArea 的既有结束行为。
- 待验证：DLevel_001～DLevel_010 的关键解法是否依赖穿过非 Draw Collider2D，以及
  Android/iOS 横屏触控手感和连续 Physics2D 查询开销。
- 2026-08-03 起规则用显式 `ContactFilter2D.useTriggers=true`，会把场景内所有非 Draw Layer
  Collider2D（包括 Trigger 和已完成线段）纳入候选，不再受运行时全局
  `Physics2D.queriesHitTriggers` 影响。
  若某个只用于逻辑检测的 Collider2D 不应阻挡，必须有明确的 Layer/物理设计例外，不能
  静默绕过共享规则。
- 回滚：移除 `_blockingLayerMask`、落笔 `IsBlockedAt()`、采样段
  `IsBlockedBetween()` 及其完成分支，并恢复 `drawArea` 缺失时的相机视口回退，即恢复
  修改前只检查 DrawLineArea 范围的行为；不需要 prefab 或存档迁移。

## 37. 2026-07-30 Reset 跳过仅进关输入延迟

### 37.1 需求、基线与影响范围

- 用户反馈：Draw Reset 完成后仍有约 1～2 秒无法点击。
- 直接代码证据：`ReloadCurrentLevelAsync()` 在黑幕淡出时再次调用
  `PlayIntroAsync()`；`DelayAutoBeginDrawHandle` 的 prefab 配置时长因而在每次
  Reset/Replay 重放，再与 0.5 秒黑幕输入门禁并发等待。
- 影响脚本：`DrawLevelRuntimeTypes.cs`、`DrawLevelCtrl.cs`、
  `DelayAutoBeginDrawHandle.cs`、`DrawPlayCtrl.cs`。
- 影响关卡：挂载 `DelayAutoBeginDrawHandle` 的 DLevel_001、DLevel_003～DLevel_009。
  没有修改任何关卡 prefab 或序列化 delay。

### 37.2 修改后行为与边界

- 新增 `IDrawLevelEnterOnlyIntroHandler` 标记，明确该类 Handle 只在首次进关执行，不能
  承担 Reset 状态恢复职责。
- `DelayAutoBeginDrawHandle` 实现该标记：首次进关仍在 Erase 完成后按 prefab delay
  等待；Reset/Replay 不再重放该等待。
- `IDrawLevelRuntime` 新增 `PlayResetIntroAsync()`；`DrawLevelCtrl` 的 Reset Intro 链
  只过滤 `IDrawLevelEnterOnlyIntroHandler`，其它真实入场、机关复位和物体释放 Handle
  保持原执行顺序。
- `DrawPlayCtrl.ReloadCurrentLevelAsync()` 改用 Reset 专用入口，并继续并发等待既有
  0.5 秒门禁。黑幕 0.4 秒淡出期间仍遮挡输入，避免画面未揭开时提前落笔。
- 没有新增全局事件、DB、存档、Luban、资源地址、analytics、广告、SDK 或平台分支。

### 37.3 验证、风险与回滚

- 静态确认 `IDrawLevelRuntime` 当前只有 `DrawLevelCtrl` 一个实现，接口新增不会遗漏其它
  运行时实现。
- 静态确认 `IDrawLevelEnterOnlyIntroHandler` 当前只由
  `DelayAutoBeginDrawHandle` 实现；DLevel_002、DLevel_010 等真实入场控制器不会被
  Reset 过滤。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1
  /p:UseSharedCompilation=false` 通过，结果为 `0 error / 101 warning`；warning 为当前
  工程既有项。
- 待验证：Unity PlayMode 中 DLevel_001 Reset 后黑幕淡出完成即允许画线；
  DLevel_003～DLevel_009 Reset 无额外空等；DLevel_002/010 Reset 仍完成真实入场和
  物体状态恢复；连续 Reset、失败自动重试和 Victory Replay。
- 待验证：Android/iOS 横屏触控、低帧率下黑幕关闭与首个触点时序。
- 风险：若未来把承担状态恢复的 Intro 错误标记为 `IDrawLevelEnterOnlyIntroHandler`，
  Reset 会跳过必要流程；接口注释和本记录将其限定为无状态的进关等待或装饰性演出。
- 回滚：删除 `IDrawLevelEnterOnlyIntroHandler` 与 `PlayResetIntroAsync()`，让
  `DelayAutoBeginDrawHandle` 恢复只实现 `IDrawLevelVisibleIntroHandler`，并把
  `ReloadCurrentLevelAsync()` 恢复调用 `PlayIntroAsync()`；无需迁移 prefab、配置或存档。

## 38. 2026-07-30 DrawLineFeature 按测试 ID 加载笔刷配置

### 38.1 需求、原行为与影响范围

- 需求：`DrawLineFeature` 根据 ID 从
  `Assets/Game/UI/DrawGame/PencilSkinPopup/Resources/PencilSkinConfig` 加载
  `DrawBrushStyleAsset`，先把测试 ID 暴露到 Inspector，方便验证材质与 Gradient 表现。
- 原行为：`DrawLineFeature` 只读取当前 DLevel prefab 上的 `drawingLineMaterial`、
  `drawingLineGradient` 和 `drawingLineWidth`；`DrawBrushStyleAsset` 没有运行时消费方。
- 影响脚本：`Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`。
- 影响资源：`Resources/PencilSkinConfig/PencilSkin_{id}.asset` 及其引用的 Material。
- 影响关卡：所有使用共享 `DrawLineFeature` 的 DrawGame 关卡；新增
  `brushStyleId` 默认值为 `10001`，未显式配置或序列化为空的关卡都会加载默认笔刷。

### 38.2 修改后行为与数据流

- `DrawLineFeature` 新增 Inspector 字符串 `brushStyleId`，默认显示 `10001`；测试时填写
  其它 ID 会构造对应 Resources 地址，字段为空仍回退到
  `PencilSkinConfig/PencilSkin_10001`。
- `InitializeRuntime()` 在运行时引用与 Draw Layer 校验通过后加载一次配置；不在
  `Update`、`Tick` 或每次落笔时重复加载。
- 加载使用 Bear `ResourceManager.Instance.Load<DrawBrushStyleAsset>()`；项目
  `LoadManager` 已在主启动流程调用 `ResourceSystemInitializer.Initialize()`，
  `ResourcesLoader` 可从上述 Resources 地址兜底读取。
- 加载成功后必须同时满足 `TryValidate(...)` 和配置内 `BrushId` 与请求 ID 精确一致，
  才把 `LineMaterial`、`ColorGradient` 复制到当前 `DrawLineFeature` 的非序列化运行时
  样式字段。
- `drawingLineWidth` 继续保留为 DLevel Inspector 配置，并始终覆盖
  `DrawBrushStyleAsset.LineWidth`；加载笔刷 asset 不修改关卡线宽。
- 新建笔画继续在 `TryBeginStroke()` 中调用 `ApplyDrawingStyle(...)`，把配置材质赋给
  `LineRenderer.sharedMaterial`，并同步 Gradient 与 widthMultiplier。
- 默认或指定资源不存在、配置无效或 ID 不一致时只记录 Bear warning，材质回退到
  `Sprites/Default` 或 lineTemplate 共享材质，Gradient 回退为黑色；Inspector 线宽保持
  生效，不关闭画线输入。
- 本次只在关卡初始化、尚未生成运行时线条前应用配置，因此不会回写活动线或已完成线；
  既有 `SetDrawingStyle(...)` 的显式刷新行为未在本次改动。

### 38.3 资源、性能与系统边界

- 配置路径不带 `Resources/` 和 `.asset` 后缀，固定格式为
  `PencilSkinConfig/PencilSkin_{id}`。
- `DrawBrushStyleAsset` 及共享 Material 只作为只读资源引用；代码不实例化或修改共享
  Material 属性。
- `DrawLineFeature` 不再序列化 `drawingLineMaterial`、`drawingLineGradient` 或
  `drawingLineColor`；现有 prefab YAML 中的旧字段会被 Unity 忽略，并可在后续正常保存
  prefab 时清理，本次不改写用户 prefab。
- 本次未接入 PencilSkin 存档、Luban、商城选择、DB/Save、RemoteConfig、广告、IAP、
  analytics、全局事件、Cover Texture、Sprite Cover/Mask 或 Shader 属性写入。
- ResourceManager 会缓存已加载配置；本次没有在单个关卡销毁时调用 `Release(path)`，
  避免无引用计数的共享路径释放影响其它消费者。测试期加载数量受 Inspector ID 数量限制，
  长期皮肤切换和统一释放策略仍待正式接入时设计。

### 38.4 验证、风险与回滚

- 静态核对 `PencilSkin_10001～10008.asset` 的资源名与内部 `brushId` 一致，且当前均配置
  `lineMaterial`、`colorGradient` 和合法 `lineWidth`。
- 静态确认 `DrawLineFeature` 已无序列化 Material / Gradient / Color 字段；材质与
  Gradient 只存在于非序列化运行时缓存，`drawingLineWidth` 是唯一保留的样式 Inspector
  字段。
- 静态确认加载只从 `InitializeRuntime()` 调用一次，落笔路径不执行资源查询；ID 留空
  会归一化为 `10001`。
- `git diff --check -- Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs` 通过，
  仅有工作区 LF/CRLF 转换提示。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1`
  通过，结果为 `0 error / 101 warning`。
- 未验证：Unity PlayMode 中分别填写空 ID、10001、10002、10007、缺失 ID 和内部 ID
  不一致资产时的视觉与 warning；所有既有关卡默认切到 10001 后的视觉一致性；
  Reset/Replay 后样式保持；Android/iOS 横屏触控和资源首次同步加载耗时。
- `uncertain`：如果脱离项目主启动流程单独运行 DLevel 场景，Bear ResourceSystem 可能
  尚未注册 Loader；当前正式玩法入口有 `LoadManager` 初始化证据，但独立场景测试仍需验证。
- 回滚本次 Inspector 精简时，重新增加序列化 `drawingLineMaterial`、
  `drawingLineGradient`、隐藏 `drawingLineColor`，让运行时 API 和
  `ApplyDrawingStyle(...)` 恢复读写这些字段，并在加载 asset 时恢复写入
  `drawingLineWidth = brushStyle.LineWidth`；不需要修改配置资产或存档。

### 38.5 2026-07-30 默认笔刷 ID 迁移到 301001

- 需求：把 `DrawLineFeature` 的默认笔刷从已移除的旧 ID `10001` 调整为当前资源 ID
  `301001`。
- 修改前基线：`DefaultBrushStyleId`、`brushStyleId` 字段初始值及空字符串 fallback
  均指向 `10001`；当前工作区已删除旧 `PencilSkin_10001.asset`，因此未显式序列化 ID
  或字段为空的实例会进入资源加载失败 fallback。
- 修改后：`DefaultBrushStyleId` 为 `301001`；新挂载的 `DrawLineFeature`、
  未序列化该字段的实例及字段为空的实例统一加载
  `PencilSkinConfig/PencilSkin_301001`。显式填写其它非空 ID 的关卡行为不变。
- 影响范围仅为 `Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs` 的默认资源
  选择；不修改 DrawGame 存档、Luban 表、商城选择、现有关卡线宽、事件、Shader、
  Cover/Mask 或平台流程。当前 `DLevel_001.prefab` 已在既有工作区改动中显式序列化
  `brushStyleId: 301001`，本次未改写 prefab。
- 静态验证：确认 `PencilSkin_301001.asset` 存在且内部 `brushId` 为 `301001`；
  全量 prefab / scene / asset 搜索只发现 `DLevel_001.prefab` 序列化
  `brushStyleId: 301001`；定向搜索确认脚本默认常量、注释和 Tooltip 均已同步。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nr:false
  /p:BuildInParallel=false` 通过，结果为 `0 error / 101 个项目既有 warning`；
  三个本次修改文件的定向 `git diff --check` 通过，仅有 LF/CRLF 转换提示。
- 未验证：Unity PlayMode 中空 ID、未序列化字段和显式 `301001` 三种入口的实际视觉；
  Android/iOS 横屏资源加载与画线表现。此修改不新增每帧工作或运行时实例，预期无新增
  GC 与移动端性能开销，但尚未进行真机验证。
- 回滚：把 `DefaultBrushStyleId` 及对应 Inspector 注释恢复为 `10001`；若旧资源仍不存在，
  回滚后默认加载将再次进入 fallback。

## 39. 2026-07-30 DrawLineFeature Texture Cover Noise 试验

### 39.1 需求、修改前基线与影响范围

- 需求：改善当前笔刷质感，使用可平铺 noise 调整 `LineRenderer` 表现，并先在默认笔刷
  `301001` 上试验。
- 修改前 `DrawLineFeature.TryBeginStroke()` 会把所有运行时线条强制设为
  `LineTextureMode.Stretch`；线越长，材质纹理沿整笔拉伸越明显。
- 修改前 `DrawLineGradient.mat._MainTex` 在当前工作区引用
  `EnterLevelLoading_img_brush.png`，`PencilSkin_301001.coverTexture` 又引用同图复制出的
  `test_brush.png`。两者都是 256×256 白色 RGB、Alpha 轮廓图，不是连续笔画用的
  无缝 noise。
- 影响代码与资源：
  `DrawLineFeature.cs`、`DrawLineGradient.shader`、`DrawLineGradient.mat`、
  `PencilSkin_301001.asset`。不修改 DLevel prefab、DrawGame 存档、Luban 表、
  物理碰撞、墨水长度、胜负、提示线、事件、广告、IAP 或平台 SDK。

### 39.2 修改后表现与数据流

- `DrawLineFeature` 在加载有效 `DrawBrushStyleAsset` 后缓存 `CoverTexture`、
  `CoverTint`、`CoverTiling` 和 `CoverStrength`；加载失败或无 Texture Cover 时把
  Cover 运行时状态恢复为白色、平铺 1、强度 0。
- 配置有效且 `coverTexture != null`、`coverStrength > 0` 时，新线和样式刷新后的线条
  使用 `LineTextureMode.Tile`；没有有效 Texture Cover 时继续使用原
  `LineTextureMode.Stretch`，避免统一改变其它无纹理笔刷。
- Shader 保留 `_MainTex × LineRenderer.colorGradient × _Color` 的基础颜色链，新增
  `_CoverTex`、`_CoverTint`、`_CoverTiling`、`_CoverStrength`。Cover 的红通道与 Alpha
  共同调制最终透明度，RGB 与 Tint 共同调制基础颜色。
- `DrawLineFeature` 通过复用的 `MaterialPropertyBlock` 向单条线写入 Cover 参数，
  继续只给 `sharedMaterial` 赋引用，不修改共享材质属性。PropertyBlock 只在首次应用时
  创建，之后在落笔和显式样式刷新时复用，不在 `Tick` 每帧分配。
- `PencilSkin_301001` 当前试验参数为项目既有
  `MMBlueNoise.png`、`coverTiling = 10`、`coverStrength = 0.45`、白色 Tint。
  该 noise 为 512×512、Repeat、Read/Write 关闭、MipMap 开启；材质 `_MainTex` 恢复为空，
  由 Shader 默认白纹理保留纯 Gradient 基础。

### 39.3 验证、风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nr:false
  /p:BuildInParallel=false` 通过，结果为 `0 error / 101 个项目既有 warning`。
- 定向 `git diff --check` 通过，仅有仓库 LF/CRLF 转换提示；Unity 6000.0.59f2 的实时
  Editor 已检测并由 `ShaderImporter` 导入 `DrawLineGradient.shader`，当前导入记录未见
  该 Shader 的编译错误。首次导入提示混合换行后已统一为 UTF-8 LF。
- 静态确认 C# 与 Shader 的四个属性名完全一致；`301001` 的 Cover GUID 指向
  `MMBlueNoise.png`，其 Repeat、Read/Write 和 MipMap 设置符合本次运行时采样要求。
- 未验证：Unity PlayMode 中短线、长线、急转角、圆头、连续多笔、Reset/Replay 的实际
  视觉；`coverTiling = 10` 和 `coverStrength = 0.45` 是首轮试验值，仍需按游戏相机与
  线宽人工调优。也未验证 Android/iOS 横屏上的压缩、闪烁、透明排序和 GPU fill-rate。
- 性能风险：使用 Texture Cover 的线条每像素增加一次纹理采样；无新增 Update 路径或
  每帧资源加载。当前只给 `301001` 配置 Cover，回归范围局部，但低端移动设备仍需真机
  观察长线和多笔重叠时的帧率。
- 回滚可把 `PencilSkin_301001.coverTexture` 清空或把 `coverStrength` 设为 0，立即恢复
  Stretch/Gradient 路径；完整回滚时删除 Shader Cover 属性、C# Cover 缓存与
  PropertyBlock 应用，并恢复 `TryBeginStroke()` 固定 Stretch。`test_brush.png` 是用户
  工作区既有未跟踪资源，本次未删除或改写。

## 40. 2026-07-30 DrawLineFeature 完成笔画视觉平滑与圆头

### 40.1 需求、原行为基线与影响范围

- 需求：玩家完成一笔画线后，自动平滑折线转角，并让线条首尾以圆滑表面结束；用户明确
  要求碰撞逻辑保持不变，并暂不增加功能开关。
- 原行为：拖动期间按 `sampleSpacing` 把原始采样点直接写入 `LineRenderer`，完成时只
  重设线段中心并以相同原始点构建 `EdgeCollider2D` 或复合胶囊碰撞体，没有完成后视觉
  插值。公共 `DrawGame_DrawLine.prefab` 的 `numCornerVertices` 与 `numCapVertices`
  均为 0；只有缺少模板时创建的临时 LineRenderer 使用 4/4。
- 影响代码仅为
  `Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`。这是共享 DrawGame 行为，
  所有使用该组件的新旧 DLevel 都会得到完成后视觉平滑和统一圆头；未修改任何 DLevel
  prefab、DB、存档、Luban、资源地址、UI、事件、analytics、广告、IAP 或平台 SDK。

### 40.2 修改后行为与数据流

- `ApplyDrawingStyle()` 统一把所有模板和运行时 LineRenderer 配置为
  `numCornerVertices = 2`、`numCapVertices = 6`，不再依赖各 prefab 模板是否配置圆角。
- 有效笔画完成时继续先调用 `RecenterActiveStrokeGeometry()`，随后使用原始
  `_activePoints` 构建既有碰撞体；碰撞完成后才调用
  `ApplyCompletedStrokeVisualSmoothing()`，只重写当前 LineRenderer 的 Positions。
- 视觉平滑执行一次 Chaikin Corner Cutting：保留原始首尾点，每个相邻采样点之间插入
  25% 与 75% 两个点。插值点位于原线段内部，不产生 Catmull-Rom 式转角外扩。
- `_activePoints`、`_activeStrokeLength`、`_usedInk`、`DrawInkState`、刚体质量计算、
  `EdgeCollider2D.points`、RDP 复合碰撞输入及 `DrawStrokeResult` 均继续使用修改前的
  原始数据。因此视觉中心线会切圆尖角，但碰撞轮廓、墨水消耗与关卡完成回调保持原逻辑。
- 少于三个采样点的有效短线不执行中心线插值，但仍使用统一圆头与圆角配置。

### 40.3 验证、风险与回滚

- 定向 `git diff --check -- DrawLineFeature.cs` 通过，仅有仓库 LF/CRLF 转换提示。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nr:false
  /p:BuildInParallel=false` 通过，结果为 `0 error / 101 个项目既有 warning`。
- 静态核对完成顺序为“原始点中心归位 → 原始点碰撞体 → 视觉点插值 → 激活刚体”，
  平滑方法没有写回 `_activePoints` 或任何墨水、存档、事件字段。
- 未验证：Unity PlayMode 中直线、锐角、连续曲线、极短线、多笔、Reset/Replay，
  DLevel_007 投掷线段以及 DLevel_010 线段互撞的实际视觉/碰撞对齐；Android/iOS 横屏
  的长线顶点量、帧率、透明排序和 Texture Cover 平铺效果也尚未验证。
- 性能风险：每笔完成时产生一个长度为原始点数两倍的 `Vector3[]`，并让完成线条保留约
  两倍视觉中心线点；不新增 `Update`、拖动阶段插值、资源加载或物理碰撞段。长墨水、多笔
  和低端移动设备仍需用 Profiler 检查一次性 GC 与 LineRenderer 顶点开销。
- 表现风险：视觉转角被向折线内部切圆，而碰撞继续保留原尖角，因此锐角处可能出现小幅
  视觉/碰撞边界差异；这是“碰撞不变”的直接结果，需在依赖精确线碰撞的关卡重点验证。
- 回滚：删除 `ApplyCompletedStrokeVisualSmoothing()` 及完成笔画中的调用，把
  `ApplyDrawingStyle()` 的圆角/圆头赋值删除，并将缺少模板时的 LineRenderer 恢复为
  `numCornerVertices = 4`、`numCapVertices = 4`；不涉及 prefab、配置或存档迁移。

## 41. 2026-07-30 默认线段改为子 BoxCollider2D 复合刚体

### 41.1 需求、原行为基线与影响范围

- 需求：默认 `EdgeCollider2D` 线段碰撞效果不理想，改为沿固定间隔采样点创建
  `BoxCollider2D`，由父线段对象上的单一 `Rigidbody2D` 统一控制；同时保证碰撞厚度、
  转角覆盖和视觉线段体积关系可解释。
- 原行为：未启用 `enableStrokeToStrokeCollision` 时，运行时线段根节点持有一个
  `EdgeCollider2D`，其 `edgeRadius = colliderRadius`、points 为原始采样点。根节点已经
  由 `PrepareRuntimeStrokeRigidbody()` 创建并暂停唯一动态 Rigidbody2D，完成碰撞构建后
  再由 `ActivateRuntimeStrokeRigidbody()` 写入质量并启用模拟。
- 影响脚本：
  `Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`、
  `Assets/Game/Scripts/Level/DrawLevel/Level7/DLevel7Ctrl.cs`。不修改 DLevel prefab、
  输入、视觉平滑、墨水、质量曲线、存档、Luban、UI、事件、资源或平台 SDK。
- DLevel_010 显式开启的“简化 CapsuleCollider2D + CompositeCollider2D”互撞路径保持
  不变；只有该路径无法生成胶囊时，fallback 从 Edge 改为 Box 链。

### 41.2 修改后结构、碰撞体积与生命周期

- `BuildBoxStrokeColliderChain()` 按每一对相邻 `_activePoints` 创建一个直接子节点，
  子节点旋转到采样段方向并只挂非 Trigger `BoxCollider2D`；子节点不创建 Rigidbody2D，
  因而全部形状自动组成根节点唯一 Rigidbody2D 的 compound collider。
- 每个 Box 的厚度为 `colliderRadius × 2`，与原 EdgeCollider2D 的完整物理厚度一致；
  不直接用 `drawingLineWidth` 覆盖关卡物理配置。当前静态配置中多数关卡为视觉宽 0.1、
  colliderRadius 0.05；DLevel_002/004/008 的视觉/碰撞差异是既有 prefab 配置，本次保留。
- 每个 Box 长度为“当前相邻采样段长度 + 一个碰撞直径”，中心仍位于两采样点中点。
  因此前后各延伸一个半径，相邻旋转 Box 会在转角重叠，避免离散采样段之间留下物理缝隙；
  首尾也各延伸一个半径，与原 EdgeCollider2D 的厚度端部覆盖量同阶。
- 根节点若来自模板且残留 EdgeCollider2D，会先禁用并销毁，避免 Box 链与旧 Edge 形成
  双重碰撞。有效线段仍按“中心归位 → 创建子碰撞盒 → 视觉平滑 → 写质量/启用根刚体”
  完成；Reset/销毁根线段时，Unity 会一并销毁全部子碰撞盒。
- DLevel_007 投掷前会依据完成后 LineRenderer 再次调整根中心；兼容逻辑现同步把所有
  直接子 Collider2D 的 localPosition 减去相同偏移，防止视觉线移动后 Box 链留在旧位置。

### 41.3 验证、性能风险与回滚

- 定向静态搜索确认共享默认路径不再 `AddComponent<EdgeCollider2D>()`，Edge 仅保留为
  模板残留清理和 DLevel_007 旧结构兼容读取；子 Box 没有 Rigidbody2D 创建代码。
- 定向 `git diff --check` 通过，仅有仓库 LF/CRLF 转换提示。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nr:false
  /p:BuildInParallel=false` 通过，结果为 `0 error / 101 个项目既有 warning`。
- 未验证：Unity PlayMode 中直线、锐角、回折、多笔、Reset/Replay、物体落在线段接缝、
  DLevel_007 投掷和 DLevel_010 fallback；Android/iOS 横屏物理稳定性与性能均待验证。
- 性能风险：默认路径现在每个有效采样段增加一个子 GameObject 和 BoxCollider2D。根刚体
  数量仍是一笔一个，但长线/多笔的 compound shape 数量会高于原单个 EdgeCollider2D；
  尤其 DLevel_001 的 `maxInkLength=50`、`sampleSpacing=0.1` 理论上可接近 500 个子盒，
  必须在移动端 Profiler 观察完成笔画时的实例化峰值、物理 Step 和 Reset 销毁峰值。
- 体积风险：Box 的方形角会比同半径圆头在对角处略大，视觉平滑线与原始采样碰撞链仍可能
  在急转角出现局部边界差异；当前优先保证无缝和既有 colliderRadius 权威。
- 回滚：把 `BuildActiveStrokeCollider()` 恢复为根 EdgeCollider2D，恢复复合碰撞失败时的
  Edge fallback，并删除 DLevel_007 的子 Collider 平移兼容；根 Rigidbody2D、质量配置、
  prefab、存档和资源均无需迁移。

## 42. 2026-07-31 成功附属物同步渐隐

### 42.1 需求、原行为与影响范围

- DLevel_002 的 `yellow_ball` 是 Actor 父节点；原成功链只淡出 Actor Spine 和 Actor
  子级 SpriteRenderer，无法覆盖球视觉，导致过关时人物消失而球仍可见。
- `DrawLevelCtrl` 新增可空序列化字段 `successExtraRendererGroup`，用于同一 DLevel 内
  需要随 Actor 同步退场的附属 SpriteRenderer 层级。当前只有 DLevel_002 绑定；其它关卡
  字段为空，不改变成功动画或序列化数据。
- 影响文件为 `DrawLevelCtrl.cs` 与 `DLevel_002.prefab`。不修改成功门触发条件、
  `ActorCtrl`、DrawLineFeature、倒计时、结果提交、存档、Luban、UI、analytics、SDK 或
  平台分支。

### 42.2 新行为、恢复与边界

- `FadeActorOutAsync()` 开始时对 Actor 组和可选额外组各执行一次 `CollectRenderers()`；
  原 `0.4s` 循环计算出 alpha 后，在同一帧依次设置 Actor Spine、Actor Sprite 组和额外组，
  最终统一写 0。因此不新增 Tween、异步任务或成功等待时间。
- DLevel_002 在 yellow_ball 根添加 `SpriteRendererGroupAlpha`，启用
  `includeInactive/autoCollectRenderers`，覆盖 idle/open/item 状态。额外组也可能收集
  Actor 的 SpriteRenderer，但相同绝对 alpha 的重复赋值不会叠乘；Actor Spine 仍走原 API。
- `ResetGameState()` 把额外组重新收集并恢复到 alpha 1，覆盖同实例 Reset 测试；正式失败、
  Replay 和下一关仍由整关实例重建恢复。

### 42.3 验证、风险与回滚

- 静态确认 `successExtraRendererGroup` 只在共享成功淡出循环与 Reset 使用；DLevel_002 的
  组件/引用/脚本 GUID 唯一且非空，其他 DLevel 没有强制绑定要求。
- 编译后的 `Assembly-CSharp.dll` 包含新增字段及局部渐隐组，Unity 最近日志无新增
  `error CS`、Compilation failed 或相关 MissingReference。
- `uncertain`：尚未完成完整 PlayMode 和 Android/iOS 横屏视觉验证。当前球 Sprite 初始
  alpha 均为 1；未来若附属组含有意半透明 Sprite，绝对 `SetAlpha` 会覆盖其基线透明度。
- 回滚为删除可选字段及其 Collect/Set/Reset 调用，并从 DLevel_002 移除球组组件和引用；
  不涉及资源、配置或数据迁移。

## 43. 2026-08-02 结果链只读状态边界

- `DrawLevelCtrl` 新增只读 `IsOutcomeLocked`，统一表示成功演出、失败等待、失败已确认或最终结算
  已占用当前尝试。它不改变既有状态机、结果提交、输入、存档或事件，只给同关局部机关提供
  “当前尝试是否已被结果链占用”的同步判断。
- DLevel017 已不再锁定或恢复角色移动；该物理弹簧只在 Trigger 入口和实际发射前读取
  `IsOutcomeLocked`，防止 0.2 秒压缩期间结果链已被其它来源占用后仍施加右上冲量。该属性不参与
  DLevel17 的 0.35 秒外部水平动量保留，也不再用于
  防止 `ResumeMoving()` 覆盖结果链，因为 DLevel17Ctrl 已删除 `ResumeMoving()` 路径。
- 属性只读取现有五个私有状态，不分配对象、不轮询场景、不新增事件或平台分支。最终
  Assembly-CSharp 完整 C# 编译为 `0 error / 104 warning`；成功门/失败/暂停交错的完整 PlayMode
  仍需随 DLevel017 验证。回滚时删除该属性，并同步移除 DLevel017 对它的两处门禁即可。

## 44. 2026-08-02 复合球刚体的 FallArea 失败恢复

- 问题不在 `DrawLevelCtrl.OnTriggerFailArea()`，而在共享入口 `FailAreaTriggerHandle`：球根
  CircleCollider 进入 Trigger 时，玩家 `ActorCtrl` 位于同一 Rigidbody2D 的子层级，旧版对碰撞
  GameObject 执行精确组件查询会返回空，事件根本没有进入 Draw 失败链。
- 新入口同时支持两类玩家：普通 Actor 继续校验 Player Tag；复合载体只接受刚体根 Collider，
  并回认“同一 attachedRigidbody 子树、最近父刚体一致、Actor Tag 为 Player”的角色。球根自身
  可为 Player 或 tools，因而 DLevel006 左右球均可触发；敌方 Actor 和其它嵌套刚体被排除。
- 事件到达后仍走既有幂等锁、失败音效、`0.2s` 等待与整关重建，不改变结果优先级，也不调用
  `ActorCtrl.Die()`。若双球成功碰撞与 FallArea 在同一物理步发生，仍由 Unity 回调中先占结果锁
  的路径决定，顺序不保证。
- 静态检查覆盖 DLevel002/006/010/015/018 及共享 FallArea，Assembly-CSharp 全量编译为
  `0 error`；完整 PlayMode 和移动端回归仍为 `uncertain`。回滚只需恢复入口旧查询，不涉及
  Prefab、配置、存档或数据迁移，但会重新造成所有共享球载体无法越界失败。

## 45. 2026-08-03 无成功门的 Actor 渐隐结算入口
## 45. 2026-08-03 Draw 进入可玩阶段切换关卡音乐

### 45.1 需求、基线与影响范围

- 需求：DrawGame 流程进入玩家可操作的 Playing 阶段时，需要与主线逻辑一致，切换到关卡音乐。
- 修改前：主线 `PlayCtrl_Playing.OnEnter()` 会先检查当前音乐标签，若不是 `musicInGame` 则调用
  `AudioManager.PlayMusic("musicInGame")`；Draw 的统一可玩入口 `DrawPlayCtrl.BeginDrawing()`
  只切换 `DrawPlayPhase.Drawing`、恢复暂停/输入和 HUD 交互，没有切换音乐。
- 影响脚本仅为 `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`。不修改 Draw 关卡 prefab、音频资源、
  Sounds Good 标签配置、DB、存档、Luban、UI、事件、analytics、广告、IAP 或平台 SDK。

### 45.2 修改后行为与流程

- DLevel013 的篮筐成功不存在 Spine 门和 `door_open`。直接调用 `TryResolve(Success)` 会跳过
  Actor/球渐隐；调用旧 `OnTriggerSuccess()` 又会在 `successDoorSpine=null` 时输出错误日志。
- 新增 `TryTriggerSuccessWithoutDoor(Collider2D, string, float fadeDelaySeconds = 0)`，复用与成功门相同的
  Actor/复合刚体身份校验、`TryBeginSuccessPerformance()` 占用、输入锁定、0.4 秒 Actor/额外渲染组渐隐
  和最终成功提交；可选延迟只发生在成功已占用之后、开始渐隐之前，负值按 `0` 处理。
  相对成功门入口的固定差异是不调用 `PlaySuccessDoorOpen()`、不播放开门音效。
- 旧 `OnTriggerSuccess(Collider2D)` 仍固定传入 `playDoorAnimation=true` 和原来的结算来源，现有成功门
  语义、UnityEvent 签名和 Prefab 绑定不变。无门方法返回 bool；DLevel013 用返回值确认成功已占用，
  但按当前需求保持球 Dynamic，并传入 `0.35s` 延迟后再渐隐。
- Assembly-CSharp Roslyn 全量编译为 `0 error`。DLevel013 篮筐 PlayMode、旧门回归及 Android/iOS 仍为
  `uncertain`。回滚新 API 时必须同步替换 DLevel13Ctrl 调用，旧成功门入口无需改动。
- `BeginDrawing()` 通过现有 `AudioManager.IsCurrentMusicTag("musicInGame")` 防止重复播放；当前不是
  关卡音乐时调用现有 `AudioManager.PlayMusic("musicInGame")`，参数与主线 Playing 入口一致。
- 音乐切换发生在 Draw 关卡资源、HUD、转场和可见 Intro 完成并准备开放输入之后，不提前到选关、
  加载或 Intro 阶段。首次进关、失败重开和胜利 Replay 最终都汇入 `BeginDrawing()`，因此共享同一
  切换边界；当前已经播放 `musicInGame` 时不重启曲目。
- 既有暂停状态、画线输入、`GamePlayPanelSwitchBlockEvent`、胜负结算、重建和退出流程保持不变。
  音乐关闭时，现有 `AudioManager.PlayMusic()` 会按 `DB.GameSetting.MusicOn` 直接返回，不绕过用户设置。

### 45.3 验证、风险与回滚

- 已完成静态调用链核对：主线基线位于 `PlayCtrl_Playing.OnEnter()`；Draw 首次进关、失败重开和
  Replay 的可玩出口均为 `DrawPlayCtrl.BeginDrawing()`。
- 定向 `git diff --check` 通过；CodeGraph 复核确认 `BeginDrawing()` 新增调用只触达现有
  `AudioManager.IsCurrentMusicTag()` 与 `AudioManager.PlayMusic()`，且没有对应自动化测试。
- C# 快速编译因当前 `Temp/bin/Debug` 缺少 193 个 Unity 项目引用 DLL 而无法形成代码结论；依赖
  还原成功后，完整项目引用构建运行 244 秒超时且没有返回本次文件诊断，因此编译状态为
  `uncertain`，需要在 Unity 完成程序集生成后补验。
- 待验证：Unity PlayMode 中从 Draw 选关进入、失败重开、胜利 Replay、音乐关闭后进入和已经播放
  `musicInGame` 时进入的听感/重复播放，以及 Android/iOS 横屏真机音频切换。
- `uncertain`：尚未完成 Android/iOS 横屏真机音频切换验证；本次没有平台分支，运行时成本仅为每次
  进入 Drawing 的一次标签检查。
- 回滚：删除 `BeginDrawing()` 中新增的音乐标签检查和 `PlayMusic` 调用即可；无需迁移资源、配置或存档。

## 46. 2026-08-03 需求确认、每日挑战 ID 与奖励墨水重建状态

### 46.1 本轮确认结论

- 当前 Draw 配置直接使用 `MaxInkLength` 的实现继续保留，不再按旧案的“100 换算为 5”语义改造；本轮不修改 Luban 数值、墨水单位或星级阈值。
- 删除未被运行时代码使用的 `DrawPlayPhase.Performance`。这只收缩共享控制器阶段枚举；
  `DrawLevelCtrl.TryBeginSuccessPerformance/CompleteSuccessPerformance` 仍是关卡本地成功演出门禁，名称和行为不变。
- 倒计时结果继续由每关 `DrawLevelCountdownCtrl.isSuccess` 配置决定，策划可按关卡选择到时胜利或失败；不把结果统一写死到共享控制器。
- Pencil 特殊解锁、多笔开关归属和反向道具延期；本轮不修改对应逻辑。

### 46.2 修改前后与数据流

- 修改前每日挑战使用 `-DrawLevelSort.Id` 作为去重键，意图通过负号与主线同序号隔离；但 Draw 配置本身已有从 20001 开始的独立 `DrawLevelData.Id`，负数键会把显示序号混作业务 ID。
- 修改后胜利链通过当前 `DrawLevelSort.Path` 取得 `DrawLevelData`，将其正数 `Id` 解析后传给
  `DailyChallenge.TryRecordVictory()`；缺少配置、非数字或非正数时只记录警告并跳过每日挑战，不伪造备用 ID。
- 修改前奖励墨水“一关一次”门禁保存在 `DrawGame_GamePlayPanel`；Reset/Replay 会销毁并重建 HUD，门禁随之丢失。
- 修改后门禁由 `DrawPlayCtrl` 持有。首次进入新关或进入下一关时重置，退出 Draw 玩法时清理；同关 Reset/Replay 仅重建关卡和 HUD，不清理门禁。新 HUD 创建后由控制器传入状态，已领取时立即隐藏奖励入口。
- 广告成功后的实际加墨仍由当前 `IDrawLevelRuntime.AddInk()` 完成，不写 DB，不跨菜单保存；广告资格、墨水快照动画和 HUD 隐藏流程保持原边界。

### 46.3 验证、风险与回滚

- 已通过 CodeGraph 和定向文本检查确认：`DrawPlayPhase.Performance` 没有运行时引用；每日挑战不再传负数；奖励门禁只有 `DrawPlayCtrl` 持有，HUD 只读取和呈现。
- `Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false` 构建通过：`0 error / 102 warning`；warning 均为项目既有程序集解析、可空标注、弃用 API 和未使用字段等诊断。
- `uncertain`：尚未完成 Unity PlayMode。需验证同关看广告后手动 Reset、失败重建和 Replay 均不再显示入口；进入下一关、返回菜单再进入时恢复；同时验证 20001+ 每日挑战去重与弹窗结算。
- 本次没有修改 prefab、Luban、存档结构、资源地址、全局事件、SDK 接口或平台分支。Android/iOS 横屏广告回调和生命周期仍待验证。
- 回滚时恢复负显示序号传参、把奖励门禁退回 HUD 并恢复 `Performance` 枚举项即可；无需迁移配置、资源或玩家存档。

## 47. 2026-08-03 Reset 与 Victory 竞态导致 Replay 黑屏修复

### 47.1 需求、复现与原行为基线

- 复现步骤：在旧 DLevel 即将提交 Victory、但 `DrawPlayCtrl.OnLevelResolved(...)` 尚未执行前点击
  `DrawGame_GamePlayPanel` 的 Reset；Reset 等待期间 VictoryPanel 仍被旧关卡打开；第一次 Reset 已经重建
  新 HUD 后，再点击残留 VictoryPanel 的 Replay，新 HUD 黑幕淡入后不会淡出。
- 原因一：`OnGameResetEvent(...)` 只关闭画线输入并启动带 `0.5` 秒等待的 `ResetGameAsync(...)`，
  没有同步声明“当前尝试正在切换”；旧关卡的延迟成功回调仍可在等待窗口内进入 Victory 结算。
- 原因二：旧 `DrawGame_GamePlayPanel` 直接订阅 `DrawGameResetEvent` 和 `GameReplayEvent`，不关心
  `DrawPlayCtrl` 是否接受请求。残留 VictoryPanel 派发 Replay 时，新关卡已经回到 `Drawing`，控制器拒绝
  Replay，但新 HUD 仍单方面执行黑幕淡入，因此没有后续 `GamePlayPanelFadeInEvent` 与淡出。

### 47.2 修复行为与边界

- `DrawPlayCtrl` 新增当前尝试切换门禁。Reset/Replay 通过活动状态、运行时、加载状态和阶段校验后立即
  设置门禁，取消尚未完成的 Victory 异步流程，再开始原有 analytics、广告和异步重建链。
- `OnLevelResolved(...)` 与 VictoryPanel 创建前的最终校验都会拒绝切换门禁期间到达的旧关卡结果；
  Reset 的玩家意图优先，不记录该过期 Victory 的通关、星级、每日挑战或成功结算。
- `DrawGame_GamePlayPanel` 不再订阅 Reset/Replay 事件。`DrawPlayCtrl` 只在接受请求后通过当前持有的
  `_gamePlayPanel` 局部引用调用 `BeginResetTransition()`，不新增全局事件，也不修改共享主线
  `GamePlayPanel`。
- 新关进入、控制器激活/停用、重建完成或打开失败时会恢复门禁；正常 Reset/Replay 仍保持原来的
  0.5 秒黑幕等待、整关与 HUD 重建、`PlayResetIntroAsync()`、淡入和输入恢复顺序。
- 有效 Reset/Replay 的失败结束、重试次数、Replay 开始、插屏策略和存档语义保持不变；本次不修改
  Luban、Prefab、资源地址、DB 结构、SDK 接口或 Android/iOS 平台分支。

### 47.3 影响范围、验证、风险与回滚

- 修改文件：`Assets/Game/Scripts/Play/DrawPlayCtrl.cs`、
  `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_GamePlayPanel.cs`。
- 静态验证：确认 HUD 不再订阅 `DrawGameResetEvent` / `GameReplayEvent`，黑幕入口仅由
  `DrawPlayCtrl` 两个已校验处理器调用；`OnLevelResolved` 和 VictoryPanel 最终创建校验均包含切换门禁。
- Unity CLI 触发整项目重新编译后，`recompile_status` 返回 `completed`、`failed: false`、`errors: []`；
  Console 未出现本次修改文件相关错误。Console 中仍有一条早于本次重编译的 Reporter `build_info`
  文件占用错误，与本次 Draw 流程修改无调用关系。
- `uncertain`：尚未通过自动化无侵入地完成本节精确竞态步骤、普通手动 Reset、正常胜利 Replay、连续
  点击 Reset、Victory 动画完成前后 Reset、Android/iOS 横屏真机触控、低帧率下 0.5 秒竞争窗口和
  切后台恢复验证；本次不新增逐帧逻辑或资源实例，性能影响仅为常量级布尔门禁与一次局部 HUD 方法调用。
- 回滚：删除 `DrawPlayCtrl` 的尝试切换门禁与局部 HUD 调用，并恢复 HUD 对两个事件的订阅和处理方法；
  无需迁移配置、资源或玩家存档。

## 48. 2026-08-03 失败插屏返回后仍显示死亡 Actor 修复

### 48.1 需求、基线与直接原因

- 角色因陷阱死亡时，`ActorCtrl.Die()` 会立即写入 `IsDied=true`；死亡演出结束后，Draw 失败链派发
  `DrawGameResetEvent`。旧流程在 `DrawPlayCtrl.OnGameResetEvent()` 中先请求失败插屏，再并行启动
  `ResetGameAsync()` 的 0.5 秒遮罩等待与整关重建。
- Android/iOS 原生广告覆盖层可能暂停 Unity PlayerLoop；`ignoreTimeScale:true` 只能绕过
  `Time.timeScale`，不能在没有 Unity 帧更新时推进等待。因此广告返回时旧 DLevel 尚未销毁，玩家会再次
  看到 `IsDied=true` 的旧 Actor，之后 Reset 才继续。
- 0.5 秒等待继续保留：旧 HUD 黑幕淡入为 0.4 秒，该窗口用于在销毁旧 HUD、旧 DLevel 和异步创建
  新实例前完成遮挡，避免加载期间露底；本次不把固定等待扩展为新的 HUD 动画完成接口。

### 48.2 修复后流程与系统边界

- `OnGameResetEvent()` 仍按原顺序记录失败/重试 analytics 和 Draw 插屏策略计数，但不再立即显示广告；
  它关闭输入后把“需要失败插屏”作为本次 Reset 重建链的局部参数传入。
- 新顺序为：旧 HUD 黑幕淡入并等待 0.5 秒 → 销毁旧关和旧 HUD → 创建全黑的新 HUD、新 DLevel 和
  存活 Actor → 完成 `PlayResetIntroAsync()` → 请求并等待现有 `InterstitialAdHelper` 结果 → 校验活动
  版本 → 派发 `GamePlayPanelFadeInEvent` → 恢复输入。
- 策略拒绝、SDK 未初始化或广告展示失败继续使用现有 Helper 结果回调，直接进入黑幕淡出；展示成功时
  Helper 仍只更新请求发起时捕获的 Draw Policy。Victory 插屏和 Victory Replay 不改变时序。
- 修改文件仅为 `Assets/Game/Scripts/Play/DrawPlayCtrl.cs` 及本地文档/规则；不修改 `ActorCtrl`、
  `InterstitialAdHelper`、SDK、Prefab、Luban、DB、资源地址或平台分支，也不新增全局事件。

### 48.3 验证、风险与回滚

- Unity CLI 整项目重编译完成，`recompile_status` 返回 `completed`、`failed:false`、`errors:[]`；静态
  调用链确认失败插屏只存在于新实例 Reset Intro 完成后的等待点，胜利插屏保持原入口。
- `uncertain`：尚未使用 Android/iOS 真实插屏验证 SDK 成功、拒绝、加载失败、切后台、横屏恢复和回调
  线程；尚未完成陷阱死亡、FallArea 失败、手动 Reset、连续 Reset、退出 Draw 和广告期间切应用的 PlayMode/
  真机回归。SDK 若违反现有接口约定且永不回调，新的全黑 HUD 会继续保持输入锁，此风险需真实 SDK 验证。
- 性能影响为每次有效失败/手动 Reset 创建一个短生命周期 `UniTaskCompletionSource`；不新增逐帧轮询、
  场景扫描、资源实例或存档写入。
- 回滚时恢复 `OnGameResetEvent()` 的即时 `TryToShowInterstitial()`，移除 Reset/Reload 的局部布尔参数和
  `WaitForFailureInterstitialAsync()`；无需迁移资源、配置或玩家数据，但会恢复广告返回露出死亡 Actor 的问题。

## 49. 2026-08-03 DrawLineFeature 多 TipsObject 兼容控制

### 49.1 需求与原行为基线

- 需求：同一 Draw 关卡允许配置并统一展示多个 TipsObject；“保留原 TipsObject”指把已有对象引用
  迁入新列表，而不是同时保留新旧两套序列化字段。
- 原实现只有单个 `tipsObject` 和单个 `_initialHintVisible`；`ShowHint(...)`、Attempt Reset 与编辑器
  自动绑定都只处理一个对象，关卡新增第二份提示时无法进入同一控制链。
- 兼容边界：现有 Prefab 中的对象引用不能丢；没有第二个提示的关卡必须保持原表现。

### 49.2 修改后行为与配置边界

- `DrawLineFeature` 删除旧 `tipsObject` 与过渡用 `additionalTipsObjects`，只保留单一
  `tipsObjects` 列表。`ShowHint(bool)` 只遍历该列表；空引用和重复引用会被安全处理。
- 运行时初始化会按对象分别缓存 `activeSelf`，Attempt Reset 逐个恢复各自初态，不会用原主对象的初态
  覆盖其它提示对象。该缓存只保存引用和布尔值，不新增逐帧扫描或实例化。
- 运行时兜底只在列表没有有效引用时追加直接子节点 `TipsObject`；编辑器 AutoBind 在空列表时收集层级内
  全部 `TipsObject` / `DrawGame_TipsObjectSubObject`（含复制后缀），不覆盖人工列表。
- 20 个现有 DLevel Prefab 已全部完成显式迁移：原提示位于 `tipsObjects[0]`。DLevel_009 与
  DLevel_011 各自保留第二个提示，因此列表各有两个不同对象；不存在旧字段或重复迁移项。
- 数据/资源/UI/事件：不读取 DB、Luban 或存档，不加载或实例化新资源，不修改 GamePlayPanel，
  不新增全局事件；提示显隐仍由现有 Draw 提示入口调用。Android/iOS 使用相同的常量级 SetActive 路径。

### 49.3 验证、风险与回滚

- 静态检查 20 个现有 `DLevel_*.prefab`：全部存在非空 `tipsObjects`，旧 `tipsObject` 与
  `additionalTipsObjects` 计数均为 0；DLevel_009、DLevel_011 各为两个不同引用。
- Unity 当前生成的 rsp 尚未包含随后新增的 `TagPressButton.cs`；在同一 Assembly-CSharp rsp 基础上
  显式补入该新源文件后，Roslyn 全量编译通过，结果为 `0 error`，warning 均为项目既有项。
- `uncertain`：尚未执行 Unity PlayMode、真实提示按钮入口、连续 Show/Hide、Attempt Reset、Replay、
  Android/iOS 横屏与低帧率验证。主要剩余风险是关卡配置遗漏附加对象，而不是运行时扩展成本。
- 回滚时需同时恢复旧单字段代码，并把 20 个 Prefab 的 `tipsObjects[0]` 写回旧字段；DLevel_009、
  DLevel_011 的第二项会失去共享控制能力。无需迁移资源、配置或存档。

## 50. 2026-08-04 Draw 玩家 Actor 皮肤所有者接入

- 原行为：`ActorSkinBinding` 默认只接受 `BaseLevelCtrl.Actor`，Draw 关卡不存在该控制器，因此
  Draw 玩家 Actor 的默认皮肤绑定不会通过身份校验。
- `DrawLevelCtrl` 现在实现 `IActorSkinBindingOwner`，公开只读 `Actor`；属性延迟复用
  `ResolveReferences()`，保证 `ActorSkinBinding.OnEnable()` 发生在 Draw 运行时初始化之前时也能取得
  当前 DLevel 受控 Actor。
- 该接口只声明身份，不由 DrawLevelCtrl 读取 DB、加载资源、订阅换肤事件或实例化部件；具体皮肤流程
  继续由 `ActorSkinBinding` 和 `SkinUtils` 负责。
- 多 Actor DLevel 仍沿用 DrawLevelCtrl 原有的首个 `ActorCtrl` 解析边界；其它 Actor、敌人或工具不会
  因同处 DLevel 而自动通过 `owner.Actor == actor` 校验。
- 不修改 prefab、Draw 存档、Luban、资源地址、UI、analytics、广告、IAP 或平台逻辑，不新增全局事件
  或逐帧扫描。
- C# 编译通过，结果为 `0 error / 101 warning`；PlayMode、Reset/Replay、多个 Actor DLevel 和移动端
  横屏验证仍为 `uncertain`。完整结构、风险和回滚见
  `Assets/Game/Scripts/Level/Actor/ActorSkinBinding功能与风险评估.md`。

## 51. 2026-08-04 奖励墨水同关重建的真实容量一致性

### 51.1 需求与原行为基线

- 用户最终确认：领取奖励墨水后，同关 Reset/失败重载/胜利 Replay 要保留真实的 +100% 容量；
  直接下一关属于新关卡周期，必须清除奖励并恢复基础容量。
- 原实现只在 `DrawPlayCtrl` 保留领取布尔值并恢复 HUD 长状态；新 DLevel 初始化会把本实例
  `_bonusInk` 归零，因而表现、广告门禁和实际容量不一致。

### 51.2 修改后流程与系统边界

- 同关新 `IDrawLevelRuntime` 初始化成功后，`DrawPlayCtrl` 根据领取门禁调用一次
  `AddInk(DrawInkState.MaxInk)`，再把最终墨水快照同步给 HUD。
- `TryEnterLevel(...)` 清除奖励门禁，因此直接下一关只使用新关基础 MaxInk，并重新开放 RewardAD；
  `DrawGame_ChoiceLevelPanel` 和 `Deactivate()` 也是清零边界。Reset/Replay 不经过新关入口，保留门禁。
- 首次广告发奖和新实例恢复都检查 `AddInk(...)` 的实际返回值；恢复失败时撤销门禁与 HUD 长状态，
  优先保证可见表现、广告资格和运行时容量一致。
- 影响文件为 `Assets/Game/Scripts/Play/DrawPlayCtrl.cs` 及本地记录；不修改关卡 prefab、
  `DrawLineFeature`、DB、Luban、资源、全局事件、analytics、广告位或平台分支。

### 51.3 验证、风险与回滚

- 静态验证确认新关清零入口、同关新运行时恢复时序及直接下一关调用链符合上述边界；
  `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal`
  通过，结果为 `0 error / 101 warning`，相关文件 `git diff --check` 通过。
- `uncertain`：Unity PlayMode 的手动 Reset、自动失败、Replay、Next、选关返回、StartPanel 返回、
  真实 RewardAD 及 Android/iOS 横屏尚未执行。
- 回滚：删除 `RestoreRewardInkForNewRuntime()` 及调用；无需迁移玩家数据，但会恢复同关重建时
  HUD 与实际奖励容量不一致的问题。

## 52. 2026-08-05 PencilSkinConfig ID 来源开关

### 52.1 需求与原行为基线

- 需求：为 `DrawLineFeature` 的 PencilSkinConfig ID 增加默认关闭的 Inspector 开关；关闭时使用
  `DB.DrawGame.CurrentSkinId`，开启时才使用 Inspector 上的 ID 读取配置。
- 原行为始终优先读取 `DB.DrawGame.CurrentSkinId`；DB ID 为空或对应配置无效时，会使用 Inspector
  `brushStyleId` 作为回退。因而 Inspector 字段不是独立测试模式，DB 模式仍可能隐式消费该值。
- 影响所有使用共享 `DrawLineFeature` 的 DrawGame 关卡，但本次不改写任何 DLevel prefab；新布尔字段
  在旧 prefab YAML 中不存在时按 C# / Unity 缺省值保持 `false`。

### 52.2 修改后行为与数据流

- 新增序列化字段 `useInspectorPencilSkinConfigId=false`，并补充中文 Tooltip 与调用边界说明。
- 开关关闭：只读取 `DB.DrawGame.CurrentSkinId`，忽略 Inspector `brushStyleId`；玩家在 PencilShop
  选择并保存新 ID 后，下一笔仍通过现有同步入口加载对应配置。
- 开关开启：只读取 Inspector `brushStyleId`，忽略 DB 当前选择，供单关或测试环境固定验证指定笔刷。
- 当前来源为空时归一化为固定默认 ID `301001`；当前来源的配置加载失败、校验失败或内部 BrushId
  不匹配时，也只回退 `301001`。DB 模式不会再回退到 Inspector ID，Inspector 模式也不会回退到 DB。
- 资源地址仍为 `PencilSkinConfig/PencilSkin_{id}`，继续使用 Bear `ResourceManager`；只在初始化及新笔
  落笔前同步，ID 未变化时不重复加载。材质、Gradient、线宽、墨水、碰撞、事件和已完成线条不变。
- 本次不写 DB、不调用 Save、不修改 PencilShop、Luban、资源资产、prefab、RemoteConfig、analytics、
  广告、IAP 或平台分支，也未增加 Editor 生命周期入口。

### 52.3 验证、风险与回滚

- 静态检查确认 `useInspectorPencilSkinConfigId` 唯一决定 ID 来源；旧的 Inspector 回退方法已移除，
  默认资源回退固定为 `301001`。
- 静态确认现有 DLevel prefab 只序列化 `brushStyleId`，均未序列化新开关，因此当前所有既有关卡默认
  继续使用 DB 来源；本次未修改这些 prefab。
- 使用 Unity 6000.0.59f2 自带 Roslyn 与当前 Bee `Assembly-CSharp.rsp` 编译完整程序集，结果为
  `0 error`；输出 warning 属于工程既有范围。目标代码 `git diff --check` 通过，仅有 LF/CRLF 提示。
- `uncertain`：尚未在 Unity Inspector 确认字段显示，也未在 PlayMode 覆盖 false + 有效/空/无效 DB ID、
  true + 有效/空/无效 Inspector ID、运行中 PencilShop 切换、Reset/Replay 及 Android/iOS 横屏资源加载。
- 回滚：移除 `useInspectorPencilSkinConfigId`，恢复 DB 优先且 Inspector ID 作为空值/无效配置回退的
  `SyncBrushStyleFromCurrentSelection()`；无需迁移存档、资源或 prefab。

## 53. 2026-08-05 Draw 进关关闭启动 Camera

### 53.1 需求与原行为基线

- 需求：进入具体 DrawGame 关卡后关闭 `GameManager` 持有的启动 Camera，避免它与 DLevel 内
  `GameCamera` 同时提供 `AudioListener`。
- 修改前主线 `PlayCtrl.CreateLevelAsync(...)` 会调用 `GameManager.CloseCamera()`，但
  `DrawPlayCtrl.OpenDrawLevelAsync(...)` 直接实例化 DLevel；现有 `GameCamera.prefab` 自带启用的
  `AudioListener`，因此 Draw 进关路径缺少与主线一致的启动相机关停边界。

### 53.2 修改后流程与系统边界

- `DrawPlayCtrl.OpenDrawLevelAsync(...)` 在资源和 `Scene` 根节点校验通过后、实例化 DLevel 前调用
  `GameManager.CloseCamera()`；关卡自己的 `GameCamera` 随后成为唯一预期的活动相机/监听器。
- `OpenLevelSelection(...)` 在创建或复用 Draw 选关面板前调用 `GameManager.OpenCamera()`，覆盖首次进入
  Draw、进关失败降级、胜利后返回选关等路径。下一关与同关 Reset/Replay 不经过选关恢复入口，启动
  Camera 保持关闭，避免切关期间短暂重开。
- 复用既有 `GameManager.OpenCamera()` / `CloseCamera()`，不新增全局事件、组件扫描、逐帧轮询、存档、
  配置、资源地址、analytics、广告、IAP 或平台分支；不修改任何已验收 DLevel prefab 或关卡脚本。

### 53.3 验证、风险与回滚

- 静态调用链确认：首次 Draw 进关在 `Instantiate` 前关闭启动 Camera；加载失败进入选关时恢复；下一关和
  Reset/Replay 保持关闭；主线既有 `PlayCtrl` 相机控制未修改。
- 待完成：C# 编译、Unity PlayMode 的首次进关/返回选关/加载失败/下一关/Reset/Replay，以及 Console
  不再出现双 `AudioListener` 警告；Android/iOS 横屏行为暂标记为 `uncertain`。
- 回滚：删除 `OpenDrawLevelAsync(...)` 的 `CloseCamera()` 和 `OpenLevelSelection(...)` 的
  `OpenCamera()`，并把 `Activate()` 中原有的 `OpenCamera()` 恢复到选关调用前；无需迁移资源或数据。

## 54. 2026-08-05 Draw 有门成功的开关门、ribbon 与胜利彩纸表现

### 54.1 需求、基线与影响范围

- 需求：DrawGame 中使用标准成功门的关卡，通关时参考主线 `SuccessAnimCtrl` 补齐开关门音效、
  `ribbon` 和 `ConfettiBlastRainbow` 胜利彩纸表现。
- 修改前 `DrawLevelCtrl` 只播放 `door_open` 与 `openDoor`，同时让 Actor 在原地渐隐；随后直接通知
  `DrawPlayCtrl` 成功结算，不会启用 `ribbon`、播放 `dose_close` 或 `closeDoor`。
- 影响文件为 `DrawLevelCtrl.cs`、新增 `DrawSuccessDoorEffectCtrl.cs` 及共享源 prefab
  `DrawGame_SuccessDoor.prefab`。静态扫描确认当前 32 个非空 `successDoorSpine` 绑定均来自该共享资源，
  其中 21 个关卡的标准成功门当前为 active；其余绑定属于 inactive 的无门/legacy 关卡。DLevel039 的
  legacy 成功门同样保持 inactive 且字段为空，不属于当前有门成功链。本次不修改任何 DLevel prefab 或
  已验收关卡局部脚本。

### 54.2 修改后表现与边界

- 有门成功入口仍先由 `TryBeginSuccessPerformance()` 锁定结果、关闭画线输入和 HUD 交互；
  `DrawLevelCtrl` 只并行等待 Actor 渐隐和成功门局部表现，二者完成后调用
  `CompleteSuccessPerformance()` 通知 `DrawPlayCtrl`。
- 新增 `DrawSuccessDoorEffectCtrl`，职责限定为当前门的 Spine、音效、`ribbon`、胜利彩纸、时序和取消清理：播放
  `door_open`、`openDoor` 并立即启用 `ribbon`；至少保持 1.6 秒后隐藏，等待 0.2 秒播放
  `dose_close`，再等待 0.25 秒播放 `closeDoor` 并启动 `ConfettiBlastRainbow`。彩纸按主线结算边界保留
  0.8 秒可见时间，并与关门动画剩余时长并行等待，二者都完成后才允许提交成功。
- 组件挂在共享 `DrawGame_SuccessDoor.prefab` 根节点，`doorSpine`、`ribbon` 和彩纸根 `ParticleSystem`
  均使用源 prefab 的序列化引用，不进行运行时跨关卡扫描。该彩纸资源原已嵌套在标准门内且
  `playOnAwake=false`；现在由组件显式停止清空后重播。`DrawLevelCtrl` 继续利用既有 `successDoorSpine`
  定位同门根节点上的组件；Reset 或门对象禁用时会取消未完成时序、隐藏 `ribbon` 并清空所有彩纸子粒子，
  结果版本检查继续阻止旧尝试提交成功。
- 无门成功入口、失败/Reset、Actor 渐隐、成功结果事件、胜利面板、星级/存档、analytics、广告、资源
  地址和平台分支保持原边界。未复用 `SuccessAnimCtrl` 的 `enterDoor`、角色销毁、震动或回调所有权。

### 54.3 验证、风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore ...` 完整构建结果为 `0 error / 101 warning`，新脚本已
  纳入 `Assembly-CSharp.csproj`；warning 属于工程既有范围。共享 prefab 的脚本 GUID、组件 fileID、
  `doorSpine`、`ribbon` 与 `ConfettiBlastRainbow` 根粒子本地引用已做静态一致性检查；
  `git diff --check` 仅报告 Unity 自动序列化新增 `MonoBehaviour` 时生成的两个空字段尾随空格。
- 静态证据确认标准成功门存在 inactive 的 `ribbon` 和已关闭 `playOnAwake` 的嵌套
  `ConfettiBlastRainbow`，门 Spine 使用 `door_open`；主线参考实现与声音表均存在 `dose_close`、
  `openDoor`、`closeDoor`。32 个非空绑定关卡均引用同一标准成功门资源，当前 active 标准门为 21 个。
- `uncertain`：尚未执行 Unity PlayMode，需验证 ribbon 与彩纸的层级/排序、1.6 秒及 0.8 秒节奏、关门动画
  完整可见、连续通关/Reset 中断、暂停，以及 Android/iOS 横屏音画同步、粒子性能和低帧率表现。
- 回滚：从共享 `DrawGame_SuccessDoor.prefab` 移除 `DrawSuccessDoorEffectCtrl`，删除新增脚本及 meta，并将
  `DrawLevelCtrl` 恢复为直接播放开门与 Actor 渐隐后结算；无需迁移配置、存档或线上数据。

## 55. 2026-08-05 DLevel_999 播放 TBC 音效

### 55.1 需求与基线

- 需求：进入 Draw 的 `DLevel_999` 时播放主线最后一关相同的 `TBC` 音效。
- 主线基线位于 `PlayCtrl.RefreshGamePanel()`：GameplayPanel 创建并设置关卡后，`Level.IsLastLevel`
  为 true 时调用 `AudioManager.PlaySound("TBC")`。
- Draw 修改前仅在 `BeginDrawing()` 保证 `musicInGame` 背景音乐，没有 TBC SFX 调用。

### 55.2 修改后行为与影响范围

- `DrawPlayCtrl.TryCreateGamePlayPanel()` 在 HUD 创建成功并设置显示 ID 后，检查 `TryEnterLevel()` 已保存的
  标准化资源路径；仅当它等于 `Assets/Game/Prefabs/DrawLevel/DLevel_999.prefab` 时调用
  `AudioManager.PlaySound("TBC")`。
- 第一版曾通过原始 `TbDrawLevelSort` 反查当前数据，但 Editor 日志确认运行时追加的 `sortId:45` 不在原始表，
  输出 `DrawLevelData is missing` 并导致门禁不命中；当前门禁已与原始排序表解耦。
- 使用实际资源路径而不是“当前排序末项”限定需求，避免远端排序缺项或临时顺序变化时让普通关误播 TBC。
- Reset/Replay 重建 HUD 时会再次播放，与主线 `RefreshGamePanel()` 的生命周期一致。
- `SoundCollection.asset` 已存在 `TBC` 标签及 AudioClip；`AudioManager` 继续负责 SFX 开关门禁。
- 不修改 `DLevel_999.prefab`、背景音乐、输入、Intro、结算、配置内容、存档、事件、Analytics、广告或平台 SDK。

### 55.3 验证、风险与回滚

- 静态确认 `tbdrawleveldata.json` 为 `20999 -> DLevel_999`，`SoundCollection.asset` 的 `TBC` 标签绑定一个 AudioClip。
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 --nologo` 成功，`0 error / 101 个项目既有 warning`；
  Unity PlayMode、Reset/Replay、SFX 关闭及 Android/iOS 横屏听感为 `uncertain`。
- 既有 Analytics 当前关查询仍会对运行时追加 `sortId:45` 输出缺失警告，但不再阻断 TBC 音效；其修复不在本需求范围。
- 回滚时删除 `DrawPlayCtrl` 的 `TbcLevelResourcePath`、`IsTbcLevel()` 和 HUD 创建后的 TBC 分支；无需迁移资源、配置或存档。

## 56. 2026-08-06 Draw 跨玩法累计关卡序号与 b_play 对齐

### 56.1 需求、原行为与影响范围

- 需求：DrawGame 的 `level_start.level`、`level_end.level` 使用主线历史成功次数与 DrawGame 历史成功次数之和，并让 `b_play` 同样累计 DrawGame 通关。
- 修改前 Draw 事件的 `level` 使用 `DrawLevelData.Id`；`UpdateBPlay()` 只统计主线 `DB.GameData.LevelSuccessCount`，Draw 通关后也没有立即刷新 `b_play`。
- 影响文件为 `GameSDKService.Game.cs`、`GameSDKService.UserProperty.cs` 和 `DrawPlayCtrl.cs`。不修改主线 `level_start` / `level_end` 的既有字段计算，不新增存档字段、事件名、配置、资源或平台分支。

### 56.2 修改后数据与事件流

- `GetCombinedLevelPassedCount()` 复用主线 `GetLevelPassedCount()`，再累加 `DB.DrawGame.LevelSuccessCount` 的全部值；Draw 存档或成功字典不可用时退化为主线累计值，不写存档。
- Draw `level_start.level` 使用“主线累计成功次数 + Draw 累计成功次数 + 1”。失败或退出的 `level_end.level` 使用同一公式。
- Draw 成功流程仍先由 `RecordLevelSuccessAnalytics()` 将当前 Draw 关成功次数加一并保存，再发送成功结束事件；因此成功 `level_end.level` 直接使用此时两种玩法成功次数之和，与本局 `level_start.level` 保持一致。
- `UpdateBPlay()` 改为上报两种玩法成功次数之和。Draw 成功 `level_end` 发送后立即调用一次 `UpdateBPlay()`；应用启动和主线既有刷新入口也会读取同一合计值。
- `level_name`、`item_category=draw`、`play_count`、时长、重试次数和结果映射保持不变；
  本节完成时 Draw 不发送 `level_end_success_N`，该历史边界已于 2026-08-07 按新要求替换，见第 60 节。
- 主线成功结算派生的 `level_end_success_N` 事件从 2026-08-06 起使用两种玩法成功次数之和生成 `N`，其事件参数 `level` 仍为当前实际主线关卡 ID。2026-08-07 起 Draw 成功也直接发送同名派生事件；`N > 50` 时不上报的限制按合计次数判断。

### 56.3 验证、风险与回滚

- 精确调用扫描确认 Draw 专用开始方法有两个调用点、结束方法有一个调用点，均已同步新签名；目标文件 `git diff --check` 未发现空白错误，仅输出工作区换行符提示。
- 已执行公式矩阵：主线成功 10 次、Draw 成功 3 次时，本局开始、失败结束均为 14；本局成功写入 Draw 第 4 次后，成功结束与 `b_play` 均为 14。
- 静态核对主线成功调用链：本次主线成功次数写入后，`LevelEndEvent` 使用跨玩法合计次数生成 `level_end_success_N`，并继续把当前主线 `levelId` 作为事件参数；尚未在真实 SDK 后台验证事件名。
- 已执行 `dotnet build Assembly-CSharp.csproj --no-restore -m:1 --nologo`，构建成功，结果为 `0 error / 1119 warning`；warning 为当前工程既有项。
- `uncertain`：尚未在 Unity PlayMode、真实 SDK 后台、Android 或 iOS 验证跨玩法切换后的事件值、用户属性刷新时序和事件去重。
- 回滚时恢复 Draw 事件以 `DrawLevelData.Id` 作为 `level`，恢复 `UpdateBPlay()` 和 `level_end_success_N` 只调用 `GetLevelPassedCount()`，并移除 Draw 成功结束后的 `UpdateBPlay()`；无需迁移存档或配置。

## 57. 2026-08-06 主线与 DrawGame revive 数据口径对齐

### 57.1 需求、原行为与影响范围

- 最终确认口径：业务操作 Replay 只指 VictoryPanel 的 Replay 按钮；失败或手动 Reset 会累计 revive，发送 `level_end(fail)` 后清理本次打点计时和尝试级提示显示状态、增加 playCount 并重新发送 `level_start`，保持开始与结束事件配对。自 2026-08-18 起，Draw 当前关已付费或广告获得的提示资格不属于打点状态，同关 Reset 额外保留该资格并恢复显示。
- 修正前 Reset 会累计一次 revive 后立即发送 `level_end(fail)`，随后清零运行态、增加 `LevelPlayCount` 并发送 `level_start(replay)`；因此连续 Reset 的 revive 被拆到多条失败结束事件，最终成功事件通常得到 `revive=0`。
- 本次修正 `PlayCtrl.cs` 与 `DrawPlayCtrl.cs` 的 Reset/Replay 调用顺序，并同步本地文档；不修改 `LevelRuntimeState` 原有计数方法、SDK 事件字段、关卡配置、资源、UI、广告或平台分支。

### 57.2 修改后数据、存档与事件流

- 主线 Reset 仍调用 `RecordReset()` / `RecordFail()`，因此继续累计 `LevelRuntimeState.ResetCount + FailCount` 并保留 `DB.GameData.LevelReviveCount` 历史写入；发送 `level_end(fail)` 后，仅调用新增的 `ResetDataPreservingRevive()` 清理计时与提示状态，再增加 `LevelPlayCount`、发送新的 `level_start` 并重建关卡。原 `ResetData()` 的全量清理语义没有改变。
- DrawGame Reset 仍调用 `RecordLevelRetryAnalytics()`，因此 `_analyticsReviveCount` 和 `DB.DrawGame.LevelReviveCount` 都加一；随后调用 `ReportLevelEndAnalytics(fail)`，清理 `_analyticsCurrentLevelTimeSeconds`，再增加 `DB.DrawGame.LevelPlayCount`、发送新的 `level_start` 并重建关卡。`_analyticsReviveCount` 与当前关 `_hasGrantedHint` 保留，提示显示状态从资格重新派生。
- Draw 成功 `level_end` 完成后清理时长、revive 与尝试级提示显示状态，但保留当前关提示资格；VictoryPanel Replay 重新派生显示状态。进入新 Draw 关卡、返回选关页或退出玩法时再同时清除提示资格和显示状态。
- 主线与 Draw 的最终成功或退出 `level_end.revive` 分别读取当前周期的 `ResetCount + FailCount` 与 `_analyticsReviveCount`。例如同一周期 Reset 三次后成功，成功结束事件发送 `revive=3`。
- 成功结算先发送 `level_end(success)` 及同步派生打点，再清零本关累计时长与 revive；VictoryPanel Replay 随后增加一次 `LevelPlayCount` 并发送新的 `level_start`，Replay 本身不调用 revive 记录方法，不增加历史 `LevelReviveCount`。
- 业务 Replay 与 `start_type` 判定分离：主线与 Draw 的每一次 `level_start`（包括普通进入、Reset、选关重进和 VictoryPanel Replay）都按当前关卡是否已存在于各自 `PassedLevels` 判定；已通关传 `replay`，未通关传 `play`。
- 主线 Reset 与 Replay 继续复用 `LevelRuntimeState.IsAttemptTransitioning` 防重，Draw 继续使用 `_isAttemptTransitionPending`；历史 revive 字段继续保留，但不参与开始事件、灯泡事件或结束事件字段组装。
- 灯泡获取与消费事件的 `revive` 继续读取主线当前 `LevelRuntimeState.ResetCount + FailCount`，不读取历史 `GameData.LevelReviveCount`。

### 57.3 验证、风险与回滚

- 静态调用目标：主线和 Draw Reset 路径均记录 revive，依次发送结束打点、增加 playCount 并发送开始打点，但不清理周期计数；两个 Victory Replay 路径不记录 revive，只增加 playCount 并发送开始事件。
- 未通关关卡的事件顺序目标：初次 `level_start(play, play_count=1)` → Reset 第一次发送 `level_end(fail, revive=1)`、`level_start(play, play_count=2)` → 后续 Reset 继续成对发送且 revive 累计 → 成功 `level_end(success, revive=3)` → 清理周期数据 → VictoryPanel Replay `level_start(replay, play_count=5)` → 新一轮成功 `level_end(success, revive=0)`。
- 主线连续 Reset/Replay 在 `IsAttemptTransitioning=true` 时直接忽略，Draw 继续使用既有 `_isAttemptTransitionPending` 门禁。
- 最终 `git diff --check` 通过；`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过，结果为 `0 error / 101 warning`，warning 为工程既有项。
- `uncertain`：尚未在 Unity PlayMode、真实 SDK 后台、Android 或 iOS 验证连续 Reset、成功、胜利 Replay、退出与切关的实际事件序列和值。
- 已知边界：Reset 的 `level_end(fail).revive` 与最终成功事件的 revive 都使用本关累计值，因此多次 Reset 时依次为 1、2、3，最终成功仍为 3；每次结束的 duration 则只覆盖对应 start 周期。BI 聚合时不得把 revive 值直接求和作为实际重试次数。
- 回滚本次修正时移除 Reset 路径的 `level_end(fail) -> playCount+1 -> level_start` 与成功结束后的周期清理，并恢复原 `start_type` 判断；无需迁移存档。

## 58. 2026-08-06 Draw 胜利面板免广告购买入口

### 58.1 需求、原行为与影响范围

- 需求：普通与首次奖励 Draw 胜利面板都启用现有 `noAd_btn`，购买语义与主线一致；Reward
  通过 `DrawGame_GameVictoryPanel` 基类复用，不在子类复制购买流程。
- 原行为：普通面板 prefab 有按钮资源但根脚本未绑定；Reward 根脚本持有引用却主动隐藏，
  两个入口均不能发起购买。
- 影响 Draw 胜利面板基类、Reward 子类和普通 prefab；不修改关卡结算、下一关/重玩事件、
  Draw 插屏自动弹窗参数、RemoteConfig、商品表、存档结构或主线胜利面板。

### 58.2 修改后流程与系统边界

- 基类统一执行商品查询、`IAP_CLK`、`PurchaseManager.Purchase`、成功/失败结算、权益复核和
  按钮显隐。购买期间同时锁定下一关与重玩，避免原生购买覆盖层期间触发结算导航。
- Reward 继续用 `HasExtraBusyState()` 把奖励广告请求/播放状态与免广告购买互斥，并移除打开
  时隐藏按钮的旧逻辑；Postcard 引导遮罩仍由 Reward 子类关闭。
- 权益判断与主线一致：优先检查 SDK 商品拥有状态，再检查 Bag 中 `GameProps.NoAds`；没有
  新增 Draw 专属购买商品或控制参数。

### 58.3 验证、风险与回滚

- CodeGraph 已核对主线购买链和 Draw 继承边界；静态 YAML 已确认普通/Reward 两个 prefab
  均绑定 `UICommon_btn` 的 `CustomButton`。
- 本次目标文件执行 `git diff --check`；定向 `Assembly-CSharp` 构建通过，结果为
  `0 error / 101 warning`，warning 为工程既有项。
- `uncertain`：仍需 Unity PlayMode、Android/iOS 真机验证购买成功/失败/取消、切后台、输入
  恢复、按钮隐藏和 Reward 奖励广告互斥。
- 回滚时删除基类购买链与普通 prefab 绑定，并恢复 Reward 打开时隐藏按钮；无需迁移存档。

## 59. 2026-08-06 Draw level_end 历史累计时长对齐主线

### 59.1 需求、原行为与影响范围

- 需求：DrawGame `level_end.total_duration` 改为当前关卡历史累计游玩时长，记录顺序和主线一致。
- 修改前 Draw 每次结束只把当前 start/end 区间时长同时传给 `duration` 与 `total_duration`，
  `DB.DrawGame.LevelPlayDuration` 虽已存在但没有写入。
- 影响 `DrawPlayCtrl.cs`、`DrawGame.cs` 与关卡打点文档；不修改事件名、`level`、`level_name`、
  revive、结果映射、Reset/Replay 配对、配置、资源或平台分支。

### 59.2 修改后数据与事件流

- `ReportLevelEndAnalytics()` 继续把当前 start/end 区间计时转换为毫秒，作为 `duration`。
- 与主线 `LevelEndEvent()` 一致，在发送每一条 Draw `level_end` 前，先以
  `DrawLevelData.Path` 为键把本次 `duration` 累加到 `DB.DrawGame.LevelPlayDuration`，调用
  `DB.SaveDrawGame()` 保存，再读取更新后的累计值作为 `total_duration`。
- Reset 的失败结束、成功结束、退出结束都会各自把对应区间时长累计一次；结束后既有计时清理保证
  下一条 start/end 区间不会重复累计。历史累计值不会因 Reset、成功结算、Replay 或切关而清空。
- 旧版本没有写入 Draw 历史时长，无法从现有数据反推补齐；升级后从存档中已有值（通常为零）继续累计。

### 59.3 验证、风险与回滚

- 静态核对目标顺序为“计算本次毫秒时长 → 累加并保存历史值 → 读取累计值 → 发送结束事件”，
  与主线顺序一致；例如历史 `5000`、本次 `2000` 时上报 `duration=2000`、`total_duration=7000`。
- `git diff --check` 通过；`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo`
  通过，结果为 `0 error / 101 warning`，warning 为工程既有项。
- `uncertain`：尚未在 Unity PlayMode、真实旧档、真实 SDK 后台、Android/iOS 验证连续 Reset、成功、
  退出和进程中断时的累计值与存档落盘结果。
- 若 `DrawGame` 存档或关卡路径缺失，当前事件仍会上报本次 `duration` 作为降级的
  `total_duration` 并输出警告；若保存失败，内存累计值仍会用于本次事件，但下次启动可能丢失该增量。
- 回滚时移除 `RecordLevelDurationAnalytics()` 调用，恢复 `total_duration=duration`，并将
  `DrawGame.levelPlayDuration` 注释和打点约定恢复为未启用状态；已有累计字典可以保留，无需迁移。

## 60. 2026-08-07 Draw 成功直接上报 level_end_success_N

### 60.1 需求、原行为与影响范围

- 用户明确要求 DrawGame 通关后也直接上报 `level_end_success_N`，触发时序和字段口径与主线基本一致。
- 修改前 Draw 成功只上报 `level_end(result=success)` 并刷新 `b_play`；Draw 成功次数只参与后续
  主线派生事件的 `N`，不会在 Draw 通关当下产生派生事件。
- 影响 `DrawPlayCtrl.cs`、`GameSDKService.Game.cs`、权威打点生命周期约定和本功能记录；不修改
  Draw 成功次数、通关存档、星级、VictoryPanel、每日挑战、广告、资源、配置或平台 SDK 接口。

### 60.2 修改后事件、数据与保护边界

- `RunVictoryProcessAsync()` 继续先通过 `RecordLevelSuccessAnalytics()` 写入并保存当前 Draw 关成功次数，
  再进入 `ReportLevelEndAnalytics(success)`；因此派生事件使用的跨玩法累计数已经包含本次成功。
- `DrawLevelEndEvent()` 先发送 `level_end(result=success)`，再刷新 `b_play`，随后复用主线同一个
  `LevelEndSuccessEvent(...)` 发送 `level_end_success_N`。成功之外的 fail/exit 不发送派生事件。
- `N` 使用 `GetCombinedLevelPassedCount()`，即主线与 DrawGame 历史累计成功次数之和；沿用共享
  上限保护，`N > 50` 时不发送。
- 派生事件参数 `level` 使用当前 `DrawLevelData.Id`，例如 `DLevel_001` 对应 `20001`；不使用可能因
  排序配置变化的选关显示 ID。若配置 ID 不是正整数，保留 `level_end` 与 `b_play`，跳过派生事件并警告。
- 成功派生事件同步完成后，DrawPlayCtrl 才清理本轮时长、revive 与提示运行态，保持既有成功结算边界。

### 60.3 验证、剩余风险与回滚

- `DrawLevelEndEvent(...)` 当前仅有 `DrawPlayCtrl.ReportLevelEndAnalytics(...)` 一个调用点，新增
  `levelDataId` 参数已经同步；`tbdrawleveldata.json` 当前 ID 使用正整数字符串。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过，结果为
  `0 error / 101 warning`，warning 为工程既有项。
- 静态顺序目标：写入 Draw 成功次数 → `level_end(success)` → `b_play` →
  `level_end_success_N(level=DrawLevelData.Id)` → 清理成功运行态；fail/exit 不进入派生事件分支。
- `uncertain`：尚未在 Unity PlayMode、真实 SDK 后台、Android/iOS 验证事件实际顺序、`N=1/50/51`
  边界、重复通关、跨玩法切换及无效 Draw 配置 ID 的日志表现。
- 回滚时先把权威生命周期约定恢复为 Draw 不直接发送，再移除 `DrawLevelEndEvent(...)` 的
  `levelDataId` 参数和成功分支中的 `LevelEndSuccessEvent(...)`；无需迁移存档、配置或资源。

## 61. 2026-08-07 主线与 Draw 全局进度序号统一

### 61.1 需求、原行为与影响范围

- 用户确认主线与画线玩法必须共用一套进度序号，统一采用“主线累计成功次数 + DrawGame 累计成功次数”。
- 修改前 Draw 的 `level_start.level`、`level_end.level`、`b_play` 与两种玩法的
  `level_end_success_N` 已使用跨玩法合计，但主线基础 `level_start.level`、`level_end.level` 仍只统计
  主线成功次数。主线成功 10 次、Draw 成功 3 次时，Draw 下一次开始为 `14`，返回主线却回退为 `11`；
  同一次主线成功还可能同时产生 `level_end.level=11` 与 `level_end_success_14`，污染全局进度语义。
- 影响 `GameSDKService.Game.cs`、权威打点生命周期约定、本功能风险记录、已有功能记录和 AI 规则提升队列；
  不修改事件名、存档结构、各玩法成功次数写入、`level_name`、`item_category`、`play_count`、
  `start_type`、duration、revive、结果映射、资源、配置或平台 SDK 接口。

### 61.2 修改后数据与事件流

- 主线详细 `LevelStartEvent(...)` 改为复用 `GetCombinedLevelPassedCount()`：
  `level = 主线累计成功次数 + Draw累计成功次数 + 1`。
- 主线基础 `LevelEndEvent(...)` 使用同一合计口径：成功时读取已写入本次主线成功后的合计值，
  失败/退出时使用当前合计值加一；因此同一次主线尝试的开始与结束保持同一序号。
- 主线成功顺序调整为：写入主线成功次数 → `level_end(success)` → `b_play` →
  `level_end_success_N` → 清理成功运行态，与 Draw 的成功打点顺序一致。
- Draw 现有开始、结束、`b_play` 与派生事件实现不变；两种玩法继续分别使用自己的
  `LevelPlayCount`、`PassedLevels`、时长和 revive 数据，只统一全局进度序号。

### 61.3 验证、剩余风险与回滚

- 静态公式矩阵覆盖 `主线=0/Draw=0`、`主线=10/Draw=3`、`主线=48/Draw=1`：两种玩法下一次
  `level_start`、失败/退出 `level_end` 与本次成功后的 `level_end`、`b_play`、`level_end_success_N`
  均使用相同合计序号。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过，结果为
  `0 error / 101 warning`，warning 为工程既有项。
- `uncertain`：尚未在 Unity PlayMode、真实 SDK 后台、Android/iOS 验证主线 → Draw、Draw → 主线、
  Reset、Replay、退出和 `N=1/50/51` 的实际事件顺序与字段值；历史版本已经上报的旧口径数据无法由客户端
  自动改写，BI 仍需按版本或上线时间确定切分口径。
- 本次按用户要求只修复全局进度序号和主线成功 `b_play` 时序；进关资源加载失败产生孤立开始事件、
  成功计数与 `PassedLevels` 分批保存、SDK 初始化前事件行为及保存失败后的端云分叉仍属于独立待处理风险。
- 回滚时先把权威约定恢复为主线只统计主线成功次数，再把主线开始/结束计数恢复为
  `GetLevelPassedCount()`，并移除主线成功分支新增的 `UpdateBPlay()`；无需迁移存档或配置。

## 62. 2026-08-13 Draw 延迟失败演出占用

### 62.1 需求、原行为与影响范围

- DLevel053 的玩家受击需要先占用失败结果并完整播放 1.5 秒表现，再走标准 Draw 失败重建；该期间成功倒计时、其它结算、画线输入和 HUD 操作被冻结，但 D053 蜂群按本关需求继续生成和运动。
- 修改前共享控制器只有延迟成功占用。若关卡只调用 `SetPaused(true)`，通用 Resume 会解除暂停，成功倒计时也没有独立的失败演出状态可判断。
- 本次只扩展 `DrawLevelCtrl` 的结果门禁和 `DrawLevelCountdownCtrl` 的扣时门禁；不改变既有 `TryFailAndReset`、Draw Reset 打点、重建顺序、存档、配置或平台流程。

### 62.2 新接口与生命周期

- `TryBeginFailurePerformance()` 在运行时未结算、无成功/失败演出和无既有陷阱等待时原子占用 `_isPlayingFailure`，关闭画线输入并开启 HUD 点击拦截。
- 占用期间 `Tick()` 不推进 Draw 输入，成功/失败提交、成功门、陷阱和 CheckFinished 均被门禁；`IsOutcomeLocked` 同样返回 true。`SetPaused/Resume` 只改变 `_isPaused`，不能清除失败占用。
- `CompleteFailurePerformance(source)` 先退出占用，再复用 `TryFailAndReset(source)` 派发标准 Draw 重建；提交失败时恢复 HUD 与当前暂停状态允许的输入。`CancelFailurePerformance()` 只供承载组件 Disable/Reset/销毁时释放尚未提交的占用。
- `DrawLevelCountdownCtrl.Update()` 在 `IsFailurePerformanceActive` 时停止扣时。Reset 和 Destroy 清除占用；Draw 新尝试进入 `BeginDrawing()` 时统一关闭 HUD 拦截，保持现有重建恢复边界。

### 62.3 验证、风险与回滚

- 该共享占用最初使用 Assembly-CSharp 响应文件完成过 `0 error / 105 warning` 编译；本轮最终 D053 三脚本另经隔离编译为 `0 error / 21` 条同名类型 warning。当前全工程编译被无关的 `Level117Ctrl.cs` 缺失 `sweat*` 字段阻断。
- 静态调用链确认 D053 占用成功后冻结成功倒计时与其它结算入口，但本关蜂群在 1.5 秒失败表现期间继续生成和运动；等待结束后再提交标准失败。真正 Pause/Resolved 才暂停蜂群，Reset/Disable 使异步版本失效并清理局部状态。
- `uncertain`：仍需 PlayMode 验证在设置面板开关、剩余时间小于 1.5 秒、多蜂同帧命中、快速退出/重开时，HUD 拦截、倒计时和失败事件均只发生一次；Android/iOS 时序同样未验证。
- 回滚前必须先确认 `TryBeginFailurePerformance`、`CompleteFailurePerformance` 和 `CancelFailurePerformance` 无其它调用方；随后移除 `_isPlayingFailure` 的共享门禁和 Countdown 判断。D053 若仍需要 1.5 秒演出，必须先改为另一条可证明不会被 Resume 打断的标准失败链，不能直接退回 Pause 代替占用。

## 63. 2026-08-19 Draw 角色成功退场时同步隐藏动态影子

### 63.1 需求、原行为基线与直接原因

- 需求：DrawGame 角色触发成功进门并完成退场后，角色本体、皮肤 Sprite 和地面影子都必须隐藏，不能在胜利面板出现前残留影子。
- 修改前 `FadeActorOutAsync()` 已通过 `ActorCtrl.SetSpineAlpha()` 和 Actor 子层级的
  `SpriteRendererGroupAlpha` 渐隐角色；静态代码曾据此认为影子也会同步渐隐。
- 当前 Actor prefab 的 `ShadowController` 会在 `LateUpdate()` 中按射线结果动态创建 `Shadow_N` 子对象，
  并重新写入其 SpriteRenderer 透明度和 active 状态。Draw 成功渐隐在 `Update` 阶段写入的影子 alpha
  会在同一帧被覆盖，因此仅依赖渲染组不能保证动态影子退场。
- 本需求不改变成功触发、开关门、结果提交、Victory UI、存档或打点时序，与
  `DevelopMD/关卡打点生命周期约定.md` 的成功生命周期无冲突。

### 63.2 修改后行为、影响范围与恢复边界

- `DrawLevelCtrl.ResolveReferences()` 从当前 Actor 子层级优先定位 `ShadowController` 所在根节点；若组件缺失，
  再按现有主线进出门约定回退查找直属 `Shadow` 节点。没有影子节点的特殊 Actor 安全跳过。
- 解析引用时缓存影子根节点的 prefab 初始 `activeSelf`。Actor 渐隐低于 `0.5` 后关闭整个影子根节点，
  渐隐完成帧再次兜底关闭，从而同时停止 `ShadowController.LateUpdate()` 并隐藏全部动态 `Shadow_N` 子对象。
- `ResetGameState()` 只恢复缓存的初始状态：初始有影子的普通关卡恢复显示；像 DLevel_016 这类 prefab
  原本就关闭 Shadow 的关卡仍保持关闭，不会因共享 Reset 被误开启。
- 影响代码仅为 `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl.cs` 和本记录；未修改 Actor prefab、
  DLevel prefab、`ShadowController`、`SpriteRendererGroupAlpha`、配置、资源地址、UI、DB/Save、事件、
  analytics、广告、SDK 或 Android/iOS 平台分支。
- 性能影响只发生在既有 `0.4s` 成功渐隐窗口：增加一次缓存引用和至多两次幂等 `SetActive(false)`，
  不新增常驻 Update、射线、实例化、Tween 或 GC 热点。

### 63.3 验证、剩余风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过，结果为
  `0 error / 106 warning`；warning 为工程既有项。
- 静态确认 canonical `Actor.prefab` 的 `Shadow` 节点同时持有 `ShadowController` 和 SpriteRenderer，
  `ShadowController.LateUpdate()` 会更新动态影子；Draw 成功链现在在半透明阈值和最终帧均关闭影子根节点，
  Reset 依据缓存的 prefab 初态恢复。
- `uncertain`：尚未在 Unity PlayMode 验证普通成功门、无门成功、球壳复合刚体、DLevel_016 初始无影子、
  成功期间快速 Reset/销毁，以及低帧率下角色与影子的最终帧同步；Android/iOS 横屏设备同样未验证。
- 建议手工回归：普通 Actor 关卡触发成功后观察 0.4 秒渐隐至 VictoryPanel 全程无残影；Reset/Replay 后
  影子恢复；DLevel_016 全程不出现影子；再覆盖一个无门成功关和一个球载体 Actor 关。
- 回滚时删除 `ActorShadowHideAlphaThreshold`、影子根节点/初态缓存、`SetActorShadowVisible()` 及其
  Fade/Reset 调用即可；不需要回滚 prefab、配置、存档或线上数据。

## 64. 2026-08-20 Draw 暂停弹窗期间 Reset/Fail 恢复可玩状态

### 64.1 需求、原行为基线与影响范围

- 需求：DrawGame 在 `GameSettingPopup` 暂停弹窗打开期间触发手动 Reset 或关卡 Fail 时，
  `DrawPlayCtrl` 必须把新尝试恢复为可游玩状态，不能让重建后的关卡继续继承旧暂停状态。
- 修改前 `Pause()` 会把 `DrawPlayCtrl._isPaused` 设为 `true`；Reset/Fail 共用的
  `OnGameResetEvent()` 接受事件后会通过 `CloseAllUI(UILayer.Popup)` 程序化关闭暂停弹窗，
  但该关闭路径不会执行弹窗按钮处理器中的 `IPlayPauseHost.Resume()`。
- 因 `_isPaused` 未复位，新 DLevel 初始化和 `BeginDrawing()` 会继续应用暂停并关闭输入，表现为
  暂停弹窗已经消失、黑幕和重建均完成，但玩家仍不能画线、关卡 Tick 也不推进。
- 影响运行时代码仅为 `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`；所有 Draw 关卡共享该控制器边界。
  未修改 `GameSettingPopup`、Bear `UIManager`、Draw HUD、DLevel prefab、配置、资源或平台代码。

### 64.2 修复后状态与系统边界

- `OnGameResetEvent()` 通过活动运行时、加载状态和重复尝试门禁后，在建立
  `_isAttemptTransitionPending` 的同时把控制器 `_isPaused` 重置为 `false`。手动 Reset 与 Fail
  均派发 `DrawGameResetEvent`，因此共用同一修复边界。
- 旧 DLevel 不会因本次复位重新开放：既有流程仍立即关闭运行时输入，随后在黑幕期间销毁旧 HUD 和
  DLevel；没有调用旧运行时的 `SetPaused(false)`，避免重建前的机关、物理或演出重新推进。
- 新 DLevel 创建时取得非暂停状态，完成 Retry Intro、失败插屏等待和黑幕淡出后，既有
  `BeginDrawing()` 统一进入 `Drawing` 并开放输入。暂停弹窗仍由 Reset 流程关闭，不需要玩家再次打开
  暂停页再点击 Resume。
- revive、`level_end(result=fail)`、新 `level_start`、提示资格、失败插屏策略和重建顺序保持现行契约；
  不新增事件、存档字段、配置读取、资源加载或 Android/iOS 分支。通用 Popup 的程序化关闭仍不自动
  Resume，避免退出、切玩法或其它清理场景错误恢复旧玩法。

### 64.3 验证、剩余风险与回滚

- `git diff --check -- Assets/Game/Scripts/Play/DrawPlayCtrl.cs` 通过；仅有仓库现有的 LF/CRLF 转换提示，
  无空白错误。
- Unity 6000.0.59f2 当前 Editor Bee `1300b0aEDbg.dag/Assembly-CSharp.rsp/.rsp2` 使用自带 Roslyn
  完整编译通过：`ExitCode=0 / Errors=0 / Warnings=104`，目标脚本无诊断。
- 常规 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 被当前缺失的
  `Temp/bin/Debug` 依赖程序集阻断，产生 133 个 `CS0006`；旧 Player Bee 响应文件也仍引用已经迁移的
  `LevelItem` 源文件。二者属于生成工程/缓存状态，不作为本次代码失败结论。
- `uncertain`：尚未在 Unity PlayMode 验证“暂停页打开时手动派发 Reset”“暂停页打开时延迟 Fail”、
  连续 Reset、失败插屏成功/拒绝/关闭后的黑幕与输入恢复，以及 Android/iOS 横屏和原生覆盖层恢复。
- 建议手工回归：进入任意 Draw 关卡后打开暂停页，分别触发 Manual Reset 与 Fail；确认暂停页关闭，
  新关完成淡入后可以立即画线且倒计时/机关继续推进，再覆盖普通暂停关闭、Menu 退出和连续 Reset。
- 回滚时只需删除 `OnGameResetEvent()` 中 `_isPaused = false` 及其说明注释；无需回滚 prefab、配置、
  存档、打点数据或线上资源。

## 65. 2026-08-21 Draw 胜利下一关改为遮住后销毁旧关

### 65.1 需求、原行为基线与直接原因

- 需求：Draw 胜利页点击下一关后采用主线转场顺序；允许先销毁旧 HUD，但应先打开
  `EnterLevelLoading`，等翻页完全遮住旧关后再销毁旧 DLevel、加载新关并由 Erase 揭开。
- 修改前 `OnEnterNextLevel()` 在创建转场前先调用 `CleanupLevelRuntime()`，同步解除运行时、销毁 HUD，
  并立即失活旧 DLevel；之后才通过 `TryEnterLevel()` 等待一帧创建 `EnterLevelLoading`。因此旧关会在
  遮罩接管前消失，与主线 `PlayCtrl` 在翻页完成回调内销毁旧关的顺序不一致。
- 历史代码与现有需求文档没有发现“必须提前销毁旧 DLevel”的产品约束；该顺序来自 Draw 下一关初版
  复用“选关进入”的无活动关卡门禁。后续为规避同帧旧 Collider/Tween 干扰而增加的立即失活，只说明
  销毁时应先 `SetActive(false)`，不要求销毁点发生在 Loading 创建之前。

### 65.2 修改后行为、数据/资源/UI 流与影响范围

- `TryEnterLevel()` 继续服务选关入口，并要求当前无活动 DLevel；新增的内部共享门禁仅允许胜利下一关在
  `Resolved`、旧运行时与旧实例都存在时选择“保留到遮住”分支，避免把普通选关误当成切关。
- 下一关请求通过后立即销毁旧 `DrawGame_GamePlayPanel`，重置新关提示/奖励会话并建立新的激活版本；
  旧 DLevel 保持可见，且沿用成功结算时已关闭的画线输入。
- `EnterLevelLoading.OnPageTurnCompleteAsync()` 先等待 Draw 提供的加载回调。回调内解除旧
  `Resolved` 订阅、失活并销毁旧 DLevel，再创建新 HUD、由 Bear 资源系统加载新 prefab、实例化并初始化
  `IDrawLevelRuntime`；全部完成后 `EnterLevelLoading` 才启动 Erase，新关输入仍要等 Erase 完成。
- 若没有有效下一关或请求校验失败，仍完整清理旧 HUD/DLevel并返回选关页；若转场创建或新关加载失败，
  仍由 `HandleOpenFailure()` 清理并执行同一降级。Reset/Fail/Victory Replay 继续走原黑幕重建链。
- 影响运行时代码仅为 `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`。没有修改 prefab、配置、存档、全局事件、
  Analytics、广告、SDK、`GamePlayPanel` 或 `EnterLevelLoading` 共享实现；移动端没有新增平台分支。

### 65.3 风险、验证与回滚

- 静态调用链确认旧 HUD 在下一关请求接受后销毁，旧 DLevel 只在翻页完成回调内释放；新 HUD/DLevel 的
  创建发生在释放之后，Erase 仍等待加载回调结束才开始。`git diff --check` 无空白错误；
  `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过，结果为
  `0 error / 106 warning`，warning 均为工程现有项。
- `uncertain`：尚需 Unity PlayMode 验证普通下一关、最后一关降级、资源缺失降级、连续点击、低帧率下
  翻页覆盖边界、带持续 Tween/Physics 的成功终态，以及 Android/iOS 横屏设备上的视觉与输入恢复。
- 建议手工回归：完成任意非末关后点击下一关，确认 HUD 先消失、旧关持续显示到完全遮住且无空场；
  新关加载完成后 Erase 揭开，Intro 和画线输入只启动一次。再覆盖末关、模拟资源加载失败和快速退出。
- 回滚时恢复 `OnEnterNextLevel()` 在请求前调用 `CleanupLevelRuntime()` 并重新使用公开 `TryEnterLevel()`；
  删除保留旧关的内部门禁、翻页完成后的释放回调与拆分清理辅助方法。无需回滚资源、配置、存档或线上数据。

## 66. 2026-10-12 Draw 入场期间暂停后恢复画线输入

### 66.1 需求、修改前核对与原行为

- 用户反馈 DLevel92 无法画线，核对方案后明确要求修复共享 Resume。前置依据已在 `DevelopMD/已有功能设计与使用记录/2026-10-12.md` 留存；本节记录最终实现。
- Editor.log 记录 DLevel_092 进关后打开 GameSettingPopup，随后 D6 Intro 完成并进入 `CompleteIntroAndBeginDrawing`，再通过设置按钮关闭暂停。原 `BeginDrawing` 在暂停状态下将独立画线开关设为 false；原 Resume 只解除暂停，没有补开输入。
- 第 35/37 节要求输入首次开放仍由 Intro 完成链负责；第 62 节要求成功/失败演出占用不被 Resume 清除；第 64/65 节要求重建、失败插屏等待和下一关遮罩边界继续保持。此次修复与现行打点生命周期约定无冲突。

### 66.2 修复后流程与影响边界

- 唯一生产脚本改动是 `Assets/Game/Scripts/Play/DrawPlayCtrl.cs` 的 Resume：沿用活动检查和解除暂停，只有 `Drawing && !isOpeningLevel && !isAttemptTransitionPending && runtime.IsRuntimeReady` 才调用 `SetInputEnabled(true)`。
- 关卡 runtime 的 `DrawLevelCtrl.SetInputEnabled` 继续拒绝已结算、成功演出或失败演出；不重入 BeginDrawing、不推进阶段、不派发 HUD 解锁事件。首次可见 Intro 未结束、加载、Reset/Replay 重建及广告等待不能通过该补开分支。
- 受影响入口为 Draw 设置关闭、商店返回/创建失败、首次操作引导关闭及共享暂停宿主恢复；所有 Draw 关卡共用该控制器。D092/D006 的双夹入场更容易出现这项交错，地图、D6 与已验收关卡专属脚本不修改。
- 本轮不增加存档/配置字段、事件、analytics、SDK 调用、资源或平台分支；未改原注释。新增成本仅在 Resume 时的阶段/门禁判断与一次输入快照通知，无逐帧新增工作。

### 66.3 验证、未覆盖与回滚

- 复用此前有效 Editor 响应文件和 Unity 6000.0.59f2 自带 Roslyn，输出重定向至独立 TEMP，完整主程序集 `0 error / 113 warning`，DrawPlayCtrl 无诊断。没有写回 Bee 清单、Packages、GuruSDK 或 GF；此项是独立源码编译，不能替代主 Unity 导入、IL2CPP 或移动端构建。
- 内存中提取修改前/后 Resume、当前 Pause/BeginDrawing 和 DrawLevelCtrl 输入/暂停方法：19 项断言通过，旧实现复现一次“暂停跨过 Intro 后输入保持 false”。覆盖入场结束后恢复、提前恢复后等待入场、普通暂停、重复恢复、失活、空/未就绪 runtime、加载、Reset/Replay、重建等待、控制器 Resolved 及 runtime 成功/失败演出门禁。
- runtime/UI/audio 使用替身，只证明这些方法的状态与门禁分支；未新增 Verify.ps1。还原新 Resume 后，DrawPlayCtrl 全文件与 HEAD 一致，说明非目标方法未变化。
- 证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-draw-resume-20261012-0a198c9998c9456e828a27fd90ad9f3e/`，含 `compile-Editor.log` 与 `resume-scenarios.log`。
- `uncertain`：主 Unity 当前实例重载后，D092/D006/延迟入场关的实际拖画、商店/操作引导关闭、D053 失败演出、Reset/Replay 真实广告和 Android/iOS 横屏未复测。D092 Prefab 在本轮开始已有外部未提交改动，不把它纳入修复或回滚。
- 回滚仅删除 Resume 新增的 Drawing/加载/尝试切换/就绪条件及补开调用；保留原解除暂停、既有资源和其它用户改动。无需存档或配置迁移。
