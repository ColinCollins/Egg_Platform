# Level74 Mow 停留触发区二次 Cut 风险评估

## 原始行为基线

- `OnTrigger2DHandle` 只在 Enter/Exit 时触发 UnityEvent，没有 Stay 入口。
- `Level74Ctrl.OnGrassEnter(...)` 在 Mow 状态下调用 `SelectGrassForPanel(...)`，由此绑定 `selectedGrass` 并显示 cut 按钮。
- `Level74GrassCtrl.TryCut(...)` 在 cut 动画结束时把 leaf 状态置为 `Hidden` 并调用操作完成回调；regrow 完成后才恢复 `Visible`。
- cut 操作完成当帧 `CanCut()` 为 false，`Level74Ctrl.OnOperationComplete()` 旧逻辑会显示不可点击 cut；后续如果没有 Exit/Enter，不会再次刷新按钮状态。

## 影响文件与系统

- 直接修改：`Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs`
- 间接受影响：Level74 Mow cut UI、Hoe dig UI 的当前选中 Grass 面板刷新。
- 未修改共享系统：`OnTrigger2DHandle`、`CustomButton`、`Level74GrassCtrl`、Actor、Door、save/config/resource/analytics/platform SDK。

## 已规避风险

- P1：修改共享触发器可能影响其他关卡。处理：不新增 `OnTriggerStay2D`，只在 Level74 本地读取当前选中 Grass 状态。
- P1：Grass 反向回调 Ctrl 会增加对象耦合和生命周期顺序风险。处理：不新增 Grass regrow 回调，Ctrl 只观察当前选中 Grass 的公开 `CanCut()` / `CanDig()`。
- P1：每帧重复 `ShowCut(...)` 可能导致按钮动画反复播放。处理：缓存上一次 Grass、工具和可操作状态，只有状态变化或强制刷新时才更新 UI。
- P1：失败、成功、退出或 Reset 后保留旧按钮状态。处理：Reset、Hide、Cleanup、fire cleanup 都清空面板状态缓存。

## 剩余风险

- P1：`Update()` 每帧检查当前选中 Grass，虽然只读一棵草的状态，但仍需 PlayMode 验证不会和按钮显示动画、工具切换动画产生节奏冲突。
- P1：如果 `selectedGrass` 保持不变但 Actor 已经物理离开触发区、Exit 事件因异常未发出，按钮可能继续依据旧选中草显示；这是原有 selectedGrass 依赖 Exit 的边界，本次未扩大到共享触发器层处理。
- OBS：Hoe dig 也复用同一刷新入口，预期只保持原有 `CanDig()` 状态显示；仍需手测确认 cut 修复没有改变 dig 体验。

## 验证证据

- 编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，0 error / 1114 warnings；warnings 属于当前工程既有范围。
- 格式：`git diff --check -- Assets\Game\Scripts\Level\LevelItem\Level_74\Level74Ctrl.cs` 无错误，仅提示 Git 工作区 LF/CRLF 转换。
- 静态风险扫描：`Level74Ctrl.cs` 本次新增代码未引入 `OnValidate`、`Reset(...)`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、Resources、YooAsset、PlayerPrefs、Analytics、Instantiate 或 `Debug.Log`；扫描命中来自旧注释和已有 `OnGameReset` 方法名。

## 未验证缺口

- Unity Editor PlayMode 未执行：Mow 停留同一 Grass trigger 内连续 cut/regrow/cut 仍需实测。
- Unity Editor PlayMode 未执行：工具切换、离开触发区、restart/fail/success/exit/disable/destroy 清理仍需实测。
- Android/iOS 横屏触摸未执行：快速点击、等待 regrow、帧稳定性和触摸可用性仍需真机或模拟器 smoke。

## 回滚路径

- 回滚 `Level74Ctrl.cs` 中面板状态缓存和 `Update()` 状态观察逻辑。
- 恢复旧的 Enter/operation complete 直接刷新按钮行为；文档可随代码回滚同步删除或保留为历史记录。

## 2026-06-24 Cut 等待重生隐藏按钮风险评估补充

### 原始行为基线

