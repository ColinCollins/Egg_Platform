# PencilSkin 笔刷配置功能与风险记录

## 1. 需求与背景

- 需求：为 `Assets/Game/UI/DrawGame/PencilSkinPopup` 先建立笔刷配置脚本，后续允许不同笔刷
  配置 Gradient、线宽、共享材质、纹理 Cover/Mask，以及独立 SpriteMask Cover 表现。
- 用户确认：切换笔刷只影响之后新画的线；当前活动线和已经完成的线必须保持创建时样式。
- 本阶段只建立 ScriptableObject 配置契约，不接入 PencilSkinPopup 交互、存档、Luban、
  ResourceManager、DrawPlayCtrl、DrawLevelCtrl、DrawLineFeature 或 Shader。

## 2. 修改前基线

- `PencilSkinPopup.cs` 只有 `BaseUIView` 生命周期骨架和创建入口。
- `PencilSkinItem.cs` 仍是空的 `Start` / `Update` 占位脚本。
- `DrawLineFeature` 已能配置 Material、Gradient 和线宽，但当前
  `SetDrawingStyle` 会刷新活动线及已完成线，与本次确认的“仅影响后续新线”目标不同；
  本阶段未修改该运行时行为。
- 模块目录内此前没有 PencilSkin/Brush 专项 Markdown 文档。

## 3. 新增配置契约

新增 `Scripts/DrawBrushStyleAsset.cs`：

- `DrawBrushStyleAsset`
  - 唯一 `brushId` 和 UI `previewSprite`。
  - 共享 `lineMaterial`、`colorGradient`、`lineWidth`。
  - 始终参与线条渲染的 Gradient。
  - 可与 Gradient 同时渲染的 `coverTexture`、平铺倍率和强度。
  - 可与 Gradient、Texture Cover 同时渲染的 `coverSprite`、`maskSprite` 和可选覆盖材质。
  - 对外只读属性及 `TryValidate(out string error)` 最小配置校验。

配置不使用互斥 RenderMode：是否启用 Texture Cover 由 `coverTexture` 是否配置决定；
是否启用 Sprite Cover / Mask 由 `coverSprite` 与 `maskSprite` 是否成对配置决定。

配置资产通过 Unity 菜单
`Create > DrawGame > Pencil Skin > Brush Style` 创建。运行时消费方必须把资产视为只读，
不能修改共享 Material 或 Gradient Key。

## 4. 数据、资源、UI 与事件边界

- 数据：本阶段没有新增 Luban 表或远程配置；未来表格只需保存 Brush ID、资源路径、
  解锁/价格/排序等轻量信息。
- 资源：本阶段没有创建 `.asset`，没有修改 YooAsset Collector，也没有调用
  `ResourceManager`；动态加载与释放仍待后续实现。
- UI：没有修改 `PencilSkinPopup`、`PencilSkinItem`、prefab 或按钮绑定。
- 存档：没有新增或修改 DB 字段；当前装备 Brush ID 的保存仍待后续实现。
- 画线：没有修改 `DrawLineFeature`；“只影响之后新画的线”已写入配置类契约，但运行时
  尚未接线，不能据此声明行为已生效。
- 事件、analytics、广告、IAP、SDK：均未修改。

## 5. 验证

- 已执行静态扫描，确认新增脚本未包含 `OnValidate`、`Reset`、`ExecuteAlways`、
  `ExecuteInEditMode`、`Update`、DB、Save、RemoteConfig、全局事件或运行时资源加载。
- 已检查新增公开类型、属性、方法和序列化字段均有中文用途/边界说明，并使用
  `#region` 分区。
- Unity 已刷新 `Assembly-CSharp.csproj` 并把 `DrawBrushStyleAsset.cs` 纳入 Compile 项；
  执行
  `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1`，
  结果为 `0 error / 101 个项目既有 warning`。
- Unity 批处理验证因同一工程已在另一个 Unity 实例打开而未执行；没有关闭用户正在使用的
  Editor。新增脚本的 C# 编译已由上述 Unity 生成工程清单构建覆盖。

## 6. 风险与未验证项

- `uncertain`：Texture Cover/Mask 的最终 Shader 属性和 SpriteMask Cover 的节点所有权
  尚未实现，当前配置字段只定义数据边界。
- 尚未创建并在 Inspector 中人工校验任一 `DrawBrushStyleAsset`。
- 尚未验证 YooAsset 动态加载路径、资源依赖收集、缓存与释放。
- 尚未把样式快照接入新笔画创建，因此当前运行时切换仍会刷新已有线条。
- Unity PlayMode、Android/iOS 横屏表现、纹理 UV、SpriteAtlas、内存和批次影响均未验证。

## 7. 回滚

1. 删除 `Scripts/DrawBrushStyleAsset.cs` 及其 `.meta`。
2. 删除本记录及其 `.meta`。
3. 删除 `DevelopMD/已有功能设计与使用记录.md` 中对应的 PencilSkin 配置记录。
4. 本阶段未修改 prefab、DB、Luban、Shader 或运行时调用方，不需要数据迁移。

## 8. 2026-07-30 PencilSkinItem 状态切换

### 8.1 需求与修改前基线

- 需求：参考 `SkinItem` 的显式状态管理方式，实现 `PencilSkinItem` 的状态切换脚本。
- 修改前 `Scripts/PencilSkinItem.cs` 只有空的 `Start` / `Update` 占位方法，没有状态枚举、
  状态节点引用或对外切换 API。
- 当前工作区中的 `Prefabs/PencilSkinItem.prefab` 已有用户制作的 `normalPart`、
  `selectedPart`、`adUnlockPart`、`coinPart` 和 `UI_brushskin_img_gou_01` 节点；本次只把
  这些节点作为脚本契约的本地证据，没有修改或覆盖该 prefab。

### 8.2 修改后行为与影响文件

- 修改 `Scripts/PencilSkinItem.cs`，新增 `PencilSkinItemState`：
  `None`、`Normal`、`Selected`、`AdUnlock`、`CoinUnlock`。
- 新增 `SetState(PencilSkinItemState)`，每次切换只显示一个对应状态节点；`Selected`
  同时显示独立勾选标记，`None` 隐藏全部状态节点。
- `Awake()` 应用序列化初始状态，默认值为 `Normal`。
- 序列化引用为空时，脚本只从当前 Item 的直接子节点按上述固定名称兜底查找；不进行
  全层级扫描、不实例化对象，也不访问运行时单例。
- 新增公开 API、序列化字段和状态值均补充中文用途、调用边界与注意事项，并按职责使用
  `#region` 分区。

### 8.3 数据、资源、UI、保存与事件流

- 调用方负责判断笔刷所有权、装备状态和解锁方式，再调用 `SetState(...)`；Item 只负责
  本地 UI 显隐，不把显示状态当作业务事实。
- 本次没有接入 PencilSkinPopup 列表生成、点击回调、Luban 表、DB/Save、金币扣除、
  广告播放、笔刷装备、资源加载、画线样式、全局事件、analytics 或平台 SDK。
