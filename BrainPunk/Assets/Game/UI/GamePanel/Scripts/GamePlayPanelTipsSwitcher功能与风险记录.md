# GamePlayPanelTipsSwitcher 展开与自动隐藏功能记录

## 需求与原行为

- 需求：`GamePlayPanelTipsSwitcher` 收到显示事件后，让 `Target` 以 `DOScale` 从 `0` 展开到 `1`；展开时长可在 Inspector 配置，默认 `0.5` 秒；展开完成后持续展示时间也可配置，默认 `3` 秒，结束时直接执行 `Target.SetActive(false)`。
- 原行为：`GamePlayPanelSwitchTipsEvent(true/false)` 只直接切换 `Target.activeSelf`；显示分支无条件获取并复位 `RotateFloatHandle`，未提供展开动画、自动隐藏或重复显示计时管理。

## 影响文件与系统

- `Assets/Game/UI/GamePanel/Scripts/GamePlayPanelTipsSwitcher.cs`：新增 DOTween 展开/展示序列和两个序列化时长字段。
- `Assets/Game/UI/GamePanel/Prefabs/GamePlayPanel.prefab` 与 `Assets/Game/UI/DrawGame/GamePlayPanel/Resources/DrawGame_GamePlayPanel.prefab`：继续作为该共享组件的现有消费者；本次代码实现未修改两个 prefab 的组件绑定。
- 继续复用既有 `GamePlayPanelSwitchTipsEvent`；未新增全局事件、配置表、存档、资源加载、打点或平台 SDK 流程。

## 修改后行为与 UI 流

```text
GamePlayPanelSwitchTipsEvent(true)
  -> 停止上一轮显示序列
  -> 激活 Target，并把 localScale 设为 Vector3.zero
  -> 若 Target 带 RotateFloatHandle，则沿用原逻辑复位旋转
  -> 以 OutQuad、unscaled time 在 expandDuration 内 DOScale 到 Vector3.one
  -> 展开完成后等待 displayDuration
  -> 直接 Target.SetActive(false)

GamePlayPanelSwitchTipsEvent(false)
  -> 停止当前显示序列
  -> 直接 Target.SetActive(false)
```

- `expandDuration` 默认 `0.5` 秒，`displayDuration` 默认 `3` 秒，均通过 `[Min(0f)]` 暴露给 Inspector；运行时仍用 `Mathf.Max(0f, value)` 防御异常负值。
- 重复收到显示事件时会杀掉旧序列并从缩放 `0` 重新计时，避免旧回调提前隐藏新一轮提示。
- 组件禁用或销毁时会停止序列；禁用时同时隐藏 `Target`。`RotateFloatHandle` 现在是可选表现组件，不再是显示流程的强制依赖。
- 展示时间从展开动画完成后开始计算；DOTween 使用 unscaled time。若原生覆盖层导致 Unity PlayerLoop 完全停止，Tween 与计时也不会推进，恢复 Unity 帧后继续。

