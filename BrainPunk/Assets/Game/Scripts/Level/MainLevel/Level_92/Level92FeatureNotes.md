# Level92 双次进门通关功能记录

## 需求与背景

- Level 92 第一次进入结束门时，播放既有开门、彩带、烟花、音效与震动表现，但不结算通关。
- 第一次表现结束后，主角复用关卡开局的出生门动画重新走出，并恢复玩家控制。
- 主角第二次进入结束门时，才进入原有标准通关流程。
- 死亡触发的失败重开、手动重置或关卡对象销毁后，本关进门计数必须清零。
- 实现必须局限于 Level 92，不能修改高复用的基础关卡、角色、门动画、存档或配置模块。

## 影响文件与系统

- 新增本关控制器：`Assets/Game/Scripts/Level/LevelItem/Level_92/Level92Ctrl.cs`
- 修改本关 Prefab：`Assets/Game/Prefabs/Level/Level_092.prefab`
- 新增本地文档：`Level92FeatureNotes.md`、`Level92RiskAssessment.md`
- 更新综合记录：`DevelopMD/已有功能设计与使用记录.md`
- 仅复用且未修改：`BaseLevelCtrl`、`SuccessAnimCtrl`、`EnterDoorAnimCtrl`、`ActorCtrl`、`OnTrigger2DHandle`、`GameResetEvent`。

## 原有行为基线

- 结束门的 `OnTrigger2DHandle.onEnter` 原本直接调用 `BaseLevelCtrl.OnActorTrigger2D(Collider2D)`。
- Actor 层进入后，`BaseLevelCtrl` 将 `IsActorEnterDoor` 设为 true；下一次关卡更新进入 `FinishedLevel()`、明信片星收集、`SuccessAnimCtrl.Play(...)` 与 SUCCESS 状态切换。
- `SuccessAnimCtrl` 默认通过 `ActorDestroyAnimHandle` 缩小并销毁角色，适合单次通关，无法让同一角色再次从出生门返回。
- `BaseLevelCtrl.TryToPlayEnterDoorAnim()` 是现有开局出生门表现入口。

## 新行为

- `Level_092.prefab` 根节点新增 `Level92Ctrl`；只有本关结束门的 UnityEvent 改绑到 `OnActorEnterSuccessDoor(Collider2D)`。
- 第一次有效触发只把本地计数设为 1，锁住玩家输入，播放 `SuccessAnimCtrl`，同时由本关 DOTween 把角色移入结束门、缩放到 0 并暂停 Rigidbody2D 模拟。
- 第一次流程不调用 `BaseLevelCtrl.OnActorTrigger2D`，因此不会提前设置 `IsActorEnterDoor`，也不会进入标准成功结算。
- 结束门表现回调到达后，本关恢复角色初始缩放、旋转和 Rigidbody2D 模拟，再调用 `BaseLevelCtrl.TryToPlayEnterDoorAnim()` 复用出生门走出表现。
- 第二次有效触发先设置本地防重标记，再调用原 `BaseLevelCtrl.OnActorTrigger2D`；后续成功动画、明信片星、SUCCESS 状态与胜利流程保持原链路。
- Level 92 的结束门实例将 `SuccessAnimCtrl.destroyActor` 置空，并移除该实例上的 `ActorDestroyAnimHandle`，避免两次流程销毁角色；角色进门表现仅由 `Level92Ctrl` 管理。
- `GameResetEvent` 会清零计数、使旧异步回调失效、清除 `IsActorEnterDoor`，并在角色尚未死亡时恢复本地 Transform 与 Rigidbody2D 状态。
- 异步流程使用递增版本号和当前阶段条件；重置、禁用或销毁后的旧回调不能再次传送角色、恢复操作或接受正式通关。

## 数据、资源、UI、存档、事件与平台流

- 第一次进入不会触发明信片星收集、SUCCESS、胜利 UI、关卡解锁、存档或分析打点；该结论来自第一次路径未调用 `BaseLevelCtrl` 成功入口的静态代码证据。
- 第二次进入完整复用现有 `BaseLevelCtrl` 成功链，不复制存档、奖励或状态切换实现。
- 仅订阅现有 `GameResetEvent`；没有新增全局事件、DB 字段、PlayerPrefs、Luban/RemoteConfig 配置、资源地址、热更规则、analytics、广告、内购或平台 SDK 分支。
- 失败死亡由现有 `BaseLevelCtrl` 最终派发 `GameResetEvent(GameResetType.Failed)`，手动重置派发 `GameResetEvent(GameResetType.Manually)`；两者共用同一个本地清零入口。

