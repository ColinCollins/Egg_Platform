# Level87 足球球框 top 功能记录

## 需求背景

`Level87Ctrl.cs` 顶部 version 1.0.0 注释要求补齐 Level87 足球流程：actor 进入 EnterArea 后显示共享 `use_btn` 和本地 Canvas，箭头在角度范围内循环摆动；点击按钮按当前角度给足球施加 force。默认情况下 enemy 拦截并失败；当 `topObj` 沿 Y 轴拖到超过 float 字段 `topTriggerPositionY` 后触发 `OnTriggerTop()`，再移动到 Transform 字段 `topEndPosition2.localPosition` 并锁定，slider 高度增加，`ArrowRange_green` 同时播放效果并填满；此后点击踢球按钮，足球碰到成功 collider 后等待 2 秒通关。

本次实现只补齐 Level87 本地逻辑，不修改共享 `DragableItem`、`GamePanel_UseBtnCommonFeature`、`Level84ActorCtrl`、存档、配置、热更资源、广告、IAP、打点或平台 SDK。

## 影响文件和系统

- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`：Level87 本地进入、use_btn、箭头、top、slider、足球受力、enemy 阻挡、成功/失败和清理流程。
- `Assets/Game/Prefabs/Level/Level_087.prefab`：本次未手动修改；文本证据显示已有 `Canvas`、`football`、`top`、`ArrowRange_green`、`ArrowRange_normal`、`EnterTrigger` 和 `qiukuang`。
- `Assets/Game/UI/GamePanel/Resources/GamePlayPanel_024_87.prefab`：文本证据显示存在 `GamePanel_UseBtnCommonFeature.Use_btn` 绑定。
- 复用既有系统：`SwitchObjActiveEvent`、`GamePanel_UseBtnCommonFeature`、`CustomButton.OnClick`、`BaseLevelCtrl.SetCtrlable(...)`、`BaseLevelCtrl.SuccessStrightly()`、`GameResetEvent(GameResetType.Failed)`、`DragableItem`、`Level84ActorCtrl`、DOTween、UniTask、Unity `Rigidbody2D`。

## 原行为基线

- `Level87Ctrl.cs` 已有 version 1.0.0 注释和部分足球流程骨架，但未完整实现 use_btn 获取/绑定、top 抬高、slider 高度联动、`ArrowRange_green` 填满、top 后成功路径和统一清理。
- `HEAD` 中 Level87 默认已有 football、enemy、actor 足球表现和失败/成功骨架；当前工作区的顶部需求注释已扩展为 `topTriggerPositionY` 触发和 `topEndPosition2` 吸附目标，后续实现需按当前注释同步，不应再无关改写该注释。
- `Level_087.prefab` 文本证据显示 `top` 挂有 `DragableItem`，`football` 挂有 `Rigidbody2D`，`Canvas`、`qiukuang` 等对象已存在；当前 `Level87Ctrl` 的新增引用主要依赖运行时按名称自动解析，避免 prefab YAML churn。

## 变更后行为

- Actor 进入 `EnterTrigger` 后，Level87 锁定 actor 输入，播放 football 入场表现，显示本地 `Canvas` 并通过 `SwitchObjActiveEvent(true)` 显示共享 `use_btn`。
- `Start()` / `OnEnable()` 会解析 `PlayCtrl.Instance.CurrentGamePlayPanel` 上的 `GamePanel_UseBtnCommonFeature.Use_btn`，订阅按钮点击；获取失败时只输出 warning 并安全跳过。
- 箭头对象按 `arrowRotateRange` 在本地循环 yoyo 转动；点击 `use_btn` 时按当前角度映射 `shotHeightRange`，对 football 施加 `new Vector2(1, height)` 方向的冲量。
- `shotHeightRange` 未配置为非零值时，使用脚本本地默认高度范围，避免 prefab 当前 `{0,0}` 造成射门方向退化。
- top 未触发时，点击 `use_btn` 会播放 actor 踢球表现、football 受力、enemy 入场扑救并给 football 追加阻挡 force，等待后派发 `GameResetEvent(GameResetType.Failed)`。
- `top` 沿 Y 轴拖到超过 float 字段 `topTriggerPositionY` 后调用 `OnTriggerTop()`：top 移动/吸附到 Transform 字段 `topEndPosition2.localPosition`，`DragableItem.SetEnableDrag(false)` 禁止继续拖拽，两个 slider 根据 top 正向 Y 偏移增加 `SpriteRenderer.size.y` / Inspector `Size.height`，`ArrowRange_green.fillAmount` 变为 1 并同时播放对应效果。
- top 已触发后，点击 `use_btn` 会进入成功候选射门；只有显式成功碰撞入口会置位成功，随后等待 `successDelay` 默认 2 秒并调用 `BaseLevelCtrl.SuccessStrightly()`，`qiukuang` / `successTarget` 只作为视觉或调试参考。
- 成功碰撞入口同时提供 `EnterCollision2D(Collider2D)`、`enterCollision2D(Collider2D)`、`EnterCollision2D(Collision2D)`、`enterCollision2D(Collision2D)` 和 `EnterSuccessArea()`，用于兼容注释里的小写入口、UnityEvent 和 `LayerCollisionDetector`。
- Disable、Destroy、失败、成功和重置路径会停止本地 tween、解绑按钮、隐藏 use_btn、恢复 top/slider/绿色范围、停止足球和 enemy 表现，并通过 session id 避免异步等待晚到结算。

## 数据、资源、UI、事件和平台流

- 数据 / 存档 / 配置：未新增 DB、PlayerPrefs、Luban、RemoteConfig、存档结构或配置表读取。
- 资源：未新增 `Resources`、`StreamingAssets`、YooAsset 或运行时实例化；依赖 prefab 已有对象和运行时自动解析。
- UI：通过既有 `SwitchObjActiveEvent` 显示/隐藏 GamePlayPanel 的 `use_btn`；Level87 本地 `Canvas` 只作为角度 UI。
- 事件：默认失败路径复用 `GameResetEvent(GameResetType.Failed)`；成功路径复用 `BaseLevelCtrl.SuccessStrightly()`；未新增成功事件、解锁、存档或 analytics 直连。
- 平台：未修改 Android/iOS SDK、ProjectSettings 或构建设置。移动端横屏触控仍需补验。

## 验证记录

- 静态源码检查确认：`Level87Ctrl.cs` 新增 use_btn 解析、top/slider/arrowRangeGreen、足球 force 射门、成功碰撞入口和 session 清理逻辑。
- 静态 prefab 文本证据确认：`Level_087.prefab` 存在 `Canvas`、`football`、`top`、`ArrowRange_green`、`ArrowRange_normal`、`EnterTrigger` 和 `qiukuang`；`top` 挂有 `DragableItem`，`football` 挂有 `Rigidbody2D`。
- 静态 UI prefab 文本证据确认：`GamePlayPanel_024_87.prefab` 挂有 `GamePanel_UseBtnCommonFeature`，且 `Use_btn` 非空。
- 静态关键字扫描确认：`Level87Ctrl.cs` 未新增 DB、Save、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、OnValidate、Reset、ExecuteAlways、ExecuteInEditMode 或 Unity `Debug.Log` 入口。
- `#region` / `#endregion` 数量已检查为一致。
- `dotnet build .\Assembly-CSharp.csproj` 已通过，结果为 0 error / 97 warning；本次修复后未再出现 `Level87Ctrl.cs` 相关 warning。
- `git diff --check` 针对本次新增中文文档、`DevelopMD/已有功能设计与使用记录.md`、Speckit spec/checklist 和 `.specify/feature.json` 通过。单独检查 `Level87Ctrl.cs` 时仅报告顶部需求注释空白行已有 trailing whitespace；为避免无关改写当前 version 1.0.0 注释内容，本次未清理这些注释空白行。

