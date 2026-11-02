# Level74 Mow 停留触发区二次 Cut 修正说明

## 需求背景

- 现象：玩家选择 Mow 后进入某棵 Grass 触发区，点击 cut 一次；如果 Actor 一直停留在同一触发区内，没有触发 Exit 再重新 Enter，草重新长出后 cut 按钮不会自动恢复可点击。
- 直接原因：`OnTrigger2DHandle` 只提供 Enter/Exit；`Level74Ctrl.OnGrassEnter(...)` 只在 Enter 时绑定 `selectedGrass` 并刷新按钮；`Level74GrassCtrl.TryCut(...)` 的 regrow 完成只恢复 Grass 自身 leaf 状态，不会重新触发 `Level74Ctrl` 的 UI 刷新入口。

## 影响范围

- 修改文件：`Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs`
- 关联但未修改：`Level74GrassCtrl.cs`、`Level74GamePlayPanelCtrl.cs`、`OnTrigger2DHandle.cs`、`Level_074.prefab`、`Grass.prefab`
- 未涉及：save、config、resource loading、hot reload、analytics、广告、IAP、平台 SDK、共享触发器脚本。

## 修正后行为

- `Level74Ctrl` 保存当前面板状态：选中 Grass、当前工具、上一次可操作状态。
- `Update()` 在运行时只检查当前 `selectedGrass` 的 `CanCut()` 或 `CanDig()`；状态没有变化时不刷新 UI，避免每帧重复播放按钮显示动画。
- 当 Mow cut 后 Grass 从不可 cut 恢复为可 cut，即使 Actor 没有离开触发区，`Level74Ctrl` 也会刷新 cut 按钮为可点击。
- `SelectGrassForPanel(...)` 与 `OnOperationComplete()` 统一走面板状态刷新入口，减少 UI 状态与 Grass 状态脱节。
- Reset、Hide、失败、成功、退出和 Cleanup 会清理面板状态缓存，避免 stale selected grass 或按钮状态残留。

## 数据与 UI 流

- UI 流：Grass Enter -> 绑定 `selectedGrass` -> 显示 cut/dig -> cut 操作结束时 `CanCut()` 为 false -> Grass regrow 完成后 `CanCut()` 变为 true -> `Update()` 观察到状态变化 -> 同一触发区内恢复 cut 可点击。
- 数据流：不读写 DB，不新增存档字段，不修改配置表。
- 资源流：不新增资源引用，不修改 prefab 绑定。
- 事件流：不新增 Grass 到 Ctrl 的回调，不修改共享 `OnTrigger2DHandle` 的 Enter/Exit 行为。

## 验证记录

