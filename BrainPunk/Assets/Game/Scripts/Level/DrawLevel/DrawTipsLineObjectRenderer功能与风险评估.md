# DrawTipsLineObjectRenderer 功能与风险评估

## 需求与背景

- 在现有 `LineRenderer.Positions` 路径版本之外，增加直接通过场景子节点编辑路径的版本。
- 用户明确不要求每个圆点是独立 GameObject；圆点应由 `DrawGame_TipsLine.mat` 的纹理 Repeat 直接渲染。
- 路径来源与渲染方式解耦：子节点只提供控制点，最终仍由一个 MeshFilter、一个 MeshRenderer 和一个合并 Mesh 渲染。

## 影响文件、资源与系统

- 运行时组件：`Assets/Game/Scripts/Level/DrawLevel/DrawTipsLineObjectRenderer.cs`。
- 呼吸透明度组件：`Assets/Game/Scripts/Level/DrawLevel/BreathLineAlphaHandle.cs`。
- 固定材质：`Assets/Game/ArtSrc/Materials/DrawGame_TipsLine.mat`。
- 路径来源：`PathRoot` 的直接子节点；未绑定 `PathRoot` 时使用组件自身的直接子节点。
- 运行时显示：组件所在节点的 `MeshFilter` 与 `MeshRenderer`。
- 不新增圆点 GameObject、对象池、资源地址、DB、存档、配置、事件、分析打点或平台 SDK 依赖。

## 原行为基线

- 第一版子节点实现会根据等距采样结果复制 `DotTemplate`，每个圆点对应一个 GameObject。
- 该方式可以逐点控制，但对象、Transform 和 Renderer 数量随圆点数增长，且材质、UV 和尺寸依赖模板自身。
- `DrawTipsLineRendererNormalizer` 使用单 Mesh，但路径数据来自 `LineRenderer.Positions`，不是子节点列表。

## 修改后行为

- `PathRoot` 的直接子节点按 Hierarchy 中的 sibling 顺序直接组成路径，不再保存或手工维护 `pathPoints` 列表。
- 相邻有效控制点各生成一个固定宽度矩形带；所有线段的顶点、UV、颜色和三角形合并到单个 Mesh。
- UV 的 U 值使用 `uvOffset + 累计路径长度 × repeat`，不会在折点处从零重新开始。
- `repeat` 表示纹理每一个本地长度单位重复的次数；默认 `4.347826` 时，单个完整纹理周期约为 0.23 本地单位，周期长度不由纹理像素尺寸决定。
- `uvOffset` 调整纹理起始相位，可以移动首个圆点相对路径起点的位置。
- 基础材质在编辑器中规范为 `Assets/Game/ArtSrc/Materials/DrawGame_TipsLine.mat`；生成提示线时，每个组件复制一份临时材质实例并赋给 MeshRenderer，不修改共享材质资源。
- `dotColor` 同步写入临时材质支持的 `_BaseColor`、`_Color` 与 `_RendererColor` 属性；Mesh 顶点色保持白色，避免材质色与顶点色重复相乘。
- `deactivatePathPointsAfterBuild` 只在运行时首次成功生成 Mesh 后关闭控制点；编辑器预览始终保持控制点可见、可直接拖动。
- 编辑器无需点击生成按钮：场景或 Prefab Stage 实例启用后会自动创建临时预览；移动、新增、删除或重排直接子节点，以及修改 Inspector 参数，都会自动刷新。
- Player 构建不包含 Editor-only `LateUpdate`；运行时路径节点移动后仍由调用方显式调用 `RebuildDots()`。`HideDots()` 只隐藏 MeshRenderer。
- `SetAlpha(float)` 只修改当前组件的 `dotColor.a` 并同步临时材质实例，不修改 RGB 或共享材质。
- `BreathLineAlphaHandle` 使用 DOTween 在 `maxAlpha` 与 `minAlpha` 之间按 Yoyo 循环，默认使用 `Ease.InOutSine`；支持开始、暂停、继续和停止。
- 临时材质实例会关闭 `_ALPHATEST_ON`，使透明度可以平滑经过原 Cutoff 0.5，而不是在呼吸中途突然消失。