## 剩余风险和未验证项

- 未执行 Unity PlayMode，仍需验证进入、普通射门失败、top 抬高锁定、slider 高度、绿色范围、成功碰撞、2 秒成功等待、失败重开、退出/disable 和快速连点。
- 未验证 actor/enemy 上的 `Level84ActorCtrl` 在 Level87 prefab 实例中的真实解析、Spine 动画节奏和动作恢复。
- 未验证 Android/iOS 横屏触控；top 拖拽、use_btn 点击和足球物理在真机上仍需补验。
- `topTriggerPositionY` 需要后续在脚本中明确为 float local Y 阈值，`topEndPosition2` 需要在脚本和 prefab 绑定中明确为 Transform，不再使用旧 fallback；真实成功碰撞 collider / UnityEvent 绑定仍需在 Inspector 或 PlayMode 中确认。
- 当前工作区存在其他无关脏文件，本记录只覆盖 Level87 本地实现和相关规格/文档。

## 回滚路径

如果 Level87 足球 top 功能不被接受，回滚 `Level87Ctrl.cs` 中本次新增的 use_btn、top、slider、arrowRangeGreen、force 射门、成功碰撞和清理逻辑；删除本功能记录、同目录风险评估、`DevelopMD/已有功能设计与使用记录.md` 的本次记录，以及 `specs/028-level87-soccer-top` 规格目录。

## 2026-06-22 最终同步记录

- `Level87Ctrl.cs` 当前已改为显式 `topTriggerPositionY` / `topEndPosition2` 模式：top 只在 `topObj.localPosition.y >= topTriggerPositionY` 时触发，到位吸附目标只取 `topEndPosition2.localPosition`。
- 旧 `topTriggerTarget` / `topTriggerLocalYOffset` active fallback 已移除；`topEndPosition2` 未绑定时只记录 warning 并忽略 top 触发，不再用默认 offset 兜底。
- `Level_087.prefab` 已新增并绑定 `topEndPosition2` 空 Transform，位置为 `qiukuang` 下本地 `(-1.383, 3, 0)`，同时写入 `topTriggerPositionY: 3`，改动限定在 Level87 prefab。
- top 后射门不再使用 `successShotMaxWait`，也不再因为等待超时自动失败；当前等待只能由显式成功碰撞/UnityEvent、retry、exit、disable、destroy 或其他 cleanup 边界结束。
- `qiukuang` / `successTarget` 只保留为视觉或调试参考，不参与成功判定；`IsFootballNearSuccessTarget()`、`successTargetRadius` 主动成功路径已移除。
- 成功入口仍为 `EnterCollision2D` / `enterCollision2D` / `EnterSuccessArea()`，仅在 top 已触发且本次射门处于成功候选阶段时接受，随后等待 `successDelay` 默认 2 秒调用 `SuccessStrightly()`。
- 最新静态编译验证为 `dotnet build .\Assembly-CSharp.csproj --no-restore` 通过，结果 0 error / 1115 warning；warning 为项目既有范围，未出现 Level87 编译错误。
- 本次实现没有继续改写脚本顶部 version 1.0.0 需求注释；只更新了新增/变更序列化字段和公开入口附近的说明。
## 2026-06-22 version 1.1 arrow 冻结补充

### 需求背景
- `Level87Ctrl.cs` 顶部 version 1.1 注释要求：触发踢球动画后，arrow 停止移动，并保留当前 range 映射出的 height 角度。
- Speckit 澄清已确定：`use_btn` 触发射门后立即停止 arrow，保持到本次尝试 cleanup；下一次进入或 retry 后恢复 arrow loop。

