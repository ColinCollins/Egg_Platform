# IWannerGame_SequenceMoveListener 功能与风险记录

日期：2026-08-18

## 需求 / 背景

- 用户要求新增 `IWannerGame_SequenceMoveListener`，整体作用与既有 `IWannerGame_MoveTriggerListener` 类似，
  但把单个目标扩展为列表并逐个移动。
- `delay` 只作用于整组动作开始之前；`interval` 作用于相邻物体的移动动作之间。

## 影响文件与系统

- 新增脚本：`Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_SequenceMoveListener.cs`。
- 新增记录：`Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_SequenceMoveListener功能与风险记录.md`。
- 更新通用记录：`DevelopMD/已有功能设计与使用记录.md`。
- 复用既有系统：
  - `Game.ItemEvent.BaseItemEventHandle` 与 `BaseItemExecutor` 的 `Execute`、`IsRunning`、`IsDone`、`ResetState` 契约；
  - DOTween `Sequence`、`DOMove`、`AppendInterval`；
  - Bear Logger，仅在没有任何有效目标时输出警告。
- 本次未修改任何现有脚本、prefab、场景、存档、配置、资源、UI、打点或平台 SDK。

## 既有行为基线

- `IWannerGame_MoveTriggerListener` 只支持一个 `Transform target`：触发时捕获 `target.position + offset`，
  等待自己的 `delay` 后用 `duration` 线性移动到终点。
- `IWannerGame_SequenceDisplayListener` 已提供列表顺序处理范例：空引用跳过，Reset 会取消旧流程，
  最终通过 `IsRunning/IsDone` 让顺序执行器继续推进；但它只控制显隐，不提供移动能力。
- 本次新增前没有可配置多个 Transform、按完整移动动作顺序推进且提供整体 `delay` 与动作间 `interval` 的 IWanner Listener。

## 新行为

- `targets` 按 Inspector 列表顺序执行，空引用不占用移动时间或间隔时间。
- 每次执行先整体等待一次 `delay`；第一个有效目标不再额外等待 `interval`。
- 每个有效目标从触发瞬间捕获的世界坐标移动到 `position + offset`，所有目标共用 `offset` 与 `duration`，
  只改变 X/Y，保留 Z，并使用线性缓动。
- 前一个有效目标完整移动结束后等待 `interval`，再启动下一个有效目标；最后一个目标完成后不追加尾部间隔。
- 有效目标数为 `N` 时，正常总耗时为
  `delay + N × duration + max(0, N - 1) × interval`。
- `duration <= 0` 时，该目标轮次通过 Sequence 回调立即写入终点，仍保留配置的整体 `delay` 和目标间 `interval`。
- 重复执行会停止旧 Sequence，并按再次触发瞬间的位置重新捕获全部有效目标终点；因此位移可累积。
- `ResetState()` 与销毁会停止未完成序列，但不会恢复已产生的位移；没有有效目标时使用 Bear Logger 警告并立即完成，
  避免 `BaseItemExecutor` 的顺序链卡住。
- `OnDrawGizmos` 为每个有效目标绘制绿色方向线和终点线框球，只读取本地序列化数据。

## 数据 / 资源 / 事件流

1. `TriggerExecutor` / `BaseItemExecutor` 调用 `Execute()`。
2. Listener 停止旧 Sequence，捕获有效目标与各自终点，设置运行状态。
3. DOTween 先等待整体 `delay`，再按“移动 → `interval` → 移动”的顺序推进。
4. Sequence 完成或被中断时，Listener 结束运行并设置 `IsDone = true`；执行器随后可继续推进。

本链路只修改场景中已绑定 Transform 的世界坐标，不接入存档、配置、资源加载、全局事件、UI 或平台原生能力。

## 验证

- 已通过 CodeGraph 和源码核对：
  - `IWannerGame_MoveTriggerListener` 的相对位移、线性缓动、Tween 清理和执行状态基线；
  - `IWannerGame_SequenceDisplayListener` 的列表顺序、空引用跳过与取消边界；
  - `BaseItemExecutor` 顺序模式依赖当前 Listener 的 `IsRunning/IsDone` 推进。
- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false
  /p:BuildInParallel=false` 通过：`0 error / 109 warning`；warning 属于当前工程既有输出范围。
- 定向 `git diff --check` 通过；脚本静态扫描未发现 `OnValidate`、`ExecuteAlways`、`ExecuteInEditMode`、
  `Reset()`、`DB`、`Save`、`RemoteConfig`、`Instance`、`Instantiate`、`Debug.Log` 或逐帧 `Update()` 接入。

## 剩余风险 / 未验证缺口

- `uncertain`：尚未在 Unity PlayMode 实测整体 `delay`、动作间 `interval`、零时长、空引用、重复触发和 Reset 表现。
- 各目标终点在 `Execute()` 时统一捕获；若目标尚未轮到移动时被其它系统改变位置，它仍会朝原捕获终点移动。
- 相同 Transform 重复配置时，各条目会复用 `Execute()` 时捕获的同一终点，后续重复动作仍会占用时长；
  这不是有意义的配置，关卡绑定时应避免重复条目。
- Reset / 中断只保留停止瞬间的位置，不自动回到关卡初始坐标；需要还原时应由关卡既有 Reset 流程负责。
- DOTween 使用 Unity PlayerLoop 与游戏时间推进；原生覆盖层导致 PlayerLoop 停止时，`delay`、移动和 `interval` 都不会继续推进。
- `uncertain`：Android / iOS 横屏真机的时间、销毁与场景切换表现尚未验证。

## 回滚路径

- 删除新增的 `IWannerGame_SequenceMoveListener.cs`、对应 `.meta`、本记录及其 `.meta`，并移除
  `DevelopMD/已有功能设计与使用记录.md` 的对应条目即可。
- 当前没有 prefab 或场景绑定需要反向迁移。

## 2026-09-01：补充 Parallel 统一移动模式

### 需求 / 背景

- 用户要求在既有多目标顺序移动基础上增加 `Parallel` 模式，使多个有效目标在统一时间一起移动。
- 默认仍需保持 `Sequence`，避免没有主动选择新模式的组件改变原有顺序时序。

### 影响文件与系统

- 修改脚本：`Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_SequenceMoveListener.cs`。
- 更新本记录与 `DevelopMD/已有功能设计与使用记录.md`。
- 复用 `Game.ItemEvent.ExecuteMode` 的 `Sequence / Parallel` 枚举，以及既有 DOTween `Sequence`、`Append`、
  `Join`、`InsertCallback` 和 Listener 的完成状态契约。
- 定向搜索未发现该 Listener 当前被 `.prefab` 或 `.unity` 资源引用；本次未修改 prefab、场景、存档、配置、
  资源加载、UI、事件、打点或平台 SDK。

### 既有行为基线

- 修改前只有顺序移动：统一等待 `delay` 后，按有效 `targets` 列表顺序执行，每个目标移动 `duration`，
  相邻动作之间等待 `interval`。
- 原总耗时为 `delay + N × duration + max(0, N - 1) × interval`；空引用不占时长或间隔。

### 新行为与时间流

- Inspector 新增 `mode`，默认显式设为 `ExecuteMode.Sequence`；枚举的零值同样是 `Sequence`。
- `Sequence` 分支保持原有行为和总耗时公式不变。
- `Parallel` 分支在统一 `delay` 结束后，同一时间点启动所有有效目标的线性 `DOMove`；所有目标共享
  `offset` 与 `duration`，完成最晚目标后统一结束，因此正常总耗时为 `delay + duration`。
- `Parallel` 明确忽略 `interval`，避免列表数量改变统一起点；`duration <= 0` 时使用时间点为 0 的
  `InsertCallback`，在统一 `delay` 后同一轮 Sequence 更新中写入全部有效目标终点。
- 两种模式仍在 `Execute()` 时统一捕获终点；重复执行、Reset、销毁、空目标和 Z 坐标保持规则不变。

### 验证

- 已核对项目现有 `BaseItemExecutor.ExecuteMode` 定义：枚举顺序为 `Sequence = 0`、`Parallel = 1`。
- 已核对项目现有 DOTween 调用证据：其它运行时代码已使用 `Sequence.Join` 和 `Sequence.InsertCallback`。
- 定向资源搜索未发现该 Listener 的 prefab / scene 绑定。
- 定向 `git diff --check` 通过；脚本静态扫描未引入 `OnValidate`、`ExecuteAlways`、`ExecuteInEditMode`、
  `Reset()`、`DB`、`Save`、`RemoteConfig`、`Instance`、`Instantiate`、`Debug.Log` 或逐帧 `Update()`。
- 完整 `Assembly-CSharp.csproj` 构建未进入本次源码的有效编译阶段：当前 `Temp/bin/Debug` 缺少约 200 个
  项目依赖 DLL，`dotnet build` 以 `CS0006` 失败；该环境缺口不构成本次脚本编译通过证据。

### 剩余风险 / 未验证缺口

- `uncertain`：尚未在 Unity PlayMode 实测 `Sequence / Parallel` 下的 delay、duration、interval、零时长、
  空引用、重复触发、Reset 和目标中途销毁表现。
- `uncertain`：尚未完成 Unity Editor 编译及 Android / iOS 横屏真机验证。
- Parallel 仍按触发瞬间捕获各自终点；移动期间其它系统同时写同一 Transform 时，最终位置竞争需由关卡侧避免。
- 原生覆盖层停止 Unity PlayerLoop 时，统一 delay 和并行动画都不会继续推进。

### 回滚路径

- 从脚本移除 `mode` 字段和 Parallel 分支，恢复无条件 `AppendInterval / Append` 即可回到原顺序行为。
- 本次没有 prefab / scene 迁移；回滚无需处理存档、配置、资源或事件数据。
