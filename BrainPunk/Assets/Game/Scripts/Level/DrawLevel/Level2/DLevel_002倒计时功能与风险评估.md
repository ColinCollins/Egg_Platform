# DLevel_002 功能与风险评估

## 1. 需求与原行为基线

- 需求：在 `DLevel_002` 的 `DrawLevelCtrl` 同级挂载独立倒计时组件，倒计时默认
  20 秒并允许在 Inspector 配置；第一笔有效笔画完成后开始倒计时。
- 剩余时间进入最后 5 秒时，通过 Bear Event 通知
  `DrawGame_GamePlayPanel` 显示 `cd_txt`；倒计时结束默认按失败处理并重置关卡，
  仅在 `isSuccess=true` 时提交关卡成功。
- 原行为没有关卡倒计时组件和倒计时事件；HUD prefab 中的 `cd_txt` 是本次开始前已由
  用户补充的节点，但业务脚本没有持有或刷新该引用。
- `DLevel_002` 当前已有的关卡对象、`DLevel001Ctrl`、`DelayAutoBeginDrawHandle` 及用户新增的
  Level2 内容不属于本需求，本次不推断、不修改其玩法职责。

## 2. 修改后行为

- `DrawLevelCountdownCtrl` 实现 `IDrawStrokeCompletedHandler`，因此由
  `DrawLevelCtrl.InitializeRuntime()` 自动发现；第一笔有效笔画完成回调只启动一次倒计时。
- `cdTime` 默认值为 20 秒，允许在 Inspector 配置。关卡暂停时不扣减时间。
- 剩余时间大于 5 秒时 HUD 保持隐藏；进入 5、4、3、2、1 秒时仅在整数秒发生变化时发送
  `DrawGameCountdownChangedEvent`，避免逐帧派发全局事件。
- 时间耗尽时先停止并隐藏倒计时，再调用
  `DrawLevelCtrl` 的一次性结算入口。`isSuccess=false` 时调用
  `TryFailAndReset(...)`，复用 `TryResolve(Failed)` 的结果锁，并只在首次失败结算成功时
  派发既有 `DrawGameResetEvent(Failed)`；`isSuccess=true` 时调用
  `TryResolve(Success)`，由 `DrawPlayCtrl.OnLevelResolved(...)` 继续原有胜利面板、星级、
  进度和插屏链。
- 成功、其他失败、主动重置、组件禁用或面板重新打开时都会关闭倒计时显示，避免残留数字。

## 3. 事件与资源流

```text
首笔 OnStrokeCompleted
  -> DrawLevelCountdownCtrl 启动
  -> 最后 5 秒整数变化
     -> DrawGameCountdownChangedEvent(visible, seconds)
     -> DrawGame_GamePlayPanel.cd_txt
  -> 时间耗尽
     -> isSuccess=false
        -> DrawLevelCtrl.TryFailAndReset
        -> DrawGameResetEvent(Failed)
        -> DrawPlayCtrl 原有重载流程
     -> isSuccess=true
        -> DrawLevelCtrl.TryResolve(Success)
        -> DrawPlayCtrl.OnLevelResolved
        -> 原有 Draw 胜利结算流程
```

- 新事件只传递显示状态和剩余整数秒，不携带关卡玩法状态。
- 本次未新增或修改存档、Luban 配置、资源地址、analytics、平台 SDK 或全局管理器初始化。
- HUD 继续使用现有 `DrawGame_GamePlayPanel.prefab`；`cd_txt` 默认关闭并由序列化引用绑定。
- Android/iOS 共用 `Time.deltaTime` 与现有关卡暂停状态，没有新增平台分支。

## 4. 影响文件

- `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCountdownCtrl.cs`
- `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl.cs`
- `Assets/Game/Scripts/Level/PlayEvents.cs`
- `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_GamePlayPanel.cs`
- `Assets/Game/Prefabs/DrawLevel/DLevel_002.prefab`
- `Assets/Game/UI/DrawGame/GamePlayPanel/Resources/DrawGame_GamePlayPanel.prefab`