- `PencilSkinItem.prefab` 当前静态 YAML 中尚未挂载 `PencilSkinItem` 组件，因此脚本只有
  在后续由 prefab 或运行时实例挂载并由调用方驱动后才会参与实际 UI 流程。

### 8.4 验证证据

- `git diff --check -- Assets/Game/UI/DrawGame/PencilSkinPopup/Scripts/PencilSkinItem.cs`
  通过，仅有工作区 LF/CRLF 转换提示。
- 静态风险扫描未发现 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、
  `Update`、DB、Save、RemoteConfig、PlayerPrefs、实例化或全局事件调用。
- 第一次 `dotnet build` 在 184 秒命令超时后继续在后台完成，未取得可用退出诊断；随后
  使用同一 Unity 生成的 `Assembly-CSharp.csproj` 重跑构建，结果为
  `0 error / 0 warning`。

### 8.5 未验证风险与回滚

- 尚未修改 prefab 挂载和序列化绑定，尚未接入 `PencilSkinPopup` 数据刷新；运行时状态
  来源、初次刷新时机和点击后的状态更新仍待后续需求实现。
- 尚未执行 Unity PlayMode，也未验证 Android/iOS 横屏下四种状态的美术层级、勾选标记、
  点击区域和触控表现。
- 回滚时恢复 `Scripts/PencilSkinItem.cs` 为空占位脚本，并删除本节及
  `DevelopMD/已有功能设计与使用记录.md` 中对应记录；本次没有数据或资源迁移。

## 9. 2026-07-30 DrawBrushStyle 可组合渲染配置

### 9.1 需求与原配置基线

- 用户确认：Gradient Color、Texture Cover/Mask 和 Sprite Cover/Mask 不是互斥渲染模式，
  同一个笔刷可以同时渲染这些表现。
- 原 `DrawBrushStyleAsset` 使用 `DrawBrushRenderMode` 在 `GradientOnly`、
  `TextureCover`、`TextureMask`、`SpriteMaskCover` 中四选一；该契约会阻止消费方同时应用
  Gradient 与多个 Cover / Mask 表现。
- CodeGraph 只发现枚举和相关属性定义，没有发现运行时代码消费 `DrawBrushRenderMode`、
  `RenderMode`、`UsesTextureCover` 或 `UsesSpriteMaskCover`，因此当前没有需要同步迁移的
  业务调用方。

### 9.2 修改后配置契约

- 删除 `DrawBrushRenderMode`、序列化 `renderMode` 和只读 `RenderMode`。
- `ColorGradient` 保持基础线条配置，消费方始终可以应用。
- `UsesTextureCover` 改为根据 `coverTexture != null` 独立判断。
- `UsesSpriteMaskCover` 改为根据 `coverSprite` 与 `maskSprite` 均不为空独立判断。
- Texture Cover 与 Sprite Cover / Mask 可以同时返回启用，消费方后续必须叠加处理，
  不能重新实现为互斥分支。
- `TryValidate(...)` 不再按 RenderMode 要求资源；只在 Cover Sprite 与 Mask Sprite
  仅配置一项时返回失败，二者同时为空或同时有效均允许。

### 9.3 数据、资源与兼容边界

- 本次只修改 `Scripts/DrawBrushStyleAsset.cs` 和文档，不修改 Shader、材质、Prefab、
  PencilSkinPopup、DrawLineFeature、DB/Save、Luban、ResourceManager、全局事件、
  analytics 或平台 SDK。
- 当前工作区新建的 `PencilSkin_10001～10003.asset` 仍含旧 `renderMode: 0` YAML；
  新脚本不再读取该字段，Unity 后续重新保存资产时可自动移除，无需本次改写用户资产。
- 没有迁移或重写用户已经配置的 Gradient、Material、Cover Texture、Sprite 或 Mask
  引用。

### 9.4 验证证据

- 静态扫描确认 PencilSkinPopup 脚本目录中已无 `DrawBrushRenderMode`、`renderMode` 或
  `RenderMode` 的 C# 引用。
- `git diff --check -- Assets/Game/UI/DrawGame/PencilSkinPopup/Scripts/DrawBrushStyleAsset.cs`
  通过，仅有工作区 LF/CRLF 转换提示。
