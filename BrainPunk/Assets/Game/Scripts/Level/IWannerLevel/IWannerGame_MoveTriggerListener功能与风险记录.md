# IWannerGame_MoveTriggerListener 功能与风险记录

日期：2026-08-10

## 需求 / 背景

- 用户要求实现 `Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_MoveTriggerListener.cs`（原文件仅有类注释与空 Start/Update）。
- 需求（来自脚本头注释）：可通过 `TriggerExecutor` 触发；提供 Vector2 `offset` 控制物体移动；OnGizmos 展示最终移动位置并包含移动方向线段；提供 `duration`（默认 0.1）与 `delay`（默认 0）参数。

## 影响文件与系统

- 新增实现：`Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_MoveTriggerListener.cs`（类名与文件名不变，未动 .meta）。
- 依赖的既有系统：
  - `Game.ItemEvent.TriggerExecutor` / `BaseItemExecutor`（`Assets/Game/Scripts/Level/MainLevel/Executor/`）：按 `items` 列表顺序或并行调用 `Execute()`，顺序模式依赖 `IsRunning`/`IsDone` 推进。
  - `Game.ItemEvent.BaseItemEventHandle`（`Assets/Game/Scripts/Level/MainLevel/Level_2/BaseItemEventHandle.cs`）：本脚本继承它。
  - DOTween（`DG.Tweening`）：参考 `ItemJumpToListener`、`ItemScaleListener` 的既有用法。
- 未修改任何其它脚本、prefab 或配置。

## 既有行为基线

- 实现前该脚本为空 MonoBehaviour，挂到 TriggerExecutor 的 items 列表会因类型不匹配无法序列化（不是 `BaseItemEventHandle`）。

## 新行为

- 继承 `BaseItemEventHandle`，可被 `TriggerExecutor` / `BaseItemExecutor` 收入 items 并触发。
- `Execute()`：延迟 `delay` 秒后，用 `duration` 秒将 `target`（为空时回退为自身 Transform）从触发瞬间位置平移 `offset`（仅 X/Y，Z 不变），线性缓动。
- 移动期间 `IsRunning = true`，完成或被中断时 `IsDone = true`，兼容顺序 / 并行两种执行模式。
- `ResetState()` 覆写：先停止未完成 Tween，再重置标志，供重复触发与关卡 Reset 使用。
- `OnDrawGizmos`：绿色线段（当前位置 → 当前位置 + offset）+ 落点线框球；仅使用本地序列化数据，编辑器安全。

## 数据 / 事件流

- 无存档、资源加载、UI、全局事件接入；仅本地 Transform 位移（DOTween）。

## 验证

- 已核对 `TriggerExecutor`、`BaseItemExecutor`、`BaseItemEventHandle` 源码，确认 `Execute`/`IsRunning`/`IsDone` 契约与顺序模式推进逻辑。
- 未做 Unity 编译验证与运行验证（本地无快速编译链路）：待编辑器内确认编译通过并挂接 TriggerExecutor 实测。

## 剩余风险 / 未验证缺口

- 编译未验证（uncertain）：DOTween `SetDelay` + `OnKill` 组合行为以既有同类脚本为参考，未在本工程实测。
- 多次触发基于触发瞬间位置累积位移，若关卡需要"回到固定终点"语义需另加字段（当前未实现，待需求确认）。
- 原生覆盖层 / 暂停期间 Tween 行为未验证（本脚本未接 ads/支付流程，风险低）。

## 回滚路径

- 将 `IWannerGame_MoveTriggerListener.cs` 恢复为原空 MonoBehaviour 模板即可，无其它文件耦合。

## 2026-08-11：BaseItemExecutor Parallel 漏执行修复

### 需求 / 背景

- `ILevel_003.prefab` 的 Trigger 配置为 `Mode: Parallel`，执行列表依次包含两个
  `IWannerGame_MoveTriggerListener` 和一个 `SwitchComponentListener`。运行时出现两个移动 Listener
  无法并行执行的问题。
- 静态调用链确认问题位于共享 `BaseItemExecutor.OnUpdate()`：旧 Parallel 分支在执行当前项前先递增
  `index`，导致 `items[0]` 永远不执行；其余项也只会每帧启动一个，不符合并行模式语义。

### 影响文件、资源与系统

- 修改 `Assets/Game/Scripts/Level/MainLevel/Executor/BaseItemExecutor.cs`。
- `Assets/Game/Prefabs/WannerLevel/ILevel_003.prefab` 仅作为受影响配置进行静态核对，本次未修改；该 prefab
  在任务开始前已有工作区改动，未覆盖其内容。
- 未修改 `IWannerGame_MoveTriggerListener.cs`、配置表、存档、资源地址、UI、Bear 全局事件、Analytics 或平台 SDK。

### 既有行为基线与修复后行为

- 修复前：Parallel 首帧把 `index` 从 0 增至 1 后只执行 `items[1]`；下一帧执行 `items[2]`，
  `items[0]` 被永久跳过。
- 修复后：Parallel 在首个执行器更新帧按列表顺序遍历全部有效 `runtimeItems`，同一帧调用每个 Listener 的
  `Execute()`，不会跳过第 0 项。
- 这里的“并行”是同一 Unity 帧内依次发起各 Listener，不是多线程执行；调用顺序仍与 `items` 序列化顺序一致。
- 本次不新增“等待全部并行项完成”的屏障，保持共享执行器原有的发起式完成边界；Sequence 仍依据当前项的
  `IsRunning` / `IsDone` 逐项推进。
- 原注释“同步执行直接过”已与拆分后的分支事实冲突，因此改为明确描述 Sequence 完成后推进，不改其它旧注释。

### 验证

- CodeGraph 与源码静态检查确认 `TriggerExecutor.OnTriggerExecute() -> BaseItemExecutor.Execute() -> OnUpdate()`
  调用链，以及旧索引递增发生在 `Execute()` 之前。
- 静态扫描 `Assets/Game` 下 7 个直接继承 `BaseItemExecutor` 的执行器和 132 个 prefab/scene 序列化组件；
  当前仅 `ILevel_003.prefab` 的该 `TriggerExecutor` 使用 `Mode: Parallel`。
- `git diff --check -- Assets/Game/Scripts/Level/MainLevel/Executor/BaseItemExecutor.cs` 通过，仅输出工作区
  LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`
  通过：`0 error / 101 warning`；warning 属于当前工程既有输出范围。

### 剩余风险 / 未验证缺口

- `uncertain`：尚未在 Unity PlayMode 实际进入 `ILevel_003` 验证两块目标同时开始移动、第三个组件同帧关闭，
  也未验证 Reset、重复进入 Trigger 和 Android/iOS 横屏运行表现。
- Parallel 现在会让过去被错误跳过的第 0 项真正执行；这是本次预期修复，但若未来其它 prefab 启用 Parallel，
  必须确认各 Listener 同帧启动时不存在顺序副作用。

### 回滚路径

- 回滚 `BaseItemExecutor.OnUpdate()` 的 Parallel 遍历分支即可恢复旧行为；无需修改 prefab、Listener、存档或配置。