## 5. 验证证据

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` 首次完整构建通过：
  `0 error / 1120 warning`；warning 为工程既有警告。最终单进程增量复核同样通过：
  `0 error / 0 warning`。
- 静态确认新脚本 GUID 与 `DLevel_002.prefab` 组件引用一致，组件的
  `drawLevelCtrl` 指向同一 prefab 根节点上的 `DrawLevelCtrl`，`cdTime` 为 20。
- 静态确认 HUD 主脚本的 `countdownText` 指向用户新增的 `cd_txt` TMP 组件，节点初始关闭。
- 静态确认两个目标 prefab 的 YAML fileID 均无重复。
- CodeGraph 已识别 `DrawLevelCountdownCtrl -> IDrawStrokeCompletedHandler` 回调关系及
  `DrawLevelCtrl.TryFailAndReset` 的失败重置入口。

## 6. 风险、未验证项与回滚

- 尚未在 Unity PlayMode 验证首笔启动时点、5 秒显示边界、暂停恢复、成功抢先结算、
  超时重载、多次进入关卡，以及 Android/iOS 横屏布局；这些项目保持为未验证风险。
- `DLevel_002` 现有的 `DLevel001Ctrl` 与 Level2 新增内容之间的最终职责属于
  `uncertain`；本次未据此推断第二关玩法，也未调整其绑定。
- 两个 prefab 的整体 `git diff --check` 仍会报告用户本次已有 YAML 行尾空格；本次新增的
  倒计时组件块和脚本未引入新的行尾空格，未对用户已有 prefab 改动做格式化重写。
- 回滚时可移除 `DLevel_002` 上的 `DrawLevelCountdownCtrl` 组件和脚本，移除
  `DrawGameCountdownChangedEvent`、HUD 的倒计时引用/订阅，以及
  `DrawLevelCtrl.TryFailAndReset`；恢复旧的私有失败派发逻辑即可，不涉及存档迁移。

## 7. 2026-07-28 Alien 入场与黄球释放

### 7.1 需求与修改前基线

- 用户明确要求：Alien 从场外水平移动到 `endPoint.position.x` 后才开放画线；移动过程中
  `yellow_ball` 保持相对 Alien 的位置不动。
- 第一笔有效画线完成后，Alien 子节点 `catch` 的 Animator 打开，并把
  `yellow_ball` 的 `Rigidbody2D` 切换为 Dynamic。
- 用户后续明确要求 Alien 的 `ActorSpineCtrl` 默认播放 `idle01`，第一笔画线完成后播放
  控制动画，播放结束再回到 `idle01`；入场改用 `DOMoveX`，并向 Inspector 暴露 delay
  与 duration。
- 修改前 `DLevel2Ctrl` 只是 Unity 生成的空 `Start/Update` 骨架。Prefab 已存在 Alien、
  catch、yellow_ball，以及 Alien 同级唯一一个带定位图标的空 Transform；该定位对象原名
  `GameObject`，本次按用户定义的用途改名为 `endPoint`。
- 修改前根节点还挂有固定等待 1 秒的 `DelayAutoBeginDrawHandle`。本次由
  `DLevel2Ctrl` 接管明确的开场门禁后，移除该冗余组件，避免额外等待改变 Alien 入场节奏。

### 7.2 修改后行为

- `DLevel2Ctrl` 实现 `IDrawLevelIntroHandler`。`DrawPlayCtrl` 保持 Intro 阶段并等待
  Alien 到达目标 X，返回后才通过既有 `BeginDrawing()` 开放输入；未新增全局事件。
- Alien 使用 DOTween `DOMoveX` 只修改世界坐标 X，Y/Z 保持 prefab 初始值。Inspector
  暴露 `entranceDelay` 与 `entranceDuration`，当前 prefab 默认分别为 0 秒和 1.2 秒；
  delay 与 tween 完成前均保持画线输入关闭。
- yellow_ball 在开场和绘制完成前保持 Kinematic，线速度/角速度归零，并持续保持初始
  localPosition/localRotation；`DOMoveX` 更新期间继续校正相对位置，因此随 Alien
  整体入场但不发生相对位移。
- 第一笔有效画线完成后，`catchAnimator.SetBool("isOpen", true)`，随后将
  yellow_ball 切换为 `RigidbodyType2D.Dynamic` 并唤醒刚体。`catch.controller` 已静态确认
  参数实际名称为 `isOpen`。
- Alien 的 `ActorSpineCtrl.defaultAnimName` 与底层 `SkeletonAnimation._animationName`
  均设为 `idle01` 且循环。第一笔完成时播放一次 `control`，TrackEntry 完成回调再恢复
  循环 `idle01`。资源中不存在名为 `controller` 的动画，实际可用动画名是 `control`；
  重置或禁用时通过版本号使旧动画完成回调失效。
- `OnDrawAttemptReset()` 会恢复 Alien 场外初始位置、catch 关闭状态、黄球 Kinematic
  状态、初始相对位置与 `idle01`，防止同实例重置残留。
- 倒计时通过既有 `DrawLevelCtrl.Resolved` 订阅停止：首次成功结果提交后自动停止并隐藏，
  不会在已经获胜后继续扣时或触发超时失败；失败结算也沿用相同的停止清理。

### 7.3 影响范围与系统流

- `Assets/Game/Scripts/Level/DrawLevel/Level2/DLevel2Ctrl.cs`
- `Assets/Game/Prefabs/DrawLevel/DLevel_002.prefab`
- `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCountdownCtrl.cs` 未新增代码；本次只复核其
  获胜停止链。
- 未修改 Draw 共享输入实现、存档、Luban 配置、资源地址、analytics、SDK 或平台分支。
- 运行时每帧工作只存在于短暂的 Alien `DOMoveX` 更新回调；到达后 tween 完成，不产生
  持续轮询。

### 7.4 验证证据

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly
  /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：
  `0 error / 102 warning`；warning 为当前工程构建输出，未阻断本次改动。
- CodeGraph 已识别 `DLevel2Ctrl` 同时实现 `IDrawLevelIntroHandler` 和
  `IDrawStrokeCompletedHandler`，入场返回前由既有 Draw intro 链保持输入门禁。
- 静态确认 DLevel2Ctrl、DrawLevelCtrl、Alien、endPoint、catch Animator、
  yellow_ball Rigidbody2D 与 Alien ActorSpineCtrl 的 prefab 序列化引用均存在。
- 静态确认 `DelayAutoBeginDrawHandle` 的组件 fileID 已从 DLevel_002 移除，
  当前 prefab YAML 共 144 个对象 fileID 且无重复。
- 静态确认 `Assets/Game/ArtSrc/Animations/Level70/catch.controller` 包含 bool 参数
  `isOpen`；yellow_ball 在 prefab 中的初始 `m_BodyType` 为 Kinematic。
- 静态解析 `AlienSpaceship.json`，确认动画集合为 `control`、`idle`、`idle01`；
  prefab 中 ActorSpineCtrl 默认动画、循环标记、SkeletonAnimation 初始动画和
  DLevel2Ctrl 的 Spine 引用均已绑定。

### 7.5 风险、未验证项与回滚

- 尚未执行 Unity PlayMode，因此 Alien 入场 delay/duration 手感、暂停/恢复、到达 X 后
  首帧开放输入、黄球相对位置、`control -> idle01` 动画衔接、catch 动画切换、
  Dynamic 下落、关卡失败/重开、获胜后倒计时停止，以及 Android/iOS 横屏表现仍待验证。
- 当前 `entranceDelay=0`、`entranceDuration=1.2` 是可调初值；若实际演出节奏不符合策划
  预期，应只调整 DLevel_002 prefab 字段，不修改共享 Draw 流程。
- endPoint 的绑定依据是 prefab 中 Alien 同级唯一的空定位 Transform；已改成明确名称并
  记录绑定。若该定位对象实际另有用途，应在 prefab 中重新指定 endPoint 后复验。
- yellow_ball 释放时会先以保持世界姿态的方式脱离 Alien 层级；当前共享根缩放为 `0.9`，
  释放后球保留缩小后的世界尺寸并再切 Dynamic。若后续新增 Alien 二次移动或缩放，
  需先验证释放父子关系与刚体缩放。
- 回滚时恢复空的 DLevel2Ctrl 或移除其组件，恢复 `DelayAutoBeginDrawHandle` 的根节点
  组件与 1 秒配置，把 `endPoint` 名称恢复为 `GameObject`，并清除 Alien
  ActorSpineCtrl 的默认动画覆盖；不涉及存档迁移。

## 8. 2026-07-28 可见入场与黑屏时序修复

### 8.1 问题基线

- `EnterLevelLoading.OnPageTurnCompleteAsync()` 会等待 `DrawPlayCtrl.OpenDrawLevelAsync()`
  返回后才开始擦除转场遮罩。
- 修改前 `OpenDrawLevelAsync()` 无条件等待 `DrawLevelCtrl.PlayIntroAsync()`；因此
  DLevel_002 的 Alien `entranceDelay + entranceDuration` 全部发生在遮罩下，玩家看到的
  黑屏时间被延长，且看不到 Alien 入场演出。
- DLevel_001 当前使用普通 `DelayAutoBeginDrawHandle`。为保护既有关卡，本次不能把所有
  Draw Intro 都统一移动到遮罩关闭后。

### 8.2 修改后流程

- 新增局部标记 `IDrawLevelVisibleIntroHandler`；只有实现该标记的关卡要求在转场完全揭开后
  再执行 Intro。`DLevel2Ctrl` 使用该标记，DLevel_001 的普通 Intro 时序保持不变。
- `DrawLevelCtrl` 在扫描关卡本地 Intro 时生成只读运行时状态
  `PlaysIntroAfterTransition`，不新增全局事件、存档或配置依赖。
- 初次进入 DLevel_002 时，`OpenDrawLevelAsync()` 完成资源实例化、HUD 创建和运行时初始化
  后立即返回，使 `EnterLevelLoading` 开始擦除。遮罩关闭回调再播放 Alien 入场，完成前
  `DrawLineFeature` 输入保持关闭，到达 endPoint 后才执行 `BeginDrawing()`。
- 2026-08-02 起，失败、手动重载和胜利 Replay 都不再播放可见 Intro。DLevel_002 在
  GameplayPanel 全黑遮罩下直接应用 FINAL 夹持态，再沿现有黑幕淡入门禁开放输入；首次进关
  和进入下一关仍在转场完全揭开后播放原 Intro。

### 8.3 影响文件与验证

- `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
- `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl.cs`
- `Assets/Game/Scripts/Level/DrawLevel/DrawLevelRuntimeTypes.cs`
- `Assets/Game/Scripts/Level/DrawLevel/Level2/DLevel2Ctrl.cs`
- C# 单进程构建通过：`0 error / 102 warning`。
- 静态调用链确认：`EnterLevelLoading` 等待加载回调返回后开始擦除；DLevel_002 的加载回调
  现在不再等待 Intro，擦除完成后才启动 Intro；`DrawLineFeature` 的
  `_isInputEnabled` 初始值为 false，只有 `CompleteIntroAndBeginDrawing()` 才开启。
