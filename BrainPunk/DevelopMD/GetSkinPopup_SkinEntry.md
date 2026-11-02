# GetSkinPopup Skin Entry

## 2026-06-17 Planning Record

### Requirement / Background

`GetSkinPopup` needs to support the skin-acquisition popup flow: close without side effects, show the acquired skin name when skin data is available, and route the yellow action button to the existing ShopPanel Skin section.

### Affected Files And Systems

- `Assets/Game/UI/GetSkinPopup/Scripts/GetSkinPopup.cs`: popup button handling, optional `Config.Skin` display data, localized skin-name refresh.
- `Assets/Game/UI/GetSkinPopup/Scripts/GetSkinPopup.Generated.cs`: generated binding file; must not be hand-edited.
- `Assets/Game/UI/GetSkinPopup/Resources/GetSkinPopup.prefab`: popup serialized UI fields.
- `Assets/Game/UI/ShopPanel/Scripts/ShopPanel.cs`: normal ShopPanel factory plus Skin-entry helper and switch-group resolution.
- `Assets/Game/UI/ShopPanel/Scripts/ShopPanelSwitchGroup.cs`: existing owner of Shop/Skin exclusive tab state.
- `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`: existing Skin page owner; must continue to own skin catalog, ownership, unlock, apply/clear, preview, save, resource, and Actor refresh behavior.
- `specs/020-getskin-popup-entry/`: feature spec, plan, research, data model, UI-flow contract, and quickstart validation.

### Existing Behavior Baseline

- Normal `ShopPanel.Create()` opens ShopPanel using the existing default Shop section.
- ShopPanel Skin owns skin list, ownership, unlock, apply/clear, preview, and Actor skin refresh behavior.
- ShopPanel owns purchase, rewarded ad, restore purchase, reward-tip-on-close, sold-out, and coin reward behavior.
- Existing Skin documentation records that Skin behavior still needs PlayMode, Android/iOS, and resource-package validation for broader Skin flows.

### Planned / New Behavior

- `GetSkinPopup` close button destroys only the popup.
- `GetSkinPopup` yellow button destroys the popup and opens ShopPanel through a Skin-section entry.
- `GetSkinPopup` displays a localized acquired skin name when `Config.Skin` data is available, falls back to configured `SkinName` when localization is missing, and leaves the name blank when skin data is missing.
- `GetSkinPopup.Create()` opens the popup without skin data and intentionally leaves the acquired skin name blank.
- The popup remains navigation/display-only and does not grant, purchase, equip, save, show ads, send analytics, or load skin resources.

### Data / Resource / Event Flow

- `Config.Skin` is passed to the popup as display-only data.
- Skin-name localization uses the configured `SkinName` key/value and falls back to raw `SkinName`.
- The popup uses `UIManager` for UI open/destroy and delegates Skin section selection to ShopPanel/ShopPanelSwitchGroup.
- No new DB, save, resource package, analytics event, platform SDK, or hot reload flow is introduced.

### Verification Planned

- Static compile: `dotnet build Assembly-CSharp.csproj --no-restore`.
- Code review: `GetSkinPopup.Create`, close, and confirm paths use `UIManager.Instance?.` so missing UIManager does not throw from the new popup entry code.
- Code review: `ShopPanel.SelectSkinPart()` resolves `ShopPanelSwitchGroup` and uses `switchGroup?.SelectSkin()`, so a prefab variant without the switch group can still open ShopPanel while skipping Skin auto-selection.
- Manual or PlayMode checks:
  - popup close does not open ShopPanel;
  - popup confirm opens ShopPanel with Skin selected;
  - normal ShopPanel entry still opens Shop;
  - repeated popup open/close/confirm does not duplicate callbacks;
  - valid skin data shows localized/fallback name;
  - missing skin data leaves name blank;
  - Shop/Skin tab switching still works.
- Android/iOS landscape smoke remains required when device access is available.

### Remaining Risks / Unverified Gaps

- This planning pass did not run Unity PlayMode or mobile device validation.
- Static compile and manual/runtime validation results need to be filled after implementation verification.
- Existing broader Skin module pending validations still apply to ShopPanel Skin browsing, resource-package availability, apply/clear, and Actor refresh flows.
- If a prefab variant lacks `ShopPanelSwitchGroup`, the planned degraded behavior is to open ShopPanel and skip Skin auto-selection rather than block the UI.

### Rollback Path

Revert `GetSkinPopup` button/name handling and the ShopPanel Skin-entry helper, then remove the `specs/020-getskin-popup-entry/` planning artifacts and this document if the feature is abandoned.

## 2026-06-17 Implementation Record

### Requirement / Background

The implementation pass completed the GetSkinPopup Skin entry workflow from `specs/020-getskin-popup-entry/tasks.md`: popup close, popup confirm to ShopPanel Skin, display-only skin name handling, regression boundaries, and local verification evidence.

### Affected Files And Systems

