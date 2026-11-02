# PlayCtrl GameReplayEvent 重播流程记录

## 需求背景

2026-06-30 需求：补充完善 `PlayCtrl.OnGameReplayEvent`。当前胜利面板存在“重玩当前关卡”按钮，而成功流程会先创建胜利面板，再由 `LevelCtrl.Victory()` 推进 `DB.GameData.CurrentLevel` 到下一关；因此胜利后如果继续走普通 `GameResetEvent`，会重建已推进后的关卡，不能稳定回到刚通关关卡。

## 影响文件与系统

- `Assets/Game/Scripts/Play/PlayCtrl.cs`
  - 监听并处理 `GameReplayEvent`。
  - 使用 `Level.CurrentLevelState.LevelId` 优先确定重播目标关卡。
  - 新增重播专用重建流程，避免把胜利后重播记成失败结束。
- `Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel.cs`
  - 胜利面板 replay 按钮从派发 `GameResetEvent` 改为派发 `GameReplayEvent`。
  - replay 按钮在每次 `OnOpen()` 时先退订再订阅 `ReplayCurrentLevel(...)`，避免 UI 实例复用后回调丢失或重复叠加。
- `Assets/Game/Scripts/Play/PlayEvents.cs`
  - 当前已有 `GameReplayEvent`，语义为“重玩当前关卡”。

未修改 prefab、Luban 配置、RemoteConfig、存档字段、资源路径、广告 SDK、IAP、平台 SDK 或全局事件定义。

## 修改前行为基线

- `PlayCtrl_Success.ShowVictory(...)` 会先调用 `GameVictoryPanel.Create(...)`，再调用 `owner.Level.Victory()`。
- `LevelCtrl.Victory()` 会在可进入下一关时把 `DB.GameData.CurrentLevel` 推进到下一关，并保存。
- 胜利面板 `ReplayCurrentLevel(...)` 原本关闭自身后派发 `GameResetEvent(GameResetType.Manually)`。
- `PlayCtrl.OnGameResetEvent(...)` 会记录 reset、上报一次 failed end、上报 replay start，并调用 `ResetGame()` 按当前 `Level.CurrentLevelData.Path` 重建关卡。
- 已存在的 `OnGameReplayEvent(...)` 只做 `CurrentLevel - 1` 后再派发 `GameResetEvent`，没有被胜利面板使用，也会复用失败重试路径。

## 修改后行为

- 胜利面板 replay 按钮关闭自身后派发 `GameReplayEvent`。
- 胜利面板每次 `OnOpen()` 都会刷新 replay 按钮绑定，并在 `ResetRuntimeState()` 中重置 `_isReplayCurrentLevel`，避免面板复用时旧防重入状态拦截新一轮点击。
- `PlayCtrl.OnGameReplayEvent(...)` 优先使用 `Level.CurrentLevelState.LevelId` 作为重播目标；这能覆盖胜利后 `DB.GameData.CurrentLevel` 已推进、但运行时状态仍记录刚通关关卡的情况。
- 目标关卡会通过 `LevelSort` 和 `LevelData` 做存在性校验；缺失时只输出 warning 并跳过，避免使用空路径重建。
- 当目标和当前 `LevelRuntimeState` 不一致时，先调用 `StartLevel(...)` 重建该关卡的运行时统计；随后统一记录一次主动重开并重置本局时间，确保 replay 重建时仍按 reset 语义跳过进门动画。
- 重播流程只上报 `LevelStartTypeReplay`，不额外上报 `LevelEndTypeFailed`，避免胜利后重玩被统计成失败。
- 关卡内普通 Reset 仍继续走 `GameResetEvent -> OnGameResetEvent -> ResetGame()`，失败结束打点、失败插屏和原有淡入节奏保持不变。

## 数据、UI、事件、广告与平台流

- 数据：`DB.GameData.CurrentLevel` 会在重播前设置回目标关卡；不新增存档字段。
- UI：胜利面板 replay 关闭自身后由 `PlayCtrl` 关闭 Popup 层并重建当前 GameplayPanel；仍复用 `GamePlayPanelFadeInEvent`。
- 事件：不新增全局事件，复用已有 `GameReplayEvent`；普通 Reset 事件语义不变。
- 广告：胜利后重播不触发 `InGameFailed` 插屏；普通 Reset/失败重试仍保留原入口。
- Analytics：胜利后重播只补一次 replay start；普通 Reset 仍保留 failed end + replay start。
- 平台：未修改 Android/iOS 专用代码，移动端横屏触控仍需 PlayMode 或真机 smoke 验证。

## 验证记录

- 静态检查确认 `GameVictoryPanel.ReplayCurrentLevel(...)` 现在派发 `GameReplayEvent`。
- 静态检查确认 `GameVictoryPanel.OnOpen()` 会调用 `BindReplayButton()`，且 `BindReplayButton()` 对 `resetBtn.OnClick` 使用先 `-=` 后 `+=` 的幂等绑定。
- 静态检查确认 `OnGameReplayEvent(...)` 不再派发 `GameResetEvent`，不再复用失败结束打点路径。
- 静态检查确认 `OnGameResetEvent(...)` 和 `ResetGame()` 保持原有普通重开路径。
- `git diff --check -- Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel.cs Assets/Game/Scripts/Play/PlayCtrl.cs DevelopMD/PlayCtrl_GameReplay_Record.md DevelopMD/已有功能设计与使用记录.md` 已通过，仅有 Git 工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error / 98 warnings。
- 完整项目引用链构建曾因 Unity/VS Code 相关 `dotnet` 进程占用 `Temp\obj\*.dll` 失败；本次未终止外部进程。

## 剩余风险与未验证项

- 尚未运行 Unity PlayMode，需验证胜利后点击 replay 是否回到刚通关关卡，而不是下一关。
- 尚未验证 replay 后 GamePlayPanel 黑场淡入、按钮状态、公告、免广告弹窗策略是否符合预期。
- 尚未验证 Android/iOS 横屏触控、胜利面板 replay 快速点击防重入和非胜利场景派发 `GameReplayEvent` 的兜底表现。

## 回滚路径

如需回滚，恢复 `GameVictoryPanel.ReplayCurrentLevel(...)` 派发 `GameResetEvent(GameResetType.Manually)`，并把 `PlayCtrl.OnGameReplayEvent(...)` 恢复为旧的 `CurrentLevel - 1` 后派发 reset 的实现；随后重新验证胜利后 replay、普通 Reset、失败重试和下一关流程。
