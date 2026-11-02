# LevelRuntimeState 功能说明

## 需求与背景

- 2026-07-16：整理 `LevelRuntimeState` 中含义模糊的 `LevelReady` 状态，并补充运行时状态、尝试切换和统计字段注释。
- 本次采用最小调整，不拆分现有 DB 统计写入，不改变重开、失败、成功、提示和 Analytics 的既有计数规则。

## 影响文件与系统

- `Assets/Game/Scripts/Play/LevelRuntimeState.cs`
  - 使用 `IsAttemptTransitioning` 表达关卡尝试正在异步重建。
  - 新增 `BeginAttemptTransition()` 与 `CompleteAttemptTransition()` 生命周期边界。
  - 修正与当前代码事实冲突的字段、时间单位和统计方法注释。
- `Assets/Game/Scripts/Play/PlayCtrl.cs`
  - 普通重开、失败重试和胜利后 replay 在重建场景前统一开始尝试切换。
- `Assets/Game/Scripts/Play/PlayCtrl_Nodes/PlayCtrl_Playing.cs`
  - 重新进入 Playing 后结束尝试切换。
  - 尝试切换期间不派发 `ShowHintBubbleEvent`。

未修改 prefab、配置表、资源路径、存档字段、全局事件定义、广告、IAP、RemoteConfig 或平台 SDK。

## 原行为基线

- 当前工作区原有 `LevelReady` 在 `StartLevel()` 和 `PlayCtrl_Playing.OnEnter()` 中设为 `true`，在普通 `GameResetEvent` 中设为 `false`。
- `LevelReady` 实际只被提示气泡逻辑读取，用于避免异步重建场景期间再次弹出气泡；名称无法说明这一调用边界。
- 胜利后 replay 的 `PrepareReplayRuntimeState()` 没有显式关闭该状态。虽然 replay 发起时通常已离开 Playing，但状态语义不完整。
- `GetCurrentAttemptTimeSeconds()` 与 `GetCurrentLevelTimeSeconds()` 的历史方法名包含 Seconds，实际返回值为毫秒；本次保留方法名以避免扩大兼容范围，仅补充事实注释。

## 调整后行为

- `StartLevel()` 将当前状态初始化为“尝试切换中”，直到场景创建完成并重新进入 Playing。
- 普通重开、失败重试和胜利后 replay 均在场景异步重建前调用 `BeginAttemptTransition()`。
- `PlayCtrl_Playing.OnEnter()` 调用 `CompleteAttemptTransition()`；暂停恢复时重复调用是幂等的，并且不会重置本局时间。
- `UpdateGuideBubble()` 在 `IsAttemptTransitioning` 为 `true` 时直接返回，避免旧场景在重建窗口再次弹出提示气泡。
- 截至 2026-07-16，`RecordReset()`、`RecordFail()`、`ResetAttemptPlayTime()`、DB 保存、Level Start/End 打点和提示入口逻辑保持当时的原调用顺序；2026-08-06 的 Replay 计数口径调整见下方新增章节。

## 数据、存档、事件与平台流

- 运行时数据流：Reset/Fail/Replay 入口 → `BeginAttemptTransition()` → 异步重建场景 → Playing `OnEnter()` → `CompleteAttemptTransition()`。
- 提示事件流：`PlayCtrl_Playing.UpdateGuideBubble()` 只在非切换期检查次数/时间门槛并派发既有 `ShowHintBubbleEvent`。
- 存档与 Analytics：截至 2026-07-16，本节对应改动不改变 `LevelPlayCount`、`LevelReviveCount`、`LevelPlayDuration`、`LevelSuccessCount` 的写入，也不改变 `LevelStartEvent` / `LevelEndEvent` 参数；当前口径以 2026-08-06 新增章节为准。
- 平台：未触碰 Android/iOS 条件分支；移动端横屏表现仍需真机验证。

## 验证记录

- `rg` 引用扫描确认旧的 `LevelReady`、`EnsureLevelReady`、`CacnelLevelReady` 已无 C# 引用。
- `git diff --check` 通过，仅有工作区既有 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：0 error / 98 warnings。

## 剩余风险与未验证项