- `Assets/Game/UI/GetSkinPopup/Scripts/GetSkinPopup.cs`: implemented popup button listeners, listener cleanup, display-only `Config.Skin` input, localized/fallback/blank name refresh, popup-layer creation, and no-data `Create()` overload.
- `Assets/Game/UI/ShopPanel/Scripts/ShopPanel.cs`: verified existing `CreateSkin(...)`, `SelectSkinPart()`, `ResolveSwitchGroup()`, and `IsSkinPartSelected()` support the Skin entry without changing normal `Create(...)`.
- `Assets/Game/UI/ShopPanel/Scripts/ShopPanelSwitchGroup.cs`: verified existing `SelectSkin()` delegates to the tab controller state path.
- `Assets/Game/UI/GetSkinPopup/Resources/GetSkinPopup.prefab`: verified serialized references for `NoBtn`, `UICommonYellow01Btn`, and `UiHuodepifu01Txt`.
- `specs/020-getskin-popup-entry/quickstart.md`: updated with missing `UIManager` and missing `ShopPanelSwitchGroup` degraded-path validation notes.
- `specs/020-getskin-popup-entry/tasks.md`: implementation tasks were marked complete after code review, compile, and recorded validation notes.

### Existing Behavior Baseline

- Normal `ShopPanel.Create(...)` still opens through the existing ShopPanel creation path and does not call `SelectSkinPart()`.
- Existing ShopPanel Skin ownership, unlock, apply, clear, preview, save, resource, and Actor refresh behavior remains owned by `ShopPanel_SkinPart`; that file was inspected but not changed in this pass.
- Generated UI binding files were inspected and not edited manually.

### New / Confirmed Behavior

- `GetSkinPopup.Create(Config.Skin data)` opens on `UILayer.Popup`, stores optional display-only skin data, and refreshes the acquired skin name.
- `GetSkinPopup.Create()` calls `Create(null)`, supporting the no-data popup path with a blank skin-name text.
- `NoBtn` closes only the current popup through `UIManager.Instance?.DestroyUI(this)`.
- `UICommonYellow01Btn` closes the popup and calls `ShopPanel.CreateSkin()`.
- `OnClose()` unsubscribes both popup button callbacks.
- Skin-name display uses I2 localization for `SkinName`, falls back to raw `SkinName`, and leaves the text blank when no skin data is present.

### Data / Resource / Event Flow

- `Config.Skin` is read only for display text. No skin ownership, save, reward, ad, purchase, analytics, or resource-loading flow was added to `GetSkinPopup`.
- The new popup entry uses `UIManager` and existing ShopPanel tab-selection code only.
- Missing `UIManager.Instance` is guarded with null-conditional access in `GetSkinPopup.Create`, close, and confirm paths.
- Missing `ShopPanelSwitchGroup` is guarded by `switchGroup?.SelectSkin()` in `ShopPanel.SelectSkinPart()`, so a prefab variant can still open ShopPanel while skipping Skin auto-selection.

### Verification Performed

- Static scan found no `OnValidate`, `Reset`, `ExecuteAlways`, `ExecuteInEditMode`, `DB`, `Save`, `RemoteConfig`, `Instantiate`, `PlayerPrefs`, or `StreamingAssets` references in `GetSkinPopup.cs` or `ShopPanelSwitchGroup.cs`.
- Prefab binding scan confirmed `GetSkinPopup.prefab` serializes `NoBtn`, `UICommonYellow01Btn`, and `UiHuodepifu01Txt`.
- `dotnet build Assembly-CSharp.csproj --no-restore` from `BrainPunk/` completed with 0 errors and 1110 existing warnings.
- `ShopPanel_SkinPart.cs` was inspected for ownership-boundary evidence and was not modified.
- `.gitignore` already contains Unity/C# generated-file patterns such as `BrainPunk/Library/`, `BrainPunk/Temp/`, `BrainPunk/[Oo]bj/`, `BrainPunk/[Bb]in/`, `*.user`, and `*.suo`.

### Remaining Risks / Unverified Gaps

- Unity PlayMode validation was not executed in this terminal session. Manual checks for popup close, confirm-to-Skin, repeated open/close/confirm, normal ShopPanel entry, and tab switching still need in-editor confirmation.
- Android/iOS landscape touch smoke was not executed in this terminal session.
- SpecKit prerequisite scripts still fail on branch naming because the current branch is `feature/1.8.0_common`, while the scripts expect a numbered feature branch. Implementation proceeded from the explicit 020 plan referenced by `AGENTS.md`.
- Existing broader Skin module pending validations still apply to resource-package availability, Skin apply/clear, and Actor refresh flows.

### Rollback Path

Revert `Assets/Game/UI/GetSkinPopup/Scripts/GetSkinPopup.cs` to remove popup button binding, display-only `Config.Skin` handling, popup-layer creation, and the no-data overload. If needed, revert the feature planning artifacts under `specs/020-getskin-popup-entry/` and this document.

## 2026-06-23 Icon Image 获取与加载记录

### 需求背景