- 2026-06-23 修正后，`Level74Ctrl` 会在 `Update()` 中观察当前 `selectedGrass` 的 `CanCut()` / `CanDig()` 状态变化，避免 Actor 停留同一 Grass trigger 时 regrow 后 cut button 不恢复。
- 该修正下，cut 动画完成回调当帧 `CanCut()` 为 false，面板会显示不可交互 cut button；Grass regrow 后再刷新为可交互。
- `Level74GrassCtrl.TryCut(...)` 的 regrow 完成仍只恢复 Grass 自身 leaf 状态，不主动回调 `Level74Ctrl`。

### 本次影响文件与系统

- 直接修改：`Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs`
- 直接影响：Level74 Mow cut button 在 Grass regrow 等待期的显示状态。
- 间接受影响：同一 `selectedGrass` 的面板状态缓存，因为隐藏后仍依赖 `CanCut()` 状态变化重新显示按钮。
- 未修改：`Level74GrassCtrl.cs`、`Level74GamePlayPanelCtrl.cs`、`OnTrigger2DHandle`、`CustomButton`、Actor、Door、prefab、save/config/resource/analytics/广告/IAP/platform SDK。

### 风险项

- P1：cut 后隐藏按钮可能让玩家误以为当前 Grass 已不可继续操作；处理：隐藏只发生在 Mow 且 `CanCut()` 为 false 的等待期，regrow 后同一观察入口会重新显示。
- P1：如果 `CanCut()` 恢复但 `Update()` 未运行或 `selectedGrass` 被错误清空，按钮不会重新显示；现有 reset/hide/exit/cleanup 会清理状态，仍需 PlayMode 验证正常 gameplay 时的恢复。
- P1：隐藏按钮后 `visibleMode` 被 `HideAll()` 置为 None，若状态缓存没有记录 false 状态，可能无法在 true 时触发恢复；本次代码先写入 `panelStateCanOperate=false` 和 `hasPanelState=true`，再隐藏按钮，确保 `CanCut()` 变 true 时状态变化可被捕获。
- OBS：Hoe dig 继续显示 `ShowDig(canOperate)`，本次没有统一隐藏不可 dig 状态；这是为了保持 Hoe 旧表现不被 Mow 需求牵连。

### 已规避风险

- 不修改共享触发器、不新增 `OnTriggerStay2D`，避免影响其他关卡。
- 不新增 Grass regrow 回调，避免 Grass 与 Ctrl 生命周期反向耦合。
- 不修改 `Level74GamePlayPanelCtrl.HideAll()` 和 `ShowCut(...)`，只在 Level74 Ctrl 中决定 Mow 等待期是否显示按钮。
- 不新增 save/config/resource/analytics/广告/IAP/platform SDK 链路。

### 验证证据

- 格式：`git diff --check -- Assets\Game\Scripts\Level\LevelItem\Level_74\Level74Ctrl.cs` 通过，0 error，仅提示 Git 工作区 LF/CRLF 转换。
- 静态风险扫描：`Level74Ctrl.cs` 扫描仅命中旧文件顶部注释中的 `Instantiate` 与已有 `OnGameReset` 方法名；本次新增分支未引入 `OnValidate`、`Reset(...)`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、Resources、YooAsset、PlayerPrefs、Analytics、`Debug.Log`、IAP 或 Advertisement。
- 编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，0 error / 1114 warnings；warnings 属于当前工程既有范围。

### 未验证缺口

- Unity Editor PlayMode 未执行：Mow cut 完成后按钮隐藏、Grass regrow 后同一触发区内按钮重新显示仍需实测。
- Unity Editor PlayMode 未执行：离开 trigger、切换工具、restart/fail/success/exit/disable/destroy 后的 UI 清理仍需实测。
- Android/iOS 横屏触摸未执行：快速点击、等待 regrow、移动中切换工具和帧稳定性仍需真机或模拟器 smoke。

### 回滚路径

- 删除 `Level74Ctrl.RefreshSelectedGrassPanelState(...)` 中 `activeTool == ToolType.Mow && !canOperate` 的 `panelCtrl.HideAll()` 分支，恢复不可 cut 时显示不可交互 cut button 的旧表现。
- 若需整体回滚 2026-06-23 的停留触发区恢复逻辑，再回滚面板状态缓存和 `Update()` 状态观察逻辑；本次补充分支可随之删除。

## 2026-06-24 Cut 按钮缩放隐藏一致性风险评估补充

### 原始行为基线