- 静态编译：已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`，结果 0 error / 1114 warnings；warnings 属于当前工程既有范围。
- 格式检查：已执行 `git diff --check -- Assets\Game\Scripts\Level\LevelItem\Level_74\Level74Ctrl.cs`，无错误，仅提示 Git 工作区 LF/CRLF 转换。
- 静态风险扫描：`rg -n "OnValidate|Reset\(|ExecuteAlways|ExecuteInEditMode|DB|Save|RemoteConfig|Resources|YooAsset|PlayerPrefs|Analytics|Instantiate|Debug\.Log" Assets\Game\Scripts\Level\LevelItem\Level_74\Level74Ctrl.cs` 命中旧文件顶部注释中的 `Instantiate` 与已有 `OnGameReset` 方法名；本次新增代码未引入这些链路。

## 未验证项

- Unity PlayMode 仍需验证：Mow 停留同一 Grass trigger 内 cut -> regrow 后按钮自动恢复可点；离开 trigger 时按钮隐藏；切换 Hoe/Eradicator 后 UI 不残留。
- Unity PlayMode 仍需验证：restart、fail、success、exit、disable/destroy 清理后不会残留 `selectedGrass` 或按钮状态。
- Android/iOS 横屏触摸与帧稳定性仍未验证。

## 回滚路径

- 恢复 `Level74Ctrl.cs` 中本次新增的面板状态缓存字段、`Update()` 状态检查、`RefreshSelectedGrassPanelState(...)`、`ResetPanelRefreshState(...)`。
- 将 `SelectGrassForPanel(...)` 与 `OnOperationComplete()` 恢复为直接调用 `panelCtrl.ShowCut(grass.CanCut())` / `panelCtrl.ShowDig(grass.CanDig())` 的旧逻辑。

## 2026-06-24 补充：Cut 后等待重生期间隐藏按钮

### 需求背景

- 需求：Level74 中 Mow 的 cut 触发完成后，隐藏当前对应的 cut button；等到 Grass 重新长好、再次 `CanCut()` 后再显示。
- 本次是在既有“停留触发区内 regrow 后恢复 cut 可点”逻辑上调整 UI 显隐节奏，不改变 Grass cut/regrow 动画、Actor tool 动作、Hoe dig、Door completion 或共享触发器。

### 调整后行为

- `Level74Ctrl.RefreshSelectedGrassPanelState(...)` 仍然观察当前 `selectedGrass`、当前工具和 `CanCut()` / `CanDig()` 状态变化。
- 当当前工具是 Mow 且 `selectedGrass.CanCut()` 为 `false` 时，调用 `panelCtrl.HideAll()` 隐藏 cut button，而不是显示不可交互按钮。
- 当同一棵 Grass regrow 完成后 `CanCut()` 从 `false` 变为 `true`，既有 `Update()` 状态观察会再次调用 `panelCtrl.ShowCut(true)`，恢复 cut button 显示和点击。
- Hoe dig 路径仍保持原有 `ShowDig(canOperate)` 行为；本次没有让 dig button 在不可操作时跟随 Mow 一起隐藏。

### 数据与 UI 流

- UI 流：Grass Enter -> Mow 显示 cut -> 点击 cut -> cut 动画完成回调 -> `CanCut()` 为 false -> 隐藏 cut button -> Grass regrow 完成 -> `CanCut()` 为 true -> 同一触发区内重新显示 cut button。
- 数据流：不读写 DB，不新增存档字段，不修改配置表。
- 资源流：不新增资源引用，不修改 prefab 绑定。
- 事件流：不新增 Grass 到 Ctrl 的回调，不修改共享 `OnTrigger2DHandle` 的 Enter/Exit 行为。

### 验证记录

- 格式检查：已执行 `git diff --check -- Assets\Game\Scripts\Level\LevelItem\Level_74\Level74Ctrl.cs`，结果 0 error，仅提示 Git 工作区 LF/CRLF 转换。
- 静态风险扫描：`rg -n "OnValidate|Reset\(|ExecuteAlways|ExecuteInEditMode|DB|Save|RemoteConfig|Resources|YooAsset|PlayerPrefs|Analytics|Instantiate|Debug\.Log|IAP|Advertisement" Assets\Game\Scripts\Level\LevelItem\Level_74\Level74Ctrl.cs` 仅命中旧文件顶部注释中的 `Instantiate` 与已有 `OnGameReset` 方法名；本次新增代码未引入这些链路。
- 静态编译：已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`，结果 0 error / 1114 warnings；warnings 属于当前工程既有范围。

### 未验证项

- Unity PlayMode 仍需验证：Mow cut 完成后 cut button 立即隐藏，Grass regrow 完成后在同一触发区内重新显示。
- Unity PlayMode 仍需验证：离开 Grass trigger、切换 Hoe/Eradicator、restart/fail/success/exit/disable/destroy 后不会残留隐藏或错误显示的按钮状态。
- Android/iOS 横屏触摸、快速点击 cut、等待 regrow 和帧稳定性仍未验证。

### 回滚路径

- 移除 `Level74Ctrl.RefreshSelectedGrassPanelState(...)` 中 Mow 且 `!canOperate` 时调用 `panelCtrl.HideAll()` 并提前返回的分支，即可恢复为显示不可交互 cut button 的旧表现。
- 文档记录不影响 runtime，可随代码回滚同步删除或保留为历史证据。

## 2026-06-24 补充：Cut 完成后隐藏走缩放动画

### 需求背景

- 需求：`Level74Ctrl` 的 Mow cut 完成后，对应 `cutButton` 的隐藏逻辑需要和正常隐藏表现一致，使用 DoTween Scale 缩放隐藏，而不是直接把 scale 置零并关闭对象。
- 本次是对 2026-06-24 “Cut 后等待重生期间隐藏按钮”表现的补齐，不改变 Grass cut/regrow 状态、Actor tool 动作、Hoe dig、开门完成条件、触发器、存档、配置、资源加载或平台链路。