- 静态确认 DLevel_001 仅绑定普通 `DelayAutoBeginDrawHandle`，不会命中可见 Intro 分支。

### 8.4 风险与回滚

- 尚未执行 Unity PlayMode，转场擦除结束到 Alien 开始移动之间的首帧视觉衔接、重载淡入、
  快速退出关卡时的异步取消，以及 Android/iOS 横屏表现仍待验证。
- 若需回滚，移除 `IDrawLevelVisibleIntroHandler` 与 `PlaysIntroAfterTransition` 分支，
  将 `OpenDrawLevelAsync()` 恢复为无条件等待 `PlayIntroAsync()`；该回滚会恢复原来的
  遮罩下播放 Alien Intro 行为，不涉及资源、配置或存档迁移。

## 9. 2026-07-28 Replay 透明点击遮挡修复

### 9.1 问题基线与根因

- Draw 成功演出开始时，`DrawLevelCtrl.OnTriggerSuccess()` 通过既有
  `GamePlayPanelSwitchBlockEvent(true)` 打开 GameplayPanel 的全屏 `ClickBlock`。
- `ClickBlock` 是铺满 Canvas 的透明 Image，`RaycastTarget` 为 true；它不仅拦截 HUD
  按钮，`DrawLineFeature.IsPointerOverUi()` 也会因此拒绝开始画线。
- 主线 Replay 会重新创建 GameplayPanel，新的 Panel 在 `OnOpen()` 中关闭 ClickBlock；
  Draw Replay 为保持 HUD 与 BlackMask 动画而复用同一 Panel，因此不能依赖 `OnOpen()`。
- 修改前 Draw 成功路径只在成功提交失败或成功演出仍进行时销毁关卡的异常分支关闭
  ClickBlock；正常成功后执行 Replay 时，`ResetForNewAttempt()` 和 BlackMask 淡出均未
  释放 ClickBlock，造成画线与 GameplayPanel 点击同时失效。

### 9.2 修改后不变量

- `DrawPlayCtrl.BeginDrawing()` 现在同时承担三项原子状态切换：
  进入 `DrawPlayPhase.Drawing`、按暂停状态恢复画线输入、派发
  `GamePlayPanelSwitchBlockEvent(false)`。
- 初次进入、普通 Intro、可见 Intro、失败重开和胜利 Replay 最终都通过
  `BeginDrawing()` 开放输入，因此未来新增新尝试入口只要遵守既有统一出口，就不会遗留
  透明 ClickBlock。
- ClickBlock 在成功演出和重建 Intro 期间仍保持锁定；只有 Intro 完成、正式允许玩家操作
  时才释放，没有提前开放 HUD 或画线。
- 未新增事件、UI 组件、Prefab、存档、配置、广告或平台分支；继续复用 Bear EventSystem
  中已有的 `GamePlayPanelSwitchBlockEvent`。

### 9.3 验证、风险与回滚

- CodeGraph 静态确认当前 `BeginDrawing()` 只有统一的
  `CompleteIntroAndBeginDrawing()` 调用出口；初次进入、可见 Intro 和重载完成均汇入该出口。
- 静态确认成功锁定使用 `GamePlayPanelSwitchBlockEvent(true)`，Drawing 开放使用同事件
  `false`，形成明确的成对状态。
- 静态确认 Draw GameplayPanel 的 ClickBlock 为全屏 Image 且 `RaycastTarget=true`，
  `DrawLineFeature.TryBeginStroke()` 会调用 `IsPointerOverUi()`。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly
  /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：
  `0 error / 102 warning`。
- 尚未执行 Unity PlayMode；胜利 Replay 后 HUD 点击、画线、连续多次 Replay、失败重开、
  Intro 期间锁定、快速退出，以及 Android/iOS 触控仍待人工验证。
- 回滚时移除 `BeginDrawing()` 中的
  `GamePlayPanelSwitchBlockEvent(false)` 派发即可；回滚会恢复 Replay 后透明 ClickBlock
  残留问题，不涉及数据迁移。