### 影响文件和系统
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`：新增本次射门冻结状态，射门入口冻结方向，football force 使用冻结方向。
- `specs/028-level87-soccer-top/quickstart.md`：补充 version 1.1 手工验证步骤。
- 未修改共享 `GamePanel_UseBtnCommonFeature`、`DragableItem`、`Level84ActorCtrl`、存档、配置、资源加载、analytics、平台 SDK 或 Level87 顶部旧注释文本。

### 行为前后
- 修改前：`ResolveShot()` 先等待 actor 踢球表现，随后 `PlayForceShot()` 读取当时 arrow 当前角度；等待期间 arrow tween 可能继续改变本次射门 height。
- 修改后：`ResolveShot()` 在隐藏 use_btn、切换 football 状态后立即调用本地冻结逻辑，先保存 `GetCurrentShotDirection()` 的结果并停止 arrow tween；`PlayForceShot()` 优先使用已冻结方向，直到 cleanup 清空冻结状态。
- 重复点击仍受 `isResolvingShot` 保护；第一次点击冻结的方向在本次尝试内保持权威。
- `ResetPresentationState()` 会清空冻结方向；`OnEnable()`、重新进入或 retry 后仍会调用既有 `StartArrowLoop()` 恢复下一次 arrow 循环。

### 数据、UI、事件和平台流
- 数据/存档/配置：无新增。
- UI：只影响 Level87 本地 arrow tween 的停止和下一次恢复；不改变 shared use_btn 显隐来源。
- 事件：失败仍走 `GameResetEvent(GameResetType.Failed)`，成功仍走 `BaseLevelCtrl.SuccessStrightly()`。
- 平台：未修改 Android/iOS SDK；移动端触摸仍需 PlayMode/真机补验。

### 验证记录
- 静态代码证据：`ResolveShot()` 在 actor 踢球等待前冻结方向；`PlayForceShot()` 使用冻结方向；`ResetPresentationState()` 清空冻结状态。
- 已补充 `quickstart.md` 的 version 1.1 手工验证步骤。
- 待补运行验证：`dotnet build .\Assembly-CSharp.csproj --no-restore`、静态禁用调用扫描、`git diff --check`。

### 剩余风险
- 未执行 Unity PlayMode，尚未验证点击同帧视觉停止、失败 retry 后 arrow 恢复、top 后 10 秒等待时 arrow 保持停止。
- 未执行 Android/iOS 横屏触摸和快速连点验证。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中本次新增的冻结方向状态、`FreezeShotDirection()` / `ClearFrozenShotDirection()` 和 `PlayForceShot()` 使用冻结方向的改动。
- 同步移除本节文档、风险评估和 quickstart 中的 version 1.1 记录。

## 2026-06-22 version 2.0 DOTween 射门与 slider height 补充

### 需求背景
- `Level87Ctrl.cs` 顶部 version 2.0 注释补充要求：`PlayForceShot` 改为 DOTween 操作，并使用 `shotDuration` 记录移动时间；整理无效逻辑并整合相关属性；`sliderObj` 修改 height，不要改 scale。
- 用户后续澄清：`sliderObjs[i]` 用的是 `SpriteRenderer`，修改的是 Inspector `Size.height`，即代码中的 `SpriteRenderer.size.y`。
- 本次实现继续遵守“注释不要删改”，未改写脚本顶部既有需求注释内容。

### 影响文件和系统
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`：调整 football 主射门移动方式、enemy 阻挡前的 football tween 清理、slider `SpriteRenderer.size.y` 高度联动缓存和恢复。
- `specs/028-level87-soccer-top/spec.md` 与 checklist：同步 version 2.0 行为要求。
- 未修改共享 `GamePanel_UseBtnCommonFeature`、`DragableItem`、`Level84ActorCtrl`、存档、配置、资源加载、analytics 或平台 SDK。
- `shotForce` 的 Inspector Tooltip 原文仍描述“冲量”，已与 v2.0 DOTween 移动事实冲突；本次只同步该字段 Tooltip 语义，未删改脚本顶部需求注释内容。

### 行为前后
- 修改前：`PlayForceShot()` 中主射门刚体冲量逻辑已被注释，实际只停止 football tween，导致点击射门后 football 主移动表现缺失。
- 修改后：`PlayForceShot()` 停止旧 tween，切换 football 为 Kinematic 清速度，根据 version 1.1 冻结方向或当前箭头方向计算目标点，并使用 `shotDuration` 做线性 `DOMove`。
- 普通失败路径中 enemy 到达阻挡点后会先停止 football 主射门 tween，再保留原有 enemy 阻挡反弹 `AddForce` 表现，避免主射门 tween 与反弹同时争夺 football 位置。
- football 初始世界坐标现在会在 `CacheInitialState()` 中缓存，进入足球流程和 cleanup 会回到该初始坐标后再执行入场/重置，避免重复尝试从上一轮射门终点继续累计偏移。
- 失败结算前会停止 football tween、清理 football 刚体状态，并停止 enemy 表现，防止失败派发前仍残留 v2.0 主射门移动。
- slider 联动从修改 `localScale.y` 改为缓存并恢复 `SpriteRenderer.size.y` 初始高度，top 抬高时只修改 `SpriteRenderer.size.y`，不改变 Transform scale。

### 数据、资源、UI、事件和平台流
- 数据 / 存档 / 配置：无新增。
- 资源：无新增 `Resources`、`StreamingAssets`、YooAsset 或运行时实例化。
- 表现：只影响 Level87 本地两个 slider `SpriteRenderer` 的 `Size.height`；`ArrowRange_green.fillAmount` 逻辑不变。
- 事件：失败仍走 `GameResetEvent(GameResetType.Failed)`，成功仍走 `BaseLevelCtrl.SuccessStrightly()`。
- 平台：未修改 Android/iOS SDK 或构建设置；横屏触摸与帧稳定性仍需补验。

### 验证记录
- 静态代码证据：`PlayForceShot()` 已使用 `DOMove(..., shotDuration)`；`ApplySliderOffset()` 和 `RestoreSliderInitialState()` 已使用 `SpriteRenderer.size.y`，未继续写入 slider `localScale` 或 `RectTransform` 高度。
- 禁用调用扫描已执行：`Level87Ctrl.cs` 未新增 DB、Save、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、OnValidate、ExecuteAlways、ExecuteInEditMode、Debug.Log、Analytics、IAP 或 Advertisement 调用。扫描命中 `AddForce` 仅保留在 enemy 阻挡反弹路径；命中 `localScale` 为 football/use_btn 表现恢复或非 slider 表现逻辑。
- 旧字段扫描已执行：未命中 `topTriggerTarget`、`topTriggerLocalYOffset`、`successShotMaxWait`、`IsFootballNearSuccessTarget` 或 `successTargetRadius`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore` 已通过，结果 0 error / 97 warning；warning 属于当前工程既有范围，未阻塞 Level87 v2.0 静态验收。
- `git diff --check -- ..\specs\028-level87-soccer-top .\Assets\Game\Scripts\Level\LevelItem\Level_87 .\DevelopMD` 已复跑通过，未报告 whitespace error；Git 仅提示相关文件下次触碰时会进行 LF/CRLF 规范化。
- Unity PlayMode 和 Android/iOS 真机验证未执行，仍需补验。

### 剩余风险
- DOTween 直接移动 Transform 是否稳定触发当前成功碰撞链路，需要 Unity PlayMode 验证；静态代码只能证明入口存在，不能证明碰撞实际发生。
- enemy 阻挡前停止主射门 tween 后再施加反弹，视觉节奏需要 PlayMode 验证是否符合策划预期。
- slider 改为 `SpriteRenderer.size.y` 后，美术表现需要 Inspector/PlayMode 确认两个对象的 `Draw Mode` / `Size.height` 表现符合关卡预期。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中 `PlayForceShot()` 的 DOTween 移动、enemy 阻挡前 football tween 清理、football 初始位置复位、失败边界清理、slider `SpriteRenderer.size.y` 缓存/应用/恢复和 `shotForce` Tooltip 同步改动。
- 同步移除本节文档、风险记录、DevelopMD 记录和 Speckit v2.0 规格补充。

## 2026-06-23 Level84 音效迁移到 Level87

### 需求背景
- 用户要求将 `Level84Ctrl` 新增的音效相关代码移植到 `Level87Ctrl`。
- 本地代码证据显示 `Level84Ctrl` 当前通过既有 `AudioManager.PlaySound(...)` 播放 `whistle`、`grassCut`、`kickFootball`、`footballTouchGround`、`footballOut`、`trap` 和 `footballAudienceCelebrate`。
- Level87 已有足球入场、共享 `use_btn` 射门、enemy 阻挡、top 后成功候选和成功/失败清理流程，本次只补齐 Level87 本地音效调用和触地回调绑定，不重构玩法流程。

### 影响文件和系统
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`：新增 `AudioManager` 引用、足球流程音效调用和 `PlayFootballTouchGround()` 公开回调。
- `Assets/Game/Prefabs/Level/Level_087.prefab`：修正已有足球 `LayerTriggerDetector.onEnter` 的触地音效回调目标，使其指向本关 `Level87Ctrl.PlayFootballTouchGround()`。
- 复用既有系统：`AudioManager.PlaySound(...)`、`LayerTriggerDetector`、DOTween、UniTask、`Level84ActorCtrl`、`BaseLevelCtrl.SuccessStrightly()`、`GameResetEvent(GameResetType.Failed)`、共享 `GamePanel_UseBtnCommonFeature.Use_btn`。
- 未修改共享 `AudioManager`、`LayerTriggerDetector`、`Level84ActorCtrl`、`GamePanel_UseBtnCommonFeature`、存档、配置、资源加载、analytics、广告/IAP 或平台 SDK。

