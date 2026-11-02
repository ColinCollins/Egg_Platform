# SkinPart version 3.0 actionButton 选中态记录

## 需求背景

本次补充 `ShopPanel_SkinPart` 顶部已有 version 3.0 注释对应的运行时行为：Skin 页在不同 `filterPart` 间切换时，需要记住每个部位最近一次正选中的 `SkinItem` 数据，并让广告、金币、内购 actionButton 跟随当前部位切回对应数据。

## 影响范围

- 运行时代码：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`。
- 相关 UI：Skin 页 `Back` / `Face` / `Body` / `FullBody` 筛选，以及 `skin_free_btn`、`skin_coin_btn`、`skin_purchase_btn`。
- 本次未修改 `SkinItem.cs`、`InfiniteScrollView.cs`、prefab、Luban 配置、RemoteConfig、资源加载、广告 SDK、IAP SDK、analytics、平台配置、Actor 关卡绑定或存档字段结构。

## 原行为基线

- `ShopPanel_SkinPart` 原先只有一个全局 `_selectedSkin`。
- 切换筛选时 `RefreshItem()` 会从当前保存数据中刷新 `_selectedSkin`，未拥有但已经临时试穿的上一个部位 skin 不会作为 actionButton 数据恢复。
- 因此玩家在 Back 选中一个未拥有的广告/金币/内购皮肤后，切到 Face 再切回 Back，预览状态仍可存在，但 actionButton 可能隐藏或不再对应 Back 的未拥有 skin。

## 新行为

- `ShopPanel_SkinPart` 新增运行时字典 `_selectedSkinsByPart`，按 `SkinPart` 记录最近一次点击的普通皮肤行；该状态只存在于当前 ShopPanel Skin 页生命周期内。
- 点击未拥有且可试穿的 `Ad` / `Coin` / `Purchase` / `Special` skin 时，会同时更新临时试穿状态和该部位的记忆选中项。
- `RefreshItem()` 改为调用 `RefreshSelectedSkinForActiveFilter()`：优先恢复当前筛选部位的记忆选中项，其次恢复同部位临时试穿项，最后回退到已保存的当前皮肤。
- 切换筛选或 owned-only 过滤刷新时，`SyncActionButtonsForActiveSelection()` 会根据当前部位 `_selectedSkin` 重新显示对应 actionButton；已拥有或无有效选中项时隐藏 actionButton。
- Restore 清理当前部位时，会同步清理该部位的记忆选中项和临时试穿状态，避免切回该部位后恢复旧按钮数据。
- 已拥有皮肤应用、广告/金币/内购授予成功仍走原 `ApplyOwnedSkin(..., true)` 保存路径，保存语义不变。

## 数据、UI、保存和事件流

- 新增状态是 `ShopPanel_SkinPart` 私有运行时缓存，不写入 `DB.GameData`；2026-07-22 生命周期优化后，Skin 页签禁用会保留该状态，同一 ShopPanel 内切回时恢复，只在 ShopPanel 关闭或组件销毁时清空。
- 预览合成仍优先使用 `_transientPreviewSkins`，否则读取 `DB.GameData` 已保存字段。
- actionButton 点击仍读取 `_selectedSkin`，因此筛选切回后购买、看广告或内购会针对当前筛选部位恢复出的 skin 执行。
- `DB.GameData.Save()` 和 `ActorSkinSelectionChangedEvent` 触发条件未扩展：只在默认拥有迁移、已拥有应用、授予成功应用、Restore 清理保存字段等原有保存路径发生。

## 验证记录

- `git diff --check -- BrainPunk/Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs` 通过；仅提示工作区 LF/CRLF 规范化信息。
- 静态扫描确认本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`RemoteConfig`、`PlayerPrefs` 或 `StreamingAssets` 链路。
- 尝试执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，但当前 Unity / dotnet 进程占用 `Temp/obj` 下 Guru 相关 dll，构建失败于 `CS2012` 文件锁。
- 再尝试 `/p:BuildProjectReferences=false` 的窄构建时缺少 `Temp\bin\Debug\Assembly-CSharp-firstpass.dll`，不能作为有效编译验证。

## 未验证风险

- 尚未执行 Unity PlayMode：需要验证在 Back/Face/Body/FullBody 各自点击未拥有广告、金币、内购皮肤后，切换到其他筛选再切回时 actionButton 文案、价格和点击授予目标均恢复到对应部位。
- 尚未验证 owned-only 开关打开时，未拥有记忆选中项被过滤隐藏后 actionButton 正确隐藏，并在关闭 owned-only 后可恢复。
- 尚未执行 Android/iOS 横屏 smoke：快速切换筛选、快速点击 actionButton、关闭重开 ShopPanel 的触控表现仍需真机确认。

## 回滚路径

回滚 `ShopPanel_SkinPart.cs` 中 `_selectedSkinsByPart`、`RememberSelectedSkin(...)`、`ClearRememberedSelection(...)`、`RefreshSelectedSkinForActiveFilter(...)`、`SyncActionButtonsForActiveSelection(...)` 及相关调用点，即可恢复为只依赖全局 `_selectedSkin` 和保存态刷新 actionButton 的旧行为。