## 10. 2026-07-28 倒计时成功结算开关

### 10.1 需求、基线与影响范围

- 用户要求为倒计时增加 `isSuccess` 判断，默认值为 `false`；倒计时结束且开关开启时，
  发送对应的关卡成功结果。
- 修改前 `DrawLevelCountdownCtrl.CompleteCountdown()` 固定调用
  `DrawLevelCtrl.TryFailAndReset(...)`，所有使用该共享组件的 DLevel 都只能在超时后失败。
- 本次只修改
  `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCountdownCtrl.cs` 及本说明；
  未修改 DLevel prefab、Draw HUD、DB、Luban、资源地址、analytics、SDK 或平台分支。
- 静态扫描确认当前 `DLevel_002.prefab` 和 `DLevel_004.prefab` 使用该组件。两者未写入新增
  字段时由 Unity 使用 bool 默认值 `false`，继续保持原超时失败行为。

### 10.2 修改后行为与事件流

- 新增私有序列化字段 `isSuccess=false`。只有需要“坚持到倒计时结束即胜利”的关卡才应在
  Inspector 显式开启；不开启时行为与修改前一致。
- 倒计时结束先停止计时并隐藏 HUD。`isSuccess=true` 时提交
  `DrawLevelResult(Success, nameof(DrawLevelCountdownCtrl))`；
  `isSuccess=false` 时继续调用 `TryFailAndReset(...)`。
- 成功分支不新增全局 `PlayEvents`，而是复用 `DrawLevelCtrl.TryResolve(...)` 的一次性结果锁。
  结果通过既有 `Resolved` 回调到达 `DrawPlayCtrl.OnLevelResolved(...)`，再执行 Draw 原有
  胜利插屏、星级记录、进度记录与胜利面板流程。
- 若关卡已被门、陷阱或其他机制先行结算，`TryResolve(...)` 返回 false，不会重复发送胜利
  或覆盖首次结果。

### 10.3 验证、剩余风险与回滚

- 已通过 CodeGraph 核对 `CompleteCountdown -> TryResolve(Success) -> Resolved ->
  DrawPlayCtrl.OnLevelResolved -> ShowVictoryPanel` 既有调用链，以及失败分支继续使用
  `TryFailAndReset -> DrawGameResetEvent(Failed)`。
- 已静态确认 `isSuccess` 是默认 false 的私有序列化字段，未新增公共 API、编辑器生命周期、
  DB/Save 访问或逐帧事件派发。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly
  /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 增量复核通过：
  `0 error / 0 warning`。
- 待执行 Unity PlayMode 的默认失败/开启成功/其他机制抢先结算/暂停恢复验证，以及
  Android/iOS 横屏设备验证；完成前这些项目均为未验证风险。
- 回滚时删除 `isSuccess` 字段和 `CompleteCountdown()` 的成功分支，恢复固定调用
  `TryFailAndReset(...)`；不涉及存档、配置或资源迁移。

## 11. 2026-07-31 外星猫与架子 Spine 替换

### 11.1 需求与修改前基线

- 用户提供新版外星猫 Spine 与独立架子 Spine，要求替换 DLevel_002 现有表现：
  架子挂到外星猫 `bone2`；进场时猫循环 `idle01`、架子夹住球并共同飞入；首笔有效线
  完成后猫播放按按钮动画、架子切换放开姿势，并真正释放球进入 2D 物理模拟。
- 整体进场继续使用回弹，而不是匀速一步到位；架子额外增加一次可见甩动。画线完成事件、
  倒计时、成功门、FallArea、星级及其他 Draw 通用流程保持不变。
- 修改前 DLevel_002 共用 `sp_AlienSpaceship_001`，架子使用 SpriteRenderer、Animator 和
  `catch.controller` 的两个静态姿势；球始终是 Alien 直接子物体，首笔后只切换
  `Rigidbody2D.bodyType`，没有脱离动画父级。
- 新猫和旧猫文件基名及 PNG 相同，但 JSON 不同；旧 SkeletonData 还被 DLevel_006、
  DLevel_010 和 Level_065 使用，因此本次独立导入 `_003`，未覆盖共享 `_001`。

### 11.2 新资源与 Prefab 层级

- 新猫资源：
  `Assets/Game/ArtSrc/Spines/sp_AlienSpaceship_003/`。Spine 版本为 `4.2.36`，
  动画为 `idle01`、`idle`、`control`；`control` 时长约 `0.6s`，`bone2` 存在。
- 新架子资源：
  `Assets/Game/ArtSrc/Spines/sp_clip_002/`。动画为 `clip_close`、`clip_open`。
  两个动画都只有 0 秒姿势关键帧，无动画事件和内置甩动。
- DLevel_002 的最终层级为：

```text
Alien (新版猫 SkeletonAnimation)
└── catch (BoneFollower -> bone2)
    └── clipSwingPivot
        ├── clipVisual (架子 SkeletonAnimation)
        └── yellow_ball (夹持期 Kinematic)
```

- 旧 catch Animator 已从 Prefab 移除，旧 Sprite 子物体保留为 inactive 便于对照；
  其 hook/block/catchArea Collider2D 已于 2026-08-02 删除，新视觉由 `clipVisual` 完整接管。
- `BoneFollower` 只负责骨骼位置、旋转和翻转；甩动作用在其子节点
  `clipSwingPivot`，避免 Spine LateUpdate 覆盖 DOTween 旋转。

### 11.3 修改后运行流程

```text
可见 Intro 开始
  -> 猫播放 idle01(loop)
  -> 架子播放 clip_close
  -> Alien.DOMoveX + Ease.OutBack
  -> clipSwingPivot 保持向后预倾
  -> 制动阶段向前越过中线
  -> 左右多次衰减并回正
  -> Intro 全部完成后开放画线

第一笔有效线完成
  -> 架子播放 clip_open
  -> 球保持世界位姿脱离 clipSwingPivot
  -> 球切换 Dynamic 并 WakeUp
  -> 猫播放 control(non-loop)
  -> control 完成后恢复 idle01(loop)
  -> 原有 15 秒倒计时与胜负链继续
```

- 球在夹持阶段为 Kinematic，并由架子层级保持局部位置、旋转和缩放；进入 Intro 时跟随
  `bone2` 的 idle 浮动、Alien 整体位移和架子甩动。