### 变更前基线
- `Level87Ctrl.cs` 变更前没有 `AudioManager.PlaySound(...)` 调用，足球流程只有动作、DOTween、按钮和胜负逻辑。
- `Level_087.prefab` 变更前已有足球触地 `LayerTriggerDetector`，`targetLayerMask.m_Bits = 64`，但 `onEnter` 中 `PlayFootballTouchGround` 的目标为空且类型仍指向 `Level84Ctrl`，无法可靠调用 Level87 本地脚本。
- `GamePlayPanel_024_87.prefab` 的共享 `Use_btn` 已挂载 `ClickAudio` 且 `AudioName: button`，因此本次没有新增按钮点击音效组件。

### 变更后行为
- `EnterSoccerFlow()` 在锁住 actor 输入后播放 `whistle`；football 入场 DOTween 完成并确认 session 仍有效后播放 `grassCut`，再显示 Level87 Canvas 和共享 `use_btn`。
- `PlayForceShot()` 在 football 有效并开始射门移动时播放 `kickFootball`；该入口同时覆盖 top 前失败射门和 top 后成功候选射门。
- 新增 `PlayFootballTouchGround()` 作为 prefab 触地触发器回调，只播放 `footballTouchGround`，不改变胜负、存档、事件或物理状态。
- `PlayEnemyBlock()` 在 enemy 阻挡时沿用 Level84 音效时机：足球切 Dynamic 并施加阻挡力后播放 `footballOut`；`knock` 粒子播放时同步播放 `trap`。
- `StartSuccessFlow()` 进入成功流程、停止 football/enemy 表现后播放 `footballAudienceCelebrate`，随后仍等待 `successDelay` 并复用 `BaseLevelCtrl.SuccessStrightly()`。
- `Level_087.prefab` 中已有足球触地回调现在绑定到 `Level87Ctrl` 实例，不再指向空目标的 `Level84Ctrl`。

### 数据、资源、UI、事件和平台流
- 数据 / 存档 / 配置：无新增 DB、PlayerPrefs、RemoteConfig、Luban 或存档结构访问。
- 资源：无新增 `Resources`、`StreamingAssets`、YooAsset 或运行时实例化；新增音效均通过既有 `AudioManager.PlaySound(string)` tag 路径调用。
- UI：共享 `use_btn` 的显示/隐藏和既有 `ClickAudio(button)` 不变；本次只影响 Level87 足球流程时序中的音效。
- 事件：失败仍派发 `GameResetEvent(GameResetType.Failed)`；成功仍调用 `BaseLevelCtrl.SuccessStrightly()`；触地音效回调不派发胜负事件。
- 平台：未修改 Android/iOS SDK、ProjectSettings 或构建配置；移动端横屏听感和触摸时序仍需补验。

### 验证记录
- 静态源码扫描确认 `Level87Ctrl.cs` 当前包含 `whistle`、`grassCut`、`kickFootball`、`footballTouchGround`、`footballOut`、`trap` 和 `footballAudienceCelebrate` 调用。
- 静态 prefab 扫描确认 `Level_087.prefab` 的 `PlayFootballTouchGround` 回调目标已为 `Level87Ctrl, Assembly-CSharp`，目标 fileID 为本关控制器组件。
- 静态禁用调用扫描未发现新增 `OnValidate`、`Reset(...)`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save 系统、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、Unity `Debug.Log`、Analytics、IAP 或 Advertisement 调用；命中的 `GetEnemySavePosition` 属于既有足球表现命名，不是存档链路。
- `#region` / `#endregion` 数量检查为 11 / 11。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 0 error / 1114 warnings；warning 属于当前工程既有范围。
- `git diff --check -- Assets\Game\Scripts\Level\LevelItem\Level_87\Level87Ctrl.cs` 未报告脚本 whitespace error，仅提示 LF/CRLF 规范化。`Level_087.prefab` 的 diff check 仍报告当前工作区已有 Unity YAML 空值行 trailing whitespace；本次未扩大清理这些非音效差异。

### 剩余风险和未验证项
- 尚未执行 Unity PlayMode，未验证 Sounds Good 中上述音效 tag 是否全部存在、播放时机和音量是否符合策划预期。
- 尚未 PlayMode 验证触地 `LayerTriggerDetector` 的 layer 64 是否只在预期地面触发，是否会因 football trigger 重复进入而重复播放 `footballTouchGround`。
- 尚未 PlayMode 验证 top 前失败射门、top 后成功候选射门、enemy 阻挡、成功等待和 retry/exit/disable 清理路径中的音效重叠情况。
- `Level_087.prefab` 在本次开始前已有其它未提交修改；本记录只覆盖音效回调目标和脚本调用点，未验证其它 prefab 差异。
- Android/iOS 横屏触摸、快速连点、重开/退出/Disable 清理和 60 秒帧稳定性仍待补验。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中新增的 `Game.Scripts.Common` using、`AudioManager.PlaySound(...)` 调用和 `PlayFootballTouchGround()` 公开回调。
- 回滚 `Level_087.prefab` 中足球 `LayerTriggerDetector.onEnter` 指向 `Level87Ctrl.PlayFootballTouchGround()` 的绑定修正。
- 同步删除本节功能记录、同日风险评估和 `DevelopMD/已有功能设计与使用记录.md` 对应条目。

## 2026-06-23 Level87 grid 随 top 增长

