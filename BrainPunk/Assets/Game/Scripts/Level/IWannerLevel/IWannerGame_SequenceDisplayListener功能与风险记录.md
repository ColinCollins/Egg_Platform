# IWannerGame_SequenceDisplayListener 功能与风险记录

## 需求与原始基线

- IWanner 关卡已有 `TriggerExecutor/BaseItemExecutor` 与移动 Listener，但没有一个 Listener 能维护
  `List<GameObject>` 并按可配置间隔依次显示。
- 需求为按列表顺序显示目标；`delay` 为 0 时直接显示全部目标。

## 影响范围

- 新增：`IWannerGame_SequenceDisplayListener.cs` 及其 Unity `.meta`。
- 复用：`Game.ItemEvent.BaseItemEventHandle`、`TriggerExecutor/BaseItemExecutor` 与 UniTask 取消机制。
- 未修改关卡 prefab、配置、存档、资源加载、Bear 全局事件、Analytics、广告或平台 SDK。

## 修改后行为与事件流

1. `Awake()` 先隐藏列表中的全部有效目标，空引用安全跳过。
2. `BaseItemExecutor` 每轮执行前调用 `ResetState()`，Listener 会取消旧流程、重新隐藏目标并清空完成状态。
3. `Execute()` 再次从头开始：第一个有效目标立即显示，后续有效目标之间按 `delay` 秒等待。
4. `delay <= 0` 时当前调用内直接显示全部有效目标，并立即设置 `IsDone = true`，允许 Sequence Executor 推进。
5. 重复调用、Reset、组件禁用或对象销毁都会取消未完成等待；等待使用游戏时间，玩法暂停时序列同步暂停。

## 验证

- `Assembly-CSharp.csproj` 定向构建：`0 error / 101 warning`。
- `Assembly-CSharp-Editor.csproj` 定向构建：`0 error / 4 warning`。
- 静态确认脚本继承 `BaseItemEventHandle`，能被现有 Executor 收集；没有新增逐帧 Update、实例化、DB 或编辑器生命周期。

## 剩余风险与未验证项

- `uncertain`：尚未在 Unity PlayMode 验证 `delay = 0`、正数、空列表、包含 null、重复触发、暂停/恢复和关卡销毁。
- Listener 所在 GameObject 不应放入目标列表，否则 `Awake()` 隐藏自身后无法接收后续触发。
- 外部脚本若在序列运行期间修改目标 Active 状态，当前 Listener 不会争夺所有权，只会在轮到该目标时执行一次
  `SetActive(true)`。
- Android/iOS 没有平台专属代码，但横屏关卡中的实际显示节奏与性能仍未真机验证。

## 回滚路径

- 从使用方 Executor 列表移除该 Listener，并删除脚本与 `.meta` 即可；无存档、配置或资源迁移。

## 2026-09-07：支持反向显示

- 需求：新增 Inspector 布尔字段 `isReverse`，开启后从 `targets` 尾到头依次显示。
- 原行为：间隔显示与立即显示均按列表从头到尾遍历。新行为：默认 `false` 保持原顺序，`true` 使用末尾索引和负向步长；不翻转或复制序列化列表。
- 影响文件：本目录 `IWannerGame_SequenceDisplayListener.cs`、本记录与 `DevelopMD/已有功能设计与使用记录.md`；未修改 Prefab 或共享 Executor。
- 事件流：Executor → Execute → 按当前方向立即显示全部目标或启动间隔显示。方向在各次流程开始时确定，空引用仍跳过，有效目标间仍等待 delay；原隐藏、取消、完成状态处理不变。
- 修正 Execute 原注释中“从第一个目标重新开始”的描述，明确为当前方向的第一个有效目标，因为新增反向模式后不再固定从列表首部开始。
- 验证：`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:q` 通过（0 错误、121 警告）；静态核对正反向边界和两个显示路径。
- 风险/缺口：PlayMode 下正反顺序、空项、单目标、delay=0、重复调用、暂停与禁用取消，以及 Android/iOS 设备表现未实测，均为 `uncertain`。本次没有新增逐帧逻辑或列表分配。
- 手工验证：列表 A/B/C 默认应按 A→B→C 显示，开启后 C→B→A；包含空项时跳过且不额外等待；delay=0 同帧按所选方向激活全部目标；重复执行仍从所选方向起点开始。
- 回滚：删除 isReverse 字段，将两个显示循环恢复从索引 0 递增，撤回本次文档段落；保留其他已有改动，无存档或资源迁移。
