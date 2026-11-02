# SkinPart Boxer_Rt 开启强制刷新记录

## 需求背景

本次修复针对 `ShopPanel_SkinPart` 重新打开 Skin 页后 `Boxer_Rt` 可能短暂显示上一次预览残留的问题。期望行为是每次开启 Skin 页都强制刷新 Boxer 预览，不沿用上一次关闭前的 RenderTexture 内容、临时试穿状态或 back/face/body 运行时挂件实例。

## 影响范围

- 运行时代码：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`。
- 预览对象：场景根 `Skin`、`SkinRt_Camera`、`Boxer_Rt` RawImage、共享 `boxer.renderTexture`、`Skin/back`、`Skin/face`、`Skin/body` 挂点。
- 未修改 `SkinItem.cs`、`InfiniteScrollView.cs`、`ShopPanel.prefab`、`Main.unity`、`GameData` 保存字段、Luban 配置、资源路径、Actor 关卡绑定、广告、金币、内购、analytics 或平台 SDK。

## 原行为基线

- `OnEnable()` 原先直接执行 `SyncTryUsingText()`、`SetBoxerPreviewActive(true, true)` 和 `ApplyCurrentSkinToViewer()`。
- `SetBoxerPreviewActive(true, true)` 会激活场景 `Skin` root 并清空 RenderTexture，但不会主动清理 `back`、`face`、`body` 挂点下可能留存的运行时子物体。
- `ClearBoxerPart(...)` 原先只销毁 `_boxerInstances` 字典中记录的实例；如果脚本生命周期重建、旧代码残留或某次实例未进入字典，挂点下的旧子物体不会被兜底清理。
- 静态场景证据显示 `Main.unity` 中 `Skin/back`、`Skin/face`、`Skin/body` 当前 `m_Children: []`，这些挂点可作为运行时 SkinPart prefab 容器使用。

## 修复后行为

- `OnEnable()` 现在统一调用 `ForceRefreshBoxerPreviewOnOpen()`，每次 Skin 页启用都走同一个强制刷新入口。
- 开启强刷会清空 ShopPanel-only 临时试穿状态，递增 `_boxerApplyVersion` 使旧的异步部件加载结果失效，并同步隐藏试穿提示。
- 强刷会先激活并解析场景 `Skin` 预览 root，再清理 `back`、`face`、`body` 挂点和 RenderTexture，最后按当前 `DB.GameData` 保存值重新调用 `ApplyCurrentSkinToViewer()`。
- `ClearBoxerPart(...)` 现在除了处理 `_boxerInstances` 字典，也会调用 `ClearBoxerPartChildren(...)`，把目标挂点下所有运行时子物体先 `SetActive(false)` 再 `Destroy(...)`，降低 Destroy 延迟到帧末时的可见残影风险。

## 数据、资源、UI、保存和事件流

- 本次只改变 ShopPanel Skin 页预览开启和挂点清理流程，不新增 `GameData.Save()`，不派发新的 `ActorSkinSelectionChangedEvent`。
- 当前保存字段读取仍通过 `GetEffectiveSkinValue(...)` 合成临时试穿和 `DB.GameData` 保存值；开启时临时试穿被清空，因此重新打开后只显示保存状态。
- 资源加载仍通过 Bear `ResourceManager` 和原有 `ApplyBoxerPartAsync(...)` 路径；本次没有新增资源地址、资源包规则或 YooAsset 直接调用。
- `Boxer_Rt` 仍显示同一个 RenderTexture；本次确保开启时先清空旧纹理内容，再由场景相机和当前皮肤状态重新渲染。

## 验证记录

- 静态检查确认 `ForceRefreshBoxerPreviewOnOpen()` 覆盖 `OnEnable()`，并且未新增 `Camera.Render()`、`OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`RemoteConfig`、`PlayerPrefs` 或 `StreamingAssets`。
- `git diff --check -- Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs` 通过，仅提示 Git 下次接触该文件时会进行 LF/CRLF 规范化。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 0 error / 1114 warnings；warnings 属于当前工程既有全局范围。

## 未验证风险

- Unity PlayMode 尚未验证反复打开/关闭 ShopPanel Skin 页时 `Boxer_Rt` 是否完全不显示上一帧残留。
- Unity PlayMode 尚未验证快速切换 Shop/Skin 页、快速试穿、Restore、关闭重开时 back/face/body 挂点是否没有旧实例闪现。
- Android/iOS 横屏真机 smoke 尚未验证 RenderTexture 清理、相机重新启用和快速触摸路径的实际表现。