## 验证证据

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`：通过，`0 error / 1119 warning`；warning 为工程现有范围，未见指向本次脚本的新增编译错误。
- `git diff --check -- Assets/Game/UI/GamePanel/Scripts/GamePlayPanelTipsSwitcher.cs`：通过，仅输出工作区 LF/CRLF 转换提示。
- 静态确认主线与 Draw HUD prefab 都通过脚本 GUID `2c6416473dd41b946b7d97a9a0d891da` 挂载该组件并绑定非空 `target`；新增字段未要求修改既有绑定。

## 剩余风险与未验证项

- `uncertain`：尚未在 Unity PlayMode 目测 `0 -> 1` 展开、展开后 `3` 秒隐藏、连续显示事件重置计时、显式隐藏事件抢占以及面板禁用/重开流程。
- `uncertain`：主线当前工作区中的新 `warningBubble` 美术层级、Draw HUD 既有旋转表现，以及 Android/iOS 横屏分辨率与触控表现仍待手工验证。
- 当前两个 prefab 未显式覆盖新增时长字段，使用脚本默认值；后续在 Inspector 修改并保存 prefab 后即可形成各 prefab 的独立序列化配置。

## 2026-09-02 Level102 局部接管边界

- `GamePlayPanelTipsSwitcher` 新增 `TargetObject`、`DisplayDuration` 和 `SetExternalControl(bool)`，仅供 Level102 在运行时接管同一 `warningBubble` 实例；默认 `_externalControl=false` 时，主线和 Draw HUD 仍完全走本组件原有事件动画。
- 外部接管开启后，本组件停止自己的 DOTween 展开/回缩序列并隐藏 Target，Level102 再按 `GamePlayPanelSwitchTipsEvent` 的同一全局门槛控制显示、物理 Collider 和拖拽生命周期。Level102 不再使用角色头顶 `HintBubble`。
- 该共享 API 不新增事件、配置表、存档或资源加载。调用方在 Teardown 需要显式 `SetExternalControl(false)`，以免复用的 GamePlayPanel 实例继续停留在外部控制状态。
- `uncertain`：Level102 warningBubble 的 WorldSpace Collider 拟合、角色站立/拖拽体验、事件订阅时序和移动端触控仍需 Unity PlayMode 验证；共享组件其他消费者的默认显示流程仅完成静态回读。

回滚时移除 `TargetObject`、`DisplayDuration`、`SetExternalControl` 及 `_externalControl` 分支，并同步删除 Level102 的调用；不修改 warningBubble 预制体和其它关卡的序列化数据。

## 回滚路径

- 从 `GamePlayPanelTipsSwitcher.cs` 删除 `expandDuration`、`displayDuration`、`_displaySequence` 和显示序列辅助方法，恢复事件回调直接调用 `target.SetActive(evt.IsShow)`。
- 回滚不需要迁移 prefab、配置、存档、资源或事件；若 prefab 后续保存了两个时长字段，旧脚本会忽略不再存在的 YAML 字段。

## 2026-08-18 自动隐藏前增加 0.2 秒回缩

### 需求、基线与修改后行为

- 需求：提示展示时间结束后，先播放 `0.2` 秒 `DOScale` 回缩，再隐藏提示对象。
- 原行为：展开并等待 `displayDuration` 后，序列直接执行 `Target.SetActive(false)`，没有收尾动画。
- 修改后：展示计时结束后，原序列追加 `Target.transform.DOScale(Vector3.zero, 0.2f)`；Tween 完成后继续由原有 `OnComplete` 隐藏 `Target`。
- `GamePlayPanelSwitchTipsEvent(false)` 仍立即停止序列并隐藏，组件禁用或销毁仍终止当前序列；重复显示仍从缩放 `0` 重新开始，不等待旧回缩完成。

### 影响范围与 UI 流

- 修改文件仅为 `Assets/Game/UI/GamePanel/Scripts/GamePlayPanelTipsSwitcher.cs`，主线与 Draw HUD 的既有组件消费者共同获得自动回缩效果。
- UI 流调整为“展开 → 展示计时 → `0.2` 秒回缩 → 隐藏”；序列继续使用 unscaled time。
- 未修改 prefab、事件类型、配置、存档、资源加载、打点、广告、支付或平台 SDK；没有新增每帧逻辑和运行时实例化。

### 验证、剩余风险与回滚

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly`：通过，`0 error / 1127 warning`；warning 为当前工程既有范围，未见指向本次脚本的编译错误。
- 已执行目标代码差异检查和敏感生命周期静态检查；没有新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、资源加载或每帧入口。
- `uncertain`：尚未在 Unity PlayMode 和 Android/iOS 横屏环境目测回缩节奏、显式隐藏抢占、重复显示、面板禁用和重开表现。
- 回滚时删除显示间隔后的 `DOScale(Vector3.zero, 0.2f)` 追加语句，即恢复计时结束后直接隐藏；无需迁移 prefab、配置、数据或资源。