### 需求背景
- 用户新增了一个 `grid`，要求它配合 `top` 的移动表现，`size.height` 需要从 0 开始增长。
- 当前 Level87 已有 top 抬高后 slider 跟随增长的本地逻辑；本次复用同一段 top 正向 Y 偏移，不新增共享系统或跨关卡抽象。

### 影响文件和系统
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`：新增/接入 `grid` 的 `SpriteRenderer.size.y` 应用、重置和按名称自动解析。
- `Assets/Game/Prefabs/Level/Level_087.prefab`：文本证据显示已存在名为 `grid` 的子物体，`SpriteRenderer.m_DrawMode: 2`，`m_Size: {x: 2.08, y: 0}`；本次脚本按子物体名解析，未为该需求额外改写 prefab 绑定。
- 复用既有系统：top 拖拽、top snap DOTween、`ApplySliderOffset(...)`、`RestoreSliderInitialState()` 和 `SpriteRenderer.size`。
- 未修改共享 `DragableItem`、`GamePanel_UseBtnCommonFeature`、`Level84ActorCtrl`、存档、配置、资源加载、analytics、广告/IAP 或平台 SDK。

### 变更前基线
- top 移动时只更新 `sliderObjs` 的 `SpriteRenderer.size.y`，未同步更新新增的 `grid`。
- `grid` 的 prefab 文本初始高度为 0；如果脚本不写入 `SpriteRenderer.size.y`，运行时无法随 top 上移展开。

### 变更后行为
- `ResolveReferences()` 在 `grid` 未手动绑定时按子物体名 `grid` 自动查找 `SpriteRenderer`。
- `ApplySliderOffset(float offset)` 现在先计算 top 相对初始位置的正向偏移缩放值，再继续更新原 slider 高度，并始终调用本地 `SetGridHeight(scaledOffset)`。
- `SetGridHeight(...)` 只修改 `grid.size.y`，保留 `grid.size.x`，并把高度强制为不小于 0。
- `RestoreSliderInitialState()` 会在重置 slider 初始高度后调用 `SetGridHeight(0f)`，因此重新进入、失败重开、Disable/Destroy 清理时 grid 高度回到 0。
- 当 slider 引用为空或缓存缺失时，grid 仍会根据 top 偏移更新，避免新增 grid 被 slider 绑定状态阻断。

### 数据、资源、UI、事件和平台流
- 数据 / 存档 / 配置：无新增 DB、PlayerPrefs、RemoteConfig、Luban 或存档结构访问。
- 资源：无新增 `Resources`、`StreamingAssets`、YooAsset 或运行时实例化；使用 prefab 中已有 `grid` 子物体。
- UI / 表现：仅影响 Level87 本地球框网格 `SpriteRenderer.size.y`，不改变 Canvas、共享 `use_btn` 或成功/失败 UI。
- 事件：不新增事件派发；成功仍走 `BaseLevelCtrl.SuccessStrightly()`，失败仍走 `GameResetEvent(GameResetType.Failed)`。
- 平台：未修改 Android/iOS SDK、ProjectSettings 或构建配置；移动端横屏视觉仍需 PlayMode/真机补验。

### 验证记录
- 静态代码证据：`Level87Ctrl.cs` 中 `ApplySliderOffset(...)` 会调用 `SetGridHeight(scaledOffset)`，`RestoreSliderInitialState()` 会调用 `SetGridHeight(0f)`，`ResolveReferences()` 会按名称查找 `grid`。
- 静态 prefab 证据：`Level_087.prefab` 存在 `m_Name: grid`，其 `SpriteRenderer` 当前 `m_DrawMode: 2` 且初始 `m_Size.y = 0`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已通过，结果为 0 error / 1114 warnings；warning 属于当前工程既有范围。
- `git diff --check -- Assets\Game\Scripts\Level\LevelItem\Level_87\Level87Ctrl.cs` 未报告脚本 whitespace error，仅提示 LF/CRLF 规范化。

### 剩余风险和未验证项
- 未执行 Unity PlayMode，尚未验证 `grid` 的展开方向、锚点、排序层和与 top/slider 的视觉对齐是否符合美术预期。
- 未执行 Inspector 验证，`grid` 仍依赖运行时按名称自动解析；如果后续 prefab 改名或对象层级移出本控制器子级，脚本将无法找到该引用。
- 未验证 Android/iOS 横屏下 top 拖拽、grid 增长和失败重开/退出后的视觉恢复。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中 `grid` 字段 Tooltip、`ResolveReferences()` 自动解析、`ApplySliderOffset(...)` 对 `SetGridHeight(...)` 的调用、`RestoreSliderInitialState()` 对 grid 的归零和 `SetGridHeight(...)` 方法。
- 同步删除本节功能记录、同日风险评估和 `DevelopMD/已有功能设计与使用记录.md` 对应条目。

## 2026-06-23 Level84 successUI 迁移到 Level87

### 需求背景
- 用户要求同步将 `Level84Ctrl` success 相关修改移植到 `Level87Ctrl`，并说明增加了一个 canvas 的控制。
- 本地代码证据显示 `Level84Ctrl.StartSuccessFlow()` 会在成功流程启动后激活 `successUI`，播放 `footballAudienceCelebrate`，等待 `successDelay` 后复用 `BaseLevelCtrl.SuccessStrightly()`。
- Level87 已有自己的 top 解锁和成功碰撞判定，本次只移植成功等待阶段的本地成功 UI/Canvas 显隐控制，不改变 Level87 成功条件。

### 影响文件和系统
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`：新增 `successUI` 序列化字段、成功 UI 显隐 helper、成功流程显示和重置/失败隐藏逻辑。
- `Assets/Game/Prefabs/Level/Level_087.prefab`：当前静态文本未确认存在已命名并绑定的 `successUI` 对象；脚本支持手动绑定，也按 `successUI`、`SuccessUI`、`successCanvas`、`SuccessCanvas` 子物体名自动解析。
- 复用既有系统：Level87 成功碰撞入口、`StartSuccessFlow()`、`successDelay`、`AudioManager.PlaySound("footballAudienceCelebrate")`、`BaseLevelCtrl.SuccessStrightly()`、失败 `GameResetEvent`。
- 未修改共享 `AudioManager`、`GamePanel_UseBtnCommonFeature`、`Level84ActorCtrl`、存档、配置、资源加载、analytics、广告/IAP 或平台 SDK。

### 变更前基线
- Level87 成功流程启动后会隐藏本地选择面板和共享 `use_btn`，停止 football/enemy 表现，播放成功欢呼音效，然后等待 `successDelay` 调用 `SuccessStrightly()`。
- Level87 变更前没有 `successUI` 字段，也不会在成功等待阶段显示独立的成功 UI/Canvas。