## 已执行验证

- 静态审计确认共享 `BaseLevelCtrl.cs`、`SuccessAnimCtrl.cs`、`EnterDoorAnimCtrl.cs`、`ActorCtrl.cs`、存档和配置文件未被本功能修改。
- Prefab 文本审计确认根节点只有一个 `Level92Ctrl`，四个必要引用均非空；结束门只有一个重写后的持久调用，目标为 `Level92Ctrl.OnActorEnterSuccessDoor`，动态 `Collider2D` 调用模式保持不变。
- Prefab 文本审计确认只在 Level 92 结束门实例上清空一次 `destroyActor` 并移除一次 `ActorDestroyAnimHandle`。
- 静态代码扫描未发现 `Update`/`FixedUpdate` 轮询、编辑器生命周期、DB/save、配置、资源加载、analytics 或平台 SDK 新入口。
- 使用 Unity 6000.0.59f2 自带 Roslyn，引用当前工程的 UnityEngine、Physics2D、UniTask、DOTween、事件/日志程序集和现有 `Assembly-CSharp.dll`，独立编译最新 `Level92Ctrl.cs` 通过，结果为 0 errors / 0 warnings。
- Luban 源表中的 `hintsResetCount` 列已恢复并重新生成配置代码与 JSON；Unity 6000.0.59f2 于 2026-07-16 16:58 重新生成 `Assembly-CSharp.dll/.pdb`，最新编译轮次未再出现原 `CS1061` 或 Level 92 错误，程序集内可检出 `HintsResetCount` 与 `Level92Ctrl`。Inspector 和 PlayMode 仍未执行。

## 手动验收清单

- 初次进入 Level 92 时，主角仍按原开局出生门动画出现。
- 第一次进入结束门时完整播放门、彩带、烟花、音效与震动；不出现胜利界面、不增加明信片星、不解锁下一关。
- 第一次表现结束后，主角从出生门走出，大小、朝向、碰撞、移动与跳跃恢复正常。
- 第二次进入结束门时只触发一次标准成功流程，并正常结算、存档和解锁。
- 第一次进入前、第一次返回后、第一次动画中分别测试死亡与手动重置；新尝试均必须从 0 次开始。
- 重置黑屏期间不能看到旧角色复活、传送、移动或提前解除输入遮罩。
- 最终成功等待期间角色不能残留碰撞或重复触发结束门。
- Android/iOS 横屏设备补验触摸、震动、帧稳定性和切后台恢复。

## 剩余风险与未验证项

- Unity PlayMode、Inspector 实例绑定、Android 与 iOS 尚未执行，视觉时序和真实物理恢复仍需按清单验收。
- `SuccessAnimCtrl.Play(Action)` 是共享的 `async void` 实现；对象销毁取消和 block 释放依赖现有重建流程。Level 92 通过版本失效与本地状态恢复限制旧回调影响，但共享方法自身的取消日志表现仍待 PlayMode 观察。
- 静态 YAML 能证明序列化文本关系，不能完全替代 Unity 导入后的 Missing Script/Inspector 检查。

## 回滚路径

- 从 `Level_092.prefab` 根节点移除 `Level92Ctrl`，把结束门 UnityEvent 目标恢复为 `BaseLevelCtrl.OnActorTrigger2D`。
- 恢复 Level 92 结束门实例的 `ActorDestroyAnimHandle` 与 `SuccessAnimCtrl.destroyActor` 引用。
- 删除 `Assets/Game/Scripts/Level/LevelItem/Level_92/Level92Ctrl.cs` 及其 meta，并移除本次 Level 92 文档记录。
- 回滚不需要修改任何共享脚本、存档、配置或其他关卡 Prefab。

## 2026-07-22：第二次进门皮肤闪帧修复

### 需求与原行为基线

- PlayMode 观察到主角第二次进入结束门时，已穿戴的皮肤部件会短暂闪现一帧。
- 修复前第二次有效触发只设置本地防重状态、启动 `PlayActorEnterDoorAsync(...)` 并调用 `BaseLevelCtrl.OnActorTrigger2D(...)`；没有像第一次触发一样在当前调用栈内立即锁定控制和停止 Actor。
- `BaseLevelCtrl` 要到下一次 `Update()` 才检查成功并调用 `ActorCtrl.StopMoving()`，而本关异步缩门流程要等待 `actorEnterDelay`（当前 prefab 为 0.4 秒）后才再次停止 Actor，因此触发帧仍可能继续一次输入、移动、翻转或动画更新。