- 静态风险扫描未发现新增 `OnValidate`、`Reset`、`ExecuteAlways`、
  `ExecuteInEditMode`、运行时 DB/Save、RemoteConfig、PlayerPrefs、实例化、全局事件或
  `Update` 路径。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1`
  通过，结果为 `0 error / 101 warning`。

### 9.5 未验证风险与回滚

- 当前仍只有配置契约，没有运行时消费实现；Gradient 与多个 Cover / Mask 的真实叠加顺序、
  Shader 属性、混合方式、SpriteMask 节点所有权和资源释放仍为 `uncertain`。
- 尚未执行 Unity Inspector 重新序列化、PlayMode 或 Android/iOS 横屏渲染验证。
- 回滚时恢复 `DrawBrushRenderMode`、`renderMode`、`RenderMode` 及按枚举判断的两个
  `Uses...` 属性，并删除本节和共享记录中的对应条目；无需修改 DB 或配置表。

## 10. 2026-07-30 DrawLineFeature 测试 ID 接入

### 10.1 需求与实现

- `DrawLineFeature` 新增 Inspector 测试字段 `brushStyleId`，默认值为 `10001`；字段留空
  时也回退加载 `PencilSkinConfig/PencilSkin_10001`，填写其它 ID 时加载对应资产。
- 加载入口固定在 `DrawLineFeature.InitializeRuntime()`，使用 Bear
  `ResourceManager.Instance.Load<DrawBrushStyleAsset>()`，不在每帧或每笔画重复加载。
- 配置通过 `TryValidate(...)` 且内部 `BrushId` 与请求 ID 一致后，复制
  `LineMaterial`、`ColorGradient` 到非序列化运行时缓存。
- `DrawLineFeature.drawingLineWidth` 保留在 Inspector，并覆盖 asset 的 `LineWidth`；
  加载配置不会改写关卡线宽。
- `drawingLineMaterial`、`drawingLineGradient`、`drawingLineColor` 已从
  DrawLineFeature 序列化配置中删除，材质和 Gradient 只能来自 asset、运行时
  `SetDrawingStyle(...)` 或失败 fallback。
- 新线创建时由既有 `ApplyDrawingStyle(...)` 写入 `sharedMaterial`、Gradient 和线宽；
  本次初始化发生在第一笔之前，不修改活动线或已完成线。
- 默认或指定资源加载失败、配置无效或 ID 不一致时使用 fallback Material 与黑色
  Gradient，继续使用 Inspector 线宽，不阻断关卡。

### 10.2 边界、验证与回滚

- 本次没有接入商城选择、DB/Save、Luban、资源 ID 存档、Cover/Mask 渲染、材质属性修改、
  全局事件、analytics 或平台 SDK。
- 静态核对 `PencilSkin_10001～10008.asset` 的文件 ID、内部 BrushId 和基础样式字段。
- `git diff --check` 通过；`Assembly-CSharp.csproj` 构建通过，
  `0 error / 101 warning`。
- 尚未执行 Unity PlayMode、独立 DLevel 场景资源系统初始化、Android/iOS 横屏或首次加载
  性能验证；所有既有关卡默认采用 10001 后的视觉一致性及 Cover/Mask 仍未验证。
- 现有 DLevel prefab YAML 可能暂时保留已删除的 Material / Gradient / Color 字段；
  Unity 会忽略这些旧字段，后续正常保存 prefab 时可清理，本次没有改写用户 prefab。
- 详细共享 DrawLineFeature 风险、数据流与回滚见
  `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl功能与风险评估.md` 第 38 节。
- 回滚本次精简时重新增加 DrawLineFeature 的序列化 Material、Gradient、隐藏 Color，并
  恢复 asset 的 LineWidth 写入；配置资产无需迁移。

## 11. 2026-07-30 PencilSkinPopup 表驱动商店

### 11.1 需求与修改前基线

- 需求：参考 `ShopPanel_SkinPart`，让 `PencilSkinPopup` 读取 `PencilSkinShop` 与
  `PencilSkin` 两张表，生成笔刷列表、刷新拥有/选中/金币/广告状态，并用
  `DrawBrushStyleAsset.PreviewSprite` 替换 `PencilSkinItem/main_img` 的贴图。
- 修改前 `PencilSkinPopup.cs` 只有 Bear UI 生命周期骨架；没有列表生成、关闭按钮、
  表读取、资源加载、存档或解锁逻辑。
- 修改前 `PencilSkinItem.cs` 只有五种视觉状态切换，不绑定配置、预览图、点击回调和
  金币价格。
- 当前工作区已把 `PencilSkinItem` 组件挂到 `PencilSkinItem.prefab` 根节点；本次保留
  该用户 prefab 改动，并通过名称兜底解析其 `main_img`、`coin_txt` 和 `clickArea`。

### 11.2 修改后行为与影响范围

- `PencilSkinPopup` 优先按 `TbPencilSkinShop.DataList` 的顺序读取 `SkinId`，再从
  `TbPencilSkin` 映射完整配置；Shop 表为空时回退 PencilSkin 表顺序，重复或无效 ID
  不生成 Item。
- Popup 在当前 `Content` 下按表行数创建并缓存 `PencilSkinItem`，再次刷新只重绑数据，
  不重复实例化；同时根据现有 `GridLayoutGroup` 的单元格和可用宽度刷新内容高度。
- Popup 通过 Bear `ResourceManager` 加载
  `PencilSkinConfig/PencilSkin_{SkinId}`，校验资源内 `BrushId` 后把
  `PreviewSprite` 传给 Item；Item 会清除 prefab 原贴图并把 `main_img` 替换为该 Preview。
- `PencilSkinItem.Bind(...)` 同步绑定 PencilSkin 配置、Preview、状态、金币文本、点击回调
  和交互开关；业务解锁与保存仍由 Popup 持有。
- `PencilSkinPopup.prefab` 新增 `Content` 与 `PencilSkinItem.prefab` 的显式序列化引用；
  关闭按钮复用已有 `UiCommonX01Btn`，不修改自动生成的
  `PencilSkinPopup.Generated.cs`。

### 11.3 状态、解锁与存档流

- 复用 DrawGame 独立存档的 `OwnedSkin` 与 `CurrentSkinId`，不读写 Actor Skin 使用的
  `DB.GameData`。
- 每次打开先执行 `EnsureValidCollections()`，把所有 `UnlockType.Default` 行补入
  `OwnedSkin`；当前 ID 为空、已不在当前目录或未拥有时，回退到当前目录首个 Default
  笔刷并调用 `DB.SaveDrawGame()`。
- 已拥有且未选中显示 `Normal`，当前装备显示 `Selected`，未拥有的 `Coin` / `Ad`
  分别显示 `CoinUnlock` / `AdUnlock`。
- 点击 `Normal` 写入 `CurrentSkinId` 并保存；金币解锁复用 `GameManager.Bag`、
  `GameProps.Coin`、`UnlimitCoin` 和 `CostType.PencilSkin`；广告解锁复用
  `RewardAdHelper` 与 `RewardPlacement.ShopReward`。解锁成功后写入 `OwnedSkin`、
  设为当前笔刷并保存。
- 当前 Item 没有 Purchase、Special 或 Unavailable 专用视觉，未拥有的这些类型会保持
  普通外观但禁用点击；本次没有新增内购按钮或隐式授予路径。
- 本次不新增全局事件、analytics、RemoteConfig、PlayerPrefs、DB 字段或表结构。
  `DrawLineFeature` 仍使用各关 Inspector 测试 ID，本次没有把 Popup 选中 ID 接到
  已经开始或之后进入的关卡画线配置。

### 11.4 验证证据

- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nr:false
  /p:BuildInParallel=false` 通过，结果为 `0 error / 101 个项目既有 warning`。
- `git diff --check` 对两个脚本和 `PencilSkinPopup.prefab` 通过，仅有工作区
  LF/CRLF 转换提示。
- 静态风险扫描未发现新增 `OnValidate`、`Reset`、`ExecuteAlways`、
  `ExecuteInEditMode`、`Update`、RemoteConfig、PlayerPrefs 或全局事件路径。
- 当前 JSON 静态核对结果为 PencilSkin 8 行、PencilSkinShop 8 行；Shop 的 8 个
  `SkinId` 均能映射到 PencilSkin，8 个 `PencilSkin_301001～301008.asset` 均存在、
  内部 BrushId 匹配且 PreviewSprite 非空。
- 静态 YAML 确认 Popup 已绑定 Content 和 PencilSkinItem prefab，Item prefab 根节点
  已挂载 `PencilSkinItem`。

### 11.5 风险、未验证项与回滚

- 尚未执行 Unity PlayMode；需要验证首次打开默认选中、滚动区域 8 项布局、关闭重开、
  Preview 比例、状态显隐、金币不足/无限金币/扣费、广告成功/失败/关闭时回调。
- 尚未执行 Android/iOS 横屏 smoke；同步加载 8 个小型配置资产的首开耗时、触摸、
  刘海/安全区和低端机 UI 实例化峰值未验证。
- 广告播放期间关闭 Popup 时沿用 SkinPart 的生命周期边界：关闭后的回调不继续写入
  Popup 状态；该平台时序仍需 PlayMode/真机确认。
- 当前 ID 已由用户配置为 `301001～301008`，旧 `10001～10008` 资源在工作区被删除；
  Popup 不硬编码旧 ID。`DrawLineFeature` 的默认 `10001` 与 Inspector 旧测试值是否同步
  迁移不属于本次范围，仍为待确认风险。