`GetSkinPopup` 需要参考 `SkinItem` 的做法完善获得皮肤弹窗里的 icon UI Image 获取和刷新逻辑，使弹窗在有 `Config.Skin` 数据时能从同名子节点兜底获取 `Image`，并按皮肤 `TextureResources` 显示获得皮肤图标。

### 影响范围

- 运行时代码：`Assets/Game/UI/GetSkinPopup/Scripts/GetSkinPopup.cs`。
- 资源/Prefab 证据：`Assets/Game/UI/GetSkinPopup/Resources/GetSkinPopup.prefab` 中存在名为 `icon` 的子对象，且该对象挂有 Unity UI `Image` 组件。
- 参考实现：`Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs` 已使用 `FindChildComponent<Image>("icon")`、`TextureResources`、Bear `ResourceManager` 和 `_iconLoadVersion` 处理列表图标。
- 本次未修改 `GetSkinPopup.Generated.cs`、`GetSkinPopup.prefab`、ShopPanel、SkinItem、Luban 配置、资源打包配置、广告、内购、analytics 或平台 SDK。

### 原行为基线

- `GetSkinPopup` 已有 `skinData` 和皮肤名称刷新逻辑。
- 当前工作区中的 `GetSkinPopup.cs` 已存在获得皮肤后写入 `DB.GameData.OwnedSkin` 并 `Save()` 的 `GrantSkin(...)` 路径；该路径不是本次 icon 修改新增。
- 修改前脚本没有按 `SkinItem` 的方式解析 `icon` 子节点，也没有根据 `TextureResources` 加载获得皮肤图标。

### 修改后行为

- `GetSkinPopup` 的 `icon` 字段明确使用 `UnityEngine.UI.Image`，移除 `System.Drawing.Image` 误用风险。
- `OnCreate()` / `OnOpen()` / `SetSkinData(...)` 会调用 `ResolveReferences()`，当序列化字段为空时从名为 `icon` 的子节点获取 UI Image。
- `RefreshSkinIcon()` 每次刷新会先递增 `_iconLoadVersion`，清空旧 `sprite` 并禁用 Image；缺少 skin 数据或 `TextureResources` 时保持空图标。
- 有效 skin 数据会通过 Bear `ResourceManager` 按 `Assets/Game/ArtSrc/Skins/UI/{TextureResources}.png` 加载 `Sprite`，成功后写入 `icon.sprite`、启用 Image，并调用 `SetNativeSize()`。
- 异步加载返回时会校验弹窗仍存在、版本号一致、当前 `skinData` 仍是同一对象、`icon` 仍有效，避免旧加载结果覆盖新弹窗或新皮肤数据。
- `OnClose()` 会递增 `_iconLoadVersion`，降低弹窗关闭后旧异步加载继续回写的风险。

### 数据、资源、UI、保存和事件流

- UI 流：`Config.Skin.TextureResources` -> Bear `ResourceManager.CanLoad<Sprite>()` -> `LoadAsync<Sprite>()` -> `icon.sprite`。
- 资源路径沿用 SkinItem 当前 UI 图标目录 `Assets/Game/ArtSrc/Skins/UI`；本次没有新增资源、移动资源或修改打包规则。
- 本次 icon 修改本身不新增事件、广告、内购、analytics、平台 SDK 或 RemoteConfig 链路。
- 当前 `GetSkinPopup.cs` 仍含已有 `GrantSkin(...)` 存档写入逻辑；该行为需要按现有需求另行确认，不在本次 icon 获取改动范围内。

### 验证记录

- 静态检查确认 `GetSkinPopup.cs` 不再引用 `System.Drawing`，并新增 `UnityEngine.UI`、`Cysharp.Threading.Tasks` 以支持 UI Image 与异步图标加载。
- 静态扫描确认本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`RemoteConfig`、`Instantiate`、`PlayerPrefs` 或 `StreamingAssets`。
- Prefab 文本检查确认 `GetSkinPopup.prefab` 存在名为 `icon` 的子对象，且其 `MonoBehaviour` 使用 Unity UI Image 脚本 guid。
- `git diff --check -- Assets/Game/UI/GetSkinPopup/Scripts/GetSkinPopup.cs` 通过；仅提示 Git 下次接触该文件时会做 LF/CRLF 规范化。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 0 error / 96 warnings；warnings 属于当前工程既有范围。

### 未验证风险

- 尚未执行 Unity PlayMode；仍需验证打开 `GetSkinPopup.Create(skin)` 后 icon 能正确显示，`Create(null)` 时 icon 清空，连续打开不同皮肤不会出现旧图标回写。
- 尚未执行资源包或真机验证；需要确认目标 `TextureResources` 在 Android/iOS 横屏运行包中可被 Bear `ResourceManager` 加载。
- 当前路径只实现 `Assets/Game/ArtSrc/Skins/UI/{TextureResources}.png` 候选；如果未来配置值直接是完整资源路径，代码可支持 `Assets/` 开头路径，但仍需具体资源验证。

