# GameVictoryPanel 打开粒子播放记录

## 需求背景

`GameVictoryPanel` 打开时需要播放 prefab 已绑定的 `openEffects`，每次打开只触发一次，不依赖 `Play On Awake` 自动播放。

## 影响范围

- `Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel.cs`
- `Assets/Game/UI/GameVictory/Resources/GameVictoryPanel.prefab`
- `Assets/Epic Toon FX/Prefabs/Environment/Confetti/Directional/ConfettiDirectionalGreenYellow.prefab`（只作为粒子源 prefab 的静态核对对象，本次未修改）

未新增资源、配置、存档字段、全局事件、分析打点、广告、IAP、RemoteConfig 或平台 SDK 链路。

## 原有行为基线

- 任务开始时，`GameVictoryPanel.cs` 已有序列化列表 `openEffects`，但 `OnOpen()` 没有调用粒子播放 API。
- 当前 `GameVictoryPanel.prefab` 已为 `openEffects` 绑定两个粒子实例。
- 两个实例都关闭了根与子粒子的 `Play On Awake`；根粒子的 `looping` 在胜利面板 prefab 上覆盖为关闭，两个子粒子在源 prefab 中也是非循环。
- 因为自动播放已关闭且没有显式播放入口，面板打开流程不会主动播放这些粒子。

## 新行为

- `GameVictoryPanel.OnOpen()` 在重置本轮打开状态后调用一次 `PlayOpenEffects()`。
- 每个非空粒子引用先通过 `Stop(...StopEmittingAndClear)` 清理可能残留的上次状态，再调用一次 `Play(true)`，同时播放其子粒子。
- 空列表或空引用会被安全跳过，不影响胜利面板其余打开流程。
- “只播放一次”由单次 `OnOpen()` 调用和 prefab 的非循环配置共同保证；未增加 `Update`、协程或重复定时触发。

## UI、数据与资源流

- UI 生命周期入口仍为 Bear UI 的 `GameVictoryPanel.OnOpen()`。
- 粒子对象仍来自 `GameVictoryPanel.prefab` 的现有序列化引用，不执行运行时加载或实例化。
- 胜利页按钮、动画、明信片星、奖励、下一关、重玩、存档和事件流程未改动。

## 验证

- 已通过 CodeGraph 核对 `GameVictoryPanel.OnOpen()` 生命周期与影响范围，未发现覆盖该面板的自动化测试。
- 已静态核对 `GameVictoryPanel.prefab` 中两个 `openEffects` 绑定，以及根/子粒子的非循环与 `Play On Awake` 关闭配置。
- 已执行目标 `git diff --check`，结果无空白错误；输出仅包含工作区既有的 LF/CRLF 转换提示。
- 已执行新增逻辑与目标脚本敏感项扫描；`PlayOpenEffects()` 只调用粒子 `Stop` / `Play`，未新增 DB、Save、RemoteConfig、实例化、全局事件或编辑器生命周期入口。目标脚本的其他敏感命中均为既有逻辑。
- 已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，结果为 0 error / 98 warnings。

## 剩余风险与未验证缺口

- 尚未在 Unity PlayMode 目视验证每次打开只播放一轮，以及快速关闭后重新打开时能从头播放。
- 尚未在 Android/iOS 横屏真机验证粒子层级、裁剪、帧耗和显示效果。
- 若后续 prefab 把任一参与播放的粒子改回循环，代码不会自动强制覆盖该序列化配置，需要同步复核本记录。

## 回滚路径

- 删除 `GameVictoryPanel.OnOpen()` 中的 `PlayOpenEffects()` 调用及该私有方法。
- 如需恢复自动播放，则还要由 prefab 明确恢复对应粒子的 `Play On Awake`；本次不建议混用自动播放与显式播放。