- 释放时先清零速度并执行 `SetParent(ballReleaseParent, true)`，保持世界位置脱离动画层级，
  随后才改为 Dynamic。这样 Rigidbody2D 不再被 `bone2` 或甩动父节点持续干扰。
- Reset 时先恢复 Kinematic、清零速度，再挂回初始夹持父节点并恢复局部姿态；
  `clip_close` 和猫 `idle01` 同时恢复。
- 进场参数当前为 `entranceDuration=0.78`、`Ease.OutBack(1.9)`；架子先保持
  `+38°` 后摆，再按 `-32° → +15° → -7° → +3° → 0°` 连续衰减，约 `0.85s`
  收稳。完整参数与调整依据见第 11.8 节。
  架子资源自身没有时间动画，开合观感依赖 Spine 默认 Mix；
  实际放球仍与首笔完成回调同步，保持旧程序触发时序。

### 11.4 影响文件、系统边界与验证

- 运行脚本：
  `Assets/Game/Scripts/Level/DrawLevel/Level2/DLevel2Ctrl.cs`
- 关卡 Prefab：
  `Assets/Game/Prefabs/DrawLevel/DLevel_002.prefab`
- 新资源：
  `Assets/Game/ArtSrc/Spines/sp_AlienSpaceship_003/`、
  `Assets/Game/ArtSrc/Spines/sp_clip_002/`
- 未修改 `DrawLineFeature`、`DrawLevelCountdownCtrl`、`DrawLevelCtrl`、成功门、
  FallArea、Luban、存档、analytics、SDK 或平台分支。Prefab 中用户已有的多笔、墨水、
  线宽、质量及 Actor override 也保持原值。
- Unity 6.0.59f2 已成功导入两套 Spine，生成 SkeletonData、Atlas 和材质；编辑器完成
  `Assembly-CSharp.dll` 编译，并执行 Prefab 迁移。迁移工具随后已从工程移除。
- 静态结构断言全部通过：两个 SkeletonData GUID、`bone2`、两个架子动画、两个猫动画、
  `clipSwingPivot`、`clipVisual`、球先脱离后 Dynamic、Reset 重新挂回、OutBack 和
  显式惯性衰减摆均存在；旧 catch controller GUID 已不在 DLevel_002 中。
- 本机没有可用 .NET SDK，因此 `dotnet build` 未执行；编译证据来自正在运行的 Unity
  编辑器程序集时间与无新增 DLevel2 C# 错误的 Editor.log。

### 11.5 剩余风险与回滚

- `uncertain`：尚未完成 Unity PlayMode 的连续动态与物理实测，尤其是 `bone2` 翻转后的
  架子朝向、球在夹口中的视觉位置、第 11.7 节新回弹参数、首笔瞬间的开夹 Mix，以及
  移动端画面。
- 架子来料只有两个零时长姿势；如果产品需要有明确起止时间的机械开夹动画，需要美术补充
  Spine 时间关键帧或后续增加代码控制的开夹过渡。本次没有凭空延迟实际放球。
- 既有“成功门演出尚未提交成功时倒计时仍可能归零”的竞态未在本次修改范围内，行为保持
  与修改前一致。
- 回滚时将 DLevel_002 Alien SkeletonData 恢复为 `_001`，移除 `BoneFollower`、
  `clipSwingPivot` 和 `clipVisual`，重新启用旧 catch 子物体与 Animator，并恢复旧版
  `DLevel2Ctrl` 的 `isOpen`/直接 Dynamic 逻辑；确认无其他引用后可删除两个新 Spine
  目录。不涉及存档、配置或数据迁移。

### 11.6 2026-07-31 Spine 显示异常修复

- 问题基线：首次导入后的关卡截图中，Alien/飞船明显发白并带紫色透明边，局部附件和夹子
  还有拉伸感。新 `_003` 与正常工作的 `_001` 使用完全相同的 PNG，SkeletonData
  `scale=0.01`、骨骼尺寸和 `bone2` 数据也一致，因此问题不来自美术内容或挂点。
- 根因是两套新 PNG 被按普通线性纹理导入：`sRGBTexture=0`、Default Texture、
  `alphaIsTransparency=0`、NPOT ToNearest、Wrap Repeat；同时新 Spine 材质未启用
  `_STRAIGHT_ALPHA_INPUT`。该组合与项目的 `Spine/Skeleton`
  `Blend One OneMinusSrcAlpha` 不匹配，透明像素的 RGB 被直接加亮，并且非 2 次幂图集
  被缩放，最终产生发白、紫边和局部 UV 拉伸。
- Alien 与 clip 的 PNG 导入现已统一为项目正常 Spine 基线：
  `sRGBTexture=1`、Sprite、`alphaIsTransparency=1`、NPOT None、Wrap Clamp；
  两个材质均启用 `_STRAIGHT_ALPHA_INPUT`，并设置 `_StraightAlphaInput=1`。
- 未改 Alien、`BoneFollower`、`clipSwingPivot` 或 `yellow_ball` 的缩放和偏移。
  与 DLevel_006、DLevel_010、Level_065 对比后，Alien 的 `(1,1,1)` 比例、Sorting
  Layer/Order 与翻转设置一致；球当前看似特殊的负 Y 缩放是抵消骨骼翻转、保持原世界姿态
  所必需，继续保留。
- Unity 6.0.59f2 已重新导入两张 PNG、两个材质与 Atlas，并完成编辑器程序集重编译。
  另通过临时实例化 DLevel_002、固定 `idle01 + clip_close` 的独立相机渲染确认：
  猫、飞船和夹子已恢复正常饱和度及透明边缘，不再出现截图中的紫边、发白或拉伸；
  临时验证脚本随后已删除，未留在工程中。
- `uncertain`：本次独立渲染验证覆盖静态合成画面，但尚未重新完整跑完移动端 PlayMode 的
  进场回弹、夹子甩动、首笔开夹和球下落全过程。若仅回滚本小节，应恢复两个 PNG meta
  和两个材质的旧值；这会重新引入当前显示问题，不影响第 11.1～11.5 节的动画与物理逻辑。

### 11.7 2026-07-31 clip 003 与入场节奏调整

- 用户提供 `003/003` 版架子 Spine，并要求架子初始倾斜和到位回弹更明显，同时缩短
  猫与架子整体飞入的拖沓感。调整前 Alien 从 `X=-16.72` 移到 `X=-4.58`，路程
  `12.14`，持续 `1.2s`；架子从正位立即执行 `18° / 0.85s` Punch，甩动主要发生在
  飞行前段，无法形成“先明显倾斜、到位再回弹”的节奏。