## 回滚路径

- 回滚 `ShopPanel_SkinPart.OnEnable()` 中对 `ForceRefreshBoxerPreviewOnOpen()` 的调用，恢复为 `SyncTryUsingText()`、`SetBoxerPreviewActive(true, true)` 和 `ApplyCurrentSkinToViewer()`。
- 移除 `ForceRefreshBoxerPreviewOnOpen()` 和 `ClearBoxerPartChildren(...)`，并恢复 `ClearBoxerPart(...)` / `ClearAllBoxerParts()` 仅按 `_boxerInstances` 字典清理的旧行为，即可回到修复前预览生命周期。

## 2026-07-20 BoxerPreview 按 SkinPart 播放试穿动作

### 需求背景

ShopPanel Skin 页的 BoxerPreview 在发生有效试穿、已拥有皮肤应用或 Restore 清理时，需要按当前 `SkinPart` 播放对应的展示动作：Back 使用帽子动作、Body 使用服装动作、FullBody 使用全身动作、Face 使用眼镜动作。

### 影响文件与系统

- 运行时代码：`Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`。
- 动作资源证据：`Assets/Game/ArtSrc/Spines/sp_Box_001/Box.json`。
- 未修改 `SkinItem.cs`、`ShopPanel.prefab`、场景对象、Luban 配置、皮肤资源路径、存档字段、事件、广告、金币、内购、analytics 或平台 SDK。

### 原行为基线与新行为

- 原逻辑在 `PlayBoxerPreviewSkinAnimationIfNeeded()` 中固定查找并播放 `preview_skin`，没有根据 `_skinPart` 区分部位；当前 `Box.json` 的实际动作名是四个带后缀的动作，不包含该固定名称。
- 新逻辑在同一播放入口内把当前 `_skinPart` 映射为：Back → `preview_skin_(cap)`、Body → `preview_skin_(clothing)`、FullBody → `preview_skin_(full)`、Face → `preview_skin_(glasses)`。
- 现有触发边界保持不变：只有 `ApplyCurrentSkinToViewer(true)` 请求切换动作时才播放；首次打开 Skin 页仍通过无动画参数恢复当前外观；同一动作仍在尚未播放完成时跳过重复触发。
- 未知枚举值仅作为防御性分支回退到 Back 动作；当前生成的 `SkinPart` 枚举只有 Back、Face、Body、FullBody 四项。

### 数据、资源、UI 与事件流

- 动画仍由场景预览 `ActorSpineCtrl` 在 0 号轨道非循环播放，切换前仍通过 skeleton 数据检查动作是否存在。
- 动作缺失仍只通过 Bear Logger 记录一次 warning，不新增每帧轮询、协程、实例化或资源加载。
- 皮肤试穿、应用、保存与 Restore 数据链没有改变；本次只改变已有 BoxerPreview 动画播放请求所选择的动作名。

### 验证记录

- 已用 PowerShell 解析 `Box.json` 的 `animations` 属性，确认四个映射动作全部存在。
- `git diff --check` 已通过；仅有工作区既有 LF/CRLF 转换提示。
- 已静态检查新增差异，没有新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`RemoteConfig`、`PlayerPrefs`、`StreamingAssets`、存档写入或事件派发。
- `dotnet build Assembly-CSharp.csproj --no-restore` 的窄构建被当前 Unity 生成目录 `Temp/bin/Debug` 中 101 个依赖 DLL 缺失阻断，未获得本次脚本的有效编译结论。
- 允许项目引用参与构建后的再次尝试仍被 `Temp/obj` 下多个 DLL、FileList 和 SourceLink 文件锁阻断；日志明确包含火绒进程占用，最终为 12 errors / 257 warnings，同样没有进入可归因于本次脚本的有效编译诊断。

### 未验证风险

- 尚未在 Unity PlayMode 实际点击 Back、Body、FullBody、Face 皮肤，确认 BoxerPreview 分别播放对应动作且部件显示时序符合美术预期。
- 尚未验证快速切换筛选、连续试穿、应用已拥有皮肤与 Restore 时的动作重入表现。
- Android/iOS 横屏真机的动画帧稳定性和触控路径尚未 smoke 验证。

### 回滚路径

将四个动作常量和 `GetBoxerPreviewAnimationName(...)` 映射移除，并把 `PlayBoxerPreviewSkinAnimationIfNeeded()` 恢复为固定动作名，即可回到修改前行为；回滚不涉及资源、prefab、场景或存档迁移。