### 变更后行为
- `successUI` 是 Level87 本地成功等待阶段 UI/Canvas 引用；字段为空时运行时会尝试按常见子物体名解析。
- `StartSuccessFlow()` 在停止 football/enemy 表现后调用 `SetSuccessUIVisible(true)`，再播放 `footballAudienceCelebrate` 并等待 `successDelay`。
- `ResetPresentationState()` 和 `CompleteFailure()` 会调用 `SetSuccessUIVisible(false)`，避免失败重开、退出、Disable 或失败结算后残留成功 UI。
- `CompleteSuccess()` 仍保持 Level87 既有通关归口，只调用 `BaseLevelCtrl.SuccessStrightly()`；成功 UI 是否在关卡通关切换后继续显示由外部关卡流程接管。
- `SetSuccessUIVisible(...)` 对空引用安全 no-op，因此当前 prefab 未绑定成功 UI 时不会阻断 Level87 成功流程。

### 数据、资源、UI、事件和平台流
- 数据 / 存档 / 配置：无新增 DB、PlayerPrefs、RemoteConfig、Luban 或存档结构访问。
- 资源：无新增 `Resources`、`StreamingAssets`、YooAsset 或运行时实例化；只控制 prefab 中已有或后续手动绑定的本地 UI GameObject。
- UI：新增成功等待阶段的本地 `successUI` 显示；已有选择面板 `Canvas` 和共享 `use_btn` 显隐逻辑不变。
- 事件：成功仍走 `BaseLevelCtrl.SuccessStrightly()`，失败仍走 `GameResetEvent(GameResetType.Failed)`；不新增事件派发。
- 平台：未修改 Android/iOS SDK、ProjectSettings 或构建配置；移动端横屏 UI 显示仍需 PlayMode/真机补验。

### 验证记录
- 静态代码证据：`Level87Ctrl.cs` 已新增 `successUI` 字段；`StartSuccessFlow()` 调用 `SetSuccessUIVisible(true)`；`ResetPresentationState()` 和 `CompleteFailure()` 调用 `SetSuccessUIVisible(false)`；`SetSuccessUIVisible(...)` 对空引用安全处理。
- 静态代码证据：`ResolveReferences()` 会在 `successUI == null` 时按 `successUI`、`SuccessUI`、`successCanvas`、`SuccessCanvas` 自动解析。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已通过，结果为 0 error / 96 warnings；warning 属于当前工程既有范围。

### 剩余风险和未验证项
- 未执行 Unity PlayMode，尚未验证 Level87 prefab 是否已绑定或包含预期成功 UI/Canvas，以及显示时机、排序层和画面位置是否符合策划预期。
- 当前 Level87 prefab 静态文本未确认 `successUI` 明确绑定；如果没有手动绑定且没有匹配命名的子物体，脚本会安全 no-op，但不会显示成功 UI。
- 未验证成功等待期间 retry/exit/disable 是否在玩家视角下隐藏成功 UI；静态代码只能证明重置路径调用隐藏。
- 未验证 Android/iOS 横屏下成功 UI 尺寸、锚点和适配。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中 `successUI` 字段、自动解析、`SetSuccessUIVisible(...)` helper，以及成功/失败/重置路径中的显隐调用。
- 同步删除本节功能记录、同日风险评估和 `DevelopMD/已有功能设计与使用记录.md` 对应条目。

## 2026-06-23 Level87 grid 同步为 grids 数组

### 需求背景
- 用户要求将 Level87 的 `grid` 修改为 `grids`，并同步代码。
- 本地代码证据显示 `Level87Ctrl.cs` 已存在 `SpriteRenderer[] grids` 序列化字段，但 `ResolveReferences()` 和 `SetGridHeight(...)` 仍残留 `grid` 单对象引用，需要同步为数组语义。
- `Level_087.prefab` 静态文本显示当前存在两个网格对象：`m_Name: grid` 和 `m_Name: grid (1)`，因此自动解析需要覆盖同名前缀对象。

