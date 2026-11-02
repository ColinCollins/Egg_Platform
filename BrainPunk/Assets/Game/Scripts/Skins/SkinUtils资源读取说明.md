# SkinUtils 资源读取整合说明

## 需求背景

本次需求是分析 `ActorSkinBinding`、`SkinItem`、`ShopPanel_SkinPart` 中 Skin 资源读取逻辑，判断并执行可收敛部分。代码证据显示 UI 图标路径、部件 prefab 路径、`ResourceManager.CanLoad` 和 `LoadAsync` 调用分散在多个脚本中，适合集中到 `SkinUtils`。

## 影响范围

- `Assets/Game/Scripts/Skins/SkinUtils.cs`：新增 Skin 图标和部件 prefab 的路径解析、可加载性检查、异步加载公共入口。
- `Assets/Game/UI/ShopPanel/Scripts/SkinItem.cs`：列表图标继续保留 `_iconLoadVersion`、Restore icon 保护和 UI 回写，资源路径与加载改用 `SkinUtils`。
- `Assets/Game/UI/GetSkinPopup/Scripts/GetSkinPopup.cs`：获得皮肤弹窗图标继续保留弹窗生命周期版本保护和 UI 缩放，资源路径与加载改用 `SkinUtils`。
- `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`：Boxer 预览继续保留本地缓存、实例化、Layer 调整和日志，prefab 路径、可加载检查和加载改用 `SkinUtils`。
- `Assets/Game/Scripts/Level/Actor/ActorSkinBinding.cs`：关卡 Actor 绑定继续保留取消令牌、版本保护和实例挂载，prefab 可加载检查和加载改用 `SkinUtils`。
- `Assets/Game/Scripts/Level/Actor/ActorSkinSelectionResolver.cs/.meta`：2026-07-20 完整收敛后删除，保存兼容解析改由 `SkinUtils` 持有。

## 原行为基线

- `SkinItem` 和 `GetSkinPopup` 都按 `Assets/Game/ArtSrc/Skins/UI/{TextureResources}.png` 解析 UI 图标，并通过 Bear `ResourceManager` 加载 `Sprite`。
- `ShopPanel_SkinPart` 和 `ActorSkinSelectionResolver` 都按 `Assets/Game/Prefabs/Skins/{PrefabResource}.prefab` 解析部件 prefab。
- `ShopPanel_SkinPart` 和 `ActorSkinBinding` 在加载 prefab 前都会调用 `CanLoad<GameObject>`，失败时不实例化。
- UI 回写、异步版本保护、取消令牌、prefab 实例化、Boxer 预览缓存和 Restore icon 保护分别由原调用方持有。

## 整合后行为

- `SkinUtils.ResolveIconLocation(Skin)` 统一处理 `TextureResources`，完整 `Assets/` 路径原样返回，短名拼接到 Skin UI 图标目录，并用 `CanLoad<Sprite>` 校验。
- `SkinUtils.ResolvePrefabLocation(string)` 统一处理 `PrefabResource`，完整 `Assets/` 路径原样返回，短名拼接到 Skin prefab 目录。
- `SkinUtils.LoadIconAsync(...)` 和 `SkinUtils.LoadPrefabAsync(...)` 只封装 Bear `ResourceManager.LoadAsync`，不处理 UI 回写、实例化、缓存或存档。
- `SkinUtils` 统一保存值匹配和 Skin 行解析；调用方仍自行持有保存写入、UI、试穿和 Actor 实例生命周期语义。

## 数据、资源、UI、保存和事件流

- 资源链路仍是 `Config.Skin.TextureResources` 到 `Sprite`，以及 `Config.Skin.PrefabResource` 到 `GameObject` prefab。
- 本次不新增资源目录、资源文件、打包规则、YooAsset 直接调用、RemoteConfig、PlayerPrefs 或 StreamingAssets 链路。
- 本次不新增 `DB.GameData`、`Save()`、`ActorSkinSelectionChangedEvent`、广告、IAP、analytics 或平台 SDK 逻辑。
- `SkinItem`、`GetSkinPopup`、`ShopPanel_SkinPart` 和 `ActorSkinBinding` 的玩家可见 UI/预览/Actor 应用流程按原调用边界保留。

## 验证记录