### 回滚路径

回滚 `GetSkinPopup.cs` 中 `icon` 字段、`ResolveReferences()`、`RefreshSkinIcon()`、`LoadIconAsync()`、资源路径解析、`_iconLoadVersion` 以及相关调用点，即可恢复到只刷新皮肤名称、不处理获得皮肤图标的旧行为。

## 2026-06-24 Body Type 跳转记录

### 需求背景

`GetSkinPopup` 点击黄色按钮跳转到 ShopPanel 时，需要在打开 Skin 大页后进一步切到 SkinPart 的 Body Type 筛选，方便玩家直接查看身体/衣服类皮肤。

### 影响范围

- `Assets/Game/UI/GetSkinPopup/Scripts/GetSkinPopup.cs`：黄色按钮跳转调用改为 `ShopPanel.CreateSkin(SkinPart.Body)`。
- `Assets/Game/UI/ShopPanel/Scripts/ShopPanel.cs`：新增 `CreateSkin(SkinPart targetSkinPart, ...)` 和内部转发入口。
- `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`：新增公开 `SelectSkinPart(SkinPart skinPart)`，复用现有筛选按钮组和列表刷新路径。
- 本次未修改 `GetSkinPopup.Generated.cs`、`GetSkinPopup.prefab`、`ShopPanel.prefab`、Skin 配置、存档字段、广告、内购、analytics、资源加载或 Actor 关卡绑定逻辑。

### 原行为基线

- `GetSkinPopup.OpenSkinPanel(...)` 原先关闭弹窗后调用 `ShopPanel.CreateSkin()`，只能切到 ShopPanel 的 Skin 大页。
- `ShopPanel_SkinPart` 内部已有 `ShowBody()` / `SetFilter(SkinPart.Body)` 和 `GameObjectToggleGroup.SelectedIndex == 2` 到 `SkinPart.Body` 的映射，但外部入口没有稳定 API 可以指定子筛选。
- Skin 页筛选列表、Restore 行、试穿、购买、授予和保存逻辑均由 `ShopPanel_SkinPart` 持有。

### 修改后行为

- `ShopPanel.CreateSkin()` 旧入口保持不变，仍只打开 Skin 大页。
- 新增 `ShopPanel.CreateSkin(SkinPart targetSkinPart, ...)`：先复用既有 ShopPanel 创建和 Skin 大页切换，再调用 `ShopPanel_SkinPart.SelectSkinPart(targetSkinPart)`。
- `ShopPanel_SkinPart.SelectSkinPart(...)` 会优先通过 `filterToggleGroup.SelectIndex(...)` 同步 UI toggle；如果筛选按钮组缺失或无法完成选择，则退化为直接 `SetFilter(...)` 刷新对应部位列表。
- `GetSkinPopup` 黄色按钮现在指定 `SkinPart.Body`，因此弹窗跳转 ShopPanel 后应显示 Body Type 列表。

### 数据、UI、保存和事件流

- 本次只改变 UI 入口定位和 SkinPart 筛选状态。
- 不新增或修改 `DB.GameData` 写入，不触发皮肤授予、试穿、购买、广告、内购、Actor 刷新事件或资源加载链路。
- `GetSkinPopup.cs` 中既有 `GrantSkin(...)` 路径不是本次新增；本次没有扩大该保存路径的调用边界。

### 验证记录