- 新来料的 PNG、Atlas、骨骼、Slot、Mesh、Bounds 与现有 `sp_clip_002` 完全相同，
  `clip_open` 也未改变。仅 `clip_close` 四根骨骼的静态角度更新：
  `bone57 -9.87→-8.26`、`bone55 -2.88→-1.27`、`bone54 10.97→8.60`、
  `bone50 2.96→0.59`。因此只原位覆盖
  `Assets/Game/ArtSrc/Spines/sp_clip_002/clip.json`，保留原 `.meta`、SkeletonData
  GUID、Atlas、PNG 和已修复的 Straight Alpha/纹理导入设置。
- 新入场参数为：
  - Alien 起始 `X=-15.4`，终点继续为 `X=-4.58`，路程缩短为 `10.82`；
  - 移动持续 `0.78s`，使用 `Ease.OutBack`、overshoot `1.9`；
  - `clipSwingPivot` 开始时相对静止姿态预倾 `+38°`，用 `0.46s / Ease.OutCubic`
    回正；
  - 在移动进度 `70%`（约 `0.546s`）开始 `32° / 0.42s` Punch，
    vibrato `4`、elasticity `0.70`，整段 Intro 约在 `0.966s` 收稳。
- 起点没有直接缩到相机左边界：DLevel_002 相机 `orthographicSize=5.9`，
  设计分辨率宽高比约 `2340/1080`，左边界约 `X=-12.78`；考虑架子预倾后的横向包围，
  `X=-15.4` 仍留有约 `0.3` 世界单位的完整出屏余量。
- `KeepBallHeld()` 继续绑定在整条 Sequence 的 OnUpdate，球在 Intro 全程保持
  Kinematic 并跟随预倾、回正和 Punch；Sequence 完成后强制恢复
  `_initialClipSwingLocalRotation`。首笔完成后的 `clip_open`、脱离父级、Dynamic、
  猫 `control`、倒计时、成功门和失败重载均未修改。
- Unity 6.0.59f2 已重新导入 `clip.json`，Spine importer 清理并重载原
  `clip_SkeletonData.asset`；`Assembly-CSharp.dll` 已重新编译，最近编译日志无新增
  C# 错误。静态检查确认新 JSON 哈希与用户来料一致，Prefab 的七个入场参数和起始 X
  均已写入。编辑器内另以真实 Prefab 渲染了 `+38°` 起始帧和静止帧，确认当前镜像下
  架子与球向左后方倾斜、回正后仍完整包住球；临时预览脚本随后已删除。
- `uncertain`：尚未在目标帧率和移动设备上完整观看新的 `0.966s` 连续动态节奏，
  到位 Punch 的主观力度仍需 PlayMode 人工确认。回滚时恢复旧 `clip.json` 的四个
  close 角度、Alien `X=-16.72`、`entranceDuration=1.2`，并把控制器恢复为正位开始的
  `18° / 0.85s / vibrato 3 / elasticity 0.45` Punch 与默认 OutBack。

### 11.8 2026-07-31 架子惯性摆动修正

- 用户反馈第 11.7 节的“先回正、到位后额外 Punch”看起来像飞行结束后突然甩了一下，
  不符合悬挂物的惯性。新要求是：Alien 向右飞时架子先向左后摆；整体制动时架子向右
  前摆越过中线；随后左右短暂衰减几次并停止。
- 当前层级存在 `catch` 的 Y 轴镜像；已由此前 `+38°` 关键帧确认，
  `clipSwingPivot` 局部正角在画面上表现为底端向左，因此后摆继续使用正角，前摆使用
  负角。代码不再调用 `DOPunchRotation`，所有目标都相对
  `_initialClipSwingLocalRotation` 使用绝对局部旋转，避免 Punch 基准位或 Tween 初始化
  时机产生跳变。
- 新显式时间轴全部使用 `Ease.InOutSine`：

| 时间点 | 目标局部角 | 含义 |
| --- | ---: | --- |
| `0.00～0.18s` | `+38°` | 起飞阶段保持向左后摆 |
| `0.43s` | `-32°` | 制动惯性带到向右前摆峰值 |
| `0.57s` | `+15°` | 第一次衰减回摆 |
| `0.68s` | `-7°` | 第二次衰减前摆 |
| `0.77s` | `+3°` | 最后一次微弱后摆 |
| `0.85s` | `0°` | 回到缓存静止姿态 |

- Alien 仍在 `0.78s` 内完成 `OutBack(1.9)` 移动。该缓动约在 `0.269s` 首次经过终点、
  `0.439s` 达到最大位置过冲；架子约在 `0.31s` 越过正位、`0.43s` 达到前摆峰值，
  与整体制动/过冲连续衔接。架子只比 Alien 最终停稳晚约 `0.07s`，Intro 门禁总时长由
  上一版约 `0.966s` 缩短到约 `0.85s`。
- 影响文件仅为 `DLevel2Ctrl.cs`、`DLevel_002.prefab` 及本地说明文档。球在整个 Intro
  继续保持 Kinematic 并由 `KeepBallHeld()` 跟随 Pivot；第一笔后的 `clip_open`、
  保持世界姿态脱父级、Dynamic/WakeUp、猫 `control → idle01`、15 秒倒计时、成功门与
  失败重载链没有改变。没有修改 DrawLineFeature、共享 Handler、Spine 资源、存档、
  Luban、UI、analytics、SDK 或平台分支。
- Unity 6.0.59f2 已重新生成 `Assembly-CSharp.dll`，时间晚于本次脚本；最近日志没有
  `error CS` 或 DLevel2 编译错误。静态检查确认旧 Punch 字段/API 已移除、Prefab 的
  十一个惯性摆字段完整写入、六段时间累计为 `0.85s`，且没有重叠的旋转 Tween。
- `uncertain`：本次未完整运行 DLevel_002 PlayMode 或移动设备，阻尼节奏和 Spine idle
  叠加后的最终主观手感仍需人工观看。若只回滚本小节，应恢复第 11.7 节的
  `0.46s` 回正、70% 起播及 `32° / 0.42s` Punch 字段与 Sequence；资源、放球和业务链
  无需回滚。

### 11.9 2026-07-31 过关时球与人物同步渐隐