- 回滚时恢复 `PencilSkinPopup.cs` 为生命周期骨架、恢复 `PencilSkinItem.cs` 为仅状态
  脚本，并移除 `PencilSkinPopup.prefab` 的 `itemContent` /
  `pencilSkinItemPrefab` 两个引用即可；本次没有 DB schema 迁移，已写入的
  `OwnedSkin` / `CurrentSkinId` 可保留或由后续数据策略清理。

## 12. 2026-07-30 PencilSkinItem prefab 显式绑定

### 12.1 需求与行为变化

- 用户要求直接在 `PencilSkinItem.prefab` 绑定脚本引用，删除运行时
  `ResolveReferences()`。
- `PencilSkinItem` 组件现已显式绑定 `normalPart`、`selectedPart`、
  `adUnlockPart`、`coinPart`、`selectedMark`、`mainImg`、`coinText` 和
  `clickButton`。
- 删除 `ResolveReferences()`、`FindChild(...)`、`FindChildComponent(...)` 和
  `FindChildRecursive(...)`，`Awake`、`Bind`、`Clear`、`SetState` 与按钮绑定不再按
  节点名称扫描层级。
- `BindButton()` 只在 `clickButton` 已绑定时注册事件；空引用保护仍保留，但 prefab
  漏绑时不会再静默从层级补齐。

### 12.2 边界、验证与回滚

- 本次只修改 `PencilSkinItem.cs` 与 `PencilSkinItem.prefab`，不改变两张 Luban 表、
  Preview 资源路径、Popup 列表、DrawGame 存档、金币/广告或画线样式逻辑。
- 静态 YAML 已核对 8 个字段均引用到 prefab 内对应 GameObject、Image、
  TextMeshProUGUI 或 CustomButton 组件。
- 静态扫描确认 `PencilSkinItem.cs` 已无 `ResolveReferences`、递归查找或
  `transform.Find`；`Assembly-CSharp.csproj` 构建通过，结果为
  `0 error / 101 个项目既有 warning`。
- Unity Inspector、PlayMode 与 Android/iOS 横屏触控尚未验证；若美术重建 prefab 时
  删除或替换节点，必须同步重新绑定字段。
- 回滚时把 prefab 的 8 个引用置空，并恢复原 `ResolveReferences` 与名称查找方法即可；
  本次不涉及数据迁移。

## 13. 2026-07-30 PencilSkinPopup 接入 InfiniteScrollView

### 13.1 需求、基线与实现

- 需求：`PencilSkinItem` 与 `SkinItem` 一样由 `InfiniteScrollView` 管理，使两个商店界面
  使用一致的 `SetData + Item.Bind` 基础列表生命周期；Pencil 笔刷不增加 SkinPart 的
  临时试穿 Preview 状态，点击 Item 后直接执行切换、金币购买或广告解锁。
- 修改前 Popup 会按表行数手工 `Instantiate` 全部 Item、缓存 `_items`，并自行根据
  `GridLayoutGroup` 计算 Content 高度；切换或购买后会遍历全部 Item 重新绑定。
- `InfiniteScrollView` 原实现把槽位 prefab、绑定、清理和缓存类型固定为 `SkinItem`。
  本次保留原 `SetItemPrefab(...)`、`SetItemPrefabSelector(...)`、`SetData(...)`、
  `TryFocusItem(...)` 和 `TryGetBoundItem(...)` 行为，新增显式的
  `SetComponentItemPrefab(...)` / `SetComponentData(...)` 通用入口。
- 通用槽位内部使用 `MonoBehaviour` 保存 prefab 与实例，业务控制器必须提供绑定和清理
  回调；SkinPart 的 Restore 行和引导仍走原 `SkinItem` 强类型接口，没有改成 Pencil
  业务或引入新的共享状态。
- `PencilSkinPopup` 删除 `_items`、`EnsureItemCount(...)`、
  `RefreshContentSize(...)` 和 Content 运行时查找。Popup prefab 直接绑定
  `InfiniteScrollView` 与 `PencilSkinItem.prefab`，初次打开重置列表位置，装备/购买/
  广告结果只刷新当前可见槽位并保持滚动位置。
- `PencilSkinItem.Bind(...)` 仍使用 `DrawBrushStyleAsset.PreviewSprite` 更新
  `mainImg`；这里的 Preview 只表示商品缩略图，不是 SkinPart 的临时试穿状态。

### 13.2 Prefab、数据与交互流

- `PencilSkinPopup.prefab` 的原 Pencil 网格继续使用 272×327 单元、横向 30/纵向 20
  间距和 4 列布局；恢复其 `ScrollRect`，并把同节点 `InfiniteScrollView` 显式绑定到
  原 viewport/content。
- 工作区中额外复制的 190×239 Skin 列表不是 Pencil 运行时数据源，当前设为 inactive，
  避免它遮挡原网格或加载 Actor `SkinItem` prefab。
- 数据流保持：
  `PencilSkinShop` 顺序 → `PencilSkin` 配置 → `InfiniteScrollView` 可见索引 →
  `PencilSkinPopup.BindSkinItem(...)` → `PencilSkinItem.Bind(...)`。
- 点击流保持：
  `Normal` 直接写入并保存 `CurrentSkinId`；
  `CoinUnlock` 检查/扣除金币后授予并选中；
  `AdUnlock` 等待奖励广告成功后授予并选中。没有新增确认页、试穿字典、Restore 行、
  Actor Skin 存档或全局事件。

### 13.3 验证、风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore -nologo -v:minimal` 通过，
  结果为 `0 error / 1119 个项目既有 warning`。
- 两个修改脚本的定向 `git diff --check` 通过；prefab 静态检查确认 70 个 YAML 对象
  fileID 无重复，Popup、ScrollRect、viewport、content 和 PencilSkinItem prefab 引用
  均存在。
- 静态回归确认 `ShopPanel_SkinPart` 仍调用原 `SkinItem` 版 `SetData(...)`，
  Restore selector 与两个引导查询入口未删除；尚未执行 ShopPanel Skin PlayMode 回归。
- 尚未执行 Unity PlayMode 和 Android/iOS 横屏验证。需要验证 8 项 4×2 布局、
  多于可见容量后的快速滚动复用、关闭重开、装备后不跳顶、金币和广告成功/失败回调，
  以及 ShopPanel Skin 的筛选、Restore 行和引导定位。
- 回滚时删除 `InfiniteScrollView` 的通用入口并恢复其 `SkinItem` 内部槽位类型；
  `PencilSkinPopup` 恢复手工 Item 缓存与 Content 高度计算；prefab 恢复原 Content 引用
  和 GridLayoutGroup 驱动。DrawGame 存档与两张配置表无需迁移。

## 14. 2026-07-30 默认笔刷 Texture Cover Noise 试验

### 14.1 需求、基线与修改

- 需求：当前默认笔刷质感较差，先让 `301001` 使用 noise 图验证连续笔画表现。
- 修改前 `DrawLineFeature` 已能读取材质与 Gradient，但未消费
  `DrawBrushStyleAsset.coverTexture`、`coverTint`、`coverTiling` 或
  `coverStrength`；运行时固定 Stretch UV。
