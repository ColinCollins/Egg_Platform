# ILevel039Ctrl 功能与风险记录

## 需求与背景

- 日期：2026-08-14。
- 本次将 `ILevel_039.prefab` 中六个主线 Portal Trigger 原本分散的 `TriggerExecutor`、Collider 开关、Portal 缩放、对象激活与地块移动配置，收口到 `ILevel039Ctrl` 统一控制。
- 原 `ILevel039Ctrl.cs` 顶部注释描述与当前关卡实际配置不一致；用户已明确该注释写错，因此本次以 prefab 中已经存在的实际引用、顺序和参数为实现依据，不沿用该注释。

## 原始行为基线

- 六个主线 Portal 各自通过 `OnTrigger2DHandle.onEnter` 调用本地 `TriggerExecutor.OnTriggerExecute`。
- 每次触发都会禁用当前 Trigger Collider 和 Portal Collider，并让当前 Portal 显示节点在 `0.1` 秒内缩放至 `0`。
- 前五步会激活下一个主线 Portal；地块移动与 Portal 缩放、下一 Portal 激活并行执行。
- 第五步的地块完成下移后才激活下方 `Portal (6)`；第六步不再激活下一个主线 Portal。
- 各 Trigger 上的旧 Executor 和 Listener 组件只在 UnityEvent 调用后运行，不会自行执行。

## 修改后的中央流程

1. 六个主线 Trigger 的 `onEnter` 统一调用根节点 `ILevel039Ctrl.TriggerNextStep()`。
2. 控制器使用 `_currentStepIndex` 每次消费一个步骤；流程不依赖 `OnEnable`，也没有第二个 Trigger 或全局事件参与计数。
3. 每步先禁用当前 Trigger Collider 与 Portal Collider，并按原平滑曲线把当前 Portal 显示节点缩放到 `0`。
4. 若配置了 `nextPortal`，立即激活下一主线 Portal。
5. 对 `moveTarget` 执行世界坐标偏移移动；移动延迟、时长和偏移均保留为单步序列化配置。
6. 若配置了 `activateAfterMove`，只在本步移动完整结束后激活目标；当前仅第五步使用，用于开启下方 `Portal (6)`。
7. 控制器销毁时按自身 Tween Id 清理尚未完成的缩放和移动 Tween，不影响其它对象的 Tween。

## 步骤配置

| 步骤 | 主线 Portal 位置 | 移动目标 | 世界坐标偏移 | 时长 | 延迟 | 额外完成动作 |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | x = -7.01 | Ground (4) | (0, -10) | 1 秒 | 2 秒 | 无 |
| 2 | x = -4.99 | Ground (5) | (0, -10) | 1 秒 | 2 秒 | 无 |
| 3 | x = -3.02 | Ground (6) | (0, -10) | 1 秒 | 2 秒 | 无 |
| 4 | x = -1 | Ground (7) | (0, -10) | 1 秒 | 2 秒 | 无 |
| 5 | x = 1 | Ground (8) | (0, -5) | 1 秒 | 2 秒 | 激活下方 Portal (6) |
| 6 | x = 3 | Ground (3) | (5, 0) | 0.1 秒 | 0 秒 | 无 |

所有步骤共用 `portalScaleDuration = 0.1` 秒。移动使用 `Ease.Linear`；Portal 消失使用与原 `AnimationCurve.EaseInOut(0, 0, 1, 1)` 等价的平滑曲线。

## 影响文件与系统

- `Assets/Game/Scripts/Level/IWannerLevel/Levels/ILevel039Ctrl.cs`
  - 新增六步顺序状态、公开 Trigger API、DOTween 表现与 Bear Logger 引用缺失告警。
- `Assets/Game/Prefabs/WannerLevel/ILevel_039.prefab`
  - 根节点新增并配置 `ILevel039Ctrl`。
  - 六个主线 Trigger UnityEvent 改为调用中央 API。
  - 保留旧 Executor 与 Listener 组件，便于对照和回滚；它们当前没有对应的 `onEnter` 调用入口。
- 未修改共享 `ActorCtrl`、`IWannerGame_Portal`、`TriggerExecutor` 或共享 Listener。

## 数据、资源与事件边界

- 本功能只读取 prefab 序列化引用和参数，不访问 DB、存档、RemoteConfig、资源加载或分析打点。
- 不新增 `PlayEvents` 或其它全局事件；入口仅为当前 prefab 内六个 `OnTrigger2DHandle.onEnter` 的本地 UnityEvent。
- 不使用 `OnEnable`、`Update`、`OnValidate`、`Reset`、`ExecuteAlways` 或编辑器预览生命周期。

## 风险评估

### 原行为一致性风险

- 中央控制器按调用次数而不是按 Trigger 身份选择步骤，因此 prefab 必须继续保证主线 Portal 按配置顺序触发。若运行时绕过激活顺序直接调用任意 Trigger，步骤仍会按下一索引消费。
- 多步移动允许并行，符合原来各 TriggerExecutor 独立启动 Tween 的行为；如果玩家能在前一步两秒延迟结束前快速触发后一步，多块地面会同时移动。
- 旧 Listener 组件仍保留但不再由六个 `onEnter` 调用；若后续在 Inspector 中重新增加旧回调，会造成中央流程与旧流程重复执行。

### 生命周期风险

- `_currentStepIndex` 只在控制器实例创建时初始化。当前未对同一实例提供 Reset API；如果未来 Reset/Replay 不重建本关 prefab，而只是重新启用同一根节点，已消费步骤不会自动归零。当前实际重置方式尚未通过 PlayMode 验证，标记为 `uncertain`。
- 根节点销毁会清理本控制器创建的 Tween；单纯禁用根节点时，`SetLink` 的默认行为是否满足本关暂停/恢复预期尚未做 PlayMode 验证，标记为 `uncertain`。

### 移动端与表现风险

- 逻辑使用常规 DOTween 游戏时间，没有逐帧分配和全局查找；Android/iOS 横屏下的实际碰撞时序、低帧率下连续触发、暂停恢复仍为 `uncertain`。
- Portal 缩放、下一 Portal 激活和地块移动的实际画面重叠尚未在 Unity Scene/Game 视图验收。

## 验证证据

- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo --verbosity:minimal`：通过，`0` 个错误、`106` 个工程既有警告。
- prefab 静态检查：中央组件定义 `1` 个，根组件引用 `1` 个，步骤条目 `6` 个，`TriggerNextStep` 绑定 `6` 个，旧 `OnTriggerExecute` 绑定 `0` 个。
- 三十个新增序列化对象引用均能在同一 prefab YAML 中找到对应本地对象定义。
- `git diff --check` 已对目标脚本和 prefab 执行；仅报告工作区换行符提示，无新增空白错误。
- 未执行 Unity PlayMode、Reset/Replay、连续六步实机流程或 Android/iOS 构建验证。

## 回滚路径

1. 将六个主线 Trigger 的 `onEnter` 目标恢复为各自原 `TriggerExecutor.OnTriggerExecute`，模式恢复为 `EventDefined`。
2. 从 `ILevel_039` 根节点移除 `ILevel039Ctrl` 组件及六步配置。
3. 删除 `ILevel039Ctrl.cs` 和本记录；旧 Executor 与 Listener 组件未删除，可直接恢复原分散流程。