## 2026-07-20 SkinPart 与 BoxerPreview 职责拆分

### 需求背景

`ShopPanel_SkinPart` 在本次整理前约 2012 行，同时承担列表筛选、所有权、广告/金币/内购、存档、临时试穿、场景查找、RenderTexture、Spine 动画、部件异步加载与实例清理。用户确认在保持现有玩家可见行为的前提下进行低风险精简，并优先拆出 BoxerPreview 运行时职责。

### 影响文件与系统

- `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`：保留目录、筛选、解锁、存档、试穿状态与预览编排。
- `Assets/Game/UI/ShopPanel/Scripts/ShopPanelBoxerPreviewController.cs`：新增本地运行时控制器，承接场景预览引用、RenderTexture、Spine、部件资源缓存/加载/清理和异步版本保护。
- `Assets/Game/UI/ShopPanel/Resources/ShopPanel.prefab`：只删除脚本已移除且原值均为空的 `skinViewer`、场景 Actor/Camera/挂点序列化键；保留 `Boxer_Rt` 与 `boxer.renderTexture` 引用。
- 未修改 Skin 配置、皮肤资源地址、场景 `Skin` 层级、GameData schema、广告/IAP SDK、analytics、Actor 关卡绑定或资源打包规则。

### 原行为基线与整理后行为

- 原 `ShopPanel_SkinPart` 自己查找场景 root `Skin`、相机、ActorSpineCtrl 和 back/face/body，持有 prefab 缓存与运行时实例，并用 `_boxerApplyVersion` 拦截过期异步回写。
- 原生产 `ShopPanel.prefab` 的 `skinViewer` 引用为空，prefab 内也没有 `UISpineCtrl` 组件；有效 FullBody 预览实际由场景 `ActorSpineCtrl` 完成。
- 整理后 `ShopPanel_SkinPart.ApplyCurrentSkinToViewer(...)` 仍按相同顺序读取保存值与临时试穿值，但只把四个有效值及可选动画部位交给 `ShopPanelBoxerPreviewController.Apply(...)`。
- 新控制器保持原场景 root 查找、RenderTexture 清理、SkinUtils 资源路径/加载、prefab 缓存、版本失效、Ignore Raycast 递归设置、挂点显隐和 0.2 秒 DOTween 缩放行为。
- Back/Body/FullBody/Face 动作映射及“同动作未完成时不重播”规则保持不变；首次打开仍只恢复外观，不主动播放切换动作。
- 删除无代码、prefab、scene 引用证据的 `ShowAll/ShowBack/ShowFace/ShowBody` 和私有 `ClearCurrentSkin(...)`；外部 `SelectSkinPart(...)`、`RefreshItem()`、`ClearCurrentSkinPart(...)` 保持。

### 数据、资源、UI、保存和事件流

- `ShopPanelBoxerPreviewController` 不引用 `DB`、不调用 `Save()`、不授予皮肤、不扣金币、不请求广告/IAP、不派发 `ActorSkinSelectionChangedEvent`。
- 所有权、当前部位字段写入、临时试穿字典、购买回调和 Bear 事件仍留在 `ShopPanel_SkinPart`，对应业务差异扫描未发现新增或删除。
- 资源加载仍通过 `SkinUtils.ResolvePrefabLocation(...)`、`CanLoadPrefabLocation(...)`、`LoadPrefabAsync(...)`，没有直接接入 YooAsset。
- 主控制器由约 2012 行降为 1388 行；新控制器约 702 行。总源码行数因新增中文 API/边界注释略有增加，本次收益是职责隔离和主控制器认知复杂度下降，不宣称运行时性能提升。

### 验证记录

- `Assembly-CSharp-firstpass.csproj` 窄构建通过：0 error / 1 warning。
- `Assembly-CSharp.csproj` 窄构建通过：0 error / 99 warnings；新控制器已包含在该 csproj 的 Compile 项中。
- `git diff --check` 通过，仅提示工作区 LF/CRLF 转换。
- 静态扫描确认新控制器没有 `DB`、`Save()`、广告、IAP、事件派发、`OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode` 或 `Update`。
- 已重新解析 `Box.json`，确认 `preview_skin_(cap)`、`preview_skin_(clothing)`、`preview_skin_(full)`、`preview_skin_(glasses)` 四个动作仍存在。