- 当前工作区曾把同一张 UI 刷子 Alpha 图同时放入材质 `_MainTex` 和
  `301001.coverTexture`。该图不是无缝 noise，长笔画会整体拉伸。
- 修改后由 `DrawLineFeature` 缓存 Texture Cover 配置并通过
  `MaterialPropertyBlock` 写给 LineRenderer；有有效 Cover 时使用 Tile，无 Cover 时保留
  Stretch。共享 `DrawLineGradient.mat` 继续只读，不在运行时修改。
- `DrawLineGradient.shader` 新增 Cover 纹理、Tint、平铺与强度参数，以 Cover 红通道和
  Alpha 调制 Gradient 基础线条；材质 `_MainTex` 清空为 Shader 默认白纹理。
- `PencilSkin_301001` 当前使用项目既有 `MMBlueNoise.png`，参数为平铺 10、强度 0.45；
  `301002～301008` 未在本次批量配置 Cover，避免未验证视觉扩散到全部皮肤。

### 14.2 系统边界、验证、风险与回滚

- 数据流为：
  `PencilSkin_301001.asset` → Bear `ResourceManager` →
  `DrawLineFeature.LoadBrushStyleFromInspectorId()` → 运行时 Cover 缓存 →
  `ApplyDrawingStyle()` → `MaterialPropertyBlock` → `DrawLineGradient.shader`。
- 本次不接 Popup 当前选择 ID、DB/Save、Luban、广告、IAP、analytics、全局事件或
  Sprite Cover/Mask；画线物理、墨水和关卡胜负链不变。
- C# 构建通过，结果为 `0 error / 101 个项目既有 warning`；定向 diff、Shader/C# 属性
  名称、资源 GUID 和 noise Import 设置静态核对通过。Unity 实时 Editor 已完成 Shader
  导入，当前日志未见本 Shader 编译错误。
- Unity PlayMode、Android/iOS 横屏和低端机尚未验证；首轮参数可能过密或颗粒过弱，
  需重点观察短线、长线、急转角、圆头、缩放和多线重叠。
- 每个启用 Cover 的线条增加一次像素纹理采样；PropertyBlock 只在应用样式时写入，
  不新增每帧分配或资源加载。
- 快速回滚：清空 `301001.coverTexture` 或把 `coverStrength` 设为 0，即可回到原
  Gradient/Stretch 表现；完整回滚路径见
  `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl功能与风险评估.md` 第 39 节。

## 15. 2026-07-30 Brush Texture Repeat 与 PS 笔刷近似

### 15.1 配置入口与运行时表现

- `DrawBrushStyleAsset.lineMaterial` 所引用材质的 `_MainTex` 保持为 Base Texture；
  它也可以承载沿线重复的基础笔刷图案。Shader 属性 ID 未改，已有材质引用不需要迁移。
  不同笔刷可以通过不同共享材质配置各自 `_MainTex`。
- `DrawLineFeature` 统一使用 `LineTextureMode.Tile`。Base Texture 参数和 TipsLine
  使用同一语义：`Base Repeat` 决定单位路径长度的完整纹理次数，
  `Base Segment Spacing` 决定每张完整纹理后的额外透明长度，完整周期为
  `1 / Base Repeat + Base Segment Spacing`。正数产生空白；负数缩短相邻印章起点距离，
  Shader 会从仍覆盖当前片元的印章中选择 Alpha 最强者，并按 `Base Alpha Cutoff`
  硬裁切，形成不累积透明度的真实重叠。Gradient 的整体透明控制仍保留。
- Base Texture 只提供 Alpha 笔刷形状，RGB 不参与输出；通过 Cutoff 的片元颜色完全
  来自 LineRenderer Gradient 与材质 Tint。
- 负间距最多合成四层，最小有效值为 `-0.75 × (1 / Base Repeat)`；继续减小不会增加
  重叠层数，以避免移动端纹理采样无上限增长。负间距分支最多进行四次 Base Texture
  采样，应在目标移动设备验证 fill-rate。
- Shader 在纹理段内把 U 重新归一化为 `0..1`，间隔段将 Alpha 置零，横向 V 保持
  单个笔刷截面。因此 Base Texture 即使是 Clamp，也能完整重复，且无需在图片中预留
  透明边来控制 spacing。`_MainTex` 使用 `NoScaleOffset`，避免旧 Tiling X 和
  `Base Repeat` 重复控制。
- `Base Rotation` 以角度制逐印章旋转 Base Alpha Mask；旋转中心固定为 UV
  `(0.5, 0.5)`，超出纹理方形的部分透明，不产生 Clamp 边缘拉伸。
- Cover / Mask 已从配置资产、运行时传参和 Shader 中移除，不再存在第二层纹理采样。
- 当前 `DrawLineGradient.mat._MainTex` 仍为空，本次只提供能力，不擅自把
  `DrawGame_TipsLine.mat` 的圆点纹理或其它资源设为玩家笔刷。

### 15.2 验证、风险与回滚

- C# 构建通过（`0 error / 101 个项目既有 warning`），定向 diff 检查通过；Unity 日志
  检测到 Shader 更新，本次窗口未见相关 Shader error。
- PlayMode、Android/iOS 尚未验证；需要用目标 brush texture 检查短线、长线、锐角、
  圆头、Base Repeat、Base Segment Spacing、Base Alpha Cutoff、硬边锯齿、MipMap 和
  移动端压缩。
- 当前方案仍是带状网格 Repeat，不包含 Photoshop 的压力、散布、角度随机和独立 Stamp；
  急转角可能折弯一个纹理周期。若要求每次盖印完整且独立旋转，需要另行采用沿路径生成
  Quad/实例的笔刷系统。
- 回滚 Shader 周期 UV 与 DrawLineFeature 强制 Tile 即可；不涉及 PencilSkin 配置表、
  DB/Save、广告、IAP、analytics、资源地址或现有资产数据迁移。详细记录见
  `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl功能与风险评估.md` 第 42 节。

## 16. 2026-07-30 Cover / Mask 表现暂时撤除

### 16.1 修改结果

- `DrawBrushStyleAsset` 现在只保留 Brush ID、预览 Sprite、Line Material、
  Color Gradient 和 Line Width；Cover Texture、Cover Sprite、Mask Sprite、
  Sprite Cover Material、Tint、Tiling、Strength 及相关 API 全部删除。
- `DrawLineFeature` 不再从笔刷资产读取 Cover，也不再创建或写入
  `MaterialPropertyBlock`；共享材质 Inspector 与运行时不会再出现隐藏的 Cover 差异。
- `DrawLineGradient.shader` 不再显示或采样 Cover；当前只采样 Base Texture Alpha，
  完成 Rotation、Repeat、正负 Spacing、最多四层重叠 Mask 和 Cutoff 后输出
  Gradient × Tint。
- 301001～301008 的 PencilSkin 资产已删除旧 Cover / Mask 序列化字段。

