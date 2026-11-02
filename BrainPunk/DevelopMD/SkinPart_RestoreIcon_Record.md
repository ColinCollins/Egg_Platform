# SkinPart Restore icon 刷新保护记录

## 需求背景

`ShopPanel_SkinPart` 的 Restore 行使用独立的 `SkinItem_Revert.prefab` 视觉。需求要求当 `SkinItem` 处于 `SkinItemState.Restore` 时，`icon` 必须一直保持 prefab 初始表现；即使 `SkinItem` 被刷新、清理、禁用/启用，或旧的普通皮肤 icon 异步加载返回，也不允许改变 Restore icon。

## 影响范围

- 运行时代码：`Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs`。
- 相关边界：`ShopPanel_SkinPart.BindSkinItem(...)` 仍负责 Restore 行绑定，`InfiniteScrollView` 仍可能在刷新或槽位切换时调用 `SkinItem.Clear()`。
- 本次未修改 `ShopPanel_SkinPart.cs`、`InfiniteScrollView.cs`、`SkinItem_Revert.prefab`、`ShopPanel.prefab`、`GameData`、Luban 配置、Actor 绑定、广告、金币、内购、analytics 或平台 SDK。

## 原行为基线

- `BindRestore(...)` 已通过 `ResetBindingVisuals(..., resetIconVisuals: false)` 避免绑定 Restore 行时清理 icon。
- `Clear()` 原本依赖当前 `_state == SkinItemState.Restore` 判断是否跳过 icon 清理。
- 普通皮肤 icon 异步加载已有 `_iconLoadVersion` 和 `data == skin` 检查，但 Restore icon 保护逻辑分散在调用参数和当前状态判断中。

## 修改后行为

- `SkinItem` 首次解析 `icon` 引用时缓存 prefab 初始 `sprite` 和 `enabled` 状态。
- `BindRestore(...)` 进入 `_preserveRestoreIconVisuals` 保护态，重置其他绑定视觉时不处理 icon，并恢复 prefab 初始 icon。
- `Clear()` 在 Restore 状态或 Restore 保护态下只清理数据、标题、选中、价格和交互状态，不清空、不禁用、不替换 icon。
- `OnEnable()` / `OnDisable()` 在 Restore 保护态下恢复 prefab 初始 icon，降低刷新、隐藏或复用过程中残留错误 icon 状态的风险。
- `LoadIconAsync(...)` 在开始前和异步返回后都会检查 Restore 保护态；槽位进入 Restore 后，普通皮肤 icon 不会回写到 Restore icon 上。
- 普通皮肤绑定会关闭 Restore icon 保护，并继续按原逻辑清理和加载普通皮肤 icon。

## 数据、资源、UI、保存和事件流

- Restore icon 保护只影响单个 `SkinItem` 的 UI Image 状态，不读取或写入 `DB.GameData`。
- 普通皮肤 icon 仍通过 Bear `ResourceManager` 加载；本次没有新增资源路径或资源打包规则。
- Restore 点击后的保存清理逻辑仍由 `ShopPanel_SkinPart.ClearCurrentSkinPart(...)` 负责；本次没有新增 `GameData.Save()` 或 `ActorSkinSelectionChangedEvent`。
- `SkinItem_Revert.prefab` 继续作为 Restore 行 icon 视觉来源；代码只恢复 prefab 初始 icon，不在运行时指定新资源。

## 验证记录

- 静态检查确认 Restore icon 保护覆盖 `BindRestore()`、`Clear()`、`OnEnable()`、`OnDisable()` 和 `LoadIconAsync()` 回写路径。
- 静态扫描确认本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、`Instantiate` 或平台 SDK 链路。
- `git diff --check -- Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs` 通过；仅提示工作区下次 Git 接触该文件时会做 LF/CRLF 规范化。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 0 error / 1114 warnings；warnings 为当前工程既有全局 warning。

## 未验证风险

- 尚未执行 Unity PlayMode，仍需验证打开 ShopPanel 后 Restore 首行 icon 在筛选切换、列表刷新、快速滚动复用、点击 Restore、关闭重开过程中始终保持 prefab 初始图标。
- 尚未执行 Android/iOS 横屏 smoke，仍需验证触摸快速滚动和刷新过程没有 Restore icon 闪烁、消失或被普通皮肤图标覆盖。

## 回滚路径

回滚 `SkinItem.cs` 中 `_preserveRestoreIconVisuals`、初始 icon 快照、`RestoreInitialIconVisuals()`、`ShouldPreserveRestoreIconVisuals()` 以及相关调用，即可恢复为仅依赖 `resetIconVisuals: false` 和 `_state == Restore` 的旧保护方式。