- 2026-06-24 “Cut 后等待重生期间隐藏按钮”调整后，Mow 且 `CanCut()` 为 false 时调用 `Level74GamePlayPanelCtrl.HideAll()`。
- `HideAll()` 内部的 `HideButton(...)` 会 `DOKill()`、关闭交互、直接把 `localScale` 设为 `Vector3.zero` 并 `SetActive(false)`，没有等待缩放动画。
- `ShowCut(...)` 的正常显示逻辑已使用 `DOScale(Vector3.one, showScaleDuration)`，因此隐藏表现与显示表现不一致。

### 本次影响文件与系统

- 直接修改：`Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs`
- 直接修改：`Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GamePlayPanelCtrl.cs`
- 直接影响：Level74 Mow cut 完成后等待 regrow 期间的 `cutButton` 隐藏表现。
- 未修改：`Level74GrassCtrl.cs`、`OnTrigger2DHandle`、`CustomButton`、Actor、Door、prefab、save/config/resource/analytics/广告/IAP/platform SDK。

### 风险项

- P1：将隐藏改成 tween 后，如果清理流程也使用延迟隐藏，可能在退出、失败、成功或 Destroy 后留下回调。处理：新增 `HideCutWithScaleTween()` 只给运行时 cut 完成分支使用，Reset/cleanup/Disable/Destroy 仍走立即 `HideAll()`。
- P1：隐藏 tween 期间如果 Grass 很快 regrow 并重新显示，旧 tween 可能和 `ShowCut(...)` 竞争。处理：`ShowOnly(...)` 进入时会对目标按钮 `DOKill()`，并重新激活、设置 scale 后播放显示动画。
- P1：`visibleMode` 被置为 None 后，`SetInteractable(true)` 不再影响正在隐藏的 cut button。处理：`HideCutWithScaleTween()` 会显式关闭 `cutButton.Interactable`；下一次 `ShowCut(true)` 会重新设置交互状态。
- OBS：`showScaleDuration` 同时作为显示和隐藏时长，本次不新增序列化字段，保持 Level74 面板本地参数简单；具体节奏仍需 PlayMode 视觉验证。

### 已规避风险

- 不修改共享 `CustomButton` 或 GamePlayPanel 通用 use_btn 逻辑，避免影响其他关卡。
- 不修改 `Level74GrassCtrl.TryCut(...)` 和 regrow 时序，避免改变 cut 可操作状态恢复窗口。
- 不新增 `OnTriggerStay2D`、Grass 反向回调、存档、配置、资源、广告、IAP 或平台 SDK 链路。

### 验证证据

- 格式：`git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GamePlayPanelCtrl.cs` 通过，0 error，仅提示 Git 工作区 LF/CRLF 转换。
- 静态风险扫描：扫描 `Level74Ctrl.cs` 与 `Level74GamePlayPanelCtrl.cs` 后，仅命中旧注释中的 `Instantiate` 和既有 `OnGameReset` 方法名；本次新增代码未引入 `OnValidate`、`Reset(...)`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、Resources、YooAsset、PlayerPrefs、Analytics、`Debug.Log`、IAP 或 Advertisement。
- 编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，0 error / 543 warnings；warnings 属于当前工程既有范围。

### 未验证缺口

- Unity Editor PlayMode 未执行：需要验证 cut 完成后的缩放隐藏、regrow 后重新显示、快速点击、离开 trigger、切换工具和失败/成功/重置/退出清理。
- Android/iOS 横屏触控未执行：快速点击 cut、等待 regrow、移动中切换工具和帧稳定性仍需真机或模拟器 smoke。

### 回滚路径

- 将 `Level74Ctrl.RefreshSelectedGrassPanelState(...)` 中的 `panelCtrl.HideCutWithScaleTween()` 改回 `panelCtrl.HideAll()`。
- 删除 `Level74GamePlayPanelCtrl.HideCutWithScaleTween()` 和 `HideButtonWithScaleTween(...)`；文档记录可随代码回滚同步删除或保留为历史证据。

## 2026-06-26 Dig UseBtn 缩放隐藏一致性风险评估补充

### 原始行为基线

