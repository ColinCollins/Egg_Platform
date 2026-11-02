# Level92 双次进门通关风险评估

## 原始行为基线

- Level 92 原结束门沿用公共单次通关绑定：Actor 首次进入即把 `BaseLevelCtrl.IsActorEnterDoor` 设为 true。
- 公共 `SuccessAnimCtrl` 默认播放结束门表现，并由 `ActorDestroyAnimHandle` 缩小、旋转、移动后销毁角色。
- 公共出生门动画由 `BaseLevelCtrl.TryToPlayEnterDoorAnim()` 驱动。

## 影响文件与资源

- `Assets/Game/Scripts/Level/LevelItem/Level_92/Level92Ctrl.cs`
- `Assets/Game/Prefabs/Level/Level_092.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_92/Level92FeatureNotes.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_92/Level92RiskAssessment.md`
- `DevelopMD/已有功能设计与使用记录.md`
- 关联但未修改：`BaseLevelCtrl.cs`、`SuccessAnimCtrl.cs`、`EnterDoorAnimCtrl.cs`、`ActorCtrl.cs`、`OnTrigger2DHandle.cs`、`PlayEvents.cs`。

## 使用的既有系统

- `SuccessAnimCtrl.Play(...)`：结束门、彩带、烟花、音效和震动表现。
- `BaseLevelCtrl.TryToPlayEnterDoorAnim()`：出生门走出表现。
- `BaseLevelCtrl.OnActorTrigger2D(...)`：第二次进入后的标准成功入口。
- `GameResetEvent`：失败重开与手动重置的统一本地清零入口。
- `ActorCtrl`、`Rigidbody2D` 与 DOTween：本关角色停止、进门、Transform 和物理恢复。

## 风险项与缓解

- **P1 第一次误结算**：若第一次调用 `BaseLevelCtrl.OnActorTrigger2D`，会提前获得星星和进入 SUCCESS。实现只在第二次调用公共入口；第一次显式保持 `IsActorEnterDoor=false`。
- **P1 角色被公共门动画销毁**：同一角色无法再次从出生门返回。仅在 `Level_092.prefab` 的结束门实例上清空 `destroyActor` 并移除 `ActorDestroyAnimHandle`，由本关脚本处理两次进门表现。
- **P1 重置残留物理或缩放**：重置可能发生在角色缩门 Tween 中。`OnGameReset` 先使旧流程失效并 Kill Tween，再恢复初始缩放、旋转、速度和 `Rigidbody2D.simulated`。
- **P1 旧异步回调串入新尝试**：成功门回调与出生门等待可能晚于重置。递增的 flow version、计数和阶段标记共同拒绝旧回调；失效的出生门 continuation 会重新停止旧角色，不恢复控制。
- **P1 重复碰撞跳过计数**：第一次播放期间由 `isFirstPassPlaying` 拒绝重复触发，第二次接受后由 `isFinalPassAccepted` 拒绝再次派发正式成功。
- **P1 死亡后仍完成第二次**：入口要求 `actor.IsDied == false`；失败重开派发 `GameResetEvent` 后清零。出生门等待期间检测到死亡时也会清空本地尝试并保持输入锁定。
- **P1 Prefab 引用缺失**：四个核心引用已静态确认非空，代码仍有本地查找和 warning 降级；Inspector/Missing Script 可视验证仍待执行。
- **P2 共享 async void 取消**：`SuccessAnimCtrl.Play(Action)` 无法由 Level 92 await，且共享实现没有本地 catch/finally。为遵守共享代码保护要求，本次不修改该模块；重置时依赖版本失效、角色恢复和整关重建，取消日志与遮罩时序需 PlayMode 重点观察。
- **OBS 性能**：每次进门只创建一个有上界的 DOTween Sequence，没有 `Update`、全关扫描、实例化循环或新资源加载；移动端仍需烟测。

## 验证证据

- `Level92Ctrl` 静态审计确认第一次和第二次路径分离，正式成功入口只有第二次可达。
- Prefab 文本计数确认 `Level92Ctrl` 组件、脚本 GUID、事件目标、方法名、程序集名、`destroyActor` override 和移除组件记录各出现一次。
- 结束门源 Prefab 的 UnityEvent `m_Mode=0`；Level 92 只覆盖 target/type/method，因此保留动态 `Collider2D` 参数模式。
- 静态敏感项扫描未发现编辑器生命周期、每帧轮询、DB/save、PlayerPrefs、RemoteConfig、资源加载、analytics、广告、IAP 或平台 SDK 新入口。
- Unity 6000.0.59f2 自带 Roslyn 对最新 `Level92Ctrl.cs` 的局部编译通过，引用当前工程相关程序集后为 0 errors / 0 warnings；该结果只证明脚本语法与当前 API 调用成立。
- 共享脚本未进入本功能变更集；工作区另有大量用户改动，本次没有清理或覆盖。
- `LevelData.HintsResetCount` 的源表列与生成物已恢复；Unity 6000.0.59f2 于 2026-07-16 16:58 重新生成 `Assembly-CSharp.dll/.pdb`，最新编译轮次未再出现原 `CS1061` 或 Level 92 错误，程序集内同时可检出 `HintsResetCount` 与 `Level92Ctrl`。

## 未验证项