- 需求：Actor 进入成功门后，`yellow_ball` 必须和人物在同一退场演出中一起渐隐。
- 原行为中 `DrawLevelCtrl.FadeActorOutAsync()` 在 `0.4s` 内同步设置
  `ActorCtrl.SetSpineAlpha(alpha)` 与 Actor 子级 `SpriteRendererGroupAlpha`。但本关
  `yellow_ball` 是 Actor 的父节点，球的 `idle/open/item` SpriteRenderer 不会被从
  Actor 向下执行的 `CollectRenderers()` 收集，因此人物消失后球仍保持可见。
- `DrawLevelCtrl` 现增加可空的 `successExtraRendererGroup`。成功淡出开始时同时收集
  Actor 组与额外组，并在每一帧及最终帧向两组写入完全相同的 alpha；没有独立 Tween、
  延迟或额外等待。未绑定该字段的其它 DLevel 继续只渐隐 Actor，运行行为不变。
- DLevel_002 在 `yellow_ball` 根增加 `SpriteRendererGroupAlpha` 并绑定到上述字段；
  `includeInactive=true`、`autoCollectRenderers=true`，因此当前 active 的 idle 球和未来
  可能启用的 open/item 状态都会一起淡出。额外组也会收集 Actor 子级 SpriteRenderer，
  但两条路径写入的是同一个绝对 alpha，不发生乘法叠加；Actor 主 Spine 仍由原 API 控制。
- `ResetGameState()` 会重新收集额外组并恢复 alpha 为 1；正式失败、Replay 和下一关仍按
  原流程销毁并重建 DLevel。成功门校验、Actor 停止、门动画、`TryResolve(Success)`、
  15 秒倒计时竞态、球的 Rigidbody2D、碰撞、放球、存档、Luban、UI、analytics、SDK
  和平台分支均未改变。
- 静态检查确认 Prefab 额外组组件、脚本 GUID、DrawLevelCtrl 引用均唯一且非空；编译后的
  `Assembly-CSharp.dll` 已包含 `successExtraRendererGroup` 与 `extraRendererGroup`，
  Unity 最近日志未见 C# 编译或相关 MissingReference 错误。
- `uncertain`：尚未在完整 PlayMode/移动设备目测人物 Spine、人物 Sprite 附件与三套球
  状态的同步透明度。当前球 Sprite 初始 alpha 均为 1；如果未来加入有意半透明素材，
  `SpriteRendererGroupAlpha.SetAlpha()` 的绝对覆盖行为需要重新评估。
- 回滚时清空 DLevel_002 的 `successExtraRendererGroup` 引用、移除 yellow_ball 上新增的
  `SpriteRendererGroupAlpha`，并删除 DrawLevelCtrl 对额外组的收集、SetAlpha 与 Reset；
  不涉及资源、配置或数据迁移。

### 11.10 2026-07-31 共享组合 Prefab 与 DLevel006 复用

- 用户确认调整后的蓝球、外星猫和 Spine 架子还会用于其它关卡，并首先要求 DLevel006
  的左右两套装置全部替换。此前只在 DLevel002 单关出现时保留局部结构；现在已有第二个
  具体复用场景和明确需求，因此抽出
  `Assets/Game/Prefabs/DrawLevel/Shared/DrawGame_AlienClipBallRig.prefab`。
- 共享 Prefab 封装猫 Spine、`BoneFollower(bone2)`、`clipSwingPivot`、架子 Spine、
  调整后的 `yellow_ball`、`payloadContentRoot`、嵌套 `Actor.prefab`、
  `DrawAlienClipBallRigView`，以及后续新增的 `DrawEntrancePathView` 与默认相对起点。
  最终位置、入场时序、首笔事件、倒计时、Tag、皮肤、碰撞成功和 Reset 规则仍由各关实例/
  控制器决定。
- DLevel002 的最终行为未变：从 `X=-15.40` 到 `-4.58`，`0.78s / OutBack(1.9)`，
  架子按 `+38 → -32 → +15 → -7 → +3 → 0°` 衰减；第一笔后开夹、脱父级、Dynamic，
  猫 `control → idle01`，15 秒倒计时以及球与人物同步渐隐均保持原链。
- DLevel006 以同一 Prefab 创建左右两实例，根直接位于最终位置 `-5.90 / +4.78`，使用等长
  `10.82` 的左右反向入场；右根当前为 `(-0.9,0.9,0.9)`。释放时把负缩放等价转移到
  `payloadContentRoot`，让 Dynamic 球根恢复正缩放且视觉/Actor/子碰撞体世界二维姿态不跳。
  两个实例另把共享球的 `Exclude Layers=Actor` 覆盖为 `0`，以保留双球 Collision2D
  成功；两侧 Actor 保留迁移前的 `shrink` 初始动画，右 Actor 继续使用镜像后的
  `box_pink` 皮肤。
- Unity 编译与 Prefab 结构校验通过；隔离 Preview Scene 调用了真实右球释放方法，
  确认球根正缩放、内容子树位置和基向量在 `0.001` 容差内不变。三套实例共享 GUID、
  七个 View 引用、嵌套 Actor、DLevel006 等长路径和碰撞层 override 均已静态确认。
- `uncertain`：尚未完整执行 DLevel002/DLevel006 玩家 PlayMode，也未跑 Android/iOS。
  提交时共享 Prefab、View 脚本及 `.meta` 必须成套纳入；若回滚共享抽取，需先把结构嵌回
  所有引用关卡，再删除共享资源。DLevel006 目前仍是两球碰撞即结算，不含真正吸附。

### 11.11 2026-08-02 夹子碰撞清理与共享初态修复

- 用户反馈共享化后出现多余 Collider Gizmo、人物绘制在球面前，以及 DLevel002 初始人物
  动画回退。审计确认持球由 `Kinematic + KeepBallHeld()` 完成，首笔后靠脱父级并切
  Dynamic 释放，Alien 根和夹子 hook/block/catchArea 碰撞体不参与这条链。
- 共享 Prefab 删除 4 个旧夹子 Collider2D，DLevel002 实例删除 Alien 根新增的 Trigger
  BoxCollider2D；保留 `yellow_ball` CircleCollider2D、Actor 主碰撞体及成功门/场景碰撞体。
- 共享化时丢失的 Actor override 已恢复到共享 Prefab：
  `SkeletonAnimation._animationName=shrink`、`ActorSpineCtrl.testAnimName=shrink`、
  根 `SortingGroup.order=68`。球壳 idle order 为 100，因此人物重新绘制在球壳下方。
  `payloadContentRoot` 继续保留，供 DLevel006 右球释放时承接镜像；当前 Actor 调整后的
  position/rotation/scale 未改。