- 尚未运行 Unity PlayMode；需验证普通重开、失败重试、胜利后 replay、暂停恢复以及气泡临界帧。
- 尚未执行 Android/iOS 横屏真机 smoke。
- `LevelRuntimeState` 当前仍混合运行时状态和 DB 统计写入；本次未做职责拆分，以避免同时扩大存档与打点回归范围。
- `TotalPlayTimeSeconds` 与 `CurrentLevelTimeSeconds` 当前仍同步累计，后续收敛前需要先确认 Analytics 和潜在反射调用边界。

## 回滚路径

- 将 `IsAttemptTransitioning` 恢复为 `LevelReady`，恢复 `EnsureLevelReady()` / `CacnelLevelReady()` 及原调用点。
- 移除 `PrepareReplayRuntimeState()` 中新增的尝试切换标记，并恢复 `UpdateGuideBubble()` 对 `LevelReady` 的判断。
- 回滚后重新验证普通 Reset、失败重试、胜利后 replay 和提示气泡显示时机。

## 2026-08-06 Replay 防重与 playCount 计数口径

### 需求与原行为基线

- 主线普通 Reset 和胜利 Replay 缺少控制器级防重门禁；异步重建期间若重复收到事件，存在重复累计和重复发送 `level_end` / `level_start` 的风险。
- 原 `RecordReset()` / `RecordFail()` 会同时增加运行态次数和 `DB.GameData.LevelReviveCount`；主线 `level_start.play_count` 使用 `LevelPlayCount + LevelReviveCount`。
- 原 DrawGame 首次进入增加 `LevelPlayCount`，Replay 增加 `LevelReviveCount`，开始事件同样发送二者之和；胜利 Replay 的历史 revive 不对应任何一条新的 `level_end.revive`。

### 调整后行为与数据流

- 主线 `OnGameResetEvent()` 和 `OnGameReplayEvent()` 在处理业务前检查既有 `IsAttemptTransitioning`；首次请求先进入切换态，后续重复事件直接忽略，不重复调用广告策略、存档或打点。
- `RecordReset()` / `RecordFail()` 继续维护当前周期运行态计数并写入 `DB.GameData.LevelReviveCount`，保留既有历史数据；Analytics 不再使用该历史字典计算 `level_start.play_count` 或灯泡事件 revive。
- 每次发送 `level_start` 都增加 `LevelPlayCount`：新关由 `StartLevel()` 增加，失败或手动 Reset 以及 VictoryPanel Replay 均在开始事件前增加一次。主线 `level_start.play_count` 只读取 `LevelPlayCount`。
- Reset 调用 `RecordReset()` / `RecordFail()` 后先发送 `level_end(fail)`，再通过 `ResetDataPreservingRevive()` 清理本次计时、`HasClickTips` 与 `HasShownGuideBubble`，保留当前关卡会话的 `HasTips` 及 `ResetCount + FailCount`，随后发送新的 `level_start` 并重建场景。
- VictoryPanel Replay 不再调用 `RecordReset()`，不会增加运行态或历史 revive；上一轮成功 `level_end` 已完成并清理周期计数，按钮只通过 `TryToAddPlayCount()` 记录新的开始。
- 业务操作上 Replay 只指 VictoryPanel 派发的 `GameReplayEvent`；打点字段 `start_type` 与操作来源解耦，所有 `level_start` 都按当前关卡是否存在于 `PassedLevels` 判定：已通关为 `replay`，未通关为 `play`。
- `GameData.LevelReviveCount` 存档字段及写入逻辑继续保留，不执行数据清理或迁移；它与当前打点字段的数据来源明确解耦。
- `ResetData()` 清理时长、revive 与尝试级提示显示状态；当前关卡会话的答案资格由离关或重新进入边界清理，不参与打点周期清零。成功 `level_end` 及其同步派生打点完成后仍调用该方法，VictoryPanel Replay 在重建前显式清理答案资格后继续幂等调用。

### 影响、验证与回滚