- `git diff --check -- Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs Assets/Game/UI/ShopPanel/Scripts/ShopPanel.cs Assets/Game/UI/GetSkinPopup/Scripts/GetSkinPopup.cs` 通过，仅有当前工作区 LF/CRLF 提示。
- 新增 diff 风险词扫描未发现本次新增行包含 `DB`、`Save()`、`RemoteConfig`、`PlayerPrefs`、`StreamingAssets`、`OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`Instantiate`、广告、内购或 analytics 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 第二次执行通过，结果为 0 error / 152 warnings；warnings 属于当前工程既有范围。
- 首次编译命令在 124 秒超时；确认并只结束匹配该命令行的残留 `dotnet build` 进程后重跑通过。

### 未验证风险

- 尚未执行 Unity PlayMode；仍需验证 `GetSkinPopup` 黄色按钮关闭弹窗、打开 ShopPanel、切到 Skin 大页并选中 Body Type toggle。
- 尚未执行 Android/iOS 横屏 smoke；仍需验证弹窗跳转和 Body Type 筛选在触摸路径下无可见异常。
- 若某个 ShopPanel prefab 变体缺少 `ShopPanelSwitchGroup` 或 SkinPart 筛选按钮组，代码会尽量退化为直接刷新 Body 列表，但实际视觉状态仍需对应 prefab 验证。

### 回滚路径

回滚 `GetSkinPopup.OpenSkinPanel(...)` 中的 `ShopPanel.CreateSkin(SkinPart.Body)` 为 `ShopPanel.CreateSkin()`，删除 `ShopPanel.CreateSkin(SkinPart, ...)`、`ShopPanel.SelectSkinPart(SkinPart)`、`ShopPanel_SkinPart.SelectSkinPart(SkinPart)` 和相关筛选 index helper，即可恢复为只打开 Skin 大页、不指定 Body Type 的旧行为。

## 2026-07-07 GetSkinTransformPopup 跳转逻辑补全记录

### 需求背景

`GetSkinTransformPopup` 脚本注释要求参考 `GetSkinPopup`：关闭按钮直接销毁当前弹窗，`goBtn` 跳转到 ShopPanel 并打开对应皮肤栏。本次只补齐该弹窗的按钮逻辑，不修改已有注释文本。

### 影响范围

- 运行时代码：`Assets/Game/UI/GetSkinPopup/Scripts/GetSkinTransformPopup/GetSkinTransformPopup.cs`。
- 资源证据：`Assets/Game/UI/GetSkinPopup/Resources/GetSkinTransformPopup.prefab` 已绑定生成字段 `CloseBtn`，但当前未序列化手写字段 `goBtn`。
- 复用系统：`ShopPanel.CreateSkin(SkinPart.Body)`、`ShopPanel_SkinPart.SelectSkinPart(SkinPart)` 和既有 `UIManager` 弹窗销毁路径。
- 本次未修改 `GetSkinTransformPopup.Generated.cs`、prefab、ShopPanel、Skin 配置、存档字段、资源加载、广告、内购、analytics 或平台 SDK。

### 原行为基线

- `GetSkinTransformPopup.Create()` 只打开 Popup 层 UI。
- `OnCreate()` 仅保留 TODO 注释，没有绑定关闭按钮或跳转按钮。
- `OnClose()` 没有解绑按钮回调。

### 修改后行为

- `CloseBtn` 在 `OnCreate()` 绑定 `ClosePopup(...)`，点击后通过 `UIManager.Instance?.DestroyUI(this)` 销毁当前弹窗。
- `goBtn` 优先使用序列化字段；字段为空时通过 `GetComponentInChildren<CommonYellowBtn>(true)` 从弹窗层级兜底获取。
- `goBtn.Btn` 点击后先销毁当前弹窗，再调用 `ShopPanel.CreateSkin(SkinPart.Body)` 打开 ShopPanel Skin 页并定位 Body Type。
- `OnClose()` 会解绑 `CloseBtn` 和 `goBtn.Btn` 回调，避免重复打开时回调叠加。
- `Create()` 改为使用 `UIManager.Instance?.OpenUI<...>()`，缺少 UIManager 时不直接空引用崩溃。

### 数据、UI、保存和事件流

- 本次只新增本地 UI 按钮绑定、弹窗销毁和 ShopPanel Skin 入口定位。
- 不新增 DB、Save、RemoteConfig、PlayerPrefs、StreamingAssets、资源加载、全局事件、广告、内购、analytics 或平台 SDK 链路。
- `SkinPart.Body` 入口沿用既有 Body Type 筛选映射；实际视觉状态仍依赖 ShopPanel prefab 与 SkinPart 筛选按钮组。

### 验证记录

- 静态读取确认 `GetSkinTransformPopup.prefab` 已序列化 `CloseBtn`，未序列化手写字段 `goBtn`，因此运行时代码保留子节点兜底查找。
- 静态扫描 `GetSkinTransformPopup.cs` 未发现新增 `DB`、`Save()`、`RemoteConfig`、`PlayerPrefs`、`StreamingAssets`、`OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`Instantiate`、广告、内购或 analytics 链路。
- `git diff --check -- Assets/Game/UI/GetSkinPopup/Scripts/GetSkinTransformPopup/GetSkinTransformPopup.cs` 通过。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 97 warnings；warnings 属于当前工程既有范围。

### 未验证风险

- 尚未执行 Unity PlayMode；仍需验证 `GetSkinTransformPopup.Create()` 打开后关闭按钮能销毁弹窗，`goBtn` 能关闭弹窗并打开 ShopPanel Body Type。
- 尚未执行 Android/iOS 横屏 smoke；触摸路径和 ShopPanel 打开后的视觉选中状态仍需真机或编辑器补验。
- 如果 prefab 未来包含多个 `CommonYellowBtn`，当前兜底查找会取第一个子节点匹配项；当前 prefab 静态证据仅发现一个黄色按钮实例。

### 回滚路径

回滚 `GetSkinTransformPopup.cs` 中新增的 `Config.Game` 引用、按钮绑定/解绑、`ClosePopup(...)`、`OpenSkinPanel(...)`、`ResolveReferences()` 和 `Create()` 空保护，即可恢复为只打开弹窗、不处理按钮行为的旧状态。

## 2026-07-07 GetSkinTransformPopup 名称与图标展示记录

### 需求背景

在已补齐关闭和跳转逻辑后，继续强调 `GetSkinTransformPopup` 需要设置皮肤 `skinName` 和 `icon`：弹窗收到 `Config.Skin` 后应显示皮肤名称，并按 `TextureResources` 加载对应 UI 图标。

