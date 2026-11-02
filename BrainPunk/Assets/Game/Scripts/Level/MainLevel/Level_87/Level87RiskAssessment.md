# Level87 足球球框 top 风险评估

## 原行为基线

- `Level87Ctrl.cs` 在 `HEAD` 中已有 version 1.0.0 注释和足球流程骨架，包含 actor/enemy/football、入场、失败和成功相关方法，但缺少完整 use_btn 绑定、top 成功解锁和清理闭环。
- 当前工作区顶部需求注释已扩展为 top 成功路径需求，并进一步明确 `topTriggerPositionY` 为 float local Y 触发阈值、`topEndPosition2` 为 Transform 吸附目标；后续实现需同步当前注释语义，不应再无关改写该注释。
- `Level_087.prefab` 已有 Level87 所需对象：`football`、`top`、`Canvas`、`ArrowRange_green`、`EnterTrigger`、`qiukuang`；本次未手动改 prefab，运行时按名称或既有引用解析。

## 影响文件和资源

- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87SoccerTop_Feature.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87RiskAssessment.md`
- `DevelopMD/已有功能设计与使用记录.md`
- `specs/028-level87-soccer-top/*`
- 相关资源证据来自 `Assets/Game/Prefabs/Level/Level_087.prefab` 和 `Assets/Game/UI/GamePanel/Resources/GamePlayPanel_024_87.prefab`，但本次没有主动改写这两个 prefab。

## 使用的既有系统

- `SwitchObjActiveEvent` 和 `GamePanel_UseBtnCommonFeature.Use_btn`：显示、隐藏和获取共享 Use 按钮。
- `CustomButton.OnClick`：触发 Level87 本地射门流程。
- `DragableItem`：top 到位后禁用拖拽。
- `Level84ActorCtrl`：复用足球射门和扑救表现 API。
- `Rigidbody2D`：football 受力射门和 enemy 阻挡反弹。
- `BaseLevelCtrl.SetCtrlable(...)`：足球流程期间锁定 actor 输入。
- `GameResetEvent(GameResetType.Failed)`：普通射门失败归口。
- `BaseLevelCtrl.SuccessStrightly()`：top 解锁后的成功归口。
- DOTween / UniTask：本地表现、等待和异步取消。

## 风险项

- P1：`use_btn` 获取依赖当前 GamePlayPanel 挂载 `GamePanel_UseBtnCommonFeature`。文本证据显示 `GamePlayPanel_024_87.prefab` 已绑定 `Use_btn`，但 PlayMode 创建时序仍需验证。
- P1：当前注释要求 `top` 超过 float 字段 `topTriggerPositionY` 后触发，并移动到 Transform 字段 `topEndPosition2.localPosition`；脚本现有 `topTriggerTarget` / `topTriggerLocalYOffset` 方案需要移除 active fallback，改为显式字段，否则策划配置和注释验收口径会不一致。
- P1：早期实现曾把 slider height 解释为 `localScale.y` 或 UI 高度；用户已澄清 `sliderObjs[i]` 是 `SpriteRenderer`，实际应修改 `SpriteRenderer.size.y` / Inspector `Size.height`。
- P1：成功路径必须依赖显式碰撞入口。当前脚本兼容 `Collision2D` 双方 collider，但若仍保留 `qiukuang` 附近半径兜底作为成功触发，则与后续澄清验收口径冲突；真实 UnityEvent / LayerMask / collider 绑定仍需 PlayMode 或 Inspector 验证。
- P1：football 改为 Dynamic 后施加冲量，失败和成功清理会切回 Kinematic 并清速度。若 PlayMode 中需要持续真实物理滚动到通关，当前成功启动后会停止 football，可能有视觉吸附风险。
- P1：`Level84ActorCtrl` 动画 API 是跨关卡复用。缺失该组件时脚本只 warning 和短等待作为防崩溃 fallback，不能视为 Level87 表现验收通过。
- P1：`HideUseButton()` 同时派发 `SwitchObjActiveEvent(false)` 并直接处理 resolved button；如果共享面板内部也响应事件播放缩放，存在重复 tween 时序，需要 PlayMode 验证最终状态稳定。
- P1：快速连点 use_btn、disable 发生在 successDelay 内、失败等待晚到，可能导致重复结算；当前通过 `isResolvingShot`、`isResolvingSuccess`、`isCompleted` 和 `sessionId` 缓解，但仍需运行验证。
- P2：新增序列化字段没有写入 prefab YAML，依赖脚本默认值和自动解析。该做法降低 prefab churn，但 Inspector 显示和策划可调性需要后续确认。

## 已缓解风险

- 成功与失败归口使用既有 `SuccessStrightly()` 和 `GameResetEvent(GameResetType.Failed)`，未新增存档、解锁、analytics 或平台 SDK 调用。
- 运行时引用缺失时多数路径直接 no-op 或 warning，避免空引用崩溃；但缺失关键引用不代表验收通过。
- `Collision2D` 成功入口检查 `collision.collider` 与 `collision.otherCollider`，降低 UnityEvent 绑定在不同物体上时误过滤 football 的风险。
- Disable/Destroy 和重置路径统一 kill tween、解绑按钮、恢复 top/slider/football/enemy 状态，并递增 session 阻止旧异步晚到。

## 验证证据

- 静态源码证据：`Level87Ctrl.cs` 已实现 use_btn 运行时解析、按钮订阅/退订、箭头循环、force 射门、top 到位、slider 高度、绿色范围填充、成功碰撞入口、失败/成功归口和清理路径；但 top 字段和行为仍需按当前注释同步为 float `topTriggerPositionY` / Transform `topEndPosition2`。
- 静态 prefab 证据：`Level_087.prefab` 文本显示 `football` 带 `Rigidbody2D`、`top` 带 `DragableItem`、存在 `Canvas`、`ArrowRange_green`、`ArrowRange_normal`、`EnterTrigger` 和 `qiukuang`。
- 静态 UI prefab 证据：`GamePlayPanel_024_87.prefab` 文本显示 `GamePanel_UseBtnCommonFeature.Use_btn` 已绑定。
- 静态扫描确认：`Level87Ctrl.cs` 未新增 DB、Save、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、OnValidate、Reset、ExecuteAlways、ExecuteInEditMode 或 Unity `Debug.Log`。
- `dotnet build .\Assembly-CSharp.csproj` 已通过，结果为 0 error / 97 warning；本次修复后未再出现 `Level87Ctrl.cs` 相关 warning。
- `git diff --check` 针对本次新增中文文档、`DevelopMD/已有功能设计与使用记录.md`、Speckit spec/checklist 和 `.specify/feature.json` 通过。单独检查 `Level87Ctrl.cs` 时仅报告顶部需求注释空白行已有 trailing whitespace；为避免无关改写当前 version 1.0.0 注释内容，本次未清理这些注释空白行。

## 未验证项

- 未执行 Unity PlayMode：需要验证 Level87 进入、use_btn 显示、普通射门失败、enemy 阻挡、失败重开、top 拖拽、slider 视觉、绿色范围、成功射门、成功碰撞和 2 秒后通关。
- 未执行 Inspector 检查：需要确认自动解析结果、关键字段默认值和成功 collider / UnityEvent 绑定符合关卡 prefab 预期。
- 未执行 Android/iOS 横屏触控验证：top 拖拽、use_btn 点击、快速连点、退出/disable 和 60 秒帧稳定性仍待补验。
- 未验证一个未受影响 Actor 关卡冒烟；当前只通过 diff 和静态扫描判断未改共享 gameplay 系统。

## 回滚路径

- 回滚 `Level87Ctrl.cs` 中本次新增的 use_btn 解析、top/slider/arrowRangeGreen 字段、足球 force 射门、成功碰撞入口、失败/成功分支和清理逻辑。
- 删除 `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87SoccerTop_Feature.md`、本风险评估、`DevelopMD/已有功能设计与使用记录.md` 的本次追加段落，以及 `specs/028-level87-soccer-top` 规格目录。
- 如果后续发现 prefab 需要手工绑定，则单独提交 Level87 prefab 绑定修复，并重新更新本风险评估。

## 2026-06-22 最终同步风险记录

### 已缓解

- `Level87Ctrl.cs` 已移除旧 `topTriggerTarget` / `topTriggerLocalYOffset` active fallback；top 触发只看显式 float `topTriggerPositionY`，吸附只取显式 Transform `topEndPosition2.localPosition`。
- `Level_087.prefab` 已新增 `topEndPosition2` 空 Transform 并绑定到 `Level87Ctrl.topEndPosition2`，同时写入 `topTriggerPositionY: 3`；该 prefab 改动仅限 Level87。
- `successShotMaxWait` 和 top 后等待超时自动失败路径已移除。top 后射门等待显式成功碰撞，或由 retry/exit/disable/destroy/cleanup 结束。
- `IsFootballNearSuccessTarget()` 与 `successTargetRadius` 主动半径成功路径已移除；`qiukuang` / `successTarget` 只作为视觉或调试参考，不触发成功。
- 静态扫描未发现 `topTriggerTarget`、`topTriggerLocalYOffset`、`successShotMaxWait`、`IsFootballNearSuccessTarget` 或 `successTargetRadius` 在 `Level87Ctrl.cs` / `Level_087.prefab` 中残留。
- `dotnet build .\Assembly-CSharp.csproj --no-restore` 已通过，结果为 0 error / 1115 warning；未发现 Level87 编译错误。

### 仍需关注

- `Level_087.prefab` 当前通过文本方式新增 `topEndPosition2`，Unity Editor 打开 prefab 后仍需确认 Inspector 显示、Transform 层级和序列化无警告。
- 成功 collider / UnityEvent 绑定仍需 PlayMode 或 Inspector 验证；静态文本只能证明脚本入口存在，不能证明碰撞链路实际触发。
- slider 已按最新澄清改为 `SpriteRenderer.size.y` 增加高度；仍需 PlayMode 视觉确认 `Size.height` 增长符合预期。
- Android/iOS 横屏触控、use_btn 快速连点、top 拖拽手感和 60 秒帧稳定性未在本环境验证，仍为上线前补验项。
## 2026-06-22 version 1.1 arrow 冻结风险记录

### 已缓解
- `ResolveShot()` 现在会在 actor 踢球动画等待前冻结本次射门方向，并停止 arrow tween，避免动画等待期间 arrow 继续移动改变本次 height。
- `PlayForceShot()` 优先使用本次冻结方向；top 前失败路径和 top 后成功候选路径共用同一冻结结果。
- `ResetPresentationState()` 清空冻结状态；下一次 `OnEnable()`、重新进入或 retry 后可重新启动 arrow loop。
- 重复点击由既有 `isResolvingShot` / `isCompleted` 保护，本次尝试内不会用第二次点击覆盖第一次冻结角度。

### 新增/剩余风险
- 未执行 Unity PlayMode，无法确认点击同帧视觉上 arrow 是否完全无继续移动帧。
- 未执行 PlayMode retry/exit/disable/success/failure 全路径，无法确认每个 cleanup 边界都能按玩家视角恢复 arrow loop。
- 未执行 Android/iOS 横屏触摸和快速连点验证；移动端输入时序仍待补验。

### 验证计划
- 静态扫描：确认 `FreezeShotDirection`、`ClearFrozenShotDirection`、`hasFrozenShotDirection`、`frozenShotDirection` 存在且未引入 DB/save/config/resource/editor/platform 调用。
- 编译：执行 `dotnet build .\Assembly-CSharp.csproj --no-restore`。
- PlayMode 待补：pre-top 点击冻结、top 后点击冻结、10 秒无碰撞等待、retry 后 arrow loop 恢复。

### 2026-06-22 静态验证结果
- 静态扫描确认 `Level87Ctrl.cs` 存在 `FreezeShotDirection()`、`ClearFrozenShotDirection()`、`hasFrozenShotDirection` 和 `frozenShotDirection`；`ResolveShot()` 在 actor 踢球等待前冻结方向并停止 arrow tween，`PlayForceShot()` 使用冻结方向。
- 禁用调用扫描未发现新增 `DB`、`RemoteConfig`、`PlayerPrefs`、`Resources`、`StreamingAssets`、`YooAsset`、`Instantiate`、`OnValidate`、`ExecuteAlways`、`ExecuteInEditMode`、`Debug.Log`、`Analytics`、`IAP` 或 `Advertisement` 调用。扫描命中 `MissingActorSaveFallbackDuration` / `GetEnemySavePosition()` 属于足球表现命名，不是 save 系统调用。
- `dotnet build .\Assembly-CSharp.csproj --no-restore` 已通过，结果为 0 error / 1115 warning；warning 属于当前工程既有范围，未阻塞 Level87 version 1.1 静态验收。
- `git diff --check -- ..\specs\028-level87-soccer-top .\Assets\Game\Scripts\Level\LevelItem\Level_87 .\DevelopMD` 仅报告 `Level87Ctrl.cs` 顶部既有需求注释空行 trailing whitespace。为遵守“不修改原有注释内容”，本次未清理该旧注释空白行，作为已知格式例外记录。
- `.specify/extensions.yml` 未配置 `before_implement` 或 `after_implement` hook。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中冻结方向字段、冻结/清理方法、`ResolveShot()` 冻结调用和 `PlayForceShot()` 使用冻结方向的改动。
- 同步回滚 `Level87SoccerTop_Feature.md`、本风险记录、`quickstart.md` 和 `DevelopMD/已有功能设计与使用记录.md` 中的 version 1.1 追加记录。

## 2026-06-22 version 2.0 DOTween 射门与 slider height 风险记录

### 原行为基线
- `Level87Ctrl.cs` 顶部 version 2.0 注释要求主射门改为 DOTween 移动，并使用 `shotDuration` 作为移动时长；同时要求 `sliderObj` 修改 height，不要改 scale。
- 本次修改前，`PlayForceShot()` 中原 `Rigidbody2D.AddForce` 主射门代码已被注释，方法只执行 `football.DOKill()`，主射门移动表现不完整。
- 本次修改前，slider 联动使用缓存 `localScale` 并写入 `localScale.y`，与最新 version 2.0 注释要求冲突。

### 已缓解
- `PlayForceShot()` 已改为停止旧 tween、切换 football 为 Kinematic、使用冻结方向计算目标点，并按 `shotDuration` 执行线性 `DOMove`。
- 普通失败路径中 enemy 阻挡前会停止 football 主射门 tween，再保留原有 enemy 反弹 `AddForce`，降低两个运动系统同时控制 football 的风险。
- football 初始世界坐标已缓存并在进入流程/cleanup 中恢复，降低重复尝试从上一轮射门终点继续偏移的风险。
- 失败结算前已补充 football tween 取消、刚体状态恢复和 enemy 表现停止，降低失败派发边界仍残留表现的风险。
- slider 联动已改为缓存 `SpriteRenderer.size.y`，应用和恢复时写回 `SpriteRenderer.size.y`，不再写入 slider `localScale` 或 `RectTransform` 高度。
- 本次未新增 DB、Save、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、OnValidate、ExecuteAlways 或 ExecuteInEditMode 入口。

### 新增/剩余风险
- P1：DOTween 直接移动 Transform 与 Unity 2D 碰撞/Trigger 的实际触发时机需要 PlayMode 验证；如果成功区依赖刚体物理步进，可能出现视觉到位但碰撞未触发。
- P1：失败路径在 `shotDuration / 2` 后启动 enemy 阻挡；当策划调整 `shotDuration` 或 `shotForce` 时，enemy 阻挡与 football 位置节奏可能需要重新校准。
- P1：slider 改为 `SpriteRenderer.size.y` 后需要确认两个 `sliderObjs` 的 `Draw Mode` / `Size.height` 视觉符合预期；静态 prefab 证据显示当前绑定对象为 `SpriteRenderer` 且存在 `m_Size`。
- P2：`shotForce` 字段名仍沿用旧名，但在 v2.0 中实际表示 DOTween 移动距离；为避免 prefab 序列化 churn 本次未重命名字段，已同步 Inspector Tooltip 说明该字段不再作为主射门刚体冲量。

### 验证计划
- 静态扫描已执行：确认 `PlayForceShot()` 使用 `DOMove` 和 `shotDuration`；slider height 路径使用 `SpriteRenderer.size.y`，未继续写入 slider `localScale` 或 `SetSizeWithCurrentAnchors`。
- 旧字段扫描已执行：未命中 `topTriggerTarget`、`topTriggerLocalYOffset`、`successShotMaxWait`、`IsFootballNearSuccessTarget` 或 `successTargetRadius`。
- 禁用调用扫描已执行：未发现新增 DB、Save、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、OnValidate、ExecuteAlways、ExecuteInEditMode、Debug.Log、Analytics、IAP 或 Advertisement 调用。
- 编译已执行：`dotnet build .\Assembly-CSharp.csproj --no-restore` 通过，0 error / 97 warning；warning 属于当前工程既有范围。
- 格式已复跑：`git diff --check -- ..\specs\028-level87-soccer-top .\Assets\Game\Scripts\Level\LevelItem\Level_87 .\DevelopMD` 通过，未报告 whitespace error；Git 仅提示相关文件下次触碰时会进行 LF/CRLF 规范化。
- PlayMode 待补：pre-top 射门移动、enemy 阻挡反弹、top 后成功碰撞、slider `SpriteRenderer.size.y` 视觉、retry/exit/disable 清理。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中 `PlayForceShot()` DOTween 移动、enemy 阻挡前 `football.DOKill()`、football 初始位置复位、失败边界清理、`sliderInitialHeights`、`ApplySliderOffset()`、slider `SpriteRenderer.size.y` 恢复逻辑和 `shotForce` Tooltip 同步。
- 同步回滚 `specs/028-level87-soccer-top`、`Level87SoccerTop_Feature.md`、本风险记录和 `DevelopMD/已有功能设计与使用记录.md` 中的 version 2.0 追加记录。

## 2026-06-23 Level84 音效迁移到 Level87 风险记录

### 原行为基线
- `Level87Ctrl.cs` 变更前没有 `AudioManager.PlaySound(...)` 调用，足球流程只有入场、共享 `use_btn`、箭头、top、射门、enemy 阻挡和成功/失败结算。
- `Level84Ctrl.cs` 当前已有的音效调用覆盖足球进入、入场后、踢球、触地、被挡出、碰撞粒子和成功欢呼。
- `Level_087.prefab` 变更前已有足球 `LayerTriggerDetector`，但 `PlayFootballTouchGround` 回调目标为空且类型仍为 `Level84Ctrl`，属于从 prefab 回调链扩散出来的无效绑定风险。

### 影响文件和资源
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`
- `Assets/Game/Prefabs/Level/Level_087.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87SoccerTop_Feature.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87RiskAssessment.md`
- `DevelopMD/已有功能设计与使用记录.md`
- 关联但未修改的既有系统：`AudioManager`、`LayerTriggerDetector`、DOTween、UniTask、`Level84ActorCtrl`、`GamePanel_UseBtnCommonFeature.Use_btn`、`BaseLevelCtrl.SuccessStrightly()`、`GameResetEvent`

### 已缓解
- `EnterSoccerFlow()` 在 actor 输入锁定后播放 `whistle`，football 入场结束且 session 有效后播放 `grassCut`，避免流程被取消后继续显示前额外播放入场后音效。
- `PlayForceShot()` 在 football 有效且真正开始射门移动时播放 `kickFootball`，覆盖 top 前失败射门和 top 后成功候选射门，不在无 football 的防崩溃 no-op 路径播放。
- 新增 `PlayFootballTouchGround()`，只播放 `footballTouchGround`，不写存档、不派发胜负事件、不修改物理状态，降低 prefab 触地回调对玩法状态的扩散风险。
- `Level_087.prefab` 的足球触地回调已指向本关 `Level87Ctrl` 组件，修复空目标 `Level84Ctrl` 绑定无法触发的问题。
- `PlayEnemyBlock()` 在切 Dynamic 并施加阻挡力后播放 `footballOut`，在 `knock` 粒子播放时播放 `trap`；两者仍受原本 enemy 阻挡流程控制。
- `StartSuccessFlow()` 在成功流程启动并停止 football/enemy 表现后播放 `footballAudienceCelebrate`，成功结算仍复用 `SuccessStrightly()`。
- 本次未新增 DB、Save、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、OnValidate、ExecuteAlways、ExecuteInEditMode、Debug.Log、Analytics、IAP 或 Advertisement 调用。

### 新增/剩余风险
- P1：新增音效 tag 只通过静态代码确认调用，尚未确认 Sounds Good 配置中 `whistle`、`grassCut`、`kickFootball`、`footballTouchGround`、`footballOut`、`trap` 和 `footballAudienceCelebrate` 全部存在；缺失 tag 可能导致运行时无声或音频系统 warning。
- P1：触地音效依赖 `LayerTriggerDetector.targetLayerMask = 64` 和目标层设置。若地面层不匹配，`footballTouchGround` 不播放；若层范围过宽，可能重复播放。
- P1：`PlayForceShot()` 是 top 前和 top 后共用入口，`kickFootball` 会在两种射门候选中播放；若后续策划只希望成功或失败某一路径播放，需要再拆分时机。
- P1：`footballAudienceCelebrate` 在 `StartSuccessFlow()` 启动时播放，随后等待 `successDelay` 通关；若成功碰撞被重复触发，当前由 `isResolvingSuccess` 和 `sessionId` 缓解，但仍需 PlayMode 验证不会重复欢呼。
- P2：`Level_087.prefab` 本次开始前已有其它未提交改动，diff 中包含非音效相关的 football、slider、grid 和 Transform 差异；本风险记录只覆盖音效迁移链路，未验证这些既有 prefab 差异。
- P2：共享 `Use_btn` 已有 `ClickAudio(button)`，本次未给 Level87 脚本额外播放按钮音效；若 `Use_btn` 绑定缺失或共享面板变体不同，按钮点击声仍依赖共享 prefab 检查。

### 验证证据
- 静态扫描确认 `Level87Ctrl.cs` 包含 `AudioManager.PlaySound("whistle")`、`"grassCut"`、`"kickFootball"`、`"footballTouchGround"`、`"footballOut"`、`"trap"` 和 `"footballAudienceCelebrate"`。
- 静态 prefab 扫描确认 `Level_087.prefab` 中 `PlayFootballTouchGround` 回调为 `m_TargetAssemblyTypeName: Level87Ctrl, Assembly-CSharp`，目标 fileID 为本关控制器组件。
- 静态扫描确认 `GamePlayPanel_024_87.prefab` 中共享 `Use_btn` 已存在且 `ClickAudio.AudioName` 为 `button`。
- 禁用调用扫描未发现新增编辑器生命周期、存档、配置、资源加载、实例化、analytics、IAP、广告或 Unity `Debug.Log` 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已通过，结果为 0 error / 1114 warnings；warning 属于当前工程既有范围。
- `git diff --check -- Assets\Game\Scripts\Level\LevelItem\Level_87\Level87Ctrl.cs` 未报告脚本 whitespace error。`Level_087.prefab` 当前 diff check 报告已有 Unity YAML 空值行 trailing whitespace，本次未清理非音效相关 prefab 差异。

### 未验证项
- 未执行 Unity PlayMode：仍需验证进入 Level87 后 `whistle`、`grassCut`、`kickFootball`、`footballTouchGround`、`footballOut`、`trap`、`footballAudienceCelebrate` 的真实播放时机、音量和重叠表现。
- 未验证 Sounds Good 音效 tag 实际存在。
- 未验证足球触地回调在 layer 64 上只按预期触发，且不会因 trigger 多次进入导致落地音效过密。
- 未验证 top 前失败、top 后成功候选、成功等待、retry、exit、disable、destroy 和快速连点全路径。
- 未验证 Android/iOS 横屏触摸、真机音频输出和 60 秒帧稳定性。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中新增的 `Game.Scripts.Common` using、所有 `AudioManager.PlaySound(...)` 调用和 `PlayFootballTouchGround()`。
- 回滚 `Level_087.prefab` 中足球 `LayerTriggerDetector.onEnter` 指向 `Level87Ctrl.PlayFootballTouchGround()` 的绑定修正。
- 同步删除 `Level87SoccerTop_Feature.md`、本风险记录和 `DevelopMD/已有功能设计与使用记录.md` 中本次 Level87 音效迁移记录。

## 2026-06-23 Level87 grid 随 top 增长风险记录

### 原行为基线
- `Level87Ctrl.cs` 已有 top 抬高后调用 `ApplySliderOffset(...)` 更新 slider `SpriteRenderer.size.y` 的逻辑。
- 新增的 `grid` prefab 文本证据显示初始 `m_Size.y = 0`，但此前脚本未在 top 移动过程中更新该 `SpriteRenderer.size.y`。
- 本次需求要求 grid 配合 top 移动，从 `size.height = 0` 进行增长。

### 影响文件和资源
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`
- `Assets/Game/Prefabs/Level/Level_087.prefab`：作为现有 grid 子物体和初始 size 证据；本次未为 grid 需求新增 prefab 绑定改写。
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87SoccerTop_Feature.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87RiskAssessment.md`
- `DevelopMD/已有功能设计与使用记录.md`

### 已缓解
- `grid` 作为 Level87 本地 `SpriteRenderer` 处理，未引入共享逻辑、资源加载、存档、配置、analytics、广告/IAP 或平台 SDK 链路。
- `ResolveReferences()` 在未绑定时会按子物体名 `grid` 自动查找，降低 prefab 序列化绑定缺失导致 grid 不生效的风险。
- `ApplySliderOffset(...)` 即使 slider 引用或缓存为空，也会继续调用 `SetGridHeight(scaledOffset)`，降低新增 grid 被旧 slider 绑定状态阻断的风险。
- `RestoreSliderInitialState()` 会调用 `SetGridHeight(0f)`，降低失败重开、重新进入、Disable/Destroy 后 grid 高度残留的风险。
- `SetGridHeight(...)` 只修改 `SpriteRenderer.size.y`，保留 `size.x` 并限制高度不小于 0，避免负高度和 Transform scale 副作用。

### 新增/剩余风险
- P1：`grid` 当前依赖按名称自动解析。若 prefab 后续改名、移出控制器子级或缺失 `SpriteRenderer`，运行时会静默 no-op，grid 不会增长。
- P1：`grid` 与 slider 共用 `sliderHeightOffsetScale` 和 top 正向 Y 偏移；如果美术预期 grid 增长倍率或终点高度不同，需要新增 Level87 本地配置。
- P1：`SpriteRenderer.size.y` 的视觉展开方向取决于 sprite draw mode、pivot 和 Transform 位置。静态文本只能证明 `m_DrawMode: 2` 与初始高度为 0，不能证明 PlayMode 下视觉方向一定符合 top 移动。
- P2：未对 prefab 执行全量 whitespace 清理；当前工作区 prefab 存在其它未提交差异，本风险记录只覆盖 grid 运行时增长链路。

### 验证证据
- 静态源码证据：`Level87Ctrl.cs` 中 `ApplySliderOffset(...)` 计算 `scaledOffset` 后调用 `SetGridHeight(scaledOffset)`；`RestoreSliderInitialState()` 调用 `SetGridHeight(0f)`；`ResolveReferences()` 为空时调用 `FindNamedChild<SpriteRenderer>("grid")`。
- 静态 prefab 证据：`Level_087.prefab` 存在名为 `grid` 的对象，`SpriteRenderer.m_DrawMode: 2`，`m_Size: {x: 2.08, y: 0}`。
- 静态禁用调用扫描未发现本次 grid 改动新增 DB、Save 系统、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、OnValidate、ExecuteAlways、ExecuteInEditMode、Unity `Debug.Log`、Analytics、IAP 或 Advertisement 调用；命中的 `GetEnemySavePosition` 属于既有足球表现命名，不是存档链路。
- `#region` / `#endregion` 数量检查为 11 / 11。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已通过，结果为 0 error / 1114 warnings；warning 属于当前工程既有范围。
- `git diff --check -- Assets\Game\Scripts\Level\LevelItem\Level_87\Level87Ctrl.cs` 未报告脚本 whitespace error，仅提示 LF/CRLF 规范化。

### 未验证项
- 未执行 Unity PlayMode：仍需验证 top 拖拽时 grid 从 0 展开、吸附到 `topEndPosition2` 后高度稳定、失败重开/退出/Disable 后恢复为 0。
- 未执行 Unity Inspector 检查：仍需确认 `grid` 的 SpriteRenderer、pivot、sorting layer/order 和与 top/slider 的视觉对齐。
- 未验证 Android/iOS 横屏触摸、快速拖拽、快速重开和 60 秒帧稳定性。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中 `grid` 字段 Tooltip、按名称解析、`ApplySliderOffset(...)` 对 grid 的更新、`RestoreSliderInitialState()` 对 grid 的归零和 `SetGridHeight(...)` 方法。
- 同步删除 `Level87SoccerTop_Feature.md`、本风险记录和 `DevelopMD/已有功能设计与使用记录.md` 中本次 grid 增长记录。

## 2026-06-23 Level84 successUI 迁移到 Level87 风险记录

### 原行为基线
- `Level84Ctrl.cs` 当前在 `StartSuccessFlow()` 中调用 `successUI.gameObject.SetActive(true)`，成功等待期间显示本地成功 UI，并在 `successDelay` 后调用 `BaseLevelCtrl.SuccessStrightly()`。
- `Level87Ctrl.cs` 变更前已有 top 解锁、成功碰撞标记、`StartSuccessFlow()`、成功欢呼音效和 `SuccessStrightly()` 归口，但没有独立成功 UI/Canvas 显示控制。
- Level87 成功条件仍应保持为：top 已触发、本次射门处于成功候选阶段、成功碰撞/入口已标记；本次不应迁移 Level84 的 `SuccessArea` 矩形检测逻辑。

### 影响文件和资源
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87SoccerTop_Feature.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87RiskAssessment.md`
- `DevelopMD/已有功能设计与使用记录.md`
- 关联但未修改的资源：`Assets/Game/Prefabs/Level/Level_087.prefab`，用于后续绑定或命名匹配成功 UI/Canvas。

### 已缓解
- success UI 被限制为 Level87 本地 `GameObject` 显隐，不新增资源加载、实例化、存档、配置、analytics、广告/IAP 或平台 SDK 链路。
- 成功判定仍沿用 Level87 现有 `topTriggered`、`isSuccessShotActive` 和 `successAreaEntered` 检查，没有引入 Level84 的矩形成功区判定。
- `SetSuccessUIVisible(...)` 对空引用安全 no-op，降低 prefab 尚未绑定时阻断成功流程的风险。
- `ResetPresentationState()` 会隐藏 success UI，覆盖 Awake、OnEnable、Disable、retry/exit 触发重置等路径。
- `CompleteFailure()` 会隐藏 success UI，降低同一次尝试从成功候选回到失败结算时出现成功 UI 残留的风险。
- `StartSuccessFlow()` 在成功等待开始时显示 success UI，然后继续复用既有 `footballAudienceCelebrate` 和 `SuccessStrightly()`，不会改变通关归口。

### 新增/剩余风险
- P1：Level87 prefab 静态文本未确认已有 `successUI` 绑定或匹配命名对象。若未绑定，代码会安全 no-op，但玩家看不到新增成功 UI。
- P1：脚本自动解析只覆盖 `successUI`、`SuccessUI`、`successCanvas`、`SuccessCanvas` 四种名称；若实际新增 canvas 名称不同，需要 Inspector 手动绑定或补充解析名。
- P1：success UI 显示时机在 `StartSuccessFlow()` 中，位于隐藏选择面板和共享 `use_btn` 之后；具体视觉层级、遮挡关系和成功等待时长需要 PlayMode 验证。
- P2：`CompleteSuccess()` 当前未主动隐藏 success UI，和 Level84 的“成功等待阶段显示直到关卡成功切换”语义一致；如果 Level87 成功后外部流程不立即切场，可能需要后续明确是否隐藏。

### 验证证据
- 静态源码证据：`Level87Ctrl.cs` 新增 `successUI` 字段，Tooltip 明确用于成功等待阶段 UI/Canvas。
- 静态源码证据：`StartSuccessFlow()` 调用 `SetSuccessUIVisible(true)`；`ResetPresentationState()` 和 `CompleteFailure()` 调用 `SetSuccessUIVisible(false)`。
- 静态源码证据：`ResolveReferences()` 尝试按 `successUI`、`SuccessUI`、`successCanvas`、`SuccessCanvas` 自动解析；`SetSuccessUIVisible(...)` 对空引用 no-op。
- 静态扫描确认本次 success UI 改动未新增 DB、Save 系统、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、OnValidate、ExecuteAlways、ExecuteInEditMode、Unity `Debug.Log`、Analytics、IAP 或 Advertisement 调用。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已通过，结果为 0 error / 96 warnings；warning 属于当前工程既有范围。

### 未验证项
- 未执行 Unity PlayMode：仍需验证 top 后成功射门进入成功等待阶段时 success UI 显示，等待 `successDelay` 后通关。
- 未执行 Unity Inspector 检查：仍需确认 Level87 prefab 是否已有 success UI/Canvas、字段是否绑定、命名是否匹配自动解析。
- 未验证失败、retry、exit、Disable/Destroy 中断成功等待时 success UI 是否按玩家视角及时隐藏。
- 未验证 Android/iOS 横屏适配、触摸遮挡和 60 秒帧稳定性。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中 `successUI` 字段、自动解析、`SetSuccessUIVisible(...)` helper，以及 `StartSuccessFlow()`、`CompleteFailure()`、`ResetPresentationState()` 中的显隐调用。
- 同步删除 `Level87SoccerTop_Feature.md`、本风险记录和 `DevelopMD/已有功能设计与使用记录.md` 中本次 success UI 迁移记录。

## 2026-06-23 Level87 grid 同步为 grids 数组风险记录

### 原行为基线
- `Level87Ctrl.cs` 已有 `SpriteRenderer[] grids` 字段，但局部实现仍残留 `grid` 单对象解析和写入逻辑。
- `Level_087.prefab` 静态文本显示存在 `grid` 与 `grid (1)` 两个网格对象；单对象解析只能覆盖其中一个，且当前字段名不一致会导致编译失败。
- 既有 grid 增长逻辑入口为 `ApplySliderOffset(...)` 和 `RestoreSliderInitialState()`，需要继续保持 top 移动增长和重置归零行为。

### 影响文件和资源
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87SoccerTop_Feature.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87RiskAssessment.md`
- `DevelopMD/已有功能设计与使用记录.md`
- `Assets/Game/Prefabs/Level/Level_087.prefab`：仅作为已有 `grid` / `grid (1)` 对象和初始 size 证据；本次未主动改写 prefab。

### 已缓解
- `ResolveReferences()` 现在统一调用 `ResolveDefaultGrids()`，未手动绑定时会收集本关子物体中名称为 `grid` 或以 `grid (` 开头的 `SpriteRenderer`。
- `SetGridHeight(...)` 改为遍历 `grids` 数组，统一写入 `SpriteRenderer.size.y`，保留 `size.x`，高度限制不小于 0。
- `ApplySliderOffset(...)` 和 `RestoreSliderInitialState()` 未改变调用入口，因此 top 移动、top 吸附完成、失败重开、退出和 Disable/Destroy 清理仍走原本的增长/归零路径。
- 本次未新增存档、配置、资源加载、实例化、editor lifecycle、analytics、广告/IAP 或平台 SDK 链路。

### 新增/剩余风险
- P1：`grids` 自动解析依赖对象名 `grid` / `grid (...)`。若 prefab 后续改名、移出控制器子级或缺失 `SpriteRenderer`，对应网格不会被更新。
- P1：两个网格统一使用同一个 `scaledOffset` 高度。如果美术预期两个网格不同高度、不同倍率或不同展开时机，需要新增 Level87 本地配置。
- P1：`SpriteRenderer.size.y` 的视觉展开方向仍取决于每个 sprite 的 draw mode、pivot、Transform 位置和排序层；静态文本不能替代 PlayMode 视觉验证。
- P2：当前工作区已有其它 Level87 prefab 差异，本次风险记录只覆盖脚本 `grids` 数组同步，不覆盖其它 prefab 改动。

### 验证证据
- 静态源码证据：`Level87Ctrl.cs` 中 `ResolveReferences()` 调用 `ResolveDefaultGrids()`，`SetGridHeight(...)` 遍历 `grids`，`IsGridRenderer(...)` 匹配 `grid` 和 `grid (` 前缀。
- 静态源码扫描确认脚本中已无 `grid` 单变量引用；剩余 `grid` 只作为对象名字符串。
- 静态 prefab 证据：`Level_087.prefab` 存在 `m_Name: grid` 和 `m_Name: grid (1)`。
- 禁用调用扫描未发现本次 grids 同步新增 DB、Save 系统、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、OnValidate、Reset、ExecuteAlways、ExecuteInEditMode、Unity `Debug.Log`、Analytics、IAP 或 Advertisement 调用；命中的 `GetEnemySavePosition` 属于既有足球表现命名，不是存档链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已通过，结果为 0 error / 96 warnings；warnings 属于当前工程既有范围。

### 未验证项
- 未执行 Unity PlayMode：仍需验证 `grid` 和 `grid (1)` 是否随 top 拖拽/吸附同时增长，失败重开、退出、Disable/Destroy 后是否同时恢复为 0。
- 未执行 Unity Inspector 检查：仍需确认 `grids` 手动绑定或自动解析结果、两个网格的 SpriteRenderer、pivot、sorting layer/order 和视觉对齐。
- 未验证 Android/iOS 横屏触摸、快速拖拽、快速重开和 60 秒帧稳定性。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中 `grids` Tooltip、`ResolveDefaultGrids()`、`IsGridRenderer(...)` 和 `SetGridHeight(...)` 数组遍历逻辑。
- 同步删除 `Level87SoccerTop_Feature.md`、本风险记录和 `DevelopMD/已有功能设计与使用记录.md` 中本次 grids 同步记录。

## 2026-06-23 Level87 blockFailure、固定成功射门方向与普通高射跳过阻挡风险记录

### 原行为基线
- top 后成功候选射门仍使用点击瞬间冻结的箭头方向，`Level87Ctrl.cs` 变更前没有单独的固定成功射门方向配置。
- 既有 `PlayEnemyBlock()` 是 top 未触发的失败阻挡路径：enemy 入场到 football 目标点，播放 `football_save`，停止 football tween，切 Dynamic 后施加 `blockForce`，播放 knock / `footballOut` / `trap`，再等待失败重置。
- top 后成功候选射门需要球进入成功 collider 后才会触发 `StartSuccessFlow()`；该成功归口由 `topTriggered`、`isSuccessShotActive` 和 `successAreaEntered` 共同保护。
- 普通射门此前不区分 height 阈值，都会进入 enemy 阻挡表现和反弹失败链路。

### 影响文件和资源
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`
- `Assets/Game/Scripts/Level/Actor/Level84ActorCtrl.cs`
- `Assets/Game/Prefabs/Level/Level_087.prefab`：作为已有 `endPoint` 子物体和当前字段未绑定的静态证据；本次未主动改写 prefab。
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87SoccerTop_Feature.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87RiskAssessment.md`
- `DevelopMD/已有功能设计与使用记录.md`

### 已缓解
- top 后成功候选射门使用新的 `PlayEnemyBlockFailure(...)`，不复用 `PlayEnemyBlock()` 的 football 反弹逻辑，降低成功球被 enemy 打出球网的风险。
- `PlayEnemyBlockFailure(...)` 只移动 enemy 并播放 `blockFailure` 动画，不调用 football `DOKill()`、不切 Dynamic、不调用 `AddForce(...)`、不播放 knock、`footballOut` 或 `trap`。
- `endPoint` 字段为空时会按子物体名自动解析；当前 prefab 静态文本存在 `m_Name: endPoint`，可覆盖字段未手动绑定的风险。
- `topTriggeredShotDirection` 为零时保持原有冻结箭头方向，降低未配置时改变成功射门方向的风险。
- 普通射门的 height 阈值只在 `topTriggered == false` 时生效，不影响 top 后成功候选射门。
- 普通高射跳过 enemyBlock 后仍调用 `CompleteFailure()`，且成功归口仍要求 `topTriggered && isSuccessShotActive && successAreaEntered`，降低 top 未触发时误通关风险。
- 新增公共 API `Level84ActorCtrl.PlayBlockFailure()` 有中文注释，说明其只用于表现，不改变胜负、存档、事件或物理状态。
- 本次未新增存档、配置、资源加载、实例化、editor lifecycle、analytics、广告/IAP 或平台 SDK 链路。

### 新增/剩余风险
- P1：`blockFailure` 动画名是否存在于 enemy 当前 Spine 动画资源未经过 PlayMode 验证；缺失时会走 `PlaySoccerAction(...)` 的 warning / 等待保护，但表现验收不通过。
- P1：`endPoint` 字段当前静态文本为 `{fileID: 0}`，运行时依赖按名称解析；若后续 prefab 改名或移出控制器子级，enemy 守门失败终点会回退到 football 目标点。
- P1：`topTriggeredShotDirection` 默认零向量，未配置时仍沿用冻结箭头方向；若策划验收要求固定方向，必须在 Inspector 中配置非零向量。
- P1：普通高射跳过阻挡后没有 enemy 视觉反馈，失败等待节奏从“半程阻挡 + enemy 可见 + reset delay”变为“完整飞行 + reset delay”，需要确认玩家是否能理解失败原因。
- P1：`normalShotSkipEnemyBlockHeight` 默认 0.25，和当前 `shotHeightRange` prefab 值 `{x: 0.3, y: 0}` 的关系意味着只有接近高角度端的普通射门会触发；实际阈值仍需 PlayMode 调整。
- P2：普通高射使用 `shotForce * 2` 扩大 DOTween 距离，可能让 football 离开镜头或撞到未预期 collider；静态代码无法证明最终落点符合视觉预期。
- P2：`PlayEnemyBlockFailure(...)` 在成功碰撞未发生时不会主动恢复 enemy，等待由成功碰撞、retry、exit、disable、destroy 或 reset 结束；当前行为与 top 后等待成功碰撞的设计一致，但需要手动验证异常中断清理。

### 验证证据
- 静态源码证据：`Level87Ctrl.PlayUnlockedShot(...)` 调用 `PlayForceShot()` 后启动 `PlayEnemyBlockFailure(currentSession)`；该方法只控制 enemy 入场和 `PlayEnemyBlockFailureAnimation()`。
- 静态源码证据：`PlayEnemyBlockFailure(...)` 没有 football `DOKill()`、`SwitchFootballState(true)`、`AddForce(...)`、knock、`footballOut` 或 `trap` 调用。
- 静态源码证据：`Level84ActorCtrl` 新增 `BlockFailureAnimName = "blockFailure"` 和 `PlayBlockFailure()`。
- 静态源码证据：`GetShotDirection()` 在 `topTriggeredShotDirection.sqrMagnitude > 0.0001f` 时使用配置方向，否则使用冻结/当前箭头方向。
- 静态源码证据：`FreezeShotDirection()` 同时冻结 `frozenShotHeight`；`ShouldSkipEnemyBlockForNormalShot()` 使用冻结 height 判断普通射门是否超过 `normalShotSkipEnemyBlockHeight`。
- 静态 prefab 证据：`Level_087.prefab` 存在 `m_Name: endPoint`，且当前脚本字段 `endPoint: {fileID: 0}`，因此自动解析仍是必要兜底。
- 静态禁用调用扫描未发现本次改动新增 DB、Save 系统、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、OnValidate、Reset、ExecuteAlways、ExecuteInEditMode、Unity `Debug.Log`、Analytics、IAP 或 Advertisement 调用；命中的 `football_save` / `GetEnemySavePosition` 属于既有足球表现命名，不是存档链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已通过，结果为 0 error / 96 warnings；warnings 属于当前工程既有范围。

### 未验证项
- 未执行 Unity PlayMode：仍需验证 `blockFailure` 动画存在、播放完整、和 `PlayEnemyBlock()` 的入场位置/节奏一致。
- 未执行 Unity PlayMode：仍需验证 top 后固定方向配置非零时 football 能进入球网并触发成功 collider。
- 未执行 Unity PlayMode：仍需验证普通 height 超阈值时 football 距离翻倍、enemy 不入场、不播放 `football_save`、不触发 knock / `footballOut` / `trap`，随后按预期失败重置。
- 未执行 Unity Inspector 检查：仍需确认 `endPoint`、`topTriggeredShotDirection` 和 `normalShotSkipEnemyBlockHeight` 的最终关卡配置。
- 未验证 Android/iOS 横屏触摸、快速点击、retry/exit/disable 中断和 60 秒帧稳定性。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中 `topTriggeredShotDirection`、`endPoint`、`PlayEnemyBlockFailure(...)`、`PlayEnemyBlockFailureAnimation()`、`GetEnemyBlockFailurePosition()` 和 `GetShotDirection()` 固定方向分支。
- 回滚 `Level87Ctrl.cs` 中 `normalShotSkipEnemyBlockHeight`、`frozenShotHeight`、`ShouldSkipEnemyBlockForNormalShot()`、`WaitNormalShotFailure(...)` 和 `PlayForceShot(float forceMultiplier)`。
- 回滚 `Level84ActorCtrl.cs` 中 `BlockFailureAnimName` 和 `PlayBlockFailure()`。
- 同步删除 `Level87SoccerTop_Feature.md`、本风险记录和 `DevelopMD/已有功能设计与使用记录.md` 中本次记录。

## 2026-06-23 Level87 top 后 AddForce 成功射门风险记录

### 原行为基线
- top 后成功候选射门此前调用 `PlayForceShot()`，沿 `GetShotDirection()` 返回方向执行 DOTween `DOMove(...)`，距离为 `shotForce`。
- 普通失败射门仍调用 `PlayForceShot(...)`，并在未跳过 enemyBlock 时进入 `PlayEnemyBlock()` 反弹失败链路。
- 成功归口没有改变：仍需 top 已触发、本次成功候选射门激活，并由 success collider / success area 标记 `successAreaEntered`。

### 影响文件和资源
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87SoccerTop_Feature.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87RiskAssessment.md`
- `DevelopMD/已有功能设计与使用记录.md`
- 本次不改写 Level87 prefab；依赖既有 `football` 的 `Rigidbody2D` 引用或运行时自动解析。

### 已缓解
- AddForce 被限制在 `PlayTopTriggeredForceShot()`，只由 `PlayUnlockedShot(...)` 调用；普通射门仍保留 DOTween 逻辑和普通 high height 分支。
- `PlayTopTriggeredForceShot()` 复用 `GetShotDirection()`，因此固定方向配置 `topTriggeredShotDirection` 仍生效。
- `PlayTopTriggeredForceShot()` 在 AddForce 前停止 football 旧 tween，并调用 `SwitchFootballState(true)` 清速度后切 Dynamic，降低 tween 和物理同时控制 football 的风险。
- `StartSuccessFlow()`、`CompleteFailure()` 和 `ResetPresentationState()` 仍调用 `football.DOKill()` 和 `SwitchFootballState(false)`，用于成功、失败、重开和清理时收回物理状态。
- `fbRigidbody` 缺失时只输出 Bear warning 并跳过 AddForce，不新增异常抛出或误通关入口。
- 本次未新增存档、配置、资源加载、实例化、editor lifecycle、analytics、广告/IAP 或平台 SDK 链路。

### 新增/剩余风险
- P1：top 后成功射门从 DOTween 轨迹改为物理冲量，真实落点取决于 Rigidbody2D、collider、gravityScale、碰撞层和场景阻挡；静态代码无法证明必定进网。
- P1：`shotForce * 2` 在 AddForce 语义下不再是 DOTween 距离，而是 Impulse 冲量大小；需要 PlayMode 调参确认与现有 prefab `shotForce` 值匹配。
- P1：Level87 当前 `SwitchFootballState()` 只切 bodyType 并清速度，不缓存/修改 gravityScale；如果 football prefab 当前 gravityScale 非预期，AddForce 轨迹可能偏离。
- P2：`footballTargetPosition` 现在只是基于方向和 `shotForce * 2` 的参考点，用于 `endPoint` 缺失 fallback，不代表物理最终落点。
- P2：物理冲量可能触发额外地面/边界/触地音效回调，仍需 PlayMode 验证不会导致重复音效或提前中断。

### 验证证据
- 静态源码证据：`PlayUnlockedShot(...)` 调用 `PlayTopTriggeredForceShot()` 和 `PlayEnemyBlockFailure(currentSession)`。
- 静态源码证据：`PlayTopTriggeredForceShot()` 调用 `SwitchFootballState(true)` 后执行 `fbRigidbody.AddForce(forceDirection * force, ForceMode2D.Impulse)`，其中 `force = Mathf.Max(0f, shotForce) * TopTriggeredShotForceMultiplier`，当前 multiplier 为 2。
- 静态源码证据：`PlayBlockedShot(...)` 仍调用 `PlayForceShot(...)`，普通射门未切到 AddForce。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已通过，结果为 0 error / 96 warnings；warnings 属于当前工程既有范围。

### 未验证项
- 未执行 Unity PlayMode：仍需验证 top 后 AddForce 轨迹能让 football 成功进入球网并触发 success collider。
- 未执行 Unity PlayMode：仍需验证成功后 `StartSuccessFlow()` 能及时收回 Dynamic 状态，不残留速度或额外碰撞。
- 未执行 Unity Inspector 检查：仍需确认 football `Rigidbody2D`、gravityScale、collision detection、layer 和 success collider 绑定。
- 未验证 Android/iOS 横屏真机物理表现一致性、快速点击、retry/exit/disable 中断和 60 秒帧稳定性。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中 `TopTriggeredShotForceMultiplier`、`PlayTopTriggeredForceShot()`，并将 `PlayUnlockedShot(...)` 恢复为调用 `PlayForceShot()`。
- 同步删除 `Level87SoccerTop_Feature.md`、本风险记录和 `DevelopMD/已有功能设计与使用记录.md` 中本次记录。
