# OnTriggerTrapHandle 触发模式功能与风险评估

## 1. 需求与背景

在保留原 `OnTriggerEnter2D` 陷阱入口的基础上，允许个别实例使用
`OnCollisionEnter2D` 触发同一套玩家判定、受击表现和 `OnTriggerTrapEvent`。

2026-07-28 补充：DLevel5 Reset 日志证明旧陷阱的 `TriggerTrapFailed()` 可在所属关卡
销毁后完成 0.5 秒等待，并把过期的全局事件发送给新关卡，导致新 Actor 再次进入死亡动画。

## 2. 原行为与修改后行为

- 原行为：仅响应 Trigger2D；要求对方 Layer 为 `Actor` 且 Tag 为 `Player`。
- 新增 `[SerializeField] bool isCollision`，默认 `false`。
- `isCollision=false`：只处理 `OnTriggerEnter2D`，现有 prefab 和关卡行为不变。
- `isCollision=true`：忽略 Trigger2D，改由 `OnCollisionEnter2D` 取得对方 Collider2D。
- 两个入口统一进入 `TryPlayKnock(...)`，继续复用原 `PlayKnock(...)`、死亡门禁、
  击飞、音效和延迟派发失败事件逻辑。
- `TriggerTrapFailed()` 的 0.5 秒等待现在绑定组件的
  `GetCancellationTokenOnDestroy()`，并使用 `SuppressCancellationThrow()` 收束正常取消；
  组件在等待期间销毁时不再派发过期的 `OnTriggerTrapEvent`。
- 2026-09-18 起，组件未销毁、等待正常完成且本次命中的 Actor 仍有效并激活时，继续派发原失败事件；单纯 Disable 陷阱组件且 Actor 仍激活时保持原派发行为，Actor 随旧关卡失活后不再派发。

## 3. 影响范围与系统边界

- 修改脚本：`OnTriggerTrapHandle.cs`。
- 静态引用包含 `Caltrop_mid.prefab`、`Caltrop_long.prefab`、DLevel_004 及多个主线关卡。
- 不修改现有 prefab 的 `isCollision` 值；新增字段缺省为 `false`。
- 不新增事件、存档、配置、资源地址、UI、广告、打点或平台 SDK。
- 该取消门禁影响所有使用此共享组件的关卡，但只改变“陷阱对象已销毁后仍派发事件”的
  越界行为，不改变存活实例的 Trigger/Collision、受击、音效和 0.5 秒失败时序。

## 4. 验证、风险与回滚

- 静态检查：Trigger 与 Collision 入口具有互斥门禁，统一复用原 Layer/Tag 判定；
  目标代码和文档无尾随空白，`git diff --check` 通过。
- C# 编译：`dotnet build Assembly-CSharp.csproj --no-restore` 通过，
  `0 error / 102 warning`；warning 为工程现有输出。
- 销毁取消修复后执行 Assembly-CSharp 聚焦构建，10.17 秒完成，
  `0 error / 103 warning`；目标 C#/Markdown 的 `git diff --check` 通过。
- Unity PlayMode、Android/iOS：未验证。
- DLevel5 Reset 修复仍需 PlayMode 验证：触发陷阱后立即 Reset，确认新 Actor 保持 idle；
  正常不 Reset 的陷阱失败仍应在 0.5 秒后进入原失败链。
- 风险：Collision 模式要求碰撞双方满足 Unity 2D 碰撞回调条件，且对方仍需使用
  `Actor` Layer 与 `Player` Tag；配置错误时不会触发。
- 回滚取消修复：将 `TriggerTrapFailed()` 恢复为无 cancellationToken 的
  `UniTask.WaitForSeconds(0.5f)`；触发模式整体回滚则删除 `isCollision`、
  `OnCollisionEnter2D` 和 `TryPlayKnock(...)`，将原 Layer/Tag 判断恢复到
  `OnTriggerEnter2D`。

## 5. 2026-09-18：旧 Actor 失活后的延迟事件防护

### 需求、原行为与证据