### 16.2 风险与回滚

- 当前 Cover / Mask 能力被完整撤下，后续若恢复必须同时恢复配置字段、消费 API、
  PropertyBlock 和 Shader，避免只恢复一半再次产生 Inspector/运行时状态差异。
- Base Texture、Gradient、Tint、宽度、预览图和 Brush ID 不受影响；PlayMode 与
  Android/iOS 的最终视觉仍需验证。
- Assembly-CSharp 构建通过（`0 error / 1119 个项目既有 warning`）；Shader 旋转
  尚未完成目标纹理的 PlayMode 和移动端视觉验证。

## 17. 2026-07-30 当前 PencilShop 选择接入玩家画线

### 17.1 运行时契约

- `PencilSkinPopup` 已有选择链保持不变：拥有皮肤被选中后写入
  `DB.DrawGame.CurrentSkinId`，并调用 `DB.SaveDrawGame()`。
- `DrawLineFeature` 现在把该存档字段作为当前笔刷 ID，在初始化及每次有效新笔画创建前
  同步。只有 ID 变化才加载 `Resources/PencilSkinConfig/PencilSkin_{id}`，因此玩家在
  Draw HUD 中切换 PencilShop 皮肤后，下一笔即可使用对应 Material 与 Gradient。
- 切换不追溯修改已完成线条，也不在一笔绘制过程中替换样式；Inspector 的
  `brushStyleId` 仅作为空选择或无效配置的回退项，最终默认值为 `301001`。
- 本次没有新增全局事件、每帧轮询、存档写入或商城职责；碰撞、墨水、线段平滑和 Shader
  Base Texture 参数不受影响。

### 17.2 验证、风险与回滚

- C# 构建通过（`0 error / 101 个项目既有 warning`），定向 diff 检查通过。
- Unity PlayMode、关卡内商店切换、无效 ID 回退和 Android/iOS 横屏尚未验证。
- 同一无效 ID 不会在每次落笔重复加载；若同 ID 资源在运行中被热修复，需要重新初始化
  或切换一次 ID 才会再次加载。
- 回滚仅需恢复 `DrawLineFeature` 初始化时单次加载 Inspector ID；PencilShop 与
  DB/Save 数据无需迁移。

## 18. 2026-07-30 当前使用项放大 1.05 倍

### 18.1 需求与实现

- 修改前 `PencilSkinItem.Selected` 只切换选中节点和勾选标记，Item 根节点缩放与其它
  状态一致。
- 修改后 `PencilSkinItem.SetState()` 在 `Selected` 状态下把 Item 根节点
  `localScale` 设置为 prefab 初始值的 `1.05` 倍；`Normal`、`AdUnlock`、
  `CoinUnlock` 和 `None` 均恢复初始缩放。
- 初始缩放只缓存一次，状态刷新始终根据该基线计算，不在现有缩放上继续相乘。因此
  Popup 刷新、选择其它笔刷、解锁后重绑和复用项 `Clear()` 不会产生累计放大或残留。
- 选中状态仍由 `PencilSkinPopup.ResolveItemState()` 根据
  `DB.DrawGame.CurrentSkinId` 判定；本次只修改 UI 表现，不改配置、购买、广告、
  DB/Save、资源加载、事件或玩家画线逻辑。

### 18.2 验证、风险与回滚

- `Assembly-CSharp.csproj` 构建通过（`0 error / 101 个项目既有 warning`），
  `PencilSkinItem.cs` 定向 `git diff --check` 通过。
- 尚未执行 Unity PlayMode 和 Android/iOS 横屏验证；需确认列表边缘 Item 放大后不会
  被 ScrollRect Mask 裁切，也不会与相邻 Item 产生不可接受的视觉重叠。
- 当前为即时缩放，不包含 Tween 动画；回滚时移除 `SetState()` 中的
  `ApplySelectedScale()` 调用及对应初始缩放缓存即可。

## 19. 2026-07-30 Base Texture 透明边缘抗锯齿

### 19.1 Shader 行为

- `DrawLineGradient` 原本已经位于 Transparent 渲染队列并使用标准 Alpha Blend；
  锯齿来源是 `_BaseCutoff` 最终执行二值 `clip`，不是渲染队列设置缺失。
- 首轮只对 Base Mask 阈值做导数平滑，用户运行反馈没有明显抗锯齿表现；该方案未覆盖
  Base Texture 横向铺满时仍然存在的 LineRenderer 网格硬边。
- 现在以 `fwidth` 根据屏幕像素导数计算 Cutoff 周围约一像素宽的 Alpha 覆盖率，再用
  `smoothstep` 平滑过渡。RGB 仍是 Gradient × Tint 的纯色，最终 Alpha 改为
  `LineRenderer Alpha × Base Texture Alpha × 抗锯齿覆盖率`，会保留笔刷纹理内部的
  半透明浓淡；完全不可见的片元继续丢弃。
- 额外使用 `lineUv.y` 计算到 LineRenderer 两侧边缘的距离并生成一像素淡出覆盖率，
  即使 Base Texture Alpha 为整块不透明，也能软化线宽两侧的几何轮廓。
- Repeat、正负 Spacing、最多四层印章 Alpha 取最大值和 Rotation 保持原逻辑，且没有
  新增 Base Texture 采样。材质和 301001～301008 配置资产无需迁移。

### 19.2 验证、风险与回滚

- 定向 diff 检查通过；Unity 日志检测到 Shader 更新，当前检查窗口未见相关 Shader
  编译错误。
- 目标笔刷的 PlayMode、Android/iOS、不同缩放和分辨率尚未验证；如果纹理 Import 使用
  Point Filter、分辨率过低或 MipMap 不适合，仍可能需要单独调整纹理资产。
- 当前修正主要覆盖线宽两侧，圆帽最外缘和急转角仍可能需要 URP 2x MSAA；不建议为了
  局部残余锯齿直接扩大透明边缘宽度，避免整体线条变虚。
- 同一 Repeat 笔画内的多个印章仍先取最大 Base Alpha；不同线条对象之间使用标准透明
  混合，半透明区域交叠后可能变浓，需要结合目标视觉确认。
- 回滚为恢复硬 Cutoff `clip`；不涉及 C#、配置、材质参数或存档迁移。

## 20. 2026-08-03 金币购买二次确认与 PropBlock 记录

### 20.1 需求、基线与修改后行为

- `PencilSkinPopup.prefab` 已嵌套公共 `Assets/Game/UI/Global/Prefabs/PropBlock.prefab`，实例名覆盖为
  `PropBlock`。它是商店顶部货币展示，不由 `PencilSkinPopup.Generated.cs` 生成独立字段；本轮记录现状，不修改 prefab 或 PropBlock 脚本。