### 受影响文件与系统

- 修改：`Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs`
- 修改：`Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GamePlayPanelCtrl.cs`
- 关联但未修改：`Level74GrassCtrl.cs`、`OnTrigger2DHandle`、`CustomButton`、`GamePlayPanel_022_74.prefab`、`Level_074.prefab`

### 调整后行为

- `Level74Ctrl.RefreshSelectedGrassPanelState(...)` 在 Mow 且 `CanCut()` 为 false 时，不再调用瞬时 `panelCtrl.HideAll()`，改为调用 `panelCtrl.HideCutWithScaleTween()`。
- `Level74GamePlayPanelCtrl.HideCutWithScaleTween()` 只处理运行时 cut 完成后的按钮隐藏表现：先关闭 `cutButton.Interactable`，再用 `DOScale(Vector3.zero, showScaleDuration)` 缩放到 0，完成后关闭对象。
- 生命周期清理、Reset、失败、成功、退出、Disable/Destroy 仍走 `HideAll()` 的立即清理逻辑，避免清理阶段留下延迟 tween 回调。
- `panelStateCanOperate=false` 的状态缓存仍保留，因此 Grass regrow 后 `CanCut()` 变 true 时，既有 `Update()` 状态观察会重新调用 `ShowCut(true)` 恢复按钮。
- Hoe dig 仍保持原有 `ShowDig(canOperate)` 策略，本次只改变 Mow cut 等待重生期间 `cutButton` 的隐藏动画。

### 数据与 UI 流

- UI 流：Grass Enter -> Mow 显示 cut -> 点击 cut -> cut 动画完成回调 -> `CanCut()` 为 false -> `cutButton` 走 DoTween Scale 缩放到 0 并隐藏 -> Grass regrow 完成 -> `CanCut()` 变 true -> 同一触发区内重新 `ShowCut(true)`。
- 数据流：不读写 DB，不新增存档字段，不修改配置表。
- 资源流：不新增资源引用，不修改 prefab 绑定。
- 事件流：不新增 Grass 到 Ctrl 的回调，不修改共享 `OnTrigger2DHandle` 的 Enter/Exit 行为。

### 验证记录

- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GamePlayPanelCtrl.cs` 通过，0 error，仅提示 Git 工作区 LF/CRLF 转换。
- 静态风险扫描已执行：`rg -n "OnValidate|Reset\(|ExecuteAlways|ExecuteInEditMode|DB|Save|RemoteConfig|Resources|YooAsset|PlayerPrefs|Analytics|Instantiate|Debug\.Log|IAP|Advertisement" Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GamePlayPanelCtrl.cs`；命中项仅为 `Level74Ctrl.cs` 顶部旧注释中的 `Instantiate` 与既有 `OnGameReset` 方法名。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，0 error / 543 warnings；warnings 属于当前工程既有范围。

### 未验证项

- Unity PlayMode 仍需验证：Mow cut 完成后 `cutButton` 缩放隐藏、Grass regrow 后同一触发区内重新显示，以及快速 cut、离开 trigger、切换工具、restart/fail/success/exit/disable/destroy 后 UI 不残留。
- Android/iOS 横屏触摸、快速点击 cut、等待 regrow、移动中切换工具的帧稳定性仍待补验。

### 回滚路径

- 将 `Level74Ctrl.RefreshSelectedGrassPanelState(...)` 中 Mow 且 `!canOperate` 分支恢复为 `panelCtrl.HideAll()`。
- 删除 `Level74GamePlayPanelCtrl.HideCutWithScaleTween()` 与 `HideButtonWithScaleTween(...)`，即可恢复为瞬时隐藏表现。

## 2026-06-26 补充：Dig 完成后 UseBtn 隐藏也走缩放动画

### 需求背景

- 需求：`Level74Ctrl` 现有 `HideCutWithScaleTween()` 只覆盖 Mow cut 完成后的按钮隐藏表现；Dig 操作完成时，当前 UseBtn 也需要有 DoTween 缩放隐藏动画。
- 本次是对 Level74 操作按钮表现的一致性补齐，不改变 Grass dig 状态、Actor tool 动作、Door 开启判定、触发器、存档、配置、资源加载、打点或平台链路。

### 受影响文件与系统

- 修改：`Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs`
- 修改：`Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GamePlayPanelCtrl.cs`
- 关联但未修改：`Level74GrassCtrl.cs`、`CustomButton`、`GamePlayPanel_022_74.prefab`、`Level_074.prefab`、共享 `OnTrigger2DHandle`

### 原始行为基线

- Mow cut 完成后，`RefreshSelectedGrassPanelState(...)` 在 `CanCut()` 为 false 时调用 `panelCtrl.HideCutWithScaleTween()`，`cutButton` 会缩放到 0 后关闭。
- Hoe Dig 完成后，`Level74GrassCtrl.TryDig(...)` 会把 `rootState` 置为 `Removed`，`OnOperationComplete()` 随后走 `HideSelectedGrass(...)`，原路径会调用 `panelCtrl.HideAll()` 立即关闭当前 UseBtn，没有隐藏 tween。
- Reset、失败、成功、退出、Disable/Destroy 等清理路径也走 `HideAll()`，用于立即清理 UI 状态。

### 调整后行为

- `Level74GamePlayPanelCtrl.HideCutWithScaleTween()` 保持原有 public 入口，但内部复用新增的当前操作按钮缩放隐藏逻辑，继续兼容 Mow cut 路径。
- 新增 `Level74GamePlayPanelCtrl.HideCurrentWithScaleTween()`，只用于 Level74 运行时操作完成后的当前可见操作按钮隐藏表现；它会按 `visibleMode` 解析 Cut 或 Dig 对应按钮，关闭交互后执行 `DOScale(Vector3.zero, showScaleDuration)`，完成后关闭对象。
- `Level74Ctrl.OnOperationComplete()` 在当前选中 Grass 已死亡或为空时，改走 `HideSelectedGrassAfterOperation(...)`，清空选中状态和面板缓存后调用 `HideCurrentWithScaleTween()`；因此 Dig 完成后的 UseBtn 也会缩放隐藏。
- Reset、失败、成功、退出、Disable/Destroy、工具切换和触发区离开仍使用原有 `HideSelectedGrass(...)` / `HideAll()` 立即隐藏，避免清理阶段留下延迟 tween 回调。

### 数据与 UI 流

- UI 流：Grass Enter -> Hoe 显示 Dig UseBtn -> 点击 Dig -> Dig 动画完成回调 -> `rootState` 变为 `Removed` -> `OnOperationComplete()` 调用当前按钮缩放隐藏 -> UseBtn scale 到 0 后关闭。
- 数据流：不读写 DB，不新增存档字段，不修改配置表。
- 资源流：不新增资源引用，不修改 prefab 绑定。
- 事件流：不新增 Grass 到 Ctrl 的回调，不修改共享 `OnTrigger2DHandle` 的 Enter/Exit 行为。

### 验证记录

- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GamePlayPanelCtrl.cs` 通过，0 error，仅提示 Git 工作区 LF/CRLF 转换。
- 静态风险扫描已执行：`rg -n "OnValidate|Reset\(|ExecuteAlways|ExecuteInEditMode|DB|Save|RemoteConfig|Resources|YooAsset|PlayerPrefs|Analytics|Instantiate|Debug\.Log|IAP|Advertisement" Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GamePlayPanelCtrl.cs`；命中项仅为 `Level74Ctrl.cs` 顶部旧注释中的 `Instantiate` 与既有 `OnGameReset` 方法名。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，0 error / 1114 warnings；warnings 属于当前工程既有范围。

### 未验证项

- Unity PlayMode 仍需验证：Hoe Dig 完成后 UseBtn 缩放隐藏、Mow cut 缩放隐藏仍正常、离开 trigger、切换工具、restart/fail/success/exit/disable/destroy 后 UI 不残留。
- Android/iOS 横屏触摸、快速点击 Dig/Cut、操作期间移动或切换工具的帧稳定性仍待补验。

### 回滚路径

- 将 `Level74Ctrl.OnOperationComplete()` 中死亡/空选中 Grass 分支恢复为调用 `HideSelectedGrass(selectedGrass)`。
- 删除 `Level74GamePlayPanelCtrl.HideCurrentWithScaleTween()`、`ResolveButton(...)` 与 `HideSelectedGrassAfterOperation(...)`，并把 `HideCutWithScaleTween()` 恢复为直接隐藏 `cutButton`，即可回到只支持 Cut 缩放隐藏的旧表现。