- `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示；当前完整验证文件集与命令见文末 2026-07-20 记录。
- 静态扫描确认重复的 `SkinIconResourceRoot`、`SkinPrefabResourceRoot`、`ResolveTextureLocation`、`CanLoadSkinLocation`、`CanLoadBoxerPartLocation` 只剩 `SkinUtils` 内的统一入口。
- 静态风险词扫描只命中原本存在的 `Instantiate(...)` 和 `DispatchEvent(...)` 调用点，未新增保存、编辑器生命周期、RemoteConfig、PlayerPrefs 或 StreamingAssets 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:quiet /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 97 warnings。

## 未验证风险

- 尚未执行 Unity PlayMode，仍需验证 ShopPanel Skin 列表图标、GetSkinPopup 图标、Boxer 预览部件和关卡 Actor 部件显示都与整合前一致。
- 尚未执行 Android/iOS 横屏 smoke，快速滚动、快速切换皮肤、打开获得皮肤弹窗和进入关卡后的资源加载表现仍待真机确认。
- 尚未执行资源包验证，生产包中 `TextureResources` 和 `PrefabResource` 是否全部可由 Bear `ResourceManager` 加载仍待补验。

## 回滚路径

回滚 `SkinUtils.cs` 公共解析/资源入口，并恢复各调用方原本的本地实现与 `ActorSkinSelectionResolver.cs/.meta`，即可回到整合前行为。

## 2026-07-20：RandomSkinRewardPool 使用能力补充

- 按当前需求，`SkinUtils` 在资源读取职责之外，新增一组独立的配置与拥有状态工具 API：
  - `TryGetSkin(skinId, out skin)`：安全查询 Skin 配置。
  - `SkinExists(skinId)`：只判断 Skin 配置是否存在。
  - `IsSkinOwned(skinId)`：判断玩家当前是否已经拥有。
  - `CanGrantRandomRewardSkin(skinId, out skin)`：同时校验存在、未拥有，并排除 Default/Unavailable。
  - `TryGetRandomUnownedRewardSkin(...)`：按 RandomSkinRewardConfig 的正权重抽取一个可发放候选。
- `RandomSkinRewardPool.xlsx` 已注册为 Luban 表 `TbRandomSkinRewardConfig`，生成代码和 JSON 由现有 ConfigManager 文件加载链读取；`SkinUtils` 不修改配置对象。
- 随机抽取 API 只返回候选，不调用 `Bag.GetNewSkin`、不发奖励、不保存，调用方必须在同一次业务结算中缓存结果，避免重复抽取。
- 配置和 DB 查询均限制在运行态；编辑态调用返回 false，不会触发 ConfigManager/DB 初始化。
- 这次扩展是用户明确指定的工具类职责调整，因此替代上文“避免工具类承担 ConfigManager、DB 或选择语义”的旧边界；资源加载 API 本身及其调用方职责保持不变。
- 编译验证为 0 error；当前奖励池两个 Skin id 均存在且为 Special。Unity PlayMode 下的拥有过滤、权重统计和全部拥有后的空候选仍待验证。
- 若回滚，只需移除 `Config And Ownership` 区域及随机池生成/注册链；原资源解析与异步加载区域无需改动。

## 2026-07-20：Skin 保存解析与资源入口完整收敛

### 需求背景

进一步检查发现 `ActorSkinSelectionResolver`、`ShopPanel_SkinPart` 和 `ShopPanelBoxerPreviewController` 分别持有重复的保存值匹配、Skin 行解析或 FullBody 名称解析；其中 resolver 的 `ResolvePartSkinKey(...)` 没有调用方。为避免保留单调用工具类和重复实现，本次把通用保存兼容解析完整收敛到 `SkinUtils`。

### 影响范围与修改后行为

- `SkinUtils` 新增 `ResolveSavedSkin(...)`、`ResolveFullBodySkinName(...)` 和 `SavedValueMatchesSkin(...)`，统一兼容 `Id`、`PrefabResource`、`SpineResource` 三类历史保存值。
- `ActorSkinBinding` 的 FullBody、部件配置和 prefab 路径全部直接使用 `SkinUtils`；取消令牌、版本保护、实例清理和挂点行为保持不变。
- `ShopPanelBoxerPreviewController` 删除本地 Skin 行遍历、三键匹配和 FullBody 名称解析，空 FullBody 值仍由控制器回退到 `_fallbackBaseSkinName`。
- `ShopPanel_SkinPart` 删除重复的三键匹配方法；新保存值仍由本地 `ResolvePartSkinKey(...)` 写入 `skin.Id`，保存策略没有改变。
- 删除无剩余调用方的 `ActorSkinSelectionResolver.cs` 及其 `.meta`，其中未使用的旧 `ResolvePartSkinKey(...)` 不迁移。

### 数据、资源、事件与平台边界

- 资源链路由 `Config.Skin.PrefabResource` 直接进入 `SkinUtils.ResolvePrefabLocation(...)`，仍解析为原有完整路径或 `Assets/Game/Prefabs/Skins/{PrefabResource}.prefab`。
- 保存兼容只读路径仍按同一部位匹配 `Id`、`PrefabResource`、`SpineResource`；不迁移、不重写已有存档。
- 不读写新的 DB/save 字段，不修改 `ActorSkinSelectionChangedEvent`、prefab、Luban 配置、资源包规则、广告、IAP、analytics、RemoteConfig 或平台 SDK。
- 不新增编辑器生命周期入口；Actor 资源加载仍处于原有运行时保护内。

### 验证记录

- 静态扫描确认项目已无 `ActorSkinSelectionResolver` 引用，也没有 `ResolveSavedSkin`、`SavedValueMatchesSkin` 或 FullBody 名称解析的重复私有实现。
- `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 99 warnings；warning 属于当前工程既有范围。
- 删除脚本后，首次构建因未跟踪的 Unity 生成 `Assembly-CSharp.csproj` 仍包含旧 `Compile Include` 而报 1 个缺失源文件错误；移除该过期生成项后重跑通过，该 `.csproj` 不进入 Git 提交。

### 未验证风险与回滚

- 尚未执行 Unity PlayMode 和 Android/iOS 横屏 smoke；ShopPanel 选中态、Boxer FullBody/部件预览、关卡 Actor 各部位实际加载与重复进入清理仍待运行时验证。
- 回滚时恢复 `ActorSkinSelectionResolver.cs/.meta` 及 ShopPanel 两处本地解析方法，并把三个调用方改回原入口；不需要修改资源、配置、prefab 或存档。