- 修改前点击金币未解锁画笔会在余额检查后立即扣除金币并解锁，没有二次确认。
- 2026-08-03 初版二次确认仍在创建 `CommmonPopup` 前检查余额；余额不足会直接显示 `U_HurryTips_NeedCoin`，没有先进入确认弹窗，与案文顺序不一致。
- 2026-08-04 修复后，点击有效金币解锁项无论当前余额都先显示“是否要花费 X 金币解锁该画笔？”。No 只关闭弹窗；Yes 才进入 `ConfirmUnlockWithCoin()`，重新检查拥有状态、Bag、无限金币和实时余额。余额不足显示 `U_HurryTips_NeedCoin`，余额足够才执行既有扣费、解锁、选中、保存、音效和成功提示。
- `CommmonPopup.Create()` 在写入本次标题、正文和回调后立即刷新文本，避免复用缓存实例时沿用上一次画笔价格；不修改公共弹窗 prefab、按钮行为或层级。
- 重复点击不会叠加多个确认弹窗；关闭 PencilSkinPopup 或销毁 View 时会一并销毁尚未处理的确认弹窗，避免父界面关闭后仍完成购买。
- 已拥有皮肤在确认回调到达时不会再次扣费，而是直接切为当前皮肤并刷新列表。

### 20.2 边界、验证与风险

- 特殊解锁按本轮决策延期，不纳入这次商店完成范围；广告解锁流程不变。
- 确认标题复用 `U_BrushSkin_Tips_01`，正文当前按需求使用中文动态价格文本。`uncertain`：正文尚无独立多语言 key，非中文语言环境的最终文案需策划/本地化确认。
- 已完成 CodeGraph 调用链和定向 `git diff --check`；`Assembly-CSharp.csproj` 构建通过（`0 error / 102 warning`）。提交前复核发现公共弹窗缓存复用不会重新执行 `Start()`，已补充 `Create()` 即时刷新；Unity PlayMode 尚未验证 No、Yes、不同价格连续购买、余额变化、无限金币、父弹窗关闭和移动端横屏触控。
- 既有扣币后 `DB.SaveDrawGame()` 失败不回滚金币的事务风险没有在本轮扩大或修复；如需处理应单独确定 Bag 与 DrawGame 存档的原子策略。
- 回滚时删除确认弹窗字段、创建/清理方法，并把扣费链恢复到 `UnlockWithCoin()` 直接执行；不涉及 prefab、配置或存档迁移。

### 20.3 2026-08-04 金币不足流程顺序修复验证

- 影响范围仅为 `PencilSkinPopup.UnlockWithCoin()` 的确认前门禁；`CommmonPopup_2` prefab、No/Yes 按钮行为、Bag 数据、无限金币、扣费类型、解锁存档、成功音效、广告购买和 `PropBlock` 均未修改。
- CodeGraph 复核确认 `UnlockWithCoin()` 只创建确认弹窗，余额读取与不足提示只存在于 Yes 回调到达的 `ConfirmUnlockWithCoin()`；弹窗期间余额发生变化时也以 Yes 时的实时余额为准。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 `0 error / 1026 warning`；warning 未指向本次修改文件。定向 `git diff --check` 通过，仅有工作区行尾转换提示。
- `uncertain`：尚未执行 Unity PlayMode、Android/iOS 横屏与真机验证。至少需要覆盖余额不足、余额足够、No、重复点击、弹窗期间余额变化、无限金币和父界面关闭。
- 仅回滚本次顺序修复时，可在 `UnlockWithCoin()` 恢复确认前的 Bag/余额检查；这会重新产生“金币不足绕过 CommonPopup”的案文差异，不涉及数据迁移。

## 21. 2026-08-05 DrawBrushStyle 预览图 Inspector 可视化

### 21.1 需求、基线与修改后行为

- 修改前 `DrawBrushStyleAsset.previewSprite` 仅使用 Unity 默认对象引用字段，选择笔刷配置资产时不能直接查看较大的图片预览。
- 修改后该字段使用 Odin `PreviewField`，以 120 像素、左对齐方式显示 Sprite 预览；原有 Sprite 引用、序列化字段名、运行时 `PreviewSprite` 只读 API 均保持不变。
- 影响文件仅为 `Scripts/DrawBrushStyleAsset.cs`；不修改 PencilSkin 配置资产、Prefab、DB、存档、资源路径、事件、分析或移动端运行时流程。
- 本次只增加 Inspector 绘制特性，不新增 `OnValidate`、自定义 Editor、实例创建或运行时数据访问。

### 21.2 验证、风险与回滚

- 定向源码扫描未发现新增 `OnValidate`、DB、单例或实例创建路径；`dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` 通过（`0 error / 1119 warning`），warning 均为项目既有告警；定向 `git diff --check` 通过。
- `uncertain`：尚未在 Unity Inspector 中人工打开 301001～301008 资产确认实际尺寸与布局；Odin 未启用或 Inspector 被其它自定义编辑器接管时，预览效果需在 Unity 内进一步确认。
- 回滚时移除 `PreviewField` 特性及 `Sirenix.OdinInspector` using 即可；无需迁移任何资产或数据。

## 22. 2026-08-06 PencilSkin 获取与消费打点

### 22.1 需求、基线与影响范围

- 需求：PencilSkin 首次获取时复用普通 Skin 的 `getNewSkin` 事件；金币购买的消费类型使用独立的 `CostType.PencilSkin`。
- 修改前金币与广告解锁只写入 `DB.DrawGame.OwnedSkin`、选择并保存，没有发送 Skin 获取事件；金币扣除使用 `CostType.Skin`，无法在消费打点中区分角色 Skin 与 PencilSkin。
- 影响文件为 `PencilSkinPopup.cs`。`SimpleBag.cs` 在本轮开始前已有工作区改动，为 `CostType` 固定原值并增加 `PencilSkin = 2`；本轮保留该改动并接入调用，没有覆盖该文件的既有修改。
- 复用 `GameSDKService.GetNewSkin(...)`，不新增 SDK 事件名、参数字段、存档字段、配置、资源、UI 或全局事件。

### 22.2 修改后数据与事件流

- 金币确认购买扣费改为 `RemoveTool(GameProps.Coin, price, CostType.PencilSkin)`；既有 `spend_virtual_currency.item_category` 因此上报 `PencilSkin`。金币无限状态仍不实际扣费，也不会产生消费事件。
- 金币解锁把 `GetSkinWayType.Purchase` 传入统一授予方法；广告成功把 `GetSkinWayType.Ad` 传入。
- `GrantAndSelectSkin(...)` 在写入前记录是否为首次拥有；只有首次加入 `OwnedSkin` 时才调用 `GameSDKService.GetNewSkin(skin.Id, getType)`，事件仍为 `getNewSkin`，字段仍为 `skin_id` 与 `getType`。
- 已拥有 PencilSkin 的普通选择、重复回调和默认笔刷补齐不会发送获取事件。广告失败/关闭不会写入存档或发送获取事件。
- 获取事件在 `DB.SaveDrawGame()` 尝试之后发送；保存失败仍沿用既有行为保留内存中的拥有状态并记录警告，因此本轮也按内存首次获得发送事件。