- 本次修正影响 `PlayCtrl.cs`、`DrawPlayCtrl.cs` 及对应本地功能记录；`LevelRuntimeState.cs` 的原有计数和存档方法不变，也不修改 prefab、配置、资源、事件名、SDK 接口或平台分支。
- 静态引用目标：Reset 路径保留 `RecordReset()` / `RecordFail()`，依次调用结束打点、仅 Reset 使用的 `ResetDataPreservingRevive()`、`TryToAddPlayCount()` 和开始打点；VictoryPanel Replay 路径不记录 revive，继续使用原 `ResetData()` 并在开始事件前增加一次 `LevelPlayCount`。
- 最终 `git diff --check` 通过；`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过，结果为 `0 error / 101 warning`，warning 为工程既有项。
- `uncertain`：尚未在 Unity PlayMode、真实 SDK 后台、Android/iOS 验证快速连点、失败自动重试、胜利 Replay、退出与切关的实际事件去重和值。
- 回滚本次修正时移除 `ResetDataPreservingRevive()`，恢复 Reset 路径直接调用原 `ResetData()`，并按届时协议恢复事件顺序；历史存档字段无需迁移。

## 2026-08-19：Reset 重建期间延后 GamePlayPanel tips 检测

### 需求、原行为与修正后行为

- 需求：玩家点击主线 `GamePlayPanel` 的 Reset 后，不应在关卡仍处于重建窗口时突然弹出 tips 气泡；应等 Reset 重建完成、重新进入 Playing 后再判断是否需要显示。
- 原行为：`PlayCtrl.OnGameResetEvent()` 已先调用 `BeginAttemptTransition()`，随后记录 Reset/Fail 次数并清理本次提示状态；但 `PlayCtrl_Playing.OnUpdate()` 的 `GamePlayPanelSwitchTipsEvent(true)` 门槛检测未读取 `IsAttemptTransitioning`。因此 Reset 次数刚达到远端门槛时，旧场景仍在重建窗口就可能派发显示事件。
- 修正后：`PlayCtrl_Playing.OnUpdate()` 继续按原顺序执行 `LevelRuntimeState.Tick(Time.deltaTime)`，随后在 `IsAttemptTransitioning` 为 `true` 时停止本帧 tips 检测；`PlayCtrl_Playing.OnEnter()` 调用 `CompleteAttemptTransition()` 后，下一次更新才重新检查首次提示引导及 GamePlayPanel tips 的时间/次数门槛。
- `UpdateGuideBubble()` 原有切换态门禁保持不变；本次没有修改气泡动画、GamePlayPanel prefab、远端门槛取值或 tips 点击行为。

### 数据、事件、验证与风险

- Reset/Fail/Replay 的 `level_start`、`level_end`、revive、playCount、`BeginAttemptTransition()`、异步重建和 `CompleteAttemptTransition()` 顺序保持不变；切换期间仍继续累计现有尝试时间，避免改变打点时长口径。
- 继续复用既有 `GamePlayPanelSwitchTipsEvent`，没有新增全局事件、DB 字段、保存、资源加载、广告、IAP、RemoteConfig 或平台 SDK 调用。
- `dotnet build .\Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo /clp:ErrorsOnly`：通过，`0 error / 106 warning`；warning 未在本任务逐项归因。
- `git diff --check` 通过；目标调用链静态复核确认 Reset 入口先开始切换、Playing 重新进入时再完成切换。`uncertain`：尚未在 Unity PlayMode 验证 Reset 门槛临界帧、失败自动重试、重建完成后立即显示、已用 tips 不再显示，以及 Android/iOS 横屏表现。
- 回滚路径：移除 `state.Tick(Time.deltaTime)` 后新增的 `state.IsAttemptTransitioning` 提前返回，即恢复重建窗口内也执行首次提示引导和 GamePlayPanel tips 门槛检测；无需迁移 prefab、配置、存档或资源。

## 2026-08-26：主线同关 Reset 保留已获得答案

### 需求、原行为与修改后行为

- 需求：玩家在主线当前关已经通过金币、广告或首次免费流程获得答案后，同关主动 Reset 或失败重试不得再次收费；进入 ChoiceLevel、VictoryPanel Replay 或以后重新进入主线关卡时才清除资格。
- 原行为：`UseTipsEvent` 把 `LevelRuntimeState.HasTips` 设为 `true`，但 Reset 共用的 `ResetDataPreservingRevive()` 会立即清为 `false`，新尝试再次点击 Tips 会回到 Normal 购买弹窗。
- 修改后：`ResetDataPreservingRevive()` 只清理尝试计时、`HasClickTips` 和 `HasShownGuideBubble`，保留 `HasTips`；新关实例再次点击 Tips 时，`GameTipsPopup.Create()` 直接选择 `GameTipsPopup_3` 展示当前关答案。
- 成功结算只清理打点周期与尝试级状态，`HasTips` 保留到玩家选择下一步；VictoryPanel Replay 在开始重建前显式清理，`StartLevel()` 在新的进入游戏边界清理，`PlayCtrl.ExitToLevelSelection()` 及主线末关进入 ChoiceLevel 前也显式清理。

### 数据、事件、打点与验证边界

- `HasTips` 仍是纯运行时字段，不新增 DB、Save、Luban、RemoteConfig 或 PlayerPrefs；应用退出或重新进入关卡不会持久继承答案。
- Reset/Fail 的 `level_end(fail)`、revive、`LevelPlayCount`、新 `level_start`、插屏请求、场景销毁和重建顺序均未改变；只调整提示资格的清理边界。
- `HasClickTips` 与 `HasShownGuideBubble` 仍按每次尝试清理；已保留 `HasTips` 时 `PlayCtrl_Playing` 不再派发 Tips Warning，角色气泡仍按原独立配置判断。
- `git diff --check` 通过，仅输出工作区换行转换提示；首次无还原定向构建因缺少 `project.assets.json`，还原后的无依赖构建因 `Temp/bin` 缺少 Unity 依赖 DLL 而被环境阻断。完整构建补齐依赖后通过（`0 error / 1138 warning`），最终代码的增量定向构建再次通过（`0 error / 120 warning`）。
- `uncertain`：Unity PlayMode 的金币/广告/首次免费三种获取方式、主动 Reset、失败重试、ChoiceLevel、Victory Replay、重新进关及 Android/iOS 横屏仍需手工验证。

### 回滚路径

- 把 `HasTips=false` 放回 `ResetDataPreservingRevive()`，删除 `PlayCtrl` 在 ChoiceLevel 与 VictoryPanel Replay 边界新增的清理语句，并同步恢复本文与生命周期约定；无需迁移存档或资源。

## 2026-09-03：主线提示入口循环状态

- 背景：主线提示入口原先用 `HasClickTips=true` 永久阻止当前尝试重复派发；UI 自动隐藏后没有记录完成时刻，无法实现“完全消失后再等待配置时长”的循环。
- `HasClickTips` 现表示提示入口气泡正在展示；新增纯运行时的 `HasShownTipsWarning` 和 `TipsWarningHiddenAtSeconds`，分别区分当前尝试是否完成过首次展示，以及上次完全消失时的尝试累计秒数。
- `SwitchClickTips(true)` 锁存本次尝试已展示；收到既有 `GamePlayPanelSwitchTipsEvent(false)` 时，`SwitchClickTips(false)` 记录 `CurrentAttemptTimeSeconds`。下一轮由 `PlayCtrl_Playing` 使用该时间差和现有 `showTipsTimeLimit` 判断。
- `StartLevel()`、`ResetAttemptPlayTime()`、`ResetDataPreservingRevive()` 和经其调用的 `ResetData()` 都清理新增状态；`ResetCount + FailCount`、`HasTips` 保留边界、DB 写入及所有关卡打点顺序不变，符合《关卡打点生命周期约定》2.1～2.4 的尝试级提示状态清理要求。
- Unity 6000.0.59f2 Roslyn 使用当前完整响应文件编译 `Assembly-CSharp` 为 `exit 0`、`0 error`；`git diff --check` 通过。PlayMode 多轮、Reset/Fail、暂停和已获答案停止循环仍为 `uncertain`。
- 回滚时删除两个新增属性及其清理/记录逻辑，并把 `HasClickTips` 恢复为“当前尝试已开放过”的一次性语义；无存档迁移。