- Mow cut 完成后等待 regrow 的隐藏路径已经使用 `Level74GamePlayPanelCtrl.HideCutWithScaleTween()`，按钮会通过 `DOScale(Vector3.zero, showScaleDuration)` 缩放到 0 后关闭。
- Hoe Dig 完成后，`Level74GrassCtrl.TryDig(...)` 的完成回调会将 `rootState` 置为 `Removed`；随后 `Level74Ctrl.OnOperationComplete()` 检测到 `selectedGrass.IsDead`，走 `HideSelectedGrass(...)` 并调用 `panelCtrl.HideAll()` 立即关闭当前 UseBtn。
- `GamePlayPanel_022_74.prefab` 文本证据显示 `Level74GamePlayPanelCtrl.cutButton` 与 `digButton` 均已绑定，Dig 可由面板控制器解析到独立按钮或 fallback 按钮。

### 本次影响文件与系统

- 直接修改：`Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs`
- 直接修改：`Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GamePlayPanelCtrl.cs`
- 直接影响：Level74 Hoe Dig 完成后当前 UseBtn 的隐藏表现，以及既有 Mow cut 缩放隐藏入口的内部实现。
- 未修改：`Level74GrassCtrl.cs`、`OnTrigger2DHandle`、`CustomButton`、Actor、Door、prefab、save/config/resource/analytics/广告/IAP/platform SDK。

### 风险项

- P1：Dig 完成后当前 Grass 会被移除，如果隐藏 tween 延迟关闭期间还能再次点击，可能重复触发操作。处理：`HideButtonWithScaleTween(...)` 在 tween 前关闭 `Interactable`，`OnDigClicked()` 也仍受 `selectedGrass`、`activeTool`、`interactionLocked` 和 `IsActorEligible()` 保护。
- P1：运行时操作完成隐藏改为 tween 后，如果 Reset、失败、成功、退出或 Destroy 也使用延迟隐藏，可能留下回调。处理：只在 `OnOperationComplete()` 的操作完成分支调用 `HideSelectedGrassAfterOperation(...)`；生命周期清理、工具切换、触发区离开仍走立即 `HideAll()`。
- P1：`HideCurrentWithScaleTween()` 依赖 `visibleMode` 判断当前按钮，如果调用前 visibleMode 已被清空，可能没有动画。处理：本次只在 `OnOperationComplete()` 内、清空面板状态前调用面板当前模式；`HideSelectedGrassAfterOperation(...)` 不调用 `HideAll()`，避免先清空 visibleMode。
- OBS：`showScaleDuration` 继续同时作为显示、Cut 隐藏和 Dig 隐藏时长，本次不新增序列化字段；具体视觉节奏仍需 PlayMode 验证。

### 已规避风险

- 不修改 `Level74GrassCtrl.TryDig(...)`、root/leaf 状态或开门判定，避免改变 Dig 完成条件。
- 不修改共享 `CustomButton`、共享触发器、GamePlayPanel prefab 绑定或通用 Use button 组件，避免影响其他关卡。
- 不新增存档、配置、资源加载、打点、广告、IAP 或平台 SDK 链路。

### 验证证据

- 格式：`git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GamePlayPanelCtrl.cs` 通过，0 error，仅提示 Git 工作区 LF/CRLF 转换。
- 静态风险扫描：扫描 `Level74Ctrl.cs` 与 `Level74GamePlayPanelCtrl.cs` 后，仅命中旧注释中的 `Instantiate` 和既有 `OnGameReset` 方法名；本次新增代码未引入 `OnValidate`、`Reset(...)`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、Resources、YooAsset、PlayerPrefs、Analytics、`Debug.Log`、IAP 或 Advertisement。
- 编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，0 error / 1114 warnings；warnings 属于当前工程既有范围。

### 未验证缺口

- Unity Editor PlayMode 未执行：需要验证 Hoe Dig 完成后当前 UseBtn 缩放隐藏、Mow cut 缩放隐藏未回归、快速点击、离开 trigger、切换工具和失败/成功/重置/退出清理。
- Android/iOS 横屏触控未执行：快速点击 Dig/Cut、操作期间移动或切换工具、按钮 tween 帧稳定性仍需真机或模拟器 smoke。

### 回滚路径

- 将 `Level74Ctrl.OnOperationComplete()` 中死亡/空选中 Grass 分支恢复为 `HideSelectedGrass(selectedGrass)`。
- 删除 `HideSelectedGrassAfterOperation(...)`、`Level74GamePlayPanelCtrl.HideCurrentWithScaleTween()` 和 `ResolveButton(...)`，并把 `HideCutWithScaleTween()` 恢复为直接对 `cutButton` 调用 `HideButtonWithScaleTween(...)`。