### 影响范围

- 运行时代码：`Assets/Game/UI/GetSkinPopup/Scripts/GetSkinTransformPopup/GetSkinTransformPopup.cs`。
- 资源/Prefab 证据：`Assets/Game/UI/GetSkinPopup/Resources/GetSkinTransformPopup.prefab` 存在名为 `icon` 的 Unity UI `Image` 子节点，`skinName_txt` 已通过生成字段绑定为 `SkinNameTxt`。
- 复用系统：`SkinUtils.ResolveIconLocation(Skin)`、`SkinUtils.LoadIconAsync(...)`、I2 `LocalizationManager.GetTranslation(...)` 和 Bear.Logger。
- 本次未修改 `GetSkinTransformPopup.Generated.cs`、prefab、ShopPanel、Skin 配置、存档字段、广告、内购、analytics 或平台 SDK。

### 原行为基线

- `GetSkinTransformPopup` 已有关闭和跳转逻辑，但没有保存 `Config.Skin` 展示数据。
- 脚本中新增过 `icon` 字段，但引用类型为 `System.Drawing.Image`，不适合 Unity UI Image。
- `SkinNameTxt` 没有在运行时根据 `Config.Skin.SkinName` 设置文本。
- `icon` 没有根据 `Config.Skin.TextureResources` 加载和设置 Sprite。

### 修改后行为

- `GetSkinTransformPopup` 现在保存展示用 `Config.Skin skinData`，`Create(Skin data)` 会打开弹窗并调用 `SetSkinData(data)`。
- `Create()` 旧入口保留，委托到 `Create(null)`；无皮肤数据时会清空 `SkinNameTxt` 和 `icon.sprite`，并禁用 `icon`。
- `RefreshSkinName()` 使用 `SkinNameTxt` 显示 I2 本地化后的 `SkinName`，缺少翻译时回退到原始 `SkinName`。
- `icon` 字段改为 Unity UI `Image`，字段为空时从名为 `icon` 的子节点兜底获取。
- `RefreshSkinIcon()` 会递增 `_iconLoadVersion`，先清空旧图标；有效 `TextureResources` 会通过 `SkinUtils` 解析并异步加载 Sprite。
- 异步加载回写前校验弹窗仍存在、版本号一致、当前 `skinData` 仍是同一对象且 `icon` 仍有效，避免旧加载结果覆盖新数据。
- `OnClose()` 递增 `_iconLoadVersion`，降低关闭后旧异步回写风险。

### 数据、资源、UI、保存和事件流

- UI 名称链路：`Config.Skin.SkinName` -> I2 Localization -> `SkinNameTxt.text`。
- UI 图标链路：`Config.Skin.TextureResources` -> `SkinUtils.ResolveIconLocation(Skin)` -> Bear `ResourceManager.LoadAsync<Sprite>()` -> `icon.sprite`。
- 本次只新增该弹窗的展示用资源读取，不授予皮肤、不穿戴皮肤、不写入 DB、不保存、不派发 Actor skin 事件。
- 不新增 RemoteConfig、PlayerPrefs、StreamingAssets、广告、内购、analytics 或平台 SDK 链路。

### 验证记录