- Unity Editor 对 Level 92 prefab 的完整可视导入结果与域重载后运行验证。
- Unity Inspector 的组件、动态 UnityEvent 和 nested prefab override 可视确认。
- PlayMode 两次进门完整时序、第一次不结算、第二次只结算一次。
- 第一次门动画前/中/后死亡和手动重置，以及重置黑屏期间的旧回调表现。
- Actor 皮肤异步加载下的出生门透明度、缩放、朝向、碰撞和移动恢复。
- Android/iOS 横屏触摸、震动、切后台和帧稳定性。

## 回滚路径

- 删除 Level 92 根节点上的专用控制器与脚本。
- 把结束门事件恢复到 `BaseLevelCtrl.OnActorTrigger2D`，恢复该实例的公共角色销毁组件和引用。
- 删除本地 Level 92 文档并移除综合记录对应章节；共享代码无需回滚。

## 2026-07-22：第二次进门闪帧风险复核

### 新增风险与缓解

- **P1 第二次触发帧仍更新 Actor**：修复前第二次分支没有同步锁控制或停止 Actor，`BaseLevelCtrl` 需到下一次 `Update()` 才进入成功处理，本关缩门逻辑还会等待 0.4 秒；该窗口允许 Actor 再执行一次输入、移动、翻转或动画更新，独立皮肤部件可能在结束门前短暂露出。当前在转交标准成功入口前同步执行 `SetCtrlable(false)`、`SetMoveInput(0f)` 和 `StopMoving()`，消除对下一帧首次停止的依赖。
- **P2 提前停止影响正式成功链**：新增调用只改变第二次触发后的 Actor 可控和速度状态；`BaseLevelCtrl.OnActorTrigger2D(...)`、成功检查、门表现、明信片星、SUCCESS、存档和解锁入口未变。`PlayActorEnterDoorAsync(...)` 原有的延迟、Rigidbody2D 暂停和 Tween 仍保留。
- **OBS 共享回归范围**：修复仅位于 `Level92Ctrl`，没有修改 `ActorCtrl`、`BaseLevelCtrl`、皮肤绑定、公共门脚本或 prefab，因此其他关卡不会进入新增分支。

### 验证证据

- 静态调用顺序确认三个同步停止调用位于第二次分支的异步缩门启动和标准成功入口之前；第一次进门分支、重置分支与防重条件未修改。
- 工程级 `dotnet build` 通过，结果为 0 个错误、99 个当前工程警告。
- 本次没有新增 `Update`/`FixedUpdate`、编辑器生命周期、实例化、DB/save、配置、资源、全局事件、analytics、广告、IAP 或平台 SDK 入口。

### 未验证项与回滚

- 尚未通过 Unity PlayMode 逐帧录制确认皮肤闪帧消失；需要同时回归第二次只结算一次、触发后不能继续移动/跳跃，以及正式门表现完整。
- Android/iOS 横屏设备的视觉时序、触控锁定和帧稳定性仍待验证。
- 回滚时仅删除 `Level92Ctrl.OnActorEnterSuccessDoor(...)` 第二次分支新增的三个同步停止调用；无需回滚共享脚本、prefab、存档、配置或资源。

## 2026-07-22：出生门恢复缩放闪现风险复核

### 新增风险与缓解

- **P1 恢复缩放早于完整隐藏**：原流程先把 Actor 从缩放 0 恢复为初始值，再进入出生门隐藏逻辑；`SpriteRendererGroupAlpha` 的缓存可能不包含异步实例化的皮肤部件，而出生门流程要等待 0.1 秒才重新收集，形成短暂可见窗口。当前在恢复缩放前先将主 Spine 设为透明，并 `CollectRenderers()` 后统一把动态皮肤渲染器设为透明。
- **P1 降级路径保持透明**：若出生门引用缺失或流程提前结束，只依赖共享出生门的 `finally` 可能无法恢复可见。当前仅在本地返回流程仍有效且 Actor 未死亡时兜底重新收集并恢复 Alpha 1，再恢复移动。
- **P2 重置时序回归**：预隐藏只加入第一次假通关后的正常出生门返回，不加入 `OnGameReset`，避免改变失败/手动重置期间的黑屏与整关重建行为。
- **OBS 共享回归范围**：没有修改 `EnterDoorAnimCtrl`、`ActorCtrl`、`ActorSkinBinding` 或 prefab，其他关卡不会进入新增透明度顺序。

### 验证证据

- 静态调用顺序确认完整隐藏发生在缩放、旋转和 Rigidbody2D 恢复之前；透明度处理同时覆盖 Spine 与重新收集后的全部 `SpriteRenderer`。
- 工程级 `dotnet build` 通过，结果为 0 个错误、99 个当前工程警告；`git diff --check` 通过。
- 敏感项扫描确认没有新增编辑器生命周期、DB/save、资源加载、配置、实例化或全局事件入口；命中的 `GameResetEvent` 订阅和游戏面板遮罩派发均为本关既有代码。

### 未验证项与回滚

- Unity PlayMode 逐帧录制尚未执行，仍需确认恢复缩放帧无皮肤闪现、出生门淡入不延迟或跳变、控制与碰撞恢复正常。
- 皮肤资源在出生门等待窗口内完成异步加载的情况，以及 Android/iOS 横屏设备表现仍待验证。
- 回滚仅需移除 `Level92Ctrl` 的本地渲染器缓存、预隐藏与完成后可见兜底；共享脚本、prefab、存档、配置和资源无需回滚。