## 2026-09-03 主线提示气泡循环展示

### 需求、基线与修改后行为

- 需求：主线提示入口达到等待时间后显示，完成当前展示并完全消失后，从该时刻重新等待同一配置时长再显示；达到重试门槛的尝试首次进入时立即显示，首次气泡消失后也改走等待时长循环。
- 原行为：`PlayCtrl_Playing` 首次满足 `showTipsTimeLimit` 或 `showTipsFailCount` 后将 `HasClickTips` 保持为 `true`，所以当前尝试只派发一次显示事件；UI 自动隐藏不会把完成时刻反馈给玩法状态。
- 修改后：普通 `GamePlayPanelTipsSwitcher` 在“展开 → `displayDuration` 展示 → 回缩 → `SetActive(false)`”全部完成后派发既有 `GamePlayPanelSwitchTipsEvent(false)`。`PlayCtrl` 据此记录气泡消失时的当前尝试时间，`PlayCtrl_Playing` 再以 `当前尝试时间 - 上次消失时间` 检查下一轮等待门槛。
- `showTipsFailCount` 只参与每次尝试的首次判断，避免累计重试次数已达标后每次消失都立即重弹；Reset/失败重试会按既有尝试边界清理循环状态，因此门槛已满足的新尝试仍会开场立即显示。

### 配置、事件与兼容边界

- 等待秒数继续读取 `ConfigManager.RemoteConfig.GetShowTipsTimeLimit()`：优先使用 `level_config.showTipsTimeLimit`，缺失时回退 Luban `GlobalConst.ShowTipsTimeLimit`。本地默认数据当前为 `10`，代码未写死该值。
- 重试门槛继续读取 `GetShowTipsFailCount()`：优先使用同名 RemoteConfig 字段，缺失时回退 Luban；本地默认数据当前为 `2`，即累计重试两次后第三次尝试首次进入即显示，代码未写死该值。
- 展示时长继续使用 `GamePlayPanelTipsSwitcher.displayDuration`；主线 Prefab 当前序列化为 `3` 秒。展开 `0.3` 秒和回缩 `0.2` 秒完成后才记录“消失”，下一轮等待不包含本轮展示/动画时间。
- 未新增全局事件、配置字段、存档、资源加载或打点。玩家已获得提示 (`HasTips=true`) 后仍停止提示气泡；玩家主动点击提示入口时既有 `false` 事件会结束当前展示，恢复 Playing 且仍未获得提示时才从关闭时刻重新等待。
- Level102 对同一 warningBubble 的外部接管在完成自己的回缩后调用 `NotifyExternalDisplayEnded()`，保持共享循环状态一致；若玩家把气泡拖成物理垫脚物，则气泡不消失，也不启动下一轮。
- 展示 Tween 继续使用 unscaled time；下一轮等待使用 `LevelRuntimeState.CurrentAttemptTimeSeconds`，只在 Playing 更新中按 `Time.deltaTime` 累加，因此暂停期间不会消耗下一轮等待时间。

### 验证、风险与回滚

- Unity 6000.0.59f2 自带 Roslyn 使用当前完整响应文件编译 `Assembly-CSharp`：`exit 0`、`0 error`；输出为工程既有 warning。
- `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示。普通 MSBuild 全 solution 仍被工程现有 Firebase 程序集缺失阻断，该失败未指向本次文件。
- `uncertain`：尚未在 Unity PlayMode 目测 `10s → 展示 3s → 回缩 → 10s` 多轮时序、第三次尝试开场立即显示、暂停/恢复、主动点击、已获提示停止循环及 Android/iOS 横屏表现。
- 回滚时移除 UI 完成后的 `false` 通知、`PlayCtrl` 的隐藏事件处理、`LevelRuntimeState` 两个循环字段及 `PlayCtrl_Playing` 的相对时间判断；Level102 同步移除外部完成通知。无需迁移配置、Prefab、存档或事件类型。