- 静态读取确认 `GetSkinTransformPopup.prefab` 中 `icon` 节点挂有 Unity UI `Image` 组件，`skinName_txt` 已绑定到 `SkinNameTxt`。
- 静态扫描确认 `GetSkinTransformPopup.cs` 不再引用 `System.Drawing`，未新增 `DB`、`Save()`、`RemoteConfig`、`PlayerPrefs`、`StreamingAssets`、`OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`Instantiate`、广告、内购或 analytics 链路。
- `git diff --check --no-index -- NUL Assets/Game/UI/GetSkinPopup/Scripts/GetSkinTransformPopup/GetSkinTransformPopup.cs` 未发现空白错误，仅有 LF/CRLF 工作区提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 97 warnings；warnings 属于当前工程既有范围。

### 未验证风险

- 尚未执行 Unity PlayMode；仍需验证 `GetSkinTransformPopup.Create(skin)` 打开后 `SkinNameTxt` 和 `icon` 能正确显示，`Create(null)` 时名称和图标为空。
- 尚未执行 Android/iOS 横屏 smoke；触摸路径、图标资源加载和 ShopPanel Body Type 跳转仍待真机或编辑器补验。
- 尚未执行资源包完整性验证；需要确认目标 `TextureResources` 在运行包中可由 Bear `ResourceManager` 加载。

### 回滚路径

回滚 `GetSkinTransformPopup.cs` 中 `Config.Skin skinData`、`icon` Unity UI 字段、`Create(Skin)`、`SetSkinData(...)`、`RefreshSkinName()`、`RefreshSkinIcon()`、`LoadIconAsync(...)`、`FindChildComponent(...)` 和 `_iconLoadVersion` 相关改动，即可恢复为只处理关闭和跳转、不显示皮肤名称与图标的状态。

## 2026-07-07 GetSkinTransformPopup 关闭回调接入记录

### 需求背景

`PostcardPopup` 整页完成后已经通过 `CommonGetRewardPopup` 发放 Chest 奖励；当奖励配置包含 Skin 时，需要在奖励弹窗关闭后继续打开 `GetSkinTransformPopup` 展示本次获得皮肤，并在转换弹窗关闭后再恢复 Postcard 后续自动切页流程。

### 影响范围

- 运行时代码：`Assets/Game/UI/GetSkinPopup/Scripts/GetSkinTransformPopup/GetSkinTransformPopup.cs`。
- 调用方：`Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs` 通过 `GetSkinTransformPopup.Create(skin, callback)` 等待转换弹窗关闭。
- 本次未修改 `GetSkinTransformPopup.Generated.cs`、prefab、ShopPanel、Skin 配置、DB/save 字段、资源打包、广告、内购、analytics 或平台 SDK。

### 原行为基线

- `GetSkinTransformPopup.Create(Skin data)` 只负责打开弹窗并设置展示用 Skin 数据。
- 关闭按钮和 goBtn 都会销毁当前弹窗，但没有对外关闭回调，调用方无法等待该弹窗关闭后继续自己的流程。
- `GetSkinTransformPopup` 已经是展示型弹窗，不授予、不穿戴、不保存皮肤。

### 修改后行为

- `GetSkinTransformPopup` 新增实例级 `_closeCallback`，由 `Create(Skin data, Action callback = null)` 传入。
- `OnClose()` 在解绑按钮并递增图标加载版本后调用 `InvokeCloseCallback()`；回调会先取出并置空，避免同一关闭生命周期重复触发。
- 旧的 `Create()` 无参入口仍保留，委托到 `Create(null, null)`。
- 皮肤名称、图标展示、关闭按钮、goBtn 跳转 ShopPanel Body Type 的既有行为保持不变。

### 数据、UI、保存和事件流

- 关闭回调只用于串联外部 UI 流程，不参与 Skin 发放、穿戴、保存、资源加载、广告、内购、analytics 或平台 SDK。
- Skin 所有权仍由 `CommonGetRewardPopup` 发放阶段调用 `Bag.GetNewSkin(...)`，转换弹窗只展示已获得皮肤。
- goBtn 路径仍会销毁转换弹窗并调用 `ShopPanel.CreateSkin(SkinPart.Body)`；调用方等待链路会在弹窗关闭后继续。

### 验证记录

- `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs Assets/Game/UI/GetSkinPopup/Scripts/GetSkinTransformPopup/GetSkinTransformPopup.cs` 通过，仅有工作区 LF/CRLF 转换提示。
- 静态扫描确认两个改动文件未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`RemoteConfig`、`PlayerPrefs`、`StreamingAssets`、广告、内购、analytics、平台 SDK 或全局事件。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 97 warnings；warnings 属于当前工程既有输出范围。

### 未验证风险

- 尚未执行 Unity PlayMode；需要验证 Postcard 奖励弹窗关闭后能打开 `GetSkinTransformPopup`，转换弹窗关闭后 Postcard 自动切页继续。
- 尚未验证 goBtn 打开 ShopPanel 后，PostcardPopup 在下层继续切页是否符合产品预期。
- 尚未执行 Android/iOS 横屏 smoke；触摸、遮罩、UI 栈和 ShopPanel 跳转表现仍待补验。

### 回滚路径

回滚 `GetSkinTransformPopup.cs` 中 `_closeCallback`、`SetCloseCallback(...)`、`InvokeCloseCallback()`、`OnClose()` 回调调用和 `Create(Skin, Action)` 参数；同时删除 `PostcardPopup_PageCompleteProcess.cs` 中等待转换弹窗关闭的调用，即可恢复为转换弹窗不参与 Postcard 后续流程串联的旧状态。

## 2026-07-08 GetSkinTransformPopup 按 SkinPart 跳转记录

### 需求背景

`GetSkinTransformPopup` 的 goBtn 原先固定调用 `ShopPanel.CreateSkin(SkinPart.Body)`，导致转换弹窗展示 Back、Face 或 FullBody 皮肤时仍跳转到 Body Type。本次要求改为从当前弹窗的 `skinData` 中读取对应部位后跳转。

### 影响范围

- 运行时代码：`Assets/Game/UI/GetSkinPopup/Scripts/GetSkinTransformPopup/GetSkinTransformPopup.cs`。
- 复用入口：`ShopPanel.CreateSkin(SkinPart targetSkinPart)` 和既有 `ShopPanel_SkinPart.SelectSkinPart(SkinPart)`。
- 数据来源：`Config.Skin.SkinPart`，字段类型为 `Config.Game.SkinPart`。
- 本次未修改 `GetSkinTransformPopup.Generated.cs`、prefab、ShopPanel、Skin 配置、DB/save、资源加载、广告、内购、analytics、RemoteConfig 或平台 SDK。

### 原行为基线

- `GetSkinTransformPopup` 已能接收 `Config.Skin skinData` 并展示名称、图标。
- goBtn 点击路径会先销毁转换弹窗，再固定调用 `ShopPanel.CreateSkin(SkinPart.Body)`。
- 无参 `Create()` 仍允许打开无皮肤数据的转换弹窗。

### 修改后行为

- goBtn 点击路径改为 `ShopPanel.CreateSkin(skinData?.SkinPart ?? SkinPart.Body)`。
- 有有效 `skinData` 时，ShopPanel Skin 页会定位到该皮肤自己的部位 Type。
- 无 `skinData` 时继续使用 Body 作为兼容兜底，避免旧无参入口打开后点击 goBtn 行为变成空操作。
- `goBtn` 字段 Tooltip 已从固定 Body Type 描述调整为对应部位 Type，避免 Inspector 文案与当前行为冲突。

### 数据、UI、保存和事件流

- 本次只读取展示数据中的 `Config.Skin.SkinPart` 用于 UI 入口定位。
- 不改变皮肤授予、穿戴、保存、资源加载、Postcard 回调、广告、内购、analytics 或平台 SDK 链路。
- 不新增全局事件或新的 UI 状态字段。

### 验证记录

- CodeGraph 确认 `GetSkinTransformPopup.OpenSkinPanel(...)` 是本次目标路径，`Config.Skin.SkinPart` 类型为 `Config.Game.SkinPart`。
- 静态 diff 确认本次代码只改 Tooltip 文案和 goBtn 的 `ShopPanel.CreateSkin(...)` 入参。
- `git diff --check -- Assets/Game/UI/GetSkinPopup/Scripts/GetSkinTransformPopup/GetSkinTransformPopup.cs DevelopMD/GetSkinPopup_SkinEntry.md DevelopMD/已有功能设计与使用记录.md` 已执行并通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已执行，但被当前 `Temp\bin\Debug` 元数据 DLL 缺失阻断，首个错误为 `Apple.Core.dll` 缺失，未获得业务脚本编译诊断。

### 未验证风险

- 尚未执行 Unity PlayMode；需要验证 Back、Face、Body、FullBody 皮肤分别打开转换弹窗后，goBtn 能定位到对应 Type。
- 尚未执行 Android/iOS 横屏 smoke；触摸路径和 ShopPanel Type 选中视觉仍待设备或编辑器补验。
- `Create(null)` 的 goBtn 仍回退 Body；这是兼容旧入口的保守行为，如产品希望无数据时禁止跳转，需要后续明确需求。

### 回滚路径

回滚 `GetSkinTransformPopup.OpenSkinPanel(...)` 中 `skinData?.SkinPart ?? SkinPart.Body` 为 `SkinPart.Body`，并恢复 goBtn Tooltip 中固定 Body Type 的描述，即可回到原固定 Body 跳转行为。

## 2026-07-21 首次免费皮肤商店引导接入

### 需求与行为

- `GetSkinPopup` 黄色按钮现在优先调用 `Shop2SkinGuideFlow.TryStart(skinData)`；只有弹窗皮肤与 `RemoteConfig.GetFreeSkinId()` 一致、皮肤已拥有、存档未触发且当前 Gameplay Shop 目标有效时才启动首次引导。
- Flow 取得 `GuideFlowManager` 执行权后重新读取 `PlayCtrl.Instance.CurrentGamePlayPanel`，切换 PlayCtrl 到 Pause，再依次引导 Shop 按钮、Skin Toggle、目标 SkinPart Toggle 和动态目录中的目标 `skinId`。
- Flow 不接收 GamePlayPanel 参数。Shop 按钮通过当前 Panel 的只读 `RectTransform` 获取；虚拟 SkinItem 在点击前重新按 id 校验，避免槽位换绑后穿戴错误皮肤。
- 不满足资格时继续调用原 `ShopPanel.CreateSkin(...)`，保留普通黄色按钮跳转行为。

### 数据与生命周期

- 新增 `HasShopToSkinGuide` 存档属性，第一步正式显示前写入并保存；目标皮肤授予仍由 `GetSkinPopup.Create(data)` 的既有 Bag 路径负责。
- Gameplay Panel 关闭、重置、重玩、切关和 PlayCtrl 销毁会取消流程；ShopPanel 打开前的早退恢复 Playing，打开后由 ShopPanel 关闭路径恢复。
- 只复用既有 `SwitchGameStateEvent` 和 `ShopPanel.Create(...)`，不新增全局事件、资源路径、奖励或分析事件。

### 验证与风险

- C# 构建通过，结果为 `0 error / 99 warning`；CodeGraph 调用链、ShopPanel prefab 绑定和新增敏感项静态检查通过。
- Unity PlayMode、虚拟列表实际滚动位置、四个点击目标、Android/iOS 横屏触摸仍待验证。
- 详细设计、失败边界和回滚方式见 `Assets/Game/Scripts/Guide/Scripts/Flow/Shop2SkinGuideFlow功能说明.md`。