- DLevel002 Intro 仍为 `X=-15.40 -> -4.58`、`0.78s / OutBack(1.9)` 和
  `+38 -> -32 -> +15 -> -7 -> +3 -> 0°`，猫 `idle01`、架子 `clip_close`、首笔释放、
  15 秒倒计时与成功渐隐链均未改。
- 一次性 Unity Prefab 迁移日志确认共享/DLevel002 分别删除 `4/1` 个目标碰撞体；YAML
  静态检查确认共享只剩球的 CircleCollider2D，三项 Actor override 已写入。完整 PlayMode、
  移动端画面和实际解法仍为 `uncertain`。
- 若只回滚本节，可恢复上述 5 个 Collider2D 及 DLevel002 Alien 实例 Trigger，并移除共享
  Actor 三项 override；这会重新引入夹子阻挡画线/球和人物遮挡回归，不涉及存档、Luban、
  UI、analytics 或平台数据迁移。

### 11.12 2026-08-02 最终位置配置与入场轨迹可视化

- 新的统一摆放规则是：猫、夹子和球的移动根节点在 Prefab/Scene 中始终位于最终释放位置，
  `entranceStartPoint` 子节点单独表示场外起点。DLevel002 根由 `(-15.40,2.41,0)` 迁移到
  `(-4.58,2.41,0)`，共享子节点仍精确表示旧起点；旧同级 `endPoint` 已删除。
- 新增共享 `DrawEntrancePathView`。它在 Scene 中绘制起终点圆标、虚线路径、箭头，以及把
  当前 Renderer 总包围盒平移到起点的虚拟占位；PlayMode 中不绘制，也不修改 Transform、
  刚体、动画或结算。
- DLevel2Ctrl 在 Awake 首次移动根节点前缓存世界起点和策划最终位置，随后放回起点；Intro
  与 Reset 只使用缓存值，避免子起点随根移动后被二次读取。Tween 改为完整 `DOMove`，但
  本关起终点 Y/Z 相同，仍是 `10.82` 距离、`0.78s / OutBack(1.9)` 的既有水平演出。
- 夹子摆动、猫 `idle01/control`、首笔放球、球脱父级、15 秒倒计时、成功球/人物同步渐隐、
  FallArea 和失败重载均未改。
- 同一扫描确认只有 DLevel02、DLevel06、DLevel10 属于这类“可见入场后放球”结构，已全部
  采用同一契约；其它 Draw 关卡释放的是箱子、箭、线、武器或气球，不在本次范围。
- Unity 迁移器对本关起终点做了 `0.001` 容差校验；C# 编译、共享 Prefab 与本关
  PrefabImporter 已通过。完整 PlayMode、暂停/Replay、成功竞态与 Android/iOS 仍为
  `uncertain`。
- 回滚必须成套恢复场外根位置、同级 `endPoint`、旧缓存方向与 `DOMoveX`；单独删 Gizmo
  不影响玩法，但单独移动根会让首帧/Reset 使用错误位置。

### 11.13 2026-08-02 球内 Actor 多边形碰撞移除

- Actor GameObject、`ActorCtrl`、`shrink` 表现、15 秒倒计时身份和成功门链全部保留；本次只在
  共享 Rig 的 Actor 嵌套实例移除 `PolygonCollider2D`，不修改通用 `Actor.prefab`。
- Actor 自带 Rigidbody2D 继续沿用既有 removed override；球的 Dynamic 由 `yellow_ball` 根
  Rigidbody2D 统一模拟，球根 CircleCollider2D 与 Actor 既有子 BoxCollider2D 保留，只移除角色
  轮廓 PolygonCollider2D。成功时 Actor 与球壳仍同步渐隐。
- 共享改动同步用于 DLevel006/010/015。需 PlayMode 回归球落地、成功门命中、倒计时失败和
  Reset/Replay；当前为 `uncertain`。回滚只恢复 PolygonCollider2D removed override，禁止恢复
  Actor 自带 Rigidbody2D。

### 11.14 2026-08-02 球壳进入成功门的 Actor 身份回认修复

- 用户在 PlayMode 反馈 DLevel002 蓝球靠近并进入成功门后没有过关。成功门本体、
  `OnTrigger2DHandle(targetTag=Player)`、`OnTriggerSuccess` 事件绑定、Layer 与 Physics2D
  矩阵均正常；回归来自 11.13 删除 Actor `PolygonCollider2D` 后，实际进入门的是父级
  `yellow_ball` 的 `CircleCollider2D`。
- 旧 `DrawLevelCtrl.OnTriggerSuccess()` 只调用
  `collider.GetComponentInParent<ActorCtrl>()`。当前 Actor 位于 `yellow_ball/payloadContentRoot`
  子层级，无法从球壳 Collider 向父级找到，因此在正式成功演出前被误拒绝。
- `DrawLevelCtrl` 新增 `ResolveSuccessTriggerActor(Collider2D)`：普通角色仍沿父级解析；父级
  不存在 Actor 时，精确比较门内 Collider 的 `attachedRigidbody` 与本关 Actor 最近的父级
  `Rigidbody2D`。只有两者是同一实际复合刚体时才回认 `_actor`，之后仍执行
  `triggerActor == _actor && !triggerActor.IsDied`，不会放宽为任意 Player Tag 物体过关。
- 本修复保留“删除多余 Actor 多边形碰撞体”的要求，同时覆盖同样使用共享球 Rig 与成功门的
  DLevel010、DLevel015；DLevel006 使用双球相遇组件，不经过成功门分支。门动画、人物与球同步
  渐隐、15 秒倒计时、失败重载、UI、存档、Luban、analytics 与平台链均未改变。
- Unity 生成的 Assembly-CSharp 编译响应文件已全量编译通过，`0 error`；Prefab 静态检查确认
  球根仍为 Player Tag、门回调仍唯一绑定到本关 DrawLevelCtrl。完整 DLevel002 PlayMode 进门、
  DLevel010/015 回归与 Android/iOS 仍为 `uncertain`。
- 回滚只需移除 `ResolveSuccessTriggerActor()` 并恢复旧父级查找；这会重新引入共享球壳无法过关。
  不应通过恢复已删除的 Actor Rigidbody2D/PolygonCollider2D 回滚，以免破坏共享复合刚体结构。