### 影响文件与修复后行为

- 运行时代码仅修改 `Level92Ctrl.OnActorEnterSuccessDoor(...)`；同步更新本功能记录、同目录风险评估和 `DevelopMD/已有功能设计与使用记录.md`。
- 第二次有效触发在启动异步缩门和转交标准成功入口前，立即调用 `levelCtrl.SetCtrlable(false)`、`actor.SetMoveInput(0f)` 与 `actor.StopMoving()`。
- 正式成功入口、0.4 秒门表现等待、DOTween 移动/旋转/缩放、明信片星、SUCCESS、胜利 UI、存档和解锁链路保持原实现。
- 未修改 `BaseLevelCtrl`、`ActorCtrl`、`ActorSkinBinding`、`SuccessAnimCtrl`、`EnterDoorAnimCtrl` 或 prefab；未新增全局事件、资源加载、DB/save 字段、配置、analytics、广告、IAP 或平台分支。

### 验证与剩余风险

- 静态调用顺序确认第二次触发会在 `PlayActorEnterDoorAsync(...).Forget()` 和 `BaseLevelCtrl.OnActorTrigger2D(...)` 前同步停止 Actor，不再依赖下一帧成功检查完成首次停止。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 个错误、99 个当前工程警告。
- 尚未重新执行 Unity PlayMode 逐帧录制；需要复验第二次触发时皮肤不再闪帧，同时确认第一次假通关、出生门返回、第二次正式结算和重置路径保持一致。
- Android/iOS 横屏设备仍需验证第二次门触发时的视觉时序、触控锁定和帧稳定性。

### 回滚路径

- 删除第二次分支新增的 `SetCtrlable(false)`、`SetMoveInput(0f)` 与 `StopMoving()` 三个同步调用，即可恢复修复前由下一帧标准成功检查负责首次停止 Actor 的行为；共享脚本、prefab、存档和配置无需回滚。

## 2026-07-22：出生门返回皮肤闪现修复

### 需求与原行为基线

- 第一次假通关结束后，`ReturnFromStartDoorAsync(...)` 原本先把 Actor 缩放从 0 恢复为初始值，再调用出生门动画隐藏角色。
- `EnterDoorAnimCtrl` 首次隐藏会立即处理主 Spine 和已缓存的 `SpriteRendererGroupAlpha` 列表，但要等待 0.1 秒才重新收集渲染器；异步实例化且不在旧列表中的皮肤部件可能在恢复缩放后短暂可见。

### 修复后行为与影响范围

- 正常出生门返回现在会先把主 Spine 设为透明，并重新收集 Actor 下的全部 `SpriteRenderer` 后统一设为透明，再恢复缩放、旋转和 Rigidbody2D 状态。
- 既有 `BaseLevelCtrl.TryToPlayEnterDoorAnim()` 继续负责角色定位、开门、等待皮肤加载窗口、淡入走出和关门；完成后本关再兜底恢复主 Spine 与全部皮肤渲染器可见。
- 修改仅位于 `Level92Ctrl`，未修改共享 `EnterDoorAnimCtrl`、`ActorCtrl`、`ActorSkinBinding`、prefab、资源、存档、配置、事件、analytics、广告、IAP 或平台分支。
- `GameResetEvent` 的原恢复入口不增加预隐藏步骤，避免改变现有重置黑屏和关卡重建时序。

### 验证、剩余风险与回滚

- 静态调用顺序确认 `PrepareActorHiddenForStartDoor()` 位于 `RestoreActorForStartDoor()` 之前，并在设为透明前调用 `CollectRenderers()` 覆盖当前动态皮肤部件。
- 工程级 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 个错误、99 个当前工程警告；`git diff --check` 与敏感项扫描通过。
- Unity PlayMode 逐帧录制及 Android/iOS 横屏设备仍待验证。
- PlayMode 需重点复验第一次缩入结束门、恢复缩放时无闪现、出生门淡入完整、控制恢复、第二次正式通关，以及皮肤资源加载较慢时的新部件透明度。
- 回滚时删除两个本地透明度辅助方法、`actorRendererGroup` 缓存字段及正常返回流程中的隐藏/可见调用即可；共享脚本、prefab、存档、配置和资源无需回滚。