### 未验证风险

- 尚未执行 Unity PlayMode；需要验证打开/关闭、四部位筛选、未拥有试穿、已拥有应用、Restore、缺失 prefab、快速连续切换和关闭时异步回写失效。
- 生产 prefab 没有 `UISpineCtrl` 的静态证据已确认，但运行时是否存在外部动态添加该组件的未发现路径仍为 `uncertain`；当前代码与资源搜索没有此类证据。
- Android/iOS 横屏真机的触控、RenderTexture、异步加载峰值和动画帧稳定性尚未 smoke 验证。

### 回滚路径

恢复 `ShopPanel_SkinPart.cs` 中原 BoxerPreview 字段、方法和 UISpine 兼容分支，恢复 `ShopPanel.prefab` 的空序列化键，删除 `ShopPanelBoxerPreviewController.cs` 及其 meta，即可回到拆分前结构；本次没有存档、配置或资源迁移需要回滚。

## 2026-07-20 BoxerPreview 展示动作结束后回到 idle

### 需求背景

Back、Body、FullBody、Face 的 BoxerPreview 展示动作均为一次性动作。为避免动作完成后停留在末帧，每个展示动作自然结束后需要回到循环 `idle` 状态。

### 影响文件与系统

- `Assets/Game/UI/ShopPanel/Scripts/ShopPanelBoxerPreviewController.cs`：在 0 号轨道播放非循环展示动作后排入循环 `idle`。
- `Assets/Game/ArtSrc/Spines/sp_Box_001/Box.json`：作为动作资源证据，静态确认包含 `idle` 与四个展示动作。
- 未修改皮肤试穿、所有权、保存、Restore、资源加载、事件、广告、内购、analytics 或平台 SDK 链路。

### 原行为基线与修改后行为

- 原行为只调用 `PlayAnimation(animationName, false, false)` 播放一次展示动作，没有后续轨道动画，动作完成后的显示状态依赖 Spine 当前轨道末帧。
- 修改后仍先播放同一个非循环展示动作；播放成功且 skeleton 中存在 `idle` 时，通过 `AnimationState.AddAnimation(0, "idle", true, 0f)` 把循环待机排到同一轨道。
- `delay=0` 由 Spine 按当前动作时长衔接，因此不会提前打断展示动作，也不新增计时器、协程或完成事件订阅。
- 同一展示动作尚未完成时仍跳过重复触发；切换到其他部位动作时，新的 `SetAnimation` 会替换旧轨道队列并重新排入 `idle`。
- 若展示动作不存在，保持原有一次 warning 并停止播放；若只有 `idle` 不存在，展示动作仍正常播放，同时新增独立的一次 warning，避免快速点击刷日志。

### 验证记录

- 已静态解析 `Box.json`，确认存在 `idle`、`preview_skin_(cap)`、`preview_skin_(clothing)`、`preview_skin_(full)`、`preview_skin_(glasses)`。
- 已确认当前 Spine Runtime 的 `AnimationState.AddAnimation(...)` 可在 0 号轨道追加后续循环动画。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 99 warnings；warnings 属于当前工程既有范围。
- `git diff --check` 通过，仅提示工作区 LF/CRLF 转换；新增控制器和本次文档的尾随空白扫描通过。
- 静态扫描确认控制器没有 `DB`、`Save()`、`ActorSkinSelectionChangedEvent`、`OnValidate`、`Reset`、`ExecuteAlways` 或 `ExecuteInEditMode`。

### 未验证风险

- 尚未执行 Unity PlayMode；四个展示动作结束后的混合过渡、末帧与 `idle` 首帧的视觉衔接仍需实际确认。
- 快速连续切换不同 SkinPart 时的轨道替换表现，以及 Android/iOS 横屏设备上的动画帧稳定性仍待 smoke 验证。

### 回滚路径

移除 `IdleAnimationName`、idle 缺失日志状态和 `animationState.AddAnimation(...)` 逻辑，即可恢复展示动作完成后不主动返回待机的原行为；无需迁移配置、资源或存档。

## 2026-07-22 Skin 页禁用时保留 Preview Session

### 需求背景

玩家在同一个 ShopPanel 内从 Skin 页切到其它商店页签后再切回时，需要继续看到离开前的临时试穿与每部位选中状态。Skin 页签的 `Disable` 只代表页签切换，不应等同于 ShopPanel 关闭；preview 内容应保留到 ShopPanel 真正关闭为止。