### 影响文件和系统
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`：同步 `grids` 字段说明、自动解析和高度应用逻辑。
- `Assets/Game/Prefabs/Level/Level_087.prefab`：仅作为已有 `grid` / `grid (1)` 对象证据，本次未主动改写 prefab 绑定。
- 本次未修改共享 `DragableItem`、`GamePanel_UseBtnCommonFeature`、`Level84ActorCtrl`、存档、配置、资源加载、analytics、广告/IAP 或平台 SDK。

### 行为前后
- 修改前：字段已是 `SpriteRenderer[] grids`，但解析和高度写入仍使用不存在的 `grid` 单对象，脚本无法完成有效的数组同步。
- 修改后：`ResolveReferences()` 调用 `ResolveDefaultGrids()`，当 `grids` 未手动绑定时，会从本关子物体中收集名称为 `grid` 或以 `grid (` 开头的 `SpriteRenderer`。
- `SetGridHeight(...)` 现在遍历 `grids` 数组，统一写入每个网格的 `SpriteRenderer.size.y`，保留各自 `size.x`，高度仍限制不小于 0。
- `ApplySliderOffset(...)` 和 `RestoreSliderInitialState()` 继续通过 `SetGridHeight(...)` 更新/归零网格，因此 top 移动、吸附完成、失败重开、退出和 Disable/Destroy 清理路径仍共用原有入口。

### 数据、资源、UI、事件和平台流
- 数据 / 存档 / 配置：无新增 DB、PlayerPrefs、RemoteConfig、Luban 或存档结构访问。
- 资源：无新增 `Resources`、`StreamingAssets`、YooAsset 或运行时实例化；仅解析 prefab 中已有子物体。
- UI / 表现：只影响 Level87 本地球框网格 `SpriteRenderer.size.y`，不改变 Canvas、共享 `use_btn`、成功 UI 或胜负归口。
- 事件：不新增事件派发；成功仍走 `BaseLevelCtrl.SuccessStrightly()`，失败仍走 `GameResetEvent(GameResetType.Failed)`。
- 平台：未修改 Android/iOS SDK、ProjectSettings 或构建配置。

### 验证记录
- 静态源码扫描确认 `Level87Ctrl.cs` 中已无 `grid` 单变量引用；剩余 `grid` 只作为子物体名称匹配字符串。
- 静态 prefab 证据确认 `Level_087.prefab` 中存在 `grid` 和 `grid (1)` 两个对象。
- 禁用调用扫描未发现本次 grids 同步新增 DB、Save 系统、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、OnValidate、Reset、ExecuteAlways、ExecuteInEditMode、Unity `Debug.Log`、Analytics、IAP 或 Advertisement 调用。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已通过，结果为 0 error / 96 warnings；warnings 属于当前工程既有范围。

### 剩余风险和未验证项
- 未执行 Unity PlayMode：仍需验证 top 拖拽和吸附过程中 `grid`、`grid (1)` 是否同时从 0 展开，并与 top、slider 视觉对齐。
- 未执行 Unity Inspector 检查：仍需确认两个网格对象的 SpriteRenderer、pivot、sorting layer/order 和数组自动解析结果。
- `grids` 自动解析依赖命名规则；如果后续 prefab 改名、移出控制器子级或缺少 `SpriteRenderer`，运行时会安全 no-op，但对应网格不会增长。
- Android/iOS 横屏触摸、快速拖拽、快速重开和 60 秒帧稳定性仍待补验。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中 `grids` Tooltip、`ResolveDefaultGrids()`、`IsGridRenderer(...)` 和 `SetGridHeight(...)` 遍历数组的改动。
- 同步删除本节功能记录、同日风险记录和 `DevelopMD/已有功能设计与使用记录.md` 对应条目。

## 2026-06-23 Level87 blockFailure、固定成功射门方向与普通高射跳过阻挡

### 需求背景
- 用户新增了 `endPoint` 点，要求 top 触发成功路径中 enemy 播放新的 `blockFailure` 动画。
- `blockFailure` 的表现移动需要和原 `PlayEnemyBlock()` 的入场节奏一致，但不能执行 football knock / 反弹逻辑，足球需要继续进入球网。
- topTrigger 触发成功后，football 的 shotDir 需要支持一个可配置固定方向。
- 用户补充要求：正常 `shotFootball` 时，如果点击瞬间 height 超出指定值，则本次射门距离为 `shotForce * 2`，并跳过 `enemyBlock` 动画。

### 影响文件和系统
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`：新增成功路径 `topTriggeredShotDirection` / `endPoint` / `PlayEnemyBlockFailure(...)`，并新增普通射门 height 阈值跳过阻挡逻辑。
- `Assets/Game/Scripts/Level/Actor/Level84ActorCtrl.cs`：新增 `PlayBlockFailure()`，复用既有足球动作播放链路播放 `blockFailure`。
- `Assets/Game/Prefabs/Level/Level_087.prefab`：静态文本显示已有 `m_Name: endPoint`，但当前脚本字段 `endPoint` 仍为 `{fileID: 0}`；脚本运行时会按名称自动解析。
- 本次未修改共享 `DragableItem`、`GamePanel_UseBtnCommonFeature`、存档、配置、资源加载、analytics、广告/IAP 或平台 SDK。

### 变更前基线
- top 后成功候选射门共用点击瞬间冻结的箭头方向，不能单独配置固定方向。
- top 后成功候选射门没有独立的 enemy 守门失败表现；若复用 `PlayEnemyBlock()` 会停止 football tween、切 Dynamic、施加 `blockForce`、播放 knock，并导致足球被挡出，不符合“进球网”要求。
- top 未触发的普通射门总会等待 `shotDuration / 2` 后进入 `PlayEnemyBlock()`，enemy 播放 `football_save`，随后足球被切回 Dynamic 并反弹失败。

### 变更后行为
- `topTriggeredShotDirection` 作为 top 后成功射门的可配置方向；当该值非零时，`PlayForceShot()` 使用其 normalized 结果，值为零时继续使用点击瞬间冻结的箭头方向。
- `endPoint` 作为 enemy `blockFailure` 表现终点；未手动绑定时 `ResolveReferences()` 按子物体名 `endPoint` 自动解析，仍找不到时回退到 football 目标点并输出 Bear warning。
- top 后成功候选射门会调用 `PlayEnemyBlockFailure(...)`：enemy 从目标点右侧入场到 `endPoint`，播放 `Level84ActorCtrl.PlayBlockFailure()`，但不会停止 football、不会切 Dynamic、不会施加 `blockForce`、不会播放 knock，也不会播放 `footballOut` / `trap`。
- `Level84ActorCtrl.PlayBlockFailure()` 只请求 `blockFailure` 动画，不改胜负、存档、事件或物理状态；缺失动画时沿用 `PlaySoccerAction(...)` 的缺失动画 warning / 等待保护。
- 普通射门会冻结点击瞬间的原始 `height` 到 `frozenShotHeight`。当 top 未触发且 height 大于 `normalShotSkipEnemyBlockHeight` 时，本次 `PlayForceShot(...)` 使用 `shotForce * 2` 的移动距离，并跳过 `PlayEnemyBlock()`。
- 普通高射跳过阻挡后仍走失败结算：等待本次 football 飞行时长和 `failureResetDelay` 后调用 `CompleteFailure()`，不会因为球可能碰到成功区而通关，因为成功归口仍要求 `topTriggered && isSuccessShotActive && successAreaEntered`。

### 数据、资源、UI、事件和平台流
- 数据 / 存档 / 配置：无新增 DB、PlayerPrefs、RemoteConfig、Luban 或存档结构访问。
- 资源：无新增 `Resources`、`StreamingAssets`、YooAsset 或运行时实例化；`blockFailure` 动画是否存在依赖 enemy 当前 Spine 动画资源，尚需 PlayMode 验证。
- UI：不改变选择面板、共享 `use_btn`、success UI 或 Canvas 显隐链路。
- 事件：成功仍走 `BaseLevelCtrl.SuccessStrightly()`，失败仍走 `GameResetEvent(GameResetType.Failed)`；不新增事件派发。
- 平台：未修改 Android/iOS SDK、ProjectSettings 或构建配置。

### 验证记录
- 静态源码证据：`Level87Ctrl.PlayUnlockedShot(...)` 会在成功候选射门后调用 `PlayEnemyBlockFailure(...)`，该方法不包含 football `DOKill()`、`AddForce(...)`、knock、`footballOut` 或 `trap` 逻辑。
- 静态源码证据：`Level87Ctrl.GetShotDirection()` 在 `topTriggeredShotDirection` 非零时返回该配置方向；否则返回冻结箭头方向。
- 静态源码证据：`Level87Ctrl.ShouldSkipEnemyBlockForNormalShot()` 只在 `topTriggered == false` 且 height 大于 `normalShotSkipEnemyBlockHeight` 时生效。
- 静态 prefab 证据：`Level_087.prefab` 存在 `m_Name: endPoint`；字段当前未绑定，运行时自动解析是必要兜底。
- 禁用调用扫描未发现本次新增逻辑引入 DB、Save 系统、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、OnValidate、Reset、ExecuteAlways、ExecuteInEditMode、Unity `Debug.Log`、Analytics、IAP 或 Advertisement 调用；命中的 `football_save` / `GetEnemySavePosition` 属于既有足球表现命名，不是存档链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已通过，结果为 0 error / 96 warnings；warnings 属于当前工程既有范围。

### 剩余风险和未验证项
- 未执行 Unity PlayMode：仍需验证 enemy Spine 中实际存在 `blockFailure` 动画，且播放节奏、朝向和入场位置与 `PlayEnemyBlock()` 的表现要求一致。
- 未执行 Unity Inspector 检查：仍需确认 `endPoint` 字段是否需要手动绑定；当前静态 prefab 显示字段为空，依赖运行时按名称解析。
- `topTriggeredShotDirection` 默认值为零，若策划需要固定成功方向，仍需在 Inspector 中配置非零向量。
- `normalShotSkipEnemyBlockHeight` 默认值为 0.25，仍需 PlayMode 调参确认“超出指定值”的实际阈值是否符合箭头 height 设计。
- 未验证普通高射跳过 enemyBlock 后 football 的最终落点、失败等待节奏和重置表现是否满足玩家视角。
- 未验证 Android/iOS 横屏触摸、快速点击、retry/exit/disable 中断和 60 秒帧稳定性。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中 `topTriggeredShotDirection`、`endPoint`、`PlayEnemyBlockFailure(...)`、`PlayEnemyBlockFailureAnimation()`、`GetEnemyBlockFailurePosition()` 和 `GetShotDirection()` 中固定方向分支。
- 回滚 `Level87Ctrl.cs` 中 `normalShotSkipEnemyBlockHeight`、`frozenShotHeight`、`ShouldSkipEnemyBlockForNormalShot()`、`WaitNormalShotFailure(...)` 和 `PlayForceShot(float forceMultiplier)` 的普通高射跳过阻挡逻辑。
- 回滚 `Level84ActorCtrl.cs` 中 `BlockFailureAnimName` 和 `PlayBlockFailure()`。
- 同步删除本节功能记录、同日风险记录和 `DevelopMD/已有功能设计与使用记录.md` 对应条目。

## 2026-06-23 Level87 top 后成功射门改为 AddForce

### 需求背景
- 用户补充要求 topTrigger 触发后 shot force 也需要 `* 2`。
- 用户随后要求 topTrigger 触发后的 shot force 改成 `Rigidbody2D.AddForce`，不再使用 DOTween。
- 本次只调整 top 后成功候选射门；top 未触发的普通射门仍保留 DOTween 距离移动和 height 超阈值跳过阻挡逻辑。

### 影响文件和系统
- `Assets/Game/Scripts/Level/LevelItem/Level_87/Level87Ctrl.cs`：新增 top 后成功射门专用 `PlayTopTriggeredForceShot()`，`PlayUnlockedShot(...)` 改为调用该方法。
- 复用既有 `fbRigidbody`、`SwitchFootballState(true)`、`GetShotDirection()`、`topTriggeredShotDirection`、成功碰撞等待和 `PlayEnemyBlockFailure(...)`。
- 不修改 `Level84ActorCtrl`、prefab、存档、配置、资源加载、UI、analytics、广告/IAP 或平台 SDK。

### 变更前基线
- top 后成功候选射门此前调用 `PlayForceShot()`，按 DOTween `DOMove(...)` 移动 football，移动距离为 `shotForce`。
- `PlayEnemyBlockFailure(...)` 不撞球，只做 enemy 守门失败表现，足球是否进网依赖 `PlayForceShot()` 的 DOTween 轨迹。

### 变更后行为
- `PlayUnlockedShot(...)` 现在调用 `PlayTopTriggeredForceShot()`。
- `PlayTopTriggeredForceShot()` 使用 `GetShotDirection()`，因此仍支持 `topTriggeredShotDirection` 固定方向；未配置时继续使用点击瞬间冻结方向。
- top 后成功候选射门会停止 football 旧 tween，切换 football 到 Dynamic，并执行 `fbRigidbody.AddForce(forceDirection * shotForce * 2, ForceMode2D.Impulse)`。
- `footballTargetPosition` 仍按方向和 `shotForce * 2` 计算，仅作为 enemy `endPoint` 缺失时的 fallback 参考；实际 football 移动交给物理冲量。
- `fbRigidbody` 缺失时输出 Bear warning 并跳过 AddForce；成功等待仍需 success collider 标记，避免无刚体时直接误通关。

### 数据、资源、UI、事件和平台流
- 数据 / 存档 / 配置：无新增 DB、PlayerPrefs、RemoteConfig、Luban 或存档结构访问。
- 资源：无新增 `Resources`、`StreamingAssets`、YooAsset 或运行时实例化。
- UI：不改变 Canvas、共享 `use_btn`、success UI 或选择面板显隐。
- 事件：成功仍由 success collider 标记后走 `BaseLevelCtrl.SuccessStrightly()`；失败仍由既有失败归口处理。
- 平台：未修改 Android/iOS SDK、ProjectSettings 或构建配置。

### 验证记录
- 静态源码证据：`PlayUnlockedShot(...)` 调用 `PlayTopTriggeredForceShot()`，普通 `PlayBlockedShot(...)` 仍调用 `PlayForceShot(...)`。
- 静态源码证据：`PlayTopTriggeredForceShot()` 调用 `SwitchFootballState(true)` 和 `fbRigidbody.AddForce(..., ForceMode2D.Impulse)`，力度为 `Mathf.Max(0f, shotForce) * 2`。
- 静态源码证据：`StartSuccessFlow()`、`CompleteFailure()` 和 `ResetPresentationState()` 仍会停止 football tween 并调用 `SwitchFootballState(false)`，用于收回 Dynamic 状态。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已通过，结果为 0 error / 96 warnings；warnings 属于当前工程既有范围。

### 剩余风险和未验证项
- 未执行 Unity PlayMode：仍需验证 top 后 AddForce 的力度、轨迹、重力影响、碰撞触发和入网表现是否符合预期。
- Level87 当前 `SwitchFootballState()` 只切 Rigidbody2D bodyType 和清速度，不像 Level84 那样缓存/恢复 gravityScale；如果 prefab gravityScale 不符合成功射门预期，需在 Inspector 或后续脚本中调整。
- `footballTargetPosition` 对 AddForce 只是 fallback 参考，不再代表真实终点；`endPoint` 应优先绑定或按名称解析成功。
- 未验证 Android/iOS 横屏设备上的物理表现一致性和 60 秒帧稳定性。

### 回滚路径
- 回滚 `Level87Ctrl.cs` 中 `TopTriggeredShotForceMultiplier`、`PlayTopTriggeredForceShot()`，并将 `PlayUnlockedShot(...)` 恢复为调用 `PlayForceShot()`。
- 同步删除本节功能记录、同日风险记录和 `DevelopMD/已有功能设计与使用记录.md` 对应条目。