## 推荐层级与使用方式

```text
ObjectTipsLine（MeshFilter + MeshRenderer + DrawTipsLineObjectRenderer，PathRoot 可指向自身）
├─ Point_00
├─ Point_01
└─ Point_02
```

1. 在 `ObjectTipsLine` 上添加 `DrawTipsLineObjectRenderer`；所需 MeshFilter 与 MeshRenderer 会由 RequireComponent 补齐。
2. 将控制点作为 `PathRoot` 的直接子节点，并按连接顺序排列；不单独绑定 `PathRoot` 时，组件自身就是路径根节点。
3. 直接新增、删除、换序或拖动控制点，编辑器预览会自动首显并实时更新，不需要收集或生成按钮。
4. 调整 `lineWidth`、`repeat`、`segmentSpacing`、`uvOffset`、`segmentTextureVariants` 或 `randomSeed` 后，预览会在下一次编辑器更新中自动重建。
5. MeshRenderer 的 Sorting Layer、Sorting Order 继续直接在 Renderer 上设置。
6. 需要呼吸效果时，在同一节点添加 `BreathLineAlphaHandle`；配置最低/最高透明度和单程时长，`playOnEnable` 开启时会在运行时自动播放。

## 编辑器与运行时边界

- `Reset` / `OnValidate` 负责规范数值、固定材质引用并把预览标记为待刷新；实际临时 Mesh/Material 创建在 Editor-only `LateUpdate` 中完成。
- 场景或 Prefab Stage 实例通过逐实例 `runInEditMode` 获得自动预览；persistent prefab asset、无效或未加载 Scene 不创建预览。
- 编辑器临时 Mesh 与材质使用 `HideFlags.HideAndDontSave`，并由 `SceneView.duringSceneGui` / `Graphics.DrawMeshNow` 直接绘制，不赋给真实 MeshFilter/MeshRenderer，因此自动首显不会新增 Prefab 的 `m_Mesh` 或 `m_Materials` override；`StageUtility.IsGameObjectRenderedByCamera` 防止主场景与 Prefab Stage 串画。
- 无变化时只比较少量直接子节点坐标，不提交 Mesh；`EditorApplication.QueuePlayerLoopUpdate()` 保证首次启用后主动请求一次编辑器更新。
- 组件禁用或销毁时释放编辑器临时 Mesh/Material 并关闭 Renderer；再次启用后会重新自动创建预览。
- 编辑器自动预览不修改控制点 Active 状态；运行时首次构建仍可按 `deactivatePathPointsAfterBuild` 隐藏控制点。
- 运行时 Mesh 与材质实例使用 `HideFlags.DontSave`，组件销毁时统一释放。
- `BreathLineAlphaHandle` 仅在 PlayMode 创建 DOTween；组件关闭或销毁时释放 Tween，停止时可选择恢复最高透明度。
- `useUnscaledTime` 默认关闭；只有暂停期间仍需播放提示线呼吸时才应开启。
- 没有 `ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、Instance、Instantiate 或全局事件。

## 性能与平台考虑

- 连续单纹理且 `segmentSpacing=0` 时，几何量只与有效路径段数相关；启用额外间距或多纹理后，几何量与实际短划线片段数相关，每片 4 顶点、2 三角形并受 `maxSegmentCount` 约束。
- 所有线段仍合并为一个 MeshRenderer，不再产生每圆点 GameObject、Transform 或 Renderer 成本；多纹理按实际有效纹理拆为子网格，最多产生 N 个 draw call。
- 动态路径重建会重新提交 Mesh 数据，适合离散更新，不应无条件每帧调用；编辑器实时预览只在有效本地路径坐标或 Inspector 参数变化时重建。
- 呼吸动画每帧只更新现有材质实例的颜色属性，不重建 Mesh、不创建新材质；每个激活组件持有一个 DOTween。
- 若运行时路径点上挂有动画、脚本或其它业务组件，自动 inactive 会停止这些组件；此类路径应关闭 `deactivatePathPointsAfterBuild`，或使用专用空 Transform 作为控制点。
- Android/iOS 横屏真机的透明 Overdraw、纹理采样、不同分辨率与非均匀父缩放仍待验证。

## Repeat 方案边界

- 每段使用独立矩形带，因此不会产生 LineRenderer 锐角处的连续带状网格挤压。
- UV 相位在折点连续；如果折点恰好落在圆点纹理的可见区域中，纹理会在折点两侧改变方向，看起来像一个圆点被折开。这是直接 Repeat 方案的固有限制。
- 如果必须保证任何折点附近的圆点都完整不折转，应使用 `DrawTipsLineRendererNormalizer` 的独立圆点 Quad 方案，而不是连续 Repeat 纹理。
- 非均匀缩放会改变世界空间线宽和 Repeat 周期；建议提示线根节点保持等比缩放。
- 当前固定材质已声明 `_BaseColor`、`_Color` 与 `_RendererColor`；脚本兼容性地同步所有存在的颜色属性。最终透明度、混合与色彩空间表现仍需在 Unity Scene/Game 视图确认。
- 关闭 Alpha Clip 后，纹理透明边缘改由透明混合处理，可能增加少量透明 Overdraw；移动端横屏实际画面和性能仍需真机确认。

## 验证记录

- 使用 Unity 6000.0.59f2 自带 Roslyn，按 Editor 与 Player 引用对修改后的组件执行定向编译，两套编译均为 0 error；仅有序列化字段未由纯 C# 编译器赋值的既有 CS0649 警告。
- `BreathLineAlphaHandle` 与新增 `SetAlpha`、平滑透明材质配置共同执行 Editor/Player 定向编译，两套均为 0 error；纯 C# 编译器仅报告序列化字段 CS0649。
- 静态检查要求：`OnValidate` 不直接进入 Mesh 创建路径；组件不包含 `Instantiate` 或 `ContextMenu`，Editor-only `LateUpdate` 不进入 Player 构建。
- 静态检查要求：自动预览只允许 loaded Scene 或 Prefab Stage 实例创建 `HideAndDontSave` 临时资源，persistent prefab asset 必须拒绝创建。
- 静态检查确认编辑器构建不隐藏控制点；只有运行时 Mesh 顶点数大于零且 `deactivatePathPointsAfterBuild` 开启时才关闭控制点。
- 参考路径总长约 10.798；默认 Repeat 下纹理 U 范围约为 0..46.95，与原 0.23 周期约 47 个圆点的密度一致。
- 完整 Unity Scene/Prefab、PlayMode 以及 Android/iOS 横屏画面仍待验证。
- 呼吸节奏、禁用组件后的透明度恢复、TimeScale 为 0 时的行为及透明边缘效果仍需 Unity PlayMode 人工验证。

## 回滚路径

- 恢复上一版 `DrawTipsLineObjectRenderer.cs` 的 DotTemplate 对象池实现，即可回到独立圆点 GameObject 方案。
- 现有 `DrawTipsLineRendererNormalizer` 与 `DLevel_001.prefab` 未被本次切换修改；回滚不涉及数据迁移、存档、配置、事件或资源地址。
- 若只回滚呼吸效果，移除 `BreathLineAlphaHandle.cs`，并撤销 `DrawTipsLineObjectRenderer.SetAlpha` 与临时材质关闭 Alpha Clip 的修改即可；路径 Mesh 与 Repeat UV 数据不受影响。

## 2026-07-29：短划线间距参数

### 需求与影响范围

- 需求是在 `DrawTipsLineObjectRenderer` 增加可直接控制相邻短划线额外空白的 Inspector 参数，并保持原纹理短划线长度不变。
- 仅修改 `Assets/Game/Scripts/Level/DrawLevel/DrawTipsLineObjectRenderer.cs` 的 Mesh 片段与 UV 生成；不修改材质、纹理、prefab、关卡路径、DB、存档、配置、事件、分析打点或平台 SDK。
- 当前工作区 `DLevel_001.prefab` 的目标组件为 `repeat=3`、`segmentSpacing=0.08`；后续多纹理配置只追加纹理列表，未改这两个值或 prefab 的其它未提交修改。

### 修改前后行为

- 原连续 Repeat 行为中，单个完整纹理周期长度为 `1 / repeat`，其中包含原图短划线和纹理自带透明边。
- 第一版间距参数把 `segmentSpacing` 直接换算成 UV 重复频率，导致完整纹理周期被缩放，可见短划线也随间距一起变长或变短；该行为不符合用户澄清后的要求。
- 修正后 `repeat` 始终决定原纹理周期长度；`segmentSpacing` 只在每个原纹理周期末端额外插入本地空白。完整循环长度为 `1 / repeat + segmentSpacing`。
- `segmentSpacing=0` 且没有配置纹理变体时完整走原连续 Repeat 分支，保持已有 Mesh、UV、`uvOffset`、源 prefab `repeat=2.28` 与当前 `DLevel_001` `repeat=3` 覆写行为。
- `segmentSpacing>0` 时，每个原纹理周期按固定长度生成到同一个 Mesh；空白区不生成几何，因此不会拉伸或压缩原纹理。
- 短划线跨折点时按路径段拆成多个矩形片段，各片段使用同一纹理周期内连续的 U 区间，间距和相位不会在折点重置。
- `uvOffset` 在间距模式下表示“原纹理周期 + 额外空白”的完整循环相位；首尾允许显示被路径边界裁切的部分短划线。
- 编辑器按钮预览与运行时 `RebuildDots()` 共用上述生成逻辑。

### 验证、风险与回滚

- 使用 Unity 6000.0.59f2 已生成的 `Assembly-CSharp.rsp` 和 Unity 自带 Roslyn 对当前 `Assembly-CSharp` 定向编译，结果为 `0 error`；输出仅包含仓库既有 warning。
- `git diff --check` 通过；该阶段未新增实例化、DB、Save、RemoteConfig 或全局事件路径。后续新增的 Editor-only `LateUpdate` 见“路径点实时预览刷新”记录。
- `segmentSpacing` 的正值会规范为至少 `0.0001`，避免过小空白造成不稳定的浮点边界切片；`0` 保留为兼容回退值。
- 间距模式的几何量与实际短划线片段数相关；每个片段 4 顶点、2 三角形，并受现有 `maxSegmentCount` 上限约束，不新增每帧重建。
- 相位使整条短路径落在额外空白内时，Mesh 可以为零顶点，现有逻辑会保持 Renderer 关闭且不自动隐藏路径控制点。
- 锐角处仍由独立矩形片段转向，可能存在原方案已有的楔形缝或重叠；非均匀父缩放仍会按方向改变世界空间长度。
- Unity Scene/Game 视图中的短划线长度对比、`segmentSpacing=0.08` 空白、折点 UV 连续性、PlayMode 重建以及 Android/iOS 横屏效果仍待人工验证。
- 回滚时删除间距生成分支与 `segmentSpacing` 字段，恢复所有路径使用连续 Repeat；不涉及 DB、配置、资源地址或存档迁移。

## 2026-07-29：多纹理确定性随机短划线

### 需求、资源与配置

- 需求是在不改变现有短划线物理长度和额外间距的前提下，支持配置 N 张线段纹理，并让每个完整短划线随机选用其中一种，使提示线外观更自然。
- `DrawTipsLineObjectRenderer.cs` 新增 `segmentTextureVariants` 与 `randomSeed`；空列表保持材质原纹理行为，空引用和重复 Texture2D 在显式重建时忽略。
- 新增资源为 `Assets/Game/UI/Global/ui_common_pic_dian_04.png`、`ui_common_pic_dian_05.png`、`ui_common_pic_dian_06.png` 及各自独立 `.meta`。三张图使用 Sprite/Single、sRGB、无 mipmap、Bilinear、Clamp、关闭 Read/Write 和透明导入。
- 当前源 `Assets/Game/Prefabs/LevelItems/DrawGame/DrawGame_TipsObjectSubObject.prefab` 已由制作现场配置 `_04/_05/_06` 三张纹理与 `randomSeed=1`；使用该共享源且没有局部覆写的 Draw 关卡会继承相同随机纹理配置。
- 当前 `DLevel_001.prefab` 对该组件没有纹理列表覆写，直接继承共享源 prefab 的 `_04/_05/_06`；本次实时预览修改不改写该 prefab 状态。
- 资源只通过序列化 Texture2D 引用进入现有 Renderer，不新增 Resources/Addressables/YooAsset 路径，也不访问 DB、存档、配置、事件、分析打点或平台 SDK。

### 生成与渲染行为

- 每个完整短划线周期只选择一次纹理；同一短划线跨折点拆成多个 Quad 时复用同一选择和连续 U 区间。
- 所有纹理都映射到 `1 / repeat` 的同一物理周期，`segmentSpacing` 继续只追加周期后的空白。因此切换纹理不会改变当前短划线长度或间距。
- 多纹理模式即使 `segmentSpacing=0` 也会按完整纹理周期拆片；只有有效纹理列表为空且间距为 0 时才走旧连续 Repeat 分支。
- 随机序列使用组件内部固定 xorshift 状态，只由 `randomSeed`、有效纹理列表顺序和周期推进决定，不调用或修改 `UnityEngine.Random`。N 大于 1 时排除上一索引，避免相邻短划线连续使用同一张图。
- 网格仍共用一组顶点、UV 与颜色；三角形按纹理归入 N 个 submesh。同一份临时颜色材质重复用于所有材质槽，并由每槽 MaterialPropertyBlock 覆盖 `_BaseMap` / `_MainTex`。
- `SetAlpha` 仍只更新一份临时材质，不重建 Mesh，也不随纹理数量循环修改颜色。材质槽和 PropertyBlock 只在 PlayMode 重建或人工首次生成编辑器预览时创建；实时预览只复用，清理预览、材质切换和组件销毁时会先清槽再释放材质。

### 验证、风险与回滚

- Unity 6000.0.59f2 的 `Assembly-CSharp.rsp` Roslyn 编译通过，结果为 `0 error`；输出仅含项目既有 warning。
- 静态检查确认没有新增 `UnityEngine.Random`、`Update` / `LateUpdate` / `FixedUpdate`、Instantiate、DB、Save、RemoteConfig、Instance 或全局事件路径。
- `_04/_05/_06` 三个纹理 GUID 均唯一，当前共享源 prefab 的数组大小为 3 且引用顺序与 Inspector 一致；三个新增 PNG 哈希互不相同。
- 单 MeshRenderer 不等于单 draw call：N 张有效纹理最多产生 N 个 submesh/draw call；当前 DLevel_001 为 4。多条提示线或 N 明显增大时必须在 Android/iOS 代表设备上 Profile。
- `_02` 为 29×15，其余三张为 28×13、27×13、29×13；当前实现把不同宽高比归一到同一物理长度和 `lineWidth`。这满足“间距不改变短划线长度”，但各图会有轻微横纵缩放，最终观感待 Scene/Game 视图确认。
- 透明三角形按纹理分组后，普通不重叠路径无顺序差异；自交路径交叉处的透明叠色顺序可能与纯路径顺序不同。
- Unity 资源重导入、编辑器预览、PlayMode 稳定序列、跨折点复用、呼吸透明度、Android/iOS 横屏画面与性能仍待人工验证。
- 回滚代码时移除 `segmentTextureVariants` / `randomSeed`、子网格与 PropertyBlock 分支；仅回退当前表现时可清空共享源 prefab 的纹理列表。删除 `_04/_05/_06` 资源前必须先移除所有 prefab 引用。

## 2026-07-30：子节点自动路径与关卡默认绑定

### 需求与影响范围

- 原行为依赖序列化 `pathPoints`，第一次预览、收集新增点和显示控制点都需要 Inspector 按钮；新增控制点后还要再次绑定列表。
- 本次要求提示线打开场景即显示，路径永远来自场景物体下的控制点顺序；新增点后无需绑定或点击，直接拖动即可。
- 同时核对 `DLevel_001` 至 `DLevel_010` 的 `DrawLineFeature` 默认引用；十关的 Camera、DrawSpace、DrawArea、LineTemplate、TipsObject 与 DrawingLineMaterial 均已有非空序列化绑定，不需要覆盖制作现场数据。

### 修改后行为与边界

- 删除 `pathPoints` 字段、`SetPathPoints` API 和收集/生成/清理/显示路径点按钮；唯一路径真源为 `pathRoot ?? transform` 的直接子节点，顺序取 `GetChild(i)` 的 sibling 顺序。
- `OnEnable`、`Reset` 与 `OnValidate` 启用逐实例编辑器预览并标记 dirty，同时注册 Editor-only 首次生成回调；回调在资源导入/编译期间持续等待，编辑器空闲后自动重建并注销，避免一次性更新请求在导入期间被跳过。
- Editor-only `LateUpdate` 在预览缺失、参数 dirty 或有效本地路径变化时重建，因此首次显示、Inspector 参数修改以及控制点增删、换序和拖动都无需按钮。
- 路径比较与生成使用同一套相邻重复点过滤和位置容差；新增、删除、重排、移动以及 `positionZ` 变化都会触发刷新，无变化时不提交 Mesh。
- 编辑器重建不再隐藏控制点；共享 Tips prefab、`DLevel_003` 和 `DLevel_007` 的现有控制点已改为 active，其他关卡对共享两个点的 inactive 覆盖已移除。
- 编辑器预览通过 SceneView 回调直接绘制临时 Mesh，不写真实 Renderer 的序列化字段；组件禁用时注销回调并释放资源，重新启用后自动注册并重建。
- 运行态判断改为 `Application.IsPlaying(gameObject)`，避免 PlayMode 中的 Prefab Stage 对象被全局 `Application.isPlaying` 误判为运行时关卡对象。
- 整个 `LateUpdate` 位于 `UNITY_EDITOR` 条件编译内，Android/iOS/Player 构建不包含该轮询；真实 Player 中若业务脚本移动路径点，仍需显式调用 `RebuildDots()`。
- `DrawLineFeature.AutoBind` 兼容真实对象名 `DrawGame_DrawLine` 与 `DrawGame_TipsObjectSubObject`，并保留旧名回退；该工具仍只由 Editor Inspector 手动触发，不挂入 `OnValidate`。

### 验证、风险与回滚

- 静态 YAML 校验通过：十个 DLevel 的六项 `DrawLineFeature` 引用均非空且目标类型正确；DLevel_003 的 127 个 document ID 唯一；源 prefab 和十关无残留 `pathPoints`；DLevel_003 保持 5 点顺序，DLevel_007 保持 3 点顺序，其余关卡至少保留共享的 2 个有效直接子节点。
- `DLevel_001` 至 `DLevel_010` 的有效提示线参数均与第一关一致：`lineWidth=0.15`、`repeat=2.6`、`segmentSpacing=0.2`、三张纹理及顺序、材质、随机种子和 SortingGroup layer/order 均继承同一共享 prefab；DLevel_003 仅显式覆盖 `pathRoot`，目标仍是本关自身提示线根节点。
- 使用 `Library/Bee/artifacts/1300b0aE.dag/Assembly-CSharp.rsp` 定向 Roslyn 编译通过，结果为 `0 error`；输出只有仓库既有 warning。
- 2026-07-30 13:51 Unity Editor 已重新生成并加载晚于源码的 `Assembly-CSharp.dll`，程序集包含首次预览重试回调，Editor.log 未记录本次 C# 编译错误。
- 2026-07-30 13:55 捕获当前 Unity 窗口进行目测：`DLevel_001` SceneView 在未点击生成按钮、未重新绑定控制点的情况下，已在两个直接子节点之间自动显示随机纹理虚线。
- 本次涉及文件的 `git diff --check` 通过；全工作区检查仍被未纳入本次修改的 `DLevel_001.prefab` 四处既有 YAML 行尾空格阻断。
- 首次预览补强不会从 `OnValidate` 直接创建 Mesh/材质；该入口只标记 dirty、注册绘制与首次生成回调，实际创建发生在后续 Editor update 或 Editor-only `LateUpdate`。
- DLevel_001 SceneView 首显已经验证；其余关卡逐一目测、Prefab Stage、连续拖动、禁用后重启、保存 Prefab 无新增 override、PlayMode 和 Android/iOS 横屏仍需人工验证。
- 编辑器直绘预览只显示在 SceneView，且不经过 SortingGroup/Renderer sorting；它用于路径制作反馈，最终层级遮挡与 GameView 表现以 PlayMode 的真实 Renderer 为准。
- Editor-only 坐标比较成本与直接子节点数量线性相关；当前关卡控制点很少。若后续单实例配置大量控制点，需要检查编辑器交互开销。
- 回滚时恢复 `pathPoints` 与人工按钮流程、恢复共享 prefab/关卡控制点 Active 覆盖，并撤销 DLevel_003 默认引用与 `AutoBind` 新名称兼容；运行时 Mesh、间距和随机纹理算法不涉及数据迁移。

## 2026-07-30：场景临时未加载时的首显重试补强

### 问题与修复行为

- 原编辑器首显入口把 `scene.IsValid() == false` 或 `scene.isLoaded == false` 当成永久失败并
  注销 bootstrap。脚本重编译、资源刷新或场景切换窗口内若实例暂时处于该状态，后续没有
  回调重新生成预览，表现为刷新后控制点仍在但提示线消失。
- `EnableEditorAutoPreview()` 现在只对 persistent prefab asset 终止注册；非 persistent 的
  活动实例即使 Scene 暂时无效或未加载，也保留 `EditorApplication.update` bootstrap。
- `TryBuildEditorPreviewWhenReady()` 把编译、资源更新、Scene 暂时无效或未加载都视为可重试
  状态，只返回等待；Scene 恢复 valid 且 loaded 后再重建预览并注销 bootstrap。
- runtime 对象、组件失活或 persistent asset 仍属于终止状态并立即注销；`OnDisable` 与
  `OnDestroy` 继续对称清理 update、SceneView 绘制回调、临时 Mesh 和临时材质。
- 等待期间不会创建 Mesh 或 Material；Player 条件编译、运行时提示线生成、间距和随机纹理
  算法均未改变。

### 验证、风险与回滚

- 静态检查确认 bootstrap 注册/注销对称，Scene 临时状态只等待，终止状态才注销；当前工程
  使用正常 Domain Reload 与 Scene Reload。
- Unity 6000.0.59f2 已在源码更新后重新编译，`Assembly-CSharp.dll` 晚于源码，Editor.log
  未记录本次 C# 编译错误或目标 Missing/NullReference 异常。
- PlayMode 的真实 Renderer 已在 DLevel_002 首显和一次 HUD 重载后的 DLevel_004 中目测显示；
  Prefab Stage 临时卸载再恢复、组件禁用后重启、连续脚本重编译以及保存 Prefab 无新增
  override 尚未逐项人工复现。
- 极端情况下，一个非 persistent、保持 active、但永远不进入 loaded Scene 的临时对象会持续
  保留 editor update；正常对象会由 Scene 加载完成或 `OnDisable` / `OnDestroy` 结束该状态。
- Android/iOS Player 不包含该 editor bootstrap，移动端画面与性能仍待验证。
- 回滚本节时，可恢复 `EnableEditorAutoPreview()` 对 invalid/unloaded Scene 的早退，并让
  `TryBuildEditorPreviewWhenReady()` 在不可构建时注销；这只回退编辑器刷新韧性，不影响
  运行时 Mesh、原图长度、间距或随机纹理。