### 影响文件与系统

- `Assets/Game/UI/ShopPanel/Scripts/ShopPanel_SkinPart.cs`：把 preview 状态清理边界从 `OnDisable` 移到显式 `ClosePreviewSession()`，并区分首次打开与同一面板内恢复。
- `Assets/Game/UI/ShopPanel/Scripts/ShopPanelBoxerPreviewController.cs`：新增 `Suspend()` / `Resume()`，仅切换场景预览 root 和相机显隐，不清理部件实例或 RenderTexture。
- `Assets/Game/UI/ShopPanel/Scripts/ShopPanel.cs`：在 `OnClose()` 中显式结束 Skin preview session，随后保留原静态隐藏兜底。
- 未修改 `ShopPanel.prefab`、Skin 配置、GameData schema、资源地址、广告/IAP、analytics、事件或平台 SDK。

### 原行为基线与修改后行为

- 原 `ShopPanelSwitchGroup` 通过 `SetActive(false)` 切走 Skin 页，触发 `ShopPanel_SkinPart.OnDisable()`；该入口会清空 `_transientPreviewSkins`、`_selectedSkinsByPart`，调用控制器 `Close()` 清理 back/face/body 实例，并在下次 `OnEnable()` 再次强刷保存态。
- 修改后 `OnDisable()` 仍解绑按钮/筛选监听、清理请求中标记、隐藏 actionButton 并清空虚拟列表，但不再清理临时试穿或每部位选中项；控制器只执行 `Suspend()` 隐藏场景 root/相机。
- 同一 ShopPanel 内再次启用 Skin 页时，`OpenOrResumeBoxerPreview()` 调用 `Resume()` 恢复原部件与 RenderTexture，并由后续筛选刷新恢复 `_selectedSkin` 和对应 actionButton，不重新应用保存态覆盖 preview。
- `ShopPanel.OnClose()` 会调用 `ClosePreviewSession()`：清空临时试穿和记忆选中项，调用控制器 `Close()` 使未完成异步加载失效、清理运行时部件并隐藏预览。`OnDestroy()` 保留相同兜底并释放 prefab 缓存。

### 数据、资源、UI、保存和事件流

- preview session 仍只存在于当前 ShopPanel 实例内，不新增存档字段，也不调用 `GameData.Save()`。
- 页签禁用期间未完成的部件异步加载允许在隐藏 root 下完成，以便切回时直接恢复；ShopPanel 关闭会递增 apply version，使未完成回写失效并清理已生成实例。
- 临时试穿仍优先于 `DB.GameData` 保存值参与 Boxer 外观合成；真正拥有、应用、Restore、广告、金币、内购和 `ActorSkinSelectionChangedEvent` 的触发条件没有改变。
- 场景 root 和相机在 Skin 页不可见期间保持禁用，不新增 `Update`、轮询、全局事件或资源重复加载入口。

### 验证记录

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：0 error / 99 warnings。
- `git diff --check` 对三个运行时代码文件通过，仅有工作区 LF/CRLF 规范化提示。
- 静态调用检查确认 `ClearAllTransientPreviews()` 与 `ClearAllRememberedSelections()` 不再由 `OnDisable()` 调用，清理入口收敛到首次会话初始化、Restore 单部位清理、`ClosePreviewSession()` 和销毁兜底。
- 静态检查确认未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`Update`、DB 写入、Save、全局事件、RemoteConfig、PlayerPrefs 或平台分支。

### 未验证风险

- 尚未执行 Unity PlayMode：需要验证未拥有皮肤试穿后切到 Shop 页再切回，四个 SkinPart 的外观、试穿提示、选中态与 actionButton 均保持。
- 尚未验证页签隐藏时部件异步加载完成、快速反复切页、Restore 后切页，以及关闭 ShopPanel 时异步回写失效和场景部件清理。
- 尚未执行关闭并重开 ShopPanel 的全新 session 验证，也未执行 Android/iOS 横屏真机的 RenderTexture、相机开销和触控 smoke。

### 回滚路径

恢复 `OnDisable()` 中对临时试穿、记忆选中项和控制器 `Close()` 的清理，恢复 `OnEnable()` 每次调用强制刷新，并移除 `ShopPanel.OnClose()` 的 `ClosePreviewSession()`、控制器 `Suspend()` / `Resume()` 与 `_hasPreviewSession`，即可回到页签切换即丢弃 preview 的原行为；无需迁移存档、配置或资源。