### 22.3 验证、风险与回滚

- CodeGraph 与精确调用扫描确认 `GrantAndSelectSkin(...)` 只有金币购买和广告成功两个调用点，分别传入 `Purchase` 与 `Ad`；获取事件受 `isNewSkin` 门禁保护。
- `git diff --check` 对 `PencilSkinPopup.cs` 与当前 `SimpleBag.cs` 差异通过，仅输出工作区换行符提示。
- `Assembly-CSharp.csproj` 构建被本地缺失的 `Temp/bin/Debug` 引用程序集阻断，输出 200 个 `CS0006`，未取得本次脚本的完整编译结论。
- `uncertain`：尚未在 Unity PlayMode、真实 SDK 后台、Android 或 iOS 验证金币购买、广告成功、重复选择的事件次数与 `getType` 序列化结果。
- 回滚时把 PencilSkin 金币扣费恢复为 `CostType.Skin`，移除授予方法的 `GetSkinWayType` 参数、`isNewSkin` 门禁后的 `GetNewSkin(...)` 调用；若不再需要独立消费类型，再单独回滚 `SimpleBag.cs` 中现有的 `PencilSkin = 2`。

## 23. 2026-08-06 Draw 虚拟货币事件 level_name 修复

### 23.1 需求、基线与影响范围

- 需求：位于 Draw 关卡内购买金币或画笔皮肤，以及退出 Draw 返回选关界面后执行相同操作时，
  `earn_virtual_currency` 与 `spend_virtual_currency` 的 `level_name` 都应等于最近一次 Draw
  `level_start.level_name`。
- 修改前两个事件共同调用 `GetCurrentLevelPathOrDefault()`，该方法只读取主线
  `PlayCtrl.Level.CurrentLevelData.Path`；因此 Draw Level3 场景错误上报上一次主线 `Level_002`，
  而不是 `DLevel_003`。
- 修复位于共享 `GameSDKService.Game.cs`，不修改 `SimpleBag`、金币商店、PencilSkinPopup、存档、
  配置、资源、UI、事件名或事件字段结构。

### 23.2 修复后事件流

- `GameSDKService` 在运行时记录最近一次 `level_start` 的 `levelPath`；主线详细开始事件和
  `DrawLevelStartEvent()` 都会刷新该上下文。
- `EarnVirtualCurrentcy()` 与 `SpendVirtualCurrentcy()` 共同读取最近开始关卡路径；退出关卡不会清理，
  所以 Draw 内和退出 Draw 后的金币获取/消费均保持最近 Draw 关卡名。下一次进入主线或另一 Draw
  关卡并发送 `level_start` 时自然覆盖。
- 本会话尚未出现 `level_start` 时回退到原主线当前关卡解析，不新增持久化字段，也不改变应用重启后的
  存档结构。

### 23.3 验证、风险与回滚

- 静态调用核对确认 `SimpleBag.AddTool()` 与 `RemoveTool()` 分别唯一进入两个虚拟货币事件；两个事件已
  切换到同一最近开始关卡解析方法，主线和 Draw 开始入口均更新该值。
- `git diff --check` 通过；`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo`
  通过，结果为 `0 error / 101 warning`，warning 为工程既有项。
- `uncertain`：尚未在 Unity PlayMode、真实 SDK 后台、Android/iOS 验证 Draw 关内购买、退出后选关商店
  购买、随后进入主线再购买的事件序列。
- 回滚时删除最近关卡路径字段与记录方法，并把两个虚拟货币事件恢复为直接调用
  `GetCurrentLevelPathOrDefault()`；无需迁移存档、配置或资源。

## 24. 2026-08-07 金币不足跳转商店

### 24.1 需求、基线与修改后行为

- 需求：PencilSkinItem 的金币画笔购买在确认后发现金币不足时，应跳转到金币商店页面。
- 修改前 `PencilSkinPopup.ConfirmUnlockWithCoin()` 在排除无限金币后只显示
  `U_HurryTips_NeedCoin` 并返回，玩家仍停留在笔刷商店，没有直接补充金币的入口。
- 修改后该分支先调用 `ShopPanel.Create(false, nameof(PencilSkinPopup))` 打开公共商店，再保留原金币不足提示。
  商店来源名用于既有 `IAP_IMP` 展示打点；`resumeGame` 保持 `false`，不引入新的暂停或恢复职责。

### 24.2 数据、UI 与系统边界

- 余额判断、无限金币豁免和实际扣费仍由 `PencilSkinPopup` 负责；`PencilSkinItem` 只负责显示状态与转发点击。
- 只有确认弹窗 Yes 后重新校验出的普通金币不足分支会打开商店；No、余额足够、无限金币、已拥有和广告解锁路径不变。
- 本次不修改 Bag、DrawGame 存档、PencilSkin 配置、Prefab、资源地址、全局事件、商品配置或平台 SDK 接口。

### 24.3 验证、风险与回滚

- 静态调用核对确认 DrawGame 既有金币不足路径也使用 `ShopPanel.Create(false, 来源名)` 后显示同一不足提示。
- `git diff --check` 通过；`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo`
  通过，结果为 `0 error / 101 warning`，warning 为工程既有项。
- `uncertain`：尚未在 Unity PlayMode、Android/iOS 横屏或真实 IAP 环境验证弹窗层级、返回笔刷页、连续点击与购买金币后的刷新表现。
- 回滚时仅删除金币不足分支中的 `ShopPanel.Create(false, nameof(PencilSkinPopup))`，即可恢复原先只提示不跳转的行为；无需迁移数据或资源。

### 24.4 2026-08-07 购买确认弹窗关闭顺序

- 根因：`CommmonPopup` 原先先执行 Yes 回调，PencilSkin 余额不足回调会同步打开 `ShopPanel`；新页面进入 UI 栈后，原确认弹窗再执行关闭时已不再是栈顶，因此可能残留。
- 修复后 Yes/No 都由 `CommmonPopup` 先关闭自身，再执行对应业务回调。Yes 随后执行 PencilSkin 余额二次校验，余额不足才打开 `ShopPanel`；No 随后只清理 PencilSkin 本地弹窗引用。
- 余额足够扣费、画笔授予、无限金币、广告解锁和父界面关闭清理流程不变；不增加延迟帧、全局事件或新的 UI 栈操作。
- 静态扫描确认 `CommmonPopup` 当前外部调用仅覆盖 PencilSkin 购买确认与主线/Draw 退出确认；退出回调改为弹窗关闭后执行，不改变退出目标。
- `git diff --check` 通过；`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo`
  通过，结果为 `0 error / 101 warning`，warning 为工程既有项。
- `uncertain`：尚未在 Unity PlayMode、Android/iOS 横屏验证 Yes、No、余额不足打开 ShopPanel、返回笔刷页与快速连续点击。
- 仅回滚本顺序修复时，恢复 `CommmonPopup` 的回调后关闭顺序即可；无需修改 PencilSkin 数据或资源，但会重新引入弹窗残留风险。