- 用户实测：失败黑屏消失后，重建场景中的 Actor 播放 `die` 动画；用户已确认采用当前脚本内的 Actor 生命周期检查。
- 原行为：`TriggerTrapFailed()` 只绑定陷阱的销毁取消令牌，等待完成后没有检查本次命中的 Actor 是否仍有效、仍激活。
- 本地日志 `C:/Users/admin/AppData/Local/Unity/Editor/Editor.log` 在 DLevel_067 重载日志之后，记录了 `OnTriggerTrapHandle.TriggerTrapFailed → DrawLevelCtrl.OnTriggerTrap → ActorCtrl.Die` 调用栈（检索 `DrawLevelCtrl:OnTriggerTrap`）。日志确认该延迟事件链调用了死亡方法；发送者/接收者的实例 ID 与准确帧序尚未记录，具体实例归属为 `uncertain`。
- `DrawLevelCtrl.CheckFinished()` 可以在看到 `Actor.IsDied` 后直接提交失败；`DrawPlayCtrl.DestroyLevelInstance()` 先 `SetActive(false)` 再安排帧末销毁，新关卡初始化时注册陷阱事件订阅。由此存在旧关卡已失活、销毁取消尚未触发、旧延迟事件仍可派发的窗口，需用 PlayMode 复现进一步核对。

### 修改后的行为与影响范围

- 在等待开始前保存 `hitActor`，派发前统一检查组件是否已销毁、`hitActor` 是否已销毁或为空，以及 `hitActor.gameObject.activeInHierarchy`；任一条件不满足时不派发。
- 保存的是本次等待开始时的 Actor，等待期间即使 `ManuallySet()` 重新绑定字段，也不会用新 Actor 的激活状态放行旧等待。
- 检查放在等待分支之外，覆盖正数、零及负数 `trapDelay`；保留现有等待、销毁取消、Trigger/Collision、受击粒子、击退、死亡和音效代码。
- 行为变化是阻止本次命中的 Actor 已失活或已销毁后发送全局失败事件；作用范围为所有使用此共享脚本的场景。对有效且激活的 Actor，源码仍保留原派发条件与延迟参数，但既有关卡表现尚未完成运行时回归。
- 实现只修改 `OnTriggerTrapHandle.cs`，不增加序列化字段或 Prefab 绑定；事件仍为既有 `OnTriggerTrapEvent`。配置、存档、资源地址、UI、广告和打点实现没有本次改动。

### 验证、剩余风险与回滚

- 实施前已记录 `git status --short`；字体、build_info、Guru GF 设置以及未跟踪的 DLevel_067 与参考图是任务开始前的已有改动。
- 静态差异检查：目标文件 `git diff --check` 通过；源码仅增加等待前的局部引用和派发前检查，原等待/取消及受击逻辑未改写。由源码核对了非正数延迟也经过检查、等待期间字段重新绑定不会替换本次检查目标；此为静态证据，不代表运行时验证。
- C# 编译：`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo -p:BuildProjectReferences=false` 通过，`0 error / 125 warning`，耗时 11.03 秒。关闭依赖项目构建以避免写入只读 Package；输出保存在 `C:/Users/admin/AppData/Local/Temp/brainpunk-ontriggertrap-20260918-build.log`。
- PlayMode 待验证：正常 Trigger/Collision 失败；Actor 失活到帧末销毁的窗口；等待期间 Actor/陷阱销毁；仅禁用陷阱且 Actor 仍激活；零/负数延迟；等待期间重新绑定 Actor；连续失败 Reset 后新 Actor 的状态。
- `uncertain`：尚未验证 DLevel_067 实际黑屏消失后的 Actor 动画、共享脚本的全部既有关卡，以及 Android/iOS 运行表现。
- 风险：Actor 因其它局部表现临时失活而非整关重建时，本次待发送事件同样会被抑制；未证明所有共享场景都允许这一结果。该检查不处理其它脚本直接派发的过期事件，也不负责去重有效 Actor 的其它发送来源。
- 回滚：仅移除本次 `hitActor` 局部变量及派发前检查，恢复原事件发送条件；保留既有销毁取消逻辑和全部 Prefab 绑定，并同步撤销本文对应说明。
